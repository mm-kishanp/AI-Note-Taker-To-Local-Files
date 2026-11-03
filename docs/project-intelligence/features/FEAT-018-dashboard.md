# FEAT-018 — Dashboard

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: `DashboardService` (`src/Chroniq.Application/Meetings/ReviewAndDashboardServices.cs`)

## User outcome

The home page shows counts (meetings, unassigned, conflicts, failures, projects) and recent sync activity, with a shortcut to open the export folder.

## Users and permissions

Local user / token holder (middleware).

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `GET /api/dashboard` | `DashboardService.GetAsync` | `MeetingsController.cs:87-93` |
| Screen | `/` | `DashboardPage`, `StatCard` (polled every 60 s by the layout) | sub-audit |

## Behavior

- **Main flow:** read-only aggregate query returning `DashboardDto`. Exact figures not traced (Unknown — `DashboardService` read only by signature).

## Data touched

Reads `Meetings`, `Projects`, `SyncRuns`.

## Integrations and side effects

None.

## Configuration and feature flags

None.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `frontend/e2e/smoke.spec.ts` | Manual browser smoke | Page renders without console errors |

## Gaps and follow-up

- No automated assertion of the figures.
