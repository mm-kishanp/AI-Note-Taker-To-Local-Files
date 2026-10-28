# Async and Await

Last reviewed: 2026-10-28

- Evidence scope: every Application, Infrastructure, Worker and Api C# file was grepped for `.Result`, `.Wait()`, `GetAwaiter().GetResult()`, `async void`, `Thread.Sleep`; sync pipeline and API clients read in full. Frontend: sub-audit.
- Status: Consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| All I/O is async and every public async method takes a `CancellationToken` that is passed through | Inferred: every sampled service and controller action (≈all) | Review |
| No sync-over-async | Inferred: 0 occurrences of `.Result`/`.Wait()`/`GetResult()` in `src/` (grep) | Nothing |
| Background work is started explicitly and observed (`SyncCoordinator` keeps the task and logs failures) | Inferred | Review |
| Bounded parallelism for per-meeting work (`Parallel.ForEachAsync`, `MaxConcurrency` clamp 1–8) | Verified: `SyncEngine.cs:141-145` | Code |
| Outbound HTTP has a timeout and bounded retries | Verified: 100 s timeout, attempts clamp 1–10, backoff cap 60 s | Code |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Async end to end on I/O paths; no sync-over-async | Follows | grep: none found |
| No async-over-sync wrappers | Partly | `SupersedeStaleRecords` returns `Task.CompletedTask` (`MeetingExporter.cs:357-371`); `SettingsController.Dto` wraps a sync result in `Task.FromResult` (`SystemControllers.cs:98-111`) — harmless |
| Every task awaited; no `async void` | Follows | `SyncCoordinator` stores `_current` and exposes `WaitAsync` (`SyncCoordinator.cs:27-60`); frontend uses `void` deliberately for invalidations |
| Cancellation passed through | Follows | Controllers accept `CancellationToken`; `SyncEngine` links run token with an abort token (`SyncEngine.cs:138-139`); `SyncCoordinator` starts the run with `CancellationToken.None` on purpose so request aborts don't cancel a run |
| Library `ConfigureAwait(false)` | Not applicable | Application code runs on ASP.NET Core (no sync context); not used anywhere |
| Right parallel primitive with bounded concurrency | Follows | `Parallel.ForEachAsync` with `MaxDegreeOfParallelism`; counters use `Interlocked` |
| Timeouts on outbound calls; retries bounded | Partly | Granola/Fireflies: yes. Anthropic SDK call: no explicit timeout (`AnthropicAiClassifier.cs:41-53`) — SDK default Unknown |

## How the code does it today

Copy `MeetingProcessor.ProcessAsync` (token passed into every call, `Interlocked` counters) and `GranolaApiClient.SendAsync` (throttle → send → classify → bounded retry with `Task.Delay(…, ct)`).

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `SynchronizationWorker.MaybeStartAsync` | Uses `DateTime.UtcNow` instead of `IClock`, making the schedule untestable | Low | `SynchronizationWorker.cs:73` |
| `SynchronizationWorker.StopAsync` | `Task.Delay(10 s, cancellationToken)` inside `WhenAny`; if the host token fires first the delay throws `TaskCanceledException` out of `StopAsync` (Inferred, Low confidence on impact) | Low | `SynchronizationWorker.cs:43-48` |
| `AnthropicAiClassifier.SuggestAsync` | No explicit timeout; a new `AnthropicClient` per call | Low | `AnthropicAiClassifier.cs:41` |
| `SyncEngine` `fatal` variable | Written from parallel lambdas without synchronisation (only the first fatal matters; benign) | Low | `SyncEngine.cs:140-177` |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Inject `IClock` into `SynchronizationWorker` | Code | Enables schedule tests | S | P2 | PIDA-008 |
| Set a timeout (e.g. linked CTS of 60 s) around the AI call | Code | Avoid stalling a meeting's processing | S | P3 | PIDA-006 |
