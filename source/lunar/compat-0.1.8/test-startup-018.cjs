// Execute the inspected launcher verifier in isolation; no network or file mutation.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const [version, originalFile, patchedFile] = process.argv.slice(2);
assert(['3.7.29-ow'].includes(version));
const old = fs.readFileSync(originalFile, 'utf8');
const changed = fs.readFileSync(patchedFile, 'utf8');
const legacy = version === '3.7.17-ow';
const name = 'Qve', next = 'Ex';
const status = 'Sx', diff = 'xx';
const genesis = 'genesis-0.1.0-SNAPSHOT-all.jar';
const upstream = 'ce6fd0c7bc1baada907f85927d469e084192636c';
const result = '05a965eb4d383e28341264787b30505c7479ef3c';
function verifier(text, hash) {
  const start = text.indexOf(name + '=async e=>');
  assert(start >= 0);
  const end = text.indexOf(',' + next + '=', start);
  assert(end > start);
  const ctx = {S: path.win32, GS: async () => hash, Tx: async () => hash};
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
  const game = '7e24f47f40cf5194e8910fce35ad270c4d2d7654';
  assert.equal((await verifier(changed, game)(file('lunar.jar', '684248561d22fbd6dbf751bea23fc592d4359ad7'))).reason, 'matched');
  await assert.rejects(verifier(changed, game)(file('lunar.jar', 'wrong-upstream')), /game file or version changed/);
  await assert.rejects(verifier(changed, 'wrong')(file('lunar.jar', '684248561d22fbd6dbf751bea23fc592d4359ad7')), /game file or version changed/);
  console.log('PASS ' + version + ': exact bootstrap hashes, cosmetics guard compatibility and ordinary/missing file verification.');
})().catch(e => { console.error(e); process.exitCode = 1; });
