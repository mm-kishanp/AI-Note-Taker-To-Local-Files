# FEAT-014 — Export root migration

Last reviewed: 2026-10-28

- Status: Partial
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: RF-018; folder picker and path validation

## User outcome

The user moves the whole export repository to a new folder: files are copied, settings switch to the new root, and the old folder is left untouched.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Validate a folder, browse folders, migrate | Middleware; explicit `confirm` | `SystemControllers.cs:200-208` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `POST /api/exports/migrate-root` | `ExportBrowserService.MigrateRootAsync` | `ExportBrowserService.cs:188-235` |
| Route | `POST /api/exports/validate-path`, `GET /api/exports/directories` | `ValidatePath`, `ListDirectories` | `:98-182` |
| Screen | Settings → Export (FolderPicker) | `SettingsPage`, `FolderPicker` | sub-audit |

## Behavior

- **Main flow:** Given `confirm=true` and a writable new root that is neither the current root nor nested with it, when migrating, then every file except temp files is copied; identical files at the destination are skipped, different ones kept with a warning (never overwritten); `export.rootDirectory` is updated; a warning reminds that separate notes/transcripts roots were not moved.
- **Validation:** writability probe file, free-space warning below 100 MB, invalid paths rejected.
- **Edge cases:** cancellable between files; large repositories are copied synchronously within the HTTP request (Inferred — could be slow).

## Data touched

`ApplicationSettings` `export`; files.

## Integrations and side effects

File copies (INT-005). `validate-path` with `createIfMissing` creates folders (SEC-002).

## Configuration and feature flags

None.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| none | — | No test references `MigrateRoot`/`migrate-root` (Verified by grep) |

## Gaps and follow-up

- Add integration tests (RF-018, PIDA-007): copy, skip identical, keep different, reject nested, settings switch.
