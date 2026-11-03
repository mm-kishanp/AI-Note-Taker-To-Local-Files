using Chroniq.Application.Classification;
using Chroniq.Application.DTOs;
using Chroniq.Application.Export;
using Chroniq.Application.Interfaces;
using Chroniq.Application.Synchronization;
using Chroniq.Domain.Entities;
using Chroniq.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Chroniq.Application.Meetings;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ValidationException(IReadOnlyList<ValidationErrorDto> errors)
    : Exception(string.Join(" ", errors.Select(e => e.Message)))
{
    public IReadOnlyList<ValidationErrorDto> Errors { get; } = errors;

    public static ValidationException Single(string field, string message) => new([new ValidationErrorDto(field, message)]);
}

public sealed class MeetingService(
    IAppDbContext db,
    ISettingsService settings,
    IAppPaths paths,
    IClock clock,
    ClassificationService classification,
    MeetingProcessor processor,
    ProjectContextService contexts)
{
    public async Task<PagedResponse<MeetingListItemDto>> QueryAsync(MeetingQuery q, CancellationToken ct)
    {
        var query = db.Meetings.AsNoTracking().Include(m => m.AssignedProject!).ThenInclude(p => p.Client).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToLower();
            query = query.Where(m => m.Title.ToLower().Contains(s) || m.AttendeesJson.ToLower().Contains(s) || m.GranolaNoteId.ToLower() == s);
        }
        if (q.From is { } from) query = query.Where(m => (m.MeetingStartTime ?? m.CreatedAt) >= from.UtcDateTime);
        if (q.To is { } to) query = query.Where(m => (m.MeetingStartTime ?? m.CreatedAt) <= to.UtcDateTime);
        if (q.ProjectId is { } pid) query = query.Where(m => m.AssignedProjectId == pid || m.ProjectAssignments.Any(a => a.ProjectId == pid));
        if (q.ClientId is { } cid) query = query.Where(m => m.AssignedProject != null && m.AssignedProject.ClientId == cid);
        if (q.Classification is { } cs) query = query.Where(m => m.ClassificationStatus == cs);
        if (q.ExportStatus is { } es) query = query.Where(m => m.ExportStatus == es);
        if (q.TranscriptStatus is { } ts) query = query.Where(m => m.TranscriptStatus == ts);
        if (q.NeedsReview is { } nr) query = query.Where(m => m.NeedsReview == nr);
        if (q.Source is { } src) query = query.Where(m => m.Source == src);

        query = (q.Sort?.ToLowerInvariant(), q.Desc) switch
        {
            ("title", false) => query.OrderBy(m => m.Title),
            ("title", true) => query.OrderByDescending(m => m.Title),
            ("project", false) => query.OrderBy(m => m.AssignedProject!.Name),
            ("project", true) => query.OrderByDescending(m => m.AssignedProject!.Name),
            ("synced", false) => query.OrderBy(m => m.LastSuccessfulFetchAt),
            ("synced", true) => query.OrderByDescending(m => m.LastSuccessfulFetchAt),
            ("status", false) => query.OrderBy(m => m.ClassificationStatus),
            ("status", true) => query.OrderByDescending(m => m.ClassificationStatus),
            (_, false) => query.OrderBy(m => m.MeetingStartTime ?? m.CreatedAt),
            _ => query.OrderByDescending(m => m.MeetingStartTime ?? m.CreatedAt),
        };

        var pageSize = Math.Clamp(q.PageSize, 1, 200);
        var page = Math.Max(1, q.Page);
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResponse<MeetingListItemDto>(items.Select(ToListItem).ToList(), page, pageSize, total);
    }

    public static MeetingListItemDto ToListItem(Meeting m) => new(
        m.Id,
        m.GranolaNoteId,
        m.Title,
        m.MeetingDate(),
        m.Attendees().Select(a => a.Name ?? a.Email ?? "").Where(x => x.Length > 0).ToList(),
        m.AssignedProjectId,
        m.AssignedProject?.Name,
        m.AssignedProject?.Client?.Name,
        m.ClassificationStatus,
        m.AssignmentMethod,
        m.Certainty,
        m.NeedsReview,
        m.NotesStatus,
        m.TranscriptStatus,
        m.ExportStatus,
        m.SourceStatus,
        m.LastSuccessfulFetchAt is { } f ? new DateTimeOffset(f, TimeSpan.Zero) : null,
        m.LastSyncError,
        m.Source);

    public async Task<MeetingDetailDto> GetAsync(Guid id, CancellationToken ct)
    {
        var m = await LoadAsync(id, ct, tracking: false);
        var layout = await LayoutAsync(ct);
        var projects = await db.Projects.AsNoTracking().ToDictionaryAsync(p => p.Id, ct);
        var history = await db.SyncRunItems.AsNoTracking().Where(i => i.MeetingId == id)
            .OrderByDescending(i => i.Timestamp).Take(50).ToListAsync(ct);
        var cal = m.CalendarEvent();

        return new MeetingDetailDto(
            ToListItem(m),
            Utc(m.CreatedAt), Utc(m.UpdatedAt),
            m.MeetingStartTime is { } s ? Utc(s) : null,
            m.MeetingEndTime is { } e ? Utc(e) : null,
            m.OwnerName, m.OwnerEmail, m.SourceUrl, cal?.EventTitle,
            m.SourceFolders().Select(f => f.Name).ToList(),
            m.Participants.Select(p => new ParticipantDto(p.Name, p.Email, p.IsOwner, p.IsAttendee, p.IsInvitee, p.SpokenSegments)).ToList(),
            m.Speakers().Select(s => new SpeakerDto(s.Name, s.Segments)).ToList(),
            m.Summary, m.NotesContent, m.TranscriptSegmentCount,
            Explain(m, projects),
            m.ExportRecords.OrderBy(r => r.Status == ExportStatus.Superseded).ThenBy(r => r.Kind)
                .Select(r => ToDto(r, layout, projects)).ToList(),
            history.Select(i => new SyncRunItemDto(i.Id, i.SyncRunId, i.MeetingId, i.GranolaNoteId, i.Operation, i.Outcome, i.ErrorCategory, i.Retryable, i.Message, Utc(i.Timestamp))).ToList(),
            m.FailureCount,
            m.NextRetryAt is { } n ? Utc(n) : null,
            m.LastErrorCategory);
    }

    public async Task<PagedResponse<TranscriptSegmentDto>> TranscriptAsync(Guid id, int page, int pageSize, CancellationToken ct)
    {
        var m = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Meeting not found.");
        var segments = m.Transcript();
        pageSize = Math.Clamp(pageSize, 1, 500);
        page = Math.Max(1, page);
        var items = segments.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new TranscriptSegmentDto(MarkdownRenderer.SpeakerLabel(s.Speaker, m.OwnerName), s.Text, s.StartTime, s.EndTime))
            .ToList();
        return new PagedResponse<TranscriptSegmentDto>(items, page, pageSize, segments.Count);
    }

    public static ClassificationExplanationDto Explain(Meeting m, IReadOnlyDictionary<Guid, Project> projects)
    {
        var c = m.Classification();
        ProjectRefDto Ref(Guid id) => projects.TryGetValue(id, out var p) ? new ProjectRefDto(p.Id, p.Name, p.Slug) : new ProjectRefDto(id, "(deleted project)", "");
        return new ClassificationExplanationDto(
            m.ClassificationStatus,
            m.AssignmentMethod,
            m.Certainty,
            c?.Tier is { } t ? ClassificationEngine.TierLabel(t) : null,
            m.ClassificationReason ?? "Not classified yet.",
            c?.Evidence.Select(e => e.Describe()).ToList() ?? [],
            c?.CandidateProjectIds.Select(Ref).ToList() ?? [],
            c?.Conflicts.Select(x => $"{ClassificationEngine.TierLabel(x.Tier)}: {x.Description}").ToList() ?? [],
            m.NeedsReview,
            c?.SuggestedNextAction,
            m.ManualAssignment,
            m.ManualAssignedAt is { } at ? Utc(at) : null);
    }

    public static ExportRecordDto ToDto(ExportRecord r, ExportLayout layout, IReadOnlyDictionary<Guid, Project> projects)
    {
        string abs;
        try { abs = layout.AbsolutePath(r.Kind, r.RelativePath); }
        catch (InvalidOperationException) { abs = r.RelativePath; }
        return new ExportRecordDto(
            r.Id, r.Kind, r.Location, r.ProjectId,
            r.ProjectId is { } pid && projects.TryGetValue(pid, out var p) ? p.Name : null,
            r.Role, r.RelativePath, abs, File.Exists(abs), r.Status,
            r.LastExportedAt is { } le ? Utc(le) : null,
            r.ConflictDetectedAt is { } cd ? Utc(cd) : null,
            r.LastError);
    }

    /// <summary>Manual assignment. Authoritative until explicitly changed; files are moved/re-exported immediately.</summary>
    public async Task<MeetingDetailDto> AssignAsync(Guid id, AssignMeetingRequest req, CancellationToken ct)
    {
        var m = await LoadAsync(id, ct, tracking: true);
        if (req.Kind == ManualAssignmentKind.Project)
        {
            if (req.ProjectId is not Guid pid || !await db.Projects.AnyAsync(p => p.Id == pid, ct))
                throw ValidationException.Single("projectId", "Choose an existing project.");
        }
        m.ManualAssignment = req.Kind;
        m.ManualProjectId = req.Kind == ManualAssignmentKind.Project ? req.ProjectId : null;
        m.ManualAssignedAt = req.Kind == ManualAssignmentKind.None ? null : clock.UtcNow.UtcDateTime;
        db.ClassificationReviews.Add(new ClassificationReview
        {
            MeetingId = m.Id,
            Action = req.Kind switch
            {
                ManualAssignmentKind.Project => ReviewAction.AssignedProject,
                ManualAssignmentKind.Shared => ReviewAction.AssignedShared,
                ManualAssignmentKind.Excluded => ReviewAction.Excluded,
                _ => ReviewAction.ClearedManualAssignment,
            },
            ProjectId = m.ManualProjectId,
            Note = req.Note,
        });
        await db.SaveChangesAsync(ct);
        await ReprocessAsync(m, reclassify: true, ExportMode.Normal, ct);
        return await GetAsync(id, ct);
    }

    public async Task<MeetingDetailDto> ReclassifyAsync(Guid id, CancellationToken ct)
    {
        var m = await LoadAsync(id, ct, tracking: true);
        db.ClassificationReviews.Add(new ClassificationReview { MeetingId = m.Id, Action = ReviewAction.RetriedClassification });
        await db.SaveChangesAsync(ct);
        await ReprocessAsync(m, reclassify: true, ExportMode.Normal, ct);
        return await GetAsync(id, ct);
    }

    public async Task<MeetingDetailDto> ReExportAsync(Guid id, ReExportRequest req, CancellationToken ct)
    {
        var m = await LoadAsync(id, ct, tracking: true);
        await ReprocessAsync(m, reclassify: false, req.Force ? ExportMode.Force : ExportMode.Reconcile, ct);
        return await GetAsync(id, ct);
    }

    private async Task ReprocessAsync(Meeting m, bool reclassify, ExportMode mode, CancellationToken ct)
    {
        var layout = await LayoutAsync(ct);
        var ctx = await classification.LoadContextAsync(ct);
        var sync = await settings.GetSectionAsync<Settings.SyncSettings>(SettingKeys.Sync, ct);
        var before = m.ExportRecords.Where(r => r.ProjectId.HasValue).Select(r => r.ProjectId!.Value).ToHashSet();
        var result = await processor.ProcessAsync(new WorkItem(m.GranolaNoteId, false, m.Id), layout, ctx, mode, reclassify, sync.MaxMeetingRetries, new SyncCounters(), ct);
        before.UnionWith(result.ProjectIds);
        await contexts.UpdateProjectsAsync(before, layout, ct);
        await contexts.UpdateReviewQueueAsync(layout, ct);
    }

    public async Task<ConflictReviewDto> ReviewConflictAsync(Guid meetingId, Guid recordId, CancellationToken ct)
    {
        var (m, record, layout, content) = await PrepareConflictAsync(meetingId, recordId, ct);
        var local = await AtomicFile.TryReadAsync(layout.AbsolutePath(record.Kind, record.RelativePath), ct);
        return new ConflictReviewDto(record.Id, record.RelativePath, local, content);
    }

    public async Task<MeetingDetailDto> ResolveConflictAsync(Guid meetingId, Guid recordId, ConflictResolution resolution, CancellationToken ct)
    {
        var (m, record, layout, content) = await PrepareConflictAsync(meetingId, recordId, ct);
        var abs = layout.AbsolutePath(record.Kind, record.RelativePath);
        var newHash = MarkdownRenderer.ComputeHash(content);
        switch (resolution)
        {
            case ConflictResolution.KeepLocal:
                break;
            case ConflictResolution.ReplaceWithSource:
                await AtomicFile.WriteAsync(abs, content, ct);
                record.LastExportedAt = clock.UtcNow.UtcDateTime;
                break;
            case ConflictResolution.SaveConflictCopy:
                var copy = Path.Combine(Path.GetDirectoryName(abs)!,
                    $"{Path.GetFileNameWithoutExtension(abs)}.conflict-{clock.UtcNow:yyyyMMddHHmmss}.md");
                await AtomicFile.WriteAsync(copy, content, ct);
                break;
        }
        record.LastGeneratedHash = newHash;
        record.SourceContentHash = m.SourceContentHash;
        record.Status = resolution == ConflictResolution.ReplaceWithSource ? ExportStatus.Exported : ExportStatus.KeptLocal;
        record.ConflictDetectedAt = null;
        record.LastError = resolution switch
        {
            ConflictResolution.KeepLocal => "Local edits kept.",
            ConflictResolution.SaveConflictCopy => "Local edits kept; source version saved as a .conflict copy.",
            _ => null,
        };
        record.UpdatedAt = clock.UtcNow.UtcDateTime;
        var active = m.ExportRecords.Where(r => r.Status != ExportStatus.Superseded).ToList();
        m.ExportStatus = active.Select(r => r.Status).OrderBy(MeetingExporter.Severity).FirstOrDefault();
        await db.SaveChangesAsync(ct);
        return await GetAsync(meetingId, ct);
    }

    /// <summary>Regenerates the document a record would contain now, for conflict review/resolution.</summary>
    private async Task<(Meeting, ExportRecord, ExportLayout, string)> PrepareConflictAsync(Guid meetingId, Guid recordId, CancellationToken ct)
    {
        var m = await LoadAsync(meetingId, ct, tracking: true);
        var record = m.ExportRecords.FirstOrDefault(r => r.Id == recordId) ?? throw new NotFoundException("Export record not found.");
        var layout = await LayoutAsync(ct);
        var projects = await db.Projects.AsNoTracking().Include(p => p.Client).ToDictionaryAsync(p => p.Id, ProjectInfo.From, ct);
        var project = record.ProjectId is { } pid ? projects.GetValueOrDefault(pid) : null;
        var canonical = m.ExportRecords.FirstOrDefault(r => r.Role == ExportRole.Primary && r.ProjectId is not null)?.ProjectId is { } cp
            ? projects.GetValueOrDefault(cp)?.Name : null;
        var doc = m.ToDocument(record.Kind == ExportKind.Transcript) with
        {
            ProjectName = project?.Name,
            ProjectSlug = project?.Slug,
            ProjectId = project?.Id,
            ClientName = project?.ClientName,
            Location = record.Location,
            Role = record.Role,
            CanonicalProject = record.Role == ExportRole.Copy ? canonical : null,
            IncludeAttachmentLinks = layout.Settings.IncludeAttachmentLinks,
        };
        var other = m.ExportRecords.FirstOrDefault(r => r.Kind != record.Kind && r.Location == record.Location && r.ProjectId == record.ProjectId && r.Status != ExportStatus.Superseded);
        if (other is not null)
        {
            var from = Path.GetDirectoryName(layout.AbsolutePath(record.Kind, record.RelativePath))!;
            var link = SafePath.ToPortable(Path.GetRelativePath(from, layout.AbsolutePath(other.Kind, other.RelativePath)));
            doc = record.Kind == ExportKind.Notes ? doc with { TranscriptLink = link } : doc with { NotesLink = link };
        }
        var content = record.Kind == ExportKind.Notes
            ? MarkdownRenderer.RenderNotes(doc, clock.UtcNow)
            : MarkdownRenderer.RenderTranscript(doc, clock.UtcNow);
        return (m, record, layout, content);
    }

    public async Task<IReadOnlyList<DestinationPreviewDto>> PreviewDestinationAsync(Guid id, ManualAssignmentKind kind, Guid? projectId, CancellationToken ct)
    {
        var m = await db.Meetings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Meeting not found.");
        var layout = await LayoutAsync(ct);
        ProjectInfo? project = null;
        if (kind == ManualAssignmentKind.Project)
        {
            var p = await db.Projects.AsNoTracking().Include(x => x.Client).FirstOrDefaultAsync(x => x.Id == projectId, ct)
                ?? throw ValidationException.Single("projectId", "Project not found.");
            project = ProjectInfo.From(p);
        }
        if (kind == ManualAssignmentKind.Excluded) return [new DestinationPreviewDto("Excluded", null, null, null)];
        var location = kind switch
        {
            ManualAssignmentKind.Project => ExportLocation.Project,
            ManualAssignmentKind.Shared => ExportLocation.Shared,
            _ => ExportLocation.Unassigned,
        };
        var date = m.MeetingDate();
        var stem = layout.FileStem(project, date, m.GranolaNoteId, m.Title);
        string Full(ExportKind k)
        {
            var dir = layout.RelativeDirectory(k, location, project, date, m.GranolaNoteId, m.Title);
            var s = stem;
            if (k == ExportKind.Transcript && layout.SharedRoot && dir == layout.RelativeDirectory(ExportKind.Notes, location, project, date, m.GranolaNoteId, m.Title))
                s += "-transcript";
            return layout.AbsolutePath(k, Path.Combine(dir, s + ".md"));
        }
        return [new DestinationPreviewDto(location.ToString(), project?.Name,
            layout.Settings.ExportNotes ? Full(ExportKind.Notes) : null,
            layout.Settings.ExportTranscripts ? Full(ExportKind.Transcript) : null)];
    }

    public async Task<string> ExportedFilePathAsync(Guid meetingId, Guid recordId, CancellationToken ct)
    {
        var r = await db.ExportRecords.AsNoTracking().FirstOrDefaultAsync(x => x.Id == recordId && x.MeetingId == meetingId, ct)
            ?? throw new NotFoundException("Export record not found.");
        var layout = await LayoutAsync(ct);
        return layout.AbsolutePath(r.Kind, r.RelativePath);
    }

    private async Task<Meeting> LoadAsync(Guid id, CancellationToken ct, bool tracking)
    {
        IQueryable<Meeting> q = db.Meetings.Include(m => m.AssignedProject!).ThenInclude(p => p.Client)
            .Include(m => m.ExportRecords).Include(m => m.Participants).Include(m => m.ProjectAssignments);
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException("Meeting not found.");
    }

    private async Task<ExportLayout> LayoutAsync(CancellationToken ct)
    {
        var s = await settings.GetSectionAsync<Settings.ExportSettings>(SettingKeys.Export, ct);
        return new ExportLayout(s, paths.BaseDirectory);
    }

    private static DateTimeOffset Utc(DateTime d) => new(DateTime.SpecifyKind(d, DateTimeKind.Utc));
}
