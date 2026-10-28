using System.Text.Json;
using System.Text.Json.Serialization;
using GranolaPI.Application.Granola;
using GranolaPI.Domain.Enums;

namespace GranolaPI.Application.Fireflies;

// Models for the official Fireflies.ai GraphQL API (https://api.fireflies.ai/graphql), verified against
// https://docs.fireflies.ai/schema. Unknown fields are ignored; every field is nullable because
// Fireflies fills summaries and sentences in only after it has processed a call.

public sealed record FirefliesUser
{
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}

public sealed record FirefliesAttendee
{
    [JsonPropertyName("displayName")] public string? DisplayName { get; init; }
    [JsonPropertyName("name")] public string? Name { get; init; }
    [JsonPropertyName("email")] public string? Email { get; init; }
}

public sealed record FirefliesSentence
{
    [JsonPropertyName("index")] public int? Index { get; init; }
    [JsonPropertyName("text")] public string? Text { get; init; }
    [JsonPropertyName("raw_text")] public string? RawText { get; init; }
    /// <summary>Seconds from the start of the recording.</summary>
    [JsonPropertyName("start_time")] public double? StartTime { get; init; }
    [JsonPropertyName("end_time")] public double? EndTime { get; init; }
    [JsonPropertyName("speaker_name")] public string? SpeakerName { get; init; }
}

public sealed record FirefliesSummary
{
    [JsonPropertyName("overview")] public string? Overview { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
    [JsonPropertyName("shorthand_bullet")] public string? ShorthandBullet { get; init; }
    [JsonPropertyName("action_items")] public string? ActionItems { get; init; }
    [JsonPropertyName("outline")] public string? Outline { get; init; }
    [JsonPropertyName("keywords"), JsonConverter(typeof(StringOrListConverter))] public List<string>? Keywords { get; init; }
    [JsonPropertyName("topics_discussed"), JsonConverter(typeof(StringOrListConverter))] public List<string>? TopicsDiscussed { get; init; }
    [JsonPropertyName("meeting_type")] public string? MeetingType { get; init; }
}

/// <summary>An entry of the <c>transcripts</c> list query (light fields only).</summary>
public sealed record FirefliesTranscriptSummary
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string? Title { get; init; }
    /// <summary>Meeting start in UTC epoch milliseconds.</summary>
    [JsonPropertyName("date")] public double? Date { get; init; }
}

public sealed record FirefliesTranscript
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("date")] public double? Date { get; init; }
    /// <summary>Length of the recording in minutes.</summary>
    [JsonPropertyName("duration")] public double? Duration { get; init; }
    [JsonPropertyName("transcript_url")] public string? TranscriptUrl { get; init; }
    [JsonPropertyName("organizer_email")] public string? OrganizerEmail { get; init; }
    [JsonPropertyName("cal_id")] public string? CalId { get; init; }
    [JsonPropertyName("user")] public FirefliesUser? User { get; init; }
    /// <summary>Participant emails. Fireflies sometimes returns several comma-separated in one entry.</summary>
    [JsonPropertyName("participants")] public List<string>? Participants { get; init; }
    [JsonPropertyName("meeting_attendees")] public List<FirefliesAttendee>? MeetingAttendees { get; init; }
    [JsonPropertyName("sentences")] public List<FirefliesSentence>? Sentences { get; init; }
    [JsonPropertyName("summary")] public FirefliesSummary? Summary { get; init; }
}

public interface IFirefliesApiClient
{
    Task<GranolaConnectionResult> TestConnectionAsync(CancellationToken cancellationToken);

    /// <summary>One page of the calls visible to the key, newest first. Fireflies pages with limit/skip (max 50).</summary>
    Task<IReadOnlyList<FirefliesTranscriptSummary>> ListTranscriptsAsync(DateTimeOffset? fromDate, int skip, int limit, CancellationToken cancellationToken);

    /// <summary>Returns the call with its sentences and AI summary, or null when it no longer exists / is inaccessible.</summary>
    Task<FirefliesTranscript?> GetTranscriptAsync(string transcriptId, CancellationToken cancellationToken);
}

/// <summary>Reads a field documented as a string that Fireflies may also return as a list of strings.</summary>
public sealed class StringOrListConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                return [reader.GetString()!];
            case JsonTokenType.StartArray:
                var list = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String) list.Add(reader.GetString()!);
                    else reader.Skip();
                }
                return list;
            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, options);
}

/// <summary>A Fireflies API failure. Derives from <see cref="GranolaApiException"/> so the sync pipeline treats both sources alike.</summary>
public sealed class FirefliesApiException(ErrorCategory category, int? statusCode, string message, string? apiCode = null, Exception? inner = null)
    : GranolaApiException(category, statusCode, message, apiCode, inner);
