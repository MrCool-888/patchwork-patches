"""Fingerprint-bound edits to Lunar's shared bootstrap HTTP downloader."""
import base64
import difflib
import hashlib
import struct

GENESIS_SHA256 = '40797ad1d1449ffff7c6e7342b79a3edc8d08517505dc2d57ce9d97e0669389c'
GENESIS_SHA1 = 'ea2f19ae5fcb3941df77637b184a1ff02157d6b1'
PATCHED_GENESIS_SHA1 = '12ec07a090a9d071363d1884a958682cd00c7d91'
ORIGIN_HELPER = 'local/lunar/BootstrapDownloads'
HTTP_ENTRY = ('com/moonsworth/lunar/ICCRRCCIOOIRICIHHCIOROHHROOHHR/'
              'RCHICCCIHRCRCHCIRRIIRRHICORHHO/OOIOHHHCIICRRRCICOIIIRRRICOCIC.class')
HTTP_SHA256 = '2a632ea44b11be790f80d530a3e31828dcff2cf97dbf05e2cfbed35db8bf4cdc'


def patch_http(data, class_file):
    if hashlib.sha256(data).hexdigest() != HTTP_SHA256:
        raise ValueError('Unsupported bootstrap HTTP class')
    cf = class_file(data)
    sends = []
    for index, entry in enumerate(cf.cp):
        if entry and entry[0] == 10:
            owner, nt = struct.unpack_from('>HH', entry, 1)
            owner_name = cf.utf(struct.unpack_from('>H', cf.cp[owner], 1)[0])
            name, descriptor = struct.unpack_from('>HH', cf.cp[nt], 1)
            if (owner_name, cf.utf(name), cf.utf(descriptor)) == (
                    'java/net/http/HttpClient', 'send',
                    '(Ljava/net/http/HttpRequest;Ljava/net/http/HttpResponse$BodyHandler;)Ljava/net/http/HttpResponse;'):
                sends.append(index)
    if len(sends) != 1:
        raise ValueError('Unsupported bootstrap HTTP send reference')
    owner = cf.add(b'\x07' + struct.pack('>H', cf.utf_add(ORIGIN_HELPER)))
    nt = cf.add(b'\x0c' + struct.pack('>HH', cf.utf_add('send'), cf.utf_add(
        '(Ljava/net/http/HttpClient;Ljava/net/http/HttpRequest;Ljava/net/http/HttpResponse$BodyHandler;)Ljava/net/http/HttpResponse;')))
    send_ref = cf.add(b'\x0a' + struct.pack('>HH', owner, nt))
    found = set()
    for kind, name, descriptor, attrs in cf.members:
        if kind != 'method':
            continue
        for start, end, attr, index in attrs:
            if attr != 'Code':
                continue
            stack, locals_count, size = struct.unpack_from('>HHI', cf.tail, start + 6)
            offset = start + 14
            code = bytes(cf.tail[offset:offset + size])
            if descriptor == '(Ljava/util/List;Ljava/lang/String;Ljava/util/function/Predicate;)V':
                if code.count(b'\x10\x07\xa4') != 2:
                    raise ValueError('Unsupported repository retry loops')
                call = b'\xb6' + struct.pack('>H', sends[0])
                if code.count(call) != 1:
                    raise ValueError('Unsupported Maven HTTP send call')
                # Each existing do/while executes once; bytecode offsets and frames stay valid.
                code = code.replace(b'\x10\x07\xa4', b'\x02\x00\xa4')
                # Same stack effect and instruction length; retain exception tables/frames.
                cf.tail[offset:offset + size] = code.replace(call, b'\xb8' + struct.pack('>H', send_ref))
                found.add('retries')
            elif name == '<clinit>':
                if code.hex() != '140037b80059b3003bb2003bb3003ab80052b2003bb9005f0200b8005bb900600200b2003cb900610200b9005e0100b30039b1':
                    raise ValueError('Unsupported HTTP client initialization')
                long_index = len(cf.cp)
                cf.cp.extend([b'\x05' + struct.pack('>q', 15000), None])
                # Request timeout remains 180s; only opening a connection is limited to 15s.
                code = (code[:6] + b'\xb3\x00\x3a' + b'\x14' + struct.pack('>H', long_index)
                        + b'\xb8\x00\x59\xb3\x00\x3b' + code[15:])
                clinit = (code, stack, locals_count)
                found.add('connect')
    if found != {'retries', 'connect'}:
        raise ValueError('Bootstrap download methods missing')
    changed = cf.replace_methods({('<clinit>', '()V'): clinit})
    edits = [dict(offset=a, length=b-a, dataBase64=base64.b64encode(changed[c:d]).decode('ascii'))
             for tag, a, b, c, d in difflib.SequenceMatcher(None, data, changed, autojunk=False).get_opcodes()
             if tag != 'equal']
    check = bytearray(data)
    for edit in reversed(edits):
        check[edit['offset']:edit['offset'] + edit['length']] = base64.b64decode(edit['dataBase64'])
    if check != changed or len(edits) > 24:
        raise ValueError('Bootstrap splice verification failed')
    return changed, edits
