import { MantineProvider } from '@mantine/core';
import { ModalsProvider } from '@mantine/modals';
import { Notifications } from '@mantine/notifications';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { vi } from 'vitest';
import { theme } from '../providers';

export interface RecordedCall {
  method: string;
  url: URL;
  headers: Record<string, string>;
  body: unknown;
}

type Handler = (call: RecordedCall) => { status?: number; body?: unknown } | undefined;

/**
 * Replaces global fetch with a router: handlers are tried in order and the first one returning a
 * response wins. Every call is recorded for assertions. Unhandled calls return 404 problem details.
 */
export function mockFetch(handlers: Handler[]) {
  const calls: RecordedCall[] = [];
  const spy = vi.spyOn(globalThis, 'fetch').mockImplementation(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(String(input), 'http://localhost');
    const call: RecordedCall = {
      method: (init?.method ?? 'GET').toUpperCase(),
      url,
      headers: Object.fromEntries(Object.entries((init?.headers ?? {}) as Record<string, string>)),
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
export const route =
  (methodAndPath: string, body: unknown | ((c: RecordedCall) => unknown), status = 200): Handler =>
  (call) => {
    const [method, path] = methodAndPath.split(' ');
    if (call.method !== method || call.url.pathname !== path) return undefined;
    return { status, body: typeof body === 'function' ? (body as (c: RecordedCall) => unknown)(call) : body };
  };

export function renderWithProviders(ui: ReactElement, { path = '/', initialEntry = '/' }: { path?: string; initialEntry?: string } = {}) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <MantineProvider theme={theme}>
        <ModalsProvider>
          <Notifications />
          <MemoryRouter initialEntries={[initialEntry]}>
            <Routes>
              <Route path={path} element={ui} />
            </Routes>
          </MemoryRouter>
        </ModalsProvider>
      </MantineProvider>
    </QueryClientProvider>,
  );
}

export const paged = <T,>(items: T[], total = items.length) => ({ items, page: 1, pageSize: 25, total });
