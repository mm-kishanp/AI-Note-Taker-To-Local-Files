# FEAT-024 — Fireflies recording download

Last reviewed: 2026-11-12

- Status: Implemented (tested with fakes; not yet run against the live Fireflies API, see Gaps)
- Confidence: High for behaviour (code and tests); Medium for the live API's error codes on plans without recording access
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: INT-002; `Meeting.Recordings`; [docs/FIREFLIES_API.md](../../FIREFLIES_API.md#recordings-audio-and-video); ADR-012; PIDA-020

## User outcome

With "Download call audio / video (Fireflies)" turned on, each Fireflies call's recording is saved during sync into a `Recordings` folder beside the call's notes and transcript, and listed on the meeting's Recordings tab.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Turn the switches on; sync; open a downloaded recording | Middleware | `Middleware.cs` |
| Fireflies account | Needs a Pro plan or higher (Fireflies' rule, not enforced by the app) | Fireflies API | docs.fireflies.ai/schema/transcript |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Setting | `export.exportAudio`, `export.exportVideo` (default off) | `ExportSettings` | `AppSettings.cs` |
| Sync | Runs inside `MeetingProcessor` after export, for Fireflies meetings | `RecordingService.SyncAsync` | `RecordingService.cs`, `MeetingProcessor.cs` |
| Route | `POST /api/meetings/{id}/recordings/{recordingId}/open?folder=` | `MeetingsController.OpenRecording` | `MeetingsController.cs` |
| UI | Meeting detail, "Recordings" tab; Settings, Export | `MeetingDetailPage.tsx`, `SettingsPage.tsx` | — |

## Behavior

- **Main flow:** Given audio and/or video is enabled and the call's primary export exists, when the call is processed, then a fresh link is requested per kind (`audio_url` / `video_url`, one small query each, apart from the transcript query), streamed to a temp file by `RecordingDownloader`, renamed when complete, and recorded in `MeetingRecordings` with its path and size. The folder follows the call's primary target: `{ProjectDirectory}/Recordings`, `Unassigned/Recordings` or `Shared/Recordings`, under an optional separate recordings root.
- **Backfill:** `SyncEngine.DiscoverFirefliesAsync` queues an already-complete call (without refetching its transcript) when an enabled kind has no row yet, or failed with attempts left (`RecordingService.NeedsDownload`). Incremental runs only list calls within the look-back, so older calls need a Full sync or Reconcile.
- **Errors never fail the call:** A Permission error (plan without recordings) marks the kind *Unavailable* and is not asked again. Other API errors and download errors mark it *Failed* and increase `Attempts` (limit 5, `RecordingService.MaxAttempts`). Authentication errors stop the run as usual. A missing link is final only once the call is older than 2 days (`NotReadyWindow`); until then no row is written and the next sync asks again.
- **Edge cases:** A file the user moved or deleted is not downloaded again by a normal sync; Reconcile and Force recreate it. Reassigning the call moves the recording (`MoveFilesOnReassignment`). A name clash with another call gets the call's short id appended. A call shared by several projects keeps one recording, in the primary project.
- **Security:** The signed link is never stored, logged or echoed in errors; only HTTPS links are fetched, with no credentials; file names use known media extensions only (a link cannot make the app save `.exe` or script files); "open" is restricted to the export roots, which now include the recordings root.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `MeetingRecordings` (new, migration `AddMeetingRecordings`) | R/W | One row per meeting and kind (unique index); `Status` Downloaded / Failed / Unavailable; relative path, size, attempts, last error |
| `ApplicationSettings` `export` | R/W | Six new fields, all with defaults, so existing settings stay valid |
| Recordings folder on disk | W | Audio and video files; large |

## Integrations and side effects

INT-002 (Fireflies GraphQL): two extra queries. The download itself goes to the file host named in the signed link (Fireflies' storage), not to the Fireflies API.

## Configuration and feature flags

`export.exportAudio`, `export.exportVideo`, `export.recordingsRootDirectory`, `export.recordingsDirectoryTemplate`, `export.unassignedRecordingsDirectory`, `export.sharedRecordingsDirectory`. See [CONFIGURATION.md](../../CONFIGURATION.md).

## Client availability

Not applicable: single-user app.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `FirefliesRecordingTests` (9) | Integration | Off by default; audio and video saved under Recordings; project folder; plan without access is not asked again; failure then retry without refetching the transcript; backfill after turning it on; old versus young calls with no link; detail API; Fireflies only |
| `RecordingDownloaderTests` (18 cases) | Unit | Extension choice (no unsafe extensions), HTTP status mapping, error pages refused, empty download, HTTPS only, no overwrite, temp file removed, signed link not echoed |
| `pages.test.tsx` "MeetingDetailPage: recordings" (3) | Frontend | Tab only when recordings exist; statuses and sizes; Play calls the open endpoint with the CSRF header; missing and failed states |

## Gaps and follow-up

- Not run against the live Fireflies API (Q-013). The query field names and the 24-hour expiry come from docs.fireflies.ai/schema/transcript. The exact error code for a plan without access is unknown: unmapped codes surface as *Failed* with Fireflies' message, up to 5 attempts, instead of *Not available*.
- Notes and transcript Markdown do not link to the recording, to avoid rewriting files users may have edited.
- Granola and WhisperAI recordings are out of scope (Granola exposes none; WhisperAI's `audio_url` is the user's own upload).
- No size limit, quota or cleanup for recordings; disk space is only checked from the response's `Content-Length`.
