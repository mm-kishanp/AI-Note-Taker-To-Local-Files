# FEAT-001 — Granola meeting sync (full + incremental)

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-001, FR-002, FR-003; `POST /api/sync/start`; entities `Meeting`, `MeetingParticipant`, `SyncRun`, `SyncRunItem`, `ApplicationSetting(sync.checkpoint)`; INT-001

## User outcome

Every meeting the user's Granola API key can read is imported once (a full historical import, optionally limited by *Initial import from*) and then kept current by incremental runs, without duplicates, and without one bad meeting blocking the rest.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Start, cancel and watch runs | `RequestProtectionMiddleware` (loopback or bearer token + CSRF header) | `Middleware.cs:76-102` |
| Scheduler | Start incremental runs | `SynchronizationWorker` (FEAT-003) | `SynchronizationWorker.cs:76` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `POST /api/sync/start` `{mode: Incremental|Full, reclassify?, force?}` | `SyncController.Start` → `SyncCoordinator.TryStart` | `SystemControllers.cs:35-40` |
| Screen | "Sync now" in the app layout (`SyncControls`) | `useSystem` hooks | frontend sub-audit |
| Job | Scheduled incremental run | `SynchronizationWorker` | FEAT-003 |

## Behavior

- **Main flow:** Given a Granola key and valid export settings, when a run starts, then `SyncEngine` acquires the DB lease (10 min), validates export settings, cleans stale `*.gpi-tmp` files, lists notes page by page (cursor; `updated_after = last successful start − IncrementalOverlapMinutes` for incremental runs, else `created_after = InitialImportFrom`), skips notes whose `updated_at` and statuses are unchanged, adds due retries, and processes the rest in parallel (`MaxConcurrency`, clamped 1–8). Each meeting is fetched with its transcript, normalised and saved, classified (FEAT-004), exported (FEAT-006), and its outcome recorded. Project context files are refreshed (FEAT-012) and the checkpoint is saved only if the listing completed (`SyncEngine.cs:33-260`, `MeetingProcessor.cs:35-157`).
- **Validation and error cases:**
  - 401/403 from Granola abort the run with status Failed and a clear message (`SyncEngine.cs:156-177`, test `Authentication_failure_aborts_the_run_with_clear_error`).
  - 429/5xx/network errors are retried with throttle and backoff (INT-001); after the last attempt the meeting is marked failed and retried in later runs with backoff 5, 10, 20 … min, max 12 h, up to `MaxMeetingRetries` (`MeetingProcessor.cs:171-182`).
  - Transcript failure is recorded separately; notes are still exported, and the transcript is never written as empty (`Partial_transcript_failure_is_recorded_and_retried_without_duplicating_notes`).
  - A paging failure does not advance the checkpoint (`Failure_during_pagination_does_not_advance_the_checkpoint`).
  - Invalid export settings fail the run before any API call (`SyncEngine.cs:54-55`).
- **Edge cases:** a note deleted in Granola is marked `Deleted` and its files kept (`SyncEngine.cs:286-290`); a note no longer accessible is marked `Inaccessible` (`MeetingProcessor.cs:74-83`); overlapping runs are rejected in-process (409) and across processes (lease) (`Overlapping_runs_are_rejected_in_process_and_across_processes`); duplicate runs never duplicate meetings or files (`Duplicate_runs_never_duplicate_meetings_or_files`).

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `Meetings`, `MeetingParticipants` | R/W | Full notes, summary and transcript JSON stored (sensitive) |
| `ExportRecords`, `MeetingProjectAssignments` | R/W | via FEAT-004/006 |
| `SyncRuns`, `SyncRunItems` | W | counters, per-meeting outcomes (unchanged meetings not itemised) |
| `ApplicationSettings` `sync.checkpoint` | R/W | last successful start/finish |
| `SyncLeases` | R/W | cross-process lock |

## Integrations and side effects

INT-001 (Granola REST). Writes Markdown files under the export roots (FEAT-006) and project context files (FEAT-012).

## Configuration and feature flags

`sync.initialImportFrom`, `sync.maxConcurrency` (2), `sync.maxRequestAttempts` (5), `sync.maxMeetingRetries` (5), `sync.incrementalOverlapMinutes` (10) (`AppSettings.cs:35-51`); key `GranolaApiKey`.

## Client availability

| Client | Enabled / Disabled / Customized / Unknown | Difference | Evidence |
| --- | --- | --- | --- |
| Not applicable | — | Single-user app | [client-matrix.md](../client-matrix.md) |

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `SyncIntegrationTests` (11 tests) | Integration | Initial import pages, incremental skip, in-place updates, partial transcript failure, auth abort, checkpoint safety, overlap prevention, restart recovery, deletion handling |
| `GranolaApiClientTests` (16) | Unit | Paging, retries, Retry-After, 413 fallback, error categorisation |
| `scripts/e2e-live.mjs` | Manual live E2E | Real Granola account end to end |

## Gaps and follow-up

- Manual meeting actions can run concurrently with a sync on the same meeting (RF-019).
- Meeting uniqueness ignores the source (RF-006).
