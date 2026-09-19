import { test, expect, request } from '@playwright/test';
import { createHash, randomBytes } from 'node:crypto';
const apiUrl = process.env.API_URL || 'http://127.0.0.1:4207';
async function hostSession() {
  const api = await request.newContext({ baseURL: apiUrl });
  const secret = randomBytes(32).toString('hex');
  const challenge = await (await api.post('/api/auth/challenge', { data: { identityHash: createHash('sha256').update(secret).digest('hex') } })).json();
  let counter = 0;
  for (;; counter++) {
    const digest = createHash('sha256').update(`${challenge.id}:${secret}:${counter}`).digest();
    if (Array.from({length:challenge.difficulty},(_,bit)=> (digest[bit >> 3] & (128 >> (bit % 8))) === 0).every(Boolean)) break;
  }
  const session = await (await api.post('/api/auth/anonymous', {data:{challengeId:challenge.id, secret, counter}})).json();
  await api.dispose();
  return request.newContext({ baseURL: apiUrl, extraHTTPHeaders:{ Authorization:`Bearer ${session.token}` } });
}
async function semantics(page) {
  await page.locator('flt-semantics-placeholder').waitFor({ timeout: 45000 });
  await page.locator('flt-semantics-placeholder').evaluate(element => element.click());
}

test('QR invite → silent guest → find song → vote → host plays → reload retains favourites', async ({ page }) => {
  const host = await hostSession();
  const response = await host.post('/api/party', {data:{name:'Friday kitchen party', supportedPlatforms:['youtube','spotify']}});
  expect(response.ok()).toBeTruthy();
  const party = await response.json();
  try {
    expect(party.joinUrl).toBe(`https://songvoter.party/join/${party.code}`);
    await page.goto(`/join/${party.code}`);
    await semantics(page);
    await expect(page.getByText('Friday kitchen party', {exact:false})).toBeVisible({timeout:45000});
    await expect(page.getByText('Sign in', {exact:true})).toHaveCount(0);
    await expect(page.getByRole('button', {name:'Start the music'})).toHaveCount(0);
    const search = page.getByRole('textbox', {name:'Find a song or paste a link'});
    await search.click();
    await search.pressSequentially('Midnight City');
    await expect(search).toHaveValue('Midnight City');
    await page.getByRole('button', {name:'Search songs', exact:true}).click();
    const add = page.getByRole('button', {name:'Add Midnight City to favourites', exact:true});
    await expect(add).toBeVisible();
    await add.click();
    await expect(page.getByRole('button', {name:'Remove Midnight City from favourites'}).first()).toBeVisible();
    let queue = await (await host.get('/api/party')).json();
    expect(queue.queue).toHaveLength(1);
    expect(queue.queue[0].score).toBe(1);
    expect(queue.members).toBe(2);
    const next = await host.post('/api/party/next', {data:{version:queue.version}});
    expect(next.ok()).toBeTruthy();
    await expect(page.getByText(/NOW PLAYING/)).toBeVisible({timeout:15000});
    await page.screenshot({path:'test-results/guest-mobile.png'});
    await page.reload();
    await semantics(page);
    await expect(page.getByText(/NOW PLAYING/)).toBeVisible({timeout:15000});
    await page.getByRole('button', {name:'Your favourites',exact:true}).click();
    await expect(page.getByRole('button', {name:'Remove Midnight City from favourites'})).toBeVisible();
    queue = await (await host.get('/api/party')).json();
    expect(queue.members).toBe(2);
    // Desktop and landscape remain usable with the same persisted identity.
    await page.setViewportSize({width:1280,height:800});
    await page.screenshot({path:'test-results/guest-desktop.png'});
    await page.getByRole('button',{name:'Leave',exact:true}).click();
    await page.getByRole('button',{name:'Leave',exact:true}).last().click();
    await expect(page.getByRole('button',{name:'Join',exact:true})).toBeVisible();
  } finally {
    await host.post('/api/party/leave');
    await host.dispose();
  }
});

test('an invalid QR invite shows a recoverable join screen', async ({page}) => {
  await page.goto('/join/000000000000');
  await semantics(page);
  await expect(page.getByText('This invite has expired or the party has ended.',{exact:false})).toBeVisible({timeout:45000});
  await expect(page.getByRole('button',{name:'Join',exact:true})).toBeVisible();
});
