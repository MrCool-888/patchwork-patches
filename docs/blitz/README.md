# Blitz Clean Desktop pack 1.1.1

Requires [Patchwork 0.8.2 prerelease](https://github.com/MrCool-888/patchwork/releases/tag/v0.8.2). The [combined prerelease](https://github.com/MrCool-888/patchwork-patches/releases/tag/blitz-3.0.7-proton-5.1.8-r3) contains this Blitz pack and the unchanged Proton VPN 1.5.1 pack. Version 1.1.1 removes the remaining **Go ad free** banners and their reserved space on player-history and match-detail pages. It retains 1.1.0's native companion-checksum repair and automatic-update policy. Validation used workspace copies; this update was not applied to the installed Blitz client.

## Included patches

Select either patch independently, or select both.

| Patch | Changes |
| --- | --- |
| Clean desktop | Cancels the verified CloudFront display-ad and Primis video-ad loaders; hides verified ad slots, upgrade controls and scoped promotional banners, including Go ad free banners on history/match pages; removes their reserved containers and sets the desktop rail width and gap to zero. |
| Compact window | Uses the existing 940 × 500 premium minimum for every account, bounded by screen size. It ignores saved larger minimums without rewriting stored preferences or account roles. |

Web cleanup supports frontend **3.0.8-ota.0** on Blitz's recognized HTTPS desktop domains. Unknown frontends retain their original behavior and emit a `[Patchwork]` compatibility diagnostic in Blitz's log. Game overlays are outside this pack's scope. Compact sizing is a local window change; other premium features are not included.

Selecting either patch disables automatic Blitz client and frontend update checks at startup and during polling. Manual checks in Blitz's tray/UI remain available. To explicitly restore automatic checks while retaining the patches, launch Blitz with the environment variable `BLITZ_AUTO_UPDATE=1`. Applying neither patch, or restoring the originals, restores the original update behavior. A manually installed update may replace patched files and require a matching new pack.

Supported desktop views bypass Chromium's memory/disk cache so cached ad scripts reach the request filter. Full page loads may download more data. Other views keep their cache behavior. The hook uses Electron's debugger API for cache control; opening DevTools can detach it. Close DevTools and reload if Blitz logs a cache-bypass diagnostic.

## Exact compatibility

- Installed executable version: **3.0.7.134**; application package version: **3.0.7**.
- Original target: `resources/app.asar`, **18,257,553 bytes**.
- Original archive SHA-256: `44132b54235250bc4db85c09c80a247ce20d4fac95476bb10244bb0ecf344193`.
- Edited members: `src/createWindow.js`, `src/autoUpdater/index.js`, `src/blitz-entry.js` and `src/ota.js`; each operation pins its exact original fingerprint.
- Companion target: `icudtl.dat`, **10,468,212 bytes**; SHA-256 `b816e1c340fbcb32c3a5dec883270aa44b929a2dc9bf4de6270fe8aee27757ad`.
- Companion change: only the final four bytes, offset **10,468,208**, hold XXH32 (seed zero) of the rebuilt archive in little-endian order. The original value is `97f3905b`.

The version label alone is insufficient. A different archive requires a matching pack. Client updates may replace the archive; frontend updates may require revised selectors and loader rules.

## Install and apply when you choose

1. Download and run `Patchwork-Setup.exe` from the patcher's 0.8.2 prerelease to install/update it for your Windows user. This is an unsigned development build. The installer contains the patcher; the packs are separate files.
2. In **Patch sources**, add `https://github.com/MrCool-888/patchwork-patches` with **Include pre-release patch packs** enabled. This imports both app bundles. For manual import, choose **Add patch file** and import `Blitz-3.0.7.patchwork.json`.
3. Patchwork automatically detects a standard Blitz installation, normally `%LOCALAPPDATA%\Programs\Blitz`. Use **Browse** for a custom folder containing `Blitz.exe`.
4. Select **Clean desktop**, **Compact window**, or both. Choose **Preview changes** and review the archive members' before/after code and companion checksum. Both patches contain executable JavaScript that runs later inside Blitz. Preview leaves Blitz files unchanged.
5. Exit Blitz from its tray menu and wait for its processes to close. Choose **Apply patches**, then reopen Blitz. Patchwork requires Blitz to be closed before apply, selection updates and restore.

To change the selection later, choose the desired patches and review the update preview. Patchwork rebuilds from verified originals and removes deselected changes during the update transaction.

For an already-applied 1.0.0 or 1.1.0 pack: use Patchwork 0.8.2, choose **Patch sources → Check now**, then select the desired Blitz patches and **Preview changes → Update patches** with Blitz closed. Keep **Clean desktop** selected for banner cleanup. Verified old backups support a direct update; no manual restore is required.

## Restore and interrupted transactions

Exit Blitz, open Patchwork's **History**, and restore the latest applied session. Original archive and companion bytes are backed up and verified; restore reproduces both original files byte for byte. A failed update rolls back to the previous selection; an interrupted session can recover through History.

Keep `%LOCALAPPDATA%\Patchwork\Data`: it contains your backups and journals. Uninstalling Patchwork preserves that data and does not itself restore Blitz. Outside edits or an app update can cause restore to refuse rather than overwrite unexpected files; use a matching current pack or repair the app with its normal installer.

## Evidence and remaining limits

All **46 Patchwork self-tests**, isolated two-file transactions, independent archive/checksum checks, update-policy fixtures and layout/lifecycle fixtures pass. The unchanged desktop hook also has earlier native Electron warm-cache evidence. Corrected startup in the installed Blitz client and full real logged-out/free/premium account, login/settings/game/update workflows remain untested. See [validation notes](VALIDATION.md) for the exact scope.

`Patchwork-Patches-Source.zip` contains both packs' source, including the Blitz generator, desktop hook and validation runners. The modified patcher source is in its own repository/release. `SHA256SUMS.txt` records the patch-release asset hashes.
