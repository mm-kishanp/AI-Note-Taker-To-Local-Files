# Commenting and Documentation

Last reviewed: 2026-10-28

- Evidence scope: XML doc comments across all non-migration C# files (sampled ~20 files in full); `docs/` folder; frontend comments (sub-audit); TODO/FIXME grep over `src`, `tests`, `frontend/src`.
- Status: Consistent
- Back to: [overview.md](overview.md)

## Standard for this project

| Rule | Source | Enforced by |
| --- | --- | --- |
| Public types and non-obvious members carry a `<summary>` explaining *why* / guarantees, not *what* | Inferred: present on nearly every service, client and entity sampled (e.g. `SyncEngine.cs:16-21`, `MeetingExporter.cs:33-37`, `ExportBrowserService.cs:15-18`) | Nothing (`CS1591` suppressed in `Directory.Build.props`) |
| Inline comments explain safety decisions ("never…", "don't follow links…") | Inferred | Review |
| User-facing behaviour is documented in `docs/*.md`; README stays the entry point | Verified: 9 docs + OpenAPI snapshot | Nothing |
| No TODO/FIXME left in code | Inferred: 0 occurrences (grep) | Nothing |

## Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Public APIs documented | Follows | sampled services/controllers |
| No commented-out code | Follows | none seen in sampled files |
| TODO/FIXME density | Follows | 0 in `src`, `tests`, `frontend/src`, `tools/node-cli` |
| Comments accurate | Partly | `Program.cs:66` says "see scripts/build.ps1"; the script is `scripts/build-frontend.ps1`. `SynchronizationWorker` summary says it runs "when a Granola key is configured" — accurate to code, but the README implies Fireflies works too (RF-004). `scripts/e2e-live.mjs` header says "two test projects" but creates one (sub-audit) |
| Docs match code | Partly | README test counts (110 unit + 28 integration) differ from current methods (87 + 30); `tools/node-cli/README.md` references a non-existent `config.example.json` |

## How the code does it today

Copy the style of `RequestProtectionMiddleware`'s summary (`Middleware.cs:52-59`): a bullet list of guarantees and the reason for each.

## Deviations

| Where | What differs from the standard | Severity | Evidence |
| --- | --- | --- | --- |
| `Program.cs:66` | Wrong script name | Low | file |
| `README.md:130` | Stale test counts | Low | test method counts |
| `tools/node-cli/README.md:10`, `config.js:28` | Point to a missing file | Low | sub-audit |

## Recommended changes

| Change | Code or standard | Why | Effort | Priority | Task |
| --- | --- | --- | --- | --- | --- |
| Fix the three stale references and avoid hard-coding test counts in README | Code/docs | Accuracy | S | P3 | PIDA-013 |
