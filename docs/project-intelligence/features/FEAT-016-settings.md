# FEAT-016 — Settings management and validation

Last reviewed: 2026-10-28

- Status: Implemented
- Confidence: High
- Inventory row: [feature-inventory.md](../feature-inventory.md)
- Clients: Not applicable
- Related: [docs/CONFIGURATION.md](../../CONFIGURATION.md); `ApplicationSetting`

## User outcome

All routine configuration — export roots and templates, sync behaviour, classification tiers, logging — is edited in the UI, validated with field-level messages, and the resolved absolute paths are shown.

## Users and permissions

| Role | Can do | Enforced at | Evidence |
| --- | --- | --- | --- |
| Local user | Read/update settings | Middleware | `SystemControllers.cs:67-111` |

## Entry points

| Type | Name / path | Handler | Evidence |
| --- | --- | --- | --- |
| Route | `GET /api/settings`, `PUT /api/settings` | `SettingsController` | `SystemControllers.cs:72-89` |
| Screen | `/settings` tabs | `SettingsPage` (646 lines) | sub-audit |

## Behavior

- **Main flow:** Given an `UpdateSettingsRequest` with any of export/sync/classification/logging, when saved, then `SettingsValidator.Validate` runs; on success each provided section is stored as JSON; classification settings are normalised; logging settings are applied to the logger state; the response includes resolved paths (`SystemControllers.cs:75-111`).
- **Validation:** errors → 400 with `errors[]` (tested).
- **Edge cases:** missing or corrupt sections fall back to defaults (`SettingsService.cs:24-36`).

## Data touched

`ApplicationSettings` (`export`, `sync`, `classification`, `logging`).

## Integrations and side effects

Logging changes are meant to apply immediately but do not (RF-003).

## Configuration and feature flags

See [docs/CONFIGURATION.md](../../CONFIGURATION.md) and `AppSettings.cs`.

## Client availability

Not applicable.

## Tests

| Test file / name | Type | What it proves |
| --- | --- | --- |
| `Invalid_settings_are_rejected_with_field_errors` | Integration | Validation |
| `Settings_round_trip_and_resolve_paths` | Integration | Persistence and path resolution |

## Gaps and follow-up

- RF-003 / PIDA-003: logging section ignored until restart.
