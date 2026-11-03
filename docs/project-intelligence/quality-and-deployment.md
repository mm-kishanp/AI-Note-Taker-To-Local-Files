# Quality and Deployment

Last reviewed: 2026-10-28

- Evidence scope: `.github/workflows/ci.yml`, `Directory.Build.props`, `.editorconfig`, all `.csproj` files, `frontend/package.json`, `frontend/vite.config.ts`, `frontend/tsconfig.json`, `run.bat`, `README.md` (Testing section), test sources. **No command was run during this audit**; every command below is Documented (read from config), not Verified.
- Confidence: High

## Local development

| Step | Command | Verified? | Notes |
| --- | --- | --- | --- |
| Prerequisites | .NET SDK 10.0, Node.js 22+ (frontend only) | No (Documented, `README.md:48-53`) | `run.bat` checks for `dotnet` and `npm` |
| Restore tools | `dotnet tool restore` | No | Installs `dotnet-ef` 10.0.5 |
| Build UI into the API | `./scripts/build-frontend.ps1` | No | `npm ci` + `npm run build`, then replaces `src/Chroniq.Api/wwwroot` |
| Run (Development) | `$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run --project src/Chroniq.Api` | No | `http://127.0.0.1:5080`, Swagger at `/swagger`, base directory = repo root (`appsettings.Development.json`) |
| Run UI with hot reload | `cd frontend; npm install; npm run dev` | No | `http://localhost:5173`, proxies `/api` to `CHRONIQ_BACKEND_URL` (default `http://127.0.0.1:5080`) |
| One-click desktop | `run.bat` / `run.bat dev` / `run.bat rebuild` / `run.bat test`; `stop.bat` | No | Force-kills processes on ports 5080 (and 5173 in `stop.bat`), see RF-010 |
| Secrets | Enter keys in Settings → Connections, or set `GRANOLA_API_KEY` / `FIREFLIES_API_KEY` / `ANTHROPIC_API_KEY` | No | Never commit values |

## Quality gates

| Gate | Command | Enforced where | Last result | Evidence |
| --- | --- | --- | --- | --- |
| .NET format | `dotnet format Chroniq.sln --verify-no-changes --severity error --no-restore` | CI | Not run in this audit | `ci.yml` "Format check" |
| .NET build | `dotnet build Chroniq.sln -c Release --no-restore` | CI | Not run | `ci.yml`; warnings are **not** errors (`TreatWarningsAsErrors=false`) |
| .NET unit + integration tests | `dotnet test Chroniq.sln -c Release --no-build` | CI (results published with `dorny/test-reporter`) | Not run. README claims 110 unit + 28 integration; the code has 87 unit and 30 integration test methods (`[Fact]`/`[Theory]`, theories expand to more cases) | `ci.yml`, `README.md:130` |
| Frontend type check + build | `npm run build` (`tsc --noEmit -p tsconfig.json && vite build`) | CI | Not run | `ci.yml`, `package.json` |
| Frontend unit tests | `npm test` (`vitest run`, `src/**/*.test.{ts,tsx}`) | CI | Not run | `ci.yml`, `vite.config.ts` |
| Frontend lint | None (no ESLint/Prettier config, although 8 `eslint-disable` comments exist) | — | — | RF-014 |
| Browser E2E smoke | `npm run e2e` (Playwright, against a running app) | Manual only | — | `frontend/e2e/` |
| Live E2E | `node scripts/e2e-live.mjs <empty-export-folder> 10` | Manual only; needs a real Granola key; changes app settings | — | `scripts/e2e-live.mjs` |
| Coverage | `coverlet.collector` referenced, no threshold or report step | Not enforced | — | test `.csproj` files |
| Security / dependency scan | None | — | — | SEC-004 |
| Pre-commit hooks | None (no `.husky/`, `.pre-commit-config.yaml`) | — | — | Verified absent |

## CI pipelines (quality checks)

| Pipeline | Trigger | Checks it runs | Blocks merge? | Evidence |
| --- | --- | --- | --- | --- |
| `CI` (`.github/workflows/ci.yml`), job `backend` (ubuntu-latest, .NET 10.0.x) | `push` to **`main`**, every `pull_request` | restore, format check, Release build, all .NET tests, test report | Unknown — branch protection is not visible in the repository (Q-001) | `ci.yml:3-7` |
| same, job `frontend` (Node 22, npm cache) | same | `npm ci`, `npm run build`, `npm test` | Unknown | `ci.yml:36-52` |

**Gap (RF-002):** the repository's branch is `master` (`git status`: `master...origin/master`) but the workflow only runs on pushes to `main`. Direct pushes to `master` run no CI; only pull requests do.

## Mapping tests to important features

| Feature | Tests | Gap |
| --- | --- | --- |
| FEAT-001 Granola sync | `SyncIntegrationTests` (11), `GranolaApiClientTests` (16) | — |
| FEAT-002 Fireflies import | `FirefliesSyncTests` (5), `FirefliesTests` (11) | — |
| FEAT-003 Scheduled sync | none (worker disabled in `TestHost.cs:200`) | RF-004, TD-001 |
| FEAT-004/005 Classification / AI | `ClassificationEngineTests` (22); AI: none | RF-017 |
| FEAT-006/007 Export and conflicts | `MeetingExporterTests` (20), `MarkdownAndPathTests` (18) | — |
| FEAT-014 Root migration | none | RF-018 |
| FEAT-019 Request protection | `ApiIntegrationTests` (CSRF, CORS, no-store, 404) | token/remote path untested (SEC-001) |
| FEAT-020 Logging | none | RF-003 |

## Deployment and debugging

- How the app is packaged and released: [deployment.md](deployment.md).
- How to run each part under a debugger and diagnose problems: [debugging.md](debugging.md).

## Gaps

- CI does not run on pushes to the actual default branch (RF-002, PIDA-002).
- The frontend build and tests may exercise stale committed `.js` files instead of the `.tsx` sources (RF-001, PIDA-001).
- No lint for the frontend; .NET analyzer warnings do not fail the build (TD-005).
- No dependency scanning (SEC-004).
- Scheduled sync, AI classification, root migration, logging settings and API-token auth have no automated tests (TD-001).
- E2E suites are manual only; none run in CI.
