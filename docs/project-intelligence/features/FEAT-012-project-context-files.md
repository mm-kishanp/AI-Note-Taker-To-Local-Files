# FEAT-012 — Project context and review queue files

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: `README.md` repository layout; [docs/MARKDOWN_FORMAT.md](../../MARKDOWN_FORMAT.md)

## User outcome

Each project folder contains a generated `PROJECT_CONTEXT.md` (meeting index plus items taken from summaries) and a `README.md` created once for the user to edit; the unassigned area contains `ReviewQueue.md`. A coding agent pointed at a project folder gets a ready index.

## Users and permissions

Not applicable — generated automatically; toggles in Settings.

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | End of each sync run; after each manual reprocess | `ProjectContextService.UpdateProjectsAsync`, `UpdateReviewQueueAsync` | `SyncEngine.cs:197-200`, `MeetingService.cs:218-219` |

## Behavior

- **Main flow:** Given projects touched by a run, when the run ends, then their context files are regenerated under the notes root (`ProjectContextService.cs:46`), and the review queue is rewritten at the parent of the unassigned notes folder (`ExportLayout.ReviewQueuePath`).
- **Edge cases:** the project `README.md` is created once and never overwritten (README claim; Inferred from `README.md:13` — not traced line by line).

## Data touched

Reads `Meetings`, `Projects`; writes files only.

## Integrations and side effects

Files under the notes root.

## Configuration and feature flags

`export.generateProjectContext` (true), `export.generateReviewQueue` (true) (`AppSettings.cs:30-31`).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `SyncIntegrationTests`, `MeetingExporterTests` (reference `PROJECT_CONTEXT`/`ReviewQueue`) | Integration / unit | Files generated (exact assertions not reviewed) |

## Gaps and follow-up

- Confirm the "README created once" behaviour with a dedicated test.
