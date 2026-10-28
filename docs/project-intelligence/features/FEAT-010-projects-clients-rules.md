# FEAT-010 — Projects, clients and rules management

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable (the `Client` entity here means the user's customers — see [client-matrix.md](../client-matrix.md))
- Related: FR-012; entities `Project`, `Client`, `ClassificationRule`

## User outcome

The user maintains projects (name, unique slug, description, customer, optional folder overrides, priority), customers, and the routing rules for each; can dry-run rules against existing meetings and apply them in bulk after confirming; can archive and restore projects.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | CRUD projects (no delete), CRUD clients, add rules, test/apply mappings | Middleware | `ProjectsController.cs` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Routes | `/api/projects` (list/create/get/update/archive/restore/rules/test-mapping/apply-mapping) | `ProjectService` | `ProjectsController.cs:11-54` |
| Routes | `/api/clients` (list/create/update/delete) | `ProjectService` | `ProjectsController.cs:63-82` |
| Screens | `/projects`, `/clients`, `/projects/:id` | `ProjectsPage`, `ProjectDetailPage`, `ProjectFormModal`, `ClientFormModal`, `RuleEditor` | sub-audit |

## Behavior

- **Main flow:** Given a valid `ProjectInput`, when saved, then the project and its rules are stored with normalised rule values (`TextNormalizer`); slugs are unique; folder overrides cannot use `{ProjectDirectory}` (`ProjectService.cs:106`).
- **Test mapping:** dry run that shows which meetings the given or saved rules would route; changes nothing (`ProjectsController.cs:41-44`).
- **Apply mapping:** requires confirmation and a meeting list; starts a background ReExport with reclassification (`:47-54`).
- **Validation:** field-level `ValidationException`s (400 with `errors[]`).
- **Edge cases:** deleting a client sets its projects' `ClientId` to null and cascades client rules (`AppDbContext.cs:61, 69`); archived projects stop receiving new meetings (`ProjectProfile.Active`).

## Data touched

`Projects`, `Clients`, `ClassificationRules` (R/W); `Meetings` via apply.

## Integrations and side effects

Apply mapping moves/rewrites files (FEAT-006/009).

## Configuration and feature flags

None.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Project_validation_and_unique_slugs` | Integration | Validation, slug uniqueness |
| `Unassigned_meeting_is_reassigned_after_saving_a_mapping` | Integration | Apply mapping path |

## Gaps and follow-up

- No delete endpoint for projects or individual rules (archive only; rules presumably replaced via update) — Q-007.
- `POST /api/projects/{id}/rules` is not used by the UI (RF-016).
- No UI tests for Projects/ProjectDetail pages (sub-audit).
