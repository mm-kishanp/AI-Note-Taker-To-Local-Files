# GranolaPI.Api (API host)

Last reviewed: 2026-10-28

- Type: API (also hosts the worker and the SPA)
- Location: `src/GranolaPI.Api/`
- Status: Active
- Confidence: High
- Part of: [project.md](../../project.md)

## What it is

The single deployable process. It wires everything together, exposes the REST API that the React UI calls, protects it, runs database migrations at start-up, hosts the scheduled sync worker and serves the built UI. It is separate from Application/Infrastructure only to keep HTTP concerns out of the business code.

## Responsibilities

- Composition root (`Program.cs`): paths, file logger, DI, JSON options, problem details, OpenAPI, CORS.
- Request protection and error mapping (`Middleware/Middleware.cs`).
- 11 controllers in 3 files, 57 endpoints (`Controllers/`; see [api-catalog.md](../../api-catalog.md)).
- Start-up EF Core migration and logging configuration.
- Static hosting of `wwwroot/` with SPA fallback; JSON 404 for unknown `/api` paths.

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-020 Request protection | owns | [FEAT-020](../../features/FEAT-020-request-protection.md) |
| FEAT-001/002/017 Sync | exposes (controllers) | [FEAT-001](../../features/FEAT-001-granola-meeting-sync.md), [FEAT-017](../../features/FEAT-017-sync-operations.md) |
| FEAT-008, 009, 010, 011, 013, 014, 015, 016, 018, 019 | exposes | see [feature-inventory.md](../../feature-inventory.md) |
| FEAT-021 Logging | contributes (registration, runtime settings) | [FEAT-021](../../features/FEAT-021-file-logging.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `Program.cs` (top-level statements; `public partial class Program` for tests) | file |
| Runtime / framework | ASP.NET Core, `net10.0`; Microsoft.AspNetCore.OpenApi 10.0.9, Swashbuckle.AspNetCore.SwaggerUI 10.3.1 | `GranolaPI.Api.csproj` |
| Build | `dotnet build GranolaProjectIntelligence.sln` (Documented) | `ci.yml` |
| Run locally | `dotnet run --project src/GranolaPI.Api` → `http://127.0.0.1:5080` (Documented) | `launchSettings.json` |
| Tests | `tests/GranolaPI.IntegrationTests` (30 tests via `WebApplicationFactory<Program>`) | `TestHost.cs` |
| Deployed as | `dotnet publish` exe (desktop) or Docker image (port 8080) | [deployment.md](../../deployment.md) |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| GranolaPI.Application | sub-project | Use cases, DTOs | csproj |
| GranolaPI.Infrastructure | sub-project | Persistence, clients, logging | csproj |
| GranolaPI.Worker | sub-project | Scheduled sync | csproj |
| SQLite file | data store | State | `Program.cs:19` |

## Interfaces

- **Exposes:** REST `/api/*` ([api-catalog.md](../../api-catalog.md)); `/openapi/v1.json` and `/swagger` in Development; static SPA.
- **Consumes:** Application services only.

## Data

Creates `data/` and the SQLite DB path; applies migrations ([data-model.md](../../data-model.md)).

## Configuration

`Urls`, `AllowedHosts`, `App:BaseDirectory`, `App:DataDirectory`, `ConnectionStrings:Default`, `Worker:Enabled`, `Security:*`, `Logging:*` (`appsettings.json`).

## Ownership and conventions

No CODEOWNERS. Controllers are thin, grouped several per file (`SystemControllers.cs`).

## Risks and gaps

- RF-003 duplicate `LogSettingsState`; SEC-001 token path untested; minor layering deviations (service locator in `MeetingsController`).
