# FEAT-009 — Manual assignment and reassignment

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-007; `ManualAssignmentKind` (None, Project, Shared, Excluded); `ClassificationReview`

## User outcome

The user can override where a meeting goes (a project, Shared, or Excluded from export, or clear the override). The override survives source updates and rule changes, and unmodified files move to the new location instead of being duplicated.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Assign / preview destination / reclassify | Middleware | `MeetingsController.cs:24-33` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `POST /api/meetings/{id}/assign-project` | `MeetingService.AssignAsync` | `MeetingService.cs:165-192` |
| Route | `GET /api/meetings/{id}/destination-preview` | `MeetingService.PreviewDestinationAsync` | `MeetingsController.cs:28-30` |
| Screen | Meeting detail → `ReassignModal` | frontend | sub-audit |

## Behavior

- **Main flow:** Given a meeting, when the user assigns it, then the manual fields are saved, a `ClassificationReview` audit row is added, and the meeting is re-classified (manual wins) and re-exported synchronously within the request (`MeetingService.cs:165-220`). The previous unmodified primary file is moved to the new path when `moveFilesOnReassignment` is on and nothing exists at the destination; otherwise the old record is superseded and its file left in place (`MeetingExporter.cs:195-214`).
- **Validation:** `Project` requires an existing project ID (400 otherwise).
- **Edge cases:** if the manual project is later removed, the meeting falls back to automatic classification and is flagged for review (`ClassificationEngine.cs:73-80`).

## Data touched

`Meetings` (manual fields), `ClassificationReviews` (audit), `ExportRecords`, project context files.

## Integrations and side effects

File moves under export roots (never overwriting).

## Configuration and feature flags

`export.moveFilesOnReassignment` (true).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Manual_override_survives_source_updates_and_rule_changes` | Integration | Manual wins over updates and rules |
| `pages.test.tsx` (ReassignModal preview) | Component | Destination preview |
| `MeetingExporterTests` | Unit | Moves on reassignment |

## Gaps and follow-up

- Reprocessing runs inside the HTTP request without the sync lease and can race a running sync on the same meeting (RF-019).
