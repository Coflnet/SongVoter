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
  await expect(page).toHaveURL(/\/join\/abcdef123456\?lang=en$/);
  await expect(page.locator('flt-semantics-placeholder')).toBeAttached({ timeout: 45000 });
});

test('German browser gets localized landing and app, with a language switch', async ({ browser }) => {
  const context = await browser.newContext({ locale: 'de-DE', viewport: { width: 390, height: 844 } });
  const page = await context.newPage();
  try {
    await page.goto('/');
    await expect(page).toHaveURL(/\/de\/$/);
    await expect(page.locator('html')).toHaveAttribute('lang', 'de');
    await expect(page.getByRole('heading', {level: 1})).toHaveText('Gute Musik ist Teamwork.');
    await page.getByText('Ist SongVoter kostenlos?', {exact: true}).click();
    await expect(page.getByText('Ja. SongVoter ist kostenlos nutzbar.', {exact: true})).toBeVisible();
    await page.getByRole('link', {name: 'So funktioniert SongVoter'}).click();
    await expect(page).toHaveURL(/\/de\/so-funktionierts\/$/);
    await expect(page.getByRole('heading', {level: 1})).toHaveText('Scannen. Importieren. Gemeinsam DJ sein.');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBeTruthy();
    await page.screenshot({path: 'test-results/german-guide.png', fullPage: true});
    await page.getByRole('link', {name: 'English', exact: true}).click();
    await expect(page.getByRole('heading', {level: 1})).toHaveText('Scan. Import. Let everyone DJ.');
    await page.goto('/app?lang=de');
    await page.locator('flt-semantics-placeholder').waitFor({timeout:45000});
    await page.locator('flt-semantics-placeholder').evaluate(element => element.click());
    await expect(page.getByRole('button', {name: 'Beitreten', exact:true})).toBeVisible({timeout:45000});
    await expect(page.getByRole('button', {name:'YouTube',exact:true})).toBeVisible();
    await expect(page.getByRole('button', {name:'Spotify',exact:true})).toBeVisible();
    await page.screenshot({path:'test-results/german-app.png'});
    await page.getByRole('button', {name:'YouTube-Playlistlink verwenden',exact:true}).click();
    await page.getByRole('textbox', {name:'Playlistlink',exact:true}).fill('https://unrelated.example/playlist');
    await page.getByRole('button', {name:'Playlist importieren',exact:true}).click();
    await expect(page.getByText('Wähle eine Playlist oder füge ihren vollständigen Link ein.',{exact:true})).toBeVisible();
  } finally { await context.close(); }
});

test('how-to articles and language alternatives are indexable without JavaScript', async ({browser}) => {
  const context = await browser.newContext({javaScriptEnabled:false});
  const page = await context.newPage();
  try {
    for (const [path,language] of [['/how-it-works/','en'],['/de/so-funktionierts/','de']]) {
      const response = await page.goto(path);
      expect(response.status()).toBe(200);
      await expect(page.locator('html')).toHaveAttribute('lang',language);
      await expect(page.locator('main h2')).toHaveCount(7);
      await expect(page.locator('link[rel=canonical]')).toHaveAttribute('href',`https://songvoter.party${path}`);
      await expect(page.locator('link[hreflang=de]')).toHaveAttribute('href','https://songvoter.party/de/so-funktionierts/');
      expect(await page.locator('main').innerText()).not.toMatch(/paid boosts|bezahlte Boosts/i);
    }
  } finally { await context.close(); }
});
