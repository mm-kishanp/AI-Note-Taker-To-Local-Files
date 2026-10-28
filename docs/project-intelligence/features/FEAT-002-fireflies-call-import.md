# FEAT-002 — Fireflies call import

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-004; INT-002; `Meeting.Source = Fireflies`; [docs/FIREFLIES_API.md](../../FIREFLIES_API.md)

## User outcome

Calls recorded by Fireflies.ai (AI notes and transcript) are imported, classified and exported exactly like Granola meetings, in the same run, with a `source: fireflies` marker.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Store a Fireflies key; sync | Middleware | `Middleware.cs` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `POST /api/sync/start` (Fireflies included automatically when a key exists) | `SyncEngine.RunAsync` | `SyncEngine.cs:69-115` |
| Route | `POST /api/fireflies/test-connection`, `/api/settings/credentials/fireflies` | `FirefliesController`, `SettingsController` | `SystemControllers.cs:126-180` |

## Behavior

- **Main flow:** Given a Fireflies key, when a Full or Incremental run starts, then calls are listed with `transcripts(fromDate, limit 50, skip)` (incremental: last successful start − `FirefliesLookbackDays`, default 3, because Fireflies exposes no update time), known calls are re-fetched only while their notes or transcript are still missing, each call is fetched (`transcript(id)` with sentences and summary), mapped by `FirefliesMapper.ToFetch` into the common model and processed like a Granola note (`SyncEngine.cs:313-346`, `MeetingProcessor.cs:58-60`).
- **Validation and error cases:** Fireflies GraphQL error codes are mapped to categories (`FirefliesApiClient.Categorize`); a Fireflies outage is recorded as a source error and the run is PartiallySucceeded while Granola continues, and vice versa (`SyncEngine.cs:87-111`; test `Both_sources_sync_together_and_a_fireflies_outage_does_not_block_granola`).
- **Edge cases:** anonymous speakers ("Speaker A") are never treated as people (test `…anonymous_speakers_are_not_people`); AI notes arriving later are picked up (test `Ai_notes_that_arrive_later_are_picked_up…`); duplicate IDs within a listing are de-duplicated (`SyncEngine.cs:329`).

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `Meetings` (`Source = Fireflies`, `GranolaNoteId` = Fireflies ID) | R/W | same sensitivity as FEAT-001 |
| `ApplicationSettings` `sync.checkpoint.fireflies`, `fireflies.connection` | R/W | — |

## Integrations and side effects

INT-002. Markdown front matter uses `fireflies_transcript_id` (`MarkdownRenderer.IdKey`).

## Configuration and feature flags

Key `FirefliesApiKey` (`FIREFLIES_API_KEY`); `sync.firefliesLookbackDays` (3).

## Client availability

Not applicable — single-user app.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `FirefliesSyncTests` (5) | Integration | Export, classification by attendee email, late AI notes, both sources together, masked credentials |
| `FirefliesTests` (11) | Unit | Mapper and client behaviour |

## Gaps and follow-up

- Fireflies-only users get no scheduled sync (RF-004, PIDA-004).
- Errors surface as "Granola" errors in problem details (RF-009).
- Uniqueness not source-aware (RF-006).
