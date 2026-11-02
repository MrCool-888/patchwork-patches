"""Independent seed-zero XXH32/footer check using only the Python standard library."""
import json
import struct
import sys
from pathlib import Path

MASK = 0xffffffff
P1, P2, P3, P4, P5 = 0x9e3779b1, 0x85ebca77, 0xc2b2ae3d, 0x27d4eb2f, 0x165667b1

def rotate(value, bits):
    value &= MASK
    return ((value << bits) | (value >> (32 - bits))) & MASK

def xxh32(data):
    size, offset = len(data), 0
    if size >= 16:
        lanes = [(P1 + P2) & MASK, P2, 0, (-P1) & MASK]
        while offset <= size - 16:
            values = struct.unpack_from('<4I', data, offset)
            for index in range(4):
                lanes[index] = rotate(lanes[index] + values[index] * P2, 13) * P1 & MASK
            offset += 16
        result = sum(rotate(lane, bits) for lane, bits in zip(lanes, (1, 7, 12, 18))) & MASK
    else:
        result = P5
    result = (result + size) & MASK
    while offset <= size - 4:
        value = struct.unpack_from('<I', data, offset)[0]
        result = rotate(result + value * P3, 17) * P4 & MASK
        offset += 4
    for value in data[offset:]:
        result = rotate(result + value * P5, 11) * P1 & MASK
    result ^= result >> 15
    result = result * P2 & MASK
    result ^= result >> 13
    result = result * P3 & MASK
    return (result ^ (result >> 16)) & MASK

assert [xxh32(value) for value in (b'', b'a', b'abc')] == [0x02cc5d05, 0x550d7456, 0x32d153ff]
root = Path(sys.argv[1])
original = Path(sys.argv[2]).read_bytes()
rows = []
for archive in sorted(root.glob('*.asar')):
    companion = archive.with_suffix('.icudtl.dat').read_bytes()
    checksum = xxh32(archive.read_bytes())
    assert companion[:-4] == original[:-4], archive.name + ': companion body changed'
    assert int.from_bytes(companion[-4:], 'little') == checksum, archive.name + ': incorrect footer'
    rows.append({'selection': archive.stem, 'xxh32': f'{checksum:08x}', 'onlyFooterChanged': True})
assert rows, 'No staged archives found'
print(json.dumps(rows, indent=2))
