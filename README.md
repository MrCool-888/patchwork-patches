# Patchwork patches

Separate patch definitions for [Patchwork](https://github.com/MrCool-888/patchwork). Never bundled in the patcher installer.

Current pack: **1.2.0**, release **proton-vpn-5.1.8-r3**. Requires **Patchwork 0.4.2+** and original **Proton VPN for Windows 5.1.8.0**. Hide promotions and free selection are patch **1.1.0**; other available patches are **1.0.0**. The pack and individual versions appear in the patcher library, preview and applied history.

## Install the patch pack

1. Update Patchwork to 0.4.2 or newer.
2. If an earlier pack is applied, restore that session in **History & restore** first. Importing a new definition does not update already-patched files.
3. Download **ProtonVPN-5.1.8.patchwork.json** from this repository's release assets. The source ZIP is for developers.
4. Choose **Add patch file** in Patchwork and import the JSON. The stable bundle ID replaces the older catalog entry without creating a duplicate.
5. Select the original Windows 5.1.8.0 program folder, choose patches and review **Preview changes**. Close Proton before applying. Writes to Program Files use the Windows administrator prompt.

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
| Free country and individual server selection | 1.1.0 | Enables cached free countries and individual free-server Connect actions, retains the exact selected server ID, and uses strict free non-business candidates without fallback. |

Settings promotion removal changes presentation and navigation. Feature-specific effective settings remain separate patches, and the actual account plan is unchanged. Service, account and maintenance notices remain available. The shared advanced UI edit merges when several selected patches use it.

## Choose an individual free server

With a signed-in free account, use Proton's **Search** to find a free server by its name or prefix, for example **NL-FREE**, then use that server row's **Connect** action. Search matches server-name prefixes; searching only FREE is not sufficient. The patch retains that exact server ID. If it is offline or absent, the request has no alternative candidate rather than silently connecting elsewhere.

Country Connect still works. This release adds individual selection through Search; it does not replace country expansion with a new server browser. City rows retain their original restrictions. Paid server rows and business gateway rows remain restricted for free accounts. Paid accounts retain their original candidate list and Smart reconnect preference.

The design follows the filtering and selected-location ideas in [Morphe's Android free-location patch](https://github.com/hxreborn/morphe-patches/blob/main/patches/src/main/kotlin/app/morphe/patches/protonvpn/misc/freeservers/ShowFreeServerLocationsPatch.kt). The Windows implementation is independently written; Android bytecode is not reused.

## Sign-in

**Guest VPN sessions are not implemented in this Windows pack.** Proton's Android app already offers [Continue as guest](https://protonvpn.com/support/best-android-vpn-app). Its official authentication library obtains a credentialless session with real access and refresh tokens. Morphe's current Proton patches observe existing user state; they do not implement that guest authentication flow. A Windows port would need to obtain and persist the guest session, load its VPN user, issue connection credentials, and handle refresh/revocation. See [AUTH-RESEARCH.md](AUTH-RESEARCH.md) for the source trace and implementation requirements.

Windows 5.1.8's normal connection path obtains an account-issued certificate using an authenticated API request. Removing its login screen does not supply those credentials. The existing guest-hole transport supports temporary API/login recovery; this pack does not turn it into a general selectable-server VPN session. The catalog explicitly marks guest sessions not implemented and leaves sign-in in place. This is an implementation limitation, not a claim that credentialless VPN use is impossible.

Evidence: [certificate API](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Api/ProtonVPN.Api/ApiClient.cs), [normal connection credentials](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Connection/ProtonVPN.Client.Logic.Connection/RequestCreators/ConnectionRequestCreator.cs), and [guest-hole login recovery](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Auth/ProtonVPN.Client.Logic.Auth/UserAuthenticator.cs).

AMOLED backgrounds, custom accent colors and styled switches also remain unimplemented. The catalog marks these four unavailable requests as planned; none has executable operations.

## Validation and reproduction

Experimental: copied actual Windows methods passed **34 selector checks**, **13 settings checks** and **19 earlier method checks**, with in-memory fixtures. There was no real sign-in, UI account session or VPN tunnel test. Server entitlements, VPN Accelerator speed, DNS/leak protection, NetShield filtering, LAN reachability, profiles and split routing need live verification. See [VALIDATION.md](VALIDATION.md).

The pack targets Windows source tag v5.1.8, commit d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9, and six exact original client assemblies. Definitions contain no executable/DLL payloads. Restoring backups returns original bytes and signatures.

Compile `source/BuildProtonPack.cs` separately against Patchwork and Mono.Cecil, then pass the original app folder and output JSON. Compile the three probe sources separately against .NET 8 references and run against a complete patched workspace copy. Probes use mock settings/navigation, reserved documentation IPs and no live network calls. These tools are excluded from the patcher.

No Proton binaries, account settings, credentials, VPN session data or user backups are distributed. Patchwork is independent of Proton and Morphe.
