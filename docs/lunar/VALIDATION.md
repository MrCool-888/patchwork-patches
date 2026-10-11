# Lunar 0.1.3 validation — 2026-10-11

## Cloth cloak physics and centered Home

Pack 0.1.3 bumps `local-cosmetics` and `clean-launcher` to 0.1.2. The optional automatic-update patch remains 0.1.0. Both inspected launchers target the same original multiver build as 0.1.2. The newly patched game SHA-1 is `83127fc392298424f53eef71f6bdb8bb61a7a2b6`; both launchers' game guards require that exact result and the matching upstream hash.

The cloak settings class contains two subscription checks: its option builder resets the cloth flag for non-subscribers, and its update callback rejects changes. `patch_cloth_menu` replaces only these two `invokevirtual` checks with `pop; iconst_1; nop`. Both sequences have the same length and stack effect. All pool entries, method layouts, branches, exceptions and StackMapTables remain byte-identical. The existing renderer/simulator is unchanged and still respects the per-cloak flag and global Render Cloth Cloaks option. Global account/subscription state is unchanged. `test-cloth.py` verifies the actual six-byte delta and rejects an already-patched class.

Home uses scoped inline styles on its existing grid/columns: centered, 1200px maximum width, proportional Friends column, balanced gap, vertical alignment and bounded Friends height. The Friends component's full-viewport mode is disabled only on Home. Shared styles/components and other pages are unchanged.

Both full-sized packs passed three-file preview, deterministic JAR, partial-write rollback, direct 0.1.2→0.1.3 upgrade, updater removal/reselection, cosmetic removal and exact final restore. All unaffected archive/JAR members matched; edited JS syntax/integrity and JAR CRCs passed. Exactly one executable fuse byte changed, four original game classes changed and two independently written helpers were added. Modified service stubs passed local CRUD/persistence/unlock/corrupt-state tests under JVM verification. Both ad hooks, strict game guards and automatic/manual updater probes passed. These checks do not install real updates or connect to multiplayer servers.

Live Windows test on 3.7.24-ow: Minecraft 1.21.11 (v2.23.2-2641) launched successfully with the new game bytes. The existing Local Outfit's Kanji Ocean cloak was selected through Locker; gear → Cosmetic Settings → Cloth Cloak could be enabled. The per-account protobuf changed and contained the Struct boolean `clothCloak=true`; existing outfit items were retained. The checked option and rotating character preview were observed with no crash or subscription dialog. Minecraft was closed normally. No world/server movement test was performed, so cape dynamics during gameplay remain unverified.

Applied the final pack with the validated worker and restarted the installed launcher. Its archive/game matched the tested staged hashes. Home's launch card and Friends panel were centered at a normal 1455×801 window size and maximized 2560×1392 size, with controls visible and no overlapping panels. The three selections remain applied. Launcher 3.7.17 was tested on copies only. Friends with real contacts, active music and minimum-size windows were not checked live. Multiplayer privacy remains unverified.

The sections below preserve the earlier updater/crash-fix evidence; their hashes and class counts describe those earlier revisions.

## Automatic launcher updates

Pack 0.1.2 adds the optional `disable-auto-updates` patch (version 0.1.0) for both inspected launcher builds. Existing ads/cosmetics operations remain byte-identical to pack 0.1.1. It blocks non-manual checks in the shared updater method, disables `autoInstallOnAppQuit`, and blocks the server-push handler even when its `forceInstall` flag is true. Startup, timer and channel-change callers use the guarded method; manual IPC and user deep-link checks retain their original behavior. Game/runtime downloads and Overwolf updates are separate and unchanged.

`source/lunar/test-updates.cjs` executes each build's actual original and transformed updater class/server handler with an isolated updater. Both builds passed: original automatic/server calls reached the fake updater; patched startup/timer/channel/server calls made none; install on exit was disabled; automatic pending-download calls did not install; manual IPC check/pending install worked; timers preserved an in-flight manual request. These tests perform no network calls or real installations.

Both full-sized packs passed preview, deterministic JAR output, injected partial-write rollback, direct 0.1.1→0.1.2 upgrade, updater deselection/reselection, cosmetic removal and byte-exact final restore. Deselection recovered exactly the 0.1.1 patched archive. Archive integrity, JS syntax, unchanged members, JAR CRCs and the existing ad/game guard probes passed; the game digest is unchanged.

On the user's Windows PC, applied all three selections with Patchwork's validated worker and restarted Lunar 3.7.24-ow through computer use. Launchpad opened normally; its log recorded `Automatic launcher updates disabled by Patchwork.` at startup. Its archive exactly matched the tested staged result. The existing patched executable/game remained unchanged. Real update installation was not attempted; timer/channel/server paths and manual update behavior were tested in isolation. The three patches remain applied. The earlier Minecraft/locker test below was not repeated for this launcher-only revision.

## Reproduced failure and fix

The installed 3.7.24-ow launcher with pack 0.1.0 stopped before Java started: its manifest expected a newer game hash, while the pack guarded the earlier patched JAR. History restore recovered the original files. Launching the original client downloaded the current multiver build, after which unpatched Minecraft reached its main menu.

Pack 0.1.1 pins multiver commit `29a43e6149a6bcbeb472b83c0290fa807e351a17`, replaces the current cosmetic manager's outgoing-push method, and updates both launchers' exact-JAR guards. Original JAR SHA-256: `f4a6a62df2d548906f272d2d0dc906b6caae6f3592f9e2dae918783324bf3cca`. Original SHA-1: `3a30454c0e076f6feee2246c5969d006414f4307`. Patched SHA-1: `bd3d081b9c8556a1b0c98c156bc0c1b783ec5bad`.

Patchwork 0.9.1 also narrows the Lunar transaction process guard to Lunar and Java. Its user-folder target had incorrectly included an unrelated running Blitz tray process. Regression tests still reject running Lunar and preserve other applications' process guards.

## Automated checks

- Patchwork: 47 self-tests passed, zero failures. Installer update runner: 10 process/filesystem checks passed.
- Both 3.7.17 and 3.7.24 packs: full-sized three-file preview, deterministic JAR output, injected partial-write rollback, apply, cosmetic deselection/update and byte-exact restore passed on private copies.
- Every unchanged ASAR/JAR member matched. Edited JS passed syntax and integrity checks; JAR CRCs passed. Exactly one executable fuse byte changed, three original game classes changed and two independently written helper classes were added.
- Current modified service stubs passed JVM verification, local unlock, CRUD, persistence and corrupt-state tests. Only login reached the fake RpcChannel; cosmetic mutations did not.
- Both shared ad hooks returned false for inspected placements. Both game guards accepted the exact current patched JAR with the matching upstream hash, rejected changed/new game builds and preserved other artifact checks.

## Actual Windows UI test

Applied both patches with Patchwork 0.9.1 to the user's installed Lunar 3.7.24-ow. Launchpad and Mission Control cleanup were observed. Minecraft **1.21.11 (v2.23.2-2641)** reached its main menu. Locker loaded **5,665 cosmetics** and displayed Local Outfit. **2021 City Bandanna** rendered, equipped and unequipped successfully; the per-account local protobuf file changed after both actions. The test selection was removed and Minecraft was closed normally. The tested patches remain applied.

No multiplayer server or world was joined. No packet capture or incoming remote cosmetic refresh test was performed. Complete multiplayer privacy and server acceptance remain unverified. Launcher 3.7.17 was tested on copies only; full launcher UI coverage and Minecraft main-menu promotion cleanup are outside the live verification. Existing saved online outfits may remain visible to others. Restore before Lunar/game updates; exact fingerprints deliberately reject other builds.

No original Lunar binaries, game classes, assets, account data or test backups are included in this repository or release assets.
