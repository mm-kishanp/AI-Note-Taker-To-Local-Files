# Code Structure and Duplication

Last reviewed: 2026-10-28

- Evidence scope: line counts of every non-migration C# file; `GranolaApiClient.cs` vs `FirefliesApiClient.cs` compared; frontend file sizes and committed artefacts (sub-audit); `tools/node-cli` vs .NET client (sub-audit).
- Status: Mostly consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Group code by feature folder inside each layer (`Synchronization/`, `Export/`, `Classification/`, `Meetings/`, `Projects/`) | Inferred: all Application files follow it | Review |
| Several small related types per file is acceptable (controllers, DTOs, entities, enums) | Inferred: `SystemControllers.cs` (6 controllers), `Dtos.cs`, `Entities.cs`, `Enums.cs`, `Middleware.cs` | Nothing |
| Keep classes under ~400 lines | Inferred: largest C# file 379 lines (`ClassificationEngine.cs`) | Nothing |
| Generated output is never committed | Inferred from `.gitignore` (`wwwroot/`, `dist/`) — **violated** in `frontend/src` | `.gitignore` (incomplete) |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| No very large files/functions | Partly | C#: max 379 lines. Frontend: `SettingsPage.tsx` 646, `MeetingDetailPage.tsx` 582 lines (sub-audit) |
| No copy-paste between modules | Partly | `GranolaApiClient.SendAsync/DelayAsync/RetryAfter` and `FirefliesApiClient.SendAsync/DelayAsync/RetryAfter` are near-identical (`GranolaApiClient.cs:174-249`, `FirefliesApiClient.cs:101-226`); frontend 409 "already running" handling repeated (`MeetingDetailPage.tsx:263`, `ProjectDetailPage.tsx:75`) |
| No duplicated per-client code | Not applicable | Single-user app |
| No dead code | Partly | Unused API calls/hooks in the UI: `api.sync.retryFailed`, `api.projects.addRule`, `useGranolaFolders` (sub-audit); legacy `tools/node-cli` duplicates the Granola client and matching (TD-002) |
| No generated files in source folders | Does not | Every `frontend/src/**/*.ts(x)` has a committed tsc-emitted `.js` twin plus `vite.config.js` and `tsconfig.tsbuildinfo` (commit `1bb6c87`) — RF-001 |

## How the code does it today

Feature folders with small focused services (`AtomicFile`, `PathTemplates`, `SafePath`, `ExportLayout`) are the model to copy. `FirefliesRequestThrottle` reuses `GranolaRequestThrottle` by inheritance — the right instinct, applied only partly.

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `frontend/src/**/*.js`, `frontend/vite.config.js`, `frontend/tsconfig.tsbuildinfo` | Generated compiler output committed; shadows sources (Vite resolves `.js` before `.tsx`, `vite.config.js` before `.ts`) | High | `frontend/node_modules/vite/dist/node/constants.js:24-40`; `frontend/tsconfig.json` has no `noEmit` |
| Granola/Fireflies clients | Duplicated retry loop, backoff and Retry-After parsing | Low | see Checks |
| `SettingsPage.tsx`, `MeetingDetailPage.tsx` | Very large components | Medium | sub-audit |
| `tools/node-cli` | Second implementation of the product, unmaintained | Low | `README.md:43` |
| `tools/node-cli/config.example copy.json` | Accidental file name; docs reference `config.example.json` | Low | sub-audit |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Remove generated `.js` and add `noEmit` + ignore rules | Code | Correct builds/tests | S | P0 | PIDA-001 |
| Extract a shared `RetryingApiTransport` for both clients | Code | One place for retry policy | M | P3 | TD-004 |
| Split `SettingsPage` into tab components | Code | Readability, testability | M | P3 | TD-004 |
| Decide whether to delete or freeze `tools/node-cli` | Code | Avoid drift | S | P3 | PIDA-016 |
