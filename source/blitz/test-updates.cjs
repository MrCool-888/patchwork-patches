const assert = require('node:assert/strict');
const vm = require('node:vm');
const { readArchive } = require('./asar-reader.cjs');
const archive = readArchive(process.argv[2]);
const client = archive.member('src/autoUpdater/index.js').toString();
const source = archive.member('src/ota.js').toString();
const environment = { env: {}, platform: 'win32' };
let clientChecks = 0, frontendChecks = 0, timers = [];
const context = vm.createContext({ process: environment, oneHour: 3600000,
  autoUpdater: { checkForUpdates: () => ++clientChecks }, setInterval: fn => timers.push(fn) });
for (const name of ['checkForUpdates', 'pollForUpdates', 'bootApp']) {
  const start = client.indexOf('function ' + name + '(');
  const end = client.indexOf('\n}\n', start) + 2;
  assert.ok(start >= 0 && end > start);
  vm.runInContext(client.slice(start, end), context);
}
const otaContext = { module: { exports: {} }, process: environment,
  require(name) {
    if (name === 'electron') return { app: { getVersion: () => '3.0.7' } };
    if (name === 'npmlog') return { error() {} };
    if (name === './db') return { get: async () => JSON.stringify({ ota: { '3.0.7': '3.0.8-ota.0' } }), write: async () => {} };
    if (name === './windows') return { client: null };
    return {};
  },
  fetch: async () => { frontendChecks++; return { ok: true, json: async () => ({ ota: { '3.0.7': '3.0.8-ota.0' } }) }; },
  setInterval: fn => timers.push(fn),
};
vm.runInNewContext(source, otaContext); const ota = otaContext.module.exports;
(async () => {
  context.pollForUpdates(); await context.bootApp(true);
  await ota.checkForUpdates(true); ota.pollForUpdates(); await timers.shift()();
  assert.equal(clientChecks, 0); assert.equal(frontendChecks, 0);
  context.checkForUpdates(); await ota.checkForUpdates();
  assert.equal(clientChecks, 1); assert.equal(frontendChecks, 1);
  assert.ok(archive.member('src/blitz-entry.js').toString().includes('const initialOtaUpdateCheck = ota.checkForUpdates(true);'));
  environment.env.BLITZ_AUTO_UPDATE = '1'; timers = [];
  context.pollForUpdates(); await ota.checkForUpdates(true); await timers.shift()();
  assert.equal(clientChecks, 3); assert.equal(frontendChecks, 2);
  console.log('PASS: client startup/polling and frontend startup/polling are off by default; manual checks and explicit opt-in still work.');
})().catch(error => { console.error(error); process.exitCode = 1; });
