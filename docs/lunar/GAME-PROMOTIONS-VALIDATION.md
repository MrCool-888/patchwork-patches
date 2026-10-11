# In-game promotion cleanup — 0.1.5

Date: 2026-10-11. Optional patch `clean-game-promotions` v0.1.0 in both Lunar packs, requiring Patchwork 0.9.2. Live testing of this revision is requested from the user; no computer-use game test or live patch installation was performed.

## Target and scope

The cached UI source ZIP was verified against source SHA-1 `b6d39b271abae35e95be9bdadf2270e908d59548`. Its extracted `static/index.js` matched the live original file byte-for-byte: 8,707,460 bytes, SHA-256 `5dfea0b2b0c331971827d97f4a8ee12b2406d005e9d5e954180ce5db328ac7db`. The patch requires this exact original JS fingerprint and eleven unique text anchors.

The inspected main-menu bridge identifies Store as `settings.labels.store`, Discover as `gui.components.discover`, and the two play buttons as `gui.components.singleplayer`/`gui.components.multiplayer`. The separate bottom navigation includes `gui.components.btnLocker` and settings. The patch filters the two promotional main buttons from the payload; it preserves bottom navigation. It omits the news carousel, coin purchase button, Medal quest/pending reward banners, video/Discord reward cards, countdown, Wrapped CTA and partner radio widget at their render callers. These components do not mount; their original internal hooks remain untouched.

The Locker shop/Purchase Lunar+ footer is omitted while sidebar categories, local outfits, equipment settings and owned Lunar+ colors remain. Store-preview checkout/back-to-store flags become false; preview, return-to-game and exit controls remain. Theme decorations/selection, ordinary alerts, gift metadata, multiplayer server lists and unrelated overlays are preserved. No new pause-menu patch is supplied: this web bundle has no identified pause route. Generic alerts/toasts lack a reliable promotion classification, so blanket suppression was avoided. Additional uninspected promotional surfaces may remain.

## Automated results

- **Patchwork 0.9.2: 49 tests passed, zero failed.** New coverage exercises a greater-than-8-MiB JS resource through preview, multi-file failure rollback, apply, direct update and byte-exact restore. JS input/output is bounded at 16 MiB; ordinary text remains bounded at 8 MiB. Invalid UTF-8/NUL, unsupported script extensions and oversize files/results are rejected. Exact hashes/counts and existing target protections remain mandatory.
- **Actual component render/click tests passed.** Original and patched bundles compile with Node's JavaScript parser. Extracted original/patched functions run in isolated local React/bridge stand-ins. Fixtures include both pending-reward/Medal states and video/Discord branches. Original promotional controls are present in the control fixtures and absent after patching. Singleplayer/Multiplayer click IDs, Locker navigation, theme selector, service alert and preview exit/return/preview paths remain. Non-main-menu child content remains. These stand-ins do not establish visual layout or embedded-browser compatibility.
- **Both full-sized packs passed** complete preview, deterministic JAR output, partial-write rollback, direct 0.1.4→0.1.5 upgrade, updater/startup deselection and reselection, promotion deselection/reselection, local-cosmetics removal and exact restoration of all five original files. Promotion-only apply/restore changes exactly one file. Deselecting promotion cleanup restores the original UI with the other four options retained.
- The existing four patch definitions match 0.1.4 exactly. Their composed executable, ASAR, game JAR and Genesis JAR outputs are byte-identical to 0.1.4. New deterministic JS output matches the independently applied source replacements. Unsupported/already-patched UI input is rejected by the source helper.

The initial sandbox test run blocked Windows `File.Replace` for scratch fixtures; rerunning the same test binary with normal Windows filesystem access passed all 49 tests. No production engine workaround was added for that sandbox restriction.

## Cache behavior and limitations

Both inspected launcher UI stages verify the cached source ZIP, then reuse the extracted source when the `.sha1` marker matches and expected ZIP files are present. Their `findMissingZipFiles` implementations check file existence. The assets index does not list `static/index.js`, so this source-file edit is not an indexed image/font asset edit. The patch changes none of these checks or markers, and adds no verifier exception. Missing files, cache re-extraction/deletion or a new UI source hash can overwrite or bypass the cleanup; restore before Lunar updates or cache clearing. A different UI build needs a newly inspected matching pack. Retain Patchwork 0.9.2 or newer to restore the large JS backup.

The new patch does not modify the launcher executable/archive, game classes, bootstrap, account data, local outfit files or network methods. Prior game-launch and cloth-cape checks remain historical evidence for the unchanged four options. Minecraft launch, visual spacing at different GUI scales, native pause-menu content, catalog/equip behavior and persistence across a real relaunch still need the user's test. Multiplayer cosmetic privacy remains unverified.

## Reproduce

Use legitimate local original files; no original UI bundle or Lunar binaries are release assets. Place the verified extracted UI's `static/index.js` beside `app.asar`, `Lunar Client.exe`, `lunar.jar` and `genesis-0.1.0-SNAPSHOT-all.jar` in the private originals directory. The generator rejects an incorrect UI fingerprint.

```powershell
python source/lunar/build-pack.py --version 3.7.24-ow --original-dir PRIVATE_ORIGINALS --output-dir PRIVATE_OUTPUT
# Repeat with 3.7.17-ow and its inspected original launcher files.
```

Build Patchwork 0.9.2 with `source/build.ps1 -Console -Tests -OutputDirectory PRIVATE_TEST_BIN`, then run `Patchwork.exe --self-test`. Compile `source/lunar/ValidatePack.cs` against that test executable and run it with `PACK_JSON ORIGINALS_DIR FRESH_SCRATCH_DIR PREVIOUS_0_1_4_PACK_JSON`. Its staged binaries stay private. Run:

```powershell
node source/lunar/test-promotions.cjs PRIVATE_ORIGINALS/index.js PRIVATE_SCRATCH/stage/index.js
```

## Requested user test

Install Patchwork 0.9.2, import/check for the matching Lunar 0.1.5 pack, close Lunar/Minecraft/Java, retain your choices, add **Remove in-game promotions**, preview and update. Launch your usual version and wait for the main menu. Confirm shop/news/coins are gone, then open Locker and confirm the shop link is gone while Local Outfit, Equip/Unequip and Cloth Cloak still work. Close and relaunch once to check persistence. Report version, any remaining promotion, layout problems or broken controls. Results are not yet available.
