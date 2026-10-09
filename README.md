# Patchwork patches

Separate patch definitions for [Patchwork](https://github.com/MrCool-888/patchwork). These files are never bundled in the patcher installer.

## Import the Proton VPN pack

Download [ProtonVPN-5.1.8.patchwork.json](ProtonVPN-5.1.8.patchwork.json), open Patchwork, and choose **Add patch file**. Importing does not apply anything. Select the original Windows **5.1.8.0** installation, choose patches, and review **Preview changes** before applying. Close Proton's desktop app first. Writes to Program Files require the Windows administrator prompt. Use **History & restore** to restore originals.

## Proton pack scope

The separate pack contains **nine available experimental client patches**:

| Patch | Implemented local behavior |
| --- | --- |
| Disable usage telemetry | Stops authenticated and pre-login statistical event senders. |
| Hide promotions | Hides connection-card, sidebar, profile-page, and tray upsell banners. |
| Remove server delay | Removes the desktop cooldown check and returns zero remaining delay. |
| LAN controls | Exposes advanced controls, reads the saved LAN toggle, and prevents its local correction from resetting it. |
| Custom DNS controls | Exposes advanced controls and reads the saved DNS toggle and resolver list. |
| NetShield controls | Opens the widget and settings and reads the saved toggle. |
| Split tunneling controls | Opens the widget and settings and reads the saved routing toggle. |
| Connection preferences | Displays default-connection/excluded-location controls and reads the saved default connection. |
| Profiles | Displays creation/editing and removes the profile row's local restriction. |

LAN and custom DNS share one Windows advanced-screen availability getter. Selecting either displays the shared screen, including NAT controls. Effective settings have separate checks. Selecting both merges the identical shared edit.

These changes do not create a paid account or expand server entitlements. Proton's connection and server validation still apply. NetShield filtering, DNS behavior, LAN reachability, profile connections, default/excluded location behavior, and split routing require live network verification. The tests cover the file transformations and selected real method behavior, not a logged-in VPN session or leak protection.

**Four requested patches remain unavailable:** AMOLED backgrounds, custom accent color, styled switches, and eligible free-location selection. They are disabled and marked planned in the catalog. Android patch instructions cannot be reused directly in Windows WinUI resources.

The pack targets Windows source tag `v5.1.8`, commit `d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9`, and the exact original installed files used for validation. It changes four client assemblies; its definitions contain no Proton executables or DLL payloads. Edited assemblies have different signatures and hashes. Restoring the backup restores their original bytes and signatures.

## Sign-in requirement

A working no-sign-in patch is **not available** for Windows 5.1.8. The normal connection path requests a VPN certificate through an authenticated API request. Removing the login UI cannot issue a usable certificate or provide an accountless VPN session. This pack leaves sign-in in place.

Evidence: [Windows installation instructions](https://protonvpn.com/support/install-windows-vpn), [certificate API implementation](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Api/ProtonVPN.Api/ApiClient.cs), and [connection credentials](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Connection/ProtonVPN.Client.Logic.Connection/RequestCreators/ConnectionRequestCreator.cs).

## Reproduce and validate

The source in `source/BuildProtonPack.cs` reads original files and produces the separate JSON pack. Compile it separately with references to the Patchwork build and Mono.Cecil, then pass the original app folder and output JSON path. `source/RuntimeProbe.cs` contains the isolated .NET 8 method checks. Neither tool is included in the patcher. See [VALIDATION.md](VALIDATION.md) for evidence and limits.

No Proton application binaries, account settings, credentials, or VPN connection data are distributed here. Patchwork is independent of Proton and Morphe.
