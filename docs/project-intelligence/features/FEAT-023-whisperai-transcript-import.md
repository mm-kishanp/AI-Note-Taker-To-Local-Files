# FEAT-023 — WhisperAI transcript import

Last reviewed: 2026-11-02

- Status: Implemented
- Confidence: High (code and tests); Medium for the live API's utterance shape (see Gaps)
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: INT-006; `Meeting.Source = WhisperAi`; [docs/WHISPERAI_API.md](../../WHISPERAI_API.md); PIDA-019

## User outcome

Completed transcripts from the WhisperAI API are imported, classified and exported like Granola meetings and Fireflies calls, in the same run, with a `source: whisperai` marker.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Store a WhisperAI key; sync | Middleware | `Middleware.cs` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `POST /api/sync/start` (WhisperAI included automatically when a key exists) | `SyncEngine.RunAsync` | `SyncEngine.cs` (`useWhisperAi`, `DiscoverWhisperAiAsync`) |
| Route | `POST /api/whisperai/test-connection`, `/api/settings/credentials/whisperai` | `WhisperAiController`, `SettingsController` | `SystemControllers.cs` |

## Behavior

- **Main flow:** Given a WhisperAI key, when a Full or Incremental run starts, then transcripts are listed newest first (`GET /transcript`, 100 per page, `before_id` cursor) back to the start date (incremental: last successful start − 1 day). Only `completed` ones are queued, each with its listing `created` time. Each one is fetched (`GET /transcript/{id}`, plus `/sentences` when it has no utterances), mapped by `WhisperAiMapper.ToFetch`, then processed like a Granola note (`MeetingProcessor` WhisperAi branch).
- **Validation and error cases:** HTTP statuses are mapped by `WhisperAiApiClient.Categorize` (402 quota → Permission, 409 → Transient). With several sources, a WhisperAI outage becomes a source error and the run ends PartiallySucceeded (test `All_three_sources_sync_together_and_a_whisperai_outage_does_not_block_the_others`). With WhisperAI alone, a rejected key fails the run.
- **Edge cases:** speaker labels stay anonymous (test `Anonymous_speakers_are_not_people…`); a job that completes later is picked up by the next incremental run; completed transcripts are not refetched; the `audio_url` is never written to disk or DB as a link (test asserts `sig=private` absent); unsafe IDs are rejected before any request.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `Meetings` (`Source = WhisperAi`, `GranolaNoteId` = WhisperAI ID) | R/W | `Source` is a string column: no migration needed |
| `ApplicationSettings` `sync.checkpoint.whisperai`, `whisperai.connection` | R/W | — |

## Integrations and side effects

INT-006. Markdown front matter uses `whisperai_transcript_id` (`MarkdownRenderer.IdKey`).

## Configuration and feature flags

Key `WhisperAiApiKey` (`WHISPERAI_API_KEY`, `Secrets:WhisperAiApiKey`). Look-back is a constant (`SyncEngine.WhisperAiLookback`, 1 day), not a setting.

## Client availability

Not applicable — single-user app.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `WhisperAiSyncTests` (5) | Integration | Paged export, skipping unfinished jobs, no refetch, three sources together, failing key, masked credentials |
| `WhisperAiTests` (19) | Unit | Mapper, client (auth header, paging, sentences fallback, errors, retries, ID validation), log redaction |
| `pages.test.tsx` "configures the WhisperAI key…" | Frontend | Settings card stores the key via `/credentials/whisperai` |

## Gaps and follow-up

- The `utterances`/`sentences` item shape and millisecond unit are taken from the docs example; the schema leaves them untyped (Q-012).
- WhisperAI-only users get no scheduled sync, the same as Fireflies-only (RF-004, PIDA-004).
- Transcripts have no title or attendees, so attendee-, email- and speaker-based rules never match them.
