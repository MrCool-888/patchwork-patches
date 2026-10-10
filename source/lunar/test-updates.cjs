// Execute the actual inspected launcher methods with an isolated updater; no network or app installation.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const [version, originalFile, patchedFile] = process.argv.slice(2);
assert.ok(['3.7.17-ow', '3.7.24-ow'].includes(version), 'Usage: node test-updates.cjs VERSION ORIGINAL_MAIN_JS PATCHED_MAIN_JS');
const original = fs.readFileSync(originalFile, 'utf8');
const patched = fs.readFileSync(patchedFile, 'utf8');

function updaterClass(text) {
  const start = text.indexOf('class{options;logger;checkTask;checkingLock=');
  const ending = 'this.resetCheckState()}}';
  const end = text.indexOf(ending, start);
  assert.ok(start >= 0 && end > start, 'Updater class boundary changed');
  return text.slice(start, end + ending.length);
}

function fixture(text) {
  const calls = {checks: 0, installs: 0, feeds: 0, logs: [], interval: null, manual: null};
  const native = {on() {}, setFeedURL() {calls.feeds++;}, async checkForUpdates() {calls.checks++; return {};}, quitAndInstall() {calls.installs++;}};
  class Logger {info(message) {calls.logs.push(message);} warn() {} error() {} debug() {}}
  const context = {
    t: {ipcMain: {handle(name, callback) {assert.equal(name, 'misc:checkForUpdates'); calls.manual = callback;}, removeHandler() {}}},
    Z: {store: {getState: () => ({misc: {}, metadata: {}, launch: {phase: 'ready'}}), dispatch() {}}},
    K9: {isAnyBrowserFocused: () => true, getWindow: () => null},
    $: {getModule: () => null}, Y: {t: () => ''}, J: {native: 'native', toast: 'toast'},
    setInterval(callback) {calls.interval = callback; return 1;}, clearInterval() {}, F9() {},
    M9: {autoUpdater: native}, j9: {autoUpdater: native}, rKe: Logger, c0e: Logger,
    lfe: 'fixture', Wy: 'fixture', hb: 'fixture', Zb: 'fixture', nx: null,
    ay: 'fixture', iy: 'fixture', Uy: 'fixture', Tb: 'fixture', Ab: null,
    Gy: false, Ky: false, oy: false, sy: false,
    bW: {Loading: 'loading', Ready: 'ready', Error: 'error'}, NH: {Loading: 'loading', Ready: 'ready', Error: 'error'},
    QZ: {version: version}, By: 0, yX: {version: version}, ey: 0,
    av: {}, M_: {}, q6() {}, u6() {}
  };
  const Updater = vm.runInNewContext('(' + updaterClass(text) + ')', context);
  return {calls, native, instance: new Updater()};
}

function serverRequest(text) {
  const start = text.indexOf('onPush=e=>{this.client.assetServerModule.logger.info(`[Updater] Checking for updates (force install:');
  const end = text.indexOf(')}},', start);
  assert.ok(start >= 0 && end > start, 'Server update handler boundary changed');
  let requests = 0;
  // Arrow function this is the VM global; supply only the expected client logger.
  const context = {q9: {checkForUpdates() {requests++;}}, client: {assetServerModule: {logger: {info() {}}}}};
  const execute = vm.runInNewContext('(' + text.slice(start + 'onPush='.length, end + 2) + ')', context);
  execute({forceInstall: false}); execute({forceInstall: true});
  return requests;
}

(async () => {
  const baseline = fixture(original);
  await baseline.instance.checkForUpdates();
  assert.equal(baseline.calls.checks, 1, 'Baseline automatic path did not reach updater');
  assert.equal(serverRequest(original), 2, 'Baseline forced update path changed');

  const test = fixture(patched);
  assert.equal(test.native.autoDownload, false);
  assert.equal(test.native.autoInstallOnAppQuit, false, 'Install on exit remains enabled');
  assert.equal(test.native.autoRunAppAfterInstall, true, 'Manual restart preference changed');
  await test.instance.checkForUpdates();
  await test.instance.checkForUpdates(false);
  test.calls.interval();
  test.instance.setOptions({url: 'changed-channel', channel: 'changed'});
  assert.equal(test.calls.checks, 0, 'Startup/timer/channel change reached native updater');
  assert.equal(test.calls.installs, 0);
  assert.equal(test.instance.checkingLock, false);
  assert.equal(test.instance.userInitiated, false);
  test.instance.updateDownloaded = true;
  await test.instance.checkForUpdates();
  assert.equal(test.calls.installs, 0, 'Automatic call installed a pending download');
  test.instance.updateDownloaded = false;
  await test.calls.manual();
  assert.equal(test.calls.checks, 1, 'Manual IPC check was blocked');
  assert.equal(test.instance.userInitiated, true);
  assert.equal(test.instance.checkingLock, true);
  test.calls.interval();
  assert.equal(test.calls.checks, 1, 'Timer started another check');
  assert.equal(test.instance.userInitiated, true, 'Timer reset an in-flight manual request');
  test.instance.resetCheckState();
  test.instance.updateDownloaded = true;
  await test.calls.manual();
  assert.equal(test.calls.installs, 1, 'Manual installation of pending download was blocked');
  assert.equal(serverRequest(patched), 0, 'Server force-install bypassed automatic update block');
  console.log(`PASS ${version}: startup/timer/channel/server updates and install on exit blocked; manual checks/install preserved.`);
})().catch(error => {console.error(error); process.exitCode = 1;});
