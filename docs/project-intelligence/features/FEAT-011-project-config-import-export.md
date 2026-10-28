# FEAT-011 — Project configuration import/export

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: `config/project-mappings.example.json`

## User outcome

Clients, projects and rules can be exported as JSON and imported into another installation (or seeded from the example file).

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Export / import | Middleware | `ProjectsController.cs:56-60` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `GET /api/projects/config/export` | `ProjectService.ExportConfigAsync` | `ProjectService.cs:292` |
| Route | `POST /api/projects/config/import` | `ProjectService.ImportConfigAsync` | `ProjectService.cs:304` |
| Screen | Projects page import/export buttons | `ProjectsPage` | sub-audit |

## Behavior

- **Main flow:** Given a `ProjectConfigExport { Version, Clients, Projects }`, when imported, then clients and projects are created or updated and an `ImportResultDto` reports what happened.
- **Validation and error cases:** Unknown — the import's merge rules and size limits were not read in detail (`ProjectService.cs:304-332`).

## Data touched

`Clients`, `Projects`, `ClassificationRules`.

## Integrations and side effects

None.

## Configuration and feature flags

None.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Project_config_export_import_round_trip` | Integration | Round trip preserves the configuration |

## Gaps and follow-up

- Document import merge semantics (update vs duplicate) in `docs/CONFIGURATION.md` after reading `ImportConfigAsync`.
