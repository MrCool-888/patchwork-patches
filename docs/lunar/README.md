# Lunar Client Patchwork prerelease

This release provides importable `.patchwork.json` files for the inspected Windows launcher builds **3.7.17-ow** and **3.7.24-ow**, with multiver game commit **29a43e6**. Exact original SHA-256 fingerprints are required. Both packs are version **0.1.3** and require **Patchwork 0.9.1**. This revision adds built-in **Cloth Cloak physics** for local cosmetics and centers Home's launch card and Friends panel in a bounded two-column layout. The ad and local-cosmetics patches are now version 0.1.2; Disable automatic updates remains version 0.1.0. Both packs passed upgrade/rollback/removal/restore checks. Minecraft **1.21.11 (v2.23.2-2641)** launched successfully on the user's Windows PC; the local cloth toggle and centered normal/maximized launcher layout were checked there. Movement in a world and multiplayer packet privacy remain unverified.

## Recover from the 0.1.0 launch failure

The old pack pinned an earlier game build. When Lunar's manifest expected the newer build, the exact-JAR guard stopped launch with `Local cosmetics patch: game file or version changed`. The error happened before Java started in the reproduced case.

1. Close Lunar, Minecraft and other Java processes. In Patchwork, use **History & restore** to restore the old Lunar session.
2. Launch the original Lunar Client once and let it finish its game update. Close Minecraft and Lunar again.
3. Update to Patchwork 0.9.1 and import the matching current pack. Preview and apply it using the steps below.

The current pack still rejects newer or damaged game builds. Restore before Lunar updates; another game build needs another matching pack. An already-updated or otherwise changed file can prevent safe history restore; retain the history/backups and resolve the changed file rather than forcing an overwrite.

## Install and test

1. Install [Patchwork 0.9.1 prerelease](https://github.com/MrCool-888/patchwork/releases/tag/v0.9.1) using its `Patchwork-Setup.exe`. Existing library/history data is retained.
2. In **Patch sources**, use `https://github.com/MrCool-888/patchwork-patches`, enable **Include pre-release patch packs**, and choose **Check now**. For a manual import, download the matching `LunarClient-3.7.24-ow.patchwork.json` or `LunarClient-3.7.17-ow.patchwork.json` and choose **Add patch file**.
3. Select the pack matching your launcher. The standard per-user installation is detected automatically. Its target folder is **your Windows user folder**, `%USERPROFILE%`, because the launcher and game are in separate subfolders. All listed operations remain inside that folder. A custom installation needs a rebuilt matching pack.
4. Before applying local cosmetics, unequip previously saved online cosmetics through the original client. Close Lunar Client, Minecraft and other Java processes.
5. Select **Remove ads and promotions**, optionally **Local cosmetics** and **Disable automatic updates**, review **Preview changes**, then **Apply patches**. The two optional patches require the launcher patch for its archive compatibility change. Disable automatic updates does not require local cosmetics. The ad patch can also be used by itself.
6. Start Lunar normally. On Minecraft's main menu, click the **shirt icon in the bottom toolbar** to open **Locker**. Wait for the catalog to load, select **Local Outfit**, choose a cosmetic, then click **Equip**. Use **Unequip** to remove it. The catalog may initially show zero items while loading. Local outfits are stored in `%USERPROFILE%\.lunarclient\local-cosmetics\` in per-account protobuf files.

Use **History & restore** to restore the original launcher executable, archive and game JAR. Keep the Patchwork data/history folder until you restore. Backups take roughly 290–310 MiB. Restore refuses to overwrite files changed by another program or update. Deselecting local cosmetics and choosing **Update patches** restores the original game JAR and removes its launch guard while retaining the selected ad cleanup.

## Enable cloth cape physics and update an existing patch

Import pack **0.1.3**, close Lunar/Minecraft/Java, retain your selections, preview and choose **Update patches**. Verified 0.1.2 histories upgrade directly. The layout change is included in **Remove ads and promotions**; cape physics is included in **Local cosmetics**.

In Minecraft, open **Locker** using the main-menu shirt icon, select **Local Outfit**, choose **Cloaks**, select and equip a cloak. Click the **gear button beneath its character preview**, then enable **Cloth Cloak** under Cosmetic Settings. The existing cloth simulator is used; no account subscription state changes. The checkbox remains optional and its value saves with your local outfit. The global **Render Cloth Cloaks** cosmetic setting must also be enabled. The stock tooltip mentions other players; this patch's inspected outfit mutations/outgoing push remain local, and the tooltip does not establish multiplayer privacy.

The local Cloth Cloak checkbox and persisted `clothCloak=true` protobuf value were verified with an equipped **Kanji Ocean** cloak. The character preview remained stable while rotating it. No world/server movement test was performed. The inspected renderer uses the existing cloth simulation when both settings are enabled.

## Disable automatic launcher updates

Import pack 0.1.3, select **Disable automatic updates**, preview and choose **Update patches** when an earlier pack is already applied. Verified 0.1.2 histories upgrade directly without restoring first. Deselecting this option and updating restores the original automatic updater while retaining your other selected patches.

The patch blocks automatic launcher checks at startup, on timers and after update-channel changes, including server-requested force-install checks. It also disables installation on app exit. **Manual Check for updates remains available. Restore all Lunar patches before using it to update the launcher.** This option does not freeze game/runtime downloads during Minecraft launch or Overwolf's own updater. The existing local-cosmetics game guard still rejects a new/damaged game build; matching new game builds need rebuilt packs.

Both inspected launchers passed isolated updater tests and direct upgrade/deselection/restore checks. On the user's PC, 3.7.24-ow opened normally after applying this option; its startup log confirmed `Automatic launcher updates disabled by Patchwork.` Real update installation was not attempted. See [validation details](https://github.com/MrCool-888/patchwork-patches/blob/lunar-client-0.1.3/docs/lunar/VALIDATION.md).

## Scope and limits

- The launcher patch disables the shared ad visibility/rendering paths used by launchpad, Mission Control/status, Satellite and Explore. It clears layout ads, news, featured servers, marketing links, video promotions, carousel, alerts and announcements; hides Apex Hosting, sidebar external/promotional items, radio upsells and inbox promotion rows. Clearing layout alerts also removes service announcements from that feed. Launchpad and Mission Control cleanup were observed live; full UI coverage is unverified. Minecraft's own main-menu promotions are outside this launcher/status patch.
- The game patch uses the existing all-cosmetics rendering path and intercepts outfit CRUD, outfit selection, favorites and badge equip before their mutation RPCs reach the network channel. It suppresses the inspected outgoing player cosmetic push and makes the local cloth option editable without changing global account flags. The helper needs the account's default outfit and available catalog assets. Favorite clicks are acknowledged without persistence. Emote/spray services are outside scope.
- **Previously saved online outfits are unaffected and may remain visible to other players.** Login and asset downloads still use Lunar's servers. Incoming remote cosmetic refresh behavior is unverified. This is an experimental local handler, with no promise of complete anonymity or multiplayer acceptance.
- Modified launcher archives require disabling one executable Electron ASAR integrity fuse. This invalidates the executable's publisher signature and removes embedded ASAR validation. Other fuses are preserved. See [Electron's fuse documentation](https://www.electronjs.org/docs/latest/tutorial/fuses).
- The launcher's game check accepts only the exact patched JAR when the original expected game hash matches. Missing, changed or new game builds stop launch. **Restore before updating Lunar.** Launcher updates can overwrite this patch. New originals require rebuilt packs.

## Evidence

Patchwork's earlier 47-test suite passed, including binary/JAR validation, deterministic output, upgrade/removal, failure rollback and exact restore. Both current packs passed three-file preview, apply/rollback, direct 0.1.2→0.1.3 update, option removal/reselection and exact restore on full-sized copies. Unaffected archive members were compared byte-for-byte, edited JavaScript passed syntax checks, JAR CRCs passed, and exactly one executable byte changed. Four original game classes changed and two independently written helper classes were added. The new cloak menu change replaces two three-byte permission checks with the same-length stack-equivalent instructions, preserving class layout, branches, exceptions and stack frames. Its regression probe checks the actual six-byte delta and rejects duplicate application. The actual modified Java service stubs passed local CRUD/persistence/unlock tests with JVM verification; only login reached the fake network channel. Both launcher ad hooks, exact patched-JAR guards and updater behavior passed isolated JavaScript tests.

On 2026-10-10, the old launch failure was reproduced and then resolved by restoring, updating the original game and applying 0.1.1. The patched game reached the main menu. Locker loaded **5,665 cosmetics**; **2021 City Bandanna** rendered, equipped and unequipped in Local Outfit. Its local protobuf file changed for both actions. The test selection was removed afterward. No multiplayer server or world was joined, and no packet capture was performed. Launcher 3.7.17 was tested on copies only.

Source and reproducible pack instructions are in `source/lunar` in the tagged patch repository and source release asset. No Lunar executables, original game classes, archives, models or textures are distributed.
