using Chroniq.Application.Classification;
using Chroniq.Application.DTOs;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Projects;
using Chroniq.Application.Settings;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chroniq.Application.Meetings;

public sealed class DashboardService(IAppDbContext db, ISettingsService settings, ISecretProvider secrets)
{
    public async Task<DashboardDto> GetAsync(CancellationToken ct)
    {
        var meetings = db.Meetings.AsNoTracking();
        var total = await meetings.CountAsync(ct);
        var exported = await meetings.CountAsync(m => m.ExportRecords.Any(r => r.Status == ExportStatus.Exported || r.Status == ExportStatus.LocallyModified || r.Status == ExportStatus.KeptLocal), ct);
        var activeRecords = db.ExportRecords.AsNoTracking().Where(r => r.Status != ExportStatus.Superseded);
        var exportedStatuses = new[] { ExportStatus.Exported, ExportStatus.LocallyModified, ExportStatus.KeptLocal };
        var notes = await activeRecords.CountAsync(r => r.Kind == ExportKind.Notes && exportedStatuses.Contains(r.Status), ct);
        var transcripts = await activeRecords.CountAsync(r => r.Kind == ExportKind.Transcript && exportedStatuses.Contains(r.Status), ct);
        var failed = await meetings.CountAsync(m => m.ExportStatus == ExportStatus.Failed || m.LastErrorCategory != ErrorCategory.None, ct);
        var conflicts = await activeRecords.CountAsync(r => r.Status == ExportStatus.Conflict, ct);
        var projects = await db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, ct);
        var unassigned = await meetings.CountAsync(m => m.ClassificationStatus == ClassificationStatus.Unassigned, ct);
        var review = await meetings.CountAsync(m => m.NeedsReview && m.ClassificationStatus != ClassificationStatus.Excluded, ct);

        var lastOk = await db.SyncRuns.AsNoTracking()
            .Where(r => r.Status == SyncRunStatus.Succeeded || r.Status == SyncRunStatus.PartiallySucceeded)
            .OrderByDescending(r => r.FinishedAt).Select(r => r.FinishedAt).FirstOrDefaultAsync(ct);
        var lastAny = await db.SyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Select(r => r.FinishedAt ?? r.StartedAt).FirstOrDefaultAsync(ct);
        var sync = await settings.GetSectionAsync<SyncSettings>(SettingKeys.Sync, ct);
        DateTimeOffset? next = null;
        if (sync.AutoSyncEnabled)
            next = lastAny == default ? DateTimeOffset.UtcNow : new DateTimeOffset(DateTime.SpecifyKind(lastAny, DateTimeKind.Utc)).AddMinutes(Math.Max(5, sync.IntervalMinutes));

        var recent = await meetings.Include(m => m.AssignedProject!).ThenInclude(p => p.Client)
            .OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt).Take(8).ToListAsync(ct);
        var runs = await db.SyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt).Take(5).ToListAsync(ct);

        return new DashboardDto(total, exported, notes, transcripts, projects, unassigned, review, failed, conflicts,
            lastOk is { } lo ? new DateTimeOffset(DateTime.SpecifyKind(lo, DateTimeKind.Utc)) : null,
            next, sync.AutoSyncEnabled, secrets.Get(SecretNames.GranolaApiKey) is not null,
            recent.Select(MeetingService.ToListItem).ToList(),
            runs.Select(SyncRunMapper.ToDto).ToList());
    }
}

public static class SyncRunMapper
{
    public static SyncRunDto ToDto(SyncRun r) => new(
        r.Id,
        new DateTimeOffset(DateTime.SpecifyKind(r.StartedAt, DateTimeKind.Utc)),
        r.FinishedAt is { } f ? new DateTimeOffset(DateTime.SpecifyKind(f, DateTimeKind.Utc)) : null,
        r.Trigger, r.Mode, r.Status, r.NotesDiscovered, r.MeetingsProcessed, r.NotesExported, r.TranscriptsExported,
        r.FilesUpdated, r.MeetingsSkipped, r.ClassificationFailures, r.ApiFailures, r.FileSystemFailures, r.Conflicts,
        r.DurationMs, r.Error);
}

public sealed class SyncHistoryService(IAppDbContext db)
{
    public async Task<PagedResponse<SyncRunDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var q = db.SyncRuns.AsNoTracking().OrderByDescending(r => r.StartedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<SyncRunDto>(items.Select(SyncRunMapper.ToDto).ToList(), page, pageSize, total);
    }

    public async Task<(SyncRunDto Run, IReadOnlyList<SyncRunItemDto> Items)> GetAsync(Guid id, CancellationToken ct)
    {
        var run = await db.SyncRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException("Sync run not found.");
        var items = await db.SyncRunItems.AsNoTracking().Where(i => i.SyncRunId == id).OrderBy(i => i.Timestamp).Take(2000).ToListAsync(ct);
        return (SyncRunMapper.ToDto(run), items.Select(i => new SyncRunItemDto(i.Id, i.SyncRunId, i.MeetingId, i.GranolaNoteId, i.Operation, i.Outcome, i.ErrorCategory, i.Retryable, i.Message,
            new DateTimeOffset(DateTime.SpecifyKind(i.Timestamp, DateTimeKind.Utc)))).ToList());
    }

    /// <summary>Marks runs left "Running" by a crashed process as interrupted. Called at startup.</summary>
    public async Task<int> MarkInterruptedAsync(CancellationToken ct)
    {
        var stale = await db.SyncRuns.Where(r => r.Status == SyncRunStatus.Running).ToListAsync(ct);
        foreach (var r in stale)
        {
            r.Status = SyncRunStatus.Interrupted;
            r.FinishedAt ??= DateTime.UtcNow;
            r.Error = "The application stopped before this run finished. Pending meetings are retried on the next run.";
        }
        await db.SaveChangesAsync(ct);
        return stale.Count;
    }
}

public sealed class UnassignedService(IAppDbContext db, ProjectService projects, MeetingService meetings, ClassificationService classification)
{
    public async Task<PagedResponse<UnassignedItemDto>> ListAsync(int page, int pageSize, CancellationToken ct)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(1, page);
        var q = db.Meetings.AsNoTracking().Include(m => m.Participants)
            .Where(m => m.SourceStatus == SourceStatus.Active
                && (m.ClassificationStatus == ClassificationStatus.Unassigned || m.ClassificationStatus == ClassificationStatus.NeedsReview
                    || (m.NeedsReview && m.ClassificationStatus != ClassificationStatus.Excluded)))
            .OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var projectMap = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        return new PagedResponse<UnassignedItemDto>(items.Select(m =>
        {
            var c = m.Classification();
            return new UnassignedItemDto(
                MeetingService.ToListItem(m),
                m.Participants.Select(p => new ParticipantDto(p.Name, p.Email, p.IsOwner, p.IsAttendee, p.IsInvitee, p.SpokenSegments)).ToList(),
                m.Speakers().Select(s => new SpeakerDto(s.Name, s.Segments)).ToList(),
                c?.CandidateProjectIds.Where(projectMap.ContainsKey).Select(id => new ProjectRefDto(id, projectMap[id].Name, projectMap[id].Slug)).ToList() ?? [],
                m.ClassificationReason ?? "Not classified yet.",
                c?.SuggestedNextAction,
                c?.AiSuggestion is { } ai ? ai.Explanation : null);
        }).ToList(), page, pageSize, total);
    }

    /// <summary>
    /// Saves a new rule on a project from the review screen, optionally assigns this meeting manually,
    /// and returns a preview of other unassigned meetings the rule would now match (not applied until confirmed).
    /// </summary>
    public async Task<TestMappingResultDto> SaveMappingAsync(Guid meetingId, SaveMappingFromReviewRequest req, CancellationToken ct)
    {
        await projects.AddRuleAsync(req.ProjectId, new RuleInput(req.Kind, req.Value), ct);
        if (req.AssignThisMeeting)
            await meetings.AssignAsync(meetingId, new AssignMeetingRequest(ManualAssignmentKind.None, null, "Mapping saved from review"), ct);
        var ctx = await classification.LoadContextAsync(ct);
        var preview = await projects.TestMappingAsync(req.ProjectId, new TestMappingRequest(null, 5000), ctx, ct);
        var unassignedOnly = preview.Items.Where(i => i.Changes && !i.IsManual
            && i.CurrentStatus is ClassificationStatus.Unassigned or ClassificationStatus.NeedsReview).ToList();
        return preview with { Items = unassignedOnly, WouldChange = unassignedOnly.Count };
    }
}
