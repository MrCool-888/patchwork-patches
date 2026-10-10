# Validation: Blitz Clean Desktop 1.1.0 / Patchwork 0.8.2 prerelease

## Startup repair and update policy — October 10, 2026

The original 1.0.0 pack omitted Blitz's native archive checksum and is superseded. The reported installed-client startup exited with **E6**. Static inspection of `blitz_core.node` established that its native check compares XXH32 (seed zero) of the complete `resources/app.asar` with the final four little-endian bytes of `icudtl.dat`. The original archive checksum is `97f3905b`, matching that footer; the old patched archive checksum was `aa3155e5`, leaving a mismatch. The fix updates the checksum rather than disabling the native comparison. No native module or executable is patched.

Original companion: 10,468,212 bytes, SHA-256 `b816e1c340fbcb32c3a5dec883270aa44b929a2dc9bf4de6270fe8aee27757ad`; footer offset 10,468,208. Every operation pins the original archive/member or companion fingerprint. Automatic update guards also edit `src/autoUpdater/index.js`, `src/blitz-entry.js` and `src/ota.js`. `src/createWindow.js` retains the prior desktop hook and compact sizing changes.

All **46 Patchwork self-tests pass, zero failures**. New coverage checks reference vectors, the real-size companion, strict paths/offset/algorithm/fingerprints, shared-operation composition, older ASAR-only history upgrades, two-file interrupted writes and rollback, selection changes and exact restoration. Actual original archive/companion copies pass independent preview/apply/update/recovery/worker/stale-preview/restore checks.

Independent Python xxHash verification confirms the rebuilt archive/footer values: both selections `cc7b10e7`, clean only `8c117863`, compact only `00e3fe28`. The companion changes only its final four bytes. Node checks retain all 3,051 members and verify untouched contents/metadata, member integrity and JavaScript syntax. Updated lifecycle, compact-size and headless browser-layout fixtures pass.

Update-policy fixtures execute the actual changed client/OTA functions. Default startup and polling produce no automatic update requests. Manual checks remain callable. `BLITZ_AUTO_UPDATE=1` explicitly restores automatic checks. Identical policy/checksum operations in both selectable patches compose once.

The corrected patch was **not applied to the installed Blitz client**, at the user's request. Live corrected startup remains unverified. An isolated native bootstrap attempt did not reproduce complete Blitz initialization and cannot establish live startup success. Earlier native Electron hook tests below did not exercise the native Blitz archive check; that limitation caused the first pack to miss E6. Real account, login/settings/game/update and monitor/DPI checks remain pending.

## Historical 1.0.0 / 0.8.0 hook and archive evidence

Windows x64, October 10, 2026. Source baseline: [MrCool-888/patchwork](https://github.com/MrCool-888/patchwork), commit `e777596adc92f888a25aca938fd03984245060ee`. Patchwork 0.8.0 and the combined patch pack are experimental prereleases. Validation did not apply changes to the installed Blitz client.

## Target evidence

Blitz.exe file version: 3.0.7.134. App package: 3.0.7. Frontend: 3.0.8-ota.0. The installed archive is 18,257,553 bytes with SHA-256 `44132b54235250bc4db85c09c80a247ce20d4fac95476bb10244bb0ecf344193` and 3,051 members. Every operation also pins the original `src/createWindow.js` fingerprint. Blitz's bundled runtime is Electron 31.2.1 / Node 20.15.0 / Chromium 126.0.6478.127.

The original executable's embedded-ASAR-integrity enforcement fuse is disabled. The pack leaves the executable unchanged; this result does not establish compatibility with other Electron applications or Blitz builds.

Verified loader rules are `dn0qt3r0xannq.cloudfront.net/blitz-ONuZ1Ty9qx/.../prebid-load.js` and `live.primis.tech/live/liveView.php`. Matching is limited to registered supported desktop-view IDs. The original client has no competing `onBeforeRequest` listener at this hook; other request-event handlers remain in place. The hook installs before the view loads and registers once per session.

Cleanup requires HTTPS, exact frontend path prefix `/v3.0.8-ota.0/` (or its root), and a recognized desktop hostname: blitz.gg, blitzapp.gg, agentselect.net, championselect.net, lolstats.com, probuilds.net or tftcomps.gg. Overlay-route URLs and other views are excluded. Unknown versions disable web cleanup and emit a compatibility diagnostic.

## Passed checks

| Area | Result and scope |
| --- | --- |
| Existing patcher regressions | All 41 original self-test scenarios pass. |
| New ASAR/client-closure tests | Three new scenarios pass: archive transactions; parser/integrity/count/path bounds; a generated running Blitz process blocks transactions. Total: **44 passed, 0 failed**. |
| ASAR parsing and rebuilding | Length changes, UTF-8 BOM, full/block SHA-256 integrity, exact matches, composition/order independence, conflicting edits, archive/member fingerprint mismatches, invalid headers/bounds, rejected paths, unpacked/link edit refusal and size limits. Ordinary text still has its original 8 MiB limit; archive limit is 64 MiB. |
| Actual archive transactions | On isolated copies: read-only preview; apply both selections and each selection individually; selection updates; induced update failure and rollback; prepared recovery; tampered/stale target refusal; worker apply/restore. Restore returns the exact original archive bytes. |
| Independent archive verification | A Node reader verifies all 3,051 paths, untouched contents and metadata, preserved unpacked/link metadata, updated offsets and edited-member integrity. All three selected outputs parse as JavaScript. |
| Actual bundled Electron reader | The original bundled Electron runtime, used in Node mode, reads the rebuilt member through its native ASAR filesystem and compiles its JavaScript successfully. This does not launch the Blitz UI. |
| Hook lifecycle fixtures | Request cancellation, allowed API URLs, unrelated view IDs, one listener per session, unknown frontend/foreign domain/overlay-route fallback, CSS replacement on load and SPA navigation, and destroyed-view cleanup/recreation. |
| Native Electron HTTPS fixture | An isolated copy of the bundled runtime loads a local HTTPS fixture with real BrowserViews. A separate live view pre-populates memory/disk caches. Supported startup, reload, full/SPA navigation and recreation pass: **10 ad requests cancelled**, **1 filter registration**, **38 calls** to the existing response handler. Unknown-version and unrelated views load their original content and ad fixtures. |
| Layout and hit targets | Native Chromium 126 and separate headless Chromium 151 fixtures collapse verified selectors, reclaim more than 300 pixels of rail space, remove hidden bounding boxes/hit targets, and hide dynamically added slots. Login/settings/news/billing fixture controls remain available. |
| Compact window | Extracted changed code runs for logged-out/free/premium labels and normal/small screens: 1920×1080 → 940×500; 800×600 → 750×500; 300×300 → 250×250. Stored larger minimums are ignored. An actual native BrowserWindow reports a 940×500 minimum. |
| Installer | Production app and installer compile as 0.8.0. **10 update-lock checks pass** on workspace installations with registry/shortcut integration disabled. Test code and patch packs are excluded from the seven-file installer payload. |

The native HTTPS fixture maps only its test hostnames to loopback and trusts only its generated test certificate in an isolated partition. No public ad, authentication or update services are contacted by that fixture. It uses a private workspace profile and hidden windows. The original installed app, its archive, account data and patcher installation were not modified by validation.

## Warm-cache implementation finding

Electron 31.2.1 can execute memory-cached scripts without invoking the request callback. Clearing the HTTP cache alone did not cover that case. The final hook uses per-WebContents `Network.setCacheDisabled` before supported loads, and bypasses cache on explicit reloads. A new view queues that command before its initial load; subsequent supported loads await it. Full supported document navigation is routed through the same loader. Unknown frontends restore cache behavior and detach the hook's owned debugger connection.

This can increase full-load network traffic. DevTools can detach the cache-control connection; the hook logs a diagnostic and reinstates it when DevTools closes. Interactive DevTools use and coexistence with other third-party debugger clients have not been validated. Request filtering and CSS remain separate from that cache control.

## Remaining live validation

These tests establish transaction safety, Electron archive parsing and controlled hook behavior. They did not establish acceptance by Blitz's separate native checksum check or a complete live session. Still pending for the corrected pack: startup using the real remote frontend, actual logged-out/free/premium sign-ins, login and settings workflows, real game pages, the update service, monitor/DPI transitions and game-overlay interaction. Account labels in fixtures are not real account tests. Game overlays and additional premium features are not included.

Frontend updates can arrive independently of the installed archive. Unknown frontend versions intentionally retain their original web behavior. Client updates can replace the archive, and a different original fingerprint needs a matching pack. Preserve backups/journals and close Blitz before any transaction.

## Source references

- [Patchwork format baseline](https://github.com/MrCool-888/patchwork/blob/e777596adc92f888a25aca938fd03984245060ee/PATCH-FORMAT.md)
- [Electron ASAR format and integrity](https://github.com/electron/asar)
- [Electron ASAR integrity enforcement](https://www.electronjs.org/docs/latest/tutorial/asar-integrity)
- [Electron webRequest API](https://www.electronjs.org/docs/latest/api/web-request)
- [Electron debugger API](https://www.electronjs.org/docs/latest/api/debugger)
- [XXH32 specification](https://github.com/Cyan4973/xxHash/blob/dev/doc/xxhash_spec.md)
