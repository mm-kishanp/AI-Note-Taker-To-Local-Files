# FEAT-006 — Markdown export of notes and transcripts

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-008, FR-009; [docs/MARKDOWN_FORMAT.md](../../MARKDOWN_FORMAT.md), [docs/CONFIGURATION.md](../../CONFIGURATION.md); entity `ExportRecord`; INT-005

## User outcome

Each meeting becomes two self-describing Markdown files — notes and transcript — under folders the user controls (`Projects/<slug>/Notes|Transcripts`, `Unassigned/…`, `Shared/…` by default), with stable file names, links between the two, and front matter that identifies the source meeting. The folder is useful without the app (Git, VS Code, coding agents).

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Configure roots, templates, what to export; re-export a meeting | Settings API, `POST /api/meetings/{id}/export` | controllers |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | Every processed meeting | `MeetingExporter.ExportAsync` | `MeetingProcessor.cs:131` |
| Route | `POST /api/meetings/{id}/export` (`force` optional) | `MeetingService.ReExportAsync` (Reconcile or Force mode) | `MeetingsController.cs:44-45`, `MeetingService.cs:203-208` |
| Route | `POST /api/sync/start {mode: ReExport|Reconcile}` | `SyncEngine` local items | `SyncEngine.cs:124-131` |

## Behavior

- **Main flow:** Given a classified meeting, when exported, then targets are computed (Assigned → project; MultiProject → primary + copies; Shared; Excluded → nothing; else Unassigned) (`MeetingExporter.cs:142-160`); for each target and kind an `ExportRecord` is found or created. New records get a path from the directory template + file-name template (`{MeetingDate}-{MeetingTitle}` by default), made unique with the last 6 chars of the ID if taken (DB records or a foreign file) and suffixed `-transcript` when notes and transcripts share a folder (`MeetingExporter.cs:163-256`). Existing records keep their path (stable names). Content is rendered with front matter and a content hash, then written atomically (temp file + flush + rename) (`AtomicFile.cs:21-48`).
- **Validation and error cases:** templates are validated (no rooted paths, `..`, invalid chars; project template must contain `{ProjectSlug}` or `{ProjectName}`) (`ExportLayout.Validate`, `PathTemplates.Validate`); every resulting path is checked to stay under its root (`SafePath.Combine`); disk-full/permission/IO errors are classified and the meeting retried later; a failed transcript retrieval is recorded as Failed, an empty transcript as Skipped, never written empty (`MeetingExporter.cs:102-134`); meetings with no usable content skip the notes file.
- **Edge cases:** Windows reserved names and over-long names are sanitised (`FileNames`); records for targets no longer valid become `Superseded` and their files are left in place (`SupersedeStaleRecords`); deleted/inaccessible source meetings write nothing new; a user-deleted file whose source is unchanged becomes `Missing` and is recreated only by Reconcile.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `ExportRecords` | R/W | relative path, hashes, status per file |
| `Meetings` | W | aggregate `ExportStatus`, `ExportPending`, paths |

## Integrations and side effects

Writes `.md` files under the configured roots (INT-005). Edit protection: FEAT-007. Reassignment moves: FEAT-009.

## Configuration and feature flags

`export.rootDirectory` (`./ChroniqKnowledge`), `notesRootDirectory`, `transcriptsRootDirectory`, `projectDirectoryTemplate`, `notesDirectoryTemplate`, `transcriptsDirectoryTemplate`, `unassigned*/shared*Directory`, `filenameTemplate`, `exportNotes`, `exportTranscripts`, `includeAttachmentLinks`, `conflictPolicy`, `moveFilesOnReassignment` (`AppSettings.cs:11-31`); per-project `NotesFolderPath`/`TranscriptFolderPath` overrides.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `MeetingExporterTests` (20) | Unit | Targets, stable names, collisions, Missing/Reconcile, moves, failures |
| `MarkdownAndPathTests` (18 + 22 data rows) | Unit | Rendering, escaping, hashing, template validation, sanitisation, root guards |
| `SyncIntegrationTests` | Integration | Files on disk after real runs |

## Gaps and follow-up

None significant found.
