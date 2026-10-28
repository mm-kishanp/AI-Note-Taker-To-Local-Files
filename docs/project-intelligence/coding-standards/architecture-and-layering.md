# Architecture and Layering

Last reviewed: 2026-10-28

- Evidence scope: all `.csproj` references; all 11 controllers (3 files); both `DependencyInjection.cs`; Application services sampled (`SyncEngine`, `MeetingProcessor`, `MeetingExporter`, `ExportBrowserService`, `ClassificationService`, `CredentialService`); frontend layering via sub-audit.
- Status: Mostly consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Dependency direction Domain ← Application ← Infrastructure/Worker ← Api | Verified: `ProjectReference`s; described in `docs/ARCHITECTURE.md` | Compiler (project references) |
| Application defines ports; Infrastructure implements them (`IAppDbContext`, `IGranolaApiClient`, `ISecretProvider`, …) | Inferred: all external access in sampled Application code goes through interfaces | Review |
| Controllers are thin: one service call per action, no business rules | Inferred: 50 of 57 actions are one-line forwards | Review |
| Long-running work runs via `SyncCoordinator`, never inside a request | Inferred: all sync starts return 202 (`SystemControllers.cs`, `ProjectsController.cs`, `MeetingsController.cs`) | Review |
| Frontend: page → hook → `frontend/src/services/api.ts`; no `fetch` in components | Inferred (sub-audit) | Review |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Dependency direction respected | Follows | csproj references; Application references only Domain + EF Core abstractions |
| No data-access or infrastructure types in controllers | Partly | `HealthController` injects `AppDbContext` directly (`SystemControllers.cs:18`); `SettingsController` uses `LogSettingsState` from Infrastructure (`:69`) |
| No service locator | Partly | `MeetingsController.LayoutFor` uses `HttpContext.RequestServices.GetRequiredService` (`MeetingsController.cs:63-68`); `SyncEngine` resolves scoped services from `IServiceScopeFactory` (legitimate for a singleton orchestrator) |
| Entities not returned from the API | Follows | Controllers return DTOs (`Dtos.cs`) |
| Business logic outside controllers | Partly | Validation in `SettingsController.SetAi` (`:145-152`) and `ProjectsController.ApplyMapping` (`:50-51`) lives in controllers |
| Application layer free of EF Core | Does not (by design) | Application uses `Microsoft.EntityFrameworkCore` (`IAppDbContext` exposes `DbSet<>`; services call `ToListAsync`) — accepted trade-off |

## How the code does it today

Good examples to copy: `MeetingsController` actions that forward to `MeetingService` (`MeetingsController.cs:14-52`); `GranolaApiClient` implementing the Application port `IGranolaApiClient`; `ExportBrowserService` taking `IShellLauncher` so OS calls stay in Infrastructure.

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `MeetingsController.LayoutFor` | Service locator via `RequestServices` instead of constructor injection or a service method | Low | `MeetingsController.cs:63-68` |
| `HealthController` | Uses `AppDbContext` (Infrastructure) directly | Low | `SystemControllers.cs:18-28` |
| `SettingsController.SetAi/DeleteAi` | Validation and store writes in the controller, unlike Granola/Fireflies keys which go through `CredentialService` | Low | `SystemControllers.cs:137-159` |
| `ProjectsController.ApplyMapping` | Request validation in controller | Low | `ProjectsController.cs:50-51` |
| `ProjectsPage.tsx` | Calls `api.projects.exportConfig` directly instead of through a hook | Low | sub-audit |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Move AI key handling into `CredentialService` (a third `MeetingSource`-like key kind) | Code | One path for all secrets, consistent validation | S | P3 | TD-004 |
| Replace `LayoutFor` with a `MeetingService.OpenExportAsync` method | Code | Removes service locator | S | P3 | TD-004 |
| Document "Application may use EF Core via `IAppDbContext`" as an accepted rule | Standard | Avoid future debates | S | P3 | — |
