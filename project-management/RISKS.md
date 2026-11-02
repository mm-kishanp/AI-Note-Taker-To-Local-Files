# Risks and Issues

Last reviewed: 2026-10-28 (seeded by the project audit)

| ID | Risk/issue | Likelihood | Impact | Evidence | Mitigation | Owner/status |
| --- | --- | --- | --- | --- | --- | --- |
| R-001 | Stale frontend code ships or is tested: committed tsc output shadows `.tsx` sources in Vite/Vitest/Docker | High | Med | RF-001; `frontend/tsconfig.json`; Vite resolution order | PIDA-001 | Open |
| R-002 | Defects reach `master` unchecked because CI only triggers on `main` | High | Med | RF-002; `ci.yml:3-6` | PIDA-002; confirm branch protection (Q-001) | Open |
| R-003 | Server-mode exposure: token path untested, host filtering off in compose, token holder has file-system reach | Low | High | SEC-001, SEC-002, SEC-005 | PIDA-005, PIDA-017; TLS proxy | Open |
| R-004 | Confidential meeting content on disk (SQLite + Markdown) unencrypted; relies on OS permissions; kept indefinitely | Med | High | `data-model.md`; `docs/SECURITY.md` | User guidance exists; decide retention (Q-010) | Open |
| R-005 | Granola public API or Fireflies GraphQL changes break sync | Med | Med | INT-001/002; fakes in tests, live E2E manual only | Error categorisation and retries; run `scripts/e2e-live.mjs` periodically; keep `docs/GRANOLA_API.md`/`FIREFLIES_API.md` current | Open |
| R-006 | Opt-in AI sends names, emails and optionally summaries to Anthropic | Low | Med | FEAT-005; `docs/SECURITY.md` | Off by default; documented; PIDA-006 tests the output guard | Accepted (by design) |
| R-007 | Single maintainer and very short history (3 commits in 2 days) — knowledge concentration | Med | Med | `git log` | This knowledge base; tests | Open |
| R-008 | Upgrades apply migrations automatically with no backup or version — difficult rollback | Low | Med | `Program.cs:51`; no tags | PIDA-018 | Open |
| R-009 | Manual meeting actions racing a running sync could leave a meeting's export state inconsistent until the next run | Low | Low | RF-019 (Inferred) | Q-011 | Open |
| R-010 | WhisperAI's live `utterances`/`sentences` shape or time unit differs from the documented example, giving wrong timestamps or empty transcripts | Low | Med | Q-012; untyped items in `whisperai-openapi.json` | Fallback to `text` keeps the transcript; verify on first real sync; adjust `WhisperAiUtterance` | Open |

Include security, tenant isolation, data migration, API compatibility, reliability, performance, operational, and delivery risks where applicable. Tenant isolation: not applicable (single-user).
