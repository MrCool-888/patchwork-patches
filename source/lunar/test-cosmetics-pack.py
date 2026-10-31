"""Verify the complete current-game delta using private original/patched JARs."""
import base64
import hashlib
import json
import sys
import zipfile

pack = json.loads(open(sys.argv[3], encoding='utf-8').read())
ops = [op for patch in pack['patches'] for op in patch['operations'] if op['kind'].startswith('jar')]
assert len(ops) == 6
with zipfile.ZipFile(sys.argv[1]) as original, zipfile.ZipFile(sys.argv[2]) as patched:
    assert original.testzip() is None and patched.testzip() is None
    added = {op['entry'] for op in ops if op['kind'] == 'jarEntryAdd'}
    changed = {op['entry'] for op in ops if op['kind'] == 'jarEntrySplice'}
    assert set(patched.namelist()) - set(original.namelist()) == added
    assert set(original.namelist()) - set(patched.namelist()) == set()
    actual = {name for name in original.namelist() if original.read(name) != patched.read(name)}
    assert actual == changed and len(changed) == 4
    for op in ops:
        result = patched.read(op['entry'])
        if op['kind'] == 'jarEntryAdd':
            assert result == base64.b64decode(op['dataBase64'])
            continue
        assert hashlib.sha256(result).hexdigest() == op['resultSha256']
        if len(op['edits']) == 2:
            before = original.read(op['entry'])
            assert len(before) == len(result)
            offsets = {edit['offset'] + j for edit in op['edits'] for j in range(3)}
            assert {i for i, (a, b) in enumerate(zip(before, result)) if a != b} == offsets
            for edit in op['edits']:
                assert before[edit['offset']] == 0xb6
                assert edit['length'] == 3 and base64.b64decode(edit['dataBase64']) == b'\x57\x04\x00'
print('PASS all JAR members: four edited classes, two original helpers, six cloth-check bytes, valid CRCs.')
