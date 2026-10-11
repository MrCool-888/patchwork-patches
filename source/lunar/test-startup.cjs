// Execute the inspected launcher verifier in isolation; no network or file mutation.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const [version, originalFile, patchedFile] = process.argv.slice(2);
assert(['3.7.17-ow', '3.7.24-ow'].includes(version));
const old = fs.readFileSync(originalFile, 'utf8');
const changed = fs.readFileSync(patchedFile, 'utf8');
const legacy = version === '3.7.17-ow';
const name = legacy ? 'Jme' : 'SS', next = legacy ? 'KS' : 'CS';
const status = legacy ? 'WS' : 'yS', diff = legacy ? 'US' : 'vS';
const genesis = 'genesis-0.1.0-SNAPSHOT-all.jar';
const upstream = 'ea2f19ae5fcb3941df77637b184a1ff02157d6b1';
const result = '106f52481af4a63ba559bb817e9ccea087f387f7';
function verifier(text, hash) {
  const start = text.indexOf(name + '=async e=>');
  assert(start >= 0);
  const end = text.indexOf(',' + next + '=', start);
  assert(end > start);
  const ctx = {S: path.win32, GS: async () => hash, bS: async () => hash};
  ctx[status] = {Matched: 'matched', Missing: 'missing', Mismatched: 'mismatch'};
  ctx[diff] = {Present: 'present', NoPreviousFile: 'no-previous', NotAvailable: 'unavailable'};
  return vm.runInNewContext('(' + text.slice(start + name.length + 1, end) + ')', ctx);
}
const file = (name, expectedHash) => ({location: 'C:\\private-test\\' + name, expectedHash});
(async () => {
  assert.equal((await verifier(old, upstream)(file(genesis, upstream))).reason, 'matched');
  assert.equal((await verifier(changed, result)(file(genesis, upstream))).reason, 'matched');
  await assert.rejects(verifier(changed, upstream)(file(genesis, upstream)), /bootstrap file or version changed/);
  await assert.rejects(verifier(changed, 'wrong')(file(genesis, upstream)), /bootstrap file or version changed/);
  await assert.rejects(verifier(changed, result)(file(genesis, 'new-upstream')), /bootstrap file or version changed/);
  assert.equal((await verifier(changed, 'normal')(file('unrelated.jar', 'normal'))).reason, 'matched');
  assert.equal((await verifier(changed, 'wrong')(file('unrelated.jar', 'normal'))).reason, 'mismatch');
  assert.equal((await verifier(changed, null)(file(genesis, upstream))).reason, 'missing');
  const game = '83127fc392298424f53eef71f6bdb8bb61a7a2b6';
  assert.equal((await verifier(changed, game)(file('lunar.jar', '3a30454c0e076f6feee2246c5969d006414f4307'))).reason, 'matched');
  console.log('PASS ' + version + ': exact bootstrap hashes, cosmetics guard compatibility and ordinary/missing file verification.');
})().catch(e => { console.error(e); process.exitCode = 1; });
