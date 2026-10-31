// Exercises the actual 3.7.25 verifier with isolated hash and path dependencies.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const crypto = require('node:crypto');
const [originalPath, patchedPath, jarPath] = process.argv.slice(2);
const upstream = '6772928bce2a69c44fd6de2b5e23ada5537bfdde';
const patched = crypto.createHash('sha1').update(fs.readFileSync(jarPath)).digest('hex');
let hash;
function verifier(file) {
    const text = fs.readFileSync(file, 'utf8');
    const start = text.indexOf('mS=async e=>{');
    const end = text.indexOf(',hS=', start);
    assert(start >= 0 && end > start);
    return vm.runInNewContext('(' + text.slice(start + 3, end) + ')', {
        fS: async () => hash, S: path.win32,
        dS: { Missing: 'missing', Matched: 'matched', Mismatched: 'mismatched' },
        uS: { Present: 'present', NoPreviousFile: 'no-previous', NotAvailable: 'unavailable' }
    });
}
(async () => {
    const before = verifier(originalPath), after = verifier(patchedPath);
    const game = { location: 'C:\\Lunar\\lunar.jar', expectedHash: upstream, url: 'fixture' };
    hash = upstream; assert.equal((await before(game)).reason, 'matched');
    hash = patched; assert.equal((await before(game)).reason, 'mismatched');
    assert.equal((await after(game)).reason, 'matched');
    for (const changed of [upstream, '0'.repeat(40), undefined]) {
        hash = changed;
        await assert.rejects(after(game), /game file or version changed/);
    }
    hash = patched;
    await assert.rejects(after({ ...game, expectedHash: '1'.repeat(40) }), /game file or version changed/);
    for (const other of [
        { ...game, location: 'C:\\Lunar\\another.jar' },
        { ...game, location: 'C:\\Lunar\\genesis-0.1.0-SNAPSHOT-all.jar' }
    ]) {
        for (const value of [upstream, patched, undefined]) {
            hash = value;
            assert.equal(JSON.stringify(await after(other)), JSON.stringify(await before(other)));
        }
    }
    console.log('PASS exact patched-JAR acceptance; changed, missing and newer game rejection; unrelated verification unchanged.');
})().catch(error => { console.error(error); process.exitCode = 1; });
