# Frontend (React SPA)

Last reviewed: 2026-10-28

- Type: Web app
- Location: `frontend/`
- Status: Active
- Confidence: High (from a read-only sub-audit of `frontend/`)
- Part of: [project.md](../../project.md)

## What it is

The user interface: a single-page React app for the dashboard, meetings, projects and clients, unassigned review, sync history, export browser and settings. It is built separately with Vite and then served by the API from `wwwroot/`.

## Responsibilities

- Routes (`frontend/src/App.tsx`): `/`, `/meetings`, `/meetings/:id`, `/projects`, `/clients`, `/projects/:id`, `/unassigned`, `/sync-history`, `/exports`, `/settings`, `*`.
- API client with CSRF header, optional bearer token from `sessionStorage`, RFC 7807 error mapping (`frontend/src/services/api.ts`).
- TanStack Query hooks per resource (`frontend/src/hooks/`); Mantine UI components (`frontend/src/components/`).

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-008, 009, 010, 011, 013, 014, 015, 016, 017, 018, 019 | owns the screens | [feature-inventory.md](../../feature-inventory.md) |
| FEAT-020 | contributes (`ApiTokenModal`, CSRF header) | [FEAT-020](../../features/FEAT-020-request-protection.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `index.html` → `frontend/src/main.tsx` → `AppRoutes` | sub-audit |
| Runtime / framework | React 18.3.1, react-router-dom 6.28.2, Mantine 7.17.8, TanStack Query 5.66.0, react-markdown 9.0.3; TypeScript 5.6.3, Vite 5.4.14 | `package.json` |
| Build | `npm run build` (`tsc --noEmit && vite build`) (Documented) | `package.json` |
| Run locally | `npm run dev` → `http://localhost:5173` (proxy `/api`) (Documented) | `vite.config.ts` |
| Tests | Vitest + Testing Library: `api.test.ts` (5), `StatusBadge.test.tsx` (3+), `pages.test.tsx` (5); Playwright `frontend/e2e/smoke.spec.ts` (manual) | sub-audit |
| Deployed as | Static bundle in the API's `wwwroot/` | `scripts/build-frontend.ps1`, `Dockerfile` |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| API host | sub-project | All data via `/api` | `api.ts` |

## Interfaces

- **Exposes:** browser UI.
- **Consumes:** the REST API ([api-catalog.md](../../api-catalog.md)).

## Data

None stored except the API token in `sessionStorage` (`gpi.apiToken`).

## Configuration

`GPI_BACKEND_URL` (dev proxy target), `GPI_E2E_URL` (Playwright).

## Ownership and conventions

page → hook → `api` layering; strict TypeScript; no ESLint/Prettier.

## Risks and gaps

- **RF-001 (High):** committed tsc output (`src/**/*.js`, `vite.config.js`, `tsconfig.tsbuildinfo`) shadows the sources.
- No lint (RF-014); large pages (`SettingsPage.tsx` 646 lines, `MeetingDetailPage.tsx` 582); unused API calls (RF-016); several pages without tests.
