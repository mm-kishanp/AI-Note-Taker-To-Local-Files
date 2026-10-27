using System.Text.Json.Serialization;

namespace GranolaPI.Application.Granola;

// Models for the official Granola public API (https://public-api.granola.ai/v1), verified against
// https://docs.granola.ai/api-reference/openapi.json. Unknown fields are ignored; optional fields are nullable.

public sealed record GranolaUser(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("email")] string? Email);

public sealed record GranolaNoteSummary
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("owner")] public GranolaUser? Owner { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; init; }
    [JsonPropertyName("deleted_at")] public DateTimeOffset? DeletedAt { get; init; }
}

public sealed record GranolaCalendarInvitee([property: JsonPropertyName("email")] string? Email);

public sealed record GranolaCalendarEvent
{
    [JsonPropertyName("event_title")] public string? EventTitle { get; init; }
    [JsonPropertyName("invitees")] public List<GranolaCalendarInvitee>? Invitees { get; init; }
    [JsonPropertyName("organiser")] public string? Organiser { get; init; }
    [JsonPropertyName("calendar_event_id")] public string? CalendarEventId { get; init; }
    [JsonPropertyName("scheduled_start_time")] public DateTimeOffset? ScheduledStartTime { get; init; }
    [JsonPropertyName("scheduled_end_time")] public DateTimeOffset? ScheduledEndTime { get; init; }
}

public sealed record GranolaFolder
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("parent_folder_id")] public string? ParentFolderId { get; init; }
    [JsonPropertyName("space_id")] public string? SpaceId { get; init; }
}

public sealed record GranolaAttachment
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("type")] public string? Type { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("width")] public double? Width { get; init; }
    [JsonPropertyName("height")] public double? Height { get; init; }
}

public sealed record GranolaSpeaker
{
    /// <summary>"microphone" or "speaker". Clients must tolerate new values.</summary>
    [JsonPropertyName("source")] public string? Source { get; init; }
    /// <summary>"me" (note owner) or "them". Omitted when unknown.</summary>
    [JsonPropertyName("attribution")] public string? Attribution { get; init; }
    /// <summary>Anonymous per-transcript label such as "Speaker A". Not an identity.</summary>
    [JsonPropertyName("diarization_label")] public string? DiarizationLabel { get; init; }
    /// <summary>Resolved name, present only when Granola identified the speaker.</summary>
    [JsonPropertyName("name")] public string? Name { get; init; }
}

public sealed record GranolaTranscriptSegment
{
    [JsonPropertyName("speaker")] public GranolaSpeaker? Speaker { get; init; }
    [JsonPropertyName("text")] public string Text { get; init; } = "";
    [JsonPropertyName("start_time")] public DateTimeOffset? StartTime { get; init; }
    [JsonPropertyName("end_time")] public DateTimeOffset? EndTime { get; init; }
}

public sealed record GranolaNote
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("owner")] public GranolaUser? Owner { get; init; }
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset UpdatedAt { get; init; }
    [JsonPropertyName("deleted_at")] public DateTimeOffset? DeletedAt { get; init; }
    [JsonPropertyName("web_url")] public string? WebUrl { get; init; }
    [JsonPropertyName("calendar_event")] public GranolaCalendarEvent? CalendarEvent { get; init; }
    [JsonPropertyName("attendees")] public List<GranolaUser>? Attendees { get; init; }
    [JsonPropertyName("attachments")] public List<GranolaAttachment>? Attachments { get; init; }
    [JsonPropertyName("folder_membership")] public List<GranolaFolder>? FolderMembership { get; init; }
    [JsonPropertyName("summary_text")] public string? SummaryText { get; init; }
    [JsonPropertyName("summary_markdown")] public string? SummaryMarkdown { get; init; }
    [JsonPropertyName("private_notes_text")] public string? PrivateNotesText { get; init; }
    [JsonPropertyName("private_notes_markdown")] public string? PrivateNotesMarkdown { get; init; }
    /// <summary>Null when not requested or not returned inline.</summary>
    [JsonPropertyName("transcript")] public List<GranolaTranscriptSegment>? Transcript { get; init; }
}

public sealed record GranolaListNotesResponse
{
    [JsonPropertyName("notes")] public List<GranolaNoteSummary> Notes { get; init; } = [];
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
    [JsonPropertyName("cursor")] public string? Cursor { get; init; }
}

public sealed record GranolaTranscriptPage
{
    [JsonPropertyName("transcript")] public List<GranolaTranscriptSegment> Transcript { get; init; } = [];
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
    [JsonPropertyName("cursor")] public string? Cursor { get; init; }
}

public sealed record GranolaFolderPage
{
    [JsonPropertyName("folders")] public List<GranolaFolder> Folders { get; init; } = [];
    [JsonPropertyName("hasMore")] public bool HasMore { get; init; }
    [JsonPropertyName("cursor")] public string? Cursor { get; init; }
}

public sealed record GranolaErrorBody
{
    [JsonPropertyName("code")] public string? Code { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
    [JsonPropertyName("requestId")] public string? RequestId { get; init; }
}
