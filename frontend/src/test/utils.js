import { jsx as _jsx, jsxs as _jsxs } from "react/jsx-runtime";
import { MantineProvider } from '@mantine/core';
import { ModalsProvider } from '@mantine/modals';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { theme } from '../providers';
/**
 * Replaces global fetch with a router: handlers are tried in order and the first one returning a
 * response wins. Every call is recorded for assertions. Unhandled calls return 404 problem details.
 */
export function mockFetch(handlers) {
    const calls = [];
    const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
        const url = new URL(String(input), 'http://localhost');
        const call = {
            method: (init?.method ?? 'GET').toUpperCase(),
            url,
            headers: Object.fromEntries(Object.entries((init?.headers ?? {}))),
            body: init?.body ? JSON.parse(String(init.body)) : undefined,
        };
        calls.push(call);
        for (const h of handlers) {
            const r = h(call);
            if (r) {
                const status = r.status ?? 200;
                return new Response(r.body === undefined ? null : JSON.stringify(r.body), {
                    status,
                    headers: { 'Content-Type': status >= 400 ? 'application/problem+json' : 'application/json' },
                });
            }
        }
        return new Response(JSON.stringify({ title: 'Not found (test)', status: 404 }), { status: 404 });
    });
    return { calls, spy };
}
/** Matches "GET /api/meetings" style routes (path only). */
export const route = (methodAndPath, body, status = 200) => (call) => {
    const [method, path] = methodAndPath.split(' ');
    if (call.method !== method || call.url.pathname !== path)
        return undefined;
    return { status, body: typeof body === 'function' ? body(call) : body };
};
export function renderWithProviders(ui, { path = '/', initialEntry = '/' } = {}) {
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
    return render(_jsx(QueryClientProvider, { client: client, children: _jsx(MantineProvider, { theme: theme, children: _jsxs(ModalsProvider, { children: [_jsx(Notifications, {}), _jsx(MemoryRouter, { initialEntries: [initialEntry], children: _jsx(Routes, { children: _jsx(Route, { path: path, element: ui }) }) })] }) }) }));
}
export const paged = (items, total = items.length) => ({ items, page: 1, pageSize: 25, total });
