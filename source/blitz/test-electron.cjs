// Run only in an isolated copy of Blitz's Electron runtime, with the accompanying hook file.
// Every HTTPS request is answered locally; no account or installed app is used.
const { app, BrowserWindow, BrowserView, session } = require('electron');
const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const https = require('node:https');
const output = process.argv.find(value => value.startsWith('--result='));
if (!output) throw new Error('Pass --result=ABSOLUTE_WORKSPACE_PATH');
const resultPath = output.slice('--result='.length);
fs.writeFileSync(resultPath, JSON.stringify({ result: 'STARTED', phase: 'main script' }));
const notices = [];
const hook = new Function('log', fs.readFileSync(path.join(__dirname, 'desktop-cleanup.js'), 'utf8') + '\nreturn patchworkInstallDesktopCleanup;')({ warn: (...args) => notices.push(args) });
const patchedModule = { exports: {} };
new Function('require', 'module', fs.readFileSync(path.join(__dirname, 'electronWindowHandlers.js'), 'utf8'))(name => {
  if (name === 'electron') return require('electron');
  if (name === 'npmlog') return { warn: (...args) => notices.push(args) };
  if (name === './native-utils') return {};
  throw Error('Unexpected fixture dependency: ' + name);
}, patchedModule);
let known, displayLoader, videoLoader, port;
const requests = [];
const privacyRequests = [];
const served = [];
const telemetryURLs = () => [
  `https://science.v2.iesdev.com:${port}/append`,
  `https://www.google-analytics.com:${port}/g/collect`,
  `https://www.googletagmanager.com:${port}/gtag/js?id=fixture`,
  `https://data.v2.iesdev.com:${port}/api/v1/query_objects/prod/bi/machine_segment?id=fixture`,
];
function markup() { return `<!doctype html><meta charset="utf-8"><style>
  .blitz-app{display:grid;grid-template-columns:minmax(0,1fr) var(--right-rail-width);gap:var(--rail-gap)}
  .ad{height:250px}.promo{height:80px}#content{height:100px}
  </style><div class="blitz-app" style="--right-rail-width:308px;--rail-gap:16px">
  <article id="content"><button id="login">Login</button><button id="settings">Settings</button></article>
  <aside id="rail" class="🤑-wrapper ad"><div class="🤑-column"><button>Ad</button></div></aside></div>
  <div id="video" data-primis-placement-id="123" class="ad"></div>
  <ul><li id="upgrade"><a class="get-premium-btn">Upgrade</a></li></ul>
  <div id="banner" class="latest-feature-container svelte-1lr1kw5 promo">Premium banner</div>
  <div id="news" class="latest-feature-container">News</div><div id="billing">Billing</div>
  <div id="history-promo-space" class="leaderboard-container" style="padding:20px;min-height:90px"><a id="history-promo" class="button leaderboard-promo svelte-x9c7ob">Go ad free</a></div>
  <div id="ad-only-space" class="leaderboard-container" style="min-height:90px"><div class="🤑-leaderboard"></div></div>
  <div id="rankings" class="leaderboard-container"><button id="ranking-row">Player rankings</button></div>
  <a id="unrelated-promo" class="leaderboard-promo">Tournament leaderboard</a>
  <script src="${displayLoader}"></script><script src="${videoLoader}"></script>
  <script>window.apiReady=Promise.all(['auth','games','updates'].map(x=>fetch('https://api.blitz.gg:${port}/'+x).then(r=>r.json()).then(()=>({name:x,ok:true}),e=>({name:x,ok:false,error:String(e)}))));
  window.telemetryReady=Promise.all(${JSON.stringify(telemetryURLs())}.map(url=>fetch(url,{method:'POST',body:'fixture'}).then(()=>true,()=>false)));
  window.addDynamic=()=>{const e=document.createElement('button');e.className='🤑-rectangle';e.id='dynamic';e.textContent='Dynamic ad';document.body.appendChild(e)};</script>`; }
const delay = ms => new Promise(resolve => setTimeout(resolve, ms));
async function poll(check) {
  for (let i = 0; i < 100; i++) { if (await check()) return; await delay(20); }
  throw new Error('Timed out waiting for injected CSS');
}
async function inspect(wc, clean) {
  await poll(() => wc.executeJavaScript(`getComputedStyle(document.querySelector('.blitz-app')).getPropertyValue('--right-rail-width').trim() === '${clean ? '0px' : '308px'}'`));
  const values = await wc.executeJavaScript(`(async()=>{
    await window.apiReady;window.addDynamic();
    const visible=id=>document.getElementById(id).getBoundingClientRect().height>0;
    return {apis:await window.apiReady,width:document.getElementById('content').getBoundingClientRect().width,
      hidden:['rail','video','upgrade','banner','dynamic','history-promo-space','history-promo','ad-only-space'].every(id=>!visible(id)),
      controls:['login','settings','news','billing','rankings','ranking-row','unrelated-promo'].every(visible),
      loaded:!!window.fixtureAdLoaded};})()`);
  assert.equal(values.hidden, clean); assert.equal(values.controls, true); assert.equal(values.loaded, !clean);
  assert.ok(values.apis.every(api => api.ok), JSON.stringify(values.apis));
  return values.width;
}
app.setPath('userData', path.join(path.dirname(resultPath), 'profile'));
app.commandLine.appendSwitch('disable-background-networking');
app.commandLine.appendSwitch('host-resolver-rules', 'MAP blitzapp.gg 127.0.0.1, MAP api.blitz.gg 127.0.0.1, MAP dn0qt3r0xannq.cloudfront.net 127.0.0.1, MAP live.primis.tech 127.0.0.1, MAP science.v2.iesdev.com 127.0.0.1, MAP www.google-analytics.com 127.0.0.1, MAP www.googletagmanager.com 127.0.0.1, MAP data.v2.iesdev.com 127.0.0.1');
app.whenReady().then(async () => {
  fs.writeFileSync(resultPath, JSON.stringify({ result: 'STARTED', phase: 'Electron ready' }));
  let registrations = 0, preservedHeaders = 0;
  const targetSession = session.fromPartition('patchwork-blitz-test');
  targetSession.webRequest.onErrorOccurred(details => notices.push(['request error', details.url, details.error]));
  await targetSession.setProxy({ mode: 'direct' });
  const cert = fs.readFileSync(path.join(__dirname, 'fixture-cert.pem'));
  // Trust only the test server's certificate, only in this isolated test partition.
  targetSession.setCertificateVerifyProc((request, callback) => {
    const matches = request.certificate.data.trim() === cert.toString().trim();
    if (!matches) notices.push(['fixture certificate mismatch', request.hostname]);
    callback(matches ? 0 : -3);
  });
  app.on('certificate-error', (event, contents, _url, _error, certificate, callback) => {
    const matches = contents.session === targetSession && certificate.data.trim() === cert.toString().trim();
    if (matches) event.preventDefault();
    callback(matches);
  });
  const server = https.createServer({ key: fs.readFileSync(path.join(__dirname, 'fixture-key.pem')), cert }, (request, response) => {
    const hostname = request.headers.host.split(':')[0];
    served.push(hostname + request.url);
    const script = hostname === 'dn0qt3r0xannq.cloudfront.net' || hostname === 'live.primis.tech';
    const api = hostname === 'api.blitz.gg';
    const telemetry = ['science.v2.iesdev.com', 'www.google-analytics.com', 'www.googletagmanager.com', 'data.v2.iesdev.com'].includes(hostname);
    response.writeHead(200, { 'Content-Type': script ? 'application/javascript' : api || telemetry ? 'application/json' : 'text/html; charset=utf-8',
      'Access-Control-Allow-Origin': '*', 'Access-Control-Allow-Headers': '*', 'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
      'Access-Control-Allow-Private-Network': 'true', 'Cache-Control': telemetry ? 'no-store' : 'public, max-age=3600' });
    response.end(script ? 'window.fixtureAdLoaded=true;' : api ? '{"ok":true}' : markup());
  });
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve)); port = server.address().port;
  known = `https://blitzapp.gg:${port}/v3.0.17/lol`;
  displayLoader = `https://dn0qt3r0xannq.cloudfront.net:${port}/blitz-ONuZ1Ty9qx/test/prebid-load.js`;
  videoLoader = `https://live.primis.tech:${port}/live/liveView.php?fixture=1`;
  const nativeFilter = targetSession.webRequest.onBeforeRequest.bind(targetSession.webRequest);
  targetSession.webRequest.onBeforeRequest = (filter, listener) => {
    registrations++;
    nativeFilter(filter, (details, callback) => listener(details, value => {
      requests.push({ id: details.webContentsId, url: details.url, cancelled: value.cancel }); callback(value);
    }));
  };
  targetSession.webRequest.onHeadersReceived((details, callback) => { preservedHeaders++; callback({}); });
  const window = new BrowserWindow({ show: false, width: 1360, height: 900 });
  function makeView(install = true) {
    const view = new BrowserView({ webPreferences: { session: targetSession, nodeIntegration: false, contextIsolation: true, sandbox: true, backgroundThrottling: false } });
    view.webContents.on('console-message', (_event, _level, message) => notices.push(['renderer', message]));
    window.setBrowserView(view); view.setBounds({ x: 0, y: 0, width: 1360, height: 900 });
    if (install) hook(view, known);
    return view;
  }
  // Seed actual memory/disk caches in an unrelated live view before installation.
  const seed = makeView(false); await seed.webContents.loadURL(known); await inspect(seed.webContents, false);
  assert.deepEqual(await seed.webContents.executeJavaScript('window.telemetryReady'), [true, true, true, true]);
  function installPrivacy(s) {
    const before = s.webRequest.onBeforeSendHeaders.bind(s.webRequest);
    s.webRequest.onBeforeSendHeaders = (filter, listener) => before(filter, (details, callback) => listener(details, value => {
      privacyRequests.push({ url: details.url, cancelled: value.cancel }); callback(value);
    }));
    const after = s.webRequest.onHeadersReceived.bind(s.webRequest);
    s.webRequest.onHeadersReceived = (filter, listener) => after(filter, (details, callback) => { preservedHeaders++; listener(details, callback); });
    patchedModule.exports.interceptRequests({ webContents: { session: s } });
  }
  installPrivacy(targetSession);
  let view = makeView(); let wc = view.webContents;
  await wc.loadURL(known); const wide = await inspect(wc, true);
  assert.deepEqual(await wc.executeJavaScript('window.telemetryReady'), [false, false, false, false]);
  for (const width of [800, 1500]) { view.setBounds({ x: 0, y: 0, width, height: 900 }); await inspect(wc, true); }
  view.setBounds({ x: 0, y: 0, width: 1360, height: 900 });
  assert.equal(requests.filter(x => x.id === wc.id && x.cancelled).length, 2);
  assert.ok(preservedHeaders > 0, 'Existing response handler did not run');
  const initialRequests = requests.length;
  const reloaded = new Promise(resolve => wc.once('did-finish-load', resolve)); wc.reload(); await reloaded;
  await inspect(wc, true); assert.equal(requests.slice(initialRequests).filter(x => x.cancelled).length, 2);
  await wc.executeJavaScript("history.pushState({}, '', '/v3.0.17/settings')"); await inspect(wc, true);
  const navigated = new Promise(resolve => wc.once('did-finish-load', resolve));
  await wc.executeJavaScript("location.href='/v3.0.17/game';true"); await navigated; await inspect(wc, true);
  await wc.loadURL(`https://blitzapp.gg:${port}/v9.0.0/lol`); const originalWidth = await inspect(wc, false);
  assert.ok(wide > originalWidth + 300, 'Native layout retained blank ad column');
  assert.ok(notices.some(x => x.join(' ').includes('unsupported frontend')));
  await wc.loadURL(known); await inspect(wc, true);
  const oldId = wc.id; wc.close(); view = makeView(); wc = view.webContents;
  await wc.loadURL(known); await inspect(wc, true); assert.equal(registrations, 1);
  assert.equal(targetSession[Symbol.for('patchwork.blitz.desktop-cleanup.v1')].views.has(oldId), false);
  const unrelated = makeView(false); await unrelated.webContents.loadURL(known); await inspect(unrelated.webContents, false);
  assert.deepEqual(await unrelated.webContents.executeJavaScript('window.telemetryReady'), [false, false, false, false]);
  assert.ok(requests.some(x => x.id === unrelated.webContents.id && !x.cancelled), 'Unrelated view was filtered');
  window.setMinimumSize(940, 500); assert.deepEqual(window.getMinimumSize(), [940, 500]);
  const result = { result: 'PASS', electron: process.versions.electron, chrome: process.versions.chrome, registrations,
    cancelled: requests.filter(x => x.cancelled).length, telemetryCancelled: privacyRequests.filter(x => x.cancelled).length, preservedHeaders,
    checks: ['native desktop request cancellation', 'existing response handler', 'startup with warm memory/disk cache', 'reload with warm fixture cache',
      'SPA and full navigation', 'unknown frontend original behavior', 'BrowserView recreation', 'unrelated view',
      'collapsed CSS and reclaimed column at 800/1500px', 'Go ad free banners and ad-only wrappers', 'dynamic slots', '940 x 500 native window minimum',
      'four telemetry destinations allowed before patch and denied after', 'telemetry blocking independent of cleanup', 'actual patched request/CORS handlers'] };
  fs.writeFileSync(resultPath, JSON.stringify(result, null, 2) + '\n'); window.destroy(); server.close(); app.exit(0);
}).catch(error => { fs.writeFileSync(resultPath, JSON.stringify({ result: 'FAIL', electron: process.versions.electron, error: error.stack, requests, privacyRequests, notices, served }, null, 2)); app.exit(1); });
setTimeout(() => { fs.writeFileSync(resultPath, JSON.stringify({ result: 'FAIL', error: '60 second timeout' })); app.exit(1); }, 60000).unref();
