# Proton VPN Windows patches

Pack **1.7.1** targets the official **Windows x64 5.1.10.0** release. It includes 15 selections and the existing color options, including guest sign-in with a helper rebuilt against 5.1.10. The 5.1.8 and 5.1.9 JSON files remain available for their respective builds.

## Apply after upgrading

1. Confirm Proton VPN is on version 5.1.10.
2. Import **ProtonVPN-5.1.10.patchwork.json** using Patchwork's **Add patch file**. The pack format requires Patchwork 0.7.0 or later.
3. Select `C:\Program Files\Proton\VPN\v5.1.10` or your equivalent installation folder.
4. Exit Proton VPN from the system tray. In Patchwork, choose your patches, review **Preview changes**, then apply.

The catalog ID is retained, so importing the new JSON updates the Proton entry in Patchwork. To restore an earlier installation, use its own entry in **History**. The installer, patcher, and pack are separate downloads.

The selections cover usage telemetry, promotions, VPN Accelerator, local server cooldown, LAN, custom DNS, NetShield and split tunneling controls, connection preferences and profiles, free location selection, paid-mode navigation, guest sign-in, and accent and switch colors. Proton's servers control account eligibility. Guest accounts retain backend free-server limits.

See the validation notes for [5.1.10](5.1.10-VALIDATION.md) and [5.1.9](5.1.9-VALIDATION.md).

## Build

Use the original app files from the official Windows x64 release, a modern Roslyn `csc.exe`, .NET 10 reference assemblies, and Patchwork with its Mono.Cecil dependency. [Proton VPN 5.1.10 release](https://github.com/ProtonVPN/win-app/releases/tag/v5.1.10)

1. Run `source/build-guest.ps1 -OriginalApp ORIGINAL_APP -OutputDirectory BUILD -Compiler CSC_EXE -RuntimeDirectory NET10_REFS`.
2. Compile `source/BuildProtonPack.cs` as an executable referencing `Patchwork.exe`, `Mono.Cecil.dll`, and `System.Core.dll`. Keep those two application dependencies alongside the generator.
3. Run `BuildProtonPack.exe ORIGINAL_APP ProtonVPN-5.1.10.patchwork.json BUILD/Patchwork.ProtonGuest.dll`.
4. Compile `source/proton/ValidatePack.cs` against the same dependencies plus `System.Web.Extensions.dll`. With Proton VPN closed, run `ValidatePack.exe PACK_JSON ORIGINAL_APP NEW_SCRATCH_DIR [WRONG_VERSION_APP]`.

The validator uses a new disposable target and separate history. It leaves a patched fixture for the existing runtime probes; it does not patch the source app folder. `source/proton/CompareTargets.cs` compares target methods across original builds. Its `--normalize-build-details` flag accounts for upstream log paths and the equivalent countries getter lambda name.

No Proton executable/DLL, account data, credentials, or user backups are included in the source archive. The JSON embeds only the project-authored guest helper, with its SHA-256. Live VPN behavior requires separate testing.
