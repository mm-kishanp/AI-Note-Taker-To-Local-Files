# Project Intelligence

Last reviewed: 2026-10-28

Evidence-based knowledge base for **Granola Project Intelligence Sync**, maintained by PIDA (see `CLAUDE.md`). Start with [project.md](project.md).

## Scope of the current audit (2026-10-28)

- **Inspected:** all backend source (`src/`), tests, CI, Docker, configuration, README and `docs/SECURITY.md`; `frontend/`, `tools/node-cli/` and `scripts/` through read-only sub-audits.
- **Sampled:** frontend code quality (6 files plus repo-wide greps); `docs/*.md` other than SECURITY used as leads.
- **Excluded:** `GranolaProjectKnowledge/` and `data/` (user meeting content and database), secrets files, `node_modules/`, build output, older generated migrations.
- **Not run:** builds, tests, linters, scanners. Commands are labelled Documented.

## Rules

- Verify content against current code before relying on it.
- Label material findings `Verified`, `Inferred`, or `Unknown`.
- Include source paths and symbols/lines where practical.
- Record scan scope and exclusions.
- Do not store credentials, tokens, production data, or other secrets. Never copy meeting content from `GranolaProjectKnowledge/` or `data/` into these pages.
- Link to existing authoritative documentation rather than duplicating it.
- Refresh relevant pages after code, schema, dependency, API, or deployment changes. `python scripts/doc-freshness.py` lists pages with stale or placeholder review dates and cited paths that no longer exist.
- Blank templates for every page live in the templates folder of the `project-audit` skill (source: `.agents/skills/project-audit/templates/`).

## Pages

| Page | What it answers |
| --- | --- |
| [project.md](project.md) | Plain-language explanation; start here |
| [projects/](projects/) | One page per sub-project: [api](projects/api/project.md), [worker](projects/worker/project.md), [application](projects/application/project.md), [infrastructure](projects/infrastructure/project.md), [domain](projects/domain/project.md), [frontend](projects/frontend/project.md), [node-cli](projects/node-cli/project.md) |
| [project-overview.md](project-overview.md) | Glossary, stack, runtime components, maturity |
| [repository-map.md](repository-map.md) | Where everything lives; where to change what |
| [feature-inventory.md](feature-inventory.md) + [features/](features/) | 22 features with status and evidence, one page each |
| [requirements.md](requirements.md) | Goals, roles, FR/NFR |
| [architecture.md](architecture.md) | Components, flows (Mermaid), trust boundaries, risks |
| [data-model.md](data-model.md) | SQLite schema, ER diagram, migrations |
| [api-catalog.md](api-catalog.md) | All 57 endpoints and the auth rules |
| [integrations.md](integrations.md) | Granola, Fireflies, Anthropic, Credential Manager, file system |
| [client-matrix.md](client-matrix.md) | Not applicable (single-user); note on the `Client` entity |
| [security-review.md](security-review.md) | Surfaces and SEC findings |
| [quality-and-deployment.md](quality-and-deployment.md) | Local setup, quality gates, CI |
| [deployment.md](deployment.md) | Desktop and Docker deployment, rollback |
| [debugging.md](debugging.md) | Debugging each part, logs, common problems |
| [coding-standards/overview.md](coding-standards/overview.md) | Standards scorecard and the eight topic pages |
| [code-review-guide.md](code-review-guide.md) | Review practice and checklist |
| [review-findings.md](review-findings.md) | RF findings |
| [tech-debt.md](tech-debt.md) | TD register |
| [seo-guide.md](seo-guide.md), [aeo-guide.md](aeo-guide.md) | Not applicable (private admin UI) |
| [open-questions.md](open-questions.md) | Q items for the owner |
| [analysis-log.md](analysis-log.md) | Audit history |

Project management: [BACKLOG](../../project-management/BACKLOG.md), [RISKS](../../project-management/RISKS.md), [DECISIONS](../../project-management/DECISIONS.md), [PROGRESS](../../project-management/PROGRESS.md).

## Equivalent existing documentation (authoritative; linked, not duplicated)

| Existing doc | Covers | PIDA page that links it |
| --- | --- | --- |
| [README.md](../../README.md) | Product overview, quick start, testing | project.md, project-overview.md |
| [docs/ARCHITECTURE.md](../ARCHITECTURE.md) | Architecture narrative | architecture.md |
| [docs/INSTALLATION.md](../INSTALLATION.md) | Install / server setup | deployment.md |
| [docs/CONFIGURATION.md](../CONFIGURATION.md) | Settings, templates, rule kinds | feature pages, deployment.md |
| [docs/CLASSIFICATION.md](../CLASSIFICATION.md) | Classification tiers | FEAT-004 |
| [docs/MARKDOWN_FORMAT.md](../MARKDOWN_FORMAT.md) | Output file format | FEAT-006, FEAT-012 |
| [docs/SECURITY.md](../SECURITY.md) | User-facing security policy | security-review.md |
| [docs/TROUBLESHOOTING.md](../TROUBLESHOOTING.md) | Troubleshooting, backup | debugging.md, deployment.md |
| [docs/GRANOLA_API.md](../GRANOLA_API.md), [docs/FIREFLIES_API.md](../FIREFLIES_API.md) | External API notes | integrations.md |
| [docs/openapi.json](../openapi.json) | REST contract snapshot | api-catalog.md |

No ADR folder, issue tracker export, CODEOWNERS or CONTRIBUTING exists.
