"""Check the real cloth menu delta without redistributing original game code."""
import sys
import zipfile
from patch_lunar import CLOTH_MENU, patch_cloth_menu

with zipfile.ZipFile(sys.argv[1]) as original, zipfile.ZipFile(sys.argv[2]) as patched:
    before = original.read(CLOTH_MENU)
    expected, edits = patch_cloth_menu(before)
    after = patched.read(CLOTH_MENU)
    assert after == expected
    assert len(before) == len(after)
    assert len(edits) == 2
    differences = {i for i, (a, b) in enumerate(zip(before, after)) if a != b}
    assert differences == {offset + j for offset, _, _ in edits for j in range(3)}
    # Every branch, exception handler, stack frame and constant stays byte-for-byte intact.
    for offset, size, replacement in edits:
        assert before[offset] == 0xb6 and size == 3 and replacement == b'\x57\x04\x00'
    try:
        patch_cloth_menu(after)
        raise AssertionError('Already-patched checks were accepted')
    except ValueError:
        pass
print('PASS: two cloak-only checks, six changed bytes, unchanged class layout/frames, duplicate application rejected.')
