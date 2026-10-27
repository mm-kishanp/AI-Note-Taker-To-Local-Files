using GranolaPI.Application.Classification;
using GranolaPI.Application.DTOs;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Projects;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace GranolaPI.Api.Controllers;

[ApiController]
[Route("api/projects")]
public sealed class ProjectsController(ProjectService projects, ClassificationService classification, SyncCoordinator sync) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ProjectDto>> List([FromQuery] string? search, [FromQuery] bool includeArchived = false, CancellationToken ct = default) =>
        projects.ListAsync(search, includeArchived, ct);

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create(ProjectInput input, CancellationToken ct)
    {
        var created = await projects.CreateAsync(input, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public Task<ProjectDto> Get(Guid id, CancellationToken ct) => projects.GetAsync(id, ct);

    [HttpPut("{id:guid}")]
    public Task<ProjectDto> Update(Guid id, ProjectInput input, CancellationToken ct) => projects.UpdateAsync(id, input, ct);

    [HttpPost("{id:guid}/archive")]
    public Task<ProjectDto> Archive(Guid id, CancellationToken ct) => projects.SetStatusAsync(id, ProjectStatus.Archived, ct);

    [HttpPost("{id:guid}/restore")]
    public Task<ProjectDto> Restore(Guid id, CancellationToken ct) => projects.SetStatusAsync(id, ProjectStatus.Active, ct);

    [HttpPost("{id:guid}/rules")]
    public Task<RuleDto> AddRule(Guid id, RuleInput input, CancellationToken ct) => projects.AddRuleAsync(id, input, ct);

    /// <summary>Dry run: shows which meetings the given (or saved) rules would route, without changing anything.</summary>
    [HttpPost("{id:guid}/test-mapping")]
    public async Task<TestMappingResultDto> TestMapping(Guid id, TestMappingRequest req, CancellationToken ct) =>
        await projects.TestMappingAsync(id, req, await classification.LoadContextAsync(ct), ct);

    /// <summary>Re-classifies and re-exports the confirmed meetings with the saved rules (manual assignments are kept).</summary>
    [HttpPost("{id:guid}/apply-mapping")]
    public ActionResult<SyncStartResult> ApplyMapping(Guid id, ApplyRulesRequest req)
    {
        if (!req.Confirm) throw ValidationException.Single("confirm", "Confirm before re-classifying meetings in bulk.");
        if (req.MeetingIds is null || req.MeetingIds.Count == 0) throw ValidationException.Single("meetingIds", "Select at least one meeting.");
        var result = sync.TryStart(new SyncRequest(SyncMode.ReExport, SyncTrigger.Manual, req.MeetingIds, Reclassify: true));
        return result.Started ? Accepted(result) : Conflict(result);
    }

    [HttpGet("config/export")]
    public Task<ProjectConfigExport> ExportConfig(CancellationToken ct) => projects.ExportConfigAsync(ct);

    [HttpPost("config/import")]
    public Task<ImportResultDto> ImportConfig(ProjectConfigExport config, CancellationToken ct) => projects.ImportConfigAsync(config, ct);
}

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(ProjectService projects) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ClientDto>> List(CancellationToken ct) => projects.ListClientsAsync(ct);

    [HttpPost]
    public Task<ClientDto> Create(ClientInput input, CancellationToken ct) => projects.SaveClientAsync(null, input, ct);

    [HttpPut("{id:guid}")]
    public Task<ClientDto> Update(Guid id, ClientInput input, CancellationToken ct) => projects.SaveClientAsync(id, input, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await projects.DeleteClientAsync(id, ct);
        return NoContent();
    }
}
