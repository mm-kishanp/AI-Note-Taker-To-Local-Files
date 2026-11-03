# Decision Log

No ADR folder existed before the 2026-10-28 audit. ADR-001 – ADR-007 record pre-existing choices inferred from code; ADR-008 and ADR-009 are proposals from the audit.

## ADR-001 — Modular monolith on ASP.NET Core with clean layering
- **Date:** 2026-10-28 (recorded; decision predates it)
- **Status:** Accepted (pre-existing, inferred from code)
- **Context:** A single-user local app that needs an API, a scheduler and a UI.
- **Decision:** One ASP.NET Core 10 process hosting API, BackgroundService worker and SPA; projects Domain ← Application ← Infrastructure/Worker ← Api.
- **Alternatives:** Separate worker service; desktop framework (WPF/Electron); Node-only (the earlier CLI).
- **Consequences:** Simple to run and deploy; one process to secure; scheduler tied to the API's lifetime.
- **Evidence:** `Chroniq.sln`, csproj references, `Program.cs`, `docs/ARCHITECTURE.md`.
- **Revisit when:** Multi-user or multi-instance hosting is needed.

## ADR-002 — SQLite via EF Core for state; Markdown files as the product output
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Context:** Local-first; output must be portable and usable without the app.
- **Decision:** EF Core 10 with SQLite (`data/chroniq.db`), migrations applied at start-up; meetings exported as Markdown with self-describing front matter.
- **Alternatives:** Files only (the earlier CLI's JSON state); a server database.
- **Consequences:** Zero-setup storage; DB and Markdown must be backed up together; schema kept provider-neutral.
- **Evidence:** `src/Chroniq.Infrastructure/DependencyInjection.cs:26`, `AppDbContext.cs:9-12`, `Program.cs:51`.
- **Revisit when:** Multiple processes/hosts must share state.

## ADR-003 — Local-first request protection instead of user accounts
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Context:** One user per installation; threats are other sites and other hosts.
- **Decision:** Loopback-only by default; optional shared bearer token (`Security:ApiToken`) for remote use; custom CSRF header; CORS allow-list; `AllowedHosts`.
- **Alternatives:** ASP.NET Core Identity / OIDC.
- **Consequences:** No login UX; a single token gives full control in server mode.
- **Evidence:** `Middleware.cs:52-112`, `appsettings.json`, `docs/SECURITY.md`.
- **Revisit when:** Q-008 (multi-user) changes.

## ADR-004 — Secrets in Windows Credential Manager or environment variables
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Decision:** `WindowsCredentialStore` on Windows desktops; read-only store + env vars (`GRANOLA_API_KEY`, `FIREFLIES_API_KEY`, `ANTHROPIC_API_KEY`) elsewhere; never in DB or JSON settings; masked in the API.
- **Consequences:** Keys must be re-entered on a new machine; containers rely on `.env`.
- **Evidence:** `CredentialStores.cs`, `CredentialService.cs`.
- **Revisit when:** macOS/Linux desktop support is wanted (Keychain/libsecret).

## ADR-005 — React + Vite + Mantine + TanStack Query SPA served by the API
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Decision:** TypeScript SPA built by Vite into `wwwroot/`, served with SPA fallback.
- **Evidence:** `frontend/package.json`, `scripts/build-frontend.ps1`, `Program.cs:67-73`.
- **Revisit when:** —

## ADR-006 — Deterministic tiered classification with optional AI
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Decision:** Ordered evidence tiers with narrowing and a configurable multi-project mode; manual decisions always win; AI (Anthropic, JSON schema constrained to known project IDs) only for meetings rules leave unassigned, off by default.
- **Evidence:** `ClassificationEngine.cs`, `ClassificationService.cs`, `AnthropicAiClassifier.cs`, `docs/CLASSIFICATION.md`.
- **Revisit when:** —

## ADR-007 — Self-hosted distribution: Windows desktop or Docker compose; no CD
- **Date:** 2026-10-28
- **Status:** Accepted (pre-existing, inferred from code)
- **Decision:** Users build and run their own copy (`run.bat`/`dotnet publish` or `docker compose up -d`); CI verifies only.
- **Evidence:** `README.md` "Production", `Dockerfile`, `docker-compose.yml`, `ci.yml`.
- **Revisit when:** Others need prebuilt releases (see PIDA-018).

## ADR-008 — Frontend lint standard and emit-safe TypeScript config
- **Date:** 2026-10-28
- **Status:** Proposed
- **Context:** No linter exists although code carries `eslint-disable` comments; `tsconfig.json` lacks `noEmit`, which allowed committed `.js` output (RF-001, RF-014).
- **Decision:** Adopt ESLint (typescript-eslint + react-hooks) run in CI; set `"noEmit": true`.
- **Alternatives:** Biome; remove the directives and stay lint-free.
- **Consequences:** New dev dependencies; a one-time fix-up.
- **Evidence:** `frontend/tsconfig.json`, `frontend/package.json`.
- **Revisit when:** —

## ADR-009 — Enforce .NET analyzers in CI for new code
- **Date:** 2026-10-28
- **Status:** Proposed
- **Context:** `TreatWarningsAsErrors=false`; most `.editorconfig` rules are `suggestion`; CI format check only fails on `error` severity.
- **Decision:** Enable `EnableNETAnalyzers`/`AnalysisLevel` and treat warnings as errors in CI builds (with a baseline for existing warnings), raising key `.editorconfig` rules to `warning`.
- **Alternatives:** Keep review-only enforcement.
- **Consequences:** Possible initial warning clean-up.
- **Evidence:** `Directory.Build.props`, `.editorconfig`, `ci.yml`.
- **Revisit when:** —

## ADR-010 — WhisperAI as a third meeting source, modelled on Fireflies
- **Date:** 2026-11-02
- **Status:** Accepted (user request: "implement the same" as the existing AI notetaker integration, using the same API-key handling)
- **Context:** WhisperAI is a transcription API, not a meeting notetaker: transcripts have no title, attendees, owner or summary; `GET /transcript/{id}` omits the creation time; listing is newest-first with a `before_id` cursor and no update filter.
- **Decision:** Add `MeetingSource.WhisperAi` with its own client, mapper, checkpoint and credential (same storage: Credential Manager / `WHISPERAI_API_KEY`). Import only `completed` transcripts; pass the listing's `created` time through `WorkItem.SourceCreatedAt`; title from the audio file name; never store `audio_url` (may be pre-signed); fixed 1-day incremental look-back instead of a new setting. Two-way `Fireflies ? … : Granola` branches were replaced by `switch` expressions so a third source cannot silently fall into the Granola branch.
- **Alternatives:** A generic "source plugin" abstraction (rejected: speculative for three sources); a `whisperAiLookbackDays` setting (deferred; add if users need it).
- **Consequences:** Classification of WhisperAI transcripts relies on keyword and AI rules only. No DB migration.
- **Evidence:** PIDA-019, FEAT-023, INT-006.
- **Revisit when:** A fourth source is added, or WhisperAI adds metadata (title, participants).

## ADR-011 — Rename the product to CHRONIQ, keeping legacy names readable
- **Date:** 2026-11-03
- **Status:** Accepted (requested by the user)
- **Context:** The product was called "Granola Project Intelligence Sync" (code prefix `GranolaPI`, abbreviation GPI). "Granola" is also the name of the third-party meeting service the app syncs from, which made the two easy to confuse.
- **Decision:** The display name is CHRONIQ. The code prefix is `Chroniq.*` (solution `Chroniq.sln`, projects, namespaces), and there is new on-disk and wire naming: `data/chroniq.db`, `ChroniqKnowledge/`, credential prefix `Chroniq:`, `X-Chroniq-Request`, `chroniq:generated` markers, `.chroniq-tmp`, `chroniq-*.log`, scheduled task `CHRONIQ`, `CHRONIQ_*` env vars. The Granola *integration* (GranolaApiClient, `GRANOLA_API_KEY`, Granola settings) keeps its name, and so does the legacy Node CLI `tools/node-cli/granola-sync.js`.
- **Backward compatibility:** These old names are still read: `GranolaProjectIntelligence:` credentials (read and deleted, `CredentialStores.cs`), `data/granola-pi.db` when no `chroniq.db` exists (`Program.cs`), `./GranolaProjectKnowledge` when export settings were never saved and only that folder exists (`Program.cs`), the `X-GPI-Request` header (`Middleware.cs`), `granola-pi:generated` markers (upgraded on rewrite), `.gpi-tmp` files (swept), `granola-pi-*.log` (pruned), and `GPI_API_TOKEN` in compose. `setup.bat` removes the old `Granola PI` task and Startup entry. The compose volume keeps its name `granola-pi-state`.
- **Not migrated:** the dev-only env vars `GPI_BACKEND_URL`, `GPI_E2E_URL`, `GPI_E2E_SCREENSHOTS` and `GPI_URL`. Docker users must rename the host folder `./GranolaProjectKnowledge` to `./ChroniqKnowledge`.
- **Evidence/tests:** `Legacy_csrf_header_from_before_the_rename_is_still_accepted`, `Cleanup_also_removes_temp_files_from_before_the_rename`, `Project_context_upgrades_markers_from_before_the_rename`.
- **Revisit when:** All known installs have run a CHRONIQ build once. The legacy fallbacks can then be removed.

Record meaningful decisions; do not create an ADR for every small implementation choice.
