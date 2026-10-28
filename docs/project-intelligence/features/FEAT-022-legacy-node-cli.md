# FEAT-022 — Legacy Node CLI exporter

Last reviewed: 2026-10-28

- Status: Deprecated
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: [projects/node-cli/project.md](../projects/node-cli/project.md); TD-002; Q-003

## User outcome

The original zero-dependency script exported Granola notes and transcripts to Markdown and filed them by project using speaker/attendee/folder scoring. The root README now describes it as "the earlier … Node script, kept for reference" (`README.md:43`).

## Users and permissions

Whoever runs it locally; it reads `GRANOLA_API_KEY` or `apiKey` from a plain-text `config.json`.

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| CLI | `node tools/node-cli/granola-sync.js [--config] [--since] [--note] [--force] [--dry-run] [--discover n]` | `main()` | `granola-sync.js:22-39` (sub-audit) |

## Behavior

Config → Granola REST (same endpoints as INT-001, ≥250 ms spacing, 5 retries, no timeout) → score-based project match → write Markdown → state file `.granola-sync-state.json`; `lastSyncAt` advances only on a run with zero failures (sub-audit).

## Data touched

Local `config.json`, state file, `output/` folders (all git-ignored).

## Integrations and side effects

INT-001.

## Configuration and feature flags

`apiKey`, `output.*`, `matching.sources`, `sync.*`, `projects[]` (names only).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `tools/node-cli/test/smoke.test.js` | Offline smoke | Routing, sanitising, incremental skip, refiling |

## Gaps and follow-up

- Docs reference a missing `config.example.json` (RF-011); plain-text key; decide to delete or freeze (PIDA-016, Q-003).
