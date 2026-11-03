using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chroniq.Application.Granola;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Settings;
using Chroniq.Application.WhisperAi;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Granola;
using Microsoft.Extensions.Logging;

namespace Chroniq.Infrastructure.WhisperAi;

/// <summary>
/// WhisperAI documents no request rate limit, so requests use the default spacing;
/// 429 responses and their Retry-After pace the rest.
/// </summary>
public sealed class WhisperAiRequestThrottle : GranolaRequestThrottle;

/// <summary>
/// Client for the official WhisperAI REST API (https://api.whisperai.com/v1), read-only.
/// Verified operations: <c>GET /transcript</c>, <c>GET /transcript/{id}</c> and <c>GET /transcript/{id}/sentences</c>.
/// The API key is sent as the raw <c>Authorization</c> header value (no "Bearer" scheme).
/// </summary>
public sealed class WhisperAiApiClient(
    HttpClient http,
    ISecretProvider secrets,
    ISettingsService settings,
    WhisperAiRequestThrottle throttle,
    ILogger<WhisperAiApiClient> logger) : IWhisperAiApiClient
{
    public static readonly Uri BaseUri = new("https://api.whisperai.com/v1/");
    public const int MaxPageSize = 200;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.AllowReadingFromString };
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private int? _maxAttempts;

    public async Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct)
    {
        var secret = secrets.Get(SecretNames.WhisperAiApiKey);
        if (secret is null)
            return new GranolaConnectionResult(false, ErrorCategory.Configuration, "No WhisperAI API key is configured.", null);
        try
        {
            var page = await ListTranscriptsAsync(null, 1, ct);
            var message = page.Transcripts is { Count: > 0 }
                ? "Connected. The API key can read transcripts."
                : "Connected. No transcripts are on this account yet.";
            return new GranolaConnectionResult(true, ErrorCategory.None, message, secret.Source);
        }
        catch (GranolaApiException ex)
        {
            return new GranolaConnectionResult(false, ex.Category, ex.Message, secret.Source);
        }
    }

    public async Task<WhisperAiTranscriptList> ListTranscriptsAsync(string? beforeId, int limit, CancellationToken ct)
    {
        var query = $"transcript?limit={Math.Clamp(limit, 1, MaxPageSize)}";
        if (beforeId is not null)
        {
            ValidateId(beforeId);
            query += "&before_id=" + Uri.EscapeDataString(beforeId);
        }
        var list = await GetAsync<WhisperAiTranscriptList>(query, ct)
            ?? throw new WhisperAiApiException(ErrorCategory.InvalidResponse, 200, "Empty response when listing WhisperAI transcripts.");
        if (list.Transcripts is null) throw new WhisperAiApiException(ErrorCategory.InvalidResponse, 200, "WhisperAI list response is missing 'transcripts'.");
        return list;
    }

    public async Task<WhisperAiTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken ct)
    {
        ValidateId(transcriptId);
        var path = "transcript/" + Uri.EscapeDataString(transcriptId);
        WhisperAiTranscript? transcript;
        try
        {
            transcript = await GetAsync<WhisperAiTranscript>(path, ct);
        }
        catch (WhisperAiApiException ex) when (ex.Category == ErrorCategory.NotFound)
        {
            return null;
        }
        if (transcript is null || transcript.Status != WhisperAiStatus.Completed || transcript.Utterances is { Count: > 0 })
            return transcript;

        // Without speaker_labels there are no utterances; sentences still carry timestamps.
        try
        {
            var sentences = await GetAsync<SentencesResponse>(path + "/sentences", ct);
            return transcript with { Sentences = sentences?.Sentences };
        }
        catch (WhisperAiApiException ex) when (ex.Category is ErrorCategory.NotFound or ErrorCategory.InvalidResponse)
        {
            logger.LogInformation("WhisperAI sentences unavailable for {TranscriptId} ({Category}); using the plain text", transcriptId, ex.Category);
            return transcript;
        }
    }

    private async Task<T?> GetAsync<T>(string relativePath, CancellationToken ct) where T : class
    {
        var secret = secrets.Get(SecretNames.WhisperAiApiKey)
            ?? throw new WhisperAiApiException(ErrorCategory.Configuration, null, "No WhisperAI API key is configured. Add one in Settings → WhisperAI Connection.");
        _maxAttempts ??= Math.Clamp((await settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct)).MaxRequestAttempts, 1, 10);
        var uri = new Uri(BaseUri, relativePath);

        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await throttle.WaitAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("Authorization", secret.Value);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                if (attempt >= _maxAttempts)
                    throw new WhisperAiApiException(ErrorCategory.TransientApi, null, $"Could not reach WhisperAI after {attempt} attempt(s): {ex.Message}", inner: ex);
                await DelayAsync(attempt, null, ct);
                continue;
            }

            using (response)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<T>(text, Json);
                    }
                    catch (JsonException ex)
                    {
                        throw new WhisperAiApiException(ErrorCategory.InvalidResponse, (int)response.StatusCode, $"WhisperAI returned an unexpected response: {ex.Message}", inner: ex);
                    }
                }

                var error = ReadError(text);
                var status = (int)response.StatusCode;
                var category = Categorize(response.StatusCode);
                var message = Describe(category, status, error);

                var retryable = category is ErrorCategory.RateLimited or ErrorCategory.TransientApi;
                if (!retryable || attempt >= _maxAttempts)
                    throw new WhisperAiApiException(category, status, message, error?.ErrorCode);

                var retryAfter = RetryAfter(response);
                if (category == ErrorCategory.RateLimited) throttle.Pause(retryAfter ?? TimeSpan.FromSeconds(10));
                logger.LogInformation("WhisperAI returned {Status} {Code}; retrying (attempt {Attempt}/{Max})", status, error?.ErrorCode, attempt, _maxAttempts);
                await DelayAsync(attempt, retryAfter, ct);
            }
        }
    }

    public static ErrorCategory Categorize(HttpStatusCode status) => status switch
    {
        // Monthly usage quota exceeded: nothing succeeds until the quota resets or the plan changes.
        HttpStatusCode.PaymentRequired => ErrorCategory.Permission,
        // The transcript isn't ready for this action yet.
        HttpStatusCode.Conflict => ErrorCategory.TransientApi,
        _ => GranolaApiClient.Categorize(status),
    };

    private static string Describe(ErrorCategory category, int status, ErrorBody? error)
    {
        var detail = error?.Error is { Length: > 0 } m ? $" ({(error.ErrorCode is null ? "" : error.ErrorCode + ": ")}{m})" : "";
        return category switch
        {
            ErrorCategory.Authentication => $"WhisperAI rejected the API key. Check or replace it in Settings{detail}.",
            ErrorCategory.Permission when status == (int)HttpStatusCode.PaymentRequired => $"WhisperAI monthly usage quota exceeded{detail}.",
            ErrorCategory.Permission => $"The WhisperAI API key does not have permission for this request{detail}.",
            ErrorCategory.NotFound => $"Not found or not accessible with this WhisperAI key{detail}.",
            ErrorCategory.RateLimited => $"WhisperAI rate limit reached{detail}.",
            _ => $"WhisperAI API error {status}{detail}.",
        };
    }

    private static ErrorBody? ReadError(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<ErrorBody>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage r)
    {
        if (r.Headers.RetryAfter?.Delta is { } d) return d;
        if (r.Headers.RetryAfter?.Date is { } date) return date - DateTimeOffset.UtcNow is var w && w > TimeSpan.Zero ? w : TimeSpan.Zero;
        return null;
    }

    private static async Task DelayAsync(int attempt, TimeSpan? retryAfter, CancellationToken ct)
    {
        if (retryAfter is { } ra)
        {
            await Task.Delay(ra > MaxBackoff ? MaxBackoff : ra, ct);
            return;
        }
        var cap = Math.Min(MaxBackoff.TotalMilliseconds, 500 * Math.Pow(2, attempt));
        await Task.Delay(TimeSpan.FromMilliseconds(250 + Random.Shared.NextDouble() * cap), ct);
    }

    private static void ValidateId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || !id.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            throw new WhisperAiApiException(ErrorCategory.InvalidResponse, null, $"Invalid WhisperAI transcript id '{id}'.");
    }

    private sealed record ErrorBody
    {
        [JsonPropertyName("error")] public string? Error { get; init; }
        [JsonPropertyName("error_code")] public string? ErrorCode { get; init; }
    }

    private sealed record SentencesResponse([property: JsonPropertyName("sentences")] List<WhisperAiUtterance>? Sentences);
}
