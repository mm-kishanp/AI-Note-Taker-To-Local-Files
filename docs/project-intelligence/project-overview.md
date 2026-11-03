# Project Overview

Last reviewed: 2026-10-28

- Evidence scope: all manifests, all backend source, frontend and Node CLI via sub-audits, CI, Docker, docs, Git history (3 commits). Excluded: user data folders (`ChroniqKnowledge/`, `data/`), `node_modules/`, build output. No build or test was run.
- Confidence: High

## What this project is

**CHRONIQ** — a local-first .NET 10 + React app that imports meetings from Granola and Fireflies, classifies them into projects with explainable rules, and writes notes and transcripts as Markdown files. Plain-language description: [project.md](project.md).

## Domain glossary

| Term | Meaning in this project | Where it appears |
| --- | --- | --- |
| Meeting | One imported Granola note or Fireflies call, with notes, summary, transcript, participants and export state | `Meeting` entity, `/meetings` |
| Source | Where a meeting came from: `Granola` or `Fireflies` | `MeetingSource`, front matter `source:` |
| `GranolaNoteId` | The source's own ID (Granola note ID **or** Fireflies transcript ID) | `Meeting.GranolaNoteId` |
| Notes / Transcript | The two exported file kinds | `ExportKind` |
| Project | A destination for meetings, with a unique folder-safe slug and rules | `Project`, `/projects` |
| Client | The user's customer; groups projects and can own rules. **Not** a software tenant | `Client`, `/clients` |
| Rule / mapping | A matcher (email, domain, participant name, speaker name, Granola folder, title keyword, keyword) that points to a project or client | `ClassificationRule`, `RuleKind` |
| Tier | A class of evidence evaluated in order (participant email → identity → speaker → client → metadata → keyword → AI) | `ClassificationTier` |
| Unassigned | Meetings no rule placed confidently; exported to `Unassigned/` and listed for review | `ExportLocation.Unassigned`, `/unassigned`, `ReviewQueue.md` |
| Shared | Meetings that belong to several/no specific project, exported to `Shared/` | `ExportLocation.Shared`, `MultiProjectMode.Shared` |
| Excluded | Manually excluded from export | `ManualAssignmentKind.Excluded` |
| Manual assignment | A user override that always wins over rules | `Meeting.ManualAssignment` |
| Export record | One generated file: path, hashes, status | `ExportRecord` |
| Conflict / LocallyModified / KeptLocal / Missing / Superseded | File states when user edits, deletions or reassignments are detected | `ExportStatus` |
| Export root | Base folder for files; optional separate notes and transcripts roots | `ExportSettings` |
| Reconcile / Re-export | Sync modes that rebuild files from stored data without API calls | `SyncMode` |
| Sync run / lease | One synchronisation execution; the DB row preventing concurrent runs | `SyncRun`, `SyncLease` |
| Checkpoint | Last successful incremental start per source | `sync.checkpoint`, `sync.checkpoint.fireflies` |
| PROJECT_CONTEXT.md | Generated per-project meeting index | `ProjectContextService` |
| CSRF header | `X-Chroniq-Request: 1`, required on state-changing API calls | `RequestProtectionMiddleware` |
| CHRONIQ | Product name (renamed from "Granola Project Intelligence" / GPI on 2026-11-03). Code prefix `Chroniq.*`, header `X-Chroniq-Request`, temp suffix `.chroniq-tmp`. Legacy names still accepted: credential prefix `GranolaProjectIntelligence`, `data/granola-pi.db`, `GranolaProjectKnowledge/`, `X-GPI-Request`, `granola-pi:generated` markers, `.gpi-tmp` | everywhere |

## Technology stack

| Area | Technology / version | Evidence | Confidence |
| --- | --- | --- | --- |
| Language / runtime | C# (latest), .NET 10 (`net10.0`); TypeScript 5.6.3; Node.js 22 (build), ≥18 (legacy CLI) | `Directory.Build.props`, `frontend/package.json`, `ci.yml` | High |
| Framework(s) | ASP.NET Core 10 (controllers, problem details, OpenAPI 10.0.9, Swagger UI 10.3.1); EF Core 10.0.5; React 18.3.1, Vite 5.4.14, Mantine 7.17.8, TanStack Query 5.66.0, react-router 6.28.2, react-markdown 9.0.3 | csproj, `package.json` | High |
| AI | Anthropic .NET SDK 13.3.0 (opt-in), default model `claude-opus-5-5` | `Chroniq.Infrastructure.csproj`, `AppSettings.cs:80` | High |
| Data store(s) | SQLite (EF Core provider, SQLitePCLRaw 3.0.5); Markdown files | `DependencyInjection.cs:26` | High |
| Testing | xUnit 2.9.3, NSubstitute 6.2.0, Mvc.Testing 10.0.5, coverlet 6.0.4; Vitest 2.1.9, Testing Library, jsdom; Playwright 1.49.1 | test csproj, `package.json` | High |
| Hosting / infrastructure | Self-hosted: Windows desktop process, or Docker (`aspnet:10.0`, non-root) via compose | `Dockerfile`, `docker-compose.yml` | High |
| CI/CD | GitHub Actions CI only (no CD) | `.github/workflows/ci.yml` | High |

## Runtime components

| Component | Type | Entry point | Depends on | Sub-project page |
| --- | --- | --- | --- | --- |
| API host | API + SPA host | `src/Chroniq.Api/Program.cs` | Application, Infrastructure, Worker | [projects/api](projects/api/project.md) |
| Scheduled sync | Worker (in-process) | `SynchronizationWorker` | Application | [projects/worker](projects/worker/project.md) |
| Use cases | Library | `AddApplication()` | Domain | [projects/application](projects/application/project.md) |
| Adapters | Library | `AddInfrastructure()` | Application, Domain | [projects/infrastructure](projects/infrastructure/project.md) |
| Domain | Library | — | — | [projects/domain](projects/domain/project.md) |
| UI | Web app (SPA) | `frontend/src/main.tsx` | API | [projects/frontend](projects/frontend/project.md) |
| Legacy CLI | CLI (deprecated) | `tools/node-cli/granola-sync.js` | Granola API | [projects/node-cli](projects/node-cli/project.md) |

## Local development

See [quality-and-deployment.md](quality-and-deployment.md#local-development) and [debugging.md](debugging.md). Short version (Documented): `dotnet tool restore`; `./scripts/build-frontend.ps1`; `dotnet run --project src/Chroniq.Api` → `http://127.0.0.1:5080`; or `run.bat`. UI hot reload: `cd frontend; npm run dev`.

## Build, test, deploy

- Build/test: `dotnet build` / `dotnet test` on the solution; `npm run build` / `npm test` in `frontend/` — all run by CI on pull requests (and pushes to `main`, see RF-002).
- Deploy: no pipeline; desktop publish or `docker compose up -d` ([deployment.md](deployment.md)).

## Maturity and activity

- Very young: 3 commits, 2026-10-27 to 2026-10-28, one author; no tags or releases (Verified, `git log`).
- Despite its age the code is feature-complete for its stated scope, with 117 backend test methods and 3 frontend test files (Verified counts).
- 0 TODO/FIXME markers (Verified).
- The most recent feature work added Fireflies as a second source (migration `20261027144432_AddMeetingSource`, `docs/FIREFLIES_API.md`).

## Known limitations and open questions

- Committed compiler output in `frontend/src` makes builds use stale code (RF-001) — fix first.
- CI does not run on pushes to `master` (RF-002, Q-001).
- Scheduled sync ignores Fireflies-only setups (RF-004, Q-004).
- Single-user design; server mode relies on one shared token (Q-005).
