# WhisperAI integration

The app can import transcripts from the [WhisperAI API](https://whisperai.com/whisper-api/docs) alongside Granola meetings and Fireflies calls. WhisperAI is optional: configure a key and every completed transcript on the account is synchronized. WhisperAI is a transcription service. It returns text only: there is no title, owner, attendee list or AI summary, and speakers are anonymous ("Speaker A"). Each transcript is classified and exported like any other meeting, but with that data missing, classification relies mostly on keyword rules and AI classification.

## Setup

1. In the [WhisperAI developer portal](https://whisperai.com/developer), create an API key (it starts with `wai_`).
2. In the app, open **Settings → Connections → WhisperAI connection**, paste the key, then click **Save & verify**. The key is validated with WhisperAI and stored in Windows Credential Manager under `GranolaProjectIntelligence:WhisperAiApiKey`.
   - On servers, set the `WHISPERAI_API_KEY` environment variable instead.
3. Run **Sync now**. The first run imports all transcripts from **Initial import from** onwards, or all history when that setting is empty.

Granola, Fireflies and WhisperAI can be used in any combination. When several are configured, one failing does not stop the others: the run finishes as *Partially succeeded* and names the failing source.

## API usage (read-only)

The client uses only the official REST API at `https://api.whisperai.com/v1`, verified against its [OpenAPI spec](https://whisperai.com/whisperai-openapi.json). The key is sent as the raw header `Authorization: wai_…` (no `Bearer` scheme). It sends three requests:

| Request | Purpose |
|---|---|
| `GET /transcript?limit=1` | Connection test |
| `GET /transcript?limit=100&before_id=…` | Lists transcripts, newest first; `before_id` pages to older ones while `page_details.next_url` is set |
| `GET /transcript/{id}` | Fetches one transcript: `status`, `text`, `utterances` (speaker turns), `audio_url`, `audio_duration`, `language_code` |
| `GET /transcript/{id}/sentences` | Only when a completed transcript has no `utterances` (it was created without `speaker_labels`) |

The app never creates, uploads, translates or deletes anything.

### Rate limits and errors

- WhisperAI documents no request rate limit. The client uses the default request spacing, and after a `429` it waits for `Retry-After` before trying again.
- Errors are mapped from the HTTP status as follows:

| Status | Treated as | Effect |
|---|---|---|
| `401` | Authentication | Fatal: the run stops |
| `402` (monthly usage quota exceeded) | Permission | Fatal: the run stops |
| `404` | Not found | The transcript is marked inaccessible; its files are kept |
| `409`, `429`, `5xx` | Transient | Retried with backoff |

## How WhisperAI data is mapped

| WhisperAI | Stored as |
|---|---|
| `id` | Source ID (`whisperai_transcript_id` in front matter) |
| `created` (list endpoint only) | Meeting date. `GET /transcript/{id}` does not return it, so the value from the listing (or from an earlier sync) is used. |
| `audio_url` file name | Title (e.g. `weekly-sync.mp3` → "weekly-sync"). Files uploaded to WhisperAI are titled "WhisperAI transcript". The URL itself is **not** stored as a link, because pre-signed URLs grant access to the recording. |
| `utterances[]` (`speaker`, `text`, `start`/`end` in ms) | Transcript segments; else `/sentences`; else the plain `text` |
| `speaker` ("A", "B"…) | Anonymous labels ("Speaker A"), never treated as people |

## Incremental synchronization

- Only `completed` transcripts are imported. Queued or processing ones are picked up by a later run, and `error` ones have no content.
- WhisperAI has no "updated since" filter, so incremental runs list transcripts created up to **one day** before the last successful run. This catches jobs that were still processing.
- A completed transcript does not change, so one that is already imported is not fetched again (unless its export is pending).

WhisperAI has its own checkpoint (`sync.checkpoint.whisperai`).

## Markdown differences

- front matter `source: whisperai` with `whisperai_transcript_id`;
- the notes file states that WhisperAI provides a transcript only; there is no summary or owner-notes section;
- no "Open in" link.

## Unverified details

The OpenAPI schema leaves `utterances` and `sentences` items untyped. The field names (`speaker`, `text`, `start`, `end`) and the millisecond unit come from the example in the API docs. Confirm them against a real transcript on first use (see `open-questions.md`).
