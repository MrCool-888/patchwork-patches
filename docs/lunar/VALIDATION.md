# Lunar 0.1.2 validation — 2026-10-10

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
