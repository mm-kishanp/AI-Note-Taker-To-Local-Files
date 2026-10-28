# FEAT-017 — Sync operations: status, history, cancel, retry, re-export, reconcile

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FEAT-001; `SyncRun`, `SyncRunItem`; `SyncMode` (Incremental, Full, RetryFailed, ReExport, Reconcile)

## User outcome

The user sees live progress, can cancel a run, retry failed meetings (all or one), regenerate files from stored data without calling the APIs, reconcile files on disk (recreate missing ones), and inspect every past run with per-meeting outcomes.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | All operations | Middleware | `SystemControllers.cs:31-65` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Routes | `GET /api/sync/status`, `POST /api/sync/cancel`, `POST /api/sync/retry-failed`, `GET /api/sync/history[/{id}]`, `POST /api/meetings/{id}/retry` | `SyncController`, `SyncCoordinator`, `SyncHistoryService` | `SystemControllers.cs`, `MeetingsController.cs:36-41` |
| Screen | Layout sync controls (status polled every 1.5 s while running), `/sync-history` | `SyncControls`, `SyncHistoryPage` | sub-audit |

## Behavior

- **Main flow:** status is an in-memory `SyncProgress` snapshot; cancel triggers the run's `CancellationTokenSource` → run status Cancelled; RetryFailed with IDs re-fetches those meetings, without IDs re-queues every failed meeting; ReExport/Reconcile process local meetings without API calls (Force overwrites edits; Reconcile recreates missing files) (`SyncEngine.cs:116-131`).
- **Recovery:** at start-up runs left `Running` are marked Interrupted (`SyncHistoryService.MarkInterruptedAsync`).
- **Error cases:** 409 when a run is active.

## Data touched

`SyncRuns`, `SyncRunItems`, `Meetings`, `ExportRecords`.

## Integrations and side effects

As FEAT-001/006.

## Configuration and feature flags

None beyond sync settings.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Restart_recovery_marks_interrupted_runs_and_resumes_pending_exports` | Integration | Recovery |
| `Overlapping_runs_are_rejected_in_process_and_across_processes` | Integration | 409 and lease |
| `Partial_transcript_failure_is_recorded_and_retried_without_duplicating_notes` | Integration | Retry |
| `MeetingExporterTests` (Reconcile) | Unit | Missing files recreated |

## Gaps and follow-up

- `POST /api/sync/retry-failed` is not called by the UI (RF-016).
- Progress is in memory; a second process's run is not visible in this process's status (Inferred, Medium).
