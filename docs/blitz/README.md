# Blitz Clean Desktop pack 1.0.0

Requires [Patchwork 0.8.0 prerelease](https://github.com/MrCool-888/patchwork/releases/tag/v0.8.0). The combined patch-pack prerelease contains this Blitz pack and the unchanged Proton VPN 1.5.1 pack. Validation used workspace copies; the installed Blitz archive was not modified.

## Included patches

Select either patch independently, or select both.

| Patch | Changes |
| --- | --- |
| Clean desktop | Cancels the verified CloudFront display-ad and Primis video-ad loaders; hides verified ad slots, upgrade controls and scoped promotional banners; sets the desktop rail width and gap to zero. |
| Compact window | Uses the existing 940 × 500 premium minimum for every account, bounded by screen size. It ignores saved larger minimums without rewriting stored preferences or account roles. |

Web cleanup supports frontend **3.0.8-ota.0** on Blitz's recognized HTTPS desktop domains. Unknown frontends retain their original behavior and emit a `[Patchwork]` compatibility diagnostic in Blitz's log. Game overlays are outside this pack's scope. Compact sizing is a local window change; other premium features are not included.

Supported desktop views bypass Chromium's memory/disk cache so cached ad scripts reach the request filter. Full page loads may download more data. Other views keep their cache behavior. The hook uses Electron's debugger API for cache control; opening DevTools can detach it. Close DevTools and reload if Blitz logs a cache-bypass diagnostic.

## Exact compatibility

- Installed executable version: **3.0.7.134**; application package version: **3.0.7**.
- Original target: `resources/app.asar`, **18,257,553 bytes**.
- Original archive SHA-256: `44132b54235250bc4db85c09c80a247ce20d4fac95476bb10244bb0ecf344193`.
- Edited member: `src/createWindow.js`; its exact original fingerprint is included in every operation.

The version label alone is insufficient. A different archive requires a matching pack. Client updates may replace the archive; frontend updates may require revised selectors and loader rules.

## Install and apply when you choose

1. Download and run `Patchwork-Setup.exe` from the patcher's 0.8.0 prerelease to install/update it for your Windows user. This is an unsigned development build. The installer contains the patcher; the packs are separate files.
2. In **Patch sources**, add `https://github.com/MrCool-888/patchwork-patches` with **Include pre-release patch packs** enabled. This imports both app bundles. For manual import, choose **Add patch file** and import `Blitz-3.0.7.patchwork.json`.
3. Select the Blitz application folder, normally `%LOCALAPPDATA%\Programs\Blitz`. Select the folder containing `Blitz.exe`, rather than the `resources` subfolder.
4. Select **Clean desktop**, **Compact window**, or both. Choose **Preview changes** and review the archive member's before/after code. Both patches contain executable JavaScript that runs later inside Blitz. Preview leaves Blitz files unchanged.
5. Exit Blitz from its tray menu and wait for its processes to close. Choose **Apply patches**, then reopen Blitz. Patchwork requires Blitz to be closed before apply, selection updates and restore.

To change the selection later, choose the desired patches and review the update preview. Patchwork rebuilds from verified originals and removes deselected changes during the update transaction.

## Restore and interrupted transactions

Exit Blitz, open Patchwork's **History**, and restore the latest applied session. Original archive bytes are backed up and verified; restore reproduces the original archive byte for byte. A failed update rolls back to the previous selection; an interrupted session can recover through History.

Keep `%LOCALAPPDATA%\Patchwork\Data`: it contains your backups and journals. Uninstalling Patchwork preserves that data and does not itself restore Blitz. Outside edits or an app update can cause restore to refuse rather than overwrite unexpected files; use a matching current pack or repair the app with its normal installer.

## Evidence and remaining limits

All **44 Patchwork self-tests**, isolated transactions on the actual archive, independent archive checks, native Electron warm-cache fixtures, layout fixtures, and **10 installer update-lock checks** pass. Full live Blitz behavior with real logged-out/free/premium accounts, login/settings/game flows and the update service remains untested. See [validation notes](VALIDATION.md) for the exact distinction between native fixtures and live-app validation.

`Patchwork-Patches-Source.zip` contains both packs' source, including the Blitz generator, desktop hook and validation runners. The modified patcher source is in its own repository/release. `SHA256SUMS.txt` records the patch-release asset hashes.
