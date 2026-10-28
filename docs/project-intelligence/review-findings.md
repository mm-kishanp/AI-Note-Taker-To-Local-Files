# Review Findings

Last reviewed: 2026-10-28

- Evidence scope: this initial audit (backend read in full or sampled; frontend and Node CLI via read-only sub-audits). No PR reviews exist yet.

## Summary

| Status | Critical | High | Medium | Low |
| --- | --- | --- | --- | --- |
| Open | 0 | 1 | 6 | 12 |
| Fixed | 0 | 0 | 0 | 0 |
| Accepted / Won't fix | 0 | 0 | 0 | 0 |

## Findings

| ID | Date | Source | Severity | Location | Finding | Topic | Status | Task |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RF-001 | 2026-10-28 | Audit | High | `frontend/src/**/*.js`, `frontend/vite.config.js`, `frontend/tsconfig.tsbuildinfo` (commit `1bb6c87`); `frontend/tsconfig.json` (no `noEmit`) | TypeScript compiler output was committed next to every `.ts/.tsx` source. Vite 5.4 resolves extension-less imports in the order `.mjs, .js, .mts, .ts, .jsx, .tsx` (`frontend/node_modules/vite/dist/node/constants.js:24-32`) and prefers `vite.config.js` over `vite.config.ts` (`:33-40`). **Failure scenario:** a developer edits `MeetingsPage.tsx`; `npm run dev`, `vite build`, Vitest and the Docker image all load the stale `MeetingsPage.js`, so the change never appears and tests pass against old code, while `tsc --noEmit` still type-checks the `.tsx` and hides the drift. | Structure / hygiene | Open | PIDA-001 |
| RF-002 | 2026-10-28 | Audit | Medium | `.github/workflows/ci.yml:3-6` | CI runs on `push` to `main` only, but the repository branch is `master`. Direct pushes to `master` (all 3 commits so far) run no format/build/test checks. | CI | Open | PIDA-002 |
| RF-003 | 2026-10-28 | Audit | Medium | `Program.cs:21-23`, `src/GranolaPI.Infrastructure/DependencyInjection.cs:51`, `SystemControllers.cs:69,86-96` | `LogSettingsState` registered twice; `SettingsController` receives the second instance, not the one `FileLoggerProvider` reads. Saving Settings → Logging (level, directory, retention) has no effect until restart. | Logging | Open | PIDA-003 |
| RF-004 | 2026-10-28 | Audit | Medium | `src/GranolaPI.Worker/SynchronizationWorker.cs:64` | Scheduled sync starts only if a Granola key exists. A user with only a Fireflies key who enables automatic sync never gets a scheduled run, although manual sync supports Fireflies-only (`SyncEngine.cs:72-74`). | Correctness | Open | PIDA-004 |
| RF-005 | 2026-10-28 | Audit | Medium | `src/GranolaPI.Api/Middleware/Middleware.cs:76-95`; `tests/` | The bearer-token and remote-rejection branches of the only authentication control have no test (same as SEC-001). | Tests / security | Open | PIDA-005 |
| RF-006 | 2026-10-28 | Audit | Low | `AppDbContext.cs:77`, `MeetingProcessor.cs:45-50`, `SyncEngine.cs:277-280` | `Meeting.GranolaNoteId` is unique on its own and lookups by it ignore `Source`. If a Fireflies transcript ID ever equals a Granola note ID, the second source's meeting would update the first one's record. Low likelihood (different ID formats). | Data | Open | PIDA-011 |
| RF-007 | 2026-10-28 | Audit | Low | `FileLogger.cs:99-100` | `SecretRedactor` has no pattern for Fireflies keys (only `grn_`, `sk-ant-`, `Bearer`). | Security / logging | Open | PIDA-012 |
| RF-008 | 2026-10-28 | Audit | Low | `SynchronizationWorker.cs:73` | Worker uses `DateTime.UtcNow` instead of `IClock`; the scheduling logic is untestable and untested. | Async / tests | Open | PIDA-008 |
| RF-009 | 2026-10-28 | Audit | Low | `FirefliesModels.cs:116`, `Middleware.cs:23-25`, `MeetingProcessor.cs:167` | Exception types reused across meanings: Fireflies errors are `GranolaApiException`s and surface as "Granola rejected the request"; DB errors are wrapped as `ExportFileException`. | Error handling | Open | TD-003 |
| RF-010 | 2026-10-28 | Audit | Low | `run.bat:25`, `stop.bat:14-19` | Launcher scripts force-kill any process listening on 5080 (and 5173 in `stop.bat`, Vite's default port), which can terminate unrelated apps. | Tooling | Open | PIDA-014 |
| RF-011 | 2026-10-28 | Audit | Low | `tools/node-cli/config.example copy.json` | Misnamed example file; `tools/node-cli/README.md:10` and `config.js:28` refer to `config.example.json`, which does not exist. | Hygiene | Open | PIDA-013 |
| RF-012 | 2026-10-28 | Audit | Low | `scripts/e2e-live.mjs` (lines 22, 34, 53) | Live E2E script changes `export.rootDirectory` and `sync.initialImportFrom` without restoring them, leaves a test project behind, and `wait()` has no timeout. | Tests | Open | PIDA-015 |
| RF-013 | 2026-10-28 | Audit | Low | `run-test.log` | Stray log file committed at the repository root. | Hygiene | Open | PIDA-013 |
| RF-014 | 2026-10-28 | Audit | Low | `frontend/src/**` (8 occurrences) | `eslint-disable-next-line react-hooks/exhaustive-deps` directives but no ESLint configured; hook dependency problems are neither checked nor documented. | Hygiene | Open | PIDA-010 |
| RF-015 | 2026-10-28 | Audit | Low | `README.md:130`, `Program.cs:66` | Stale docs: README test counts (110 + 28 vs 87 + 30 test methods); comment references non-existent `scripts/build.ps1`. | Docs | Open | PIDA-013 |
| RF-016 | 2026-10-28 | Audit | Low | `frontend/src/services/api.ts`, `frontend/src/hooks/useSettings.ts:92` | Dead client code: `api.sync.retryFailed`, `api.projects.addRule`, `useGranolaFolders` are never called (sub-audit). | Structure | Open | Q-007 |
| RF-017 | 2026-10-28 | Audit | Medium | `src/GranolaPI.Infrastructure/Ai/AnthropicAiClassifier.cs` | No tests for `Parse` (rejects unknown project IDs, null handling, truncation) or for the schema; this is the guard against prompt-injected routing. | Tests / security | Open | PIDA-006 |
| RF-018 | 2026-10-28 | Audit | Medium | `ExportBrowserService.MigrateRootAsync` (`:188-235`) | Export root migration (bulk copy, never-overwrite, settings switch) has no automated test. | Tests | Open | PIDA-007 |
| RF-019 | 2026-10-28 | Audit | Low | `MeetingService.cs:165-220, 229-…`; `SyncEngine.cs:145-171` | Manual actions (assign, reclassify, re-export, resolve conflict) reprocess the meeting inside the HTTP request without taking the sync lease, while a running sync may process the same meeting in another scope. No concurrency tokens are configured, so the last `SaveChanges` wins (Inferred, Medium confidence; not reproduced). Possible result: a superseded record or status flip-flop for that meeting until the next run. | Correctness / concurrency | Open | Q-011 |

- **Fixed** needs evidence: the commit, PR, or task that fixed it, and the date.
- **Accepted / Won't fix** needs a reason and who decided.
- Findings that describe long-lived structural problems also get a `TD-NNN` entry in `tech-debt.md`.
