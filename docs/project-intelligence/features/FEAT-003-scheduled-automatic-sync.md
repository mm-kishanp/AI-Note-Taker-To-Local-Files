# FEAT-003 — Scheduled automatic sync

Last reviewed: 2026-10-28

- Status: Partial
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-005; `SynchronizationWorker`; settings `sync.autoSyncEnabled`, `sync.intervalMinutes`

## User outcome

When automatic synchronisation is turned on, the app runs an incremental sync every *N* minutes without user action, and recovers safely after restarts.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Enable/disable in Settings → Synchronization | Settings API | `SettingsController.Update` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Job | `SynchronizationWorker` (30 s `PeriodicTimer`) | `MaybeStartAsync` | `SynchronizationWorker.cs:25-78` |
| Config | `Worker:Enabled` (default true) | `Program.cs:28` | `appsettings.json` |

## Behavior

- **Main flow:** Given `AutoSyncEnabled` and a Granola key, when 30 s tick and no run is active and `max(5, IntervalMinutes)` have elapsed since the last manual or scheduled run started/finished, then an Incremental run with trigger `Scheduled` is started (`SynchronizationWorker.cs:58-78`).
- **Start-up:** unfinished runs are marked Interrupted and pending meetings will be retried (`RecoverAsync`, test `Restart_recovery_marks_interrupted_runs_and_resumes_pending_exports` covers the history service part).
- **Shutdown:** cancels the current run and waits up to 10 s (`StopAsync`).
- **Error cases:** exceptions in the check are logged and the loop continues.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `SyncRuns` | R | last start/finish |
| `ApplicationSettings` `sync` | R | interval, enabled |

## Integrations and side effects

Same as FEAT-001/002 once a run starts.

## Configuration and feature flags

`Worker:Enabled`; `sync.autoSyncEnabled` (default false, `AppSettings.cs:36`); `sync.intervalMinutes` (default 30, min 5).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| none | — | The worker is disabled in the integration host (`TestHost.cs:200`); scheduling logic is untested |

## Gaps and follow-up

- **Requires a Granola key**: a Fireflies-only setup never auto-syncs (`SynchronizationWorker.cs:64`) — RF-004, PIDA-004, Q-004.
- Uses `DateTime.UtcNow` instead of `IClock` → untestable (RF-008, PIDA-008).
