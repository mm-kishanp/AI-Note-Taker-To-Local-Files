# FEAT-013 — Export browser and open in editor

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: SEC-002; `ExportBrowserService`

## User outcome

The user browses the export tree (root, notes or transcripts), previews any Markdown file in the app, and opens a file or folder in Explorer / the default editor.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Browse and read `.md` under the roots; open in OS (desktop only) | Middleware + root guards | `ExportBrowserService.cs` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `GET /api/exports/tree`, `GET /api/exports/file`, `POST /api/exports/open` | `ExportBrowserService` | `SystemControllers.cs:182-198` |
| Route | `POST /api/meetings/{id}/exports/{recordId}/open` | `OpenAbsoluteUnderRoots` | `MeetingsController.cs:54-61` |
| Screen | `/exports`, Dashboard "open folder", meeting detail | `ExportBrowserPage`, `Markdown` component | sub-audit |

## Behavior

- **Main flow:** tree lists folders (hidden `.`-folders and reparse points skipped) and `.md` files, up to 5000 nodes with a warning; file read resolves the relative path under the chosen root with `SafePath.Combine` and only allows `.md` ≤ 20 MB (`ExportBrowserService.cs:24-78`).
- **Open:** fails with a validation error when not running on a desktop OS; the path must exist and be under a root; launched with `explorer.exe`/`open`/`xdg-open` and a single argument (`DependencyInjection.cs:57-70`).
- **Error cases:** traversal (`../`) → 400 (tested); missing file → 404.
- **UI rendering:** `react-markdown` with `skipHtml`; images become links (sub-audit).

## Data touched

Reads export settings and files only.

## Integrations and side effects

OS shell (INT-005).

## Configuration and feature flags

Export roots.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Export_browser_blocks_path_traversal` | Integration | Root guard |
| Playwright smoke | Manual | Page renders |

## Gaps and follow-up

- No component test for `ExportBrowserPage`/`Markdown` sanitisation (sub-audit).
