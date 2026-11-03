# API Catalog

Last reviewed: 2026-10-28

- Evidence scope: every controller in `src/Chroniq.Api/Controllers/` (57 endpoints), `Program.cs` route mappings, `Middleware/Middleware.cs`. The generated contract snapshot is [docs/openapi.json](../openapi.json) (served at `/openapi/v1.json` and Swagger UI `/swagger` in Development only, `Program.cs:60-64`; the integration test `Openapi_document_is_not_exposed_outside_development` proves it is hidden otherwise).
- Confidence: High

## Authentication and conventions (apply to every `/api` route)

| Rule | Detail | Evidence |
|---|---|---|
| Caller restriction | Without `Security:ApiToken`: only loopback callers (403 otherwise, unless `Security:AllowRemoteWithoutToken=true`). With a token: **every** caller (including loopback) must send `Authorization: Bearer <token>` (401 otherwise). `/api/health` is exempt. | `Middleware.cs:76-95` |
| CSRF | Every non-GET/HEAD/OPTIONS `/api` request must send `X-Chroniq-Request: 1` (403 otherwise) | `Middleware.cs:97-102`; test `State_changing_requests_require_the_csrf_header` |
| Host / CORS | `AllowedHosts` `localhost;127.0.0.1` (desktop) / `*` (compose); CORS origins `Security:AllowedOrigins` (default Vite dev server) | `appsettings.json`, `docker-compose.yml`, `Program.cs:40-44` |
| Errors | RFC 7807 problem details with `category`, `correlationId`, and `errors[]` for validation; 400 validation, 404 not found, 409 sync running, 424 Granola auth/permission, 502 Granola error, 499 cancelled, 500 unexpected (message hidden outside Development) | `Middleware.cs:14-50` |
| Headers | `X-Correlation-ID` echoed or generated; `Cache-Control: no-store` on `/api`; `X-Content-Type-Options: nosniff`; `Referrer-Policy: no-referrer` | `Middleware.cs:66-75` |
| Enums | Serialized as strings | `Program.cs:30-36` |
| Unknown `/api/*` | JSON 404 problem, never the SPA page | `Program.cs:71-72`; test `Unknown_api_paths_return_404_json_not_the_ui_page` |

"Auth" below is **Local/Token** for every route except health (Anonymous). There are no roles.

## Endpoints

| Method | Route | Purpose | Auth | Request / contract | Response / side effects | Evidence |
|---|---|---|---|---|---|---|
| GET | `/api/health` | Liveness, DB check, whether any key is configured, sync running | Anonymous | — | `HealthDto` | `SystemControllers.cs:16-29` |
| POST | `/api/sync/start` | Start a run (Incremental, Full, RetryFailed, ReExport, Reconcile) | Local/Token + CSRF | `StartSyncRequest(Mode, MeetingIds?, Reclassify, Force)` | 202 `SyncStartResult` or 409 | `SystemControllers.cs:35-40` |
| POST | `/api/sync/retry-failed` | Retry all failed meetings | Local/Token + CSRF | — | 202/409 (not called by the UI) | `:42-47` |
| POST | `/api/sync/cancel` | Cancel the running sync | Local/Token + CSRF | — | 202 or 204 | `:49-50` |
| GET | `/api/sync/status` | Live progress snapshot | Local/Token | — | `SyncProgressSnapshot` | `:52-53` |
| GET | `/api/sync/history` | Paged run list | Local/Token | `page`, `pageSize` | `PagedResponse<SyncRunDto>` | `:55-57` |
| GET | `/api/sync/history/{id}` | Run detail with items | Local/Token | — | `{ run, items }` (untyped `object`) | `:59-64` |
| GET / PUT | `/api/settings` | Read / update export, sync, classification, logging settings | Local/Token (+CSRF on PUT) | `UpdateSettingsRequest` validated by `SettingsValidator` | `SettingsDto` incl. resolved paths | `:72-89` |
| GET / PUT / DELETE | `/api/settings/credentials/granola` | Masked status / save+validate / remove Granola key | Local/Token (+CSRF) | `SetCredentialRequest(ApiKey, Validate)` | `CredentialStatusDto` (masked) | `:115-122`, `CredentialService.cs` |
| GET / PUT / DELETE | `/api/settings/credentials/fireflies` | Same for Fireflies | Local/Token (+CSRF) | same | same | `:126-133` |
| GET / PUT / DELETE | `/api/settings/credentials/whisperai` | Same for WhisperAI (added 2026-11-02) | Local/Token (+CSRF) | same | same | `SystemControllers.cs` `SettingsController.WhisperAiStatus/SetWhisperAi/DeleteWhisperAi` |
| GET / PUT / DELETE | `/api/settings/credentials/ai` | Same for the Anthropic key (no validation call) | Local/Token (+CSRF) | same | `AiStatusDto` | `:137-159` |
| POST | `/api/granola/test-connection` | Test Granola key | Local/Token + CSRF | — | `ConnectionTestDto` | `:166-167` |
| GET | `/api/granola/folders` | Granola folders visible to the key | Local/Token | — | `GranolaFolder[]` (UI hook exists but is unused) | `:170-171` |
| POST | `/api/fireflies/test-connection` | Test Fireflies key | Local/Token + CSRF | — | `ConnectionTestDto` | `:178-179` |
| POST | `/api/whisperai/test-connection` | Test WhisperAI key (added 2026-11-02) | Local/Token + CSRF | — | `ConnectionTestDto` | `SystemControllers.cs` `WhisperAiController` |
| GET | `/api/exports/tree` | Export tree (≤5000 nodes) | Local/Token | `root` = root/notes/transcripts | `ExportTreeDto` | `:187-188`, `ExportBrowserService.cs:24-34` |
| GET | `/api/exports/file` | Read one `.md` (≤20 MB) under a root | Local/Token | `root`, `path` (root-guarded) | `FileContentDto`; test `Export_browser_blocks_path_traversal` | `:190-191` |
| POST | `/api/exports/open` | Open root/file in OS shell (desktop only) | Local/Token + CSRF | `root`, `path?` | 204; starts `explorer.exe`/`open`/`xdg-open` | `:193-198` |
| POST | `/api/exports/validate-path` | Check/create a folder, writability, free space | Local/Token + CSRF | `ValidatePathRequest(Path, CreateIfMissing)` | `ValidatePathResultDto`; **may create a directory anywhere the process can write** | `ExportBrowserService.cs:98-157` |
| POST | `/api/exports/migrate-root` | Copy export repo to a new root and switch settings | Local/Token + CSRF | `MigrateRootRequest(NewRoot, Confirm)` | `MigrateRootResultDto`; copies files, never deletes | `ExportBrowserService.cs:188-235` |
| GET | `/api/exports/directories` | Folder picker: drives/home or sub-folder names of any path | Local/Token | `path?` | `DirectoryListingDto` (≤500 names) | `ExportBrowserService.cs:159-182` |
| GET | `/api/meetings` | Filter/sort/page meetings | Local/Token | `MeetingQuery` | `PagedResponse<MeetingListItemDto>` | `MeetingsController.cs:14-15` |
| GET | `/api/meetings/{id}` | Meeting detail | Local/Token | — | `MeetingDetailDto` | `:17-18` |
| GET | `/api/meetings/{id}/transcript` | Paged transcript | Local/Token | `page`, `pageSize` (default 100) | `PagedResponse<TranscriptSegmentDto>` | `:20-22` |
| POST | `/api/meetings/{id}/assign-project` | Manual assignment (Project/Shared/Excluded/None) | Local/Token + CSRF | `AssignMeetingRequest(Kind, ProjectId?, Note?)` | `MeetingDetailDto`; re-classify + re-export | `:25-26` |
| GET | `/api/meetings/{id}/destination-preview` | Where files would go for an assignment | Local/Token | `kind`, `projectId?` | `DestinationPreviewDto[]` | `:28-30` |
| POST | `/api/meetings/{id}/reclassify` | Re-run rules | Local/Token + CSRF | — | `MeetingDetailDto` | `:32-33` |
| POST | `/api/meetings/{id}/retry` | Re-fetch from source in background | Local/Token + CSRF | — | 202/409 | `:36-41` |
| POST | `/api/meetings/{id}/export` | Regenerate files; `force` overwrites local edits | Local/Token + CSRF | `ReExportRequest(Notes, Transcript, Force)` | `MeetingDetailDto` | `:44-45` |
| GET | `/api/meetings/{id}/exports/{recordId}/conflict` | Conflict review (local vs regenerated) | Local/Token | — | `ConflictReviewDto` | `:47-48` |
| POST | `/api/meetings/{id}/exports/{recordId}/resolve` | Resolve conflict | Local/Token + CSRF | `ResolveConflictRequest(Resolution)` | `MeetingDetailDto` | `:50-52` |
| POST | `/api/meetings/{id}/exports/{recordId}/open` | Open exported file/folder in OS | Local/Token + CSRF | `folder` | 204 | `:54-61` |
| GET | `/api/unassigned` | Review queue | Local/Token | `page`, `pageSize` | `PagedResponse<UnassignedItemDto>` | `:75-77` |
| POST | `/api/unassigned/{id}/assign` | Assign from review | Local/Token + CSRF | `AssignMeetingRequest` | `MeetingDetailDto` | `:79-80` |
| POST | `/api/unassigned/{id}/mappings` | Save participant/speaker mapping from a meeting and preview matches | Local/Token + CSRF | `SaveMappingFromReviewRequest` | `TestMappingResultDto` | `:83-84` |
| GET | `/api/dashboard` | Dashboard statistics | Local/Token | — | `DashboardDto` | `:91-92` |
| GET / POST | `/api/projects` | List (search, includeArchived) / create | Local/Token (+CSRF) | `ProjectInput` | `ProjectDto[]` / 201 `ProjectDto` | `ProjectsController.cs:15-24` |
| GET / PUT | `/api/projects/{id}` | Get / update | Local/Token (+CSRF) | `ProjectInput` | `ProjectDto` | `:26-30` |
| POST | `/api/projects/{id}/archive`, `/restore` | Change status | Local/Token + CSRF | — | `ProjectDto` | `:32-36` |
| POST | `/api/projects/{id}/rules` | Add a rule (not called by the UI) | Local/Token + CSRF | `RuleInput(Kind, Value, Enabled)` | `RuleDto` | `:38-39` |
| POST | `/api/projects/{id}/test-mapping` | Dry run of rules | Local/Token + CSRF | `TestMappingRequest` | `TestMappingResultDto` | `:42-44` |
| POST | `/api/projects/{id}/apply-mapping` | Re-classify + re-export selected meetings (needs `confirm`) | Local/Token + CSRF | `ApplyRulesRequest(Confirm, MeetingIds)` | 202/409 | `:47-54` |
| GET | `/api/projects/config/export` | Export clients/projects/rules as JSON | Local/Token | — | `ProjectConfigExport` | `:56-57` |
| POST | `/api/projects/config/import` | Import that JSON | Local/Token + CSRF | `ProjectConfigExport` | `ImportResultDto` | `:59-60` |
| GET / POST | `/api/clients` | List / create clients | Local/Token (+CSRF) | `ClientInput` | `ClientDto` | `:67-71` |
| PUT / DELETE | `/api/clients/{id}` | Update / delete client | Local/Token + CSRF | `ClientInput` | `ClientDto` / 204 | `:73-81` |
| GET | `/openapi/v1.json`, `/swagger` | Contract and UI | Development only | — | — | `Program.cs:60-64` |
| GET | `/*` (non-API) | Static SPA with fallback to `index.html` | None (static) | — | — | `Program.cs:67-73` |

No project or rule **delete** endpoint exists; projects are archived (Verified). There are no webhooks, message topics or events; the app only calls out.
