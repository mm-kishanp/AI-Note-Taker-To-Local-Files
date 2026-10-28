# Architecture

Granola Project Intelligence Sync is a modular monolith: one process hosts the REST API, the background synchronization worker, and the built React UI.

```text
            ┌──────────────── GranolaPI.Api (ASP.NET Core) ─────────────────┐
 Browser ──►│ RequestProtection (CSRF header, loopback/API token)            │
 (React)    │ Controllers ──► Application services                           │
            │ SynchronizationWorker (BackgroundService) ──► SyncCoordinator  │
            └──────────────┬──────────────────────────────┬─────────────────┘
                           │                              │
                 SQLite (operational state)     Export roots (Markdown)
                           │
               Granola public API (HTTPS, bearer key)
```

## Projects and responsibilities

| Project | Responsibility | Key types |
|---|---|---|
| `GranolaPI.Domain` | Entities and enums with no dependencies. Text normalisation and slug rules. | `Meeting`, `Project`, `Client`, `ClassificationRule`, `ExportRecord`, `SyncRun`, `TextNormalizer`, `Slug` |
| `GranolaPI.Application` | All business logic, independent of transport and storage technology. | `ClassificationEngine` (pure), `ClassificationService`, `MarkdownRenderer`, `ExportLayout`, `MeetingExporter`, `ProjectContextService`, `MeetingIngestor`, `MeetingProcessor`, `SyncEngine`, `SyncCoordinator`, services and DTOs |
| `GranolaPI.Infrastructure` | Adapters to the outside world. | `AppDbContext` (EF Core/SQLite + migrations), `SettingsService`, `SqlSyncLease`, `GranolaApiClient` + `GranolaRequestThrottle`, `WindowsCredentialStore`, `SecretProvider`, `FileLoggerProvider`, `AnthropicAiClassifier`, `ShellLauncher` |
| `GranolaPI.Worker` | Scheduled runs and restart recovery. | `SynchronizationWorker` |
| `GranolaPI.Api` | HTTP surface, error mapping, request protection, static hosting of the UI. | Controllers, `ApiExceptionHandler`, `RequestProtectionMiddleware` |
| `frontend/` | React UI that talks only to the documented REST API. | See `frontend/README.md` |

The Application layer depends on `IAppDbContext` (EF Core abstractions), not on SQLite. To move to SQL Server or PostgreSQL, swap the provider in `Infrastructure/DependencyInjection.cs` and generate new migrations. The schema stores enums as strings and uses no SQLite-specific features.

## Synchronization pipeline

Each meeting goes through this pipeline inside its own DI scope. Every step is persisted before the next starts:

1. **Discover**: page through `GET /v1/notes`, using `updated_after` from the last successful checkpoint (minus a configurable overlap), or `created_after` for the initial import. A note is skipped when its local `UpdatedAt` matches, nothing is pending, and its transcript did not fail.
2. **Fetch**: call `GET /v1/notes/{id}?include=transcript`. If that returns `413`, the transcript is fetched through the paged transcript endpoint instead.
3. **Persist**: normalise the note into `Meeting` (`MeetingIngestor`), compute `SourceContentHash`, set `ExportPending = true`, and save.
4. **Classify**: run `ClassificationService` (the deterministic engine, plus optional AI), then save.
5. **Export**: `MeetingExporter` resolves paths (sticky per `ExportRecord`), renders Markdown, and writes each file atomically after conflict checks. It then updates the notes and transcript records **separately** and saves.
6. **Record**: write per-meeting outcomes to `SyncRunItems`. Finally, refresh `PROJECT_CONTEXT.md` for the projects that were touched, then `ReviewQueue.md`, and advance the checkpoint only if listing completed.

A failure in one meeting never stops the others. Authentication and permission failures abort the run, because every later request would fail too.

### Concurrency and recovery

- `SyncCoordinator` allows one run per process and runs it in the background, so the UI stays responsive. Its progress is polled from `GET /api/sync/status`.
- `SqlSyncLease` is a compare-and-set row in `SyncLeases` that stops two processes sharing the same database from running at once. It is renewed while a run progresses.
- Bounded parallelism (`MaxConcurrency`, 1–8) is combined with a process-wide request throttle (≈4.5 requests/second) that stays under Granola's limits. A 429 pauses every request until `Retry-After` has passed.
- At startup, runs left `Running` are marked `Interrupted`. Meetings with `ExportPending`, a failed transcript, or a recorded error are picked up again by later runs, using per-meeting exponential backoff (5, 10, 20 … minutes, up to `MaxMeetingRetries`).

## Database and file-system consistency

The database and the file system cannot share a transaction, so the export is designed to be recoverable instead:

- Files are written to `.<name>.<guid>.gpi-tmp` in the destination folder, flushed, and renamed over the target. Stray temp files are removed at the start of each run.
- An `ExportRecord` is marked `Exported` only after its file is on disk. Notes and transcripts have separate records, so a failed transcript can be retried without rewriting the notes.
- Every generated file carries its source ID (`granola_note_id` or `fireflies_transcript_id`) and `content_hash` in its front matter. User edits, and files the app did not generate, can therefore be detected even when the database is lost.

## Data ownership

| Store | Contents |
|---|---|
| SQLite (`data/granola-pi.db`) | Normalised meeting records (including stored source content, so files can be regenerated without re-downloading), projects/clients/rules, export records, sync runs and items, settings, review audit, sync lease. |
| Export roots | Markdown notes, transcripts, `README.md`, `PROJECT_CONTEXT.md`, `ReviewQueue.md`. These are portable and self-describing. |
| OS credential store / environment | Granola API key, optional Anthropic API key. |

## API surface

The REST API is documented with OpenAPI (`/openapi/v1.json` and `/swagger` in Development; snapshot in `docs/openapi.json`). Main groups:

| Group | Purpose |
|---|---|
| `/api/dashboard` | Dashboard figures |
| `/api/meetings` | Query, detail, transcript, assign, reclassify, retry, re-export, and conflict review/resolution |
| `/api/projects`, `/api/clients` | CRUD, rules, test-mapping, apply-mapping, config import/export |
| `/api/unassigned` | Review queue and save-mapping |
| `/api/sync` | Start, retry-failed, cancel, status, history |
| `/api/settings` | Settings sections and masked credentials |
| `/api/granola` | Test connection, folders |
| `/api/exports` | Tree, file, open, validate-path, migrate-root, directory picker |
| `/api/health` | Health check |
