import type {
  AiStatusDto, ApplyRulesRequest, AssignMeetingRequest, ClientDto, ClientInput, ConflictResolution, ConflictReviewDto,
  ConnectionTestDto, CredentialStatusDto, DashboardDto, DestinationPreviewDto, DirectoryListingDto, ExportRootKind,
  ExportTreeDto, FileContentDto, GranolaFolder, HealthDto, ImportResultDto, ManualAssignmentKind, MeetingDetailDto,
  MeetingListItemDto, MeetingQuery, MeetingSource, MigrateRootRequest, MigrateRootResultDto, PagedResponse, ProblemDetails,
  ProjectConfigExport, ProjectDto, ProjectInput, ReExportRequest, RuleDto, RuleInput, SaveMappingFromReviewRequest,
  SetCredentialRequest, SettingsDto, StartSyncRequest, SyncProgressSnapshot, SyncRunDetailDto, SyncRunDto,
  SyncStartResult, TestMappingRequest, TestMappingResultDto, TranscriptSegmentDto, UnassignedItemDto,
  UpdateSettingsRequest, ValidatePathRequest, ValidatePathResultDto,
} from '../types/api';

export const API_BASE = '/api';
export const CSRF_HEADER = 'X-GPI-Request';
export const API_TOKEN_STORAGE_KEY = 'gpi.apiToken';
/** Window event dispatched when the server answers 401 (remote deployment requires an API token). */
export const AUTH_REQUIRED_EVENT = 'gpi:auth-required';

/** Optional bearer token for remote deployments (Security:ApiToken). Stored per browser session only. */
export function getApiToken(): string | null {
  try {
    const t = window.sessionStorage.getItem(API_TOKEN_STORAGE_KEY);
    return t && t.trim() ? t.trim() : null;
  } catch {
    return null;
  }
}

export function setApiToken(token: string | null): void {
  try {
    if (token && token.trim()) window.sessionStorage.setItem(API_TOKEN_STORAGE_KEY, token.trim());
    else window.sessionStorage.removeItem(API_TOKEN_STORAGE_KEY);
  } catch {
    /* storage unavailable (private mode / blocked) — token simply is not persisted */
  }
}

export interface FieldError {
  field: string;
  message: string;
}

/** Error thrown for any failed API call. Carries the parsed RFC 7807 problem details. */
export class ApiError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string | null;
  readonly category: string | null;
  readonly correlationId: string | null;
  readonly errors: FieldError[];
  /** Raw JSON body (e.g. a SyncStartResult on 409). */
  readonly body: unknown;

  constructor(init: {
    status: number; title: string; detail?: string | null; category?: string | null; correlationId?: string | null;
    errors?: FieldError[]; body?: unknown;
  }) {
    super(init.detail || init.title);
    this.name = 'ApiError';
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail ?? null;
    this.category = init.category ?? null;
    this.correlationId = init.correlationId ?? null;
    this.errors = init.errors ?? [];
    this.body = init.body;
  }

  /** Errors for a field, matching case-insensitively and allowing "section.field" prefixes. */
  fieldError(field: string): string | undefined {
    const f = field.toLowerCase();
    const hit = this.errors.filter((e) => {
      const ef = e.field.toLowerCase().replace(/^\$\./, '');
      return ef === f || ef.endsWith('.' + f);
    });
    return hit.length ? hit.map((e) => e.message).join(' ') : undefined;
  }

  get isNetworkError(): boolean {
    return this.status === 0;
  }
}

function normalizeErrors(errors: ProblemDetails['errors']): FieldError[] {
  if (!errors) return [];
  if (Array.isArray(errors)) {
    return errors
      .filter((e) => e && typeof e === 'object')
      .map((e) => {
        const r = e as unknown as Record<string, unknown>;
        return { field: String(r.field ?? r.Field ?? ''), message: String(r.message ?? r.Message ?? '') };
      });
  }
  const out: FieldError[] = [];
  for (const [field, msgs] of Object.entries(errors)) {
    for (const m of Array.isArray(msgs) ? msgs : [msgs]) out.push({ field, message: String(m) });
  }
  return out;
}

/** Converts a failed fetch Response into an ApiError, parsing problem+json when present. */
export async function toApiError(res: Response): Promise<ApiError> {
  let body: unknown = undefined;
  const text = await res.text().catch(() => '');
  if (text) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  }
  if (body && typeof body === 'object') {
    const p = body as ProblemDetails & { message?: string };
    return new ApiError({
      status: res.status,
      title: p.title || res.statusText || `Request failed (${res.status})`,
      detail: p.detail ?? p.message ?? null,
      category: typeof p.category === 'string' ? p.category : null,
      correlationId: p.correlationId ?? res.headers.get('X-Correlation-ID') ?? p.traceId ?? null,
      errors: normalizeErrors(p.errors),
      body,
    });
  }
  return new ApiError({
    status: res.status,
    title: res.statusText || `Request failed (${res.status})`,
    detail: typeof body === 'string' && body.length < 500 ? body : null,
    correlationId: res.headers.get('X-Correlation-ID'),
    body,
  });
}

export type QueryValue = string | number | boolean | null | undefined;

export function buildUrl(path: string, query?: Record<string, QueryValue>): string {
  let url = API_BASE + path;
  if (query) {
    const params = new URLSearchParams();
    for (const [k, v] of Object.entries(query)) {
      if (v === undefined || v === null || v === '') continue;
      params.append(k, String(v));
    }
    const qs = params.toString();
    if (qs) url += '?' + qs;
  }
  return url;
}

export interface RequestOptions {
  query?: Record<string, QueryValue>;
  body?: unknown;
  signal?: AbortSignal;
}

/** Low-level typed request. Adds the CSRF header + JSON content type to every non-GET request. */
export async function request<T>(method: string, path: string, opts: RequestOptions = {}): Promise<T> {
  const upper = method.toUpperCase();
  const headers: Record<string, string> = { Accept: 'application/json' };
  const isSafe = upper === 'GET' || upper === 'HEAD';
  if (!isSafe) {
    headers[CSRF_HEADER] = '1';
    headers['Content-Type'] = 'application/json';
  }
  const token = getApiToken();
  if (token) headers.Authorization = `Bearer ${token}`;
  let res: Response;
  try {
    res = await fetch(buildUrl(path, opts.query), {
      method: upper,
      headers,
      body: !isSafe && opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      signal: opts.signal,
      credentials: 'same-origin',
      // Bypass the HTTP cache so a stale browser entry can never stand in for live API data.
      cache: 'no-store',
    });
  } catch (e) {
    if (e instanceof DOMException && e.name === 'AbortError') throw e;
    throw new ApiError({
      status: 0,
      title: 'Cannot reach the backend',
      detail: 'The Granola Project Intelligence service did not respond. Make sure it is running.',
      category: 'Network',
    });
  }
  if (!res.ok) {
    const err = await toApiError(res);
    if (res.status === 401 && !path.startsWith('/health')) {
      try {
        window.dispatchEvent(new CustomEvent(AUTH_REQUIRED_EVENT, { detail: { message: err.title, hadToken: !!token } }));
      } catch {
        /* non-browser environment */
      }
    }
    throw err;
  }
  if (res.status === 204) return undefined as T;
  const text = await res.text();
  if (!text) return undefined as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    // Every API endpoint returns JSON. Anything else (typically the app's HTML page) means the
    // backend does not know this endpoint, usually because it is an older version than the UI.
    throw new ApiError({
      status: res.status,
      title: 'Unexpected response from the backend',
      detail: 'The backend did not return data for this request. If you just updated the app, restart it with stop.bat and run.bat.',
      category: 'InvalidResponse',
    });
  }
}

const enc = encodeURIComponent;
const SOURCE_PATHS: Record<MeetingSource, string> = { Granola: 'granola', Fireflies: 'fireflies', WhisperAi: 'whisperai' };
const sourcePath = (source: MeetingSource) => SOURCE_PATHS[source];

/** Starts a background run; a 409 (already running) is surfaced as an ApiError whose detail is the server message. */
async function startRun(path: string, body?: unknown): Promise<SyncStartResult> {
  return request<SyncStartResult>('POST', path, { body });
}

export const api = {
  health: () => request<HealthDto>('GET', '/health'),
  dashboard: () => request<DashboardDto>('GET', '/dashboard'),

  meetings: {
    list: (q: MeetingQuery, signal?: AbortSignal) =>
      request<PagedResponse<MeetingListItemDto>>('GET', '/meetings', { query: { ...q } as Record<string, QueryValue>, signal }),
    get: (id: string) => request<MeetingDetailDto>('GET', `/meetings/${enc(id)}`),
    transcript: (id: string, page: number, pageSize: number) =>
      request<PagedResponse<TranscriptSegmentDto>>('GET', `/meetings/${enc(id)}/transcript`, { query: { page, pageSize } }),
    assign: (id: string, body: AssignMeetingRequest) => request<MeetingDetailDto>('POST', `/meetings/${enc(id)}/assign-project`, { body }),
    destinationPreview: (id: string, kind: ManualAssignmentKind, projectId?: string | null) =>
      request<DestinationPreviewDto[]>('GET', `/meetings/${enc(id)}/destination-preview`, { query: { kind, projectId } }),
    reclassify: (id: string) => request<MeetingDetailDto>('POST', `/meetings/${enc(id)}/reclassify`),
    retry: (id: string) => startRun(`/meetings/${enc(id)}/retry`),
    reExport: (id: string, body: ReExportRequest) => request<MeetingDetailDto>('POST', `/meetings/${enc(id)}/export`, { body }),
    conflict: (id: string, recordId: string) =>
      request<ConflictReviewDto>('GET', `/meetings/${enc(id)}/exports/${enc(recordId)}/conflict`),
    resolveConflict: (id: string, recordId: string, resolution: ConflictResolution) =>
      request<MeetingDetailDto>('POST', `/meetings/${enc(id)}/exports/${enc(recordId)}/resolve`, { body: { resolution } }),
    openExport: (id: string, recordId: string, folder: boolean) =>
      request<void>('POST', `/meetings/${enc(id)}/exports/${enc(recordId)}/open`, { query: { folder } }),
  },

  unassigned: {
    list: (page: number, pageSize: number) =>
      request<PagedResponse<UnassignedItemDto>>('GET', '/unassigned', { query: { page, pageSize } }),
    assign: (id: string, body: AssignMeetingRequest) => request<MeetingDetailDto>('POST', `/unassigned/${enc(id)}/assign`, { body }),
    saveMapping: (id: string, body: SaveMappingFromReviewRequest) =>
      request<TestMappingResultDto>('POST', `/unassigned/${enc(id)}/mappings`, { body }),
  },

  projects: {
    list: (search?: string, includeArchived?: boolean) =>
      request<ProjectDto[]>('GET', '/projects', { query: { search, includeArchived } }),
    get: (id: string) => request<ProjectDto>('GET', `/projects/${enc(id)}`),
    create: (body: ProjectInput) => request<ProjectDto>('POST', '/projects', { body }),
    update: (id: string, body: ProjectInput) => request<ProjectDto>('PUT', `/projects/${enc(id)}`, { body }),
    archive: (id: string) => request<ProjectDto>('POST', `/projects/${enc(id)}/archive`),
    restore: (id: string) => request<ProjectDto>('POST', `/projects/${enc(id)}/restore`),
    addRule: (id: string, body: RuleInput) => request<RuleDto>('POST', `/projects/${enc(id)}/rules`, { body }),
    testMapping: (id: string, body: TestMappingRequest) =>
      request<TestMappingResultDto>('POST', `/projects/${enc(id)}/test-mapping`, { body }),
    applyMapping: (id: string, body: ApplyRulesRequest) => startRun(`/projects/${enc(id)}/apply-mapping`, body),
    exportConfig: () => request<ProjectConfigExport>('GET', '/projects/config/export'),
    importConfig: (body: ProjectConfigExport) => request<ImportResultDto>('POST', '/projects/config/import', { body }),
  },

  clients: {
    list: () => request<ClientDto[]>('GET', '/clients'),
    create: (body: ClientInput) => request<ClientDto>('POST', '/clients', { body }),
    update: (id: string, body: ClientInput) => request<ClientDto>('PUT', `/clients/${enc(id)}`, { body }),
    remove: (id: string) => request<void>('DELETE', `/clients/${enc(id)}`),
  },

  sync: {
    start: (body: StartSyncRequest) => startRun('/sync/start', body),
    retryFailed: () => startRun('/sync/retry-failed'),
    cancel: () => request<void>('POST', '/sync/cancel'),
    status: () => request<SyncProgressSnapshot>('GET', '/sync/status'),
    history: (page: number, pageSize: number) =>
      request<PagedResponse<SyncRunDto>>('GET', '/sync/history', { query: { page, pageSize } }),
    run: (id: string) => request<SyncRunDetailDto>('GET', `/sync/history/${enc(id)}`),
  },

  settings: {
    get: () => request<SettingsDto>('GET', '/settings'),
    update: (body: UpdateSettingsRequest) => request<SettingsDto>('PUT', '/settings', { body }),
    credential: (source: MeetingSource) => request<CredentialStatusDto>('GET', `/settings/credentials/${sourcePath(source)}`),
    setCredential: (source: MeetingSource, body: SetCredentialRequest) =>
      request<CredentialStatusDto>('PUT', `/settings/credentials/${sourcePath(source)}`, { body }),
    deleteCredential: (source: MeetingSource) => request<CredentialStatusDto>('DELETE', `/settings/credentials/${sourcePath(source)}`),
    aiCredential: () => request<AiStatusDto>('GET', '/settings/credentials/ai'),
    setAiCredential: (body: SetCredentialRequest) => request<AiStatusDto>('PUT', '/settings/credentials/ai', { body }),
    deleteAiCredential: () => request<AiStatusDto>('DELETE', '/settings/credentials/ai'),
  },

  sources: {
    testConnection: (source: MeetingSource) => request<ConnectionTestDto>('POST', `/${sourcePath(source)}/test-connection`),
  },

  granola: {
    folders: () => request<GranolaFolder[]>('GET', '/granola/folders'),
  },

  exports: {
    tree: (root: ExportRootKind) => request<ExportTreeDto>('GET', '/exports/tree', { query: { root } }),
    file: (root: ExportRootKind, path: string) => request<FileContentDto>('GET', '/exports/file', { query: { root, path } }),
    open: (root: ExportRootKind, path?: string | null) => request<void>('POST', '/exports/open', { query: { root, path } }),
    validatePath: (body: ValidatePathRequest) => request<ValidatePathResultDto>('POST', '/exports/validate-path', { body }),
    migrateRoot: (body: MigrateRootRequest) => request<MigrateRootResultDto>('POST', '/exports/migrate-root', { body }),
    directories: (path?: string | null) => request<DirectoryListingDto>('GET', '/exports/directories', { query: { path } }),
  },
};

export type Api = typeof api;
