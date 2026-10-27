using System.Security.Cryptography;
using System.Text;
using GranolaPI.Application.Granola;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using GranolaPI.Domain.Rules;

namespace GranolaPI.Application.Meetings;

/// <summary>Normalizes a Granola API note into the local <see cref="Meeting"/> record. Source content is kept verbatim.</summary>
public static class MeetingIngestor
{
    public static void Apply(Meeting m, GranolaNoteFetch fetch, DateTimeOffset now)
    {
        var n = fetch.Note;
        var cal = n.CalendarEvent;

        m.GranolaNoteId = n.Id;
        m.Title = FirstNonEmpty(n.Title, cal?.EventTitle) ?? "Untitled meeting";
        m.CreatedAt = n.CreatedAt.UtcDateTime;
        m.UpdatedAt = n.UpdatedAt.UtcDateTime;
        m.OwnerName = n.Owner?.Name;
        m.OwnerEmail = n.Owner?.Email;
        m.SourceUrl = n.WebUrl;
        m.SourceStatus = n.DeletedAt is null ? SourceStatus.Active : SourceStatus.Deleted;

        var attendees = n.Attendees ?? [];
        m.AttendeesJson = MeetingJson.Serialize(attendees);
        var emails = attendees.Select(a => a.Email)
            .Concat(cal?.Invitees?.Select(i => i.Email) ?? [])
            .Append(cal?.Organiser)
            .Select(TextNormalizer.Email)
            .Where(e => e.Length > 0)
            .Distinct()
            .ToList();
        m.ParticipantEmailsJson = MeetingJson.Serialize(emails);
        m.CalendarEventJson = MeetingJson.Serialize(cal);
        m.SourceFoldersJson = MeetingJson.Serialize(n.FolderMembership ?? []);
        m.SourceFolderId = n.FolderMembership?.FirstOrDefault()?.Id;
        m.AttachmentsJson = MeetingJson.Serialize(n.Attachments ?? []);

        m.Summary = FirstNonEmpty(n.SummaryMarkdown, n.SummaryText);
        m.NotesContent = FirstNonEmpty(n.PrivateNotesMarkdown, n.PrivateNotesText);
        m.NotesStatus = m.Summary is null && m.NotesContent is null ? ContentStatus.Empty : ContentStatus.Available;

        var transcript = fetch.Transcript;
        if (transcript is { Success: true })
        {
            var segments = transcript.Segments;
            m.TranscriptContent = MeetingJson.Serialize(segments);
            m.TranscriptSegmentCount = segments.Count;
            m.TranscriptStatus = segments.Count > 0 ? ContentStatus.Available : ContentStatus.Empty;
            m.SpeakerMetadataJson = MeetingJson.Serialize(segments
                .Where(s => !string.IsNullOrWhiteSpace(s.Speaker?.Name))
                .GroupBy(s => s.Speaker!.Name!.Trim())
                .Select(g => new SpeakerMetadata(g.Key, g.Count()))
                .OrderByDescending(s => s.Segments)
                .ToList());
            var times = segments.Where(s => s.StartTime.HasValue).ToList();
            m.MeetingStartTime = (cal?.ScheduledStartTime ?? times.FirstOrDefault()?.StartTime)?.UtcDateTime;
            m.MeetingEndTime = (cal?.ScheduledEndTime ?? segments.LastOrDefault(s => s.EndTime.HasValue)?.EndTime)?.UtcDateTime;
        }
        else
        {
            // Keep any previously stored transcript; mark the retrieval as failed so it is retried.
            m.TranscriptStatus = ContentStatus.Failed;
            m.MeetingStartTime = cal?.ScheduledStartTime?.UtcDateTime ?? m.MeetingStartTime;
            m.MeetingEndTime = cal?.ScheduledEndTime?.UtcDateTime ?? m.MeetingEndTime;
        }

        RebuildParticipants(m, attendees, cal);
        m.SourceContentHash = ComputeSourceHash(m);
        m.LastSuccessfulFetchAt = now.UtcDateTime;
        m.UpdatedLocallyAt = now.UtcDateTime;
    }

    private static void RebuildParticipants(Meeting m, List<GranolaUser> attendees, GranolaCalendarEvent? cal)
    {
        var owner = TextNormalizer.Email(m.OwnerEmail);
        var speakers = m.Speakers();
        var list = new List<MeetingParticipant>();
        foreach (var a in attendees)
        {
            var email = TextNormalizer.Email(a.Email);
            list.Add(new MeetingParticipant
            {
                MeetingId = m.Id,
                Name = a.Name,
                Email = email.Length > 0 ? email : null,
                IsOwner = email.Length > 0 && email == owner,
                IsAttendee = true,
                SpokenSegments = speakers.FirstOrDefault(s => TextNormalizer.Name(s.Name) == TextNormalizer.Name(a.Name))?.Segments ?? 0,
            });
        }
        foreach (var inv in cal?.Invitees ?? [])
        {
            var email = TextNormalizer.Email(inv.Email);
            if (email.Length == 0) continue;
            var existing = list.FirstOrDefault(p => p.Email == email);
            if (existing is not null) existing.IsInvitee = true;
            else list.Add(new MeetingParticipant { MeetingId = m.Id, Email = email, IsInvitee = true, IsOwner = email == owner });
        }
        foreach (var s in speakers.Where(s => !list.Any(p => TextNormalizer.Name(p.Name) == TextNormalizer.Name(s.Name))))
            list.Add(new MeetingParticipant { MeetingId = m.Id, Name = s.Name, SpokenSegments = s.Segments });

        m.Participants.Clear();
        m.Participants.AddRange(list);
    }

    /// <summary>Hash of the source fields that affect generated content (attachment URLs excluded: they are re-signed).</summary>
    public static string ComputeSourceHash(Meeting m)
    {
        var sb = new StringBuilder();
        sb.Append(m.Title).Append('\u001f')
          .Append(m.CreatedAt.ToString("O")).Append('\u001f')
          .Append(m.MeetingStartTime?.ToString("O")).Append('\u001f')
          .Append(m.MeetingEndTime?.ToString("O")).Append('\u001f')
          .Append(m.OwnerName).Append(m.OwnerEmail).Append('\u001f')
          .Append(m.AttendeesJson).Append('\u001f')
          .Append(m.CalendarEventJson).Append('\u001f')
          .Append(m.SourceFoldersJson).Append('\u001f')
          .Append(string.Join(",", m.Attachments().Select(a => a.Id))).Append('\u001f')
          .Append(m.Summary).Append('\u001f')
          .Append(m.NotesContent).Append('\u001f')
          .Append(m.TranscriptContent);
        return "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
