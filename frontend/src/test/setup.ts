import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

// jsdom lacks a few browser APIs Mantine relies on.
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  // Plain function (not vi.fn) so vi.restoreAllMocks() between tests cannot strip it.
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
});

class ResizeObserverStub {
  observe() {}
  unobserve() {}
  disconnect() {}
}
window.ResizeObserver = window.ResizeObserver ?? (ResizeObserverStub as unknown as typeof ResizeObserver);
Element.prototype.scrollIntoView = Element.prototype.scrollIntoView ?? (() => {});

afterEach(() => {
  cleanup();
  vi.restoreAllMocks();
  try {
    window.sessionStorage.clear();
  } catch {
    /* ignore */
  }
});
