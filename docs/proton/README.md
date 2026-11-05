# Proton VPN 5.1.9

Pack **1.6.0** targets the official **Windows x64 5.1.9.0** release. It keeps the existing 15 selections and color options, rebuilds the guest helper against 5.1.9, and pins the new executable and assembly fingerprints. The 5.1.8 JSON remains unchanged for the older app.

## Apply after upgrading

1. Update Proton VPN to 5.1.9 using its official installer or updater.
2. Import **ProtonVPN-5.1.9.patchwork.json** using Patchwork's **Add patch file**. Use current Patchwork; the pack format requires 0.7.0 or later.
3. Select the new program folder, normally `C:\Program Files\Proton\VPN\v5.1.9`.
4. Close Proton VPN, choose the patches you want, review **Preview changes**, then apply.

The existing catalog ID is retained, so importing this JSON replaces the older Proton definition. Existing History and backups remain available. Apply only to matching original 5.1.9 files; restore a session through its matching History entry. The installer, patcher, and pack are separate downloads.

The selections cover usage telemetry, promotions, VPN Accelerator, local server cooldown, LAN/custom DNS/NetShield/split-tunneling controls, connection preferences and profiles, free location selection, paid-mode navigation, guest sign-in, and accent/switch colors. Account eligibility remains controlled by Proton's servers.

See [5.1.9 validation](5.1.9-VALIDATION.md) for the checks and limits. Earlier 5.1.8 research and validation remain at the repository root.

## Build

Use original files extracted from the [official 5.1.9 x64 installer](https://github.com/ProtonVPN/win-app/releases/tag/v5.1.9), a modern Roslyn `csc.exe`, .NET 8 reference assemblies, and Patchwork with its Mono.Cecil dependency.

1. Run `source/build-guest.ps1 -OriginalApp ORIGINAL_APP -OutputDirectory BUILD -Compiler CSC_EXE -RuntimeDirectory NET8_REFS`.
2. Compile `source/BuildProtonPack.cs` as an executable referencing `Patchwork.exe`, `Mono.Cecil.dll`, and `System.Core.dll`. Keep those two application dependencies alongside the generator.
3. Run `BuildProtonPack.exe ORIGINAL_APP ProtonVPN-5.1.9.patchwork.json BUILD/Patchwork.ProtonGuest.dll`.
4. Compile `source/proton/ValidatePack.cs` against the same dependencies plus `System.Web.Extensions.dll`, then run `ValidatePack.exe PACK_JSON ORIGINAL_APP NEW_SCRATCH_DIR OPTIONAL_WRONG_VERSION_APP`.

The validator uses a new disposable target and separate history. It leaves a patched fixture for the existing runtime probes; it does not patch the source app folder. `source/proton/CompareTargets.cs` compares original 5.1.8/5.1.9 targets; its optional `--normalize-build-details` flag accounts for upstream logging paths and verifies the body behind the renamed countries getter lambda.

No Proton executable/DLL, account data, credentials, or user backups are included in the source archive. The JSON embeds only the project-authored guest helper, with its SHA-256. Live VPN behavior requires separate testing.
