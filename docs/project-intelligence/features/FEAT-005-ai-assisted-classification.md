# FEAT-005 — AI-assisted classification (opt-in)

Last reviewed: 2026-10-28

- Status: Partial
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: INT-003; NFR-005; [docs/SECURITY.md](../../SECURITY.md) "AI data sharing"

## User outcome

For meetings no rule could place, the app can ask Claude to suggest one of the user's existing projects, with the evidence it relied on. Off by default.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Enable in Settings, store an Anthropic key | `SettingsController` (`/credentials/ai`) | `SystemControllers.cs:137-159` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Internal | Classification of an unassigned, non-manual meeting | `ClassificationService.ClassifyAsync` | `ClassificationService.cs:68-82` |

## Behavior

- **Main flow:** Given AI enabled, the AI tier not disabled, provider `anthropic` and a key, when deterministic classification returns Unassigned and there is no manual decision, then title, participants (names/emails), identified speakers, folder names, optionally the summary (truncated to 4000 chars) and the active project list are sent; the model must answer JSON matching a schema whose `project_id` enum is the known IDs or null; the suggestion re-enters the engine as AI-tier evidence (`AnthropicAiClassifier.cs:36-63`, `ClassificationService.cs:86-95`).
- **Validation and error cases:** unknown project IDs, invalid JSON, or refusal → no suggestion; any exception → warning log, deterministic result kept.
- **Edge cases:** meeting content is wrapped in tags and declared untrusted in the system prompt (prompt-injection mitigation).

## Data touched

Reads `Meetings`, `Projects`; writes classification fields via FEAT-004. Sends personal data (names, emails) to Anthropic when enabled.

## Integrations and side effects

INT-003 (Anthropic SDK 13.3.0). No explicit timeout set.

## Configuration and feature flags

`classification.ai.enabled` (false), `.provider` (`anthropic`), `.model` (`claude-opus-5-5`), `.includeSummary` (false); key `AiApiKey` / `ANTHROPIC_API_KEY`.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| none | — | No tests reference `AnthropicAiClassifier` (Verified by grep) |

## Gaps and follow-up

- Add unit tests for `Parse` and the schema, and a service test with a fake `IAiClassifier` (RF-017, PIDA-006).
- Add a timeout around the call (async-await page).
