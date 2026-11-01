import base64
import argparse
import hashlib
import json
import struct
import sys
import zipfile
import importlib.util
from pathlib import Path

root = Path(__file__).resolve().parent
sys.path.insert(0, str(root))
import patch_lunar as lunar

parser = argparse.ArgumentParser(description='Rebase the Lunar 0.1.7 pack onto the inspected Genesis 9.10.1 build.')
parser.add_argument('--old-pack', type=Path, required=True)
parser.add_argument('--original-dir', type=Path, required=True)
parser.add_argument('--output', type=Path, required=True)
args = parser.parse_args()
pack_bytes = args.old_pack.read_bytes()
assert hashlib.sha256(pack_bytes).hexdigest() == 'cb594d8c6d3f5fe39aa19cc12b237cf69a7a5e8a50f29fd9d5fd2bac50fa6b0a'
pack = json.loads(pack_bytes)
game = args.original_dir / 'lunar.jar'
game_sha256 = hashlib.sha256(game.read_bytes()).hexdigest()
game_sha1 = hashlib.sha1(game.read_bytes()).hexdigest()
assert game_sha256 == '88d372051c67e39d07fe4763a0c01fb29f93b1900e3956bb02a722b0ff32755c'
assert game_sha1 == '684248561d22fbd6dbf751bea23fc592d4359ad7'
manager = 'com/moonsworth/lunar/client/OROHROORCHIHCRHOROOHOHORCHHOII/ICCOIIRCOIOHHHHCIRHHIRICRCROOO/RHIOIIOORCOOCCRCHOHHOOIIHRHCIH.class'
method = 'RHCHRHOHRHICHCICRHOIROOCRRROOC'
cloth = ('com/moonsworth/lunar/client/CIRHHCCHOCHRICRHHHRORCCRIIHIHI/RHOICRHRRHCICCRORRHHIRIIHHIIHI/'
         'HHROHORCRHHRHCHHOCOCIHHCIORHIH/IOOCCRIRHHROHIHRCHHRRIOICHIOIR/COIOIRHRRORRHHOIIORORRHOOCRHII.class')
cloth_methods = {
    ('IOOCCRIRHHROHIHRCHHRRIOICHIOIR','(Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;'),
    ('OICROIOCCHRCHRIRIRRIHOIRRCCRCH','(Lcom/moonsworth/lunar/client/IOIOOCICIIHCIRIRRHHOIOOOICIRRC/'
     'RHOICRHRRHCICCRORRHHIRIIHHIIHI/COIOIRHRRORRHHOIIORORRHOOCRHII;)Ljava/lang/String;'),
}
captures = {}
old_init = lunar.ClassFile.__init__
old_replace = lunar.ClassFile.replace_methods
def init(cf, data):
    old_init(cf, data)
    cf.original = data
def capture(cf, bodies):
    old = cf.original
    count = struct.unpack_from('>H', old, 8)[0]
    start = len(old) - len(cf.tail)
    edits = []
    if len(cf.cp) != count:
        edits.append((8, 2, struct.pack('>H', len(cf.cp))))
        edits.append((start, 0, b''.join(c for c in cf.cp[count:] if c)))
    for kind, name, descriptor, attrs in cf.members:
        if kind != 'method' or (name, descriptor) not in bodies:
            continue
        code, stack, locals_count = bodies[(name, descriptor)]
        for first, end, attr, index in attrs:
            if attr == 'Code':
                payload = struct.pack('>HHI', stack, locals_count, len(code)) + code + b'\0\0\0\0'
                edits.append((start + first, end - first, struct.pack('>HI', index, len(payload)) + payload))
    captures[hashlib.sha256(old).hexdigest()] = sorted(edits)
    return old_replace(cf, bodies)
lunar.ClassFile.__init__ = init
lunar.ClassFile.replace_methods = capture
def splice(op, entry, old, changed, edits):
    result = bytearray(old)
    for offset, length, data in reversed(edits):
        result[offset:offset+length] = data
    assert bytes(result) == changed
    op.update(sha256=game_sha256, entry=entry, entrySha256=lunar.sha(old), resultSha256=lunar.sha(changed),
              edits=[{'offset':o,'length':n,'dataBase64':base64.b64encode(v).decode()} for o,n,v in edits])

patches = {p['id']:p for p in pack['patches']}
ops = patches['local-cosmetics']['operations']
with zipfile.ZipFile(game) as z:
    for op in ops:
        if op['kind'].startswith('jar'):
            op['sha256'] = game_sha256
    old = z.read(manager)
    changed = lunar.ClassFile(old).replace_methods({(method,'()V'):(b'\xb1',0,1)})
    splice(ops[2], manager, old, changed, captures[lunar.sha(old)])
    old = z.read(cloth)
    assert lunar.sha(old) == 'aab91ba3722a51c0eec7a60351f6e359de8bd7f3fd315085c9cad71c869c6740'
    cf = lunar.ClassFile(old)
    refs = []
    for i, item in enumerate(cf.cp):
        if item and item[0] == 10:
            owner, nt = struct.unpack_from('>HH', item, 1)
            n, d = struct.unpack_from('>HH', cf.cp[nt], 1)
            owner_name = cf.utf(struct.unpack_from('>H', cf.cp[owner], 1)[0])
            if (owner_name, cf.utf(n), cf.utf(d)) == ('com/moonsworth/lunar/client/OROHROORCHIHCRHOROOHOHORCHHOII/ICCOIIRCOIOHHHHCIRHHIRICRCROOO/IOIOOCICIIHCIRIRRHHOIOOOICIRRC', 'HIHOCHIHOHIIRIROOOICHOCIOIHCIC', '()Z'):
                refs.append(i)
    assert len(refs) == 1
    pattern = b'\xb6' + struct.pack('>H', refs[0])
    edits = []
    tail = len(old) - len(cf.tail)
    for kind, name, descriptor, attrs in cf.members:
        if kind != 'method' or (name, descriptor) not in cloth_methods:
            continue
        for first, end, attr, index in attrs:
            if attr != 'Code':
                continue
            size = struct.unpack_from('>I', cf.tail, first + 10)[0]
            code = cf.tail[first+14:first+14+size]
            assert code.count(pattern) == 1
            edits.append((tail + first + 14 + code.index(pattern), 3, b'\x57\x04\x00'))
    assert len(edits) == 2
    changed = bytearray(old)
    for offset, size, data in edits:
        changed[offset:offset+size] = data
    splice(ops[3], cloth, old, bytes(changed), sorted(edits))
    assert lunar.sha(z.read(ops[0]['entry'])) == ops[0]['entrySha256']
    assert lunar.sha(z.read(ops[1]['entry'])) == ops[1]['entrySha256']

guard = ops[-1]
guard['replacement'] = guard['replacement'].replace('3a30454c0e076f6feee2246c5969d006414f4307',game_sha1).replace('83127fc392298424f53eef71f6bdb8bb61a7a2b6','7e24f47f40cf5194e8910fce35ad270c4d2d7654')
spec = importlib.util.spec_from_file_location('startup_updated', root / 'patch_startup_updated.py')
startup = importlib.util.module_from_spec(spec)
spec.loader.exec_module(startup)
genesis = args.original_dir / 'genesis-0.1.0-SNAPSHOT-all.jar'
assert lunar.sha(genesis.read_bytes()) == startup.GENESIS_SHA256
startup_ops = patches['bootstrap-download-fallback']['operations']
with zipfile.ZipFile(genesis) as z:
    original_class = z.read(startup.HTTP_ENTRY)
    modified_class, edits = startup.patch_http(original_class, lunar.ClassFile)
    startup_ops[0].update(sha256=startup.GENESIS_SHA256, entry=startup.HTTP_ENTRY,
                          entrySha256=lunar.sha(original_class), resultSha256=lunar.sha(modified_class), edits=edits)
for op in startup_ops[1:3]:
    op['sha256'] = startup.GENESIS_SHA256
startup_guard = startup_ops[-1]
startup_guard['replacement'] = startup_guard['replacement'].replace('ea2f19ae5fcb3941df77637b184a1ff02157d6b1',startup.GENESIS_SHA1).replace('12ec07a090a9d071363d1884a958682cd00c7d91','05a965eb4d383e28341264787b30505c7479ef3c')
patches['local-cosmetics']['version'] = '0.1.3'
patches['bootstrap-download-fallback']['version'] = '0.1.2'
pack['appVersion'] = '3.7.29-ow / Genesis 9.10.1'
pack['packVersion'] = '0.1.8'
args.output.write_text(json.dumps(pack,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print('Generated candidate; validate exact JAR output hashes and all guards before use.')
