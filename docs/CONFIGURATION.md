# Configuration

Routine configuration happens in the UI (Settings, Projects, Clients). Settings are validated when saved and stored in the database (`ApplicationSettings`). A setting that is missing falls back to its default, so upgrading keeps working. [`config/export-settings.example.json`](../config/export-settings.example.json) shows every setting with its default.

Secrets (API keys) are **not** settings. See [SECURITY.md](SECURITY.md).

## Export

| Setting | Default | Meaning |
|---|---|---|
| `rootDirectory` | `./GranolaProjectKnowledge` | Main export root. Can be absolute or relative to the app base directory. |
| `notesRootDirectory` | *(root)* | Optional separate root for notes |
| `transcriptsRootDirectory` | *(root)* | Optional separate root for transcripts |
| `projectDirectoryTemplate` | `Projects/{ProjectSlug}` | Folder for a project. Must contain `{ProjectSlug}` or `{ProjectName}`. |
| `notesDirectoryTemplate` | `{ProjectDirectory}/Notes` | Notes folder, relative to the notes root |
| `transcriptsDirectoryTemplate` | `{ProjectDirectory}/Transcripts` | Transcripts folder, relative to the transcripts root |
| `unassignedNotesDirectory` / `unassignedTranscriptsDirectory` | `Unassigned/Notes`, `Unassigned/Transcripts` | Destination for meetings that need review. `ReviewQueue.md` is written to the parent folder. |
| `sharedNotesDirectory` / `sharedTranscriptsDirectory` | `Shared/Notes`, `Shared/Transcripts` | Destination for meetings marked Shared |
| `filenameTemplate` | `{MeetingDate}-{MeetingTitle}` | File name without `.md`. Must not contain folders. |
| `exportNotes` / `exportTranscripts` | `true` | Turn either kind off |
| `includeAttachmentLinks` | `true` | Link Granola photo attachments. The URLs are signed, so anyone holding them can view the photo. |
| `conflictPolicy` | `PreserveUserEdits` | Or `AlwaysOverwrite`, to make generated files fully managed by the app |
| `moveFilesOnReassignment` | `true` | Move unmodified files when a meeting changes project |
| `generateProjectContext` | `true` | Maintain `README.md` and `PROJECT_CONTEXT.md` for each project |
| `generateReviewQueue` | `true` | Maintain `Unassigned/ReviewQueue.md` |

### Template variables

| Variable | Example | Available in |
|---|---|---|
| `{ProjectName}` | `Motor Dealer App` | project templates, folder overrides, filename |
| `{ProjectSlug}` | `MotorDealerApp` | project templates, folder overrides, filename |
| `{ClientName}` | `Example Client A` (`NoClient` if none) | project templates, folder overrides, filename |
| `{ProjectDirectory}` | `Projects/MotorDealerApp` | notes/transcripts templates only |
| `{MeetingDate}` | `2026-10-26` | everywhere |
| `{Year}` `{Month}` `{Day}` | `2026` `10` `26` | everywhere |
| `{MeetingId}` | `not_1d3tmYTlCICgjy` | everywhere |
| `{MeetingTitle}` | `Client-Requirement-Discussion` in file names | everywhere |

Templates are validated when saved. They must be relative, must not use `.` or `..` segments, must not contain characters that are invalid in file names, and may only use known variables. Variable **values** are sanitised into single path segments, so a project name such as `../x` cannot escape the root. Every resolved path is also checked against its root before any write.

### Separate destinations for notes and transcripts

```json
{
  "rootDirectory": "D:/Knowledge/Granola",
  "notesRootDirectory": "D:/MeetingNotes",
  "transcriptsRootDirectory": "D:/MeetingTranscripts",
  "notesDirectoryTemplate": "{ProjectDirectory}",
  "transcriptsDirectoryTemplate": "{ProjectDirectory}"
}
```

This produces `D:/MeetingNotes/Projects/MotorDealerApp/…` and `D:/MeetingTranscripts/Projects/MotorDealerApp/…`. If both kinds resolve to the same folder, the transcript file is named `…-transcript.md`.

### Per-project folder overrides

A project can override its notes and/or transcripts folder, for example `Clients/{ClientName}/{ProjectSlug}/Notes`. The override is relative to the corresponding root.

### Changing paths later

Existing files keep their paths: export records are sticky, and filenames stay stable across re-exports. Changing a template or project slug affects only meetings exported for the first time after the change, and meetings that move to another project. Use **Export Browser → Migrate root** to copy the whole repository to a new root. It never overwrites files at the destination, never deletes the old folder, and updates the setting afterwards.

## Synchronization

| Setting | Default | Meaning |
|---|---|---|
| `autoSyncEnabled` | `false` | Run incremental syncs on a schedule |
| `intervalMinutes` | `30` | 5–1440 |
| `initialImportFrom` | *(all)* | Only import notes created on or after this date during the first full import |
| `maxConcurrency` | `2` | Meetings processed in parallel (1–8). Requests are throttled to stay under Granola's limits anyway. |
| `maxRequestAttempts` | `5` | Attempts per Granola request for transient errors |
| `maxMeetingRetries` | `5` | Scheduled retries for a failing meeting before only a manual retry will try again |
| `incrementalOverlapMinutes` | `10` | Overlap applied to the incremental checkpoint, to tolerate clock skew |

## Classification

| Setting | Default | Meaning |
|---|---|---|
| `tierOrder` | email → identity → speaker → client → metadata → keyword → AI | Evaluation order |
| `disabledTiers` | `[]` | Tiers to skip |
| `multiProjectMode` | `SingleProject` | What happens when one tier matches several projects: `SingleProject` (review), `Shared`, or `MultipleExports` |
| `ignoreOwner` | `true` | Ignore your own email and name |
| `useCalendarInvitees` | `true` | Count invitee emails as participants |
| `minimumSpeakerSegments` | `1` | Minimum number of identified segments before a speaker counts |
| `ai.enabled` / `ai.model` / `ai.autoAssign` / `ai.includeSummary` | off / `claude-opus-5-5` / off / off | Optional AI tier. See [CLASSIFICATION.md](CLASSIFICATION.md). |

## Logging

`minimumLevel` (Trace…Critical), `logDirectory` (default `./data/logs`), and `retentionDays` (default 14). Logs are JSON lines, with Granola and Anthropic keys and bearer tokens redacted.

## Projects, clients and rules

Manage these in the UI, or import and export them as JSON (Projects → Import / Export configuration). The format is shown in [`config/project-mappings.example.json`](../config/project-mappings.example.json). Import matches projects by ID, then by slug, and clients by name. It creates or updates entries and never deletes anything.

## Host settings (`appsettings.json` / environment variables)

| Key | Default | Meaning |
|---|---|---|
| `Urls` | `http://127.0.0.1:5080` | Listening address (loopback only by default) |
| `App:BaseDirectory` | content root (repo root in Development) | Base for relative paths |
| `App:DataDirectory` | `data` | Database and default log folder |
| `ConnectionStrings:Default` | `Data Source=<data>/granola-pi.db` | Database connection |
| `Worker:Enabled` | `true` | Host the scheduled worker in this process |
| `Security:UseWindowsCredentialManager` | `true` | Store keys in Windows Credential Manager |
| `Security:ApiToken` | *(none)* | When set, every `/api` call except `/api/health` must send `Authorization: Bearer <token>` |
| `Security:AllowedOrigins` | the Vite dev origins | CORS origins |
| `GRANOLA_API_KEY`, `ANTHROPIC_API_KEY` | — | Secrets supplied by the environment (server deployments) |

Environment variables use `__` as the separator, for example `Security__ApiToken`.
