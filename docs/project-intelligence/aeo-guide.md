# AEO Guide

Last reviewed: 2026-10-28

- Evidence scope: `frontend/index.html`, `frontend/public/`, routing, hosting (same as [seo-guide.md](seo-guide.md)).
- Applicability: **Not applicable — no public content.** The SPA is a private admin UI for one user's confidential meeting data. It is not meant to be crawled or cited by AI assistants.

Note: the *output* of the app (the Markdown export folder) is designed to be read by coding agents such as Claude Code or Copilot on the user's machine (`README.md:5`). That is local file access, not web crawling, so answer-engine optimisation does not apply.

## AI crawler access

| Item | Current state | Evidence |
| --- | --- | --- |
| `robots.txt` rules for AI crawlers | Absent (no `robots.txt`) | `frontend/public/` contains only `favicon.svg` |
| Content available without running JavaScript | No (client-only SPA, data behind loopback/token) | `index.html` |
| `llms.txt` | Absent | — |

Allowing or blocking AI crawlers is a business decision; given the content is private, no change is recommended beyond the optional `noindex` in the SEO guide.

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| All checks | Not applicable | No public content |

## Findings

| Where | Issue | Impact | Severity | Evidence |
| --- | --- | --- | --- | --- |
| — | None | — | — | — |

## Recommended changes

| Change | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- |
| None | Not applicable | — | — | — |
