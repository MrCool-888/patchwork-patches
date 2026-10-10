const fs = require('node:fs');
const crypto = require('node:crypto');
function readArchive(filename) {
  const bytes = fs.readFileSync(filename);
  const headerSize = bytes.readUInt32LE(4);
  const header = JSON.parse(bytes.toString('utf8', 16, 16 + bytes.readUInt32LE(12)));
  function member(path) {
    let entry = header;
    for (const part of path.split('/')) entry = entry.files[part];
    if (!entry || entry.unpacked || entry.link) throw new Error('Missing packed entry: ' + path);
    const start = 8 + headerSize + Number(entry.offset);
    return bytes.subarray(start, start + entry.size);
  }
  return { bytes, header, member };
}
function hash(bytes) { return crypto.createHash('sha256').update(bytes).digest('hex'); }
module.exports = { readArchive, hash };
