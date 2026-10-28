# FEAT-021 — Structured file logging with redaction

Last reviewed: 2026-10-28

- Status: Partial
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: [coding-standards/logging.md](../coding-standards/logging.md); RF-003, RF-007

## User outcome

The app writes diagnosable JSON-lines logs to `data/logs`, one file per day, with API keys removed and old files pruned; the level, folder and retention can be changed in Settings.

## Users and permissions

Local user (Settings → Logging).

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | Logger provider | `FileLoggerProvider` | `FileLogger.cs:20-95`, `Program.cs:21-23` |
| Route | `PUT /api/settings` (`logging` section) | `SettingsController.ApplyLogging` | `SystemControllers.cs:83-96` |

## Behavior

- **Main flow:** log entries are serialised as JSON (`ts`, `level`, `category`, `message`, properties, `traceId`, `exception`), passed through `SecretRedactor`, queued (10 000) and written by a background thread; files older than `RetentionDays` are deleted once a day.
- **Runtime settings (defect):** start-up applies saved logging settings to the provider's state (`Program.cs:52-53`), but later changes made in the UI update a different `LogSettingsState` instance (RF-003), so they take effect only after a restart.
- **Edge cases:** write errors are swallowed (logging never crashes the app); a full queue drops lines.

## Data touched

Log files only.

## Integrations and side effects

File system.

## Configuration and feature flags

`logging.minimumLevel` (Information), `logging.logDirectory` (`./data/logs`), `logging.retentionDays` (14); `Logging:LogLevel:*` for framework filters.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| none | — | No test covers redaction or runtime settings |

## Gaps and follow-up

- PIDA-003 (single instance + test); PIDA-012 (redaction coverage).
