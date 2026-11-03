using System.Text.Json;
using Chroniq.Application.Classification;
using Chroniq.Application.Export;
using Chroniq.Application.Granola;
using Chroniq.Domain.Entities;

namespace Chroniq.Application.Meetings;

public sealed record SpeakerMetadata(string Name, int Segments);

/// <summary>Typed access to the structured JSON columns stored on <see cref="Meeting"/>.</summary>
public static class MeetingJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try { return JsonSerializer.Deserialize<T>(json, Options); }
        catch (JsonException) { return default; }
    }

    public static List<GranolaUser> Attendees(this Meeting m) => Deserialize<List<GranolaUser>>(m.AttendeesJson) ?? [];
    public static List<string> ParticipantEmails(this Meeting m) => Deserialize<List<string>>(m.ParticipantEmailsJson) ?? [];
    public static List<SpeakerMetadata> Speakers(this Meeting m) => Deserialize<List<SpeakerMetadata>>(m.SpeakerMetadataJson) ?? [];
    public static GranolaCalendarEvent? CalendarEvent(this Meeting m) => Deserialize<GranolaCalendarEvent>(m.CalendarEventJson);
    public static List<GranolaFolder> SourceFolders(this Meeting m) => Deserialize<List<GranolaFolder>>(m.SourceFoldersJson) ?? [];
    public static List<GranolaAttachment> Attachments(this Meeting m) => Deserialize<List<GranolaAttachment>>(m.AttachmentsJson) ?? [];
    public static List<GranolaTranscriptSegment> Transcript(this Meeting m) => Deserialize<List<GranolaTranscriptSegment>>(m.TranscriptContent) ?? [];
    public static ClassificationResult? Classification(this Meeting m) => Deserialize<ClassificationResult>(m.ClassificationDetailJson);

    public static DateTimeOffset MeetingDate(this Meeting m) =>
        new(DateTime.SpecifyKind(m.MeetingStartTime ?? m.CreatedAt, DateTimeKind.Utc));

    public static MeetingFacts ToFacts(this Meeting m)
    {
        var cal = m.CalendarEvent();
        return new MeetingFacts
        {
            Title = m.Title,
            OwnerName = m.OwnerName,
            OwnerEmail = m.OwnerEmail,
            Attendees = m.Attendees().Select(a => new ParticipantFact(a.Name, a.Email)).ToList(),
            InviteeEmails = cal?.Invitees?.Select(i => i.Email).Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e!).ToList() ?? [],
            IdentifiedSpeakers = m.Speakers().ToDictionary(s => s.Name, s => s.Segments),
            FolderNames = m.SourceFolders().Select(f => f.Name).ToList(),
            FolderIds = m.SourceFolders().Select(f => f.Id).ToList(),
            SummaryText = m.Summary,
        };
    }

    public static MeetingDocument ToDocument(this Meeting m, bool includeTranscript)
    {
        var cal = m.CalendarEvent();
        var classification = m.Classification();
        return new MeetingDocument
        {
            Source = m.Source,
            GranolaNoteId = m.GranolaNoteId,
            Title = m.Title,
            MeetingDate = m.MeetingDate(),
            MeetingStart = m.MeetingStartTime is { } s ? new DateTimeOffset(DateTime.SpecifyKind(s, DateTimeKind.Utc)) : null,
            MeetingEnd = m.MeetingEndTime is { } e ? new DateTimeOffset(DateTime.SpecifyKind(e, DateTimeKind.Utc)) : null,
            CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt = new DateTimeOffset(DateTime.SpecifyKind(m.UpdatedAt, DateTimeKind.Utc)),
            OwnerName = m.OwnerName,
            OwnerEmail = m.OwnerEmail,
            Attendees = m.Attendees().Select(a => new ParticipantFact(a.Name, a.Email)).ToList(),
            CalendarTitle = cal?.EventTitle,
            SummaryMarkdown = m.Summary,
            NotesMarkdown = m.NotesContent,
            Transcript = includeTranscript ? m.Transcript() : [],
            Attachments = m.Attachments(),
            SourceUrl = m.SourceUrl,
            ClassificationStatus = m.ClassificationStatus,
            ClassificationMethod = m.AssignmentMethod,
            ClassificationRule = classification?.Tier is { } t ? ClassificationEngine.TierLabel(t) : null,
        };
    }
}
