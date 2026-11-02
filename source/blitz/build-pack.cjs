const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { readArchive, hash } = require('./asar-reader.cjs');
const installed = process.argv[2] || path.join(process.env.LOCALAPPDATA, 'Programs/Blitz/resources/app.asar');
const destination = process.argv[3] || path.join(process.cwd(), 'Blitz-3.0.17.patchwork.json');
const archive = readArchive(installed);
assert.equal(hash(archive.bytes), '41ae887fb6e213722ae66e764c09fb2b6bc590800a32c0c4d587f98a0de4d579', 'Use the verified original Blitz 3.0.17 archive.');
const entry = 'src/createWindow.js';
const original = archive.member(entry).toString('utf8');
function editMember(member, find, replacement) {
  const text = archive.member(member).toString('utf8');
  assert.equal(text.split(find).length - 1, 1, 'Expected one original match in ' + member + ': ' + find);
  return { kind: 'asarTextReplace', file: 'resources/app.asar', sha256: hash(archive.bytes),
    entry: member, entrySha256: hash(archive.member(member)), find, replacement, count: 1 };
}
const edit = (find, replacement) => editMember(entry, find, replacement);
const icu = fs.readFileSync(process.argv[4] || path.resolve(path.dirname(installed), '..', 'icudtl.dat'));
assert.equal(hash(icu), '53c9cab5dbb99bc76a25e50eee075de152554ea0fb8b82588d5008857180a87f', 'Use the verified original Blitz 3.0.17 ICU file.');
const checksum = { kind: 'asarChecksum', file: 'icudtl.dat', sha256: hash(icu), archive: 'resources/app.asar', archiveSha256: hash(archive.bytes), algorithm: 'xxhash32', offset: icu.length - 4 };
// Automatic checks are opt-in; the existing tray/UI manual checks remain available.
const automaticUpdates = [
  editMember('src/autoUpdater/index.js', 'function pollForUpdates() {', 'function pollForUpdates() {\n  if (process.env.BLITZ_AUTO_UPDATE !== "1") return;'),
  editMember('src/autoUpdater/index.js', 'function bootApp(showSplash) {', 'function bootApp(showSplash) {\n  if (process.env.BLITZ_AUTO_UPDATE !== "1") return Promise.resolve();'),
  editMember('src/blitz-entry.js', 'const initialOtaUpdateCheck = ota.checkForUpdates();', 'const initialOtaUpdateCheck = ota.checkForUpdates(true);'),
  editMember('src/ota.js', '  async checkForUpdates() {', '  async checkForUpdates(automatic = false) {\n    if (automatic && process.env.BLITZ_AUTO_UPDATE !== "1") return null;'),
  editMember('src/ota.js', '        await this.checkForUpdates();', '        await this.checkForUpdates(true);'),
];
const hook = fs.readFileSync(path.join(__dirname, 'desktop-cleanup.js'), 'utf8').trim();
const telemetryFilter = fs.readFileSync(path.join(__dirname, 'telemetry-filter.js'), 'utf8').trim();
const crashReporter = archive.member('src/crashReporter.js').toString('utf8');
const machineInfo = archive.member('src/util/machineInfo.js').toString('utf8');
const profileStart = machineInfo.indexOf('const getUserProfile = async () => {');
const profileEnd = machineInfo.indexOf('\n\nmodule.exports', profileStart);
assert.ok(profileStart >= 0 && profileEnd > profileStart);
const system = archive.member('src/listeners/system.js').toString('utf8');
const hardwareStart = system.indexOf('addListener(EvTypes.MACHINE_INFO, async () => {');
assert.ok(hardwareStart >= 0 && system.slice(hardwareStart).trimEnd().endsWith('});'));
const start = 'function createBrowserView({ width, height, url }) {';
const load = '  browserView.webContents.loadURL(url);';
const savedMinimum = `  MIN_WIDTH = Math.min(
    (await get("MIN_WIDTH")) || DEFAULT_MIN_WIDTH,
    DEFAULT_MIN_WIDTH
  );
  MIN_HEIGHT = (await get("MIN_HEIGHT")) || MIN_HEIGHT;`;
const accountStart = original.indexOf('  fetchUser().then((user) => {');
const accountEnd = original.indexOf('\n\n  windows.client._initialURL', accountStart);
assert.ok(accountStart >= 0 && accountEnd > accountStart);
const pack = {
  // Keep the published bundle ID so importing an update replaces the existing catalog entry.
  schemaVersion: 1, id: 'blitz-clean-desktop-3-0-7', appId: 'blitz', appName: 'Blitz',
  appVersion: '3.0.17 (frontend 3.0.17)', packVersion: '1.2.0', minimumPatcherVersion: '0.8.2',
  versionFile: 'resources/app.asar', versionSha256: hash(archive.bytes), author: 'Local build', source: 'Local Blitz Clean Desktop pack',
  patches: [
    { id: 'clean-desktop', name: 'Clean desktop', category: 'Desktop', version: '1.2.0', status: 'ready',
      description: 'Blocks verified display/video ad loaders, collapses ad spaces, and hides verified banners and upgrade buttons on frontend 3.0.17, including Go ad free banners and their reserved space on player-history and match-detail pages. Supported desktop views bypass cache so cached ad scripts are filtered too. Unknown frontends retain original behavior. Contains executable JavaScript client edits. See validation notes for runtime limits.',
      operations: [edit(start, hook + '\n\n' + start), edit(load, '  patchworkInstallDesktopCleanup(browserView, url);\n' + load), ...automaticUpdates, checksum] },
    { id: 'compact-window', name: 'Compact window', category: 'Local features', version: '1.2.0', status: 'ready',
      description: 'Allows a 940 x 500 minimum desktop window for every account, bounded by the screen size. Does not change account roles or stored minimum-size preferences. Contains executable JavaScript client edits.',
      operations: [
        edit('const DEFAULT_MIN_WIDTH = 1075;', 'const DEFAULT_MIN_WIDTH = 940;'),
        edit('const DEFAULT_HEIGHT = 850;', 'const DEFAULT_HEIGHT = 500;'),
        edit(savedMinimum, '  MIN_WIDTH = DEFAULT_MIN_WIDTH;\n  MIN_HEIGHT = DEFAULT_HEIGHT;'),
        edit(original.slice(accountStart, accountEnd), `  windows.client.setMinimumSize(
    Math.max(1, Math.min(MIN_WIDTH, displaySize.width - SCREEN_MARGIN)),
    Math.max(1, Math.min(MIN_HEIGHT, displaySize.height - SCREEN_MARGIN))
  );`),
        ...automaticUpdates, checksum,
      ] },
    { id: 'remove-telemetry', name: 'Remove telemetry', category: 'Privacy', version: '1.0.0', status: 'ready',
      description: 'Blocks verified Blitz Science event/error uploads and Google Analytics requests in desktop and overlay sessions. Disables Electron/native crash-handler setup, hardware-spec replies, open-window usage reporting and machine-ID profile segmentation. Retains game IPC, account APIs and feature-flag machine IDs. Local logs and functional game-data requests remain; this is not a guarantee that every native or future telemetry path is removed. Contains executable JavaScript client edits.',
      operations: [
        editMember('src/electronWindowHandlers.js', 'function interceptRequests(window) {', telemetryFilter + '\n\nfunction interceptRequests(window) {'),
        editMember('src/electronWindowHandlers.js', '      // Handle LCU', '      if (patchworkIsTelemetryURL(parsedUrl)) {\n        callback({ cancel: true });\n        return;\n      }\n\n      // Handle LCU'),
        editMember('src/crashReporter.js', crashReporter, '// Patchwork: neither Electron nor the custom native crash handler is started.\nmodule.exports = { setupCrashHandler: async () => {} };\n'),
        editMember('src/util/machineInfo.js', machineInfo.slice(profileStart, profileEnd), 'const getUserProfile = async () => null;'),
        editMember('src/listeners/system.js', system.slice(hardwareStart), 'addListener(EvTypes.MACHINE_INFO, async () => null);\n'),
        editMember('src/listeners/bapp.js', 'addListener(EvTypes.GET_OPEN_WINDOWS, async () => {\n  return getOpenWindows();\n});', 'addListener(EvTypes.GET_OPEN_WINDOWS, async () => []);'),
        ...automaticUpdates, checksum,
      ] },
  ],
};
for (const patch of pack.patches) patch.description += ' Automatic Blitz client/frontend updates are off by default; manual checks remain available. Maintains the native archive checksum in the final four bytes of icudtl.dat.';
fs.mkdirSync(path.dirname(destination), { recursive: true });
fs.writeFileSync(destination, JSON.stringify(pack, null, 2) + '\n');
console.log('Built ' + destination + ' (' + fs.statSync(destination).size + ' bytes)');
