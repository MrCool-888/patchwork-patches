# Patchwork patches

Separate patch definitions for [Patchwork](https://github.com/MrCool-888/patchwork). Never bundled in the patcher installer.

Current pack: **1.3.0**, release **proton-vpn-5.1.8-r4**. Requires **Patchwork 0.6.0+** and original **Proton VPN for Windows 5.1.8.0**. Free selection is patch **1.2.0**, promotions is **1.1.0**, and other available patches are **1.0.0**. The pack and individual versions appear in the patcher library, preview and applied history.

## Install the patch pack

1. Update Patchwork to **0.6.0 or newer** before importing this pack. It adds the server-list and theme operations.
2. In **Patch sources**, paste `https://github.com/MrCool-888/patchwork-patches` and choose **Add source** with pre-releases enabled. It checks immediately and automatically about once an hour while open. With an earlier pack applied and verified Patchwork history, preview and choose **Update patches** directly; no manual restore is needed. Older 0.4.x patchers still require restore first.
3. For a manual import instead, download **ProtonVPN-5.1.8.patchwork.json** from this repository's release assets. The source ZIP is for developers.
4. Choose **Add patch file** in Patchwork and import the JSON. The stable bundle ID replaces the older catalog entry without creating a duplicate.
5. Select the compatible Windows 5.1.8.0 program folder, choose patches and review **Preview changes**. Close Proton before applying. Writes to Program Files use the Windows administrator prompt.

The original hashes determine compatibility. Different app versions need a matching new pack. Restore verified originals through History to remove the patches.

## Available patches

| Patch | Version | Local behavior |
| --- | --- | --- |
| Disable usage telemetry | 1.0.0 | Stops authenticated and pre-login statistical event senders. |
| Hide upgrade promotions | 1.1.0 | Hides connection-card, sidebar, profile-page and tray banners; removes settings subscription badges and advanced/default-connection/excluded-location upgrade cards. Settings links open normal pages. |
| Keep VPN Accelerator enabled | 1.0.0 | Keeps the effective setting and settings toggle on, shows On in connection settings, and opens its page without an upgrade prompt. Overrides a saved off value. Restore this patch to regain toggle control. |
| Remove local server change delay | 1.0.0 | Removes the desktop cooldown check and returns zero remaining delay. |
| LAN connections controls | 1.0.0 | Exposes advanced controls and reads the saved LAN toggle. |
| Custom DNS controls | 1.0.0 | Exposes advanced controls and reads the saved DNS toggle/list. |
| NetShield client controls | 1.0.0 | Opens widget/settings and reads the saved toggle. Server filtering still depends on the account. |
| Split tunneling client controls | 1.0.0 | Opens widget/settings and reads the saved routing toggle. |
| Connection preference controls | 1.0.0 | Displays default/excluded location controls and reads the saved default connection. |
| Connection profile controls | 1.0.0 | Displays creation/editing and removes the local profile row restriction. |
| Free country and individual server selection | 1.2.0 | Expanding a free country lists its individual free servers; Connect keeps the exact selected server ID. Search also works. |
| AMOLED dark theme | 1.0.0 | Forces Dark theme and uses black/near-black surfaces. |
| Custom accent color | 1.0.0 | Configurable primary/link/focus color and button text color. |
| Custom switch colors | 1.0.0 | Configurable enabled track and knob colors with native controls. |

Settings promotion removal changes presentation and navigation. Feature-specific effective settings remain separate patches, and the actual account plan is unchanged. Service, account and maintenance notices remain available. The shared advanced UI edit merges when several selected patches use it.

## Choose an individual free server

With a signed-in free account, expand a country in the normal country list and choose an individual free server’s **Connect** action. This fixes the upgrade prompt caused by the previous pack expanding countries into paid city rows. Free expansion uses the actual free-server loader and filters by country. Paid accounts retain city/state browsing.

You can also use **Search** with a full free-server name or prefix such as **NL-FREE**. The selected server ID is retained; an offline or missing selected server has no fallback. Paid server rows, cities and business gateway rows keep their existing restrictions for free accounts. Maintenance and protocol checks remain active.

The design follows the filtering and selected-location ideas in [Morphe's Android free-location patch](https://github.com/hxreborn/morphe-patches/blob/main/patches/src/main/kotlin/app/morphe/patches/protonvpn/misc/freeservers/ShowFreeServerLocationsPatch.kt). The Windows implementation is independently written; Android bytecode is not reused.

## Sign-in

**Guest VPN sessions remain unavailable.** The Windows compatibility attempt obtained a temporary unauthenticated session, but Proton’s credentialless endpoint rejected Windows with HTTP 422, Code 5003: **Platform expected to be in [Android, iOS]**. The session was revoked. Sign-in is still required. See [AUTH-RESEARCH.md](AUTH-RESEARCH.md) for the live result, official Android source trace and remaining client work.

## Custom themes

Select **AMOLED dark theme**, **Custom accent color**, and/or **Custom switch colors**. Selecting a color patch exposes hex fields and a **Choose…** color picker. Pick contrasting text/knob colors, preview, then apply. Colors persist in Patchwork preferences and the applied history. Change a color and apply a new preview to update an already-patched app directly. Removing a selected theme on the next patch update restores its original resources.

AMOLED forces Dark theme and black/near-black background brushes. Accent and switch overrides support Light and Dark themes. Only bounded color/brush resources are generated; no patch-supplied XAML or executable payloads are loaded. High Contrast dictionaries are not changed.

## Validation and reproduction

Experimental: copied actual Windows methods passed **40 selector checks**, **13 settings checks** and **19 earlier method checks**, with in-memory fixtures. An isolated real WinUI host loaded all 74 generated resources and verified five effective brush lookups. There was no real signed-in Proton UI session or VPN tunnel test. Server entitlements, VPN Accelerator speed, DNS/leak protection, NetShield filtering, LAN reachability, profiles and split routing need live verification. See [VALIDATION.md](VALIDATION.md).

The pack targets Windows source tag v5.1.8, commit d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9, and six exact original client assemblies. Definitions contain no executable/DLL payloads. Restoring backups returns original bytes and signatures.

Compile `source/BuildProtonPack.cs` separately against Patchwork and Mono.Cecil, then pass the original app folder and output JSON. Compile the three method probe sources separately against .NET 8 references and run against a complete patched workspace copy. Probes use mock settings/navigation, reserved documentation IPs and no live network calls. The optional ThemeLoadProbe and DumpThemes sources verify the generated dictionaries using an isolated native WinUI host; see VALIDATION.md. GuestCompatibilityProbe.cjs performs the explicit live compatibility attempt and revokes its transient sessions. These tools are excluded from the patcher.

No Proton binaries, account settings, credentials, VPN session data or user backups are distributed. Patchwork is independent of Proton and Morphe.

