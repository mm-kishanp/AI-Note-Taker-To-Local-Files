using System.Diagnostics;
using GranolaPI.Application.Classification;
using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Settings;
using GranolaPI.Domain.Entities;
using GranolaPI.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GranolaPI.Application.Synchronization;

/// <summary>
/// Orchestrates a synchronization run. Modes:
/// Incremental (updated_after checkpoint + pending retries), Full (every accessible note),
/// RetryFailed, ReExport (no API calls) and Reconcile (verify files on disk, recreate missing ones).
/// One meeting failing never stops the others; authentication/permission failures abort the run.
/// </summary>
public sealed class SyncEngine(
    IServiceScopeFactory scopes,
    SyncProgress progress,
    ISyncLease lease,
    IAppPaths paths,
    IClock clock,
    ILogger<SyncEngine> logger)
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(10);
    private readonly string _owner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public async Task<SyncRun> RunAsync(SyncRequest request, CancellationToken ct)
    {
        if (!await lease.TryAcquireAsync(_owner, LeaseDuration, ct)) throw new SyncAlreadyRunningException();

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();
        var api = scope.ServiceProvider.GetRequiredService<IGranolaApiClient>();
        var counters = new SyncCounters();
        var stopwatch = Stopwatch.StartNew();
        var touchedProjects = new HashSet<Guid>();
        var run = new SyncRun { Id = request.RunId, Trigger = request.Trigger, Mode = request.Mode, Status = SyncRunStatus.Running, StartedAt = clock.UtcNow.UtcDateTime };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync(ct);

        var listingComplete = false;
        try
        {
            var settings = await settingsService.GetAsync(ct);
            var errors = ExportLayout.Validate(settings.Export);
            if (errors.Count > 0) throw new InvalidOperationException("Export settings are invalid: " + string.Join(" ", errors));
            var layout = new ExportLayout(settings.Export, paths.BaseDirectory);
            AtomicFile.CleanupTempFiles(layout.NotesRoot);
            if (!layout.SharedRoot) AtomicFile.CleanupTempFiles(layout.TranscriptsRoot);

            var classification = scope.ServiceProvider.GetRequiredService<ClassificationService>();
            var ctx = await classification.LoadContextAsync(ct);

            progress.Phase("Discovering meetings");
            var items = new List<WorkItem>();
            var exportMode = ExportMode.Normal;
            var reclassify = request.Reclassify;
            switch (request.Mode)
            {
                case SyncMode.Incremental:
                case SyncMode.Full:
                    var checkpoint = await settingsService.GetSectionAsync<SyncCheckpoint>(SettingKeys.Checkpoint, ct);
                    var query = BuildQuery(request.Mode, settings.Sync, checkpoint);
                    run.UpdatedAfterFilter = query.UpdatedAfter?.ToString("O") ?? (query.CreatedAfter is { } ca ? $"created_after {ca:O}" : "all");
                    items = await DiscoverAsync(api, db, query, request.Mode == SyncMode.Full, counters, run, ct);
                    listingComplete = true;
                    await AddPendingRetries(db, items, settings.Sync, onlyDue: true, ct);
                    break;
                case SyncMode.RetryFailed when request.MeetingIds is { Count: > 0 } ids:
                    // Explicit retry of selected meetings: always re-fetch from Granola.
                    items = await db.Meetings.AsNoTracking().Where(m => ids.Contains(m.Id))
                        .Select(m => new WorkItem(m.GranolaNoteId, true, m.Id)).ToListAsync(ct);
                    break;
                case SyncMode.RetryFailed:
                    await AddPendingRetries(db, items, settings.Sync, onlyDue: false, ct);
                    break;
                case SyncMode.ReExport:
                    exportMode = request.Force ? ExportMode.Force : ExportMode.Normal;
                    items = await LocalItems(db, request.MeetingIds, ct);
                    break;
                case SyncMode.Reconcile:
                    exportMode = ExportMode.Reconcile;
                    items = await LocalItems(db, request.MeetingIds, ct);
                    break;
            }

            progress.Queued(items.Count);
            progress.Phase($"Processing {items.Count} meeting(s)");
            await db.SaveChangesAsync(ct);

            var abort = new CancellationTokenSource();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, abort.Token);
            GranolaApiException? fatal = null;
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(settings.Sync.MaxConcurrency, 1, 8), CancellationToken = linked.Token };
            var results = new System.Collections.Concurrent.ConcurrentBag<(WorkItem Item, ProcessResult Result)>();
            try
            {
                await Parallel.ForEachAsync(items, options, async (item, token) =>
                {
                    progress.Current(item.GranolaNoteId);
                    await lease.RenewAsync(_owner, LeaseDuration, token);
                    ProcessResult result;
                    try
                    {
                        using var itemScope = scopes.CreateScope();
                        var processor = itemScope.ServiceProvider.GetRequiredService<MeetingProcessor>();
                        result = await processor.ProcessAsync(item, layout, ctx, exportMode, reclassify, settings.Sync.MaxMeetingRetries, counters, token);
                    }
                    catch (GranolaApiException ex) when (ex.Category is ErrorCategory.Authentication or ErrorCategory.Permission)
                    {
                        fatal = ex;
                        await abort.CancelAsync();
                        return;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        var category = ex is ExportFileException fe ? fe.Category : ErrorCategory.Unknown;
                        logger.LogError(ex, "Processing {NoteId} failed", item.GranolaNoteId);
                        result = new ProcessResult(item.MeetingId, "Failed", category, true, ex.Message, []);
                    }
                    Interlocked.Increment(ref counters.MeetingsProcessed);
                    progress.Processed(result.Failed);
                    results.Add((item, result));
                });
            }
            catch (OperationCanceledException) when (fatal is not null && !ct.IsCancellationRequested)
            {
                // fall through to fatal handling below
            }
            if (fatal is not null) throw fatal;

            foreach (var (item, result) in results)
            {
                foreach (var p in result.ProjectIds) touchedProjects.Add(p);
                if (result.Outcome is "Unchanged" or "SourceInactive") continue;
                db.SyncRunItems.Add(new SyncRunItem
                {
                    SyncRunId = run.Id,
                    MeetingId = result.MeetingId,
                    GranolaNoteId = item.GranolaNoteId,
                    Operation = item.Fetch ? "FetchAndExport" : request.Mode.ToString(),
                    Outcome = result.Outcome,
                    ErrorCategory = result.Category,
                    Retryable = result.Retryable,
                    Message = Truncate(result.Message, 1000),
                    Timestamp = clock.UtcNow.UtcDateTime,
                });
            }

            progress.Phase("Updating project context");
            var contexts = scope.ServiceProvider.GetRequiredService<ProjectContextService>();
            await contexts.UpdateProjectsAsync(touchedProjects, layout, ct);
            await contexts.UpdateReviewQueueAsync(layout, ct);

            var anyFailure = results.Any(r => r.Result.Failed);
            run.Status = anyFailure ? SyncRunStatus.PartiallySucceeded : SyncRunStatus.Succeeded;

            if (listingComplete && request.Mode is SyncMode.Incremental or SyncMode.Full)
            {
                await settingsService.SaveSectionAsync(SettingKeys.Checkpoint, new SyncCheckpoint
                {
                    LastSuccessfulIncrementalStart = new DateTimeOffset(run.StartedAt, TimeSpan.Zero),
                    LastSuccessfulRunFinishedAt = clock.UtcNow,
                }, ct);
            }
        }
        catch (OperationCanceledException)
        {
            run.Status = SyncRunStatus.Cancelled;
            run.Error = "Cancelled.";
        }
        catch (GranolaApiException ex)
        {
            run.Status = SyncRunStatus.Failed;
            run.Error = $"{ex.Category}: {ex.Message}";
            logger.LogWarning("Sync run {RunId} failed: {Error}", run.Id, run.Error);
        }
        catch (Exception ex)
        {
            run.Status = SyncRunStatus.Failed;
            run.Error = ex is InvalidOperationException or ExportFileException ? ex.Message : $"Unexpected error: {ex.Message}";
            logger.LogError(ex, "Sync run {RunId} failed", run.Id);
        }
        finally
        {
            stopwatch.Stop();
            run.FinishedAt = clock.UtcNow.UtcDateTime;
            run.DurationMs = stopwatch.ElapsedMilliseconds;
            run.NotesDiscovered = counters.NotesDiscovered;
            run.MeetingsProcessed = counters.MeetingsProcessed;
            run.NotesExported = counters.NotesExported;
            run.TranscriptsExported = counters.TranscriptsExported;
            run.FilesUpdated = counters.FilesUpdated;
            run.MeetingsSkipped = counters.MeetingsSkipped;
            run.ClassificationFailures = counters.ClassificationFailures;
            run.ApiFailures = counters.ApiFailures;
            run.FileSystemFailures = counters.FileSystemFailures;
            run.Conflicts = counters.Conflicts;
            try
            {
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Could not persist sync run {RunId}", run.Id);
            }
            await lease.ReleaseAsync(_owner, CancellationToken.None);
        }
        return run;
    }

    private static NoteListQuery BuildQuery(SyncMode mode, SyncSettings s, SyncCheckpoint checkpoint)
    {
        if (mode == SyncMode.Incremental && checkpoint.LastSuccessfulIncrementalStart is { } last)
            return new NoteListQuery(UpdatedAfter: last.AddMinutes(-Math.Max(0, s.IncrementalOverlapMinutes)));
        return new NoteListQuery(CreatedAfter: s.InitialImportFrom);
    }

    private async Task<List<WorkItem>> DiscoverAsync(IGranolaApiClient api, IAppDbContext db, NoteListQuery query, bool full, SyncCounters counters, SyncRun run, CancellationToken ct)
    {
        var items = new List<WorkItem>();
        var cursor = (string?)null;
        do
        {
            var page = await api.ListNotesAsync(query with { Cursor = cursor }, ct);
            var ids = page.Items.Select(n => n.Id).ToList();
            var known = await db.Meetings.AsNoTracking()
                .Where(m => ids.Contains(m.GranolaNoteId))
                .Select(m => new { m.GranolaNoteId, m.UpdatedAt, m.ExportPending, m.TranscriptStatus, m.SourceStatus, m.ClassificationStatus })
                .ToDictionaryAsync(m => m.GranolaNoteId, ct);

            foreach (var note in page.Items)
            {
                counters.NotesDiscovered++;
                progress.Discovered();
                if (note.DeletedAt is not null)
                {
                    await MarkDeleted(db, note.Id, ct);
                    continue;
                }
                if (known.TryGetValue(note.Id, out var k)
                    && k.UpdatedAt == note.UpdatedAt.UtcDateTime
                    && !k.ExportPending
                    && k.TranscriptStatus != ContentStatus.Failed
                    && k.SourceStatus == SourceStatus.Active)
                {
                    counters.MeetingsSkipped++;
                    continue;
                }
                items.Add(new WorkItem(note.Id, Fetch: true));
            }
            cursor = page.HasMore ? page.Cursor : null;
            if (page.HasMore && string.IsNullOrEmpty(page.Cursor))
                throw new GranolaApiException(ErrorCategory.InvalidResponse, null, "Granola reported more pages but returned no cursor.");
        } while (cursor is not null);
        return items;
    }

    private static async Task MarkDeleted(IAppDbContext db, string noteId, CancellationToken ct)
    {
        var m = await db.Meetings.FirstOrDefaultAsync(x => x.GranolaNoteId == noteId, ct);
        if (m is null || m.SourceStatus == SourceStatus.Deleted) return;
        m.SourceStatus = SourceStatus.Deleted;
        m.LastSyncError = "Deleted in Granola. Exported files were kept.";
    }

    private async Task AddPendingRetries(IAppDbContext db, List<WorkItem> items, SyncSettings s, bool onlyDue, CancellationToken ct)
    {
        var now = clock.UtcNow.UtcDateTime;
        var queued = items.Select(i => i.GranolaNoteId).ToHashSet();
        var q = db.Meetings.AsNoTracking().Where(m => m.SourceStatus == SourceStatus.Active
            && (m.ExportPending || m.TranscriptStatus == ContentStatus.Failed || m.LastErrorCategory != ErrorCategory.None));
        if (onlyDue) q = q.Where(m => m.FailureCount <= s.MaxMeetingRetries && (m.NextRetryAt == null || m.NextRetryAt <= now));
        var pending = await q.Select(m => new { m.Id, m.GranolaNoteId, m.TranscriptStatus, m.LastErrorCategory }).ToListAsync(ct);
        foreach (var p in pending.Where(p => !queued.Contains(p.GranolaNoteId)))
        {
            // Re-fetch when the source retrieval failed; otherwise only the export needs to be retried.
            var refetch = p.TranscriptStatus == ContentStatus.Failed || p.LastErrorCategory is ErrorCategory.RateLimited or ErrorCategory.TransientApi or ErrorCategory.InvalidResponse;
            items.Add(new WorkItem(p.GranolaNoteId, refetch, p.Id));
        }
    }

    private static async Task<List<WorkItem>> LocalItems(IAppDbContext db, IReadOnlyList<Guid>? ids, CancellationToken ct)
    {
        var q = db.Meetings.AsNoTracking().Where(m => m.SourceStatus == SourceStatus.Active);
        if (ids is { Count: > 0 }) q = q.Where(m => ids.Contains(m.Id));
        return await q.Select(m => new WorkItem(m.GranolaNoteId, false, m.Id)).ToListAsync(ct);
    }

    private static string? Truncate(string? s, int max) => s is null || s.Length <= max ? s : s[..max];
}
