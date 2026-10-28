# Technical Debt

Last reviewed: 2026-10-28

- Evidence scope: all source areas (backend read; frontend and Node CLI via sub-audits); TODO/FIXME count = 0 across `src`, `tests`, `frontend/src`, `tools/node-cli`; file sizes; manifests. Git hotspots could not be measured: the history has 3 commits (2026-10-27/28) by one author.

## Summary

The code is young (first commit 2026-10-27) and clean: no TODOs, current runtimes (.NET 10, EF Core 10, React 18, Vite 5) and consistent patterns. Debt is concentrated in **test coverage of secondary paths**, **repository hygiene/tooling** (committed build output, no frontend lint, CI trigger) and **naming left over from the Granola-only origin** now that Fireflies is a second source.

## Hotspots

| Area / path | Why it is a hotspot | Related debt | Evidence |
| --- | --- | --- | --- |
| `frontend/src/` | Committed compiler output doubles every file; two largest UI components 582–646 lines | TD-004, RF-001 | sub-audit |
| `src/GranolaPI.Application/Synchronization/` + `src/GranolaPI.Infrastructure/{Granola,Fireflies}/` | Core flow; most recent feature work (Fireflies) touched it; duplicated client retry logic | TD-003, TD-004 | `git log --stat` |
| `src/GranolaPI.Application/Classification/ClassificationEngine.cs` | Largest C# file (379 lines), central business rules | — | line counts |

## Debt register

| ID | Area | Debt | Type | Evidence | Impact | Effort | Priority | Status | Task |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TD-001 | Tests | No automated tests for: scheduled worker, AI classifier, export root migration, API-token/remote auth path, runtime logging settings | Test | `TestHost.cs:200` disables the worker; grep finds no tests for these | Regressions in opt-in and server-mode paths go unnoticed (RF-003 and RF-004 are examples) | M | P1 | Open | PIDA-005, PIDA-006, PIDA-007, PIDA-008 |
| TD-002 | `tools/node-cli` | Legacy exporter duplicates the Granola client, matching and Markdown rendering; docs broken; kept "for reference" | Code | `README.md:43`, sub-audit | Confusion and drift; plain-text key storage path remains | S | P3 | Open | PIDA-016 |
| TD-003 | Domain / Application | Source-specific names for multi-source concepts (`GranolaNoteId`, `GranolaApiException` base for Fireflies, `GranolaNoteFetch`, `GranolaConnectionState`); unique index not source-aware | Design | `Entities.cs:64`, `FirefliesModels.cs:116`, `AppDbContext.cs:77` | Misleading error messages, risky lookups, harder third-source addition | L (needs migration) | P3 | Open | PIDA-011 |
| TD-004 | Code | Duplicated retry/backoff loops in two API clients; large UI components; service locator in `MeetingsController`; AI key handling outside `CredentialService`; inline timeouts/user-agent; correlation ID not in logs | Code | coding-standards pages | Maintenance cost | M | P3 | Open | — (pick up opportunistically) |
| TD-005 | Tooling | No frontend lint; .NET analyzers not enforced (`TreatWarningsAsErrors=false`, most `.editorconfig` rules at suggestion); no dependency scanning; no coverage threshold | Infrastructure | `Directory.Build.props`, `.editorconfig`, `.github/` | Defects and vulnerable packages slip through | M | P2 | Open | PIDA-009, PIDA-010 |
| TD-006 | Delivery | No versioning, tags, changelog or published artefacts; migrations auto-apply without backup | Infrastructure | `git tag` empty, `Program.cs:51` | Hard to identify builds or roll back safely | S–M | P3 | Open | PIDA-018 |
| TD-007 | Repository hygiene | Committed tsc output, stray `run-test.log`, misnamed node-cli example | Code | RF-001, RF-011, RF-013 | Wrong builds (RF-001), confusion | S | P0 (RF-001) / P3 | Open | PIDA-001, PIDA-013 |

- **Design**: wrong boundaries, layering violations, tight coupling.
- **Code**: duplication, very large classes or functions, dead code, magic values.
- **Test**: missing tests on critical paths, flaky or slow suites.
- **Dependency**: outdated, unsupported, or vulnerable packages and runtimes (versions from manifests).
- **Infrastructure**: manual deployment steps, no rollback, unpinned images.
- **Documentation**: missing or wrong docs that slow people down.
- **Security**: weaknesses that are not yet exploitable findings (those go in `security-review.md`).

Dependency note (Verified from manifests, no vulnerability data): `xunit` 2.9.3 is the v2 line (v3 exists) and `Microsoft.NET.Test.Sdk` 17.14.1 — both supported; `dorny/test-reporter@v1` is pinned to a major tag. No end-of-life runtime found.

## Paid down

| ID | Date | How | Evidence |
| --- | --- | --- | --- |
| — | — | Nothing paid down yet (first audit) | — |
