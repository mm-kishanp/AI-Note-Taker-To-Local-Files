# SEO Guide

Last reviewed: 2026-10-28

- Evidence scope: `frontend/index.html`, `frontend/public/`, `frontend/src/App.tsx` routes (sub-audit), `Program.cs` hosting, `RequestProtectionMiddleware`.
- Applicability: **Not applicable — the only web UI is a private, single-user admin SPA** served on `127.0.0.1:5080` (or behind a token in server mode). All content is the user's confidential meeting data loaded from `/api` after the loopback/token check (`Middleware.cs:76-95`); there are no public pages, no marketing site and no content meant to be indexed. No `robots.txt` or sitemap exists (Verified).

## Rendering

| Sub-project / site | Rendering | Framework | Evidence |
| --- | --- | --- | --- |
| `frontend/` (served by the API from `wwwroot`) | Client-only SPA | React 18 + Vite | `frontend/index.html`, `Program.cs:67-73` |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| All checks | Not applicable | Private tool; indexing would be undesirable |

## Findings

| Where | Issue | Impact | Severity | Evidence |
| --- | --- | --- | --- | --- |
| — | None | — | — | — |

## Recommended changes

| Change | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- |
| Optional: add `<meta name="robots" content="noindex, nofollow">` to `index.html` | Defence in depth if a server-mode instance is ever exposed publicly behind a proxy | S | P3 | — |
