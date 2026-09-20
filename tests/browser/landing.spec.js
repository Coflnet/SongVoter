import { test, expect } from '@playwright/test';

test('landing loads without authentication and joins directly from an invite', async ({ page }) => {
  const appRequests = [];
  page.on('request', request => {
    if (/\/api\/|flutter_bootstrap|main\.dart/.test(request.url())) appRequests.push(request.url());
  });
  await page.goto('/');
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Good music is a group effort.');
  await expect(page.getByRole('link', { name: 'Get the host app' })).toHaveAttribute('href', '/downloads/songvoter.apk');
  expect(appRequests).toEqual([]);
  for (const width of [390, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await page.screenshot({ path: `test-results/landing-${width}.png`, fullPage: true });
  }
  await page.getByText('Does everyone need an app?', { exact: true }).click();
  await expect(page.getByText('Only the host needs the Android app.', { exact: false })).toBeVisible();
  const invite = page.getByLabel('Party code or invite link');
  await invite.fill('https://unrelated.example/123456789abc');
  await page.getByRole('button', { name: 'Let me in' }).click();
  expect(await invite.evaluate(input => input.validity.customError)).toBeTruthy();
  await invite.fill('https://songvoter.party/join/ABCDEF123456');
  await page.getByRole('button', { name: 'Let me in' }).click();
  await expect(page).toHaveURL(/\/join\/abcdef123456$/);
  await expect(page.locator('flt-semantics-placeholder')).toBeAttached({ timeout: 45000 });
});
