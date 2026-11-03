# Client Matrix

Last reviewed: 2026-10-28

- Evidence scope: searched source and configuration for tenant/client selection mechanisms (`tenant`, `ClientId`, `X-Client`, per-client config files, feature flags, per-client folders, build variants). No client list was provided by the user in this session.
- Confidence: High

**Not applicable — single-user, single-deployment application.** Each installation serves one person (one API key set, one SQLite database, no user accounts; `Program.cs`, `Middleware.cs`). There is no tenant selection, no per-client configuration or branding, and no client-specific code path.

**Terminology warning:** the codebase has a domain entity called `Client` (`src/Chroniq.Domain/Entities/Entities.cs:5-15`, `ClientsController`, `/clients` page). It represents *the user's own customers* (for example the company a meeting was with) and groups projects and classification rules. It is user-entered data, not a software client or tenant, and does not change application behaviour per installation. Do not record those records here.

## Client list sources

| Source | Type | Date captured | Notes |
|---|---|---|---|
| Code and configuration search | code | 2026-10-28 | No tenants or deployment variants found |
| User | user-provided | — | None provided in this session |

## Clients

| ID | Client name | Code / key in source | Status | Environments / deployments | Source | Evidence |
|---|---|---|---|---|---|---|
| — | Not applicable — single-user application | — | — | Desktop (Windows) or self-hosted container per user | code | `Program.cs`, `docker-compose.yml` |

## How client-specific behavior is implemented

Not applicable — no mechanism exists. Behaviour differences are per installation, controlled by the installing user's settings (`ApplicationSettings` rows) and environment variables, not by a client key.

## Client-specific code locations

Not applicable — none found (Verified by search for client/tenant conditionals).

## Feature availability by client

Not applicable. Every feature in [feature-inventory.md](feature-inventory.md) is available to every installation; desktop-only features (Credential Manager writes, open in Explorer) depend on the host OS, not on a client (see FEAT-013, FEAT-015).

## Customizations per client

Not applicable.

## Risks and gaps

| Item | Clients affected | Evidence | Suggested action |
|---|---|---|---|
| Confusion between the domain `Client` entity and PIDA's client concept | — | `Entities.cs:5` | Keep this note; use "customer" in prose when ambiguous |
| Future multi-user hosting would need authentication, per-user data and secrets; none exist today | — | `Middleware.cs:60-105` | Q-008 |
