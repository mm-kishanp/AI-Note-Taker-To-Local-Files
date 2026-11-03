using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Chroniq.Application.Fireflies;
using Chroniq.Application.Granola;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Settings;
using Chroniq.Domain.Enums;
using Chroniq.Infrastructure.Granola;
using Microsoft.Extensions.Logging;

namespace Chroniq.Infrastructure.Fireflies;

/// <summary>
/// Fireflies allows 30 (Free) to 60 (Pro and above) requests per minute. Spacing requests just over
/// a second apart stays within the Pro limit; on Free, 429 responses and their Retry-After pace the rest.
/// </summary>
public sealed class FirefliesRequestThrottle : GranolaRequestThrottle
{
    public FirefliesRequestThrottle() => MinInterval = TimeSpan.FromMilliseconds(1100);
}

/// <summary>
/// Client for the official Fireflies.ai GraphQL API (https://api.fireflies.ai/graphql), read-only.
/// Verified operations: the <c>user</c>, <c>transcripts</c> and <c>transcript</c> queries. Bearer-token authentication.
/// </summary>
public sealed class FirefliesApiClient(
    HttpClient http,
    ISecretProvider secrets,
    ISettingsService settings,
    FirefliesRequestThrottle throttle,
    ILogger<FirefliesApiClient> logger) : IFirefliesApiClient
{
    public static readonly Uri Endpoint = new("https://api.fireflies.ai/graphql");
    public const int MaxPageSize = 50;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { NumberHandling = JsonNumberHandling.AllowReadingFromString };
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private int? _maxAttempts;

    private const string UserQuery = "query { user { name email } }";

    private const string ListQuery =
        "query Transcripts($fromDate: DateTime, $limit: Int, $skip: Int) { transcripts(fromDate: $fromDate, limit: $limit, skip: $skip) { id title date } }";

    private const string TranscriptQuery =
        "query Transcript($id: String!) { transcript(id: $id) { id title date duration transcript_url organizer_email cal_id participants " +
        "user { name email } meeting_attendees { displayName name email } " +
        "sentences { index text raw_text start_time end_time speaker_name } " +
        "summary { overview notes shorthand_bullet action_items outline keywords topics_discussed meeting_type } } }";

    public async Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken ct)
    {
        var secret = secrets.Get(SecretNames.FirefliesApiKey);
        if (secret is null)
            return new GranolaConnectionResult(false, ErrorCategory.Configuration, "No Fireflies API key is configured.", null);
        try
        {
            var data = await SendAsync<UserData>(UserQuery, null, ct);
            var who = data?.User?.Email ?? data?.User?.Name;
            var calls = await ListTranscriptsAsync(null, 0, 1, ct);
            var message = (who is null ? "Connected." : $"Connected as {who}.")
                + (calls.Count > 0 ? " The API key can read calls." : " No calls are visible to this key yet.");
            return new GranolaConnectionResult(true, ErrorCategory.None, message, secret.Source);
        }
        catch (GranolaApiException ex)
        {
            return new GranolaConnectionResult(false, ex.Category, ex.Message, secret.Source);
        }
    }

    public async Task<IReadOnlyList<FirefliesTranscriptSummary>> ListTranscriptsAsync(DateTimeOffset? fromDate, int skip, int limit, CancellationToken ct)
    {
        var variables = new Dictionary<string, object?>
        {
            ["limit"] = Math.Clamp(limit, 1, MaxPageSize),
            ["skip"] = Math.Max(0, skip),
            ["fromDate"] = fromDate?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        };
        var data = await SendAsync<TranscriptsData>(ListQuery, variables, ct)
            ?? throw new FirefliesApiException(ErrorCategory.InvalidResponse, 200, "Empty response when listing Fireflies calls.");
        if (data.Transcripts is null) throw new FirefliesApiException(ErrorCategory.InvalidResponse, 200, "Fireflies list response is missing 'transcripts'.");
        return data.Transcripts;
    }

    public async Task<FirefliesTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken ct)
    {
        ValidateId(transcriptId);
        try
        {
            var data = await SendAsync<TranscriptData>(TranscriptQuery, new Dictionary<string, object?> { ["id"] = transcriptId }, ct);
            return data?.Transcript;
        }
        catch (FirefliesApiException ex) when (ex.Category == ErrorCategory.NotFound)
        {
            return null;
        }
    }

    private async Task<T?> SendAsync<T>(string query, Dictionary<string, object?>? variables, CancellationToken ct) where T : class
    {
        var secret = secrets.Get(SecretNames.FirefliesApiKey)
            ?? throw new FirefliesApiException(ErrorCategory.Configuration, null, "No Fireflies API key is configured. Add one in Settings → Fireflies Connection.");
        _maxAttempts ??= Math.Clamp((await settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct)).MaxRequestAttempts, 1, 10);

        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            await throttle.WaitAsync(ct);
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = JsonContent.Create(new { query, variables }, options: Json),
            };
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
                    throw new FirefliesApiException(ErrorCategory.TransientApi, null, $"Could not reach Fireflies after {attempt} attempt(s): {ex.Message}", inner: ex);
                await DelayAsync(attempt, null, ct);
                continue;
            }

            using (response)
            {
                GraphQlResponse<T>? body;
                try
                {
                    var text = await response.Content.ReadAsStringAsync(ct);
                    body = string.IsNullOrWhiteSpace(text) ? null : JsonSerializer.Deserialize<GraphQlResponse<T>>(text, Json);
                }
                catch (JsonException ex)
                {
                    if (response.IsSuccessStatusCode)
                        throw new FirefliesApiException(ErrorCategory.InvalidResponse, (int)response.StatusCode, $"Fireflies returned an unexpected response: {ex.Message}", inner: ex);
                    body = null;
                }

                var error = body?.Errors?.FirstOrDefault();
                if (response.IsSuccessStatusCode && error is null) return body?.Data;

                var status = (int)response.StatusCode;
                var code = error?.Code ?? error?.Extensions?.Code;
                var category = Categorize(response.StatusCode, code, error?.Message);
                var message = Describe(category, status, code, error?.Message);

                var retryable = category is ErrorCategory.RateLimited or ErrorCategory.TransientApi;
                if (!retryable || attempt >= _maxAttempts)
                    throw new FirefliesApiException(category, status, message, code);

                var retryAfter = RetryAfter(response, error);
                if (category == ErrorCategory.RateLimited) throttle.Pause(retryAfter ?? TimeSpan.FromSeconds(10));
                logger.LogInformation("Fireflies returned {Status} {Code}; retrying (attempt {Attempt}/{Max})", status, code, attempt, _maxAttempts);
                await DelayAsync(attempt, retryAfter, ct);
            }
        }
    }

    public static ErrorCategory Categorize(HttpStatusCode status, string? code, string? message)
    {
        switch (code?.ToLowerInvariant())
        {
            case "auth_failed" or "invalid_api_key" or "unauthenticated" or "unauthorized" or "invalid_token":
                return ErrorCategory.Authentication;
            case "forbidden" or "paid_required" or "not_in_team" or "require_elevated_privilege" or "account_cancelled" or "feature_disabled":
                return ErrorCategory.Permission;
            case "object_not_found" or "not_found":
                return ErrorCategory.NotFound;
            case "too_many_requests":
                return ErrorCategory.RateLimited;
            case "request_timeout" or "invariant_violation":
                return ErrorCategory.TransientApi;
        }
        if (status == HttpStatusCode.OK || status == HttpStatusCode.BadRequest)
        {
            // GraphQL errors without a documented code: an invalid key is reported in the message.
            if (message is not null && (message.Contains("api key", StringComparison.OrdinalIgnoreCase) || message.Contains("authenticat", StringComparison.OrdinalIgnoreCase)))
                return ErrorCategory.Authentication;
            return ErrorCategory.InvalidResponse;
        }
        return GranolaApiClient.Categorize(status);
    }

    private static string Describe(ErrorCategory category, int status, string? code, string? message)
    {
        var detail = message is { Length: > 0 } ? $" ({(code is null ? "" : code + ": ")}{message})" : "";
        return category switch
        {
            ErrorCategory.Authentication => $"Fireflies rejected the API key. Check or replace it in Settings{detail}.",
            ErrorCategory.Permission => $"The Fireflies API key does not have permission for this request{detail}.",
            ErrorCategory.NotFound => $"Not found or not accessible with this Fireflies key{detail}.",
            ErrorCategory.RateLimited => $"Fireflies rate limit reached{detail}.",
            _ => $"Fireflies API error {status}{detail}.",
        };
    }

    private static TimeSpan? RetryAfter(HttpResponseMessage r, GraphQlError? error)
    {
        if (r.Headers.RetryAfter?.Delta is { } d) return d;
        if (r.Headers.RetryAfter?.Date is { } date) return date - DateTimeOffset.UtcNow is var w && w > TimeSpan.Zero ? w : TimeSpan.Zero;
        // The GraphQL error carries retryAfter as an epoch timestamp in milliseconds.
        if (error?.Extensions?.Metadata?.RetryAfter is { } epochMs)
        {
            var wait = DateTimeOffset.FromUnixTimeMilliseconds((long)epochMs) - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
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
            throw new FirefliesApiException(ErrorCategory.InvalidResponse, null, $"Invalid Fireflies transcript id '{id}'.");
    }

    private sealed record GraphQlResponse<T>
    {
        [JsonPropertyName("data")] public T? Data { get; init; }
        [JsonPropertyName("errors")] public List<GraphQlError>? Errors { get; init; }
    }

    private sealed record GraphQlError
    {
        [JsonPropertyName("message")] public string? Message { get; init; }
        [JsonPropertyName("code")] public string? Code { get; init; }
        [JsonPropertyName("extensions")] public GraphQlExtensions? Extensions { get; init; }
    }

    private sealed record GraphQlExtensions
    {
        [JsonPropertyName("code")] public string? Code { get; init; }
        [JsonPropertyName("metadata")] public GraphQlErrorMetadata? Metadata { get; init; }
    }

    private sealed record GraphQlErrorMetadata
    {
        [JsonPropertyName("retryAfter")] public double? RetryAfter { get; init; }
    }

    private sealed record UserData([property: JsonPropertyName("user")] FirefliesUser? User);

    private sealed record TranscriptsData([property: JsonPropertyName("transcripts")] List<FirefliesTranscriptSummary>? Transcripts);

    private sealed record TranscriptData([property: JsonPropertyName("transcript")] FirefliesTranscript? Transcript);
}
