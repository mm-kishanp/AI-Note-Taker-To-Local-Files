# FEAT-015 — API key management

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: NFR-002; [security-review.md](../security-review.md); INT-001–INT-004

## User outcome

The user pastes a Granola, Fireflies or Anthropic key in Settings; it is validated (Granola/Fireflies), stored in Windows Credential Manager, shown only masked, and can be tested or removed. On servers keys come from environment variables and the UI says the store is read-only.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user / token holder | Read masked status, set, delete, test | Middleware | `SystemControllers.cs:113-180` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Routes | `GET/PUT/DELETE /api/settings/credentials/{granola,fireflies,ai}` | `CredentialService` (Granola/Fireflies), controller (AI) | `SystemControllers.cs:115-159` |
| Routes | `POST /api/granola/test-connection`, `POST /api/fireflies/test-connection` | `CredentialService.TestAsync` | `:166-179` |
| Screen | Settings → Connections; `ApiTokenModal` is separate (FEAT-020) | `SettingsPage` | sub-audit |

## Behavior

- **Main flow:** Given a key of 10–512 characters without whitespace, when saved with `validate=true`, then it is written to the store, a test call is made, and if the source answers Authentication failure the previous key is restored (or the new one deleted) and 400 is returned (`CredentialService.cs:28-53`). Status returns `{configured, source, masked (first 4 + •••••••• + last 4), canWrite, storeName, last check results}`.
- **Resolution order:** OS store first, then `GRANOLA_API_KEY`/`FIREFLIES_API_KEY`/`ANTHROPIC_API_KEY`, then `Secrets:*` configuration (`CredentialStores.cs:137-164`).
- **Error cases:** read-only store → 400 telling the user which environment variable to set; Credential Manager read failure falls back to configuration.
- **Edge cases:** a key with permission (403) errors is kept (only 401 is treated as wrong key).

## Data touched

| Entity / table | Read / write | Notes |
| --- | --- | --- |
| Windows Credential Manager `GranolaProjectIntelligence:{GranolaApiKey,FirefliesApiKey,AiApiKey}` | R/W | Secret values |
| `ApplicationSettings` `granola.connection`, `fireflies.connection` | R/W | last check status only, no secrets |

## Integrations and side effects

INT-001/002 test calls; INT-004.

## Configuration and feature flags

`Security:UseWindowsCredentialManager` (true; false in Docker).

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Credentials_are_masked_and_never_returned_in_full` | Integration | Masking |
| `Invalid_key_input_is_rejected_with_problem_details` | Integration | Input validation |
| `Fireflies_credentials_are_masked_and_tested_separately` | Integration | Fireflies path |
| `pages.test.tsx` (credential forms) | Component | UI shows masked keys only |

## Gaps and follow-up

- AI key handling bypasses `CredentialService` and has no test (architecture-and-layering page).
