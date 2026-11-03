# Installation

## Requirements

- .NET SDK 10.0 to build; the ASP.NET Core 10 runtime is enough to run a published build.
- Node.js 22 or later, to build the frontend.
- A Granola API key (Granola desktop app → Settings → Connectors → API keys).

## Windows desktop (recommended)

```powershell
dotnet tool restore
./scripts/build-frontend.ps1                       # npm ci + build, copies the UI into src/Chroniq.Api/wwwroot
dotnet publish src/Chroniq.Api -c Release -o C:\Apps\Chroniq
C:\Apps\Chroniq\Chroniq.Api.exe                # http://127.0.0.1:5080
```

- The database is created and migrated automatically at startup, in `C:\Apps\Chroniq\data\`. To keep data elsewhere, set `App:BaseDirectory` or `App:DataDirectory` in `appsettings.json`.
- To start the app at log-on, create a Task Scheduler task that runs `Chroniq.Api.exe` with "Start in" set to the install folder, triggered at log-on.
- First run: open the UI, then go to Settings → Connections → paste the Granola key (and optionally the Fireflies key) → Save & verify. After that, choose the export root and run **Sync now**.

## Development

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Development"        # base directory = repo root; Swagger at /swagger
dotnet run --project src/Chroniq.Api
cd frontend; npm install; npm run dev              # http://localhost:5173 (proxies /api)
```

To create a database migration after changing entities:

```powershell
dotnet ef migrations add <Name> --project src/Chroniq.Infrastructure --startup-project src/Chroniq.Api --output-dir Persistence/Migrations
```

## Docker / server

```bash
cat > .env <<'EOF'
GRANOLA_API_KEY=grn_...
CHRONIQ_API_TOKEN=<a long random string>
EOF
docker compose up -d --build                       # http://127.0.0.1:8080
```

- The container stores state in a named volume (`/data/state`) and writes the Markdown repository to `./ChroniqKnowledge` on the host. In Settings → Export, set the export root to `/data/ChroniqKnowledge`.
- All API calls require the token. The UI asks for it once per browser session.
- Expose it beyond localhost only behind an HTTPS reverse proxy.
- Folder pickers and "open in editor" refer to the container's file system. They are of limited use there.

## Secrets in CI

The CI pipeline needs no secrets: all tests use fakes. To run the optional live check (`scripts/e2e-live.mjs`) in a pipeline, store `GRANOLA_API_KEY` as an encrypted secret, start the API with it, and run the script against a throw-away export folder. Never put keys in workflow files.

## Uninstall

1. Stop the app.
2. Delete the install folder, and `data/` if you no longer need the history.
3. Remove the credential in Windows Credential Manager (`Chroniq:*`).

Your Markdown export folders are yours. Keep or delete them as you like.
