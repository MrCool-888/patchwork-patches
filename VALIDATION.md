# Proton VPN 5.1.8 patch validation

Pack **1.4.0**, release **r5**, requires **Patchwork 0.6.1+**. Free selection is patch 1.3.0, promotions 1.2.0, and other available patches 1.0.0. Tested October 9, 2026.

## File transformations and updates

All 15 available patches previewed independently. The combined plan changes eight exact original assemblies. Repeated combined previews produced identical hashes. Custom accent/switch choices changed output, were recorded in history and restored byte-for-byte.

Applied pack 1.3.0 to a complete workspace copy, simulated the older journal format without patch IDs, then updated directly to 1.4.0 without manual restore. Preview left files unchanged; the parent became Superseded and eight output hashes matched. After the probes, restoration recovered all original fingerprints. The launcher and original fingerprints are unchanged. The live installed app and its settings were not modified.

| File | SHA-256 |
| --- | --- |
| ProtonVPN.StatisticalEvents.dll | 55b53b4a1cc777cecfd17f5fb1012df1e292e0f3d5aff0ebe94da63cc2e17644 |
| ProtonVPN.Client.dll | a07dd312e8997c99e335d871b206f180d78fa0d950d929a234194148635fd8d4 |
| ProtonVPN.Client.Logic.Connection.dll | d3bd9d21a04c2c16c24430ee1c3fa1a8a351e122a9981a188dea447350ec76bc |
| ProtonVPN.Client.Settings.dll | e5193706fda534c6c87a2418fb8c9de083b45a8647222a4c00b3d134fd2ded5d |
| ProtonVPN.Client.Logic.Servers.dll | e133e5654ace39139d15c04c0f7bed6acbffd2afd4852ba3c13960ea9e7d147a |
| ProtonVPN.Client.Logic.Connection.Contracts.dll | 05c6253a7555518be1eb87bc628398330af3bd0d998ca52348150ed62ee43058 |
| ProtonVPN.Client.Common.UI.dll | 02ffdd254326230c3ce16e97d5c7b639c83355bd33c503f079540b5a77a64c19 |
| ProtonVPN.Client.Logic.Announcements.dll | 6a16e53b2223d8c0c22fb0afbf4545c23c1d2ff9422636f36a3e1c28b3243985 |

## Actual copied method checks

The .NET 8 copies passed **55 selector/offer checks**, **13 settings/Accelerator checks**, and **19 prior method checks** (87 total). New checks cover standard free-server/country Recents, exact recent intent, maintenance, paid P2P recent restrictions, free/paid mode tabs and component identity, and promotional offer filtering with the original NPS survey path retained. Earlier checks cover search, free/business/paid/offline filtering, maintenance, no server fallback, account plan preservation, strict selection and row Connect actions.

Fixtures use in-memory settings/cache/navigation proxies and reserved documentation IPs. They never sign in, connect a VPN, run a service or write real settings.

## Real WinUI resources

Extracted three generated XAML dictionaries from the patched LoadTypographyResourceDictionary method using source/DumpThemes.cs. An isolated .NET 8/native Windows App Runtime host using source/ThemeLoadProbe.cs successfully loaded **74 Color/SolidColorBrush resources**, then verified effective Light/Dark accent and switch-track lookups plus the Dark black background. A pre-existing accent dictionary was overridden as expected. The host used copied runtime files and a test-only launcher bound to ThemeLoadProbe.dll. No production app window or account session was opened.

The same native host also passed seven promotional-control checks: zero-size/no-hit shared UpsellBanner and UpsellFeatureContentControl; restricted row hiding and available-row restoration; and the four actual compiled Upgrade content bindings in change-server, carousel, P2P detection and streaming detection views. Button objects were real WinUI controls; account/view-model dependencies were in-memory fixtures. No production client was launched.

Patchwork's 39 app scenarios exercise the earlier behavior plus enum filtering/guards and UI hooks with nulls, branches, source identity, paid preservation and rejection of invalid receivers/signatures/values. Full Proton page rendering and every dynamic navigation path still need user UI verification. The source audit covers the built-in upgrade controls and promotional offer feed; normal service warnings are separate.

## Guest compatibility attempt

The Windows-identity credentialless request was rejected, but the requested Android-identity attempt succeeded: HTTP 200/Code 1000 for bootstrap and guest creation, VPN credentials present under Windows-identity GET /vpn/v2, and HTTP 200/Code 1000 with a certificate under a Windows-identity certificate request. Both sessions were revoked with HTTP 200. No credentials, identifiers, private keys or certificates were stored or published. See AUTH-RESEARCH.md. This validates API compatibility only; sign-in remains required until the Windows client integration is implemented and a tunnel is tested.

## Limits

No real signed-in Proton UI session, VPN tunnel, Accelerator throughput, DNS leaks, NetShield server filtering, LAN reachability, split routing, profiles or server entitlements were tested. The free-server change fixes the inspected client browsing/connect path; backend availability and protocol validation remain Proton's. Theme resources are bounded overrides, and other compiled graphics/gradient effects may retain original colors.

Generator and probes are separate source tools, never installer payloads. No Proton binaries, account data, user backups, identifiers or credentials are distributed.
