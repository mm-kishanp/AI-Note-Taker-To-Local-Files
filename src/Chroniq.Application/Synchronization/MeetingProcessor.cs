using Chroniq.Application.Classification;
using Chroniq.Application.Export;
using Chroniq.Application.Fireflies;
using Chroniq.Application.Granola;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Meetings;
using Chroniq.Application.WhisperAi;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Chroniq.Application.Synchronization;

/// <param name="GranolaNoteId">The source's ID: a Granola note ID, a Fireflies transcript ID or a WhisperAI transcript ID.</param>
/// <param name="SourceCreatedAt">Creation time from the source's listing, for sources whose detail endpoint omits it (WhisperAI).</param>
public sealed record WorkItem(string GranolaNoteId, bool Fetch, Guid? MeetingId = null, MeetingSource Source = MeetingSource.Granola,
    DateTimeOffset? SourceCreatedAt = null);

public sealed record ProcessResult(Guid? MeetingId, string Outcome, ErrorCategory Category, bool Retryable, string? Message, IReadOnlyList<Guid> ProjectIds)
{
    public bool Failed => Category != ErrorCategory.None;
}

/// <summary>
/// Processes one meeting in its own scope: fetch → persist → classify → export → record.
/// Each step is saved before the next so an interrupted run can resume from the database state.
/// </summary>
public sealed class MeetingProcessor(
    IAppDbContext db,
    IGranolaApiClient api,
    IFirefliesApiClient fireflies,
    IWhisperAiApiClient whisperAi,
    ClassificationService classification,
    MeetingExporter exporter,
    IClock clock,
    ILogger<MeetingProcessor> logger)
{
    public async Task<ProcessResult> ProcessAsync(
        WorkItem item,
        ExportLayout layout,
        ClassificationContext ctx,
        ExportMode exportMode,
        bool reclassify,
        int maxMeetingRetries,
        SyncCounters counters,
        CancellationToken ct)
    {
        var meeting = await db.Meetings
            .Include(m => m.ExportRecords)
            .Include(m => m.ProjectAssignments)
            .Include(m => m.Participants)

            .FirstOrDefaultAsync(m => item.MeetingId != null ? m.Id == item.MeetingId : m.GranolaNoteId == item.GranolaNoteId, ct);
        var now = clock.UtcNow;

        if (item.Fetch)
        {
            GranolaNoteFetch? fetched;
            try
            {
                fetched = item.Source switch
                {
                    MeetingSource.Fireflies => await fireflies.GetTranscriptAsync(item.GranolaNoteId, ct) is { } call ? FirefliesMapper.ToFetch(call) : null,
                    // A retry has no listing data; keep the creation time stored on the first sync.
                    MeetingSource.WhisperAi => await whisperAi.GetTranscriptAsync(item.GranolaNoteId, ct) is { } t
                        ? WhisperAiMapper.ToFetch(t, item.SourceCreatedAt ?? (meeting is null ? null : new DateTimeOffset(DateTime.SpecifyKind(meeting.CreatedAt, DateTimeKind.Utc))))
                        : null,
                    _ => await api.GetNoteAsync(item.GranolaNoteId, includeTranscript: true, ct),
                };
            }
            catch (GranolaApiException ex)
            {
                Interlocked.Increment(ref counters.ApiFailures);
                if (meeting is not null)
                {
                    RecordFailure(meeting, ex.Category, ex.Message, now, maxMeetingRetries);
                    await db.SaveChangesAsync(ct);
                }
                if (ex.Category is ErrorCategory.Authentication or ErrorCategory.Permission) throw;
                return new ProcessResult(meeting?.Id, "FetchFailed", ex.Category, ex.IsRetryable, ex.Message, []);
            }

            if (fetched is null)
            {
                if (meeting is null) return new ProcessResult(null, "NotAccessible", ErrorCategory.None, false, "Note is not accessible.", []);
                meeting.SourceStatus = SourceStatus.Inaccessible;
                meeting.LastSyncError = "The note is no longer accessible with this API key (deleted or permissions changed). Exported files were kept.";
                meeting.LastErrorCategory = ErrorCategory.NotFound;
                meeting.ExportPending = false;
                await db.SaveChangesAsync(ct);
                return new ProcessResult(meeting.Id, "SourceInaccessible", ErrorCategory.None, false, meeting.LastSyncError, []);
            }

            if (meeting is null)
            {
                meeting = new Meeting { GranolaNoteId = fetched.Note.Id, Source = item.Source };
                db.Meetings.Add(meeting);
            }
            MeetingIngestor.Apply(meeting, fetched, now);
            meeting.ExportPending = true;
            if (fetched.Transcript is { Success: false } tf)
            {
                Interlocked.Increment(ref counters.ApiFailures);
                RecordFailure(meeting, tf.ErrorCategory == ErrorCategory.None ? ErrorCategory.TranscriptUnavailable : tf.ErrorCategory,
                    $"Transcript retrieval failed: {tf.Error}", now, maxMeetingRetries);
            }
            else
            {
                ClearFailure(meeting);
            }
            await SaveAsync(ct); // persist the normalized record before classification/export
        }
        else if (meeting is null)
        {
            return new ProcessResult(null, "Missing", ErrorCategory.Database, false, "Meeting not found locally.", []);
        }

        if (meeting.SourceStatus != SourceStatus.Active)
            return new ProcessResult(meeting.Id, "SourceInactive", ErrorCategory.None, false, null, []);

        if (item.Fetch || reclassify || meeting.ClassificationStatus == ClassificationStatus.Pending)
        {
            try
            {
                var result = await classification.ClassifyAsync(meeting, ctx, ct);
                ClassificationService.Apply(meeting, result);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Interlocked.Increment(ref counters.ClassificationFailures);
                logger.LogError(ex, "Classification failed for {NoteId}", meeting.GranolaNoteId);
                meeting.ClassificationStatus = ClassificationStatus.NeedsReview;
                meeting.NeedsReview = true;
                meeting.ClassificationReason = "Classification failed; review manually.";
                meeting.LastErrorCategory = ErrorCategory.Classification;
            }
            await SaveAsync(ct);
        }

        var outcome = await exporter.ExportAsync(meeting, layout, ctx.ProjectInfos, exportMode, ct);
        Interlocked.Add(ref counters.NotesExported, outcome.NotesWritten);
        Interlocked.Add(ref counters.TranscriptsExported, outcome.TranscriptsWritten);
        Interlocked.Add(ref counters.FilesUpdated, outcome.FilesUpdated);
        Interlocked.Add(ref counters.Conflicts, outcome.Conflicts);

        var fsErrors = outcome.Errors.Where(e => e.Category != ErrorCategory.TranscriptUnavailable).ToList();
        if (fsErrors.Count > 0)
        {
            Interlocked.Increment(ref counters.FileSystemFailures);
            RecordFailure(meeting, fsErrors[0].Category, string.Join("; ", fsErrors.Select(e => $"{e.Kind}: {e.Message}")), now, maxMeetingRetries);
        }
        else if (meeting.TranscriptStatus != ContentStatus.Failed && meeting.LastErrorCategory is not ErrorCategory.Classification)
        {
            ClearFailure(meeting);
        }
        meeting.UpdatedLocallyAt = now.UtcDateTime;
        await SaveAsync(ct);

        var projectIds = meeting.ExportRecords.Where(r => r.ProjectId.HasValue).Select(r => r.ProjectId!.Value).Distinct().ToList();
        if (meeting.LastErrorCategory != ErrorCategory.None)
            return new ProcessResult(meeting.Id, "Failed", meeting.LastErrorCategory, meeting.NextRetryAt is not null, meeting.LastSyncError, projectIds);
        var label = outcome.Conflicts > 0 ? "Conflict"
            : outcome.NotesWritten + outcome.TranscriptsWritten > 0 ? "Exported"
            : "Unchanged";
        return new ProcessResult(meeting.Id, label, ErrorCategory.None, false, null, projectIds);
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw new ExportFileException(ErrorCategory.Database, $"Database update failed: {ex.InnerException?.Message ?? ex.Message}", ex);
        }
    }

    private static void RecordFailure(Meeting m, ErrorCategory category, string message, DateTimeOffset now, int maxRetries)
    {
        m.FailureCount++;
        m.LastErrorCategory = category;
        m.LastSyncError = message;
        m.ExportPending = true;
        var retryable = category is not (ErrorCategory.Authentication or ErrorCategory.Permission or ErrorCategory.Configuration);
        // Bounded exponential backoff between sync runs: 5, 10, 20, 40 ... minutes (max 12h).
        m.NextRetryAt = retryable && m.FailureCount <= maxRetries
            ? now.UtcDateTime.AddMinutes(Math.Min(5 * Math.Pow(2, m.FailureCount - 1), 720))
            : null;
    }

    private static void ClearFailure(Meeting m)
    {
        m.FailureCount = 0;
        m.LastErrorCategory = ErrorCategory.None;
        m.LastSyncError = null;
        m.NextRetryAt = null;
    }
}
