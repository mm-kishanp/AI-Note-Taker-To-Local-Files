# Troubleshooting

Each failure is recorded with an error category, shown on the meeting page and in Sync History. The full details are in `data/logs/chroniq-YYYYMMDD.log` (JSON lines). Search the log for the correlation ID shown in an error message.

| Symptom / category | Cause | Fix |
|---|---|---|
| **Authentication**: "Granola rejected the API key" | The key was revoked, mistyped or expired | Settings → Connections: replace the key, then test it. |
| **Authentication**: "Fireflies rejected the API key" | The Fireflies key was regenerated or mistyped | Settings → Connections → Fireflies connection: replace the key. |
| **Rate limited** (Fireflies) | Free plan: 30 requests/min and 500/day | Syncs resume automatically; lower the sync frequency or use a paid plan. See [FIREFLIES_API.md](FIREFLIES_API.md). |
| **Permission** | The key lacks a scope, or the workspace has not enabled API access | Recreate the key with the *Personal notes* (and/or *Public notes*) scope. On Enterprise, ask an admin to enable member API access. |
| Connected, but "no notes visible" | Granola returns only notes that have a generated summary **and** a transcript | Wait for Granola to finish processing, or check that the meeting was recorded. |
| **RateLimited** | Too many requests (25 per 5 s, 5/s sustained) | Retried automatically, honouring `Retry-After`. Lower `maxConcurrency` if it keeps happening. |
| **TransientApi** | Granola outage or network problem | Retried with backoff. The meeting is retried on later runs (5, 10, 20 … min). Use **Retry failed** to retry immediately. |
| **InvalidResponse** | Granola returned something unexpected | Retry later. If it persists, report it with the `requestId` from the message. |
| **TranscriptUnavailable**: notes exported, transcript missing | Transcript retrieval failed | The notes are kept and the transcript is retried. Nothing empty is written. Use **Retry from Granola** on the meeting. |
| **FilePermission** | The export folder is read-only or locked (for example, a file open in an editor that locks it, or a sync client) | Close the program or fix folder permissions, then use **Retry failed**. |
| **DiskSpace** | The drive is full | Free some space and retry. |
| **FileConflict** / status *Conflict* | You edited a file, and the source meeting has also changed | Meeting page → Exported files → Review conflict → keep, replace or save a copy. |
| Status *Missing* | You moved or deleted an exported file | Nothing is recreated unless the source changes. Run **Sync → Reconcile files** to recreate missing files. |
| Meeting stuck in **Unassigned** | No rule matched, or several projects matched | The reason is shown on the Unassigned page. Add an email, domain or speaker rule, or a keyword rule that tells the projects apart. |
| A meeting assigned to the wrong project | A rule is too broad (for example, a shared domain) | Assign it manually (manual always wins), or adjust the rules and use **Test mapping** before applying. |
| **Configuration**: "Export settings are invalid" | A template is invalid or a root is missing | Fix it under Settings → Export. Validation messages name the field. |
| "A synchronization is already running" (409) | A run is in progress, or another process holds the lease | Wait, or cancel it from the header. A stale lease from a crashed process expires after 10 minutes. |
| Run shows **Interrupted** | The app stopped mid-run | Nothing to do. Pending meetings are retried on the next run. |
| UI shows 403 "Missing X-Chroniq-Request header" | A custom script is calling the API | Send the `X-Chroniq-Request: 1` header on POST/PUT/DELETE. |
| UI shows 403 "Remote access is disabled" | The app was opened from another machine | Use `127.0.0.1`, or configure `Security:ApiToken` and HTTPS. |
| Startup error about the database | The database file is locked or corrupt | Stop other instances. Restore from backup (below). |

## Backup and recovery

**What to back up:**

1. **Export roots.** These are the Markdown knowledge base. Copy them, or better, keep them in a Git repository.
2. **`data/chroniq.db`.** This holds projects, rules, settings, manual assignments and sync history. Stop the app first, or copy it together with its `-wal`/`-shm` files.
3. **Configuration.** Use Projects → Export configuration (JSON) as a portable copy of projects, clients and rules.
4. API keys are **not** included. Keep them in your password manager.

**Restore on a new machine:**

1. Install the app and copy `data/chroniq.db` and the export roots.
2. If paths differ, update Settings → Export. Records are stored relative to the roots, so moving the roots is safe.
3. Re-enter the Granola key.
4. Run **Sync → Reconcile files**.

**If only the Markdown survived:** install fresh, import your project configuration (or recreate projects), point the export root at the existing folder, and run a full sync. Files carry `granola_note_id` and `content_hash`, so the app recognises its own unmodified files and rewrites them only if they change. Files you edited are flagged instead of overwritten. Manual assignments and sync history are not stored in the Markdown and must be redone.

**Move the export root:** use Export Browser → Migrate root. It copies everything, overwrites nothing, keeps the old folder, and updates the setting.
