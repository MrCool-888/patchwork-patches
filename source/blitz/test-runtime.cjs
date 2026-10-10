const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const assert = require('node:assert/strict');
const { EventEmitter } = require('node:events');
const { readArchive, hash } = require('./asar-reader.cjs');
const known = 'https://blitzapp.gg/v3.0.8-ota.0/lol';
const notices = [];
const context = vm.createContext({ URL, Symbol, Set, Map, log: { warn: (...args) => notices.push(args) } });
vm.runInContext(fs.readFileSync(path.join(__dirname, 'desktop-cleanup.js'), 'utf8'), context);
const install = context.patchworkInstallDesktopCleanup;
const session = { registrations: 0, webRequest: { onBeforeRequest(filter, callback) {
  session.registrations++; session.filter = filter; session.callback = callback;
} } };
let id = 0;
class Contents extends EventEmitter {
  constructor(url = known) { super(); this.id = ++id; this.session = session; this.url = url; this.dead = false; this.styles = new Map(); this.insertions = 0;
    this.debugger = new EventEmitter(); this.debugger.attached = false;
    this.debugger.isAttached = () => this.debugger.attached;
    this.debugger.attach = () => { this.debugger.attached = true; };
    this.debugger.detach = () => { this.debugger.attached = false; this.debugger.emit('detach', {}, 'test'); };
    this.debugger.sendCommand = async (command, options) => { assert.equal(command, 'Network.setCacheDisabled'); this.cacheDisabled = options.cacheDisabled; };
  }
  isDestroyed() { return this.dead; }
  getURL() { return this.url; }
  async loadURL(value) { this.url = value; this.emit('did-start-navigation', {}, value, false, true); this.styles.clear(); this.emit('did-finish-load'); }
  reload() { this.reloaded = 'normal'; }
  reloadIgnoringCache() { this.reloaded = 'uncached'; }
  async insertCSS(css, options) { assert.equal(options.cssOrigin, 'user'); const key = 'css-' + ++this.insertions; this.styles.set(key, css); return key; }
  async removeInsertedCSS(key) { this.styles.delete(key); }
  destroy() { this.dead = true; this.emit('destroyed'); }
}
function request(wc, url) {
  let calls = 0, result;
  session.callback({ webContentsId: wc.id, url }, value => { calls++; result = value.cancel; });
  assert.equal(calls, 1); return result;
}
const flush = () => new Promise(resolve => setImmediate(resolve));
function walk(node, prefix = '') {
  const result = new Map();
  for (const [name, item] of Object.entries(node.files)) {
    const entry = prefix + name;
    if (item.files) for (const [key, value] of walk(item, entry + '/')) result.set(key, value);
    else result.set(entry, item);
  }
  return result;
}
function verifyArchives() {
  const installed = readArchive(process.argv[3] || path.join(process.env.LOCALAPPDATA, 'Programs/Blitz/resources/app.asar'));
  assert.equal(hash(installed.bytes), '44132b54235250bc4db85c09c80a247ce20d4fac95476bb10244bb0ecf344193');
  const originalEntries = walk(installed.header);
  for (const selection of ['both', 'clean', 'compact']) {
    const changed = readArchive(path.join(process.argv[2] || path.join(__dirname, '../blitz-integration/artifacts'), selection + '.asar'));
    const entries = walk(changed.header);
    assert.deepEqual([...entries.keys()].sort(), [...originalEntries.keys()].sort());
    for (const [entry, metadata] of originalEntries) {
      if (metadata.link || metadata.unpacked) assert.deepEqual(entries.get(entry), metadata);
      else if (!['src/createWindow.js', 'src/autoUpdater/index.js', 'src/blitz-entry.js', 'src/ota.js'].includes(entry)) {
        assert.equal(hash(installed.member(entry)), hash(changed.member(entry)), 'Untouched entry changed: ' + entry);
        const { offset: beforeOffset, ...before } = metadata;
        const { offset: afterOffset, ...after } = entries.get(entry);
        assert.deepEqual(after, before, 'Untouched metadata changed: ' + entry);
      }
    }
    const member = changed.member('src/createWindow.js');
    const integrity = entries.get('src/createWindow.js').integrity;
    assert.equal(integrity.hash, hash(member));
    assert.equal(integrity.blocks.length, Math.ceil(member.length / integrity.blockSize));
    for (let n = 0; n < integrity.blocks.length; n++) assert.equal(integrity.blocks[n], hash(member.subarray(n * integrity.blockSize, (n + 1) * integrity.blockSize)));
    const text = member.toString('utf8'); new vm.Script(text, { filename: selection + '-createWindow.js' });
    assert.equal(text.includes('function patchworkInstallDesktopCleanup'), selection !== 'compact');
    assert.equal(text.includes('const DEFAULT_MIN_WIDTH = 940;'), selection !== 'clean');
  }
  console.log('PASS actual archive: all 3051 entries preserved, metadata/integrity valid, three selections and JavaScript syntax verified');
}
function verifyCompact() {
  const text = readArchive(path.join(process.argv[2], 'compact.asar')).member('src/createWindow.js').toString();
  const constants = text.match(/const DEFAULT_MIN_WIDTH = \d+;/)[0] + text.match(/const DEFAULT_HEIGHT = \d+;/)[0];
  const startup = text.match(/  MIN_WIDTH = DEFAULT_MIN_WIDTH;\n  MIN_HEIGHT = DEFAULT_HEIGHT;/)[0];
  const minimum = text.match(/  windows\.client\.setMinimumSize\([\s\S]*?\n  \);/)[0];
  for (const account of ['logged-out', 'free', 'premium']) for (const [width, height, expected] of [[1920, 1080, [940, 500]], [800, 600, [750, 500]], [300, 300, [250, 250]]]) {
    let actual;
    const ctx = { MIN_WIDTH: 1075, MIN_HEIGHT: 850, SCREEN_MARGIN: 50, displaySize: { width, height },
      account, fetchUser() { throw new Error('Compact mode queried account roles'); },
      windows: { client: { setMinimumSize: (...size) => { actual = size; } } } };
    vm.runInNewContext(constants + startup + minimum, ctx);
    assert.deepEqual(actual, expected);
  }
  console.log('PASS compact mode: logged-out/free/premium fixtures and three screen sizes; stored large minimums ignored');
}
async function verifyHook() {
  const wc = new Contents(); install({ webContents: wc }, known);
  await wc.loadURL(known); assert.equal(wc.cacheDisabled, true);
  wc.reload(); await flush(); assert.equal(wc.reloaded, 'uncached');
  await wc.loadURL('https://blitzapp.gg/v9.0.0/lol'); assert.equal(wc.cacheDisabled, false);
  wc.reload(); assert.equal(wc.reloaded, 'normal');
  await wc.loadURL(known); assert.equal(wc.cacheDisabled, true);
  const ad = 'https://dn0qt3r0xannq.cloudfront.net/blitz-ONuZ1Ty9qx/blitz-app/prebid-load.js';
  const primis = 'https://live.primis.tech/live/liveView.php?s=123';
  assert.equal(request(wc, ad), true); assert.equal(request(wc, primis), true);
  for (const allowed of ['https://auth.blitz.gg/graphql', 'https://utils.iesdev.com/static/json/app/ota', 'https://dn0qt3r0xannq.cloudfront.net/another-app/prebid-load.js', 'https://live.primis.tech/not-ads', 'https://127.0.0.1:5000/game', 'invalid']) assert.equal(request(wc, allowed), false);
  const game = new Contents(); assert.equal(request(game, ad), false);
  wc.emit('did-finish-load'); await flush(); assert.equal(wc.styles.size, 1);
  const css = [...wc.styles.values()][0];
  wc.emit('did-finish-load'); await flush(); assert.equal(wc.styles.size, 1);
  wc.url = known + '/champions'; wc.emit('did-navigate-in-page', {}, wc.url, true); await flush(); assert.equal(wc.styles.size, 1);
  wc.url = 'https://blitzapp.gg/v3.0.9/lol'; wc.emit('did-navigate-in-page', {}, wc.url, true); await flush();
  assert.equal(wc.styles.size, 0); assert.equal(request(wc, ad), false); assert.ok(notices.length > 0);
  wc.url = known; wc.emit('did-start-navigation', {}, known, false, true); wc.styles.clear(); wc.emit('did-finish-load'); await flush(); assert.equal(wc.styles.size, 1);
  wc.destroy(); assert.equal(request(wc, ad), false);
  const recreated = new Contents(); install({ webContents: recreated }, known); recreated.emit('did-finish-load'); await flush();
  assert.equal(session.registrations, 1); assert.equal(request(recreated, ad), true); assert.equal(recreated.styles.size, 1);
  const foreign = new Contents('https://evil.example/v3.0.8-ota.0'); install({ webContents: foreign }, foreign.url); foreign.emit('did-finish-load'); await flush();
  assert.equal(request(foreign, ad), false); assert.equal(foreign.styles.size, 0);
  const overlay = new Contents(known + '?overlay-route=lol'); install({ webContents: overlay }, overlay.url); assert.equal(request(overlay, ad), false);
  recreated.emit('did-start-navigation', {}, 'https://blitzapp.gg/v3.0.9/lol', false, true); assert.equal(request(recreated, ad), false);
  console.log('PASS desktop hook: loader blocking, allowed APIs, unrelated views, unknown versions, reload, SPA navigation, recreation and one session listener');
  return css;
}
async function verifyLayout(css) {
  const playwright = require(process.env.PATCHWORK_PLAYWRIGHT_MODULE || 'playwright');
  const browser = await playwright.chromium.launch({ executablePath: process.env.PATCHWORK_CHROMIUM_EXECUTABLE || undefined, headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1100, height: 800 } });
    const markup = `<style>.blitz-app{display:grid;grid-template-columns:minmax(0,1fr) var(--right-rail-width);gap:var(--rail-gap)}.ad{width:308px;height:250px}.promo{height:80px}#content{height:100px;background:#ccc}</style>
      <div class="blitz-app" style="--right-rail-width:308px;--rail-gap:16px"><article id="content"><button id="settings">Settings</button><button id="login">Login</button></article><aside id="rail" class="🤑-wrapper ad"><div class="🤑-column"><div class="🤑-rectangle"><button id="ad-click">Ad</button></div></div></aside></div>
      <div id="leaderboard" class="🤑-leaderboard ad"></div><div id="video" data-primis-placement-id="123" class="ad"></div>
      <ul><li id="upgrade"><a class="get-premium-btn" href="/premium">Upgrade</a></li></ul><div id="banner" class="latest-feature-container svelte-1lr1kw5 promo">Promotion</div><button id="blitz3" class="blitz3-promo svelte-lj7rjc">Promotion</button>
      <section id="news" class="latest-feature-container">News</section><div id="billing" class="premium-purchase-modal">Account billing</div>
      <section id="history"><button id="history-row">Open match</button></section>
      <div id="history-promo-space" class="leaderboard-container" style="display:flex;padding-top:20px;min-height:90px"><a id="history-promo" class="button leaderboard-promo svelte-x9c7ob" href="/premium?ref=leaderboard-lol"><span>Go ad free</span></a><div class="🤑-leaderboard"></div></div>
      <section id="scores"><button id="score-details">Match scores</button></section>
      <div id="match-promo-space" class="leaderboard-container" style="display:flex;padding-top:20px;min-height:90px"><a id="match-promo" class="button leaderboard-promo svelte-x9c7ob" href="/premium?ref=leaderboard-tft"><span>Go ad free</span></a></div>
      <a id="standalone-promo" class="leaderboard-promo svelte-x9c7ob" href="/premium?ref=leaderboard-valorant">Go ad free</a>
      <div id="ad-only-space" class="leaderboard-container" style="min-height:90px"><div class="🤑-leaderboard"></div></div>
      <section id="rankings" class="leaderboard-container"><button id="ranking-row">Player rankings</button></section>
      <a id="unrelated-promo" class="leaderboard-promo" href="/leaderboards">Tournament leaderboard</a>`;
    for (const width of [800, 1500]) {
      await page.setViewportSize({ width, height: 800 });
      await page.setContent(markup); const before = await page.locator('#content').boundingBox();
      await page.addStyleTag({ content: css }); const after = await page.locator('#content').boundingBox();
      assert.ok(after.width > before.width + 300, 'Ad column was not reclaimed');
      for (const name of ['rail', 'leaderboard', 'video', 'upgrade', 'banner', 'blitz3', 'history-promo-space', 'history-promo', 'match-promo-space', 'match-promo', 'standalone-promo', 'ad-only-space']) {
        assert.equal(await page.locator('#' + name).isVisible(), false, name + ' still visible');
        assert.equal(await page.locator('#' + name).boundingBox(), null);
      }
      for (const name of ['settings', 'login', 'news', 'billing', 'history', 'history-row', 'scores', 'score-details', 'rankings', 'ranking-row', 'unrelated-promo']) assert.equal(await page.locator('#' + name).isVisible(), true);
      await page.evaluate(() => { const dynamic = document.createElement('div'); dynamic.id = 'dynamic'; dynamic.className = '🤑-rectangle'; dynamic.innerHTML = '<button>Ad</button>'; document.body.appendChild(dynamic); });
      assert.equal(await page.locator('#dynamic').isVisible(), false);
      await page.evaluate(() => { const dynamic = document.createElement('div'); dynamic.id = 'dynamic-promo-space'; dynamic.className = 'leaderboard-container'; dynamic.innerHTML = '<a class="leaderboard-promo svelte-x9c7ob" href="/premium?ref=leaderboard-lol">Go ad free</a>'; document.body.appendChild(dynamic); });
      assert.equal(await page.locator('#dynamic-promo-space').boundingBox(), null);
      await page.locator('#settings').click(); await page.locator('#login').click();
      await page.locator('#history-row').click(); await page.locator('#score-details').click(); await page.locator('#ranking-row').click();
    }
    console.log('PASS Chromium layout fixtures: Go ad free banners and reserved space removed at 800/1500px; dynamic promotions collapsed, history/score/ranking controls retained, no hidden hit targets');
  } finally { await browser.close(); }
}
(async () => { verifyArchives(); verifyCompact(); await verifyLayout(await verifyHook()); })().catch(error => { console.error(error); process.exitCode = 1; });
