# FEAT-007 — Edit protection and conflict resolution

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-010; NFR-001 (never lose user data); `ExportRecord.Status`; flow diagram in [architecture.md](../architecture.md#2-export-write-with-edit-protection)

## User outcome

If the user edits an exported file, later syncs never silently overwrite it. When the source meeting also changed, the file is marked as a conflict and the user chooses what to keep.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Review and resolve conflicts; force re-export | Middleware; explicit `force`/resolution parameters | `MeetingsController.cs:44-52` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | Every export write | `MeetingExporter.WriteAsync` | `MeetingExporter.cs:258-328` |
| Route | `GET /api/meetings/{id}/exports/{recordId}/conflict` | `MeetingService.ReviewConflictAsync` | `MeetingService.cs:222-227` |
| Route | `POST /api/meetings/{id}/exports/{recordId}/resolve` `{resolution: KeepLocal|ReplaceWithSource|SaveConflictCopy}` | `MeetingService.ResolveConflictAsync` | `MeetingService.cs:229-…` |
| Screen | Meeting detail → Conflict modal | `ConflictModal` | frontend |

## Behavior

- **Main flow:** Given an existing file, when a new version is rendered, then: a file whose front matter names another meeting is never overwritten (Conflict, unless Force); an unmodified file (front-matter `content_hash` equals the recomputed hash) is rewritten only if content changed; an edited file stays as is — `LocallyModified` if the source has not changed, `Conflict` if it has — unless Force or `ConflictPolicy.AlwaysOverwrite` (`MeetingExporter.cs:279-315`).
- **Resolution:** KeepLocal (status KeptLocal; not flagged again until the source changes), ReplaceWithSource (atomic overwrite), SaveConflictCopy (writes `<name>.conflict-yyyyMMddHHmmss.md` next to the file and keeps the local one) (`MeetingService.cs:234-257`).
- **Edge cases:** volatile front-matter lines are excluded from the hash so timestamps do not create false edits (`MarkdownRenderer.ComputeHash`); a file the user deleted while the source is unchanged is not recreated in Normal mode.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `ExportRecords` | R/W | `LastGeneratedHash`, `SourceContentHash`, `ConflictDetectedAt`, `Status` |

## Integrations and side effects

Reads and writes Markdown files; never deletes.

## Configuration and feature flags

`export.conflictPolicy` (`PreserveUserEdits` default, or `AlwaysOverwrite`).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `MeetingExporterTests` | Unit | LocallyModified vs Conflict, foreign files, Force |
| `MarkdownAndPathTests` | Unit | Hashing and unmodified detection |
| `scripts/e2e-live.mjs` | Manual | Conflict detection and resolution live |

## Gaps and follow-up

- A sync and a manual resolve on the same meeting are not serialised (RF-019).
