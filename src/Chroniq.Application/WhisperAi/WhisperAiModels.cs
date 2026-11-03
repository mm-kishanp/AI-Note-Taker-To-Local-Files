using System.Globalization;
using System.Text.Json.Serialization;
using Chroniq.Application.Granola;
using Chroniq.Domain.Enums;

namespace Chroniq.Application.WhisperAi;

// Models for the official WhisperAI REST API (https://api.whisperai.com/v1), verified against
// https://whisperai.com/whisperai-openapi.json. WhisperAI is a transcription service: transcripts carry
// no title, attendees or AI notes. Unknown fields are ignored; optional fields are nullable.

/// <summary>An entry of <c>GET /v1/transcript</c> (light fields only).</summary>
public sealed record WhisperAiTranscriptListItem
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    /// <summary>"queued", "processing", "completed" or "error".</summary>
    [JsonPropertyName("status")] public string? Status { get; init; }
    /// <summary>When the transcript job was created. Only the list endpoint returns it.</summary>
    [JsonPropertyName("created")] public string? Created { get; init; }
    [JsonPropertyName("completed")] public string? Completed { get; init; }
    [JsonPropertyName("audio_url")] public string? AudioUrl { get; init; }
    [JsonPropertyName("error_code")] public string? ErrorCode { get; init; }

    [JsonIgnore] public DateTimeOffset? CreatedAt => WhisperAiTime.Parse(Created);
}

public sealed record WhisperAiPageDetails
{
    [JsonPropertyName("limit")] public int? Limit { get; init; }
    [JsonPropertyName("result_count")] public int? ResultCount { get; init; }
    [JsonPropertyName("prev_url")] public string? PrevUrl { get; init; }
    [JsonPropertyName("next_url")] public string? NextUrl { get; init; }
}

public sealed record WhisperAiTranscriptList
{
    [JsonPropertyName("page_details")] public WhisperAiPageDetails? PageDetails { get; init; }
    [JsonPropertyName("transcripts")] public List<WhisperAiTranscriptListItem>? Transcripts { get; init; }

    /// <summary>Older transcripts exist when <c>next_url</c> is set.</summary>
    [JsonIgnore] public bool HasMore => !string.IsNullOrEmpty(PageDetails?.NextUrl);
}

/// <summary>
/// A speaker turn (<c>utterances</c>) or a sentence (<c>/sentences</c>). The OpenAPI schema leaves these
/// untyped; the documented example is <c>{ "speaker": "A", "text": "…", "start": 240, "end": 26560 }</c>.
/// </summary>
public sealed record WhisperAiUtterance
{
    /// <summary>Anonymous diarization label such as "A". Never a person's name.</summary>
    [JsonPropertyName("speaker")] public string? Speaker { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    /// <summary>Milliseconds from the start of the audio.</summary>
    [JsonPropertyName("start")] public double? Start { get; init; }
    [JsonPropertyName("end")] public double? End { get; init; }
}

public sealed record WhisperAiTranscript
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("audio_url")] public string? AudioUrl { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("language_code")] public string? LanguageCode { get; init; }
    /// <summary>Length of the audio in seconds.</summary>
    [JsonPropertyName("audio_duration")] public double? AudioDuration { get; init; }
    [JsonPropertyName("error")] public string? Error { get; init; }
    [JsonPropertyName("error_code")] public string? ErrorCode { get; init; }
    /// <summary>Speaker turns; only present when the transcript was created with <c>speaker_labels</c>.</summary>
    [JsonPropertyName("utterances")] public List<WhisperAiUtterance>? Utterances { get; init; }
    /// <summary>Filled from <c>/sentences</c> by the client when the transcript has no speaker turns.</summary>
    [JsonPropertyName("sentences")] public List<WhisperAiUtterance>? Sentences { get; init; }
}

public static class WhisperAiStatus
{
    public const string Completed = "completed";
    public const string Error = "error";
}

public interface IWhisperAiApiClient
{
    Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>One page of the account's transcripts, newest first, older than <paramref name="beforeId"/> when given (max 200).</summary>
    Task<WhisperAiTranscriptList> ListTranscriptsAsync(string? beforeId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the transcript, or null when it no longer exists / is inaccessible. A completed transcript
    /// without speaker turns comes back with its <c>sentences</c> filled in.
    /// </summary>
    Task<WhisperAiTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken cancellationToken);
}

/// <summary>A WhisperAI API failure. Derives from <see cref="GranolaApiException"/> so the sync pipeline treats every source alike.</summary>
public sealed class WhisperAiApiException(ErrorCategory category, int? statusCode, string message, string? apiCode = null, Exception? inner = null)
    : GranolaApiException(category, statusCode, message, apiCode, inner);

internal static class WhisperAiTime
{
    /// <summary>WhisperAI timestamps are ISO 8601; one without an offset is UTC.</summary>
    public static DateTimeOffset? Parse(string? value) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var d) ? d : null;
}
