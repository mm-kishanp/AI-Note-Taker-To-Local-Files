# Debugging Guide

Last reviewed: 2026-10-28

- Evidence scope: `src/GranolaPI.Api/Properties/launchSettings.json`, `appsettings*.json`, `Program.cs`, `FileLogger.cs`, `Middleware.cs`, `frontend/vite.config.ts`, `run.bat`, `stop.bat`, `tests/GranolaPI.IntegrationTests/TestHost.cs`, [docs/TROUBLESHOOTING.md](../TROUBLESHOOTING.md). All commands are **Documented** (read), none were run in this audit. No `.vscode/launch.json` or `.idea` run configurations are committed.
- Confidence: Medium

## Prerequisites

.NET SDK 10.0, Node.js 22+, a Granola and/or Fireflies API key for real data (tests need none). Use a throw-away export folder when debugging so real notes are not touched.

## Debug each sub-project

| Sub-project | How to start in debug mode | IDE configuration | Ports / URLs | Evidence |
| --- | --- | --- | --- | --- |
| API host (includes Worker, Application, Infrastructure, Domain) | `dotnet run --project src/GranolaPI.Api` (profile `http`, `ASPNETCORE_ENVIRONMENT=Development`), or F5 in Visual Studio / Rider / VS Code C# Dev Kit on `GranolaPI.Api` | `Properties/launchSettings.json` profile `http` | `http://127.0.0.1:5080`, Swagger `/swagger`, OpenAPI `/openapi/v1.json` | `launchSettings.json` |
| Frontend | `cd frontend; npm run dev` (or `run.bat dev`, which opens API + Vite windows) | none committed; use browser devtools | `http://localhost:5173` (strict port), proxies `/api` and `/openapi` | `vite.config.ts` |
| Node CLI (legacy) | `node --inspect tools/node-cli/granola-sync.js --dry-run` | none | — | `tools/node-cli/package.json` |

Development mode sets `App:BaseDirectory` to the repo root (`appsettings.Development.json`), so the database is `<repo>/data/granola-pi.db` and the default export root is `<repo>/GranolaProjectKnowledge`.

**Frontend gotcha (RF-001):** committed `.js` files next to the `.tsx` sources are resolved first by Vite and Vitest, and `vite.config.js` overrides `vite.config.ts`. If an edit to a `.tsx` file has no effect, this is why. Delete the generated `.js` twins locally (PIDA-001).

## Attach to a running process or container

- Desktop: attach the .NET debugger to `GranolaPI.Api.exe` / `dotnet` (process started by `run.bat` in a window titled `Granola PI*`).
- Container: the runtime image has no debugger; inspect logs in the `granola-pi-state` volume (`/data/state/logs`) or run the API locally against a copy of the database.

## Local configuration for debugging

- Disable the scheduler while debugging a manual run: set `Worker__Enabled=false` (environment) or turn off automatic sync in Settings.
- Use environment variables for keys instead of Credential Manager: `GRANOLA_API_KEY`, `FIREFLIES_API_KEY`, `ANTHROPIC_API_KEY`, plus `Security__UseWindowsCredentialManager=false`.
- Point the app at a scratch database with `ConnectionStrings__Default="Data Source=<path>"` or a different `App__DataDirectory`.

## Test data

- Integration tests use fake Granola/Fireflies clients and a temporary base directory (`TestHost.cs`); copy that pattern to reproduce a sync bug without network access.
- `config/project-mappings.example.json` can be imported from the Projects page to create sample projects and rules.
- `scripts/e2e-live.mjs` runs a full live scenario against your own account; it overwrites `export.rootDirectory` and `sync.initialImportFrom` and leaves a project behind (RF-012), so use a scratch install.

## Logs

- Where logs go locally: `data/logs/granola-pi-yyyyMMdd.log` (JSON lines: `ts`, `level`, `category`, `message`, structured properties, `traceId`, `exception`). The console logger is also active under `dotnet run`.
- How to raise the log level: Settings → Logging (minimum level, directory, retention), or `Logging:LogLevel:Default` in configuration. **Known bug (RF-003):** level/directory changes saved in the UI only take effect after a restart.
- How to find one request's logs: every API response and problem detail carries `X-Correlation-ID` / `correlationId`. The file logger writes `traceId` from `Activity.Current`, which is not always the same value as the correlation ID (the middleware prefers a caller-supplied header). Search by path and time if the IDs differ.
- Sync failures: the Sync History page and `GET /api/sync/history/{id}` show per-meeting outcomes, error categories and messages; meetings carry `LastSyncError`, `FailureCount`, `NextRetryAt`.

## Debugging tests

- `dotnet test --filter "FullyQualifiedName~SyncIntegrationTests"`; debug a single test from the IDE test explorer.
- `cd frontend; npx vitest <pattern>` or `npm run test:watch`.
- Playwright: `npm run e2e -- --debug` against a running app (`GPI_E2E_URL`, default `http://127.0.0.1:5080`).

## Background jobs, queues, and scheduled tasks

- `SynchronizationWorker` ticks every 30 s and starts an incremental run when auto-sync is on, a **Granola** key exists and `IntervalMinutes` (min 5) has passed since the last manual or scheduled run (`SynchronizationWorker.cs:58-78`). Fireflies-only setups never auto-sync (RF-004).
- A run that seems stuck: check `GET /api/sync/status`, cancel with `POST /api/sync/cancel`. The cross-process lease (`SyncLeases` row `sync`) expires after 10 minutes without renewal; interrupted runs are marked at the next start-up (`RecoverAsync`).
- Breakpoints: `SyncEngine.RunAsync` (orchestration), `MeetingProcessor.ProcessAsync` (one meeting), `MeetingExporter.WriteAsync` (file decisions), `ClassificationEngine.Classify` (routing).

## Diagnosing deployed environments

Read-only steps: `GET /api/health`; open Sync History; read the latest log file; open the SQLite database read-only with a SQLite browser (copy it first; it contains meeting content). Do not edit `ExportRecords` by hand — use Reconcile (`POST /api/sync/start {"mode":"Reconcile"}`) to recreate missing files.

## Common problems

| Symptom | Likely cause | Fix | Evidence |
| --- | --- | --- | --- |
| 409 "Synchronization already running" | A run is in progress in this or another process (lease) | Wait, or cancel; lease expires after 10 min | `SyncCoordinator.cs:22-23`, `SyncEngine.cs:35` |
| 424 "Granola rejected the request" / run fails with Authentication | Key revoked or wrong scope | Replace the key in Settings | `Middleware.cs:23`, `GranolaApiClient.cs:282-283` |
| 403 "Missing X-GPI-Request header" when scripting | CSRF guard | Send `X-GPI-Request: 1` on POST/PUT/DELETE | `Middleware.cs:97-102` |
| 403 "Remote access is disabled" | Calling from another host without a token | Set `Security:ApiToken` and send `Authorization: Bearer` | `Middleware.cs:91-94` |
| Meeting in Conflict | File edited locally and source changed | Resolve on the meeting page (keep local / overwrite) | `MeetingExporter.cs:315` |
| File "Missing" after a sync | You deleted/moved an exported file and nothing changed upstream | Run Reconcile to recreate | `MeetingExporter.cs:267-272` |
| Transcript shows Failed | Transcript retrieval failed; notes still exported | Automatic retry with backoff, or Retry on the meeting | `MeetingProcessor.cs:92-97` |
| UI change has no effect in dev | Committed `.js` shadows `.tsx` | Remove generated `.js` | RF-001 |
| `run.bat` closes another app on port 5080 / `stop.bat` kills another Vite server | Scripts force-kill by port | Free the port or edit the scripts | RF-010 |

More: [docs/TROUBLESHOOTING.md](../TROUBLESHOOTING.md).

## Useful tools and commands

- `dotnet ef migrations add <Name> --project src/GranolaPI.Infrastructure --startup-project src/GranolaPI.Api` — new migration (tool from `dotnet-tools.json`; Documented).
- Swagger UI at `/swagger` (Development) — try endpoints with the CSRF header.
- `python scripts/doc-freshness.py` — check this knowledge base for stale pages.
