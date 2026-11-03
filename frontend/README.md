# Frontend

React 18 + TypeScript (strict) + Vite, React Router 6, TanStack Query 5, Mantine 7. The UI talks only to the documented REST API (`/api`, see `../docs/openapi.json`).

## Development

Start the backend first (`dotnet run --project ../src/Chroniq.Api`, listening on `127.0.0.1:5080`), then:

```powershell
npm install
npm run dev        # http://localhost:5173 — proxies /api and /openapi to the backend
```

Set `CHRONIQ_BACKEND_URL` to proxy to another backend address.

## Commands

| Command | Purpose |
|---|---|
| `npm run build` | Type-check (`tsc --noEmit`) and build into `dist/` |
| `npm test` | Vitest + React Testing Library (fetch is mocked; no backend needed) |
| `npm run typecheck` | Type-check only |
| `../scripts/build-frontend.ps1` | `npm ci` + build, then copy `dist/` into `src/Chroniq.Api/wwwroot` for production hosting |

## Structure

```text
src/services/api.ts   Typed fetch client: CSRF header on writes, optional bearer token, RFC 7807 problem parsing
src/hooks/            TanStack Query hooks per resource (queryKeys.ts keeps invalidation consistent)
src/pages/            Dashboard, Meetings, Meeting detail, Projects, Project detail, Unassigned, Sync history, Export browser, Settings
src/components/       Shared UI (StatusBadge, RuleEditor, FolderPicker, Reassign/SaveMapping/Conflict modals, …)
src/types/api.ts      Types matching the backend DTOs
src/test/             Test setup, fetch router mock, fixtures
```

## Conventions

- Every non-GET request sends `X-Chroniq-Request: 1` (the backend's CSRF guard).
- Remote deployments: if the server answers 401 "A valid API token is required", the UI asks for the token once and keeps it in `sessionStorage` (`gpi.apiToken`).
- Meeting content is untrusted: Markdown is rendered with raw HTML disabled, and images are shown as links.
- Status badges always show text; color is only a secondary cue.
