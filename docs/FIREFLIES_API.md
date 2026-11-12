# Fireflies.ai integration

The app can import calls recorded by [Fireflies.ai](https://docs.fireflies.ai/getting-started/quickstart) alongside Granola meetings. Fireflies is optional: configure a key and every call the key can see is synchronized. Its AI notes go to the notes file and its transcript to the transcript file. Both are classified and exported exactly like Granola meetings.

## Setup

1. In Fireflies, open **app.fireflies.ai → Integrations → Fireflies API** and copy the API key.
2. In the app, open **Settings → Connections → Fireflies connection**, paste the key, then click **Save & verify**. The key is validated with Fireflies and stored in Windows Credential Manager under `Chroniq:FirefliesApiKey`.
   - On servers, set the `FIREFLIES_API_KEY` environment variable instead.
3. Run **Sync now**. The first run imports all calls from **Initial import from** onwards, or all history when that setting is empty.

Granola and Fireflies can be used together or separately. When both are configured, one failing does not stop the other: the run finishes as *Partially succeeded* and names the failing source.

## API usage (read-only)

The client uses only the official GraphQL endpoint `POST https://api.fireflies.ai/graphql` with `Authorization: Bearer <key>`. It sends these queries:

| Query | Purpose |
|---|---|
| `user { name email }` | Connection test |
| `transcripts(fromDate, limit, skip) { id title date }` | Lists calls, 50 per page (`limit` maximum) |
| `transcript(id)` | Fetches one call: title, date, duration, `transcript_url`, `organizer_email`, `participants`, `meeting_attendees`, `user`, `sentences`, and `summary` (`overview`, `notes`, `shorthand_bullet`, `action_items`, `outline`, `keywords`, `topics_discussed`, `meeting_type`) |
| `transcript(id) { audio_url }` / `{ video_url }` | A fresh download link for the recording. Only sent when audio or video download is turned on (see below) |

The app never sends mutations.

## Recordings (audio and video)

Turn on **Settings → Export → Download call audio / video (Fireflies)** and each Fireflies call's recording is downloaded during sync, next to the call's other files:

```text
ChroniqKnowledge/Projects/MotorDealerApp/Recordings/2026-10-26-Client-Requirement-Discussion-audio.mp3
ChroniqKnowledge/Projects/MotorDealerApp/Recordings/2026-10-26-Client-Requirement-Discussion-video.mp4
```

Audio and video are separate switches and are off by default. Unassigned and Shared calls go to `Unassigned/Recordings` and `Shared/Recordings`. A call shared by several projects keeps its recording once, in the primary project's folder. The folders and an optional separate recordings root are set under Export settings.

What Fireflies requires:

- **A Pro plan or higher** for both `audio_url` and `video_url`. On other plans the recording is marked *Not available* and is not requested again.
- **Video** also needs "Record meeting video" turned on in Fireflies.
- Links **expire after 24 hours**, so the app asks for a fresh link right before each download. The link is never saved to disk or the database, and never logged.
- Each link request costs one API request and is paced like the others (about 1.1 s apart).

How downloads behave:

- The link points at a file host, not the Fireflies API, so the API key is never sent with the download.
- The file is streamed to a temporary file and renamed only when complete, so an interrupted download never leaves a partial recording. A download is abandoned if no data arrives for 60 seconds.
- Only known media extensions (`.mp3`, `.m4a`, `.mp4`, `.webm`, and similar) are used for the file name. Anything else falls back to `.mp3` or `.mp4`.
- A recording problem never fails the call: its notes and transcript are exported regardless. Failures are shown on the meeting's **Recordings** tab and retried on later syncs, up to 5 attempts.
- A call that Fireflies has not finished processing (younger than 2 days, no link yet) is simply asked again on the next sync. After that, "no link" is final.
- Files you move or delete are not downloaded again by a normal sync. To recreate missing files, use **Re-export → recreate missing files** (reconcile) on the meeting, or a Reconcile sync.
- Reassigning a call to another project moves its recording with it, unless "Move unmodified files when a meeting is reassigned" is off.

**Calls imported before you turned this on:** a normal incremental sync only looks back a few days (*Fireflies look-back*). Run a **Full** sync, or a Reconcile, to download recordings for older calls.

Recordings are large. A one-hour video is often several hundred MB, so check free space on the recordings drive first.

### Rate limits and errors

- Fireflies allows 30 (Free) or 60 (Pro and above) requests per minute. Free and Pro plans also have a daily cap.
- The client spaces requests about 1.1 s apart.
- After a `429`, it waits for `Retry-After` (or `extensions.metadata.retryAfter`) before trying again.
- Errors are mapped from the GraphQL `code` as follows:

| Codes | Treated as | Effect |
|---|---|---|
| `auth_failed`, invalid key | Authentication | Fatal: the run stops |
| `forbidden`, `paid_required`, `not_in_team` and similar | Permission | Fatal: the run stops |
| `object_not_found` | Not found | The call is marked inaccessible; its files are kept |
| `too_many_requests`, `request_timeout`, `5xx` | Transient | Retried with backoff |

## How Fireflies data is mapped

| Fireflies | Stored as |
|---|---|
| `id` | Source ID (`fireflies_transcript_id` in front matter) |
| `date` (epoch ms), `duration` (minutes) | Meeting start / end |
| `user`, else `organizer_email` | Owner |
| `meeting_attendees`, `participants` (comma-separated entries are split) | Attendees and participant emails, used by Email / EmailDomain / ParticipantName rules |
| `sentences[].speaker_name` | Speaker names, used by SpeakerName rules. Labels like "Speaker 1" are kept as anonymous labels and never treated as people. |
| `summary.*` | The **AI Notes** section of the notes file (Overview, Notes, Action Items, Outline, Topics Discussed, Keywords, Meeting Type) |
| `transcript_url` | "Open in Fireflies" link |

## Incremental synchronization

Fireflies has no "updated since" filter, and it adds transcripts and AI notes some time after a call ends. Incremental runs therefore work like this:

- They list calls from **Fireflies look-back (days)** before the last successful run. The setting is under Settings → Synchronization; the default is 3 days.
- A call that is already imported is fetched again only while its transcript or AI notes are still missing. Complete calls cost nothing beyond the list request.
- A call whose AI notes arrive later is rewritten in place. If you edited the file, the usual conflict rules apply.

Fireflies has its own checkpoint (`sync.checkpoint.fireflies`), separate from Granola's.

## Markdown differences

Fireflies files look the same as Granola files except for:

- front matter `source: fireflies` with `fireflies_transcript_id` (instead of `granola_note_id`);
- a `## AI Notes` section (instead of `## Summary`);
- no "Meeting Notes" section, because Fireflies has no owner-written notes.
