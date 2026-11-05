const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const { readArchive } = require('./asar-reader.cjs');
const archive = readArchive(process.argv[2]);
const text = name => archive.member(name).toString('utf8');
const handlers = {};
const blockedCalls = [];
const forbidden = name => () => { blockedCalls.push(name); throw Error('Unexpected collection: ' + name); };
const context = { module: { exports: {} }, URL, Buffer, process: { env: {} },
  require(name) {
    if (name === 'electron') return { app: { getVersion: () => '3.0.17' }, shell: {} };
    return {};
  } };
vm.runInNewContext(text('src/electronWindowHandlers.js'), context);
context.module.exports.interceptRequests({ webContents: { session: { webRequest: {
  onBeforeSendHeaders(filter, callback) { handlers.before = callback; },
  onHeadersReceived(filter, callback) { handlers.after = callback; },
  onBeforeRequest() { throw Error('Telemetry replaced the desktop ad listener'); },
} } } });
function request(url) {
  let calls = 0, result;
  handlers.before({ url, requestHeaders: {} }, value => { calls++; result = value; });
  assert.equal(calls, 1, url);
  return result;
}
const denied = [
  'https://science.v2.iesdev.com/append',
  'https://science.v2.iesdev.com/append?batch=1',
  'https://www.google-analytics.com/g/collect',
  'https://region1.google-analytics.com/g/collect',
  'https://google-analytics.com/collect',
  'https://www.googletagmanager.com/gtag/js?id=fixture',
  'https://data.v2.iesdev.com/api/v1/query_objects/prod/bi/machine_segment?id=fixture',
];
for (const url of denied) assert.equal(request(url).cancel, true, url);
const allowed = [
  'https://auth.blitz.gg/graphql', 'https://utils.iesdev.com/static/json/app/ota',
  'https://lol.v2.iesdev.com/match/fixture', 'https://deadlock.v2.iesdev.com/deadlock/match/fixture',
  'https://data.v2.iesdev.com/api/v1/query_objects/prod/game/stats',
  'https://blitz-native.blitz.gg/production/deadlock/fixture.json',
  'https://www.googletagmanager.com/unrelated',
  'https://science.v2.iesdev.com.evil.example/append',
  'https://notgoogle-analytics.com/collect', 'https://127.0.0.1:2999/liveclientdata/allgamedata',
];
for (const url of allowed) {
  const result = request(url);
  assert.equal(result.cancel, false, url);
  assert.equal(result.requestHeaders['X-Blitz-Version'], '3.0.17');
}
assert.equal(request('wss://riot:fixture@riot.local/socket').requestHeaders.Authorization,
  'Basic ' + Buffer.from('riot:fixture').toString('base64'));
assert.equal(request('wss://rt.blitz.gg/socket').requestHeaders['Blitz-Client'], 'true');
let response;
handlers.after({ url: 'https://auth.blitz.gg/graphql', responseHeaders: {}, statusLine: 'HTTP/1.1 200 OK' }, value => { response = value; });
assert.equal(response.cancel, false);
assert.deepEqual(Array.from(response.responseHeaders['Access-Control-Allow-Origin']), ['*']);

(async () => {
  const crash = { module: { exports: {} }, require: forbidden('crash dependency') };
  vm.runInNewContext(text('src/crashReporter.js'), crash);
  assert.equal(await crash.module.exports.setupCrashHandler(), undefined);

  const machine = { module: { exports: {} }, process: { platform: 'win32' }, require(name) {
    if (name === 'crypto') return require('node:crypto');
    if (name === 'os') return {};
    if (name === '../native-utils') return { getMachineID: forbidden('machine ID') };
    if (name === '../db') return { get: forbidden('cached profile'), write: forbidden('profile write') };
    return new Proxy({}, { get: (_target, key) => forbidden('hardware ' + key) });
  }, fetch: forbidden('profile request') };
  vm.runInNewContext(text('src/util/machineInfo.js'), machine);
  assert.equal(await machine.module.exports.getUserProfile(), null);

  const listeners = {};
  const system = { process: { platform: 'win32' }, console, require(name) {
    if (name === '../listenMessage') return { addListener: (type, callback) => { listeners[type] = callback; } };
    if (name === '../EventTypes.json') return new Proxy({}, { get: (_target, key) => key });
    return new Proxy({}, { get: (_target, key) => forbidden('system ' + key) });
  } };
  vm.runInNewContext(text('src/listeners/system.js'), system);
  assert.equal(await listeners.MACHINE_INFO(), null);
  const windows = text('src/listeners/bapp.js').match(/addListener\(EvTypes\.GET_OPEN_WINDOWS, async \(\) => \[\]\);/);
  assert.ok(windows, 'Open-window analytics collector still present');
  let openWindows;
  vm.runInNewContext(windows[0], { EvTypes: { GET_OPEN_WINDOWS: 'windows' }, addListener: (_type, fn) => { openWindows = fn; } });
  assert.deepEqual(Array.from(await openWindows()), []);
  assert.deepEqual(blockedCalls, []);
  console.log('PASS telemetry: seven denied destinations; ten allowed APIs; LCU/auth/CORS headers retained; no crash, hardware, profile or open-window collection in patched handlers.');
})().catch(error => { console.error(error); process.exitCode = 1; });
