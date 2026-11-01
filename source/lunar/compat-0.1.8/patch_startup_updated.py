"""Fingerprint-bound edits to Lunar's shared bootstrap HTTP downloader."""
import base64
import difflib
import hashlib
import struct

GENESIS_SHA256 = 'c3bde53edad048d9b21cf9e42a0797cdbe9f6e62c1c3ba07a30343f75077605c'
GENESIS_SHA1 = 'ce6fd0c7bc1baada907f85927d469e084192636c'
PATCHED_GENESIS_SHA1 = '05a965eb4d383e28341264787b30505c7479ef3c'
ORIGIN_HELPER = 'local/lunar/BootstrapDownloads'
HTTP_ENTRY = ('com/moonsworth/lunar/ICCOIIRCOIOHHHHCIRHHIRICRCROOO/'
              'ICCOCOHOCCOROROIRHCCRHICOOOHHC/RHIOIIOORCOOCCRCHOHHOOIIHRHCIH.class')
HTTP_SHA256 = '49f03b3ba8f560217017a1df09b01a7627b0be99a9dd809dad539408c202cae9'


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
                if code.hex() != '140037b80059b30039b20039b3003bb80052b20039b9005f0200b8005bb900600200b2003cb900610200b9005e0100b3003ab1':
                    raise ValueError('Unsupported HTTP client initialization')
                long_index = len(cf.cp)
                cf.cp.extend([b'\x05' + struct.pack('>q', 15000), None])
                # Request timeout remains 180s; only opening a connection is limited to 15s.
                code = (code[:6] + b'\xb3\x00\x3b' + b'\x14' + struct.pack('>H', long_index)
                        + b'\xb8\x00\x59\xb3\x00\x39' + code[15:])
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
