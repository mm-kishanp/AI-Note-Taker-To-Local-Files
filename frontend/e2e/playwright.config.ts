import { defineConfig } from '@playwright/test';

// Runs against an already running app (API + built UI). Read-only: it only navigates and reads.
//   CHRONIQ_E2E_URL=http://127.0.0.1:5080 npm run e2e
export default defineConfig({
  testDir: '.',
  timeout: 30_000,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.CHRONIQ_E2E_URL ?? 'http://127.0.0.1:5080',
    viewport: { width: 1440, height: 900 },
    screenshot: 'only-on-failure',
  },
});
