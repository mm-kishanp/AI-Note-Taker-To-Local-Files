# GranolaPI.Worker (scheduled sync)

Last reviewed: 2026-10-28

- Type: Worker (library hosted inside the API process)
- Location: `src/GranolaPI.Worker/`
- Status: Active
- Confidence: High
- Part of: [project.md](../../project.md)

## What it is

A `BackgroundService` that starts incremental syncs on a schedule and marks interrupted runs after a restart. It is a separate project so the scheduling concern can be enabled/disabled (`Worker:Enabled`) and kept out of the API, but it always runs in the API process (no separate host).

## Responsibilities

- 30-second tick; start a scheduled Incremental run when due.
- Start-up recovery: mark unfinished runs Interrupted.
- Graceful shutdown: cancel and wait up to 10 s.

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-003 Scheduled automatic sync | owns | [FEAT-003](../../features/FEAT-003-scheduled-automatic-sync.md) |
| FEAT-017 Sync operations | contributes (recovery) | [FEAT-017](../../features/FEAT-017-sync-operations.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `SynchronizationWorker.ExecuteAsync`; registered by `AddSynchronizationWorker()` | `SynchronizationWorker.cs`, `Program.cs:28` |
| Runtime / framework | `net10.0`, Microsoft.Extensions.Hosting.Abstractions 10.0.5, EF Core 10.0.5 | csproj |
| Build | part of the solution | — |
| Run locally | runs with the API unless `Worker__Enabled=false` | `appsettings.json` |
| Tests | none (disabled in integration tests) | `TestHost.cs:200` |
| Deployed as | inside the API | — |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| GranolaPI.Application | sub-project | `SyncCoordinator`, `SyncHistoryService`, settings, secrets | csproj |

## Interfaces

- **Exposes:** `WorkerRegistration.AddSynchronizationWorker`.
- **Consumes:** `SyncCoordinator`, `ISettingsService`, `ISecretProvider`, `IAppDbContext`.

## Data

Reads `SyncRuns` and the `sync` settings section.

## Configuration

`Worker:Enabled`; `sync.autoSyncEnabled`, `sync.intervalMinutes`.

## Ownership and conventions

Single file.

## Risks and gaps

- RF-004 (requires a Granola key), RF-008 (uses `DateTime.UtcNow`, untested).
