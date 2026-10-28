export const API_BASE = '/api';
export const CSRF_HEADER = 'X-GPI-Request';
export const API_TOKEN_STORAGE_KEY = 'gpi.apiToken';
/** Window event dispatched when the server answers 401 (remote deployment requires an API token). */
export const AUTH_REQUIRED_EVENT = 'gpi:auth-required';
/** Optional bearer token for remote deployments (Security:ApiToken). Stored per browser session only. */
export function getApiToken() {
    try {
        const t = window.sessionStorage.getItem(API_TOKEN_STORAGE_KEY);
        return t && t.trim() ? t.trim() : null;
    }
    catch {
        return null;
    }
}
export function setApiToken(token) {
    try {
        if (token && token.trim())
            window.sessionStorage.setItem(API_TOKEN_STORAGE_KEY, token.trim());
        else
            window.sessionStorage.removeItem(API_TOKEN_STORAGE_KEY);
    }
    catch {
        /* storage unavailable (private mode / blocked) — token simply is not persisted */
    }
}
/** Error thrown for any failed API call. Carries the parsed RFC 7807 problem details. */
export class ApiError extends Error {
    status;
    title;
    detail;
    category;
    correlationId;
    errors;
    /** Raw JSON body (e.g. a SyncStartResult on 409). */
    body;
    constructor(init) {
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
    fieldError(field) {
        const f = field.toLowerCase();
        const hit = this.errors.filter((e) => {
            const ef = e.field.toLowerCase().replace(/^\$\./, '');
            return ef === f || ef.endsWith('.' + f);
        });
        return hit.length ? hit.map((e) => e.message).join(' ') : undefined;
    }
    get isNetworkError() {
        return this.status === 0;
    }
}
function normalizeErrors(errors) {
    if (!errors)
        return [];
    if (Array.isArray(errors)) {
        return errors
            .filter((e) => e && typeof e === 'object')
            .map((e) => {
            const r = e;
            return { field: String(r.field ?? r.Field ?? ''), message: String(r.message ?? r.Message ?? '') };
        });
    }
    const out = [];
    for (const [field, msgs] of Object.entries(errors)) {
        for (const m of Array.isArray(msgs) ? msgs : [msgs])
            out.push({ field, message: String(m) });
    }
    return out;
}
/** Converts a failed fetch Response into an ApiError, parsing problem+json when present. */
export async function toApiError(res) {
    let body = undefined;
    const text = await res.text().catch(() => '');
    if (text) {
        try {
            body = JSON.parse(text);
        }
        catch {
            body = text;
        }
    }
    if (body && typeof body === 'object') {
        const p = body;
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
export function buildUrl(path, query) {
    let url = API_BASE + path;
    if (query) {
        const params = new URLSearchParams();
        for (const [k, v] of Object.entries(query)) {
            if (v === undefined || v === null || v === '')
                continue;
            params.append(k, String(v));
        }
        const qs = params.toString();
        if (qs)
            url += '?' + qs;
    }
    return url;
}
/** Low-level typed request. Adds the CSRF header + JSON content type to every non-GET request. */
export async function request(method, path, opts = {}) {
    const upper = method.toUpperCase();
    const headers = { Accept: 'application/json' };
    const isSafe = upper === 'GET' || upper === 'HEAD';
    if (!isSafe) {
        headers[CSRF_HEADER] = '1';
        headers['Content-Type'] = 'application/json';
    }
    const token = getApiToken();
    if (token)
        headers.Authorization = `Bearer ${token}`;
    let res;
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
    }
    catch (e) {
        if (e instanceof DOMException && e.name === 'AbortError')
            throw e;
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
            }
            catch {
                /* non-browser environment */
            }
        }
        throw err;
    }
    if (res.status === 204)
        return undefined;
    const text = await res.text();
    if (!text)
        return undefined;
    try {
        return JSON.parse(text);
    }
    catch {
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
const sourcePath = (source) => (source === 'Fireflies' ? 'fireflies' : 'granola');
/** Starts a background run; a 409 (already running) is surfaced as an ApiError whose detail is the server message. */
async function startRun(path, body) {
    return request('POST', path, { body });
}
export const api = {
    health: () => request('GET', '/health'),
    dashboard: () => request('GET', '/dashboard'),
    meetings: {
        list: (q, signal) => request('GET', '/meetings', { query: { ...q }, signal }),
        get: (id) => request('GET', `/meetings/${enc(id)}`),
        transcript: (id, page, pageSize) => request('GET', `/meetings/${enc(id)}/transcript`, { query: { page, pageSize } }),
        assign: (id, body) => request('POST', `/meetings/${enc(id)}/assign-project`, { body }),
        destinationPreview: (id, kind, projectId) => request('GET', `/meetings/${enc(id)}/destination-preview`, { query: { kind, projectId } }),
        reclassify: (id) => request('POST', `/meetings/${enc(id)}/reclassify`),
        retry: (id) => startRun(`/meetings/${enc(id)}/retry`),
        reExport: (id, body) => request('POST', `/meetings/${enc(id)}/export`, { body }),
        conflict: (id, recordId) => request('GET', `/meetings/${enc(id)}/exports/${enc(recordId)}/conflict`),
        resolveConflict: (id, recordId, resolution) => request('POST', `/meetings/${enc(id)}/exports/${enc(recordId)}/resolve`, { body: { resolution } }),
        openExport: (id, recordId, folder) => request('POST', `/meetings/${enc(id)}/exports/${enc(recordId)}/open`, { query: { folder } }),
    },
    unassigned: {
        list: (page, pageSize) => request('GET', '/unassigned', { query: { page, pageSize } }),
        assign: (id, body) => request('POST', `/unassigned/${enc(id)}/assign`, { body }),
        saveMapping: (id, body) => request('POST', `/unassigned/${enc(id)}/mappings`, { body }),
    },
    projects: {
        list: (search, includeArchived) => request('GET', '/projects', { query: { search, includeArchived } }),
        get: (id) => request('GET', `/projects/${enc(id)}`),
        create: (body) => request('POST', '/projects', { body }),
        update: (id, body) => request('PUT', `/projects/${enc(id)}`, { body }),
        archive: (id) => request('POST', `/projects/${enc(id)}/archive`),
        restore: (id) => request('POST', `/projects/${enc(id)}/restore`),
        addRule: (id, body) => request('POST', `/projects/${enc(id)}/rules`, { body }),
        testMapping: (id, body) => request('POST', `/projects/${enc(id)}/test-mapping`, { body }),
        applyMapping: (id, body) => startRun(`/projects/${enc(id)}/apply-mapping`, body),
        exportConfig: () => request('GET', '/projects/config/export'),
        importConfig: (body) => request('POST', '/projects/config/import', { body }),
    },
    clients: {
        list: () => request('GET', '/clients'),
        create: (body) => request('POST', '/clients', { body }),
        update: (id, body) => request('PUT', `/clients/${enc(id)}`, { body }),
        remove: (id) => request('DELETE', `/clients/${enc(id)}`),
    },
    sync: {
        start: (body) => startRun('/sync/start', body),
        retryFailed: () => startRun('/sync/retry-failed'),
        cancel: () => request('POST', '/sync/cancel'),
        status: () => request('GET', '/sync/status'),
        history: (page, pageSize) => request('GET', '/sync/history', { query: { page, pageSize } }),
        run: (id) => request('GET', `/sync/history/${enc(id)}`),
    },
    settings: {
        get: () => request('GET', '/settings'),
        update: (body) => request('PUT', '/settings', { body }),
        credential: (source) => request('GET', `/settings/credentials/${sourcePath(source)}`),
        setCredential: (source, body) => request('PUT', `/settings/credentials/${sourcePath(source)}`, { body }),
        deleteCredential: (source) => request('DELETE', `/settings/credentials/${sourcePath(source)}`),
        aiCredential: () => request('GET', '/settings/credentials/ai'),
        setAiCredential: (body) => request('PUT', '/settings/credentials/ai', { body }),
        deleteAiCredential: () => request('DELETE', '/settings/credentials/ai'),
    },
    sources: {
        testConnection: (source) => request('POST', `/${sourcePath(source)}/test-connection`),
    },
    granola: {
        folders: () => request('GET', '/granola/folders'),
    },
    exports: {
        tree: (root) => request('GET', '/exports/tree', { query: { root } }),
        file: (root, path) => request('GET', '/exports/file', { query: { root, path } }),
        open: (root, path) => request('POST', '/exports/open', { query: { root, path } }),
        validatePath: (body) => request('POST', '/exports/validate-path', { body }),
        migrateRoot: (body) => request('POST', '/exports/migrate-root', { body }),
        directories: (path) => request('GET', '/exports/directories', { query: { path } }),
    },
};
