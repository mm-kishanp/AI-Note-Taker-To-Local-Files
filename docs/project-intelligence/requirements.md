# Requirements

Last reviewed: 2026-10-28

- Evidence scope: reverse-engineered from code, tests, `README.md` and `docs/`. Business goals are **Inferred** (no product brief exists); functional requirements are Verified against the cited code and tests unless marked.
- Confidence: Medium (goals) / High (functional)

## Business goals

- **BR-001 (Inferred, High):** Turn meeting notes and transcripts from Granola (and Fireflies) into a durable, project-organised Markdown knowledge base the user owns, so it can be read, versioned and given to coding agents. Evidence: `README.md:3-5`, export design.
- **BR-002 (Inferred, High):** Require minimal manual filing: route meetings automatically with explainable rules, and send only uncertain ones to review. Evidence: classification engine, review queue.
- **BR-003 (Inferred, High):** Never lose or silently corrupt the user's data (meetings, edits, files). Evidence: never-delete, atomic writes, conflict detection, failure states (`docs/SECURITY.md` "File system").
- **BR-004 (Inferred, High):** Keep confidential meeting content and API keys private by default (local-first, opt-in AI). Evidence: loopback-only default, credential handling, AI off by default.

## User roles and permissions

| Role | Capability | Restrictions | Evidence |
| --- | --- | --- | --- |
| Local user (owner of the installation) | Everything in the UI/API | Must call from loopback, or present the API token if one is configured | `Middleware.cs:76-95` |
| Remote token holder (server mode) | Everything | Bearer token required; TLS via external proxy | `docker-compose.yml` |
| Unauthenticated caller | `GET /api/health`, static UI files | — | `Middleware.cs:76` |

There are no accounts, roles or per-user data (Verified).

## Functional requirements

- **FR-001 (FEAT-001):** Given a Granola key and no checkpoint, when a Full or first Incremental sync runs, then every accessible note created after `InitialImportFrom` (or all) is imported and exported once; subsequent incremental runs only process notes updated since the last successful start minus the overlap. *Tests:* `Initial_import_exports_every_page_and_incremental_skips_unchanged`.
- **FR-002 (FEAT-001):** Given a sync in progress, when a second start is requested in this or another process, then it is rejected (409 / lease) and no meeting or file is duplicated. *Tests:* `Overlapping_runs_…`, `Duplicate_runs_…`.
- **FR-003 (FEAT-001):** Given Granola returns 401/403, when syncing, then the run stops with status Failed and a clear message, and the checkpoint does not advance; given a paging failure, the checkpoint does not advance. *Tests:* `Authentication_failure_…`, `Failure_during_pagination_…`.
- **FR-004 (FEAT-002):** Given a Fireflies key, when syncing, then Fireflies calls are imported alongside Granola meetings and an outage of one source does not stop the other. *Tests:* `Both_sources_sync_together_…`.
- **FR-005 (FEAT-003):** Given automatic sync is enabled and a key is configured, when the interval elapses, then an incremental sync starts without user action. *Partially met:* only when a **Granola** key exists (RF-004); untested.
- **FR-006 (FEAT-004):** Given rules, when a meeting is classified, then the strongest tier with evidence decides, ambiguous results go to review (default) and the decision and evidence are stored. *Tests:* `ClassificationEngineTests`.
- **FR-007 (FEAT-009):** Given a manual assignment, when the source changes or rules change, then the manual assignment still wins and disagreements are recorded. *Tests:* `Manual_override_survives_…`.
- **FR-008 (FEAT-006):** Given a meeting, when exported, then notes and transcript are written as separate Markdown files with stable names under root-guarded, template-driven folders, atomically. *Tests:* `MeetingExporterTests`, `MarkdownAndPathTests`.
- **FR-009 (FEAT-006):** Given transcript retrieval failed, when exporting, then no empty transcript is written and the meeting is retried later. *Tests:* `Partial_transcript_failure_…`.
- **FR-010 (FEAT-007):** Given an exported file edited by the user, when the source changes, then the file is not overwritten and is marked Conflict until the user chooses keep, replace or save a conflict copy. *Tests:* `MeetingExporterTests`.
- **FR-011 (FEAT-008):** Given an unassigned meeting, when the user saves a mapping from it and confirms, then matching meetings are re-classified and moved. *Tests:* `Unassigned_meeting_is_reassigned_after_saving_a_mapping`.
- **FR-012 (FEAT-010):** Given project input, when saved, then slugs are unique and invalid input returns field errors. *Tests:* `Project_validation_and_unique_slugs`.
- **FR-013 (FEAT-015):** Given a new API key, when saved with validation and the source rejects it (401), then the previous key is restored and the full key is never returned by the API. *Tests:* `Credentials_are_masked_…`, `Invalid_key_input_…`.
- **FR-014 (FEAT-020):** Given a state-changing API request without `X-Chroniq-Request: 1`, then it is rejected with 403. *Tests:* `State_changing_requests_require_the_csrf_header`.
- **FR-015 (FEAT-001):** Given a meeting deleted or no longer accessible at the source, when syncing, then its exported files are kept and it is marked Deleted/Inaccessible. *Tests:* `Deleted_or_inaccessible_notes_keep_exported_files`.

## Non-functional requirements

| ID | Category | Requirement / target | Evidence | Validation method |
| --- | --- | --- | --- | --- |
| NFR-001 | Reliability / data safety | Never delete user files; never overwrite user edits without explicit action; atomic writes | `AtomicFile.cs`, `MeetingExporter.cs`, `MigrateRootAsync` | Unit + integration tests |
| NFR-002 | Security | Listen on loopback by default; remote access only with a bearer token; CSRF header; keys never in DB/logs/browser | `Middleware.cs`, `appsettings.json`, `CredentialStores.cs`, `FileLogger.cs` | Integration tests (partial, SEC-001) |
| NFR-003 | Privacy | `Cache-Control: no-store` on API; no telemetry; AI opt-in; logs redact keys | `Middleware.cs:75`, `docs/SECURITY.md` | Test `Api_responses_are_never_cached_by_the_browser` |
| NFR-004 | Rate limits | Granola ≤ ~4.5 req/s (220 ms spacing), Fireflies ≤ ~55 req/min (1100 ms); honour `Retry-After`; ≤10 attempts; backoff cap 60 s; HTTP timeout 100 s | `GranolaApiClient.cs:17-45`, `FirefliesApiClient.cs:20-23`, `DependencyInjection.cs:37-47` | Unit tests |
| NFR-005 | Resilience | Per-meeting retry with backoff 5·2^(n−1) min up to 12 h, `MaxMeetingRetries` default 5; run survives single-meeting failures; restart recovery | `MeetingProcessor.cs:171-182`, `SynchronizationWorker.cs:50-56` | Integration tests |
| NFR-006 | Concurrency | One sync at a time across processes (10-min lease renewed per meeting); per-run parallelism 1–8 (default 2) | `SqlSyncLease`, `SyncEngine.cs:30, 141` | Integration test |
| NFR-007 | Limits | Export tree ≤ 5000 nodes; file preview ≤ 20 MB; file names ≤ 120 chars; folder listing ≤ 500 entries; run item message ≤ 1000 chars | `ExportBrowserService.cs:21-22, 177`, `PathTemplates.cs:114`, `SyncEngine.cs:192` | Code review |
| NFR-008 | Operability | JSON-lines logs with 14-day retention; correlation ID on every response; health endpoint | `FileLogger.cs`, `Middleware.cs`, `HealthController` | Manual |
| NFR-009 | Portability | Export folder is self-describing (front matter with source IDs); DB schema avoids SQLite-specific features | `MarkdownRenderer.cs`, `AppDbContext.cs:9-12` | Code review |

## Constraints and assumptions

- Single user per installation (Verified). Multi-user use is out of scope (Inferred).
- Windows is the primary desktop target; Credential Manager writes, folder picker drives and "open in Explorer" are Windows-first (`Dockerfile` comment, `README.md`).
- .NET 10 and Node.js 22 are required to build (`Directory.Build.props`, `README.md`).
- Granola's public API only returns notes with a generated summary and transcript (`GranolaApiClient.cs:74`, Documented by the code's own message).

## Open questions

See [open-questions.md](open-questions.md): Q-004 (Fireflies-only auto-sync intended?), Q-005 (is server mode used?), Q-010 (data retention).
