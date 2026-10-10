# Proton VPN 5.1.8 patch validation

Pack **1.3.0**, release **r4**, requires **Patchwork 0.6.0+**. Free selection is patch 1.2.0, promotions 1.1.0, and other available patches 1.0.0. Tested October 9, 2026.

## File transformations and updates

All 14 available patches previewed independently. The combined plan changes six exact original assemblies. Repeated combined previews produced identical hashes. Custom accent/switch choices changed the generated output, were recorded in history and restored byte-for-byte.

Applied pack 1.2.0 to a complete workspace copy, simulated the older journal format without patch IDs, then updated directly to 1.3.0 without manual restore. Preview left files unchanged; the parent became Superseded and six output hashes matched. After the probes, restoration recovered all original fingerprints. The launcher and original fingerprints are unchanged. The live installed app and its settings were not modified.

| File | SHA-256 |
| --- | --- |
| ProtonVPN.StatisticalEvents.dll | 55b53b4a1cc777cecfd17f5fb1012df1e292e0f3d5aff0ebe94da63cc2e17644 |
| ProtonVPN.Client.dll | a07dd312e8997c99e335d871b206f180d78fa0d950d929a234194148635fd8d4 |
| ProtonVPN.Client.Logic.Connection.dll | d3bd9d21a04c2c16c24430ee1c3fa1a8a351e122a9981a188dea447350ec76bc |
| ProtonVPN.Client.Settings.dll | e5193706fda534c6c87a2418fb8c9de083b45a8647222a4c00b3d134fd2ded5d |
| ProtonVPN.Client.Logic.Servers.dll | e133e5654ace39139d15c04c0f7bed6acbffd2afd4852ba3c13960ea9e7d147a |
| ProtonVPN.Client.Logic.Connection.Contracts.dll | 05c6253a7555518be1eb87bc628398330af3bd0d998ca52348150ed62ee43058 |

## Actual copied method checks

The .NET 8 copies passed **40 selector checks**, **13 settings/Accelerator checks**, and **19 prior method checks** (72 total). The six new selector checks verify free country expansion returns country-matching free server rows, bypasses paid city/state loading, keeps the row context, clears the local upgrade gate, sends the exact clicked server ID, yields no result for a country without free servers, and retains the original paid city/state path. Earlier checks cover search, free/business/paid/offline filtering, maintenance, no server fallback, account plan preservation, strict selection and row Connect actions.

Fixtures use in-memory settings/cache/navigation proxies and reserved documentation IPs. They never sign in, connect a VPN, run a service or write real settings.

## Real WinUI resources

Extracted three generated XAML dictionaries from the patched LoadTypographyResourceDictionary method using source/DumpThemes.cs. An isolated .NET 8/native Windows App Runtime host using source/ThemeLoadProbe.cs successfully loaded **74 Color/SolidColorBrush resources**, then verified effective Light/Dark accent and switch-track lookups plus the Dark black background. A pre-existing accent dictionary was overridden as expected. The host used copied runtime files and a test-only launcher bound to ThemeLoadProbe.dll. No production app window or account session was opened.

Patchwork's 37 app scenarios also exercise generated-hook execution, composed resources, color validation, saved preferences, journal choices, direct color updates, byte-exact restore, worker choice freezing and altered-job refusal. This verifies resource loading and lookup; full Proton page rendering, system High Contrast appearance and interactive native color-picker behavior still need user UI verification.

## Guest compatibility attempt

The live Windows probe received HTTP 200/Code 1000 for an unauthenticated session, then **HTTP 422/Code 5003: Platform expected to be in [Android, iOS]** for credentialless authentication. DELETE /auth revoked the temporary session with HTTP 200. Tokens remained in memory and are not distributed. The independent source/GuestCompatibilityProbe.cjs logs status codes only, with the known platform error. See AUTH-RESEARCH.md. Guest sessions stay unavailable.

## Limits

No real signed-in Proton UI session, VPN tunnel, Accelerator throughput, DNS leaks, NetShield server filtering, LAN reachability, split routing, profiles or server entitlements were tested. The free-server change fixes the inspected client browsing/connect path; backend availability and protocol validation remain Proton's. Theme resources are bounded overrides, and other compiled graphics/gradient effects may retain original colors.

Generator and probes are separate source tools, never installer payloads. No Proton binaries, account data, user backups, identifiers or credentials are distributed.
