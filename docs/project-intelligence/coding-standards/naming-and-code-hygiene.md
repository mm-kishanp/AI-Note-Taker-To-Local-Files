# Naming and Code Hygiene

Last reviewed: 2026-10-28

- Evidence scope: `.editorconfig`, `Directory.Build.props`, all non-migration C# file names and public type names, suppression grep (`#pragma warning disable`, `SuppressMessage`, `eslint-disable`, `@ts-ignore`), frontend hygiene via sub-audit.
- Status: Mostly consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| File-scoped namespaces | Verified: `.editorconfig` `csharp_style_namespace_declarations = file_scoped:warning` | `dotnet format` only at error severity (so not gating) |
| Private fields `_camelCase` | Verified: `.editorconfig` naming rule (severity suggestion) | Nothing |
| `var` where type is apparent; braces when multiline | Verified: `.editorconfig` (suggestion) | Nothing |
| LF line endings, UTF-8, final newline; CRLF for `.sln`/`.csproj`/`.bat` | Verified: `.editorconfig` | `dotnet format` whitespace |
| PascalCase types/members; `Async` suffix on async methods; `I` prefix on interfaces | Inferred: all sampled code | Review |
| Sealed classes by default | Inferred: nearly every service/controller is `sealed` | Review |
| TypeScript strict, no unused locals/params | Verified: `frontend/tsconfig.json` | `tsc --noEmit` in CI |
| Domain vocabulary matches the UI and docs (Meeting, Project, Client, Rule, Unassigned, Shared, Export record) | Inferred | Review |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Consistent casing | Follows | sampled files |
| Names match the glossary | Partly | `Meeting.GranolaNoteId` stores Fireflies transcript IDs too (`Entities.cs:63-64`); `WorkItem.GranolaNoteId`, `GranolaNoteFetch`, `GranolaConnectionResult`, `GranolaConnectionState` used for Fireflies; `GranolaPI` prefix everywhere (product was renamed from a Granola-only tool) |
| Suppressed warnings | Partly | C#: only in generated migrations (`#pragma warning disable 612, 618`). Frontend: 8 `eslint-disable-next-line react-hooks/exhaustive-deps` although no ESLint is configured (dead directives) |
| Debug leftovers | Follows | 0 `Console.Write`, 0 `console.*` |
| Committed artefacts | Does not | `frontend/src/**/*.js`, `vite.config.js`, `tsconfig.tsbuildinfo` (RF-001); `run-test.log` (RF-013); `tools/node-cli/config.example copy.json` (RF-011) |
| Warnings as errors | Does not | `TreatWarningsAsErrors=false`; `CS1591` suppressed globally |

## How the code does it today

Clean, consistent C# naming; `sealed` + primary constructors; expressive method names (`MarkInterruptedAsync`, `SupersedeStaleRecords`, `OpenAbsoluteUnderRoots`).

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `Meeting.GranolaNoteId` and related types | Source-specific names for multi-source data | Low | `Entities.cs:64`, `MeetingProcessor.cs:15` |
| `frontend/src/**` | 8 dead `eslint-disable` directives | Low | grep |
| Repository root | Stray `run-test.log` committed | Low | `git ls-files` |
| `tools/node-cli` | `config.example copy.json` | Low | `git ls-files` |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Add ESLint with `react-hooks` (makes the directives meaningful) | Code + standard | Catch hook dependency bugs | S | P2 | PIDA-010 / ADR-008 |
| Rename to `SourceId` etc. with a migration (column rename) | Code | Clarity; enables a `(Source, SourceId)` unique index | L | P3 | TD-003 / PIDA-011 |
| Remove stray files; rename the node-cli example | Code | Hygiene | S | P3 | PIDA-013 |
| Raise `.editorconfig` severities that matter to `warning` and enable analyzers in CI | Standard | Make conventions enforceable | M | P2 | ADR-009 |
