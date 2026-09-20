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
  let guestToken;
  try {
    const term = process.env.E2E_SONG_URL || 'Midnight City';
    let title = 'Midnight City';
    if (process.env.E2E_SONG_URL) {
      const imported = await host.post('/api/songs/import', {data:{url:term}});
      expect(imported.ok()).toBeTruthy();
      title = (await imported.json()).title;
    }
    page.on('response', async response => {
      if (response.url().endsWith('/api/auth/anonymous') && response.ok()) guestToken = (await response.json()).token;
    });
    const response = await host.post('/api/party', {data:{name:'Friday kitchen party', supportedPlatforms:['youtube','spotify']}});
    expect(response.ok()).toBeTruthy();
    const party = await response.json();
    expect(party.joinUrl).toBe(`https://songvoter.party/join/${party.code}`);
    await page.goto(`/join/${party.code}`);
    await semantics(page);
    await expect(page.getByText('Friday kitchen party', {exact:false})).toBeVisible({timeout:45000});
    await expect(page.getByText('Sign in', {exact:true})).toHaveCount(0);
    await expect(page.getByRole('button', {name:'Start the music'})).toHaveCount(0);
    const search = page.getByRole('textbox', {name:'Find a song or paste a link'});
    await search.click();
    await search.pressSequentially(term);
    await expect(search).toHaveValue(term);
    await page.getByRole('button', {name:'Search songs', exact:true}).click();
    const add = page.getByRole('button', {name:`Add ${title} to favourites`, exact:true});
    await expect(add).toBeVisible();
    await add.click();
    await expect(page.getByRole('button', {name:`Remove ${title} from favourites`}).first()).toBeVisible();
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
    await expect(page.getByRole('button', {name:`Remove ${title} from favourites`})).toBeVisible();
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
    if (guestToken) await host.delete('/api/user', {headers:{Authorization:`Bearer ${guestToken}`}});
    await host.delete('/api/user');
    await host.dispose();
  }
});

test('an invalid QR invite shows a recoverable join screen', async ({page}) => {
  await page.goto('/join/000000000000');
  await semantics(page);
  await expect(page.getByText('This invite has expired or the party has ended.',{exact:false})).toBeVisible({timeout:45000});
  await expect(page.getByRole('button',{name:'Join',exact:true})).toBeVisible();
});

for (const provider of ['youtube', 'spotify']) {
  test(`${provider} OAuth return keeps the QR guest profile and imports a selected list`, async ({page}) => {
    const host = await hostSession();
    let guestToken, proof, connected = false;
    const state = `browser-${provider}-state`;
    const name = provider === 'youtube' ? 'YouTube' : 'Spotify';
    try {
      const song = await (await host.post('/api/songs/import', {data:{url:process.env.E2E_SONG_URL || 'https://youtu.be/dX3k_QDnzHE'}})).json();
      const party = await (await host.post('/api/party', {data:{name:'Playlist import party', supportedPlatforms:['youtube','spotify']}})).json();
      page.on('response', async response => {
        if (response.url().endsWith('/api/auth/anonymous') && response.ok()) guestToken = (await response.json()).token;
      });
      // Deterministic provider boundary. Profile, party, favourites and queue still use the real API.
      // Separate backend integration tests exercise the OAuth exchange, proof binding and provider endpoints.
      await page.route(`**/api/import/${provider}/**`, async route => {
        const request = route.request();
        const path = new URL(request.url()).pathname;
        if (path.endsWith('/lists')) return route.fulfill({json:{connected,available:true,lists:connected ? [{id:'liked',name:'Liked songs'},{id:'saved-list',name:'Kitchen favourites'}] : [],next:null}});
        if (path.endsWith('/connect')) {
          const data = request.postDataJSON();
          proof = data.proof;
          expect(proof).toMatch(/^[a-f0-9]{64}$/);
          expect(data.native).toBe(false);
          return route.fulfill({json:{state,url:`https://provider.example/authorize?state=${state}`}});
        }
        if (path.endsWith('/complete')) {
          expect(request.postDataJSON()).toEqual({state,proof});
          expect(request.headers().authorization).toBe(`Bearer ${guestToken}`);
          connected = true;
          return route.fulfill({status:204});
        }
        return route.abort();
      });
      await page.route('https://provider.example/**', route => route.fulfill({contentType:'text/html', body:`<script>location.replace(${JSON.stringify((process.env.WEB_URL || 'http://127.0.0.1:4307') + `/app?import=${provider}&state=${state}&lang=en`)});</script>`}));
      await page.route(`**/api/import/${provider}`, async route => {
        expect(route.request().postDataJSON()).toEqual({listId:'saved-list'});
        const result = await host.post('/api/party/add', {headers:{Authorization:`Bearer ${guestToken}`},data:[song.id]});
        expect(result.ok()).toBeTruthy();
        await route.fulfill({json:{added:1,total:1,limit:30,limitReached:false,moreAvailable:false}});
      });
      await page.goto(`/join/${party.code}`);
      await semantics(page);
      await expect(page.getByRole('button',{name,exact:true})).toBeVisible({timeout:45000});
      await page.getByRole('button',{name,exact:true}).click();
      await expect(page).toHaveURL(new RegExp(`/app\\?import=${provider}`));
      await semantics(page);
      await expect(page.getByText('Kitchen favourites',{exact:false})).toBeVisible({timeout:45000});
      await page.getByText('Kitchen favourites',{exact:false}).click();
      await expect(page.getByRole('button',{name:'Use my favourites',exact:true})).toBeVisible();
      let queue = await (await host.get('/api/party')).json();
      expect(queue.members).toBe(2);
      expect(queue.queue).toHaveLength(1);
      expect(queue.queue[0].score).toBe(1);
      await page.getByRole('button',{name:'Use my favourites',exact:true}).click();
      queue = await (await host.get('/api/party')).json();
      expect(queue.queue).toHaveLength(1);
      await page.reload();
      await semantics(page);
      await expect(page.getByRole('button',{name:'Use my favourites',exact:true})).toBeVisible({timeout:45000});
      await expect(page.getByText('Kitchen favourites',{exact:false})).toHaveCount(0);
    } finally {
      if (guestToken) await host.delete('/api/user',{headers:{Authorization:`Bearer ${guestToken}`}});
      await host.delete('/api/user');
      await host.dispose();
    }
  });
}

test('live YouTube playlist imports after QR join and persists without searching', async ({page}) => {
  test.skip(!process.env.E2E_PLAYLIST_URL, 'Set E2E_PLAYLIST_URL to verify the real YouTube catalogue.');
  const host = await hostSession();
  let guestToken;
  try {
    page.on('response', async response => {
      if (response.url().endsWith('/api/auth/anonymous') && response.ok()) guestToken = (await response.json()).token;
    });
    const party = await (await host.post('/api/party',{data:{name:'Live playlist import',supportedPlatforms:['youtube']}})).json();
    await page.goto(`/join/${party.code}?lang=de`);
    await semantics(page);
    await page.getByRole('button',{name:'YouTube-Playlistlink verwenden',exact:true}).click({timeout:45000});
    await page.getByRole('textbox',{name:'Playlistlink',exact:true}).fill(process.env.E2E_PLAYLIST_URL);
    const imported = page.waitForResponse(response => response.url().endsWith('/api/import/youtube') && response.request().method() === 'POST');
    await page.getByRole('button',{name:'Playlist importieren',exact:true}).click();
    const result = await imported;
    expect(result.ok(), await result.text()).toBeTruthy();
    const data = await result.json();
    expect(data.added).toBeGreaterThan(1);
    expect(data.total).toBeLessThanOrEqual(30);
    await expect(page.getByRole('button',{name:'Meine Favoriten nutzen',exact:true})).toBeVisible();
    let queue = await (await host.get('/api/party')).json();
    expect(queue.queue).toHaveLength(data.total);
    expect(queue.members).toBe(2);
    expect(queue.queue.reduce((sum,song) => sum + song.score,0)).toBeLessThan(1.02);
    await page.reload();
    await semantics(page);
    await page.getByRole('button',{name:'Meine Favoriten nutzen',exact:true}).click({timeout:45000});
    queue = await (await host.get('/api/party')).json();
    expect(queue.queue).toHaveLength(data.total);
    await page.screenshot({path:'test-results/live-german-playlist-import.png'});
  } finally {
    if (guestToken) await host.delete('/api/user',{headers:{Authorization:`Bearer ${guestToken}`}});
    await host.delete('/api/user');
    await host.dispose();
  }
});
