using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chroniq.Application.Export;

/// <summary>
/// Maintains README.md (created once, never overwritten), PROJECT_CONTEXT.md (only the marked generated
/// section is rewritten; user text outside it is preserved) and the Unassigned ReviewQueue.md.
/// Content is derived only from exported metadata and sections of Granola's own summaries.
/// </summary>
public sealed partial class ProjectContextService(IAppDbContext db, IClock clock, ILogger<ProjectContextService> logger)
{
    public const string StartMarker = "<!-- chroniq:generated:start -->";
    public const string EndMarker = "<!-- chroniq:generated:end -->";
    // Markers written before the rename to CHRONIQ; upgraded to the current ones on the next rewrite.
    private const string LegacyStartMarker = "<!-- granola-pi:generated:start -->";
    private const string LegacyEndMarker = "<!-- granola-pi:generated:end -->";
    private const int MaxMeetingsListed = 200;
    private const int MaxMeetingsForExtracts = 30;

    public async Task UpdateProjectsAsync(IEnumerable<Guid> projectIds, ExportLayout layout, CancellationToken ct)
    {
        if (!layout.Settings.GenerateProjectContext) return;
        foreach (var id in projectIds.Distinct())
        {
            var project = await db.Projects.AsNoTracking().Include(p => p.Client).FirstOrDefaultAsync(p => p.Id == id, ct);
            if (project is null) continue;
            try
            {
                await UpdateProjectAsync(project, layout, ct);
            }
            catch (Exception ex) when (ex is ExportFileException or InvalidOperationException or IOException)
            {
                logger.LogWarning("Could not update context files for project {Project}: {Message}", project.Name, ex.Message);
            }
        }
    }

    private async Task UpdateProjectAsync(Project project, ExportLayout layout, CancellationToken ct)
    {
        var info = ProjectInfo.From(project);
        var projectDir = SafePath.Combine(layout.NotesRoot, layout.ProjectDirectory(info, clock.UtcNow));
        Directory.CreateDirectory(projectDir);

        var readme = Path.Combine(projectDir, "README.md");
        if (!File.Exists(readme))
            await AtomicFile.WriteAsync(readme, Readme(project), ct);

        var records = await db.ExportRecords.AsNoTracking()
            .Where(r => r.ProjectId == project.Id && r.Location == ExportLocation.Project && r.Status != ExportStatus.Superseded)
            .ToListAsync(ct);
        var meetingIds = records.Select(r => r.MeetingId).Distinct().ToList();
        var meetings = await db.Meetings.AsNoTracking()
            .Where(m => meetingIds.Contains(m.Id))
            .OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt)
            .Take(MaxMeetingsListed)
            .ToListAsync(ct);

        var generated = GeneratedSection(project, meetings, records, layout, projectDir);
        var path = Path.Combine(projectDir, "PROJECT_CONTEXT.md");
        var existing = await AtomicFile.TryReadAsync(path, ct);
        string updated;
        if (existing is null)
        {
            updated = ContextHeader(project) + generated;
        }
        else
        {
            var text = MarkdownRenderer.NormalizeNewlines(existing).Replace(LegacyStartMarker, StartMarker).Replace(LegacyEndMarker, EndMarker);
            var start = text.IndexOf(StartMarker, StringComparison.Ordinal);
            var end = text.IndexOf(EndMarker, StringComparison.Ordinal);
            if (start < 0 || end < start)
            {
                logger.LogInformation("PROJECT_CONTEXT.md for {Project} has no generated markers; leaving it untouched.", project.Name);
                return;
            }
            updated = text[..start] + generated.TrimEnd('\n') + text[(end + EndMarker.Length)..];
        }
        if (existing is not null && MarkdownRenderer.NormalizeNewlines(existing) == updated) return;
        await AtomicFile.WriteAsync(path, updated, ct);
    }

    public async Task UpdateReviewQueueAsync(ExportLayout layout, CancellationToken ct)
    {
        if (!layout.Settings.GenerateReviewQueue) return;
        var meetings = await db.Meetings.AsNoTracking()
            .Where(m => m.ClassificationStatus == ClassificationStatus.Unassigned || m.ClassificationStatus == ClassificationStatus.NeedsReview)
            .Where(m => m.SourceStatus == SourceStatus.Active)
            .OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt)
            .ToListAsync(ct);
        var projects = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        var path = layout.ReviewQueuePath();
        var dir = Path.GetDirectoryName(path)!;
        var b = new StringBuilder();
        b.AppendLine("# Review Queue").AppendLine();
        b.AppendLine("Meetings that could not be assigned to a project automatically. Assign them in the");
        b.AppendLine("CHRONIQ app (Unassigned page). This file is regenerated on every sync.").AppendLine();
        b.AppendLine($"**{meetings.Count}** meeting(s) waiting for review.").AppendLine();
        if (meetings.Count > 0)
        {
            b.AppendLine("| Date | Meeting | Participants | Status | Candidates | Reason |");
            b.AppendLine("|---|---|---|---|---|---|");
            foreach (var m in meetings)
            {
                var c = m.Classification();
                var candidates = c?.CandidateProjectIds.Select(id => projects.GetValueOrDefault(id, "?")).ToList() ?? [];
                var link = m.NotesExportPath is { } rel ? Link(m.Title, dir, Path.Combine(layout.NotesRoot, rel)) : Cell(m.Title);
                var people = string.Join(", ", m.Attendees().Select(a => a.Name ?? a.Email).Where(x => !string.IsNullOrWhiteSpace(x)).Take(6));
                b.AppendLine($"| {Date(m)} | {link} | {Cell(people)} | {m.ClassificationStatus} | {Cell(string.Join(", ", candidates))} | {Cell(m.ClassificationReason ?? "")} |");
            }
            b.AppendLine();
        }
        var content = b.ToString();
        var existing = await AtomicFile.TryReadAsync(path, ct);
        if (existing is not null && MarkdownRenderer.NormalizeNewlines(existing) == content) return;
        await AtomicFile.WriteAsync(path, content, ct);
    }

    private static string Readme(Project p)
    {
        var b = new StringBuilder();
        b.AppendLine($"# {p.Name}").AppendLine();
        if (!string.IsNullOrWhiteSpace(p.Description)) b.AppendLine(p.Description!.Trim()).AppendLine();
        if (p.Client is not null) b.AppendLine($"**Client:** {p.Client.Name}").AppendLine();
        b.AppendLine("This folder is maintained by CHRONIQ.").AppendLine();
        b.AppendLine("- `Notes/` — meeting notes and Granola summaries, one Markdown file per meeting.");
        b.AppendLine("- `Transcripts/` — full meeting transcripts, one Markdown file per meeting.");
        b.AppendLine("- `PROJECT_CONTEXT.md` — an index of meetings plus items recorded in Granola summaries, for coding agents.").AppendLine();
        b.AppendLine("This README is created once and never overwritten; edit it freely.");
        return b.ToString();
    }

    private static string ContextHeader(Project p)
    {
        var b = new StringBuilder();
        b.AppendLine($"# {p.Name} — Project Context").AppendLine();
        b.AppendLine("<!-- Add your own notes anywhere outside the generated section; they are preserved. -->").AppendLine();
        if (p.Client is not null) b.AppendLine($"- **Client:** {p.Client.Name}");
        if (!string.IsNullOrWhiteSpace(p.Description)) b.AppendLine($"- **Description:** {p.Description!.Trim()}");
        b.AppendLine();
        return b.ToString();
    }

    private static string GeneratedSection(Project project, List<Meeting> meetings, List<ExportRecord> records, ExportLayout layout, string projectDir)
    {
        var b = new StringBuilder();
        b.AppendLine(StartMarker);
        b.AppendLine("<!-- This section is regenerated automatically after each sync. Do not edit inside the markers. -->").AppendLine();
        b.AppendLine($"## Meetings ({meetings.Count})").AppendLine();
        if (meetings.Count == 0)
        {
            b.AppendLine("_No meetings exported for this project yet._").AppendLine();
        }
        else
        {
            b.AppendLine("| Date | Meeting | Participants | Files |");
            b.AppendLine("|---|---|---|---|");
            foreach (var m in meetings)
            {
                b.AppendLine($"| {Date(m)} | {Cell(m.Title)} | {Cell(People(m))} | {Files(m, records, layout, projectDir)} |");
            }
            b.AppendLine();
        }

        var counts = meetings.SelectMany(m => m.Attendees().Where(a => !string.Equals(a.Email, m.OwnerEmail, StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Name ?? a.Email ?? "").Distinct())
            .Where(n => n.Length > 0)
            .GroupBy(n => n).Select(g => (Name: g.Key, Count: g.Count()))
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).Take(15).ToList();
        if (counts.Count > 0)
        {
            b.AppendLine("## Recurring participants").AppendLine();
            foreach (var (name, count) in counts) b.AppendLine($"- {Cell(name)} — {count} meeting(s)");
            b.AppendLine();
        }

        var recent = meetings.Take(MaxMeetingsForExtracts).ToList();
        AppendExtracts(b, "Action items recorded in Granola summaries", ActionHeading(), recent, records, layout, projectDir);
        AppendExtracts(b, "Decisions recorded in Granola summaries", DecisionHeading(), recent, records, layout, projectDir);
        AppendExtracts(b, "Open questions recorded in Granola summaries", QuestionHeading(), recent, records, layout, projectDir);
        b.AppendLine(EndMarker);
        return b.ToString();
    }

    [GeneratedRegex(@"action items?|next steps?|to-?dos?|follow[- ]?ups?", RegexOptions.IgnoreCase)]
    private static partial Regex ActionHeading();

    [GeneratedRegex(@"decisions?|agreed|agreements?", RegexOptions.IgnoreCase)]
    private static partial Regex DecisionHeading();

    [GeneratedRegex(@"open questions?|questions|unresolved|open items?|risks?", RegexOptions.IgnoreCase)]
    private static partial Regex QuestionHeading();

    [GeneratedRegex(@"^\s*#{1,6}\s+(.*)$")]
    private static partial Regex AnyHeading();

    private static void AppendExtracts(StringBuilder b, string title, Regex heading, List<Meeting> meetings, List<ExportRecord> records, ExportLayout layout, string projectDir)
    {
        var sections = meetings
            .Select(m => (Meeting: m, Lines: ExtractSection(m.Summary, heading)))
            .Where(x => x.Lines.Count > 0)
            .ToList();
        if (sections.Count == 0) return;
        b.AppendLine($"## {title}").AppendLine();
        b.AppendLine("> Copied verbatim from sections of Granola's generated summaries. These are discussion records, not verified decisions or tasks.").AppendLine();
        foreach (var (m, lines) in sections)
        {
            b.AppendLine($"### {Date(m)} — {Cell(m.Title)} ({Files(m, records, layout, projectDir)})").AppendLine();
            foreach (var line in lines) b.AppendLine(line);
            b.AppendLine();
        }
    }

    /// <summary>Returns the lines under headings matching <paramref name="heading"/> in a markdown summary.</summary>
    public static List<string> ExtractSection(string? markdown, Regex heading)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(markdown)) return result;
        var inSection = false;
        foreach (var raw in MarkdownRenderer.NormalizeNewlines(markdown).Split('\n'))
        {
            var h = AnyHeading().Match(raw);
            if (h.Success)
            {
                inSection = heading.IsMatch(h.Groups[1].Value);
                continue;
            }
            if (inSection && raw.Trim().Length > 0) result.Add(raw.TrimEnd());
        }
        return result;
    }

    private static string Files(Meeting m, List<ExportRecord> records, ExportLayout layout, string projectDir)
    {
        var mine = records.Where(r => r.MeetingId == m.Id).ToList();
        var parts = new List<string>();
        foreach (var r in mine.OrderBy(r => r.Kind))
        {
            var abs = layout.AbsolutePath(r.Kind, r.RelativePath);
            parts.Add(Link(r.Kind == ExportKind.Notes ? "Notes" : "Transcript", projectDir, abs));
        }
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }

    private static string Link(string text, string fromDir, string toAbs)
    {
        if (!string.Equals(Path.GetPathRoot(fromDir), Path.GetPathRoot(toAbs), StringComparison.OrdinalIgnoreCase)) return Cell(text);
        return $"[{Cell(text)}](<{SafePath.ToPortable(Path.GetRelativePath(fromDir, toAbs))}>)";
    }

    /// <summary>Attendees plus speakers Granola identified in the transcript (some speakers are not calendar attendees).</summary>
    private static string People(Meeting m)
    {
        var names = m.Attendees().Select(a => a.Name ?? a.Email).Concat(m.Speakers().Select(s => s.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return names.Count > 8 ? string.Join(", ", names.Take(8)) + $" +{names.Count - 8}" : string.Join(", ", names);
    }

    private static string Date(Meeting m) => (m.MeetingStartTime ?? m.CreatedAt).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Cell(string s) => s.Replace("|", "\\|").Replace("\n", " ").Replace("<", "\\<").Replace(">", "\\>").Trim();
}
