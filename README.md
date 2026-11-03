# CHRONIQ

A local-first app that pulls your meetings from [Granola](https://docs.granola.ai/introduction) and, optionally, the calls recorded by [Fireflies.ai](https://docs.fireflies.ai/getting-started/quickstart) and the transcripts in your [WhisperAI](https://whisperai.com/whisper-api/docs) API account. It works out which project each one belongs to, and saves the notes and the transcript as separate Markdown files in a folder structure you control.

The output is a plain folder of Markdown. It stays useful when the app isn't running: open it in VS Code, commit it to Git, or point Claude Code, GitHub Copilot or another coding agent at a project folder.

```text
ChroniqKnowledge/
├── Projects/
│   └── MotorDealerApp/
│       ├── Notes/2026-10-26-Client-Requirement-Discussion.md
│       ├── Transcripts/2026-10-26-Client-Requirement-Discussion.md
│       ├── README.md            (created once, yours to edit)
│       └── PROJECT_CONTEXT.md   (meeting index + items from Granola summaries)
├── Unassigned/
│   ├── Notes/  Transcripts/
│   └── ReviewQueue.md
└── Shared/
    ├── Notes/  Transcripts/
```

## What it does

- **Syncs** every meeting your Granola key can read and every call your Fireflies key can read (AI notes plus transcript; see [docs/FIREFLIES_API.md](docs/FIREFLIES_API.md)) and every completed transcript your WhisperAI key can read (transcript only; see [docs/WHISPERAI_API.md](docs/WHISPERAI_API.md)): a full historical import first, then incremental runs. You can sync manually or on a schedule, retry failed meetings, re-export, and reconcile the files on disk.
- **Classifies** each meeting with deterministic rules you configure. Rules can match participant emails or domains, attendee names, speakers Granola identified in the transcript, client mappings, Granola folders, and title or summary keywords. Every assignment records the rule and evidence behind it. AI-assisted classification is optional and off by default.
- **Never loses a meeting.** Meetings it can't place confidently go to `Unassigned/` and the review screen. A manual assignment always wins over the rules.
- **Writes notes and transcripts separately.** Each type has its own root folder and directory templates, and the defaults keep them in the same tree. Writes are atomic, filenames are stable, and collisions are resolved safely.
- **Protects your edits.** Each file carries a content hash. If you edit a file and the source meeting later changes, the file is marked as a conflict instead of being overwritten.
- **Keeps your key safe.** On Windows the API key is stored in Windows Credential Manager. Servers read it from environment variables. The browser UI never sees the full key.

## Architecture

A modular monolith: ASP.NET Core 10 Web API, EF Core with SQLite, and a background worker, plus a React + TypeScript (Vite, Mantine, TanStack Query) frontend.

```text
src/Chroniq.Domain          Entities, enums, normalisation rules
src/Chroniq.Application     Use cases: classification engine, Markdown export, sync engine, services, DTOs
src/Chroniq.Infrastructure  EF Core/SQLite, Granola API client, credential stores, file logger, Claude classifier
src/Chroniq.Worker          Scheduled synchronisation (BackgroundService)
src/Chroniq.Api             REST API, middleware, hosts the worker and the built frontend
frontend/                     React UI
tests/                        xUnit unit + integration tests (frontend tests live in frontend/)
tools/node-cli/               The earlier zero-dependency Node script, kept for reference
```

For details see [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Prerequisites

- .NET SDK 10.0
- Node.js 22 or later (only to build or develop the frontend)
- A Granola API key: in the Granola desktop app, go to **Settings → Connectors → API keys → Create new key** and choose the *Personal notes* scope. On Enterprise plans an admin must enable API access first.
- Optional: a Fireflies API key, from **app.fireflies.ai → Integrations → Fireflies API**.
- Optional: a WhisperAI API key (`wai_…`), from the [WhisperAI developer portal](https://whisperai.com/developer).

## Quick start (Windows desktop)

Double-click `run.bat`. It builds what is missing, starts the app at http://127.0.0.1:5080 and opens your browser. `stop.bat` stops it. Other modes: `run.bat rebuild` (after pulling changes), `run.bat dev` (hot-reload UI) and `run.bat test`.

To keep it always running instead, run `setup.bat [port] [minutes]` (default `5080`, `60`). It publishes the app to `publish\`, registers a "CHRONIQ" Task Scheduler task that starts it hidden at every Windows sign-in and restarts it after a crash, and turns on scheduled sync at that interval. `setup.bat stop` stops it until next sign-in, `setup.bat uninstall` removes the task (data is kept). Re-run `setup.bat` after pulling changes.

The manual equivalent:

```powershell
dotnet tool restore
./scripts/build-frontend.ps1                # builds the UI into src/Chroniq.Api/wwwroot
dotnet run --project src/Chroniq.Api      # http://127.0.0.1:5080
```

Then, in the app:

1. Go to **Settings → Connections**, paste the Granola API key, then click **Save & verify**. The key is validated and saved to Windows Credential Manager. To import Fireflies calls or WhisperAI transcripts too, add that key in its card below.
2. Under **Settings → Export**, choose the root folder. Optionally choose separate folders for notes and transcripts.
3. Create **Projects** and add rules. To find the right names, run a first sync and look at the **Unassigned** page: it lists each meeting's attendees and the speakers Granola identified.
4. Click **Sync now**. The first run is a full historical import. Set **Initial import from** first if you only want recent meetings.
5. Review **Unassigned**. Assign meetings, or save a mapping straight from a meeting; the app then offers to apply that rule to similar meetings after you confirm.
6. Optionally turn on **Automatic synchronization** under Settings → Synchronization.

## Development

```powershell
# Backend (Development: base directory = repo root, OpenAPI + Swagger UI at /swagger)
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/Chroniq.Api

# Frontend with hot reload (proxies /api to 127.0.0.1:5080)
cd frontend; npm install; npm run dev       # http://localhost:5173
```

The REST contract is documented by OpenAPI at `/openapi/v1.json` in Development, with a snapshot in [docs/openapi.json](docs/openapi.json).

## Production

| Scenario | How |
|---|---|
| Personal Windows machine | `dotnet publish src/Chroniq.Api -c Release -o publish` after building the frontend, then run `publish/Chroniq.Api.exe` (for example, from Task Scheduler at log-on). It listens on `127.0.0.1:5080` only. |
| Server / NAS | `docker compose up -d` with `GRANOLA_API_KEY` (and optionally `FIREFLIES_API_KEY`, `WHISPERAI_API_KEY`) and `CHRONIQ_API_TOKEN` in a `.env` file. Remote callers must send the API token; put HTTPS in front. See [docs/INSTALLATION.md](docs/INSTALLATION.md). |

## Configuration

Everything routine is configured in the UI. For the reference (settings, template variables, separate note/transcript roots, rule kinds, multi-project modes, conflict policy), see [docs/CONFIGURATION.md](docs/CONFIGURATION.md). Example files are in [config/](config/). `project-mappings.example.json` can be imported from the Projects page.

## How classification works

The tiers are evaluated in a configurable order, and the strongest one with evidence wins:

1. Manual assignment
2. Participant email
3. Participant identity (attendee name, email domain)
4. Speaker Granola identified by name
5. Client mapping
6. Granola folder
7. Keywords
8. Optional AI suggestion
9. Unassigned

Ambiguous results go to review by default. Anonymous labels such as "Speaker A" are never treated as identities. For details see [docs/CLASSIFICATION.md](docs/CLASSIFICATION.md).

## Where data lives

| Data | Location | Portable? |
|---|---|---|
| Meeting knowledge (Markdown) | Export root(s) | Yes. Copy, back up or put under Git. Files are self-describing (front matter has the Granola note ID). |
| Operational state: meetings, mappings, export records, sync history, settings | `data/chroniq.db` (SQLite) | Yes, with the app. Copying only the Markdown does **not** carry this over. |
| Granola / Fireflies / WhisperAI / AI API keys | Windows Credential Manager, or environment variables | No. Re-enter on a new machine. |
| Logs | `data/logs` (JSON lines; secrets redacted) | — |

Backup and recovery are covered in [docs/TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md#backup-and-recovery).

## Testing

```powershell
dotnet test                         # 129 unit + 35 integration tests, no credentials needed
cd frontend; npm test               # 21 Vitest + React Testing Library tests (fetch mocked)

# Optional, against a running app:
cd frontend; npx playwright install chromium; npm run e2e
                                    # read-only browser smoke test: every page renders without console errors
node scripts/e2e-live.mjs <empty-export-folder> 10
                                    # live check against your own Granola account (read-only on Granola):
                                    # sync, mapping, moves, manual override, conflict detection
```

CI (`.github/workflows/ci.yml`) restores, checks formatting, builds, runs all backend tests, builds and tests the frontend, and publishes the test results.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Installation](docs/INSTALLATION.md)
- [Configuration](docs/CONFIGURATION.md)
- [Granola API](docs/GRANOLA_API.md)
- [Fireflies integration](docs/FIREFLIES_API.md)
- [WhisperAI integration](docs/WHISPERAI_API.md)
- [Classification](docs/CLASSIFICATION.md)
- [Markdown format](docs/MARKDOWN_FORMAT.md)
- [Security](docs/SECURITY.md)
- [Troubleshooting & backup](docs/TROUBLESHOOTING.md)
