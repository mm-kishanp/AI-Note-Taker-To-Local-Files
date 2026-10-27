# Granola API integration

Verified on 2026-10-26 against the official documentation (https://docs.granola.ai/introduction and the OpenAPI document at https://docs.granola.ai/api-reference/openapi.json). Read-only calls to a real account confirmed the response shapes. Only documented, public endpoints are used.

## Authentication

- Base URL: `https://public-api.granola.ai/v1`
- Header: `Authorization: Bearer grn_…`
- Keys are created in the Granola desktop app: **Settings → Connectors → API keys**. Scopes are *Personal notes* (your notes and notes shared with you) and *Public notes* (workspace-visible notes and the Team space). Business/Enterprise admins can also create workspace keys. On Enterprise, an admin must enable API access for members.
- Errors use a JSON body: `{"code":"INVALID_API_KEY","message":"…","requestId":"…","timestamp":"…"}`.

## Endpoints used

| Method | Path | Used for |
|---|---|---|
| GET | `/v1/notes` | Discovery. Query: `created_after`, `created_before`, `updated_after` (ISO date or date-time), `folder_id`, `space_id`, `cursor`, `page_size` (1–30). Response: `{notes, hasMore, cursor}`. |
| GET | `/v1/notes/{note_id}?include=transcript` | Full note with inline transcript. Returns `413 TRANSCRIPT_TOO_LARGE` when the transcript cannot be inlined. |
| GET | `/v1/notes/{note_id}` | Note without transcript (the 413 fallback). |
| GET | `/v1/notes/{note_id}/transcript` | Paged transcript (`cursor`, `page_size` 1–100) → `{transcript, hasMore, cursor}`. |
| GET | `/v1/folders` | Lists folders (`cursor`, `page_size`). Shown in the UI to help write Granola-folder rules. |

Not used: spaces, audit events, webhooks and legal holds. Webhooks exist in Granola's API but are not relied on, so the sync works with any key. Incremental sync uses `updated_after` together with local change detection.

## Note fields consumed

`id`, `title`, `owner{name,email}`, `created_at`, `updated_at`, `deleted_at`, `web_url`, `calendar_event{event_title, invitees[{email}], organiser, scheduled_start_time, scheduled_end_time}`, `attendees[{name,email}]`, `attachments[{id,type,url,width,height}]`, `folder_membership[{id,name,parent_folder_id,space_id}]`, `summary_markdown`/`summary_text`, `private_notes_markdown`/`private_notes_text`, `transcript[]`.

Transcript segments: `text`, `start_time`, `end_time`, `speaker{source, attribution?, diarization_label?, name?}`.

- `source`: `microphone` or `speaker` (system audio). Treated as an open set.
- `attribution`: `me` (the note owner) or `them`. Omitted when unknown.
- `name`: present **only** when Granola identified the speaker. This is the only speaker field used as an identity.
- `diarization_label`: an anonymous label such as "Speaker A". It is local to one transcript and is never used for identity.

In practice only some segments carry a name. A sample live note had 15 named segments out of 127, which is why classification also uses attendee and invitee emails.

## Behaviour and limits

| Behaviour | Handling |
|---|---|
| Only notes with a generated summary **and** transcript are returned. Unprocessed notes return 404. | They appear on a later sync once Granola has finished processing them. |
| `private_notes_*` is returned only to the owner's personal key. | It is `null` otherwise, and the "Meeting Notes" section is omitted. |
| `deleted_at` is visible only to keys with access to all workspace notes. | The meeting is marked `Deleted`; its exported files are kept. |
| A 404 on a known note (deleted or permission removed) | The meeting is marked `Inaccessible`; its files are kept. |
| Attachment URLs are signed and may change. | URLs are excluded from the change-detection hash and linked as-is (they can be turned off in settings). |

## Rate limits and retries

Granola documents a burst of 25 requests per 5 seconds, sustained at 5 per second, per user or workspace. The client:

- spaces requests at least 220 ms apart, process-wide;
- on 429, honours `Retry-After` (and pauses all requests until then);
- retries 5xx, 408 and network errors with exponential backoff and full jitter, capped at 60 s, up to `MaxRequestAttempts` (default 5);
- does **not** retry 401/403 (authentication/permission) or schema errors;
- validates note IDs before building URLs, never logs the key, and puts Granola's `requestId` in error messages for support.

## Error categories

| HTTP | Category | Retry |
|---|---|---|
| 401 | Authentication | No — fix the key |
| 403 | Permission | No — check scopes |
| 404 | NotFound | — (meeting marked inaccessible) |
| 413 | handled by the paged-transcript fallback | — |
| 429 | RateLimited | Yes |
| 5xx / timeout / network | TransientApi | Yes |
| Malformed JSON | InvalidResponse | No |

## Live verification

`scripts/e2e-live.mjs` runs a read-only check against a real account through the app's API. It covers connection, full sync, incremental sync, mapping, moving files, manual override, and conflict detection.
