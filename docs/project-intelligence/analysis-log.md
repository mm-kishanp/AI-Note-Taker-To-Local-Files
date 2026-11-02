# Analysis Log

Append an entry for each significant audit or refresh.

## 2026-11-02 — WhisperAI integration (PIDA-019)

- Trigger: user request to add the WhisperAI API (<https://whisperai.com/whisper-api/docs>) like the existing Fireflies integration.
- Sources: the docs page is a client-rendered SPA, so content came from its published `whisperai-openapi.json` and `whisperai-api.postman_collection.json` (found via the page's JS bundles) plus the docs chunk's examples. Downloaded to the session scratchpad only.
- Verified: base URL, raw `Authorization: wai_…` header, list/get/sentences endpoints, `before_id` paging, statuses, error codes (OpenAPI). Inferred (high): utterance fields and millisecond unit from the docs example (`"start": 240, "end": 26560`) → Q-012.
- Changes and verification: see PIDA-019 in `BACKLOG.md` and `PROGRESS.md`.

## 2026-10-28 — Initial PIDA project audit (whole repository)

- Trigger: `/project-audit` (first audit; every knowledge-base page was still a template).
- Directories/files inspected: all manifests and build config; every non-migration C# file in `src/` (read in full or skimmed) and the latest migration; `tests/` (test names and counts, `TestHost.cs`); `.github/workflows/ci.yml`; `Dockerfile`; `docker-compose.yml`; `appsettings*.json`; `README.md`; `docs/SECURITY.md` (other `docs/*.md` used as leads and linked); `frontend/` and `tools/node-cli/` + `scripts/` + `run.bat`/`stop.bat` through two parallel read-only `pida-auditor` sub-audits. I personally re-verified the sub-audit's High-severity claim (RF-001) against `frontend/tsconfig.json`, `frontend/vite.config.ts` and Vite's `constants.js`.
- Commands/checks run: read-only `git status`, `git log`, `git ls-files`, `git grep`, `git check-ignore`; file listing and line counts. **No build, test, lint or formatter was run**; all quality-gate commands are recorded as Documented.
- Findings: 19 review findings (1 High, 6 Medium, 12 Low), 6 security findings (1 Medium, 4 Low, 1 Info), 7 tech-debt items; 22 features (17 Implemented, 4 Partial, 1 Deprecated). Top items: committed tsc output shadows frontend sources (RF-001); CI trigger on `main` vs `master` (RF-002); duplicate `LogSettingsState` (RF-003); scheduled sync requires a Granola key (RF-004); token auth path untested (SEC-001).
- Verified facts: architecture, endpoints, data model, integrations, security controls and feature statuses — see each page's evidence.
- Inferences and confidence: business goals (High); RF-019 race between manual actions and sync (Medium); rollback approach (High); Anthropic SDK timeout behaviour (Unknown).
- Unknowns: branch protection; whether server mode is used; intended retention; whether the legacy CLI is still used (Q-001 – Q-011).
- Excluded/unavailable areas: `GranolaProjectKnowledge/` and `data/` (git-ignored user meeting content and database — deliberately not opened), `tools/node-cli/config.json` and any `.env` (secrets), `node_modules/`, `wwwroot/`, generated migrations other than the latest, `docs/openapi.json` line by line. Frontend files beyond the six sampled were checked by grep only. Mid-audit the working tree changed: the user committed the previously staged changes (`1bb6c87`, `10d2095`); this audit reflects that committed state.
- Documents updated: every page in `docs/project-intelligence/` (README, project, project-overview, repository-map, feature-inventory, requirements, architecture, data-model, api-catalog, integrations, client-matrix, security-review, quality-and-deployment, deployment, debugging, 9 coding-standards pages, code-review-guide, review-findings, tech-debt, seo-guide, aeo-guide, open-questions, this log); 22 new `features/FEAT-*.md`; 7 new `projects/*/project.md`; `project-management/BACKLOG.md`, `RISKS.md`, `DECISIONS.md`, `PROGRESS.md`.
- Final checks: placeholder search (none left); secret-pattern search (one false positive, the setting name `AllowRemoteWithoutToken=true` in `api-catalog.md`, which contains no value); internal Markdown links (none broken); `python scripts/doc-freshness.py` (repository-relative source paths were expanded to full paths). The remaining "missing path" reports are expected:
  - API routes such as `api/health`;
  - git-ignored or build folders (`wwwroot/`, `data/`, `node_modules/`);
  - sub-project-relative paths on pages that state their location;
  - `Type.MethodA/MethodB` symbol lists;
  - `scripts/build.ps1`, which RF-015 cites because the file does not exist.
- Next steps: answer Q-001 – Q-005; do PIDA-001 (P0); then PIDA-002 – PIDA-005 (P1).
