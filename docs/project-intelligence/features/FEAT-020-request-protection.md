# FEAT-020 — Local-first request protection

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: Medium
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: NFR-002, NFR-003; SEC-001, SEC-005; flow diagram in [architecture.md](../architecture.md#3-request-protection)

## User outcome

Only the person on the machine — or, in server mode, someone holding the API token — can use the API. Other websites open in the user's browser cannot trigger actions, and meeting data is never cached by the browser.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Loopback caller (no token configured) | Everything | `RequestProtectionMiddleware` | `Middleware.cs:91` |
| Remote caller (no token configured) | Nothing (403) unless `AllowRemoteWithoutToken` | same | `:91-95` |
| Any caller (token configured) | Everything with `Authorization: Bearer <token>`; 401 without | same | `:81-90` |
| Anyone | `GET /api/health`, static SPA | same | `:76` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Middleware | Every request | `RequestProtectionMiddleware.InvokeAsync` | `Middleware.cs:64-105` |
| Screen | `ApiTokenModal` opens on any 401 and stores the token in `sessionStorage` | frontend `api.ts:51-72, 224-229` | sub-audit |

## Behavior

- **Main flow:** every response gets `X-Correlation-ID`, `nosniff`, `no-referrer`; `/api` responses get `Cache-Control: no-store`; caller check as above; state-changing `/api` requests need `X-Chroniq-Request: 1`. CORS only allows `Security:AllowedOrigins`; `AllowedHosts` limits host headers (desktop).
- **Validation and error cases:** 401/403 as RFC 7807 problems; token compared in constant time.
- **Edge cases:** an over-long or missing correlation header is replaced (`:67`); `RemoteIpAddress == null` counts as loopback (`:80`).

## Data touched

None.

## Integrations and side effects

None.

## Configuration and feature flags

`Security:ApiToken`, `Security:AllowRemoteWithoutToken` (false), `Security:AllowedOrigins`, `AllowedHosts`.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Health_is_public_and_reports_status` | Integration | Health exemption |
| `State_changing_requests_require_the_csrf_header` | Integration | CSRF rule |
| `Cross_origin_preflight_from_unknown_site_is_not_allowed` | Integration | CORS |
| `Api_responses_are_never_cached_by_the_browser` | Integration | `no-store` |
| `api.test.ts` | Frontend unit | CSRF header, bearer token, 401 event |

## Gaps and follow-up

- Token present/absent/wrong and non-loopback rejection are untested (SEC-001, PIDA-005).
- Compose disables host filtering (`AllowedHosts: "*"`) (SEC-005).
