# Project Progress

- Last updated: 2026-11-02
- Current goal: Add WhisperAI as a third meeting source (PIDA-019)
- Current task ID: PIDA-019
- Overall status: Done (uncommitted; awaiting the user's review and commit)

## 2026-11-02 — PIDA-019 WhisperAI transcript import

- **Completed:** The WhisperAI REST API was read from its published OpenAPI spec (`https://whisperai.com/whisperai-openapi.json`), because the docs page is a JS app. Then the integration was implemented: client, mapper, sync discovery, credentials, controllers, Markdown, frontend card, badge and filter, plus `wai_` log redaction. Docs: `docs/WHISPERAI_API.md`, FEAT-023, INT-006, ADR-010, R-010, Q-012; README updated.
- **Files changed:** `src/GranolaPI.Application/WhisperAi/*` (new), `src/GranolaPI.Infrastructure/WhisperAi/*` (new), `Enums.cs`, `Interfaces.cs`, `CredentialService.cs`, `SyncEngine.cs`, `MeetingProcessor.cs`, `MarkdownRenderer.cs`, `CredentialStores.cs`, `DependencyInjection.cs`, `FileLogger.cs`, `SystemControllers.cs`; `frontend/src/{types/api,services/api,pages/SettingsPage,pages/MeetingsPage,pages/MeetingDetailPage,components/MeetingsTable,pages/pages.test}.{ts,tsx}` plus their `.js` twins (regenerated with `tsc` because they shadow the sources, RF-001); tests `WhisperAiTests.cs`, `WhisperAiSyncTests.cs`, `TestHost.cs`.
- **Verification (all run 2026-11-02):** `dotnet build`: 0 warnings, 0 errors. `dotnet test`: 129 unit + 35 integration passed. `npm run typecheck`: passed. `npx vitest run`: 21 passed. `npm run build`: passed.
- **Not verified:** no live call with a real `wai_` key (Q-012).
- **Not touched:** the concurrent `setup.bat` work (committed separately as `d19e3e4` during this session).
- **Next steps:** user reviews and commits; sync once with a real key to settle Q-012; consider PIDA-004 (scheduled sync for setups without a Granola key now also covers WhisperAI-only).

## Completed (2026-10-28 audit)
- Preflight: repository `C:\workspace\Discussions\Granola`, branch `master` tracking `origin/master`. At the start, many changes were staged and uncommitted. During the audit the user committed them (`1bb6c87`, `10d2095`), and the tree was then clean. The audit did not touch code.
- Discovery of 233 tracked files in a single pass. Two parallel read-only sub-audits covered `frontend/` and `tools/node-cli/` + `scripts/`. The High-severity frontend claim was re-verified by hand.
- Knowledge base written: every page in `docs/project-intelligence/`, 22 feature pages and 7 sub-project pages.
- Project management seeded: 18 backlog tasks, 9 risks, 9 decisions (7 pre-existing, 2 proposed).

## Files changed
- `docs/project-intelligence/*.md` (24 top-level pages, rewritten from templates)
- `docs/project-intelligence/coding-standards/*.md` (9 pages)
- `docs/project-intelligence/features/FEAT-001` … `FEAT-022` (new)
- `docs/project-intelligence/projects/{api,worker,application,domain,infrastructure,frontend,node-cli}/project.md` (new)
- `project-management/BACKLOG.md`, `RISKS.md`, `DECISIONS.md`, `PROGRESS.md`

## Verification
- Command: `python scripts/doc-freshness.py`. Result: it ran and reported 60 pages. The only "missing path" reports left are expected ones (API routes, ignored folders, sub-project-relative paths, symbol lists); details are in `analysis-log.md`.
- Command: a search of the knowledge base for template placeholders. Result: none left.
- Command: a search of the knowledge base for secret-like patterns. Result: one false positive (a setting name), no values.
- Command: a check of internal Markdown links. Result: none broken.
- Limitations: no build, test, lint or scan was run. Test counts come from `[Fact]`/`[Theory]` attributes, not from test runs.

## Blockers and decisions needed
- Q-001: default branch and CI gating.
- Q-002: were the committed `.js` files intentional?
- Q-003: what should happen to `tools/node-cli`?
- Q-004: should a Fireflies-only setup get scheduled sync?
- Q-005: is server mode used in practice?

## Project pages remaining
- None. Every sub-project has a page.

## Feature pages remaining
- None. Every inventory feature has a page.

## Next exact steps
1. Owner answers Q-001 to Q-005 (see `docs/project-intelligence/open-questions.md`).
2. PIDA-001 (P0): remove the committed tsc output, add `noEmit`, run `npm run build` and `npm test`.
3. PIDA-002 to PIDA-005 (P1): fix the CI trigger, the `LogSettingsState` registration, Fireflies-only auto-sync, and the token-path tests.
4. Re-run `/project-refresh` after those changes to mark RF/TD items Fixed with evidence.

## Context handoff
The app is a .NET 10 modular monolith (API, worker, application, infrastructure, domain) with a React SPA. It syncs Granola and Fireflies meetings into Markdown folders, classified by rules. It is single-user and local-first. The SQLite DB and the `GranolaProjectKnowledge/` folder contain confidential meeting content; never open or copy them. The findings register is in `review-findings.md` (RF-001 to RF-019) and the debt register in `tech-debt.md` (TD-001 to TD-007).
