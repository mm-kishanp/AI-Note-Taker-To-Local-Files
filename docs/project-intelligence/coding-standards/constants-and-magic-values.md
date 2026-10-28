# Constants and Magic Values

Last reviewed: 2026-10-28

- Evidence scope: API clients, sync engine, worker, export services, middleware, DI registration, settings defaults; frontend via sub-audit.
- Status: Mostly consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Tunables a user may change live in typed settings with defaults (`ExportSettings`, `SyncSettings`, `ClassificationSettings`, `LoggingSettings`) | Verified: `src/GranolaPI.Application/Settings/AppSettings.cs` | `SettingsValidator` |
| Fixed limits are `private const` / `static readonly` with a descriptive name | Inferred: `MaxNodes`, `MaxReadBytes` (`ExportBrowserService.cs:21-22`), `LeaseDuration` (`SyncEngine.cs:30`), `Tick` (`SynchronizationWorker.cs:23`), `MaxBackoff`, `BaseAddress`/`Endpoint`, `MaxStemLength`, `TempSuffix` | Review |
| Header names and setting/secret keys are constants | Verified: `RequestProtectionMiddleware.CsrfHeader`, `SettingKeys`, `SecretNames` | Review |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| No unexplained literals in conditions | Partly | `free < 100L * 1024 * 1024` (`ExportBrowserService.cs:151`); retry backoff `5 * Math.Pow(2, …)`, `720` minutes (`MeetingProcessor.cs:180`, explained by a comment); `Take(500)` folder listing (`ExportBrowserService.cs:177`); `Truncate(…, 1000)` (`SyncEngine.cs:192`) |
| No hardcoded URLs/hosts | Follows | External endpoints are named constants (`GranolaApiClient.BaseAddress`, `FirefliesApiClient.Endpoint`); local defaults in `appsettings.json` |
| No hardcoded client IDs | Not applicable | — |
| Timeouts named | Partly | `TimeSpan.FromSeconds(100)` HTTP timeouts inline in DI (`src/GranolaPI.Infrastructure/DependencyInjection.cs:39-46`); `TimeSpan.FromSeconds(10)` in `StopAsync`; `"GranolaProjectIntelligenceSync/1.0"` user agent duplicated |
| Frontend | Partly | Polling intervals (1.5 s / 10 s / 30 s / 60 s), notification timeouts, page sizes inline (sub-audit) |

## How the code does it today

Copy `ExportBrowserService`'s `MaxNodes`/`MaxReadBytes` constants and the settings-record defaults in `AppSettings.cs`.

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `src/GranolaPI.Infrastructure/DependencyInjection.cs:39-46` | Duplicated inline timeout and user-agent string | Low | file |
| `ExportBrowserService.cs:151,177` | Inline 100 MB warning threshold and 500-entry cap | Low | file |
| `frontend/src/hooks/useSystem.ts:7-20` | Inline polling intervals | Low | sub-audit |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Name the HTTP timeout and user agent once (`HttpDefaults`) | Code | Single place to tune | S | P3 | TD-004 |
| Collect frontend polling intervals in one `constants.ts` | Code | Easier tuning | S | P3 | TD-004 |
