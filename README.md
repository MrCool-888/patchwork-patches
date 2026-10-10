# Patchwork patches

Separate patch definitions for [Patchwork](https://github.com/MrCool-888/patchwork). Never bundled in the patcher installer.

Experimental prerelease: **1.5.0**, release **proton-vpn-5.1.8-r6**. Requires **Patchwork 0.7.0+** and original **Proton VPN for Windows 5.1.8.0**. The real Windows guest connection test remains pending. Free selection is patch **1.3.0**, promotions is **1.2.0**, and other available patches are **1.0.0**. The pack and individual versions appear in the patcher library, preview and applied history.

## Install the patch pack

1. Install **Patchwork 0.7.0 or newer** before importing this pack. Earlier patchers check only stable app releases, so download its prerelease installer once. In 0.7.0, enable prerelease app updates for future experimental versions.
2. In **Patch sources**, paste `https://github.com/MrCool-888/patchwork-patches` and choose **Add source** with pre-releases enabled. It checks immediately and automatically about once an hour while open. With an earlier pack applied and verified Patchwork history, preview and choose **Update patches** directly; no manual restore is needed. Older 0.4.x patchers still require restore first.
3. For a manual import instead, download **ProtonVPN-5.1.8.patchwork.json** from this repository's release assets. The source ZIP is for developers.
4. Choose **Add patch file** in Patchwork and import the JSON. The stable bundle ID replaces the older catalog entry without creating a duplicate.
5. Select the compatible Windows 5.1.8.0 program folder, choose patches and review **Preview changes**. Close Proton before applying. Writes to Program Files use the Windows administrator prompt.

The original hashes determine compatibility. Different app versions need a matching new pack. Restore verified originals through History to remove the patches.

## Available patches

| Patch | Version | Local behavior |
| --- | --- | --- |
| Disable usage telemetry | 1.0.0 | Stops authenticated and pre-login statistical event senders. |
| Hide upgrade promotions | 1.2.0 | Hides shared banners/feature cards, subscription badges, explicit Upgrade buttons in change-server and network dialogs, promotional announcement offers/popups and restricted row upgrade actions. Available rows retain Connect. |
| Keep VPN Accelerator enabled | 1.0.0 | Keeps the effective setting and settings toggle on, shows On in connection settings, and opens its page without an upgrade prompt. Overrides a saved off value. Restore this patch to regain toggle control. |
| Remove local server change delay | 1.0.0 | Removes the desktop cooldown check and returns zero remaining delay. |
| LAN connections controls | 1.0.0 | Exposes advanced controls and reads the saved LAN toggle. |
| Custom DNS controls | 1.0.0 | Exposes advanced controls and reads the saved DNS toggle/list. |
| NetShield client controls | 1.0.0 | Opens widget/settings and reads the saved toggle. Server filtering still depends on the account. |
| Split tunneling client controls | 1.0.0 | Opens widget/settings and reads the saved routing toggle. |
| Connection preference controls | 1.0.0 | Displays default/excluded location controls and reads the saved default connection. |
| Connection profile controls | 1.0.0 | Displays creation/editing and removes the local profile row restriction. |
| Free country and individual server selection | 1.3.0 | Expanding a free country lists individual free servers; Connect keeps the exact server ID in country lists, Search and standard Recents. |
| Hide paid country modes | 1.0.0 | Hides Secure Core, P2P and Tor country tabs on free accounts. Paid accounts retain their original tabs. |
| Guest VPN session | 1.0.0 | Adds Continue as guest using real credentialless sessions and the native Windows auth, key and certificate pipeline. Executable managed client code; real tunnel validation pending. |
| Custom accent color | 1.0.0 | Configurable primary/link/focus color and button text color. |
| Custom switch colors | 1.0.0 | Configurable enabled track and knob colors with native controls. |

Settings promotion removal changes presentation and navigation. Feature-specific effective settings remain separate patches, and the actual account plan is unchanged. Service, account and maintenance notices remain available. The shared advanced UI edit merges when several selected patches use it.

## Choose an individual free server

With a signed-in or authorized guest free session, expand a country in the normal country list and choose an individual free server’s **Connect** action. This fixes the upgrade prompt caused by the previous pack expanding countries into paid city rows. Free expansion uses the actual free-server loader and filters by country. Paid accounts retain city/state browsing.

You can also use **Search** with a full free-server name or prefix such as **NL-FREE**, or reconnect through **Recents**. Standard country and individual-server Recents now use Connect without the blanket free-account upgrade gate. Paid feature/profile Recents retain restrictions. An offline or missing selected server has no fallback. Paid server rows, cities and business gateways keep their existing restrictions; the promotions patch hides their upgrade actions. Maintenance and protocol checks remain active.

Select **Hide paid country modes** to remove Secure Core, P2P and Tor country tabs for free accounts. This is a navigation change, not access to their paid servers. Restart Proton after an account-plan change. The optional patch keeps the original paid-account list and the All component objects.

The design follows the filtering and selected-location ideas in [Morphe's Android free-location patch](https://github.com/hxreborn/morphe-patches/blob/main/patches/src/main/kotlin/app/morphe/patches/protonvpn/misc/freeservers/ShowFreeServerLocationsPatch.kt). The Windows implementation is independently written; Android bytecode is not reused.

## Sign-in

The optional **Guest VPN session** patch adds **Continue as guest** alongside normal sign-in. Sign out first if an account is active. It creates a real guest session, stores credentials through Proton's existing protected settings, retrieves the authorized free plan, and uses the native key/certificate managers. Resume uses the existing refresh pipeline; logout uses native disconnect, revocation and key removal. Failure and cancellation discard credentials. Human verification stops guest mode and directs the user to normal sign-in.

The bootstrap requests use the Android app identity; subsequent VPN, refresh and certificate requests use the native Windows pipeline. The backend still controls eligibility. The patch contains independently written executable code from [source/GuestClient.cs](source/GuestClient.cs), embedded with a SHA-256 checked by Patchwork. Review that code before applying. Local lifecycle, actual DLL hook, native WinUI and live API checks passed; this experimental prerelease still needs a real installed-client tunnel test. See [AUTH-RESEARCH.md](AUTH-RESEARCH.md).

## Custom themes

Select **Custom accent color** and/or **Custom switch colors**. Selecting a color patch exposes hex fields and a **Choose…** color picker. Pick contrasting text/knob colors, preview, then apply. Colors persist in Patchwork preferences and the applied history. Change a color and apply a new preview to update an already-patched app directly. Removing a selected theme on the next patch update restores its original resources.

AMOLED has been removed from this candidate. Updating a previously patched installation removes its forced-dark and black-surface edits using verified originals. Accent and switch overrides support Light and Dark themes. Their operations generate only bounded color/brush resources. High Contrast dictionaries are not changed.

## Validation and reproduction

Experimental: copied actual Windows methods passed selector, settings and auth-hook checks with in-memory fixtures. An isolated real WinUI host verified guest controls. Live API tests passed native Windows token rotation, free VPN authorization, certificate issuance/renewal, signed server retrieval and logout. No production account settings were modified. A real installed-client guest tunnel and traffic/DNS checks remain pending. See [VALIDATION.md](VALIDATION.md).

The pack targets Windows source tag v5.1.8, commit d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9, and exact original client assembly fingerprints. The optional guest patch embeds a managed helper DLL; the other patches remain declarative. Restoring backups returns original bytes and signatures.

Build `source/GuestClient.cs` using `source/build-guest.ps1` with original 5.1.8 assemblies, a .NET 8 runtime reference directory and modern Roslyn compiler. Compile `source/BuildProtonPack.cs` separately against Patchwork 0.7.0 and Mono.Cecil, then pass the original app folder, output JSON and helper DLL as its three arguments. `-Tests`, `-Ui` and `-Live` build the separate guest probes. The live probe makes explicit real API calls and revokes its temporary sessions; other probes use fixtures. Native UI probes require a copied Windows App Runtime host and its runtime dependencies. These tools and guest code are excluded from the patcher installer.

No Proton binaries, account settings, credentials, VPN session data or user backups are distributed. Patchwork is independent of Proton and Morphe.

