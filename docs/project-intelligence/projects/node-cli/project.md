# Node CLI (legacy)

Last reviewed: 2026-10-28

- Type: CLI
- Location: `tools/node-cli/`
- Status: Deprecated ("kept for reference", `README.md:43`)
- Confidence: High (from a read-only sub-audit)
- Part of: [project.md](../../project.md)

## What it is

The project's first implementation: a zero-dependency Node.js script that exports Granola notes and transcripts to Markdown and files them by project. The .NET app replaced it; nothing in the app calls it.

## Responsibilities

Config loading (`tools/node-cli/src/config.js`), Granola client (`tools/node-cli/src/api.js`), scoring-based project matching (`tools/node-cli/src/match.js`), Markdown rendering (`tools/node-cli/src/markdown.js`), sync and state (`tools/node-cli/src/sync.js`).

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| FEAT-022 Legacy Node CLI exporter | owns | [FEAT-022](../../features/FEAT-022-legacy-node-cli.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | `granola-sync.js` `main()` (`bin: granola-sync`) | `package.json` |
| Runtime / framework | Node.js ≥18, no dependencies, no lockfile | `package.json` |
| Build | none | — |
| Run locally | `node granola-sync.js --config config.json [--dry-run]` (Documented) | `README.md` of the tool |
| Tests | `npm test` → `tools/node-cli/test/smoke.test.js` (offline) | file |
| Deployed as | not deployed | — |

## Dependencies

| Depends on | Kind | Why | Evidence |
| --- | --- | --- | --- |
| Granola public API | external | INT-001 | `tools/node-cli/src/api.js` |

## Interfaces

- **Exposes:** CLI flags `--config`, `--since`, `--note`, `--force`, `--dry-run`, `--discover [n]`, `--help`.
- **Consumes:** INT-001.

## Data

`config.json` (may contain a plain-text key), `.granola-sync-state.json`, `output/` — all git-ignored.

## Configuration

`GRANOLA_API_KEY` or `apiKey`; `output.*`, `matching.sources`, `sync.*`, `projects[]`.

## Ownership and conventions

Separate style from the .NET app; no shared code.

## Risks and gaps

- Duplicated logic (TD-002); misnamed example config (RF-011); no request timeout; decide its future (Q-003, PIDA-016).
