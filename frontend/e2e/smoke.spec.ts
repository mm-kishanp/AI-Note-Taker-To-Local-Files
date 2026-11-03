import { expect, test, type Page } from '@playwright/test';

// Read-only smoke test: every page renders without console errors or uncaught exceptions.
const shots = process.env.CHRONIQ_E2E_SCREENSHOTS;

const pages: { path: string; heading: RegExp }[] = [
  { path: '/', heading: /dashboard/i },
  { path: '/meetings', heading: /meetings/i },
  { path: '/projects', heading: /projects/i },
  { path: '/unassigned', heading: /unassigned|review/i },
  { path: '/sync-history', heading: /synchronization history/i },
  { path: '/exports', heading: /export browser/i },
  { path: '/settings', heading: /settings/i },
];

function trackErrors(page: Page) {
  const errors: string[] = [];
  page.on('pageerror', (e) => errors.push(`pageerror: ${e.message}`));
  page.on('console', (m) => {
    if (m.type() === 'error') errors.push(`console: ${m.text()}`);
  });
  return errors;
}

for (const p of pages) {
  test(`renders ${p.path}`, async ({ page }) => {
    const errors = trackErrors(page);
    await page.goto(p.path);
    await expect(page.getByRole('heading', { level: 2, name: p.heading }).first()).toBeVisible();
    await page.waitForLoadState('networkidle');
    if (shots) await page.screenshot({ path: `${shots}/${p.path === '/' ? 'dashboard' : p.path.slice(1)}.png`, fullPage: true });
    expect(errors).toEqual([]);
  });
}

test('meeting detail opens from the meetings list', async ({ page }) => {
  const errors = trackErrors(page);
  await page.goto('/meetings');
  const firstRow = page.locator('tbody tr').first();
  await firstRow.waitFor({ timeout: 10_000 }).catch(() => {});
  test.skip((await firstRow.count()) === 0, 'No meetings synced yet');
  await firstRow.click();
  await expect(page).toHaveURL(/\/meetings\/[0-9a-f-]{36}/);
  await expect(page.getByText(/classification/i).first()).toBeVisible();
  await page.waitForLoadState('networkidle');
  if (shots) await page.screenshot({ path: `${shots}/meeting-detail.png`, fullPage: true });
  expect(errors).toEqual([]);
});

test('settings tabs render', async ({ page }) => {
  const errors = trackErrors(page);
  await page.goto('/settings');
  for (const tab of ['Synchronization', 'Export', 'Classification', 'Logging']) {
    await page.getByRole('tab', { name: tab }).click();
    await expect(page.getByRole('heading', { level: 4, name: tab }).first()).toBeVisible();
    if (shots) await page.screenshot({ path: `${shots}/settings-${tab.toLowerCase()}.png`, fullPage: true });
  }
  expect(errors).toEqual([]);
});
