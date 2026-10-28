# FEAT-019 — Meeting browsing

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FEAT-007, FEAT-009; `MeetingQuery`

## User outcome

The user lists meetings with filters (project, client, status, source, dates, search), sorting and paging kept in the URL; opens a meeting to read notes, a paged transcript, the classification explanation and export status, and acts on it (reassign, reclassify, retry, re-export, resolve conflicts, open files).

## Users and permissions

Local user / token holder (middleware).

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Routes | `GET /api/meetings`, `GET /api/meetings/{id}`, `GET /api/meetings/{id}/transcript` | `MeetingService.QueryAsync/GetAsync/TranscriptAsync` | `MeetingsController.cs:14-22` |
| Screens | `/meetings`, `/meetings/:id` | `MeetingsPage`, `MeetingDetailPage` (582 lines), `MeetingsTable` | sub-audit |

## Behavior

- **Main flow:** paged queries return list DTOs; detail returns `MeetingDetailDto`; transcript is paged (default 100 segments).
- **Error cases:** unknown ID → 404 problem (tested).

## Data touched

Reads `Meetings`, `ExportRecords`, `MeetingParticipants`, `Projects`.

## Integrations and side effects

None (actions are covered by other features).

## Configuration and feature flags

None.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Unknown_meeting_returns_404_problem` | Integration | 404 contract |
| `pages.test.tsx` (MeetingsPage filters) | Component | Filter/URL behaviour |
| Playwright smoke | Manual | Pages render |

## Gaps and follow-up

- `MeetingDetailPage` is large and has no component test (sub-audit).
