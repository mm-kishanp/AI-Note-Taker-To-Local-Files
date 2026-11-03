# Chroniq.Infrastructure (adapters)

Last reviewed: 2026-10-28

- Type: Library
- Location: `src/Chroniq.Infrastructure/`
- Status: Active
- Confidence: High
- Part of: [project.md](../../project.md)

## What it is

Implementations of the Application ports: SQLite persistence and migrations, the Granola and Fireflies HTTP clients, the optional Anthropic classifier, credential stores, the file logger and the OS shell launcher.

## Responsibilities

- `Persistence/`: `AppDbContext`, `SettingsService`, `SqlSyncLease`, EF migrations.
- `Granola/GranolaApiClient` + `GranolaRequestThrottle`; `Fireflies/FirefliesApiClient` + `FirefliesRequestThrottle`.
- `Ai/AnthropicAiClassifier`.
- `Security/CredentialStores.cs`: `WindowsCredentialStore`, `ReadOnlyCredentialStore`, `InMemoryCredentialStore` (tests), `SecretProvider`.
- `Logging/FileLogger.cs`: `FileLoggerProvider`, `LogSettingsState`, `SecretRedactor`.
- `DependencyInjection.cs`: `AddInfrastructure`, `ShellLauncher`.

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-001 / FEAT-002 | owns the API clients | [FEAT-001](../../features/FEAT-001-granola-meeting-sync.md), [FEAT-002](../../features/FEAT-002-fireflies-call-import.md) |
| FEAT-005 | owns the Anthropic adapter | [FEAT-005](../../features/FEAT-005-ai-assisted-classification.md) |
| FEAT-015 | owns credential stores | [FEAT-015](../../features/FEAT-015-api-key-management.md) |
| FEAT-021 | owns the logger | [FEAT-021](../../features/FEAT-021-file-logging.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `AddInfrastructure(configuration, connectionString)` | `DependencyInjection.cs:24-53` |
| Runtime / framework | `net10.0`; EF Core SQLite 10.0.5, SQLitePCLRaw 3.0.5, Microsoft.Extensions.Http 10.0.5, Anthropic 13.3.0 | csproj |
| Build | part of the solution; migrations via `dotnet ef` | `dotnet-tools.json` |
| Run locally | not runnable alone | — |
| Tests | `GranolaApiClientTests`, `FirefliesTests` (unit); integration tests use the real `AppDbContext` with fakes for clients | test projects |
| Deployed as | inside the API | — |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| Chroniq.Application, Chroniq.Domain | sub-projects | Ports, entities | csproj |
| Granola API, Fireflies API, Anthropic API | external | INT-001–003 | [integrations.md](../../integrations.md) |
| Windows Credential Manager | OS | INT-004 | `CredentialStores.cs` |

## Interfaces

- **Exposes:** port implementations registered in DI.
- **Consumes:** INT-001–INT-005.

## Data

SQLite schema and migrations ([data-model.md](../../data-model.md)); log files.

## Configuration

`Security:UseWindowsCredentialManager`, secret names `GRANOLA_API_KEY`, `FIREFLIES_API_KEY`, `ANTHROPIC_API_KEY`, `Secrets:*`.

## Ownership and conventions

Typed `HttpClient`s with 100 s timeout and a shared user agent; throttles are singletons.

## Risks and gaps

- RF-003 (`LogSettingsState` registered again here), RF-007 (redactor), RF-017 (AI untested), duplicated retry loops (TD-004).
