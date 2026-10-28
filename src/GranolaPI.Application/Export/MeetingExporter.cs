using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GranolaPI.Application.Export;

public enum ExportMode
{
    /// <summary>Write new/changed content; never overwrite user edits; don't recreate files the user removed.</summary>
    Normal,
    /// <summary>Like Normal, but recreate missing files.</summary>
    Reconcile,
    /// <summary>Regenerate even over user edits (explicit user action only).</summary>
    Force,
}

public sealed record ExportError(ExportKind Kind, ErrorCategory Category, string Message);

public sealed class ExportOutcome
{
    public int NotesWritten { get; set; }
    public int TranscriptsWritten { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesMoved { get; set; }
    public int Conflicts { get; set; }
    public List<ExportError> Errors { get; } = [];
    public bool HasFailures => Errors.Count > 0;
}

/// <summary>
/// Writes a meeting's notes and transcript files. Recoverable by design: the record is updated only
/// after the file is safely on disk, notes and transcript are tracked separately, and a failed step
/// leaves the meeting flagged <see cref="Meeting.ExportPending"/> for the next run.
/// </summary>
public sealed class MeetingExporter(IAppDbContext db, IClock clock, ILogger<MeetingExporter> logger)
{
    private sealed record Target(ExportLocation Location, ProjectInfo? Project, ExportRole Role);

    public async Task<ExportOutcome> ExportAsync(
        Meeting meeting,
        ExportLayout layout,
        IReadOnlyDictionary<Guid, ProjectInfo> projects,
        ExportMode mode,
        CancellationToken ct)
    {
        var outcome = new ExportOutcome();
        var s = layout.Settings;
        var targets = Targets(meeting, projects);
        var now = clock.UtcNow;

        if (meeting.SourceStatus != SourceStatus.Active)
        {
            // Deleted or inaccessible at the source: keep whatever was exported, write nothing new.
            meeting.ExportPending = false;
            return outcome;
        }

        var noUsableContent = string.IsNullOrWhiteSpace(meeting.Summary) && string.IsNullOrWhiteSpace(meeting.NotesContent)
            && meeting.TranscriptStatus is ContentStatus.Empty;

        var canonical = targets.FirstOrDefault(t => t.Role == ExportRole.Primary)?.Project?.Name;
        var baseDoc = meeting.ToDocument(includeTranscript: false);
        var transcriptDoc = meeting.TranscriptStatus == ContentStatus.Available ? meeting.ToDocument(includeTranscript: true) : null;

        foreach (var target in targets)
        {
            var notesRec = s.ExportNotes ? await RecordFor(meeting, ExportKind.Notes, target, layout, ct) : null;
            var transRec = s.ExportTranscripts ? await RecordFor(meeting, ExportKind.Transcript, target, layout, ct) : null;

            var doc = baseDoc with
            {
                ProjectName = target.Project?.Name,
                ProjectSlug = target.Project?.Slug,
                ProjectId = target.Project?.Id,
                ClientName = target.Project?.ClientName,
                Location = target.Location,
                Role = target.Role,
                CanonicalProject = target.Role == ExportRole.Copy ? canonical : null,
                IncludeAttachmentLinks = s.IncludeAttachmentLinks,
            };

            var transcriptWillExist = transRec is not null && transcriptDoc is not null;
            if (notesRec is not null && transcriptWillExist)
                doc = doc with { TranscriptLink = RelativeLink(layout, ExportKind.Notes, notesRec.RelativePath, ExportKind.Transcript, transRec!.RelativePath) };

            if (notesRec is not null)
            {
                if (noUsableContent)
                {
                    SetStatus(notesRec, ExportStatus.Skipped, "Granola returned no usable content for this meeting.");
                }
                else
                {
                    var content = MarkdownRenderer.RenderNotes(doc, now);
                    await WriteAsync(meeting, notesRec, layout, content, mode, outcome, ct);
                }
            }

            if (transRec is not null)
            {
                switch (meeting.TranscriptStatus)
                {
                    case ContentStatus.Available:
                        var tdoc = transcriptDoc! with
                        {
                            ProjectName = doc.ProjectName,
                            ProjectSlug = doc.ProjectSlug,
                            ProjectId = doc.ProjectId,
                            ClientName = doc.ClientName,
                            Location = doc.Location,
                            Role = doc.Role,
                            CanonicalProject = doc.CanonicalProject,
                            NotesLink = notesRec is not null && notesRec.Status != ExportStatus.Skipped
                                ? RelativeLink(layout, ExportKind.Transcript, transRec.RelativePath, ExportKind.Notes, notesRec.RelativePath)
                                : null,
                        };
                        await WriteAsync(meeting, transRec, layout, MarkdownRenderer.RenderTranscript(tdoc, now), mode, outcome, ct);
                        break;
                    case ContentStatus.Failed:
                        // Never export a failed retrieval as an empty transcript.
                        SetStatus(transRec, ExportStatus.Failed, meeting.LastSyncError ?? "Transcript retrieval failed; will retry.");
                        outcome.Errors.Add(new ExportError(ExportKind.Transcript, ErrorCategory.TranscriptUnavailable, "Transcript retrieval failed."));
                        break;
                    case ContentStatus.Empty:
                        SetStatus(transRec, ExportStatus.Skipped, "Granola returned no transcript for this meeting.");
                        break;
                    default:
                        SetStatus(transRec, ExportStatus.Pending, "Transcript not yet available.");
                        break;
                }
            }
        }

        await SupersedeStaleRecords(meeting, targets, layout, ct);
        UpdateAggregate(meeting, targets, now);
        return outcome;
    }

    private static List<Target> Targets(Meeting meeting, IReadOnlyDictionary<Guid, ProjectInfo> projects)
    {
        switch (meeting.ClassificationStatus)
        {
            case ClassificationStatus.Assigned when meeting.AssignedProjectId is Guid pid && projects.TryGetValue(pid, out var p):
                return [new Target(ExportLocation.Project, p, ExportRole.Primary)];
            case ClassificationStatus.MultiProject:
                var assigned = meeting.ProjectAssignments.OrderByDescending(a => a.IsPrimary)
                    .Where(a => projects.ContainsKey(a.ProjectId)).ToList();
                if (assigned.Count == 0) goto default;
                return assigned.Select((a, i) => new Target(ExportLocation.Project, projects[a.ProjectId], i == 0 ? ExportRole.Primary : ExportRole.Copy)).ToList();
            case ClassificationStatus.Shared:
                return [new Target(ExportLocation.Shared, null, ExportRole.Primary)];
            case ClassificationStatus.Excluded:
                return [];
            default:
                return [new Target(ExportLocation.Unassigned, null, ExportRole.Primary)];
        }
    }

    /// <summary>Finds or creates the record for a target. Existing records keep their path (stable filenames).</summary>
    private async Task<ExportRecord> RecordFor(Meeting meeting, ExportKind kind, Target target, ExportLayout layout, CancellationToken ct)
    {
        var projectId = target.Project?.Id;
        var existing = meeting.ExportRecords.FirstOrDefault(r =>
            r.Kind == kind && r.Location == target.Location && r.ProjectId == projectId && r.Status != ExportStatus.Superseded);
        if (existing is not null)
        {
            existing.Role = target.Role;
            return existing;
        }

        var date = meeting.MeetingDate();
        var dir = layout.RelativeDirectory(kind, target.Location, target.Project, date, meeting.GranolaNoteId, meeting.Title);
        var stem = layout.FileStem(target.Project, date, meeting.GranolaNoteId, meeting.Title);
        if (kind == ExportKind.Transcript && layout.SharedRoot)
        {
            var notesDir = layout.RelativeDirectory(ExportKind.Notes, target.Location, target.Project, date, meeting.GranolaNoteId, meeting.Title);
            if (string.Equals(notesDir, dir, StringComparison.OrdinalIgnoreCase)) stem += "-transcript";
        }
        var relative = await UniquePath(meeting, kind, layout, dir, stem, ct);

        var record = new ExportRecord
        {
            MeetingId = meeting.Id,
            Kind = kind,
            Location = target.Location,
            ProjectId = projectId,
            Role = target.Role,
            RelativePath = relative,
            Status = ExportStatus.Pending,
        };

        // Reassignment: move the previous unmodified primary file instead of leaving a duplicate behind.
        var previous = meeting.ExportRecords.FirstOrDefault(r =>
            r.Kind == kind && r.Role == ExportRole.Primary && target.Role == ExportRole.Primary && r.Status != ExportStatus.Superseded
            && (r.Location != target.Location || r.ProjectId != projectId));
        if (previous is not null && layout.Settings.MoveFilesOnReassignment)
        {
            var from = layout.AbsolutePath(kind, previous.RelativePath);
            var to = layout.AbsolutePath(kind, relative);
            var content = await AtomicFile.TryReadAsync(from, ct);
            if (content is not null && MarkdownRenderer.IsUnmodified(content) && !File.Exists(to))
            {
                AtomicFile.Move(from, to);
                record.LastGeneratedHash = previous.LastGeneratedHash;
                record.SourceContentHash = previous.SourceContentHash;
                record.Status = ExportStatus.Exported;
                previous.Status = ExportStatus.Superseded;
                previous.LastError = $"Moved to {SafePath.ToPortable(relative)}";
                logger.LogInformation("Moved {Kind} for {NoteId} from {From} to {To}", kind, meeting.GranolaNoteId, previous.RelativePath, relative);
            }
        }

        meeting.ExportRecords.Add(record);
        db.ExportRecords.Add(record);
        return record;
    }

    private async Task<string> UniquePath(Meeting meeting, ExportKind kind, ExportLayout layout, string dir, string stem, CancellationToken ct)
    {
        var shortId = meeting.GranolaNoteId.Length > 6 ? meeting.GranolaNoteId[^6..] : meeting.GranolaNoteId;
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var name = attempt switch
            {
                0 => stem,
                1 => $"{stem}-{shortId}",
                _ => $"{stem}-{shortId}-{attempt}",
            };
            var rel = SafePath.ToPortable(Path.Combine(dir, name + ".md"));
            if (await IsTaken(meeting, kind, layout, rel, ct)) continue;
            return rel;
        }
        throw new ExportFileException(ErrorCategory.FileConflict, $"Could not find a free file name for '{stem}'.");
    }

    private async Task<bool> IsTaken(Meeting meeting, ExportKind kind, ExportLayout layout, string rel, CancellationToken ct)
    {
        var lower = rel.ToLowerInvariant();
        var kinds = layout.SharedRoot ? new[] { ExportKind.Notes, ExportKind.Transcript } : [kind];
        var inDb = await db.ExportRecords.AnyAsync(r => kinds.Contains(r.Kind) && r.RelativePath.ToLower() == lower
            && !(r.MeetingId == meeting.Id && r.Status == ExportStatus.Superseded), ct);
        if (inDb) return true;
        if (meeting.ExportRecords.Any(r => kinds.Contains(r.Kind) && r.RelativePath.Equals(rel, StringComparison.OrdinalIgnoreCase) && r.Status != ExportStatus.Superseded))
            return true;

        var abs = layout.AbsolutePath(kind, rel);
        if (!File.Exists(abs)) return false;
        // A file we generated for this very meeting earlier (e.g. state was lost) can be reused.
        var content = await AtomicFile.TryReadAsync(abs, ct);
        var fm = content is null ? null : MarkdownRenderer.ReadFrontMatter(content);
        return fm is null || !fm.TryGetValue(MarkdownRenderer.IdKey(meeting.Source), out var id) || id != meeting.GranolaNoteId
            || !fm.TryGetValue("content_type", out var type) || type != (kind == ExportKind.Notes ? "notes" : "transcript");
    }

    private async Task WriteAsync(Meeting meeting, ExportRecord record, ExportLayout layout, string content, ExportMode mode, ExportOutcome outcome, CancellationToken ct)
    {
        var abs = layout.AbsolutePath(record.Kind, record.RelativePath);
        var newHash = MarkdownRenderer.ComputeHash(content);
        try
        {
            var existing = await AtomicFile.TryReadAsync(abs, ct);
            if (existing is null)
            {
                var previouslyExported = record.LastGeneratedHash is not null && record.Status is ExportStatus.Exported or ExportStatus.LocallyModified or ExportStatus.KeptLocal or ExportStatus.Missing;
                if (previouslyExported && mode == ExportMode.Normal && record.LastGeneratedHash == newHash)
                {
                    // The user moved or deleted the file and nothing changed at the source: respect that.
                    SetStatus(record, ExportStatus.Missing, "File not found at its export path. Run reconciliation to recreate it.");
                    return;
                }
                await AtomicFile.WriteAsync(abs, content, ct);
                Written(record, newHash, meeting, outcome, updated: previouslyExported);
                return;
            }

            var fm = MarkdownRenderer.ReadFrontMatter(existing);
            var ours = fm is not null && fm.TryGetValue(MarkdownRenderer.IdKey(meeting.Source), out var id) && id == meeting.GranolaNoteId;
            if (!ours && mode != ExportMode.Force)
            {
                Conflict(record, "Another file now occupies this export path; it was not overwritten.", outcome);
                return;
            }

            if (ours && MarkdownRenderer.IsUnmodified(existing))
            {
                if (MarkdownRenderer.ComputeHash(existing) == newHash)
                {
                    record.LastGeneratedHash = newHash;
                    record.SourceContentHash = meeting.SourceContentHash;
                    SetStatus(record, ExportStatus.Exported, null);
                    return;
                }
                await AtomicFile.WriteAsync(abs, content, ct);
                Written(record, newHash, meeting, outcome, updated: true);
                return;
            }

            // The file was edited by the user.
            if (mode == ExportMode.Force || layout.Settings.ConflictPolicy == ConflictPolicy.AlwaysOverwrite)
            {
                await AtomicFile.WriteAsync(abs, content, ct);
                Written(record, newHash, meeting, outcome, updated: true);
                return;
            }
            if (record.Status == ExportStatus.KeptLocal && record.LastGeneratedHash == newHash)
                return;
            if (record.LastGeneratedHash == newHash)
            {
                SetStatus(record, ExportStatus.LocallyModified, "Edited locally; the source has not changed.");
                return;
            }
            Conflict(record, "The file was edited locally and the source meeting has changed. Resolve the conflict to continue.", outcome);
        }
        catch (ExportFileException ex)
        {
            SetStatus(record, ExportStatus.Failed, ex.Message);
            outcome.Errors.Add(new ExportError(record.Kind, ex.Category, ex.Message));
            logger.LogWarning("Export of {Kind} for {NoteId} failed: {Category} {Message}", record.Kind, meeting.GranolaNoteId, ex.Category, ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            SetStatus(record, ExportStatus.Failed, ex.Message);
            outcome.Errors.Add(new ExportError(record.Kind, ErrorCategory.Configuration, ex.Message));
        }
    }

    private void Written(ExportRecord record, string hash, Meeting meeting, ExportOutcome outcome, bool updated)
    {
        record.LastGeneratedHash = hash;
        record.SourceContentHash = meeting.SourceContentHash;
        record.LastExportedAt = clock.UtcNow.UtcDateTime;
        record.ConflictDetectedAt = null;
        SetStatus(record, ExportStatus.Exported, null);
        if (record.Kind == ExportKind.Notes) outcome.NotesWritten++;
        else outcome.TranscriptsWritten++;
        if (updated) outcome.FilesUpdated++;
    }

    private void Conflict(ExportRecord record, string message, ExportOutcome outcome)
    {
        record.ConflictDetectedAt ??= clock.UtcNow.UtcDateTime;
        SetStatus(record, ExportStatus.Conflict, message);
        outcome.Conflicts++;
    }

    private void SetStatus(ExportRecord record, ExportStatus status, string? error)
    {
        record.Status = status;
        record.LastError = error;
        record.UpdatedAt = clock.UtcNow.UtcDateTime;
    }

    /// <summary>Records for targets the meeting no longer belongs to are superseded; their files are left untouched.</summary>
    private Task SupersedeStaleRecords(Meeting meeting, List<Target> targets, ExportLayout layout, CancellationToken ct)
    {
        foreach (var r in meeting.ExportRecords.Where(r => r.Status != ExportStatus.Superseded))
        {
            var stillTargeted = targets.Any(t => t.Location == r.Location && t.Project?.Id == r.ProjectId)
                && (r.Kind == ExportKind.Notes ? layout.Settings.ExportNotes : layout.Settings.ExportTranscripts);
            if (!stillTargeted)
            {
                r.Status = ExportStatus.Superseded;
                r.LastError ??= "Meeting no longer exported here; file left in place.";
                r.UpdatedAt = clock.UtcNow.UtcDateTime;
            }
        }
        return Task.CompletedTask;
    }

    private static void UpdateAggregate(Meeting meeting, List<Target> targets, DateTimeOffset now)
    {
        var active = meeting.ExportRecords.Where(r => r.Status != ExportStatus.Superseded).ToList();
        var primary = active.Where(r => r.Role == ExportRole.Primary).ToList();
        meeting.NotesExportPath = primary.FirstOrDefault(r => r.Kind == ExportKind.Notes)?.RelativePath;
        meeting.TranscriptExportPath = primary.FirstOrDefault(r => r.Kind == ExportKind.Transcript)?.RelativePath;
        meeting.ExportStatus = active.Count == 0 ? ExportStatus.Skipped : active.Select(r => r.Status).OrderBy(Severity).First();
        meeting.ExportPending = active.Any(r => r.Status is ExportStatus.Failed or ExportStatus.Pending);
        if (active.Any(r => r.Status == ExportStatus.Exported) && !meeting.ExportPending)
            meeting.LastSuccessfulExportAt = now.UtcDateTime;
    }

    public static int Severity(ExportStatus s) => s switch
    {
        ExportStatus.Failed => 0,
        ExportStatus.Conflict => 1,
        ExportStatus.Pending => 2,
        ExportStatus.Missing => 3,
        ExportStatus.LocallyModified => 4,
        ExportStatus.KeptLocal => 5,
        ExportStatus.Exported => 6,
        ExportStatus.Skipped => 7,
        _ => 8,
    };

    private static string? RelativeLink(ExportLayout layout, ExportKind fromKind, string fromRel, ExportKind toKind, string toRel)
    {
        var from = Path.GetDirectoryName(layout.AbsolutePath(fromKind, fromRel))!;
        var to = layout.AbsolutePath(toKind, toRel);
        if (!string.Equals(Path.GetPathRoot(from), Path.GetPathRoot(to), StringComparison.OrdinalIgnoreCase)) return null;
        return SafePath.ToPortable(Path.GetRelativePath(from, to));
    }
}
