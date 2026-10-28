# GranolaPI.Application (use cases)

Last reviewed: 2026-10-28

- Type: Library
- Location: `src/GranolaPI.Application/`
- Status: Active
- Confidence: High
- Part of: [project.md](../../project.md)

## What it is

The heart of the product: synchronisation orchestration, meeting processing, classification, Markdown rendering and export, project/meeting services, settings models and DTOs. It defines the ports that Infrastructure implements, so it can be tested with fakes.

## Responsibilities

- `Synchronization/`: `SyncCoordinator`, `SyncEngine`, `MeetingProcessor`, progress, lease port.
- `Classification/`: `ClassificationEngine` (pure), `ClassificationService`, AI port.
- `Export/`: `MeetingExporter`, `MarkdownRenderer`, `ExportLayout`, `PathTemplates`/`FileNames`/`SafePath`, `AtomicFile`, `ProjectContextService`, `ExportBrowserService`.
- `Meetings/`: `MeetingService`, `MeetingIngestor`, `MeetingJson`, `UnassignedService`, `DashboardService`, `SyncHistoryService`.
- `Projects/ProjectService`; `Granola/` (client port, models, `CredentialService`); `Fireflies/` (models, mapper); `Settings/` (records + validator); `DTOs/`; `Interfaces/`.

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-001, 002, 004, 006, 007, 008, 009, 010, 011, 012, 013, 014, 015, 016, 017, 018, 019 | owns the business logic | [feature-inventory.md](../../feature-inventory.md) |
| FEAT-005 AI classification | owns the port and orchestration | [FEAT-005](../../features/FEAT-005-ai-assisted-classification.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `DependencyInjection.AddApplication()` | `DependencyInjection.cs` |
| Runtime / framework | `net10.0`; EF Core 10.0.5 (abstractions via `IAppDbContext`), Logging/Options abstractions | csproj |
| Build | part of the solution | — |
| Run locally | not runnable alone | — |
| Tests | `tests/GranolaPI.UnitTests` (classification, export, Markdown/paths) and integration tests | test projects |
| Deployed as | inside the API | — |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| GranolaPI.Domain | sub-project | Entities, enums, rules | csproj |
| Microsoft.EntityFrameworkCore | package | LINQ over `IAppDbContext` | csproj |

## Interfaces

- **Exposes:** services registered in `AddApplication` (singletons: `SyncProgress`, `SyncEngine`, `SyncCoordinator`; scoped: the rest).
- **Consumes (ports):** `IAppDbContext`, `ISettingsService`, `ISyncLease`, `IGranolaApiClient`, `IFirefliesApiClient`, `IAiClassifier`, `ISecretProvider`, `ICredentialStore`, `IShellLauncher`, `IAppPaths`, `IClock`.

## Data

All entities via `IAppDbContext`; files via `AtomicFile`.

## Configuration

Settings records in `Settings/AppSettings.cs` (stored in DB).

## Ownership and conventions

Feature folders; sealed primary-constructor services; records for DTOs/settings.

## Risks and gaps

- Source-specific naming (TD-003); retry/manual-action concurrency (RF-019); migration feature untested (RF-018).
