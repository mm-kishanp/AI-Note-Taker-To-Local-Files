# Coding Standards: Overview

Last reviewed: 2026-10-28

- Evidence scope: backend — all 52 non-migration C# files read or skimmed, with closer reading of `Program.cs`, `Middleware.cs`, `SystemControllers.cs`, `MeetingsController.cs`, `SyncEngine.cs`, `MeetingProcessor.cs`, `MeetingExporter.cs`, `ExportBrowserService.cs`, `PathTemplates.cs`, `AtomicFile.cs`, `GranolaApiClient.cs`, `FirefliesApiClient.cs`, `AnthropicAiClassifier.cs`, `CredentialStores.cs`, `FileLogger.cs`, `SettingsService.cs`, `AppDbContext.cs`, `SynchronizationWorker.cs`. Frontend — 6 files sampled by a sub-audit (`api.ts`, `AppLayout.tsx`, `MeetingsPage.tsx`, `SettingsPage.tsx`, `Markdown.tsx`, `notify.ts`) plus repo-wide greps. Legacy `tools/node-cli` — sub-audit summary only. Most-changed files cannot be ranked: the history has three commits.
- Standard sources found: `.editorconfig` (Verified), `Directory.Build.props` (nullable enabled, `TreatWarningsAsErrors=false`, `CS1591` suppressed), `frontend/tsconfig.json` (`strict`, `noUnusedLocals`, `noUnusedParameters`). No CONTRIBUTING, style guide, analyzer rule set, ESLint or Prettier config.
- Enforcement found: CI runs `dotnet format --verify-no-changes --severity error` (only `error`-severity rules fail; most `.editorconfig` rules are `suggestion`, and namespace style is `warning`) and `tsc --noEmit` via `npm run build`. No pre-commit hooks, no frontend lint.

## How the code is implemented

The backend is a small, carefully written clean-architecture monolith. Use cases live in `GranolaPI.Application` as concrete services with primary constructors (`public sealed class X(IDep a, ...)`), consumed directly by thin controllers that mostly forward to one service method. Ports (interfaces) are declared in Application and implemented in Infrastructure. Records are used for DTOs, settings and results; entities are mutable classes. Errors are domain exceptions (`ValidationException`, `NotFoundException`, `GranolaApiException`, `ExportFileException`, `SyncAlreadyRunningException`) mapped centrally to RFC 7807 problem details. Async with `CancellationToken` is used end to end. XML doc comments explain *why* on most public types. Defensive design is visible everywhere: atomic file writes, root-guarded paths, never-delete policies, explicit failure states instead of empty data.

The frontend follows a page → hook (TanStack Query) → `frontend/src/services/api.ts` → `fetch` layering with typed DTOs, Mantine components and centralised error notification.

## Scorecard

| Topic | Status | Documented or inferred | Enforced by | Top issue | Page |
| --- | --- | --- | --- | --- | --- |
| Architecture and layering | Mostly consistent | Inferred (project references) + docs/ARCHITECTURE.md | Project references only | Controllers resolve services from `HttpContext.RequestServices` and use Infrastructure types directly | [architecture-and-layering.md](architecture-and-layering.md) |
| Async and await | Consistent | Inferred | Nothing | Worker uses `DateTime.UtcNow`; AI call has no explicit timeout | [async-await.md](async-await.md) |
| Code structure and duplication | Mostly consistent | Inferred | Nothing | Multi-type files; duplicated retry/backoff code in two API clients; legacy CLI duplicates logic; committed tsc output | [code-structure-and-duplication.md](code-structure-and-duplication.md) |
| Commenting and documentation | Consistent | Inferred | Nothing (`CS1591` off) | One stale comment (`Program.cs:66`) | [commenting-and-documentation.md](commenting-and-documentation.md) |
| Constants and magic values | Mostly consistent | Inferred | Nothing | Timeouts and intervals inline in a few places | [constants-and-magic-values.md](constants-and-magic-values.md) |
| Error handling and security | Consistent | Inferred + docs/SECURITY.md | Tests (partly) | Exception types reused across sources/purposes | [error-handling-and-security.md](error-handling-and-security.md) |
| Logging | Mostly consistent | Inferred | Nothing | Duplicate `LogSettingsState` breaks runtime log settings; correlation ID not in log entries | [logging.md](logging.md) |
| Naming and code hygiene | Mostly consistent | `.editorconfig` (Verified) | `dotnet format` (error severity only) | Source-specific names (`GranolaNoteId`) for multi-source data; 8 dead `eslint-disable` comments | [naming-and-code-hygiene.md](naming-and-code-hygiene.md) |

## Top recommended changes

| # | Change | Topic | Why it matters | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | Delete committed `frontend/src/**/*.js`, `vite.config.js`, `tsconfig.tsbuildinfo`; set `"noEmit": true`; ignore them | Structure / hygiene | They shadow the real sources in Vite and Vitest | S | P0 | PIDA-001 |
| 2 | Register one `LogSettingsState` instance | Logging | UI logging settings silently ignored until restart | S | P1 | PIDA-003 |
| 3 | Add ESLint (typescript-eslint + react-hooks) to the frontend and CI, or remove the dead directives | Hygiene | 8 suppressions of a rule nothing runs; hook dependency bugs go unnoticed | S | P2 | PIDA-010 |
| 4 | Enable .NET analyzers at warning level and fail CI on new warnings | Hygiene | `TreatWarningsAsErrors=false`, only error-severity format rules gate CI | M | P2 | ADR-009 |
| 5 | Extract the shared retry/backoff/throttle loop of the Granola and Fireflies clients | Duplication | Two near-identical 60-line loops | M | P3 | TD-004 |
| 6 | Introduce source-neutral names (`SourceId`, `SourceApiException`) | Naming | Fireflies data flows through Granola-named types and columns | L | P3 | TD-003 |

## Changes to the standards themselves

- Proposed ADR-008: frontend lint standard (ESLint + `react-hooks`) and an emit-safe `tsconfig` (`noEmit: true`).
- Proposed ADR-009: enable `AnalysisLevel`/`EnableNETAnalyzers` with `TreatWarningsAsErrors` in CI for new code.
- Make the dominant conventions below explicit (they are currently inferred): primary-constructor services, records for DTOs/settings, one domain exception type per failure kind mapped by `ApiExceptionHandler`, `CancellationToken` on every async public method, "never delete user files".

## Related

- Code review checklist: [code-review-guide.md](../code-review-guide.md)
- Findings from audits and reviews: [review-findings.md](../review-findings.md)
- Technical debt: [tech-debt.md](../tech-debt.md)
