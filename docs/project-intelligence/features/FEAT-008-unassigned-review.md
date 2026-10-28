# FEAT-008 — Unassigned review queue and mapping from review

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-011; `/unassigned` page; `ReviewQueue.md` (FEAT-012)

## User outcome

Meetings the rules could not place are listed with their attendees and identified speakers so the user can assign them, or save a participant/speaker mapping straight from a meeting and apply it to similar meetings after confirming.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | List, assign, save mapping | Middleware | `MeetingsController.cs:71-85` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `GET /api/unassigned` | `UnassignedService.ListAsync` | `ReviewAndDashboardServices.cs` |
| Route | `POST /api/unassigned/{id}/assign` | `MeetingService.AssignAsync` | FEAT-009 |
| Route | `POST /api/unassigned/{id}/mappings` | `UnassignedService.SaveMappingAsync` → preview of matching meetings | `ReviewAndDashboardServices.cs:128` |
| Screen | `/unassigned`, `SaveMappingModal` | frontend | sub-audit |

## Behavior

- **Main flow:** Given unassigned meetings, when the user saves a mapping (e.g. an attendee email → project), then a rule is created and a dry-run result lists other unassigned meetings it would route; applying uses `POST /api/projects/{id}/apply-mapping` with explicit confirmation, which re-classifies and re-exports in the background (`ProjectsController.cs:47-54`).
- **Validation:** apply requires `confirm=true` and at least one meeting ID.

## Data touched

`Meetings` (read), `ClassificationRules` (write), then FEAT-004/006 effects.

## Integrations and side effects

Moves files from `Unassigned/` to project folders when unmodified (FEAT-009).

## Configuration and feature flags

None specific.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Unassigned_meeting_is_reassigned_after_saving_a_mapping` | Integration | Save mapping → apply → reassigned and moved |
| `pages.test.tsx` (SaveMappingModal confirmation) | Component | UI requires confirmation |

## Gaps and follow-up

None significant found.
