# Error Handling and Security

Last reviewed: 2026-10-28

- Evidence scope: `Middleware.cs`, all controllers, `SyncEngine`, `MeetingProcessor`, `MeetingExporter`, `AtomicFile`, `ExportBrowserService`, both API clients, `CredentialService`, `CredentialStores`, `FileLogger`; frontend error handling via sub-audit. Security detail: [security-review.md](../security-review.md).
- Status: Consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Throw typed domain exceptions; map them centrally to RFC 7807 with `category` and `correlationId`; never return stack traces | Verified: `ApiExceptionHandler` (`Middleware.cs:14-50`) | Integration tests (`Invalid_settings_are_rejected_with_field_errors`, `Unknown_meeting_returns_404_problem`) |
| One meeting failing never stops the run; auth/permission failures abort it | Verified: `SyncEngine.cs:16-21, 145-177` | Integration tests |
| A failed retrieval is recorded as failed, never as empty content | Verified: `GranolaApiClient.cs:141-145`, `MeetingExporter.cs:122-126` | Tests |
| File-system errors are classified (`FilePermission`, `DiskSpace`, `FileConflict`, `Configuration`) and retried | Verified: `AtomicFile.Classify` | Unit tests |
| Every path is root-guarded; every ID validated before use in a URL | Verified: `SafePath`, `ValidateNoteId`, `ValidateId` | Tests (path traversal) |
| Never delete or overwrite user files without an explicit user action | Verified: `MeetingExporter.WriteAsync`, `MigrateRootAsync` | Tests (`MeetingExporterTests`) |
| Frontend: problems mapped to `ApiError`; `notifyError` shows them; no retries on 4xx | Inferred (sub-audit) | Tests (`api.test.ts`) |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| No empty catch that hides failures | Partly | Intentional, commented swallows: `FileLogger.WriteLoop` (`FileLogger.cs:57-60`), `AtomicFile.TryDelete` (`:125-128`), `ExportBrowserService` unreadable folders (`:61-64`, `:180`), drive info `catch (ArgumentException) { }` (`:154`) |
| No catch-all returning success | Follows | Catch-alls set Failed status and log (`SyncEngine.cs:228-233`, `SyncCoordinator.cs:40-43`) |
| No stack traces in responses | Follows | `Middleware.cs:38` |
| No string-built SQL or shell | Follows | EF LINQ only; `ProcessStartInfo.ArgumentList` |
| Input validation on every write endpoint | Follows | `SettingsValidator`, `ProjectService` validation, `CredentialService` key checks |
| Authorization on every endpoint | Not applicable / Follows | Single user; middleware covers all `/api` |
| Exception types match their meaning | Partly | `FirefliesApiException` derives from `GranolaApiException` (`FirefliesModels.cs:116`); DB failures wrapped as `ExportFileException(ErrorCategory.Database)` (`MeetingProcessor.cs:167`); `ApiExceptionHandler` labels any `GranolaApiException` (incl. Fireflies) "Granola rejected the request" (`Middleware.cs:23-25`) |

## How the code does it today

Copy `MeetingProcessor.RecordFailure` (category + message + bounded backoff) and `ExportBrowserService.Resolve` (convert guard failure to a `ValidationException`).

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `FirefliesApiException : GranolaApiException` | Fireflies errors reported to the UI as Granola errors | Low | `Middleware.cs:23-25`, `FirefliesModels.cs:116` |
| `MeetingProcessor.SaveAsync` | Database error wrapped in `ExportFileException` | Low | `MeetingProcessor.cs:159-169` |
| `ExportBrowserService.ValidatePath` | Can create a directory anywhere the process can write | Low (by design; see SEC-002) | `:112-124` |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Introduce a source-neutral `SourceApiException` (with `Source`) and a `PersistenceException` | Code | Accurate messages and categories | M | P3 | TD-003 |
| Add tests for the token/remote path of the middleware | Code | Only authentication control | S | P1 | PIDA-005 |
