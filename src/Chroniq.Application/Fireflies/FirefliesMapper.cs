using System.Text;
using System.Text.RegularExpressions;
using Chroniq.Application.Granola;

namespace Chroniq.Application.Fireflies;

/// <summary>
/// Converts a Fireflies call into the normalized note shape the rest of the pipeline works with
/// (ingestion, classification, Markdown export). Fireflies content is kept verbatim; only its
/// AI summary sections are arranged under headings.
/// </summary>
public static partial class FirefliesMapper
{
    public static GranolaNoteFetch ToFetch(FirefliesTranscript t)
    {
        var start = t.Date is { } ms ? DateTimeOffset.FromUnixTimeMilliseconds((long)ms) : (DateTimeOffset?)null;
        var end = start is { } s && t.Duration is > 0 ? s.AddMinutes(t.Duration.Value) : (DateTimeOffset?)null;

        var participantEmails = (t.Participants ?? [])
            .SelectMany(p => p.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(e => e.Contains('@'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var attendees = (t.MeetingAttendees ?? [])
            .Select(a => new GranolaUser(FirstNonEmpty(a.Name, a.DisplayName), NullIfEmpty(a.Email)))
            .Where(a => a.Name is not null || a.Email is not null)
            .ToList();
        // Calls without calendar attendee details still list participant emails.
        foreach (var email in participantEmails.Where(e => !attendees.Any(a => string.Equals(a.Email, e, StringComparison.OrdinalIgnoreCase))))
            attendees.Add(new GranolaUser(null, email));

        var owner = t.User is { } u && (NullIfEmpty(u.Name) ?? NullIfEmpty(u.Email)) is not null
            ? new GranolaUser(NullIfEmpty(u.Name), NullIfEmpty(u.Email))
            : NullIfEmpty(t.OrganizerEmail) is { } org ? new GranolaUser(null, org) : null;

        var note = new GranolaNote
        {
            Id = t.Id,
            Title = NullIfEmpty(t.Title),
            Owner = owner,
            // Fireflies has no "updated at"; the call time is the stable source timestamp.
            CreatedAt = start ?? DateTimeOffset.UnixEpoch,
            UpdatedAt = start ?? DateTimeOffset.UnixEpoch,
            WebUrl = NullIfEmpty(t.TranscriptUrl),
            CalendarEvent = new GranolaCalendarEvent
            {
                EventTitle = NullIfEmpty(t.Title),
                Invitees = participantEmails.Select(e => new GranolaCalendarInvitee(e)).ToList(),
                Organiser = NullIfEmpty(t.OrganizerEmail),
                CalendarEventId = NullIfEmpty(t.CalId),
                ScheduledStartTime = start,
                ScheduledEndTime = end,
            },
            Attendees = attendees,
            SummaryMarkdown = SummaryMarkdown(t.Summary),
        };

        var segments = (t.Sentences ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.Text ?? x.RawText))
            .OrderBy(x => x.Index ?? int.MaxValue)
            .Select(x => new GranolaTranscriptSegment
            {
                Speaker = Speaker(x.SpeakerName),
                Text = (x.Text ?? x.RawText)!.Trim(),
                StartTime = start is { } st && x.StartTime is { } off ? st.AddSeconds(off) : null,
                EndTime = start is { } st2 && x.EndTime is { } off2 ? st2.AddSeconds(off2) : null,
            })
            .ToList();

        return new GranolaNoteFetch(note, new GranolaTranscriptResult(true, segments));
    }

    /// <summary>
    /// Fireflies labels speakers it could not name "Speaker 1", "Speaker 2"... Those are kept as anonymous
    /// diarization labels so they are never treated as a person's identity during classification.
    /// </summary>
    public static GranolaSpeaker? Speaker(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var trimmed = name.Trim();
        return AnonymousSpeaker().IsMatch(trimmed)
            ? new GranolaSpeaker { DiarizationLabel = trimmed }
            : new GranolaSpeaker { Name = trimmed };
    }

    [GeneratedRegex(@"^(speaker|unknown speaker|participant)\s*\d*$", RegexOptions.IgnoreCase)]
    private static partial Regex AnonymousSpeaker();

    /// <summary>Fireflies' AI notes as Markdown, or null while Fireflies has not produced a summary yet.</summary>
    public static string? SummaryMarkdown(FirefliesSummary? s)
    {
        if (s is null) return null;
        var b = new StringBuilder();
        void Section(string heading, string? body)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            b.Append("## ").Append(heading).Append("\n\n");
            b.Append(body.Replace("\r\n", "\n").Trim()).Append("\n\n");
        }
        Section("Overview", s.Overview);
        Section("Notes", FirstNonEmpty(s.Notes, s.ShorthandBullet));
        Section("Action Items", s.ActionItems);
        Section("Outline", s.Outline);
        Section("Topics Discussed", List(s.TopicsDiscussed));
        if (s.Keywords is { Count: > 0 } k) Section("Keywords", string.Join(", ", k.Select(x => x.Trim()).Where(x => x.Length > 0)));
        if (!string.IsNullOrWhiteSpace(s.MeetingType)) Section("Meeting Type", s.MeetingType);
        var text = b.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static string? List(List<string>? items) =>
        items is { Count: > 0 } ? string.Join("\n", items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => "- " + i.Trim())) : null;

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? FirstNonEmpty(params string?[] values) => values.Select(NullIfEmpty).FirstOrDefault(v => v is not null);
}
