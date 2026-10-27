# Project classification

The engine (`Application/Classification/ClassificationEngine.cs`) is deterministic. Given the same meeting metadata, rules and settings, it always produces the same result, and it records why.

## Inputs

The engine uses only metadata Granola provides:

| Input | Source |
|---|---|
| Participants | `attendees` (name and email) plus calendar `invitees` (email). Invitees can be switched off. |
| Identified speakers | Transcript segments whose `speaker.name` is set, with segment counts. Anonymous `diarization_label`s are **never** used. |
| Granola folders | `folder_membership` names and IDs |
| Title and summary text | Used by keyword rules |
| Owner | Your own name and email are ignored by default (`IgnoreOwner`), so your presence never routes a meeting |

## Rules

Rules belong to a project, or to a client (client rules produce candidate projects).

| Kind | Matches | Notes |
|---|---|---|
| `Email` | Exact participant email | Case-insensitive |
| `EmailDomain` | Participant email domain | `acme.com` or `@acme.com` |
| `ParticipantName` | Attendee name, **and** identified speaker name | Whitespace and case are normalised |
| `SpeakerName` | Identified speaker name only | Useful for people who speak but are not on the invite |
| `GranolaFolder` | Granola folder name or ID | |
| `TitleKeyword` | Whole word or phrase in the title | Works in any script (Gujarati, Hindi, …) |
| `Keyword` | Whole word or phrase in the title or Granola summary | |

## Tiers and priority

The default order is below. You can reorder or disable tiers in Settings → Classification.

| # | Tier | Rules used | Certainty |
|---|---|---|---|
| 0 | **Manual assignment** | — | Certain (always wins) |
| 1 | Participant email | `Email` | Certain |
| 2 | Participant identity | `ParticipantName` (attendees), `EmailDomain` | High (domain: Medium) |
| 3 | Verified speaker | `SpeakerName`, `ParticipantName` (identified speakers) | High |
| 4 | Client mapping | Client rules → that client's active projects | Medium |
| 5 | Metadata | `GranolaFolder` | High |
| 6 | Keywords | `TitleKeyword`, `Keyword` | Medium |
| 7 | AI (optional) | Model suggestion constrained to existing project IDs | Low |
| 8 | Unassigned | — | — |

The algorithm:

1. Find the first tier, in order, that has any evidence.
2. If that tier points to one project, assign it.
3. If it points to several projects (for example, a client with two projects), check later tiers **restricted to those candidates**. If a later tier narrows them down to one, assign it, recording both pieces of evidence.
4. If the candidates are still ambiguous, the multi-project mode decides:
   - `SingleProject` (default): `NeedsReview`. The meeting goes to `Unassigned/` and the review queue, listing the candidates.
   - `Shared`: the meeting goes to `Shared/`.
   - `MultipleExports`: the files are exported to every candidate project. The canonical file goes to the project with the lowest priority value; the others are marked copies with provenance.
5. Evidence from other tiers that points elsewhere is recorded as **conflicts**, and shown on the meeting page.
6. Archived projects are never assigned automatically.

The stored explanation includes the status, method (Manual/Rule/AI), certainty category, tier, the rule and matched value, the candidates, the conflicts, whether review is needed, and a suggested next action. The certainty is a category tied to the kind of evidence, not a made-up percentage.

## Manual overrides

Assigning a meeting to a project, to Shared, or excluding it is stored separately from the automatic result. Later syncs re-run the rules, keep the manual decision, and record any disagreement as a conflict. Choosing **Automatic** clears the manual decision.

## Changing rules for existing meetings

Changing rules does not silently reshuffle exported meetings:

- Project page → **Test mapping**: evaluates draft rules against existing meetings without saving anything, and shows what would change. Manual assignments are flagged and are never changed.
- **Apply to selected meetings**: requires explicit confirmation, then re-classifies and re-exports only the meetings you selected.
- Unassigned page → **Save mapping**: adds a rule from a clicked participant or speaker, optionally assigns this meeting, then previews the other unassigned meetings that would now match and offers to apply after you confirm.

When a meeting moves, its unmodified generated files are **moved** to the new location. Files you have edited are left in place, and new files are written at the new location.

## AI-assisted classification (optional)

This is off by default and needs an Anthropic API key (Settings → Classification), so it is opt-in.

- It is consulted only when no deterministic tier matched and there is no manual decision.
- It receives the title, participant names and emails, identified speaker names, Granola folder names, and your project list (ID, name, client, description). It receives Granola's summary **only** if *Include summary* is on. Transcripts are never sent.
- It must answer with structured JSON naming an existing project ID or `null`. Unknown IDs are rejected. Meeting text is passed as untrusted data, and the system prompt tells the model to ignore any instructions inside it.
- By default the answer is only a **suggestion**, and the meeting goes to review. With *Auto-assign*, it is assigned with `Low` certainty and labelled `Ai`.
- A refusal, an error, or an unusable answer leaves the meeting unassigned. AI problems never block a sync.
