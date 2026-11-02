# Development Backlog

Statuses: Proposed, Ready, In Progress, Blocked, In Review, Done, Deferred.

Seeded by the project audit of 2026-10-28. Every task is `Proposed` until the owner prioritises it.

## Tasks

### PIDA-001 — Remove committed TypeScript output from the frontend and prevent it recurring
- **Status:** Proposed
- **Priority:** P0
- **Outcome:** The dev server, build, tests and Docker image use the real `.ts/.tsx` sources.
- **Scope:** `git rm` every tsc-emitted `frontend/src/**/*.js` twin, `frontend/vite.config.js`, `frontend/tsconfig.tsbuildinfo`; add `"noEmit": true` to `frontend/tsconfig.json`; add ignore rules (`frontend/src/**/*.js`, `frontend/vite.config.js`, `*.tsbuildinfo`).
- **Out of scope:** Refactoring components.
- **Dependencies:** Q-002 (confirm not intentional).
- **Likely components/files:** `frontend/src/**`, `frontend/tsconfig.json`, `.gitignore`.
- **Acceptance criteria:**
  - [ ] AC-1: `git ls-files frontend | grep -E '\.js$'` returns no file that has a `.ts`/`.tsx` sibling.
  - [ ] AC-2: `npx tsc -p frontend/tsconfig.json` emits no files.
  - [ ] AC-3: `npm run build` and `npm test` pass.
- **Test plan:** Run frontend build and tests; manually check a visible UI change appears in `npm run dev`.
- **Risks:** If a `.js` file has diverged from its `.tsx`, the newer behaviour may be in the `.js` — diff before deleting.
- **Estimate:** 0.5 h (High confidence).
- **Evidence/related docs:** RF-001, `docs/project-intelligence/coding-standards/code-structure-and-duplication.md`.

### PIDA-002 — Run CI on pushes to the actual default branch
- **Status:** Proposed
- **Priority:** P1
- **Outcome:** Every change to `master` is formatted, built and tested.
- **Scope:** Change `on.push.branches` in `.github/workflows/ci.yml` to `[master]` (or rename the branch to `main`).
- **Out of scope:** Branch protection settings (hosting, needs the owner).
- **Dependencies:** Q-001.
- **Likely components/files:** `.github/workflows/ci.yml`.
- **Acceptance criteria:**
  - [ ] AC-1: A push to the default branch triggers both CI jobs.
- **Test plan:** Push a no-op commit to a branch named like the default branch in a fork, or observe the next push.
- **Risks:** None.
- **Estimate:** 10 min.
- **Evidence/related docs:** RF-002.

### PIDA-003 — Apply logging settings at runtime (single `LogSettingsState`)
- **Status:** Proposed
- **Priority:** P1
- **Outcome:** Changing the log level, folder or retention in Settings takes effect immediately.
- **Scope:** Remove `services.AddSingleton<LogSettingsState>()` from `src/GranolaPI.Infrastructure/DependencyInjection.cs` (the API registers the instance), or move the single registration into Infrastructure; add an integration test.
- **Out of scope:** Logger redesign.
- **Dependencies:** none.
- **Likely components/files:** `src/GranolaPI.Infrastructure/DependencyInjection.cs:51`, `src/GranolaPI.Api/Program.cs:21-23`, `tests/GranolaPI.IntegrationTests/`.
- **Acceptance criteria:**
  - [ ] AC-1: Resolving `LogSettingsState` from DI returns the same instance the `FileLoggerProvider` uses.
  - [ ] AC-2: After `PUT /api/settings` with `logging.minimumLevel = "Warning"`, Information messages are no longer written.
- **Test plan:** Integration test asserting instance identity and level change; `dotnet test`.
- **Risks:** Unit tests that call `AddInfrastructure` alone may need to register the state themselves.
- **Estimate:** 1 h.
- **Evidence/related docs:** RF-003, FEAT-021.

### PIDA-004 — Scheduled sync for Fireflies-only setups
- **Status:** Proposed
- **Priority:** P1
- **Outcome:** Automatic sync runs when a Granola, Fireflies or WhisperAI key is configured.
- **Scope:** Change the key check in `SynchronizationWorker.MaybeStartAsync` to accept any source secret (WhisperAI added 2026-11-02, PIDA-019); update its summary comment.
- **Out of scope:** Per-source schedules.
- **Dependencies:** Q-004; PIDA-008 makes it testable.
- **Likely components/files:** `src/GranolaPI.Worker/SynchronizationWorker.cs:64`.
- **Acceptance criteria:**
  - [ ] AC-1: With only `FIREFLIES_API_KEY` set and auto-sync on, a scheduled run starts when due.
  - [ ] AC-2: With no key, no run starts.
- **Test plan:** Worker unit test with fake clock and secrets (after PIDA-008).
- **Risks:** Low.
- **Estimate:** 1 h including test.
- **Evidence/related docs:** RF-004, FEAT-003.

### PIDA-005 — Test the API-token and remote-caller paths of request protection
- **Status:** Proposed
- **Priority:** P1
- **Outcome:** The only authentication control is regression-tested.
- **Scope:** Integration tests: token configured → missing/wrong token 401, right token 2xx, `/api/health` open; no token → non-loopback caller 403, `AllowRemoteWithoutToken` allows; CSRF still required with a token.
- **Out of scope:** Changing behaviour.
- **Dependencies:** none.
- **Likely components/files:** `tests/GranolaPI.IntegrationTests/ApiIntegrationTests.cs`, `TestHost.cs` (configurable settings; simulate remote IP via a test middleware or `TestServer` `HttpContext.Connection.RemoteIpAddress`).
- **Acceptance criteria:**
  - [ ] AC-1: Tests cover each branch of `Middleware.cs:76-102`.
- **Test plan:** `dotnet test`.
- **Risks:** None.
- **Estimate:** 2 h.
- **Evidence/related docs:** SEC-001, RF-005, FEAT-020.

### PIDA-006 — Tests for the AI classifier
- **Status:** Proposed
- **Priority:** P2
- **Outcome:** The guard against prompt-injected or invalid AI answers is covered.
- **Scope:** Unit tests for `AnthropicAiClassifier.Parse` (unknown ID → null, null ID → null, malformed JSON → null, evidence capped at 10, explanation truncated at 500) and `Schema`; service test with a fake `IAiClassifier` (only for Unassigned, non-manual meetings; exceptions swallowed). Optionally add a timeout around the call.
- **Out of scope:** Live API calls.
- **Dependencies:** none.
- **Likely components/files:** `src/GranolaPI.Infrastructure/Ai/AnthropicAiClassifier.cs`, `src/GranolaPI.Application/Classification/ClassificationService.cs`, `tests/GranolaPI.UnitTests/Classification/`.
- **Acceptance criteria:**
  - [ ] AC-1: Each `Parse` rule has a test.
  - [ ] AC-2: A failing classifier never blocks deterministic classification (tested).
- **Test plan:** `dotnet test`.
- **Risks:** None.
- **Estimate:** 2 h.
- **Evidence/related docs:** RF-017, FEAT-005.

### PIDA-007 — Tests for export root migration
- **Status:** Proposed
- **Priority:** P2
- **Outcome:** Moving the export folder is proven safe.
- **Scope:** Integration tests for `POST /api/exports/migrate-root`: requires confirm; copies files; skips identical; keeps different files with a warning; rejects nested roots; switches settings; leaves the old root untouched.
- **Dependencies:** none.
- **Likely components/files:** `ExportBrowserService.MigrateRootAsync`, `tests/GranolaPI.IntegrationTests/`.
- **Acceptance criteria:**
  - [ ] AC-1: All listed behaviours have assertions.
- **Test plan:** `dotnet test`.
- **Risks:** None.
- **Estimate:** 2 h.
- **Evidence/related docs:** RF-018, FEAT-014.

### PIDA-008 — Make the scheduler testable and test it
- **Status:** Proposed
- **Priority:** P2
- **Outcome:** Scheduling rules (enabled flag, interval minimum 5, last-run calculation, key presence) are covered.
- **Scope:** Inject `IClock` into `SynchronizationWorker` (replace `DateTime.UtcNow`); extract `MaybeStartAsync` decision into a testable method; add tests.
- **Dependencies:** none.
- **Likely components/files:** `src/GranolaPI.Worker/SynchronizationWorker.cs`, new worker tests.
- **Acceptance criteria:**
  - [ ] AC-1: Tests for due / not due / disabled / no key.
- **Test plan:** `dotnet test`.
- **Risks:** None.
- **Estimate:** 2 h.
- **Evidence/related docs:** RF-008, TD-001.

### PIDA-009 — Add dependency vulnerability scanning
- **Status:** Proposed
- **Priority:** P2
- **Outcome:** Vulnerable NuGet/npm packages and outdated Actions are flagged automatically.
- **Scope:** `.github/dependabot.yml` for nuget, npm (`/frontend`), github-actions; or CI steps `dotnet list package --vulnerable --include-transitive` and `npm audit --audit-level=high`.
- **Dependencies:** owner approval (changes shared repository configuration).
- **Likely components/files:** `.github/`.
- **Acceptance criteria:**
  - [ ] AC-1: Scanning runs on a schedule or on every PR.
- **Test plan:** Observe the first run.
- **Risks:** PR noise.
- **Estimate:** 1 h.
- **Evidence/related docs:** SEC-004, TD-005.

### PIDA-010 — Frontend lint
- **Status:** Proposed
- **Priority:** P2
- **Outcome:** Hook dependency bugs and unused code are caught; existing `eslint-disable` comments become meaningful.
- **Scope:** Add ESLint (typescript-eslint, eslint-plugin-react-hooks) with a `lint` script and a CI step; fix or justify the 8 suppressions.
- **Dependencies:** ADR-008 acceptance.
- **Likely components/files:** `frontend/package.json`, new `frontend/eslint.config.js`, `ci.yml`.
- **Acceptance criteria:**
  - [ ] AC-1: `npm run lint` passes in CI.
- **Test plan:** CI.
- **Risks:** New dev dependencies.
- **Estimate:** 2–3 h.
- **Evidence/related docs:** RF-014, TD-005.

### PIDA-011 — Source-aware meeting identity
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** Granola and Fireflies IDs can never collide.
- **Scope:** Migration replacing the unique index on `GranolaNoteId` with (`Source`, `GranolaNoteId`); filter lookups by source (`MeetingProcessor.cs:50`, `SyncEngine` discovery); optionally rename to `SourceId` (TD-003).
- **Dependencies:** Q-009; database migration — needs approval.
- **Likely components/files:** `AppDbContext.cs`, new migration, `MeetingProcessor.cs`, `SyncEngine.cs`.
- **Acceptance criteria:**
  - [ ] AC-1: Two meetings with the same ID but different sources can coexist (test).
- **Test plan:** Integration test; migration applied to a copy of a real DB.
- **Risks:** Migration on user databases; rename touches many files.
- **Estimate:** 0.5–2 days depending on the rename.
- **Evidence/related docs:** RF-006, TD-003.

### PIDA-012 — Redact every configured secret from logs
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** No API key from any provider can reach a log file.
- **Scope:** Extend `SecretRedactor` with the resolved secret values (or a Fireflies pattern); test.
- **Likely components/files:** `src/GranolaPI.Infrastructure/Logging/FileLogger.cs`.
- **Acceptance criteria:**
  - [ ] AC-1: A log message containing the Fireflies key is written with `[REDACTED]` (test).
- **Test plan:** Unit test.
- **Estimate:** 1 h.
- **Evidence/related docs:** RF-007, SEC-003.

### PIDA-013 — Repository and documentation housekeeping
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** No stray or misleading files.
- **Scope:** Delete `run-test.log`; rename `tools/node-cli/config.example copy.json` → `config.example.json`; fix `Program.cs:66` comment (`scripts/build-frontend.ps1`); update or drop hard-coded test counts in `README.md:130`.
- **Acceptance criteria:**
  - [ ] AC-1: All four items done; `dotnet build` still clean.
- **Test plan:** Build.
- **Estimate:** 20 min.
- **Evidence/related docs:** RF-011, RF-013, RF-015.

### PIDA-014 — Safer launcher scripts
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** `run.bat`/`stop.bat` only stop this app's processes.
- **Scope:** Kill by window title or recorded PID, or check that the process on 5080/5173 is `dotnet`/`node` started from this repository before killing.
- **Likely components/files:** `run.bat:25`, `stop.bat:14-25`.
- **Acceptance criteria:**
  - [ ] AC-1: An unrelated process on port 5173 survives `stop.bat`.
- **Test plan:** Manual on Windows.
- **Estimate:** 1–2 h.
- **Evidence/related docs:** RF-010.

### PIDA-015 — Make the live E2E script non-destructive
- **Status:** Proposed
- **Priority:** P3
- **Scope:** Restore `export.rootDirectory`/`sync.initialImportFrom`, archive the created project, add a timeout to `wait()`, fix the header comment.
- **Likely components/files:** `scripts/e2e-live.mjs`.
- **Acceptance criteria:**
  - [ ] AC-1: Settings after the script equal settings before it.
- **Test plan:** Run against a scratch install.
- **Estimate:** 1 h.
- **Evidence/related docs:** RF-012.

### PIDA-016 — Decide the future of `tools/node-cli`
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** One maintained implementation.
- **Scope:** Owner decision (Q-003); then delete, or add a clear deprecation banner and fix its README.
- **Dependencies:** Q-003.
- **Acceptance criteria:**
  - [ ] AC-1: Decision recorded in DECISIONS.md and applied.
- **Estimate:** 30 min after decision.
- **Evidence/related docs:** TD-002, FEAT-022.

### PIDA-017 — Harden and document server mode
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** Running in Docker does not give token holders broader file-system reach than needed.
- **Scope:** Document `AllowedHosts` for production; optionally restrict export roots, `validate-path` and `directories` to an allow-list (e.g. under `/data`) when `Security:ApiToken` is set.
- **Dependencies:** Q-005.
- **Likely components/files:** `docker-compose.yml`, `docs/INSTALLATION.md`, `ExportBrowserService.cs`, `SettingsValidator.cs`.
- **Acceptance criteria:**
  - [ ] AC-1: Docs updated; if implemented, roots outside the allow-list are rejected (test).
- **Estimate:** 1–4 h.
- **Evidence/related docs:** SEC-002, SEC-005.

### PIDA-019 — Import WhisperAI transcripts as a third meeting source
- **Status:** Done (2026-11-02)
- **Priority:** P1 (user request)
- **Outcome:** With a WhisperAI API key configured the same way as the Granola and Fireflies keys (Settings card or `WHISPERAI_API_KEY`), completed WhisperAI transcripts are synced, classified and exported alongside the other sources.
- **Scope:** `IWhisperAiApiClient` + client (raw `Authorization` header, `GET /transcript` with `before_id` paging, `GET /transcript/{id}`, `/sentences` fallback); mapper; `MeetingSource.WhisperAi`; sync discovery with its own checkpoint and a 1-day look-back; credential endpoints + test-connection; Markdown front matter; Settings card, badge and filter labels; `wai_` log redaction; docs.
- **Out of scope:** Uploading audio or creating transcripts; translation; realtime; a configurable look-back setting; PIDA-004 (scheduled sync for non-Granola-only setups).
- **Dependencies:** None. No DB migration (`Meetings.Source` is a string column).
- **Likely components/files:** `src/GranolaPI.Application/WhisperAi/`, `src/GranolaPI.Infrastructure/WhisperAi/`, `SyncEngine.cs`, `MeetingProcessor.cs`, `CredentialService.cs`, `MarkdownRenderer.cs`, `SystemControllers.cs`, `frontend/src/pages/SettingsPage.tsx`.
- **Acceptance criteria:**
  - [x] AC-1: Key stored, masked, validated and restored on rejection like the Fireflies key (`WhisperAi_credentials_are_masked_and_tested_separately`, `pages.test.tsx`).
  - [x] AC-2: Completed transcripts exported across pages with `source: whisperai`; queued/processing/error ones skipped (`Completed_transcripts_are_exported_across_pages…`).
  - [x] AC-3: Completed transcripts are not refetched; a job that completes later is imported by the next incremental run (`Anonymous_speakers_are_not_people_and_complete_transcripts_are_not_refetched`).
  - [x] AC-4: A WhisperAI outage does not block Granola/Fireflies (`All_three_sources_sync_together…`).
  - [x] AC-5: Speaker labels stay anonymous; the `audio_url` is not written to files.
- **Test plan:** `dotnet test` (unit + integration), `npm run typecheck`, `npx vitest run`, `npm run build`. All passed on 2026-11-02.
- **Risks:** Utterance shape untyped in the spec (Q-012).
- **Evidence/related docs:** FEAT-023, INT-006, ADR-010, `docs/WHISPERAI_API.md`.

### PIDA-018 — Versioning and safe upgrades
- **Status:** Proposed
- **Priority:** P3
- **Outcome:** Builds are identifiable and database upgrades are reversible.
- **Scope:** Add `<Version>` to `Directory.Build.props`, tag releases; copy `granola-pi.db` to a timestamped backup before applying pending migrations at start-up.
- **Dependencies:** Q-006.
- **Likely components/files:** `Directory.Build.props`, `Program.cs:48-51`.
- **Acceptance criteria:**
  - [ ] AC-1: `/api/health` reports the configured version.
  - [ ] AC-2: A pending migration produces a backup file first (test).
- **Estimate:** 2–3 h.
- **Evidence/related docs:** TD-006, `deployment.md`.

---

Do not reuse IDs. Do not mark tasks Done until acceptance criteria and required verification pass.
