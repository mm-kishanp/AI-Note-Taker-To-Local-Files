# Repository Map

Last reviewed: 2026-10-28

- Evidence scope: `git ls-files` (233 tracked files at the time of the audit), every manifest, every C# source file under `src/` (read or skimmed), the test projects, `frontend/` (read by a sub-audit), `tools/node-cli/`, `scripts/`, `.github/`, `docs/`. Not inspected: contents of `GranolaProjectKnowledge/` and `data/` (git-ignored user data: meeting notes, transcripts and the SQLite database), `node_modules/`, build output.
- Confidence: High

## Top-level layout

| Path | Purpose | Language/tooling | Evidence | Confidence |
|---|---|---|---|---|
| `src/GranolaPI.Domain/` | Entities, enums, value objects (`Slug`), text normalisation rules. No dependencies. | C# / .NET 10 | `GranolaPI.Domain.csproj`, `Entities/Entities.cs` | High |
| `src/GranolaPI.Application/` | Use cases: sync engine, meeting processor, classification engine, Markdown export, services, DTOs, settings models, interfaces (ports) | C# / .NET 10, EF Core abstractions | `GranolaPI.Application.csproj`, `DependencyInjection.cs` | High |
| `src/GranolaPI.Infrastructure/` | Adapters: EF Core/SQLite `AppDbContext` + migrations, Granola and Fireflies HTTP clients, Anthropic classifier, credential stores, file logger, shell launcher | C# / .NET 10 | `GranolaPI.Infrastructure.csproj`, `DependencyInjection.cs` | High |
| `src/GranolaPI.Worker/` | `SynchronizationWorker` (BackgroundService) that starts scheduled syncs; hosted inside the API process | C# / .NET 10 | `SynchronizationWorker.cs` | High |
| `src/GranolaPI.Api/` | ASP.NET Core host: controllers, middleware, composition root, serves the built SPA from `wwwroot/` | C# / ASP.NET Core 10 | `Program.cs`, `Controllers/`, `Middleware/Middleware.cs` | High |
| `frontend/` | React 18 + TypeScript SPA (Vite, Mantine, TanStack Query) | TypeScript, npm | `frontend/package.json`, `frontend/src/main.tsx` | High |
| `tests/GranolaPI.UnitTests/` | xUnit unit tests: classification, export, Markdown/paths, Granola and Fireflies clients | C#, xUnit, NSubstitute | `GranolaPI.UnitTests.csproj` | High |
| `tests/GranolaPI.IntegrationTests/` | xUnit integration tests over `WebApplicationFactory` with fake Granola/Fireflies clients | C#, xUnit, Mvc.Testing | `TestHost.cs` | High |
| `tools/node-cli/` | Earlier zero-dependency Node.js exporter, "kept for reference" | Node.js ≥18 | `tools/node-cli/package.json`, `README.md:43` | High |
| `scripts/` | `build-frontend.ps1` (builds UI into `wwwroot`), `e2e-live.mjs` (live E2E against a running app), plus PIDA helper scripts (`doc-freshness.py`, `repo-inventory.sh`, `sync-skills.py`) | PowerShell, Node, Python | file contents | High |
| `config/` | Example settings and project-mapping JSON (importable from the Projects page) | JSON | `config/*.example.json` | High |
| `docs/` | Authoritative product docs (architecture, configuration, classification, Markdown format, security, troubleshooting, Granola and Fireflies API notes) and the OpenAPI snapshot; `docs/project-intelligence/` is this knowledge base | Markdown, JSON | `docs/` | High |
| `.github/workflows/ci.yml` | The only CI pipeline | GitHub Actions | file | High |
| `.claude/`, `.agents/`, `CLAUDE.md`, `AGENTS.md` | Agent instructions and PIDA skills (tooling, not product code) | Markdown | files | High |
| `run.bat`, `stop.bat` | Windows desktop launcher / stopper | Batch | files | High |
| `Dockerfile`, `docker-compose.yml` | Server/NAS container image and compose file | Docker | files | High |
| `Directory.Build.props`, `.editorconfig`, `dotnet-tools.json` | Shared .NET build settings, code style, local `dotnet-ef` tool | MSBuild, EditorConfig | files | High |
| `run-test.log` | A committed 98-byte log of a failed `run.bat` invocation; stray artifact (RF-013) | text | file | High |
| `GranolaProjectKnowledge/`, `data/` | Local runtime output (exported Markdown, SQLite DB, logs). Git-ignored; contain confidential meeting content. Not inspected. | — | `.gitignore:11-16` | High |

## Entry points

| Entry point | Type | How it starts | Evidence |
|---|---|---|---|
| `src/GranolaPI.Api/Program.cs` | Web API + SPA host + hosted worker | `dotnet run --project src/GranolaPI.Api` (listens on `http://127.0.0.1:5080`), `run.bat`, published `GranolaPI.Api.exe`, or the Docker image (`ENTRYPOINT dotnet GranolaPI.Api.dll`, port 8080) | `Program.cs`, `appsettings.json` (`Urls`), `Dockerfile` |
| `SynchronizationWorker.ExecuteAsync` | Background job (30 s tick) | Registered by `AddSynchronizationWorker()` when `Worker:Enabled` is true (default) | `Program.cs:28`, `SynchronizationWorker.cs:23-41` |
| `frontend/src/main.tsx` | SPA | `npm run dev` (port 5173, proxies `/api`), or built into `wwwroot/` and served by the API | `frontend/vite.config.ts`, `scripts/build-frontend.ps1` |
| `tools/node-cli/granola-sync.js` | CLI (legacy) | `node granola-sync.js [--config ...]` | `tools/node-cli/package.json` |
| `scripts/e2e-live.mjs` | Manual live E2E | `node scripts/e2e-live.mjs <exportRoot> [days]` against a running app | file header |

## Build and dependency manifests

| Manifest/lockfile | Scope | Package manager | Notes |
|---|---|---|---|
| `GranolaProjectIntelligence.sln` | All .NET projects | dotnet | 5 src + 2 test projects |
| `Directory.Build.props` | All .NET projects | MSBuild | `net10.0`, nullable, `TreatWarningsAsErrors=false`, `NoWarn CS1591` |
| `src/*/*.csproj`, `tests/*/*.csproj` | Per project | NuGet (no lockfile) | EF Core 10.0.5, Anthropic 13.3.0, Swashbuckle UI 10.3.1, xUnit 2.9.3 |
| `dotnet-tools.json` | Local tools | dotnet | `dotnet-ef` 10.0.5 |
| `frontend/package.json` + `package-lock.json` | SPA | npm | Exact pinned versions |
| `tools/node-cli/package.json` | Legacy CLI | npm (no deps, no lockfile) | `engines: node >=18` |

## Tests

| Location | Framework | Type | Command | Evidence |
|---|---|---|---|---|
| `tests/GranolaPI.UnitTests/` | xUnit + NSubstitute | Unit (87 `[Fact]`/`[Theory]` methods) | `dotnet test` (Documented) | file count from `git grep` |
| `tests/GranolaPI.IntegrationTests/` | xUnit + `WebApplicationFactory` | Integration (30 methods; fake Granola/Fireflies; in-memory credential store; worker disabled) | `dotnet test` (Documented) | `TestHost.cs:197-216` |
| `frontend/src/**/*.test.tsx` | Vitest + Testing Library (jsdom) | Unit/component (3 files) | `cd frontend; npm test` (Documented) | `frontend/vite.config.ts` `test.include` |
| `frontend/e2e/smoke.spec.ts` | Playwright | Read-only browser smoke against a running app; not in CI | `npm run e2e` (Documented) | `frontend/package.json` |
| `tools/node-cli/test/smoke.test.js` | Node assert | Offline smoke | `npm test` in `tools/node-cli` (Documented) | file |
| `scripts/e2e-live.mjs` | Node script | Live E2E against your Granola account (mutates app settings) | manual | file |

## Generated, vendored, and excluded areas

- `src/GranolaPI.Infrastructure/Persistence/Migrations/` — EF Core generated migrations (`generated_code = true` in `.editorconfig`); only the latest migration was read.
- `frontend/src/**/*.js`, `frontend/vite.config.js`, `frontend/tsconfig.tsbuildinfo` — TypeScript compiler output committed by accident (RF-001). Do not edit; they shadow the `.ts/.tsx` sources.
- `src/GranolaPI.Api/wwwroot/` — built SPA, git-ignored, produced by `scripts/build-frontend.ps1`.
- `docs/openapi.json` — snapshot of the generated OpenAPI document (131 KB); summarised, not read line by line.
- `GranolaProjectKnowledge/`, `data/`, `tools/node-cli/config.json`, `.env` — user data or secrets; never opened.

## Ownership and conventions

- No `CODEOWNERS`, PR template or CONTRIBUTING file (Verified: absent from `git ls-files`).
- Clean-architecture layering: Domain ← Application ← Infrastructure/Worker ← Api (Verified by `ProjectReference` entries).
- Several types per file is the norm (`Controllers/SystemControllers.cs` holds six controllers; `DTOs/Dtos.cs` holds all DTOs; `Entities/Entities.cs` holds all entities). See [coding-standards/code-structure-and-duplication.md](coding-standards/code-structure-and-duplication.md).
- Code style: `.editorconfig` (file-scoped namespaces, `_camelCase` private fields, LF line endings). CI enforces `dotnet format --verify-no-changes --severity error`.
- **Where to change what:** sync behaviour → `src/GranolaPI.Application/Synchronization/`; how files are named/written → `src/GranolaPI.Application/Export/`; routing rules → `src/GranolaPI.Application/Classification/`; HTTP surface → `src/GranolaPI.Api/Controllers/`; external APIs → `src/GranolaPI.Infrastructure/Granola|Fireflies|Ai/`; schema → `src/GranolaPI.Domain/Entities/Entities.cs` + `src/GranolaPI.Infrastructure/Persistence/AppDbContext.cs` + a new migration; UI → `frontend/src/pages/` and `frontend/src/hooks/`.
