using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace GranolaPI.Infrastructure.Granola;

/// <summary>
/// Spaces requests to stay under Granola's documented limits (burst 25 per 5s, 5 requests/second
/// sustained) across every client instance in the process.
/// </summary>
public class GranolaRequestThrottle
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _next = DateTimeOffset.MinValue;
    public TimeSpan MinInterval { get; init; } = TimeSpan.FromMilliseconds(220);

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var wait = _next - now;
            if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
            _next = DateTimeOffset.UtcNow + MinInterval;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>After a 429, hold every request until the server's Retry-After has passed.</summary>
    public void Pause(TimeSpan delay)
    {
        var until = DateTimeOffset.UtcNow + delay;
        if (until > _next) _next = until;
    }
}

/// <summary>
/// Client for the official Granola public API (https://public-api.granola.ai/v1).
/// Verified operations: GET /v1/notes, GET /v1/notes/{id}[?include=transcript],
/// GET /v1/notes/{id}/transcript (paged) and GET /v1/folders. Bearer-token authentication.
/// </summary>
public sealed class GranolaApiClient(
    HttpClient http,
    ISecretProvider secrets,
    ISettingsService settings,
    GranolaRequestThrottle throttle,
    ILogger<GranolaApiClient> logger) : IGranolaApiClient
{
    public static readonly Uri BaseAddress = new("https://public-api.granola.ai/v1/");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private int? _maxAttempts;

    public async Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct)
    {
        var secret = secrets.Get(SecretNames.GranolaApiKey);
        if (secret is null)
            return new GranolaConnectionResult(false, ErrorCategory.Configuration, "No Granola API key is configured.", null);
        try
        {
            var page = await ListNotesAsync(new NoteListQuery(PageSize: 1), ct);
            var message = page.Items.Count > 0
                ? "Connected. The API key can read notes."
                : "Connected, but no notes are visible to this key yet (only notes with a generated summary and transcript are returned).";
            return new GranolaConnectionResult(true, ErrorCategory.None, message, secret.Source);
        }
        catch (GranolaApiException ex)
        {
            return new GranolaConnectionResult(false, ex.Category, ex.Message, secret.Source);
        }
    }

    public async Task<PagedResult<GranolaNoteSummary>> ListNotesAsync(NoteListQuery query, CancellationToken ct)
    {
        var qs = new List<string> { $"page_size={Math.Clamp(query.PageSize, 1, 30)}" };
        if (query.CreatedAfter is { } ca) qs.Add("created_after=" + Uri.EscapeDataString(ca.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        if (query.CreatedBefore is { } cb) qs.Add("created_before=" + Uri.EscapeDataString(cb.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        if (query.UpdatedAfter is { } ua) qs.Add("updated_after=" + Uri.EscapeDataString(ua.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")));
        if (!string.IsNullOrEmpty(query.Cursor)) qs.Add("cursor=" + Uri.EscapeDataString(query.Cursor));
        var body = await SendAsync<GranolaListNotesResponse>("notes?" + string.Join('&', qs), ct)
            ?? throw new GranolaApiException(ErrorCategory.InvalidResponse, 200, "Empty response when listing notes.");
        if (body.Notes is null) throw new GranolaApiException(ErrorCategory.InvalidResponse, 200, "List notes response is missing 'notes'.");
        return new PagedResult<GranolaNoteSummary>(body.Notes, body.HasMore, body.Cursor);
    }

    public async Task<GranolaNoteFetch?> GetNoteAsync(string noteId, bool includeTranscript, CancellationToken ct)
    {
        ValidateNoteId(noteId);
        if (!includeTranscript)
        {
            var plain = await GetOrNullAsync<GranolaNote>($"notes/{noteId}", ct);
            return plain is null ? null : new GranolaNoteFetch(plain, null);
        }
        try
        {
            var note = await GetOrNullAsync<GranolaNote>($"notes/{noteId}?include=transcript", ct);
            if (note is null) return null;
            if (note.Transcript is not null)
                return new GranolaNoteFetch(note, new GranolaTranscriptResult(true, note.Transcript));
            // Transcript not inlined: fall back to the dedicated endpoint.
            return new GranolaNoteFetch(note, await GetTranscriptAsync(noteId, ct));
        }
        catch (GranolaApiException ex) when (ex.StatusCode == 413 || ex.ApiCode == "TRANSCRIPT_TOO_LARGE")
        {
            logger.LogInformation("Transcript for {NoteId} too large to inline; fetching in pages.", noteId);
            var note = await GetOrNullAsync<GranolaNote>($"notes/{noteId}", ct);
            if (note is null) return null;
            return new GranolaNoteFetch(note, await GetTranscriptAsync(noteId, ct));
        }
    }

    public async Task<GranolaTranscriptResult> GetTranscriptAsync(string noteId, CancellationToken ct)
    {
        ValidateNoteId(noteId);
        var all = new List<GranolaTranscriptSegment>();
        string? cursor = null;
        try
        {
            do
            {
                var url = $"notes/{noteId}/transcript?page_size=100" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor));
                var page = await SendAsync<GranolaTranscriptPage>(url, ct)
                    ?? throw new GranolaApiException(ErrorCategory.InvalidResponse, 200, "Empty transcript page.");
                all.AddRange(page.Transcript ?? []);
                cursor = page.HasMore ? page.Cursor : null;
                if (page.HasMore && string.IsNullOrEmpty(page.Cursor))
                    throw new GranolaApiException(ErrorCategory.InvalidResponse, 200, "Transcript reported more pages but returned no cursor.");
            } while (cursor is not null);
            return new GranolaTranscriptResult(true, all);
        }
        catch (GranolaApiException ex) when (ex.Category is not (ErrorCategory.Authentication or ErrorCategory.Permission))
        {
            // Report failure explicitly: a failed retrieval must never look like an empty transcript.
            return new GranolaTranscriptResult(false, [], ex.Category == ErrorCategory.NotFound ? ErrorCategory.TranscriptUnavailable : ex.Category, ex.Message);
        }
    }

    public async Task<IReadOnlyList<GranolaFolder>> ListFoldersAsync(CancellationToken ct)
    {
        var all = new List<GranolaFolder>();
        string? cursor = null;
        do
        {
            var page = await SendAsync<GranolaFolderPage>("folders?page_size=30" + (cursor is null ? "" : "&cursor=" + Uri.EscapeDataString(cursor)), ct)
                ?? throw new GranolaApiException(ErrorCategory.InvalidResponse, 200, "Empty folders response.");
            all.AddRange(page.Folders ?? []);
            cursor = page.HasMore ? page.Cursor : null;
        } while (cursor is not null);
        return all;
    }

    private async Task<T?> GetOrNullAsync<T>(string path, CancellationToken ct) where T : class
    {
        try
        {
            return await SendAsync<T>(path, ct);
        }
        catch (GranolaApiException ex) when (ex.Category == ErrorCategory.NotFound)
        {
            return null;
        }
    }

    private async Task<T?> SendAsync<T>(string relative, CancellationToken ct) where T : class
    {
        var secret = secrets.Get(SecretNames.GranolaApiKey)
            ?? throw new GranolaApiException(ErrorCategory.Configuration, null, "No Granola API key is configured. Add one in Settings → Granola Connection.");
        _maxAttempts ??= Math.Clamp((await settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct)).MaxRequestAttempts, 1, 10);

        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await throttle.WaitAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, relative));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret.Value);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= _maxAttempts)
                    throw new GranolaApiException(ErrorCategory.TransientApi, null, $"Could not reach Granola after {attempt} attempt(s): {ex.Message}", inner: ex);
                await DelayAsync(attempt, null, ct);
                continue;
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return await response.Content.ReadFromJsonAsync<T>(Json, ct);
                    }
                    catch (JsonException ex)
                    {
                        throw new GranolaApiException(ErrorCategory.InvalidResponse, (int)response.StatusCode, $"Granola returned an unexpected response: {ex.Message}", inner: ex);
                    }
                }

                var error = await ReadErrorAsync(response, ct);
                var status = (int)response.StatusCode;
                var category = Categorize(response.StatusCode);
                var message = Describe(response.StatusCode, error);

                var retryable = category is ErrorCategory.RateLimited or ErrorCategory.TransientApi;
                if (!retryable || attempt >= _maxAttempts)
                    throw new GranolaApiException(category, status, message, error?.Code);

                var retryAfter = RetryAfter(response);
                if (category == ErrorCategory.RateLimited) throttle.Pause(retryAfter ?? TimeSpan.FromSeconds(5));
                logger.LogInformation("Granola returned {Status}; retrying (attempt {Attempt}/{Max})", status, attempt, _maxAttempts);
                await DelayAsync(attempt, retryAfter, ct);
            }
        }
    }

    private static async Task DelayAsync(int attempt, TimeSpan? retryAfter, CancellationToken ct)
    {
        if (retryAfter is { } ra)
        {
            await Task.Delay(ra > MaxBackoff ? MaxBackoff : ra, ct);
            return;
        }
        // Exponential backoff with full jitter: random(0, 0.5s * 2^attempt), capped.
        var cap = Math.Min(MaxBackoff.TotalMilliseconds, 500 * Math.Pow(2, attempt));
        await Task.Delay(TimeSpan.FromMilliseconds(250 + Random.Shared.NextDouble() * cap), ct);
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage r)
    {
        if (r.Headers.RetryAfter?.Delta is { } d) return d;
        if (r.Headers.RetryAfter?.Date is { } date) return date - DateTimeOffset.UtcNow is var w && w > TimeSpan.Zero ? w : TimeSpan.Zero;
        return null;
    }

    private static async Task<GranolaErrorBody?> ReadErrorAsync(HttpResponseMessage r, CancellationToken ct)
    {
        try
        {
            var text = await r.Content.ReadAsStringAsync(ct);
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<GranolaErrorBody>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ErrorCategory Categorize(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => ErrorCategory.Authentication,
        HttpStatusCode.Forbidden => ErrorCategory.Permission,
        HttpStatusCode.NotFound => ErrorCategory.NotFound,
        HttpStatusCode.TooManyRequests => ErrorCategory.RateLimited,
        HttpStatusCode.RequestEntityTooLarge => ErrorCategory.InvalidResponse,
        HttpStatusCode.RequestTimeout => ErrorCategory.TransientApi,
        >= HttpStatusCode.InternalServerError => ErrorCategory.TransientApi,
        _ => ErrorCategory.InvalidResponse,
    };

    private static string Describe(HttpStatusCode status, GranolaErrorBody? error)
    {
        var detail = error?.Message is { Length: > 0 } m ? $" ({error.Code}: {m})" : "";
        var reference = error?.RequestId is { Length: > 0 } id ? $" [request {id}]" : "";
        return status switch
        {
            HttpStatusCode.Unauthorized => $"Granola rejected the API key. Check or replace it in Settings{detail}.{reference}",
            HttpStatusCode.Forbidden => $"The API key does not have permission for this resource. Check the key's note access scopes in Granola{detail}.{reference}",
            HttpStatusCode.NotFound => $"Not found or not accessible with this key{detail}.{reference}",
            HttpStatusCode.TooManyRequests => $"Granola rate limit reached{detail}.{reference}",
            HttpStatusCode.RequestEntityTooLarge => $"Transcript too large to return inline{detail}.{reference}",
            _ => $"Granola API error {(int)status}{detail}.{reference}",
        };
    }

    private static void ValidateNoteId(string noteId)
    {
        if (string.IsNullOrWhiteSpace(noteId) || noteId.Length > 64 || !noteId.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
            throw new GranolaApiException(ErrorCategory.InvalidResponse, null, $"Invalid note id '{noteId}'.");
    }
}
