# FEAT-004 — Rule-based project classification

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: FR-006, FR-007; [docs/CLASSIFICATION.md](../../CLASSIFICATION.md); entities `ClassificationRule`, `Project`, `Client`, `MeetingProjectAssignment`

## User outcome

Each meeting is routed to the project it belongs to using deterministic, explainable rules; uncertain meetings go to review instead of the wrong folder.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Define rules (FEAT-010), tune tier order/disabled tiers/multi-project mode (FEAT-016) | Middleware | — |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | Every processed meeting | `ClassificationService.ClassifyAsync` → `ClassificationEngine.Classify` | `MeetingProcessor.cs:112-129` |
| Route | `POST /api/meetings/{id}/reclassify`, `POST /api/projects/{id}/apply-mapping`, `test-mapping` | `MeetingService`, `ProjectService` | controllers |

## Behavior

- **Main flow:** Given projects with enabled rules (`Email`, `EmailDomain`, `ParticipantName`, `SpeakerName`, `GranolaFolder`, `TitleKeyword`, `Keyword`) and client rules, when a meeting is classified, then evidence is collected per tier in the configured order (default: participant email → participant identity → speaker identity → client → metadata → keyword → AI); the first tier with evidence decides; ambiguous candidates are narrowed by weaker tiers; if still ambiguous, `MultiProjectMode` decides (SingleProject → review, Shared, MultipleExports → primary + copies) (`ClassificationEngine.cs:7-16, 84-…`).
- **Manual override:** a manual decision always wins; disagreeing automatic results are stored as conflicts; a manual project that no longer exists falls back to automatic and flags review (`ClassificationEngine.cs:31-82`).
- **Facts used:** attendees, invitees (if `UseCalendarInvitees`), identified speakers with ≥ `MinimumSpeakerSegments`, folders, title/summary keywords; the owner is ignored by default (`IgnoreOwner`). Anonymous speaker labels are never identities.
- **Error cases:** an exception during classification sets `NeedsReview` and `ErrorCategory.Classification` without stopping export (`MeetingProcessor.cs:119-127`).
- **Explanation:** the full result (tier, evidence, candidates, conflicts) is stored in `Meeting.ClassificationDetailJson`.

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| `Projects`, `Clients`, `ClassificationRules` | R | loaded once per run (`LoadContextAsync`) |
| `Meetings` (classification fields), `MeetingProjectAssignments` | W | `ClassificationService.Apply` |

## Integrations and side effects

Optional AI tier (FEAT-005). Classification result drives export targets (FEAT-006).

## Configuration and feature flags

`classification.tierOrder`, `disabledTiers`, `multiProjectMode` (SingleProject), `ignoreOwner` (true), `useCalendarInvitees` (true), `minimumSpeakerSegments` (1) (`AppSettings.cs:55-74`).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `ClassificationEngineTests` (22 methods, 6 data rows) | Unit | Tier precedence, narrowing, multi-project modes, manual override, anonymous speakers |
| `Manual_override_survives_source_updates_and_rule_changes`, `Unassigned_meeting_is_reassigned_after_saving_a_mapping` | Integration | End-to-end routing |

## Gaps and follow-up

None significant found.
