const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const { readArchive, hash } = require('./asar-reader.cjs');
const installed = process.argv[2] || path.join(process.env.LOCALAPPDATA, 'Programs/Blitz/resources/app.asar');
const destination = process.argv[3] || path.join(process.cwd(), 'Blitz-3.0.7.patchwork.json');
const archive = readArchive(installed);
assert.equal(hash(archive.bytes), '44132b54235250bc4db85c09c80a247ce20d4fac95476bb10244bb0ecf344193', 'Use the verified original Blitz archive.');
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
assert.equal(hash(icu), 'b816e1c340fbcb32c3a5dec883270aa44b929a2dc9bf4de6270fe8aee27757ad', 'Use the verified original Blitz ICU file.');
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
  schemaVersion: 1, id: 'blitz-clean-desktop-3-0-7', appId: 'blitz', appName: 'Blitz',
  appVersion: '3.0.7.134 (frontend 3.0.8-ota.0)', packVersion: '1.1.1', minimumPatcherVersion: '0.8.2',
  versionFile: 'resources/app.asar', versionSha256: hash(archive.bytes), author: 'Local build', source: 'Local Blitz Clean Desktop pack',
  patches: [
    { id: 'clean-desktop', name: 'Clean desktop', category: 'Desktop', version: '1.1.1', status: 'ready',
      description: 'Blocks verified display/video ad loaders, collapses ad spaces, and hides verified banners and upgrade buttons on frontend 3.0.8-ota.0, including Go ad free banners and their reserved space on player-history and match-detail pages. Supported desktop views bypass cache so cached ad scripts are filtered too. Unknown frontends retain original behavior. Contains executable JavaScript client edits. See validation notes for runtime limits.',
      operations: [edit(start, hook + '\n\n' + start), edit(load, '  patchworkInstallDesktopCleanup(browserView, url);\n' + load), ...automaticUpdates, checksum] },
    { id: 'compact-window', name: 'Compact window', category: 'Local features', version: '1.1.0', status: 'ready',
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
  ],
};
for (const patch of pack.patches) patch.description += ' Automatic Blitz client/frontend updates are off by default; manual checks remain available. Maintains the native archive checksum in the final four bytes of icudtl.dat.';
fs.mkdirSync(path.dirname(destination), { recursive: true });
fs.writeFileSync(destination, JSON.stringify(pack, null, 2) + '\n');
console.log('Built ' + destination + ' (' + fs.statSync(destination).size + ' bytes)');
