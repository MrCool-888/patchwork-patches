# Lunar Client Patchwork prerelease

This release provides importable `.patchwork.json` files for the inspected Windows launcher builds **3.7.17-ow** and **3.7.24-ow**, with multiver game commit **29a43e6**. Exact original SHA-256 fingerprints are required. Both packs are version **0.1.1** and require **Patchwork 0.9.1**. Minecraft **1.21.11 (v2.23.2-2641)** launched successfully with launcher 3.7.24-ow; locker rendering and local equip/unequip were verified on the user's Windows PC. Both packs passed transactions on copies. Multiplayer packet privacy remains unverified.

## Recover from the 0.1.0 launch failure

The old pack pinned an earlier game build. When Lunar's manifest expected the newer build, the exact-JAR guard stopped launch with `Local cosmetics patch: game file or version changed`. The error happened before Java started in the reproduced case.

1. Close Lunar, Minecraft and other Java processes. In Patchwork, use **History & restore** to restore the old Lunar session.
2. Launch the original Lunar Client once and let it finish its game update. Close Minecraft and Lunar again.
3. Update to Patchwork 0.9.1 and import the matching 0.1.1 pack. Preview and apply it using the steps below.

The current pack still rejects newer or damaged game builds. Restore before Lunar updates; another game build needs another matching pack. An already-updated or otherwise changed file can prevent safe history restore; retain the history/backups and resolve the changed file rather than forcing an overwrite.

## Install and test

1. Install [Patchwork 0.9.1 prerelease](https://github.com/MrCool-888/patchwork/releases/tag/v0.9.1) using its `Patchwork-Setup.exe`. Existing library/history data is retained.
2. In **Patch sources**, use `https://github.com/MrCool-888/patchwork-patches`, enable **Include pre-release patch packs**, and choose **Check now**. For a manual import, download the matching `LunarClient-3.7.24-ow.patchwork.json` or `LunarClient-3.7.17-ow.patchwork.json` and choose **Add patch file**.
3. Select the pack matching your launcher. The standard per-user installation is detected automatically. Its target folder is **your Windows user folder**, `%USERPROFILE%`, because the launcher and game are in separate subfolders. All listed operations remain inside that folder. A custom installation needs a rebuilt matching pack.
4. Before applying local cosmetics, unequip previously saved online cosmetics through the original client. Close Lunar Client, Minecraft and other Java processes.
5. Select **Remove ads and promotions** and **Local cosmetics**, review **Preview changes**, then **Apply patches**. Local cosmetics requires the launcher patch. The ad patch can also be used by itself.
6. Start Lunar normally. On Minecraft's main menu, click the **shirt icon in the bottom toolbar** to open **Locker**. Wait for the catalog to load, select **Local Outfit**, choose a cosmetic, then click **Equip**. Use **Unequip** to remove it. The catalog may initially show zero items while loading. Local outfits are stored in `%USERPROFILE%\.lunarclient\local-cosmetics\` in per-account protobuf files.

Use **History & restore** to restore the original launcher executable, archive and game JAR. Keep the Patchwork data/history folder until you restore. Backups take roughly 290–310 MiB. Restore refuses to overwrite files changed by another program or update. Deselecting local cosmetics and choosing **Update patches** restores the original game JAR and removes its launch guard while retaining the selected ad cleanup.

## Scope and limits

- The launcher patch disables the shared ad visibility/rendering paths used by launchpad, Mission Control/status, Satellite and Explore. It clears layout ads, news, featured servers, marketing links, video promotions, carousel, alerts and announcements; hides Apex Hosting, sidebar external/promotional items, radio upsells and inbox promotion rows. Clearing layout alerts also removes service announcements from that feed. Launchpad and Mission Control cleanup were observed live; full UI coverage is unverified. Minecraft's own main-menu promotions are outside this launcher/status patch.
- The game patch uses the existing all-cosmetics rendering path and intercepts outfit CRUD, outfit selection, favorites and badge equip before their mutation RPCs reach the network channel. It suppresses the inspected outgoing player cosmetic push. The helper needs the account's default outfit and available catalog assets. Favorite clicks are acknowledged without persistence. Emote/spray services are outside scope.
- **Previously saved online outfits are unaffected and may remain visible to other players.** Login and asset downloads still use Lunar's servers. Incoming remote cosmetic refresh behavior is unverified. This is an experimental local handler, with no promise of complete anonymity or multiplayer acceptance.
- Modified launcher archives require disabling one executable Electron ASAR integrity fuse. This invalidates the executable's publisher signature and removes embedded ASAR validation. Other fuses are preserved. See [Electron's fuse documentation](https://www.electronjs.org/docs/latest/tutorial/fuses).
- The launcher's game check accepts only the exact patched JAR when the original expected game hash matches. Missing, changed or new game builds stop launch. **Restore before updating Lunar.** Launcher updates can overwrite this patch. New originals require rebuilt packs.

## Evidence

Patchwork's 47-test suite passed, including new binary/JAR validation, deterministic output, upgrade/removal, failure rollback and exact restore. Each real pack parsed and previewed three files; apply/rollback/update/restore passed on full-sized copies. Unaffected archive members were compared byte-for-byte, edited JavaScript passed syntax checks, JAR CRCs passed, and exactly one executable byte changed. Only three original game classes changed and two independently written helper classes were added. The actual modified Java service stubs passed local CRUD/persistence/unlock tests with JVM verification; only login reached the fake network channel. Both launcher ad hooks and exact patched-JAR guards passed isolated JavaScript tests.

On 2026-10-10, the old launch failure was reproduced and then resolved by restoring, updating the original game and applying 0.1.1. The patched game reached the main menu. Locker loaded **5,665 cosmetics**; **2021 City Bandanna** rendered, equipped and unequipped in Local Outfit. Its local protobuf file changed for both actions. The test selection was removed afterward. No multiplayer server or world was joined, and no packet capture was performed. Launcher 3.7.17 was tested on copies only.

Source and reproducible pack instructions are in `source/lunar` in the tagged patch repository and source release asset. No Lunar executables, original game classes, archives, models or textures are distributed.
