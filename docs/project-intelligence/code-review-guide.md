# Code Review Guide

Last reviewed: 2026-10-28

- Evidence scope: `.github/` (only `.github/workflows/ci.yml`), repository root (no `CODEOWNERS`, PR template or CONTRIBUTING), `CLAUDE.md`/`AGENTS.md` (agent workflow), Git history (3 commits by one author, all on `master`). Hosting settings (branch protection, required reviewers) are not visible.
- Confidence: Medium

## How reviews work here

| Item | Current practice | Evidence |
| --- | --- | --- |
| Pull request template | None | Verified absent |
| Code owners / required reviewers | None found | Verified absent |
| Required checks before merge | CI jobs `backend` and `frontend` run on pull requests; whether they are required is Unknown (Q-001). Pushes to `master` run no CI (RF-002) | `ci.yml` |
| Branch protection and approvals | Unknown — not visible in the repository | Q-001 |
| Branch and commit conventions | Conventional-commit style seen (`feat: …`) in 2 of 3 commits; commits made directly on `master` | `git log` |
| Review tooling | `dorny/test-reporter` publishes .NET test results; agent reviewers (`.claude/agents/pida-reviewer.md`) and `/project-review` skill available | `ci.yml`, `.claude/` |

## Review checklist

- **Correctness**: acceptance criteria met; edge cases and error paths handled. For sync changes: one meeting's failure must not stop the run; auth/permission failures must abort it; a failed retrieval must never look like empty content; checkpoints only advance after a complete listing.
- **Data safety (project-specific, blocking)**: the app must never delete or silently overwrite a user's file. Any new write path goes through `AtomicFile` and `SafePath`; any overwrite needs an explicit user action (`Force`) or the configured conflict policy.
- **Architecture and layering**: logic in Application services, controllers stay thin, external access through ports. [coding-standards/architecture-and-layering.md](coding-standards/architecture-and-layering.md)
- **Async**: `CancellationToken` accepted and passed; no `.Result`/`.Wait()`; long work goes through `SyncCoordinator`. [coding-standards/async-await.md](coding-standards/async-await.md)
- **Structure and duplication**: reuse `ExportLayout`, `PathTemplates`, `SafePath`, the throttles; no generated files committed (check `frontend/src` for `.js`). [coding-standards/code-structure-and-duplication.md](coding-standards/code-structure-and-duplication.md)
- **Comments and docs**: `<summary>` on new public types explaining guarantees; update `docs/*.md` and `docs/openapi.json` when behaviour or contract changes. [coding-standards/commenting-and-documentation.md](coding-standards/commenting-and-documentation.md)
- **Constants**: user-tunable values go in settings records with validation; fixed limits get named constants. [coding-standards/constants-and-magic-values.md](coding-standards/constants-and-magic-values.md)
- **Errors and security**: throw the right domain exception; validate input; IDs validated before URLs; paths root-guarded; no raw HTML rendering in the UI; the CSRF header rule still covers new state-changing routes; secrets only via `ISecretProvider`/`ICredentialStore`, never returned unmasked. [coding-standards/error-handling-and-security.md](coding-standards/error-handling-and-security.md)
- **Logging**: message templates; no meeting content, emails or keys. [coding-standards/logging.md](coding-standards/logging.md)
- **Naming and hygiene**: `.editorconfig` conventions; `dotnet format` clean; no stray artefacts. [coding-standards/naming-and-code-hygiene.md](coding-standards/naming-and-code-hygiene.md)
- **Schema changes**: new EF migration included; enum stored as string; backwards-compatible settings (new properties need defaults); remember migrations run automatically at start-up.
- **Tests**: new behaviour covered for normal, failure and boundary cases (use `TestHost` fakes for sync paths); no test weakened.
- **Clients**: not applicable (single-user app; see [client-matrix.md](client-matrix.md)).
- **Docs**: feature pages, sub-project pages, API catalog and data model updated.

## What blocks a merge

| Severity | Meaning | Blocks merge? |
| --- | --- | --- |
| Critical | Security breach, data loss (deleting/overwriting user notes), or outage likely | Yes |
| High | Wrong main-flow behaviour, broken compatibility, significant security weakness | Yes |
| Medium | Edge-case bug, missing important test, reliability or performance risk | Team decision |
| Low | Maintainability, minor convention, documentation | No; fix or log |

## Recurring review themes

From this audit (no PR history exists yet): generated or stray files committed (RF-001, RF-011, RF-013); source-specific naming in shared code (TD-003); missing tests for new opt-in paths (AI, migration, worker); configuration triggers that do not match the repository (RF-002).
