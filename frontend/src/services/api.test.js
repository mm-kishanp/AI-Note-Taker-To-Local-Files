import { describe, expect, it, vi } from 'vitest';
import { mockFetch, route } from '../test/utils';
import { api, ApiError, AUTH_REQUIRED_EVENT, CSRF_HEADER, setApiToken } from './api';
describe('api client', () => {
    it('adds the CSRF header and JSON body to state-changing requests only', async () => {
        const { calls } = mockFetch([
            route('GET /api/health', { status: 'Healthy' }),
            route('POST /api/sync/start', { started: true, runId: 'r1', message: 'ok' }, 202),
        ]);
        await api.health();
        await api.sync.start({ mode: 'Full' });
        expect(calls[0].headers[CSRF_HEADER]).toBeUndefined();
        expect(calls[1].headers[CSRF_HEADER]).toBe('1');
        expect(calls[1].headers['Content-Type']).toBe('application/json');
        expect(calls[1].body).toEqual({ mode: 'Full' });
    });
    it('parses RFC 7807 problem details with field errors', async () => {
        mockFetch([
            route('PUT /api/settings', {
                title: 'Validation failed', status: 400, detail: 'Interval must be between 5 and 1440 minutes.', category: 'Configuration',
                correlationId: 'abc', errors: [{ field: 'sync.intervalMinutes', message: 'Interval must be between 5 and 1440 minutes.' }],
            }, 400),
        ]);
        const err = await api.settings.update({ export: null, sync: null, classification: null, logging: null }).catch((e) => e);
        expect(err).toBeInstanceOf(ApiError);
        expect(err.status).toBe(400);
        expect(err.title).toBe('Validation failed');
        expect(err.correlationId).toBe('abc');
        expect(err.fieldError('intervalMinutes')).toContain('between 5 and 1440');
    });
    it('sends the API token as a bearer header when one is stored', async () => {
        const { calls } = mockFetch([route('GET /api/dashboard', {})]);
        await api.dashboard();
        expect(calls[0].headers.Authorization).toBeUndefined();
        setApiToken('remote-token');
        await api.dashboard();
        expect(calls[1].headers.Authorization).toBe('Bearer remote-token');
    });
    it('announces a 401 so the UI can ask for the API token', async () => {
        mockFetch([route('GET /api/dashboard', { title: 'A valid API token is required.', status: 401 }, 401)]);
        const listener = vi.fn();
        window.addEventListener(AUTH_REQUIRED_EVENT, listener);
        await expect(api.dashboard()).rejects.toBeInstanceOf(ApiError);
        window.removeEventListener(AUTH_REQUIRED_EVENT, listener);
        expect(listener).toHaveBeenCalledTimes(1);
    });
    it('reports an unreachable backend as a network error', async () => {
        vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'));
        const err = await api.health().catch((e) => e);
        expect(err).toBeInstanceOf(ApiError);
        expect(err.isNetworkError).toBe(true);
    });
});
