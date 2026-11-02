using System.Reflection;
using GranolaPI.Application.DTOs;
using GranolaPI.Application.Export;
using GranolaPI.Application.Granola;
using GranolaPI.Application.Interfaces;
using GranolaPI.Application.Meetings;
using GranolaPI.Application.Settings;
using GranolaPI.Application.Synchronization;
using GranolaPI.Domain.Enums;
using GranolaPI.Infrastructure.Logging;
using GranolaPI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace GranolaPI.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db, ISecretProvider secrets, SyncProgress progress) : ControllerBase
{
    [HttpGet]
    public async Task<HealthDto> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        return new HealthDto(dbOk ? "Healthy" : "Degraded",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
            dbOk, secrets.Get(SecretNames.GranolaApiKey) is not null || secrets.Get(SecretNames.FirefliesApiKey) is not null
                || secrets.Get(SecretNames.WhisperAiApiKey) is not null,
            progress.IsRunning, DateTimeOffset.UtcNow);
    }
}

[ApiController]
[Route("api/sync")]
public sealed class SyncController(SyncCoordinator coordinator, SyncProgress progress, SyncHistoryService history) : ControllerBase
{
    [HttpPost("start")]
    public ActionResult<SyncStartResult> Start(StartSyncRequest req)
    {
        var result = coordinator.TryStart(new SyncRequest(req.Mode, SyncTrigger.Manual, req.MeetingIds, req.Reclassify, req.Force));
        return result.Started ? Accepted(result) : Conflict(result);
    }

    [HttpPost("retry-failed")]
    public ActionResult<SyncStartResult> RetryFailed()
    {
        var result = coordinator.TryStart(new SyncRequest(SyncMode.RetryFailed, SyncTrigger.Manual));
        return result.Started ? Accepted(result) : Conflict(result);
    }

    [HttpPost("cancel")]
    public IActionResult Cancel() => coordinator.Cancel() ? Accepted() : NoContent();

    [HttpGet("status")]
    public SyncProgressSnapshot Status() => progress.Snapshot;

    [HttpGet("history")]
    public Task<PagedResponse<SyncRunDto>> History([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        history.ListAsync(page, pageSize, ct);

    [HttpGet("history/{id:guid}")]
    public async Task<object> Run(Guid id, CancellationToken ct)
    {
        var (run, items) = await history.GetAsync(id, ct);
        return new { run, items };
    }
}

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(ISettingsService settings, IAppPaths paths, LogSettingsState logState, CredentialService credentials,
    ICredentialStore store, ISecretProvider secrets) : ControllerBase
{
    [HttpGet]
    public async Task<SettingsDto> Get(CancellationToken ct) => await Dto(await settings.GetAsync(ct));

    [HttpPut]
    public async Task<SettingsDto> Update(UpdateSettingsRequest req, CancellationToken ct)
    {
        var errors = SettingsValidator.Validate(req.Export, req.Sync, req.Classification, req.Logging);
        if (errors.Count > 0) throw new ValidationException(errors);
        if (req.Export is not null) await settings.SaveSectionAsync(SettingKeys.Export, req.Export, ct);
        if (req.Sync is not null) await settings.SaveSectionAsync(SettingKeys.Sync, req.Sync, ct);
        if (req.Classification is not null) await settings.SaveSectionAsync(SettingKeys.Classification, SettingsValidator.Normalize(req.Classification), ct);
        if (req.Logging is not null)
        {
            await settings.SaveSectionAsync(SettingKeys.Logging, req.Logging, ct);
            ApplyLogging(logState, req.Logging, paths.BaseDirectory);
        }
        return await Dto(await settings.GetAsync(ct));
    }

    public static void ApplyLogging(LogSettingsState state, LoggingSettings s, string baseDir)
    {
        state.MinimumLevel = Enum.TryParse<LogLevel>(s.MinimumLevel, out var level) ? level : LogLevel.Information;
        state.Directory = Path.IsPathRooted(s.LogDirectory) ? s.LogDirectory : Path.Combine(baseDir, s.LogDirectory);
        state.RetentionDays = s.RetentionDays;
    }

    private Task<SettingsDto> Dto(AppSettingsSnapshot s)
    {
        ResolvedPathsDto resolved;
        try
        {
            var layout = new ExportLayout(s.Export, paths.BaseDirectory);
            resolved = new ResolvedPathsDto(layout.Root, layout.NotesRoot, layout.TranscriptsRoot, paths.BaseDirectory);
        }
        catch (ArgumentException)
        {
            resolved = new ResolvedPathsDto("", "", "", paths.BaseDirectory);
        }
        return Task.FromResult(new SettingsDto(s.Export, s.Sync, s.Classification, s.Logging, resolved));
    }

    // ---- Granola credentials (never returned in full) ----

    [HttpGet("credentials/granola")]
    public Task<CredentialStatusDto> GranolaStatus(CancellationToken ct) => credentials.StatusAsync(ct);

    [HttpPut("credentials/granola")]
    public Task<CredentialStatusDto> SetGranola(SetCredentialRequest req, CancellationToken ct) => credentials.SetAsync(req, ct);

    [HttpDelete("credentials/granola")]
    public Task<CredentialStatusDto> DeleteGranola(CancellationToken ct) => credentials.DeleteAsync(ct);

    // ---- Fireflies credentials (never returned in full) ----

    [HttpGet("credentials/fireflies")]
    public Task<CredentialStatusDto> FirefliesStatus(CancellationToken ct) => credentials.StatusAsync(MeetingSource.Fireflies, ct);

    [HttpPut("credentials/fireflies")]
    public Task<CredentialStatusDto> SetFireflies(SetCredentialRequest req, CancellationToken ct) => credentials.SetAsync(MeetingSource.Fireflies, req, ct);

    [HttpDelete("credentials/fireflies")]
    public Task<CredentialStatusDto> DeleteFireflies(CancellationToken ct) => credentials.DeleteAsync(MeetingSource.Fireflies, ct);

    // ---- WhisperAI credentials (never returned in full) ----

    [HttpGet("credentials/whisperai")]
    public Task<CredentialStatusDto> WhisperAiStatus(CancellationToken ct) => credentials.StatusAsync(MeetingSource.WhisperAi, ct);

    [HttpPut("credentials/whisperai")]
    public Task<CredentialStatusDto> SetWhisperAi(SetCredentialRequest req, CancellationToken ct) => credentials.SetAsync(MeetingSource.WhisperAi, req, ct);

    [HttpDelete("credentials/whisperai")]
    public Task<CredentialStatusDto> DeleteWhisperAi(CancellationToken ct) => credentials.DeleteAsync(MeetingSource.WhisperAi, ct);

    // ---- Optional AI provider key ----

    [HttpGet("credentials/ai")]
    public AiStatusDto AiStatus()
    {
        var s = secrets.Get(SecretNames.AiApiKey);
        return new AiStatusDto(s is not null, s is null ? null : CredentialService.Mask(s.Value), store.CanWrite);
    }

    [HttpPut("credentials/ai")]
    public AiStatusDto SetAi(SetCredentialRequest req)
    {
        var key = req.ApiKey?.Trim() ?? "";
        if (key.Length < 10 || key.Any(char.IsWhiteSpace)) throw ValidationException.Single("apiKey", "Enter a valid API key.");
        if (!store.CanWrite) throw ValidationException.Single("apiKey", "The credential store is read-only here; set ANTHROPIC_API_KEY instead.");
        store.Write(SecretNames.AiApiKey, key);
        return AiStatus();
    }

    [HttpDelete("credentials/ai")]
    public AiStatusDto DeleteAi()
    {
        if (store.CanWrite) store.Delete(SecretNames.AiApiKey);
        return AiStatus();
    }
}

[ApiController]
[Route("api/granola")]
public sealed class GranolaController(CredentialService credentials, IGranolaApiClient api) : ControllerBase
{
    [HttpPost("test-connection")]
    public Task<ConnectionTestDto> Test(CancellationToken ct) => credentials.TestAsync(ct);

    /// <summary>Granola folders visible to the key (useful for GranolaFolder rules).</summary>
    [HttpGet("folders")]
    public async Task<IReadOnlyList<GranolaFolder>> Folders(CancellationToken ct) => await api.ListFoldersAsync(ct);
}

[ApiController]
[Route("api/fireflies")]
public sealed class FirefliesController(CredentialService credentials) : ControllerBase
{
    [HttpPost("test-connection")]
    public Task<ConnectionTestDto> Test(CancellationToken ct) => credentials.TestAsync(MeetingSource.Fireflies, ct);
}

[ApiController]
[Route("api/whisperai")]
public sealed class WhisperAiController(CredentialService credentials) : ControllerBase
{
    [HttpPost("test-connection")]
    public Task<ConnectionTestDto> Test(CancellationToken ct) => credentials.TestAsync(MeetingSource.WhisperAi, ct);
}

[ApiController]
[Route("api/exports")]
public sealed class ExportsController(ExportBrowserService browser) : ControllerBase
{
    /// <summary>root = "root" | "notes" | "transcripts".</summary>
    [HttpGet("tree")]
    public Task<ExportTreeDto> Tree([FromQuery] string root = "root", CancellationToken ct = default) => browser.TreeAsync(root, ct);

    [HttpGet("file")]
    public Task<FileContentDto> File([FromQuery] string root, [FromQuery] string path, CancellationToken ct) => browser.ReadFileAsync(root, path, ct);

    [HttpPost("open")]
    public async Task<IActionResult> Open([FromQuery] string root = "root", [FromQuery] string? path = null, CancellationToken ct = default)
    {
        await browser.OpenAsync(root, path, ct);
        return NoContent();
    }

    [HttpPost("validate-path")]
    public ValidatePathResultDto Validate(ValidatePathRequest req) => browser.ValidatePath(req);

    [HttpPost("migrate-root")]
    public Task<MigrateRootResultDto> Migrate(MigrateRootRequest req, CancellationToken ct) => browser.MigrateRootAsync(req, ct);

    /// <summary>Folder picker support: lists sub-folders (names only) of a local path, or drives when empty.</summary>
    [HttpGet("directories")]
    public DirectoryListingDto Directories([FromQuery] string? path) => browser.ListDirectories(path);
}
