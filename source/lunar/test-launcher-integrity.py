"""Check the packaged ASAR digest without changing any Electron security fuse."""
import hashlib
import struct
import sys
from pathlib import Path
from patch_lunar import asar_read

before, after, archive = [Path(p).read_bytes() for p in sys.argv[1:]]
sentinel = b'dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX'
assert before.count(sentinel) == after.count(sentinel) == 1
wire = before.index(sentinel) + len(sentinel)
assert before[wire:wire + 11] == after[wire:wire + 11] == b'\x01\x09101111011'
digest = hashlib.sha256(archive[16:16 + struct.unpack_from('<I', archive, 12)[0]]).hexdigest().encode('ascii')
assert after.count(digest) == 1
offset = after.index(digest)
assert after[offset - len(b'"value":"'):offset] == b'"value":"'
assert before[:offset] == after[:offset] and before[offset + 64:] == after[offset + 64:]
_, members, base = asar_read(archive)
checked = 0
for member in members.values():
    if 'integrity' not in member or 'offset' not in member:
        continue
    start = base + int(member['offset'])
    data = archive[start:start + member['size']]
    integrity = member['integrity']
    assert integrity['algorithm'] == 'SHA256'
    assert hashlib.sha256(data).hexdigest() == integrity['hash']
    blocks = [hashlib.sha256(data[i:i + integrity['blockSize']]).hexdigest()
              for i in range(0, max(1, len(data)), integrity['blockSize'])]
    assert blocks == integrity['blocks']
    checked += 1
assert checked > 0
print(f'PASS all nine fuses preserved; exact embedded header digest; {checked} archive member/block hashes.')
