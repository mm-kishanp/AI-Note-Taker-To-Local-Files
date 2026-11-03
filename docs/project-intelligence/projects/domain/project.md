# Chroniq.Domain (entities and rules)

Last reviewed: 2026-10-28

- Type: Library
- Location: `src/Chroniq.Domain/`
- Status: Active
- Confidence: High
- Part of: [project.md](../../project.md)

## What it is

Dependency-free core types: the persisted entities, all enums, the `Slug` value object and text normalisation used to compare names, emails and keywords. Every other project depends on it.

## Responsibilities

- `Entities/Entities.cs`: `Client`, `Project`, `ClassificationRule`, `Meeting`, `MeetingParticipant`, `MeetingProjectAssignment`, `ExportRecord`, `SyncRun`, `SyncRunItem`, `ApplicationSetting`, `ClassificationReview`, `SyncLease`.
- `Enums/Enums.cs`: statuses, rule kinds, tiers, modes, error categories.
- `Rules/TextNormalizer.cs`, `ValueObjects/Slug.cs`.

## Features

| Feature | Role in this sub-project | Page |
| --- | --- | --- |
| All features | contributes (data types) | [feature-inventory.md](../../feature-inventory.md) |

## How it starts and runs

| Item | Value | Evidence |
| --- | --- | --- |
| Entry point | none (library) | — |
| Runtime / framework | `net10.0`, no packages | csproj |
| Build | part of the solution | — |
| Run locally | not applicable | — |
| Tests | covered indirectly by unit tests | — |
| Deployed as | inside the API | — |

## Dependencies

None.

## Interfaces

- **Exposes:** public entity/enum types.
- **Consumes:** nothing.

## Data

Defines the shape of every table ([data-model.md](../../data-model.md)).

## Configuration

None.

## Ownership and conventions

Mutable entity classes with code-generated Guid IDs and UTC `DateTime`s.

## Risks and gaps

- `Meeting.GranolaNoteId` holds IDs from two sources (TD-003).
