# Lunar Client 0.1.8 candidate

This candidate updates the 3.7.29-ow pack for the refreshed game and Genesis 9.10.1 artifacts. Version 0.1.7 targeted an older cached game and bootstrap, so Lunar's current launch response caused its strict version checks to reject them before Java started. The guards remain strict and accept only the newly inspected upstream and patched SHA-1 pairs.

The separately observed 1.21.11 native crash occurred with the patches restored: Windows reported access violation 0xC0000005 in `lwjgl.dll`. Its cause is not established. An unpatched 1.8.9 launch reached the menu.

## Build

Python 3 and the included files are sufficient. Supply locally obtained, unmodified game and Genesis JARs; these are not included. The input pack is the included 0.1.7 JSON. The script verifies exact fingerprints and operation sites before emitting a new pack:

```powershell
python rebase-018.py --old-pack LunarClient-3.7.29-ow-0.1.7.patchwork.json --original-dir "path\to\originals" --output LunarClient-3.7.29-ow-0.1.8.patchwork.json
```

The builder carries over the existing launcher and UI changes, remaps the cosmetic push method, makes two same-length cloak-only permission edits, and updates the bootstrap downloader class while retaining the previous local helper classes. The original cosmetics service stubs and all direct protobuf classes referenced by the helper are byte-identical to the previous build. The bootstrap helper's single-URL method is unchanged.

## Verification

Patchwork's isolated five-file transaction passed preview, deterministic output, injected rollback, option removal/reselection and exact restore in a serial run. A separate run using the installed Patchwork 1.1.1 engine also passed apply, selection changes and exact restore. One earlier scratch run reported a changed-file guard during the transaction; that did not recur in the serial and production-engine runs. Both patcher versions generated the same guarded SHA-1s.

Actual downloader fixtures passed 14 assertions on Java 17, 21 and 25 against both original and candidate. Helper tests passed 15 checks on each runtime. The isolated cosmetics fixture passed local outfit CRUD, persistence, verification and no-mutation-RPC checks. ZIP CRC checks confirmed only the four intended game classes and one intended Genesis class changed, with two helper additions to each; the cloak class has precisely two three-byte edits. Launcher syntax and isolated strict hash-verifier/updater fixtures passed.

Live testing of this candidate, including the cloak UI and multiplayer cosmetic privacy, remains necessary. The launcher edit invalidates the executable signature and disables embedded ASAR integrity validation. Restore through Patchwork before updating Lunar; do not force restoration over files changed externally.
