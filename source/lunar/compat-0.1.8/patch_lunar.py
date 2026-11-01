"""Version-specific Lunar Client patch. Python standard library only."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path
import struct
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent
EXPECTED = {
    'app.asar': 'cc33e5054e13a68b980d1496116c74e7b89d38cbcd18b6ea27c3d392609065c4',
    'lunar.jar': 'f4a6a62df2d548906f272d2d0dc906b6caae6f3592f9e2dae918783324bf3cca',
}
COSMETIC_MANAGER = ('com/moonsworth/lunar/client/IHCHHIIIHROCIROHROCHRCIROHOICH/'
                    'IHIRRHORRRRHICCOHOHORCHIHHHIOO/OOIOHHHCIICRRRCICOIIIRRRICOCIC.class')
COSMETIC_PUSH_METHOD = 'CIOIIHIROORCOCIHHRHCROOHHCHOOO'
CLOTH_MENU = ('com/moonsworth/lunar/client/CIRIIHCCROCIOIRORIOIOCHHIIHRCC/'
              'CICOHIIRRHHIIHRROHOICOCRHHHHOO/ROHIOOCICRORHRCIHHHHHHRICCCRRC/'
              'CRRHRIRIHOHRHRCCHRCOICHOOIHOIC/ORCOIHCCICICIHRCCCCHHHRICHHICI.class')
ORIGINAL_GAME_SHA1 = '3a30454c0e076f6feee2246c5969d006414f4307'
HELPER = 'local/lunar/LocalCosmetics'
CALLBACK = 'Lcom/google/protobuf/RpcCallback;'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def replace_once(text, old, new):
    if text.count(old) != 1:
        raise ValueError('Unsupported launcher structure: ' + old[:80])
    return text.replace(old, new, 1)


def asar_read(blob):
    if struct.unpack_from('<I', blob)[0] != 4:
        raise ValueError('Invalid ASAR size pickle')
    header_size = struct.unpack_from('<I', blob, 4)[0]
    json_size = struct.unpack_from('<I', blob, 12)[0]
    header = json.loads(blob[16:16 + json_size])
    files = {}

    def walk(node, prefix=''):
        for name, entry in node.get('files', {}).items():
            if 'files' in entry:
                walk(entry, prefix + name + '/')
            else:
                files[prefix + name] = entry
    walk(header)
    return header, files, 8 + header_size


def patch_asar(blob, game_hash):
    header, files, base = asar_read(blob)
    changes = {}

    def edit(name, pairs):
        entry = files[name]
        start = base + int(entry['offset'])
        text = blob[start:start + entry['size']].decode('utf-8')
        for old, new in pairs:
            text = replace_once(text, old, new)
        changes[name] = text.encode('utf-8')

    layout = ('return{alert:LIe(e.alert),blogPosts:RIe(e.blogPosts,W9.getReadPosts()),'
              'serversCta:e.serversCta,servers:e.shelfServers,navItems:e.navItems,'
              'videoPromotion:e.videoPromotion,ads:e.ads,theme:`default`,'
              'carousel:e.carousel,announcements:e.announcements}')
    guard = ('Jme=async e=>{let t=await GS(e.location);'
        'if((0,S.basename)(e.location)===`lunar.jar`){'
        'if(e.expectedHash===`' + ORIGINAL_GAME_SHA1 + '`&&'
        't===`' + game_hash + '`)return{reason:WS.Matched};'
        'throw Error(`Local cosmetics patch: game file or version changed. '
        'Restore the patch before updating.`)}if(!t)')
    edit('dist-electron/electron/main.js', [(layout,
         'return{alert:void 0,blogPosts:[],serversCta:void 0,servers:[],navItems:[],'
         'videoPromotion:void 0,ads:[],theme:`default`,carousel:[],announcements:[]}'),
         ('Jme=async e=>{let t=await GS(e.location);if(!t)', guard)])
    edit('dist/assets/use-show-ad.js', [
        ('Qt=e=>{let t=l(e=>e.metadata.ads)', 'Qt=e=>{return false;let t=l(e=>e.metadata.ads)'),
        ('Xt=e=>{let t=Nt()', 'Xt=e=>{return null;let t=Nt()'),
        ('onHighImpactRemoved:c})=>{let[l,u]', 'onHighImpactRemoved:c})=>{return null;let[l,u]'),
    ])
    edit('dist/assets/main.js', [
        ('lz=()=>{let{t:e}=N()', 'lz=()=>{return null;let{t:e}=N()'),
        ('cB=()=>{let{t:e}=N()', 'cB=()=>{return null;let{t:e}=N()'),
        ('TA=()=>{let{t:e}=N()', 'TA=()=>{return null;let{t:e}=N()'),
        ("),n},[t,i,a,r,p]", "),n.filter(e=>e.type!==`external`&&e.type!==`promotion`)},[t,i,a,r,p]"),
    ])
    edit('dist/assets/topbar.js', [
        ('Xj=e=>{let{t}=O();return(0,H.jsxs)(Wj,',
         'Xj=e=>{return null;let{t}=O();return(0,H.jsxs)(Wj,'),
        ('$D=({notification:e})=>{let t=ZD[e.type]',
         '$D=({notification:e})=>{if(e.type===HD.Notification_NotificationType.NOTIFICATION_TYPE_PROMOTION)return null;let t=ZD[e.type]'),
    ])
    body = bytearray()
    for name, entry in files.items():
        if entry.get('unpacked') or 'offset' not in entry:
            continue
        start = base + int(entry['offset'])
        data = changes.get(name, blob[start:start + entry['size']])
        entry['offset'] = str(len(body))
        entry['size'] = len(data)
        if name in changes and 'integrity' in entry:
            integrity = entry['integrity']
            if integrity['algorithm'] != 'SHA256':
                raise ValueError('Unsupported ASAR integrity algorithm')
            block = integrity['blockSize']
            integrity['hash'] = sha(data)
            integrity['blocks'] = [sha(data[i:i + block]) for i in range(0, len(data), block)]
        body.extend(data)
    encoded = json.dumps(header, ensure_ascii=False, separators=(',', ':')).encode('utf-8')
    payload = struct.pack('<I', len(encoded)) + encoded
    payload += b'\0' * (-len(payload) % 4)
    pickle = struct.pack('<I', len(payload)) + payload
    output = struct.pack('<II', 4, len(pickle)) + pickle + body
    _, verified, verified_base = asar_read(output)
    for name, data in changes.items():
        entry = verified[name]
        start = verified_base + int(entry['offset'])
        if output[start:start + entry['size']] != data:
            raise ValueError('ASAR output verification failed')
    return bytes(output)


def patch_exe(blob):
    sentinel = b'dL7pKGdnNz796PbbjQWNKmHXBZaB9tsX'
    if blob.count(sentinel) != 1:
        raise ValueError('Unsupported Electron fuse layout')
    start = blob.index(sentinel) + len(sentinel)
    if blob[start:start + 10] != b'\x01\x0810111101':
        raise ValueError('Unsupported Electron fuse configuration')
    output = bytearray(blob)
    output[start + 2 + 4] = ord('0')
    return bytes(output)


class ClassFile:
    """Preserve the original class; replace only selected straight-line methods."""
    def __init__(self, data):
        if data[:4] != b'\xca\xfe\xba\xbe':
            raise ValueError('Invalid Java class')
        self.prefix = data[:8]
        self.cp = [None]
        pos = 10
        count = struct.unpack_from('>H', data, 8)[0]
        while len(self.cp) < count:
            start = pos
            tag = data[pos]
            pos += 1
            if tag == 1:
                size = struct.unpack_from('>H', data, pos)[0]
                pos += 2 + size
            elif tag in (3, 4, 9, 10, 11, 12, 17, 18): pos += 4
            elif tag in (5, 6): pos += 8
            elif tag in (7, 8, 16, 19, 20): pos += 2
            elif tag == 15: pos += 3
            else: raise ValueError('Unknown constant pool tag: ' + str(tag))
            self.cp.append(data[start:pos])
            if tag in (5, 6): self.cp.append(None)
        self.tail = bytearray(data[pos:])
        self.members = []
        p = 6 + 2 + 2 * struct.unpack_from('>H', self.tail, 6)[0]
        for kind in ('field', 'method'):
            number = struct.unpack_from('>H', self.tail, p)[0]
            p += 2
            for _ in range(number):
                _, name, descriptor, attrs = struct.unpack_from('>HHHH', self.tail, p)
                p += 8
                attributes = []
                for _ in range(attrs):
                    index, size = struct.unpack_from('>HI', self.tail, p)
                    attributes.append((p, p + 6 + size, self.utf(index), index))
                    p += 6 + size
                self.members.append((kind, self.utf(name), self.utf(descriptor), attributes))

    def utf(self, index):
        entry = self.cp[index]
        if entry[0] != 1: raise ValueError('Expected UTF8 constant')
        return entry[3:].decode('utf-8', errors='replace')

    def add(self, entry):
        if entry in self.cp: return self.cp.index(entry)
        self.cp.append(entry)
        return len(self.cp) - 1

    def utf_add(self, text):
        value = text.encode('utf-8')
        return self.add(b'\x01' + struct.pack('>H', len(value)) + value)

    def methodref(self, name, descriptor):
        owner = self.add(b'\x07' + struct.pack('>H', self.utf_add(HELPER)))
        nt = self.add(b'\x0c' + struct.pack('>HH', self.utf_add(name), self.utf_add(descriptor)))
        return self.add(b'\x0a' + struct.pack('>HH', owner, nt))

    def fieldref(self, name, descriptor):
        matches = []
        for i, entry in enumerate(self.cp):
            if entry and entry[0] == 9:
                _, nt = struct.unpack_from('>HH', entry, 1)
                n, d = struct.unpack_from('>HH', self.cp[nt], 1)
                if self.utf(n) == name and self.utf(d) == descriptor: matches.append(i)
        if len(matches) != 1: raise ValueError('Missing field: ' + name)
        return matches[0]

    def replace_methods(self, bodies):
        edits = []
        found = set()
        for kind, name, descriptor, attrs in self.members:
            key = (name, descriptor)
            if kind != 'method' or key not in bodies: continue
            code, stack, locals_count = bodies[key]
            for start, end, attribute, index in attrs:
                if attribute == 'Code':
                    payload = struct.pack('>HHI', stack, locals_count, len(code)) + code + b'\0\0\0\0'
                    edits.append((start, end, struct.pack('>HI', index, len(payload)) + payload))
                    found.add(key)
        if found != set(bodies): raise ValueError('Missing methods: ' + str(set(bodies) - found))
        for start, end, data in sorted(edits, reverse=True): self.tail[start:end] = data
        return (self.prefix + struct.pack('>H', len(self.cp)) +
                b''.join(entry for entry in self.cp[1:] if entry is not None) + self.tail)


def patch_stub(data, badge=False):
    cf = ClassFile(data)
    bodies = {}
    prefix = 'com/lunarclient/websocket/' + ('badge/v1/' if badge else 'cosmetic/v2/')
    names = ['equipBadge'] if badge else ['createOutfit', 'updateOutfit', 'deleteOutfit',
                                        'selectOutfit', 'favoriteCosmetic', 'unfavoriteCosmetic']
    for name in names:
        request = 'L' + prefix + name[0].upper() + name[1:] + 'Request;'
        descriptor = '(Lcom/google/protobuf/RpcController;' + request + CALLBACK + ')V'
        target = cf.methodref(name, '(' + request + CALLBACK + ')V')
        bodies[(name, descriptor)] = (b'\x2c\x2d\xb8' + struct.pack('>H', target) + b'\xb1', 2, 4)
    if not badge:
        channel = cf.fieldref('channel', 'Lcom/google/protobuf/RpcChannel;')
        descriptor = '(Lcom/google/protobuf/RpcController;L' + prefix + 'LoginRequest;' + CALLBACK + ')V'
        target = cf.methodref('login', '(Lcom/google/protobuf/RpcChannel;' + descriptor[1:])
        code = b'\x2a\xb4' + struct.pack('>H', channel) + b'\x2b\x2c\x2d\xb8' + struct.pack('>H', target) + b'\xb1'
        bodies[('login', descriptor)] = (code, 4, 4)
    return cf.replace_methods(bodies)


def patch_cloth_menu(data):
    """Allow only the cloak option's two local checks; preserve code sizes/stack maps."""
    cf = ClassFile(data)
    refs = []
    for i, entry in enumerate(cf.cp):
        if entry and entry[0] == 10:
            _, nt = struct.unpack_from('>HH', entry, 1)
            n, d = struct.unpack_from('>HH', cf.cp[nt], 1)
            if cf.utf(n) == 'CRHRRORHHOCOCHHOHIRIIHOIHIIIOI' and cf.utf(d) == '()Z':
                refs.append(i)
    if len(refs) != 1: raise ValueError('Unsupported cloth cloak check')
    pattern = b'\xb6' + struct.pack('>H', refs[0])
    methods = {
        ('CRRHRIRIHOHRHRCCHRCOICHOOIHOIC', '(Ljava/lang/Integer;Ljava/lang/String;Ljava/lang/String;)Ljava/lang/String;'),
        ('ROICRHIOIOCHHICCROCHHCRIORRIRH', '(Lcom/moonsworth/lunar/client/HIRCHOCCIROORHOHOOHOIOHOOIHRCR/'
         'CICOHIIRRHHIIHRROHOICOCRHHHHOO/RCHICCCIHRCRCHCIRRIIRRHICORHHO;)Ljava/lang/String;'),
    }
    edits = []
    tail_start = len(data) - len(cf.tail)
    for kind, name, descriptor, attrs in cf.members:
        if kind != 'method' or (name, descriptor) not in methods: continue
        for start, _, attr, _ in attrs:
            if attr != 'Code': continue
            size = struct.unpack_from('>I', cf.tail, start + 10)[0]
            code = cf.tail[start + 14:start + 14 + size]
            if code.count(pattern) != 1: raise ValueError('Unsupported cloth cloak menu method')
            # pop consumes the manager; iconst_1 supplies permission with the original stack effect.
            edits.append((tail_start + start + 14 + code.index(pattern), 3, b'\x57\x04\x00'))
    if len(edits) != 2: raise ValueError('Missing cloth cloak menu checks')
    result = bytearray(data)
    for offset, size, replacement in edits: result[offset:offset + size] = replacement
    return bytes(result), sorted(edits)


def patch_jar(blob):
    changes = {}
    with zipfile.ZipFile(io.BytesIO(blob)) as source:
        if any(name.upper().endswith(('.SF', '.RSA', '.DSA', '.EC')) for name in source.namelist()):
            raise ValueError('Refusing to alter a signed game JAR')
        stub = 'com/lunarclient/websocket/cosmetic/v2/CosmeticService$Stub.class'
        badge = 'com/lunarclient/websocket/badge/v1/BadgeService$Stub.class'
        changes[stub] = patch_stub(source.read(stub))
        changes[badge] = patch_stub(source.read(badge), badge=True)
        manager = ClassFile(source.read(COSMETIC_MANAGER))
        changes[COSMETIC_MANAGER] = manager.replace_methods({
            (COSMETIC_PUSH_METHOD, '()V'): (b'\xb1', 0, 1)
        })
        changes[CLOTH_MENU], _ = patch_cloth_menu(source.read(CLOTH_MENU))
        for name in ('LocalCosmetics.class', 'LocalCosmetics$1.class'):
            changes['local/lunar/' + name] = (ROOT / 'helper' / name).read_bytes()
        output = io.BytesIO()
        with zipfile.ZipFile(output, 'w') as target:
            for info in source.infolist():
                target.writestr(info, changes.pop(info.filename, source.read(info.filename)))
            for name, data in changes.items(): target.writestr(name, data, compress_type=zipfile.ZIP_DEFLATED)
    with zipfile.ZipFile(io.BytesIO(output.getvalue())) as verified:
        bad = verified.testzip()
        if bad: raise ValueError('Corrupt patched JAR: ' + bad)
    return output.getvalue()


def ensure_closed():
    if os.name != 'nt': return
    result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-Command',
        "@(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -in @('Lunar Client','java','javaw') }).Count"],
        capture_output=True, text=True, check=True)
    if int(result.stdout.strip()) != 0:
        raise ValueError('Close Lunar Client and the game (including Java processes) first.')


def atomic_write(path, data):
    fd, name = tempfile.mkstemp(prefix=path.name + '.', suffix='.tmp', dir=path.parent)
    try:
        with os.fdopen(fd, 'wb') as handle: handle.write(data)
        os.replace(name, path)
    finally:
        if os.path.exists(name): os.unlink(name)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['check', 'stage', 'apply', 'restore'], nargs='?', default='check')
    launcher = Path(os.environ.get('LOCALAPPDATA', str(Path.home() / 'AppData' / 'Local'))) / 'Programs' / 'Lunar Client'
    if not launcher.exists(): launcher = Path.home() / 'AppData' / 'Local' / 'Programs' / 'Lunar Client'
    parser.add_argument('--launcher', type=Path, default=launcher)
    parser.add_argument('--game', type=Path, default=Path.home() / '.lunarclient' / 'offline' / 'multiver' / 'lunar.jar')
    parser.add_argument('--stage-dir', type=Path)
    args = parser.parse_args()
    paths = {'app.asar': args.launcher / 'resources' / 'app.asar',
             'Lunar Client.exe': args.launcher / 'Lunar Client.exe', 'lunar.jar': args.game}
    marker = ROOT / 'backup' / 'manifest.json'
    if args.action == 'restore':
        ensure_closed()
        manifest = json.loads(marker.read_text(encoding='utf-8'))
        for name, info in manifest.items():
            target = Path(info['path'])
            if sha(target.read_bytes()) not in (info['patched'], info['original']):
                raise ValueError('File changed after patch; refusing to overwrite: ' + str(target))
            if sha((ROOT / 'backup' / name).read_bytes()) != info['original']:
                raise ValueError('Backup is damaged: ' + name)
        for name, info in manifest.items(): atomic_write(Path(info['path']), (ROOT / 'backup' / name).read_bytes())
        print('Original launcher and game files restored. Local outfit files are preserved.')
        return
    expected = dict(EXPECTED)
    expected['Lunar Client.exe'] = (ROOT / 'exe-sha256.txt').read_text().strip()
    originals = {name: path.read_bytes() for name, path in paths.items()}
    for name, data in originals.items():
        if sha(data) != expected[name]:
            raise ValueError('Unsupported or already modified file: ' + str(paths[name]) +
                             '\nThis patch only supports the exact inspected 3.7.17-ow installation.')
    print('Exact launcher and game versions match.')
    if args.action == 'check': return
    game = patch_jar(originals['lunar.jar'])
    patched = {'app.asar': patch_asar(originals['app.asar'], hashlib.sha1(game).hexdigest()),
               'Lunar Client.exe': patch_exe(originals['Lunar Client.exe']),
               'lunar.jar': game}
    if args.action == 'stage':
        if not args.stage_dir: raise ValueError('--stage-dir is required')
        args.stage_dir.mkdir(parents=True, exist_ok=True)
        for name, data in patched.items(): atomic_write(args.stage_dir / name, data)
        print('Patched copies staged in ' + str(args.stage_dir.resolve()))
        return
    ensure_closed()
    if marker.exists():
        saved = json.loads(marker.read_text(encoding='utf-8'))
        if any(saved[name]['original'] != expected[name] or saved[name]['path'] != str(paths[name].resolve()) for name in paths):
            raise ValueError('Existing backup belongs to another installation. Keep it and use a fresh patch folder.')
    backup = ROOT / 'backup'
    backup.mkdir(exist_ok=True)
    manifest = {}
    for name, data in originals.items():
        destination = backup / name
        if destination.exists() and sha(destination.read_bytes()) != sha(data):
            raise ValueError('Refusing to overwrite a different backup: ' + name)
        atomic_write(destination, data)
        manifest[name] = {'path': str(paths[name].resolve()), 'original': sha(data), 'patched': sha(patched[name])}
    atomic_write(marker, json.dumps(manifest, indent=2).encode('utf-8'))
    completed = []
    try:
        for name, data in patched.items():
            if sha(paths[name].read_bytes()) != expected[name]: raise ValueError('File changed while patching: ' + name)
            atomic_write(paths[name], data)
            completed.append(name)
    except Exception:
        for name in completed: atomic_write(paths[name], originals[name])
        raise
    print('Patch installed. Original files are in ' + str(backup))
    print('Updates can replace this patch. Re-check before applying it again.')


if __name__ == '__main__':
    try:
        main()
    except Exception as error:
        print('ERROR: ' + str(error), file=sys.stderr)
        sys.exit(1)
