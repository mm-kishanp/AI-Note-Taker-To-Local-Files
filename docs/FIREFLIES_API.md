# Fireflies.ai integration

The app can import calls recorded by [Fireflies.ai](https://docs.fireflies.ai/getting-started/quickstart) alongside Granola meetings. Fireflies is optional: configure a key and every call the key can see is synchronized. Its AI notes go to the notes file and its transcript to the transcript file. Both are classified and exported exactly like Granola meetings.

## Setup

1. In Fireflies, open **app.fireflies.ai → Integrations → Fireflies API** and copy the API key.
2. In the app, open **Settings → Connections → Fireflies connection**, paste the key, then click **Save & verify**. The key is validated with Fireflies and stored in Windows Credential Manager under `GranolaProjectIntelligence:FirefliesApiKey`.
   - On servers, set the `FIREFLIES_API_KEY` environment variable instead.
3. Run **Sync now**. The first run imports all calls from **Initial import from** onwards, or all history when that setting is empty.

Granola and Fireflies can be used together or separately. When both are configured, one failing does not stop the other: the run finishes as *Partially succeeded* and names the failing source.

## API usage (read-only)

The client uses only the official GraphQL endpoint `POST https://api.fireflies.ai/graphql` with `Authorization: Bearer <key>`. It sends three queries:

| Query | Purpose |
|---|---|
| `user { name email }` | Connection test |
| `transcripts(fromDate, limit, skip) { id title date }` | Lists calls, 50 per page (`limit` maximum) |
| `transcript(id)` | Fetches one call: title, date, duration, `transcript_url`, `organizer_email`, `participants`, `meeting_attendees`, `user`, `sentences`, and `summary` (`overview`, `notes`, `shorthand_bullet`, `action_items`, `outline`, `keywords`, `topics_discussed`, `meeting_type`) |

The app never sends mutations.

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
