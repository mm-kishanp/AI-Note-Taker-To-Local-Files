using GranolaPI.Application.DTOs;
using GranolaPI.Application.Export;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GranolaPI.Api.Controllers;

[ApiController]
[Route("api/meetings")]
public sealed class MeetingsController(MeetingService meetings, SyncCoordinator sync, ExportBrowserService browser) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<MeetingListItemDto>> List([FromQuery] MeetingQuery query, CancellationToken ct) => meetings.QueryAsync(query, ct);

    [HttpGet("{id:guid}")]
    public Task<MeetingDetailDto> Get(Guid id, CancellationToken ct) => meetings.GetAsync(id, ct);

    [HttpGet("{id:guid}/transcript")]
    public Task<PagedResponse<TranscriptSegmentDto>> Transcript(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        meetings.TranscriptAsync(id, page, pageSize, ct);

    /// <summary>Manual assignment (project, Shared, Excluded) or clear it with kind=None.</summary>
    [HttpPost("{id:guid}/assign-project")]
    public Task<MeetingDetailDto> Assign(Guid id, AssignMeetingRequest req, CancellationToken ct) => meetings.AssignAsync(id, req, ct);

    [HttpGet("{id:guid}/destination-preview")]
    public Task<IReadOnlyList<DestinationPreviewDto>> Preview(Guid id, [FromQuery] ManualAssignmentKind kind, [FromQuery] Guid? projectId, CancellationToken ct) =>
        meetings.PreviewDestinationAsync(id, kind, projectId, ct);

    [HttpPost("{id:guid}/reclassify")]
    public Task<MeetingDetailDto> Reclassify(Guid id, CancellationToken ct) => meetings.ReclassifyAsync(id, ct);

    /// <summary>Re-fetches the meeting from Granola in the background.</summary>
    [HttpPost("{id:guid}/retry")]
    public ActionResult<SyncStartResult> Retry(Guid id)
    {
        var result = sync.TryStart(new SyncRequest(SyncMode.RetryFailed, SyncTrigger.Manual, [id]));
        return result.Started ? Accepted(result) : Conflict(result);
    }

    /// <summary>Regenerates files from stored data. force=true overwrites local edits (explicit user action).</summary>
    [HttpPost("{id:guid}/export")]
    public Task<MeetingDetailDto> Export(Guid id, ReExportRequest req, CancellationToken ct) => meetings.ReExportAsync(id, req, ct);

    [HttpGet("{id:guid}/exports/{recordId:guid}/conflict")]
    public Task<ConflictReviewDto> Conflict(Guid id, Guid recordId, CancellationToken ct) => meetings.ReviewConflictAsync(id, recordId, ct);

    [HttpPost("{id:guid}/exports/{recordId:guid}/resolve")]
    public Task<MeetingDetailDto> Resolve(Guid id, Guid recordId, ResolveConflictRequest req, CancellationToken ct) =>
        meetings.ResolveConflictAsync(id, recordId, req.Resolution, ct);

    [HttpPost("{id:guid}/exports/{recordId:guid}/open")]
    public async Task<IActionResult> Open(Guid id, Guid recordId, [FromQuery] bool folder, CancellationToken ct)
    {
        var path = await meetings.ExportedFilePathAsync(id, recordId, ct);
        var layout = await LayoutFor(ct);
        browser.OpenAbsoluteUnderRoots(layout, folder ? Path.GetDirectoryName(path)! : path);
        return NoContent();
    }

    private async Task<ExportLayout> LayoutFor(CancellationToken ct)
    {
        var settings = HttpContext.RequestServices.GetRequiredService<Application.Interfaces.ISettingsService>();
        var paths = HttpContext.RequestServices.GetRequiredService<Application.Interfaces.IAppPaths>();
        return new ExportLayout(await settings.GetSectionAsync<Application.Settings.ExportSettings>(Application.Interfaces.SettingKeys.Export, ct), paths.BaseDirectory);
    }
}

[ApiController]
[Route("api/unassigned")]
public sealed class UnassignedController(UnassignedService unassigned, MeetingService meetings) : ControllerBase
{
    [HttpGet]
    public Task<PagedResponse<UnassignedItemDto>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        unassigned.ListAsync(page, pageSize, ct);

    [HttpPost("{id:guid}/assign")]
    public Task<MeetingDetailDto> Assign(Guid id, AssignMeetingRequest req, CancellationToken ct) => meetings.AssignAsync(id, req, ct);

    /// <summary>Saves a participant/speaker mapping from the review screen and previews other matching unassigned meetings.</summary>
    [HttpPost("{id:guid}/mappings")]
    public Task<TestMappingResultDto> SaveMapping(Guid id, SaveMappingFromReviewRequest req, CancellationToken ct) => unassigned.SaveMappingAsync(id, req, ct);
}

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardService dashboard) : ControllerBase
{
    [HttpGet]
    public Task<DashboardDto> Get(CancellationToken ct) => dashboard.GetAsync(ct);
}
