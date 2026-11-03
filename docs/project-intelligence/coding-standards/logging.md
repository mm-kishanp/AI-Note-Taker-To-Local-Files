# Logging

Last reviewed: 2026-10-28

- Evidence scope: `FileLogger.cs`, `Program.cs` logging setup, `src/Chroniq.Infrastructure/DependencyInjection.cs`, `SettingsController.ApplyLogging`, every `logger.Log*` call in sampled files; `Console.Write` grep (0 hits in `src`); frontend `console.*` (0, sub-audit).
- Status: Mostly consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Use `ILogger<T>` with message templates and named placeholders (`{NoteId}`, `{RunId}`) | Inferred: every sampled log call | Nothing |
| Never log secrets; all output passes through `SecretRedactor` | Verified: `FileLogger.cs:84-91`, `docs/SECURITY.md` | Code |
| Do not log meeting content; log IDs, categories and messages | Inferred: sampled calls log note IDs, paths, statuses | Review |
| Levels: Error for crashes/unexpected, Warning for degraded/recoverable, Information for lifecycle and retries | Inferred | Review |
| No `Console.WriteLine` / `console.log` | Inferred: 0 occurrences | Nothing |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Structured logging | Follows | e.g. `SyncEngine.cs:165`, `MeetingExporter.cs:212` |
| No secrets in logs | Partly | Redactor covers `grn_`, `sk-ant-`, `Bearer`; Fireflies key pattern missing (SEC-003) |
| No personal data | Partly | Export paths (contain meeting titles) logged at Information (`MeetingExporter.cs:212`); `ApiExceptionHandler` logs `ex.Message` for 4xx (`Middleware.cs:32`) |
| Correlation ID in log entries | Partly | Middleware sets `HttpContext.TraceIdentifier` to the correlation ID (`Middleware.cs:66-69`), but the file logger writes `Activity.Current.TraceId` (`FileLogger.cs:86-87`) and ignores scopes (`BeginScope` returns null, `:72`); the two IDs differ when the caller supplies `X-Correlation-ID` |
| Runtime log configuration works | Does not | `LogSettingsState` is registered twice: the instance passed to `FileLoggerProvider` (`Program.cs:21-23`) and a second `AddSingleton<LogSettingsState>()` (`src/Chroniq.Infrastructure/DependencyInjection.cs:51`). DI resolves the last registration, so `SettingsController` (`SystemControllers.cs:69, 86`) updates an instance the logger never reads. Changes apply only after restart, when `Program.cs:52-53` re-applies the saved settings to the logger's instance. RF-003 |
| Log retention | Follows | Daily files pruned after `RetentionDays` (`FileLogger.cs:50-55`) |
| Back-pressure | Follows (lossy) | Bounded queue of 10 000; `TryAdd` drops lines when full (`FileLogger.cs:23, 38`) |

## How the code does it today

Copy `logger.LogWarning("Export of {Kind} for {NoteId} failed: {Category} {Message}", …)` (`MeetingExporter.cs:321`): IDs and categories, not content.

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `src/Chroniq.Infrastructure/DependencyInjection.cs:51` | Second `LogSettingsState` singleton shadows the logger's instance | Medium | see Checks |
| `FileLogger` | Scopes ignored; correlation ID not logged | Low | `FileLogger.cs:72, 86` |
| `SecretRedactor` | Missing Fireflies key pattern | Low | `FileLogger.cs:99` |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Remove `services.AddSingleton<LogSettingsState>()` from Infrastructure (or register the instance once there) and add a test that saving logging settings changes the provider's level | Code | Fix RF-003 | S | P1 | PIDA-003 |
| Write `HttpContext.TraceIdentifier` as `correlationId` (support `BeginScope` or enrich in middleware) | Code | One ID from response to log line | S | P3 | TD-004 |
| Redact the resolved secret values themselves | Code | Covers every provider | S | P3 | PIDA-012 |
