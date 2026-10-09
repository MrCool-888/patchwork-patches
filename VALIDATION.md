# Proton VPN 5.1.8 patch validation

Pack **1.1.0**, release r2, Patchwork **0.4.0**. Available patch revisions are **1.0.0**. Tested October 8, 2026 (local date).

## Proton pack on actual copied assemblies

- Each of the ten available patches passed preview independently.
- Selecting all ten produced six changed client assemblies. Repeated previews produced identical output hashes.
- The combined plan applied to a workspace copy and restored the exact original bytes.
- A separate combined session was loaded under the installed .NET 8 runtime for **19 checks**: four telemetry getters, two cooldown methods, four UI availability/restriction getters, a free account-plan check, and eight saved-toggle checks (false/true for LAN, custom DNS, NetShield and split tunneling).
- The saved-toggle checks used an in-memory mock settings cache. No real user settings, login, network session, or connection was used.
- The copied assemblies were restored afterward and matched the installed originals. The installed Proton files retained their original pack fingerprints.

Original file fingerprints:

| File | SHA-256 |
| --- | --- |
| ProtonVPN.StatisticalEvents.dll | 55b53b4a1cc777cecfd17f5fb1012df1e292e0f3d5aff0ebe94da63cc2e17644 |
| ProtonVPN.Client.dll | a07dd312e8997c99e335d871b206f180d78fa0d950d929a234194148635fd8d4 |
| ProtonVPN.Client.Logic.Connection.dll | d3bd9d21a04c2c16c24430ee1c3fa1a8a351e122a9981a188dea447350ec76bc |
| ProtonVPN.Client.Settings.dll | e5193706fda534c6c87a2418fb8c9de083b45a8647222a4c00b3d134fd2ded5d |
| ProtonVPN.Client.Logic.Servers.dll | e133e5654ace39139d15c04c0f7bed6acbffd2afd4852ba3c13960ea9e7d147a |
| ProtonVPN.Client.Logic.Connection.Contracts.dll | 05c6253a7555518be1eb87bc628398330af3bd0d998ca52348150ed62ee43058 |

The pack includes the separate original launcher fingerprint used as its version guard.

## Limits

`SelectorProbe.cs` passed **21 isolated runtime checks** against the actual patched .NET 8 assemblies: free-only country cache and maintenance flags; empty cache; unchanged paid lists; actual free-server loader excluding paid/business servers; single-country intent preserved by the actual connection manager; free entitlement unchanged; generator restricted to free candidates; selected online NL server excludes JP, paid, business and offline candidates; a country with no free server yields no candidate; free selection bypasses cross-country Smart fallback while paid preferences remain effective; country row Connect access, inherited free/paid propagation, child-server restrictions, and the actual Connect action submitting the selected country to a mock manager.

The check uses reserved documentation IP `192.0.2.1`, fake settings/cache interfaces, and workspace copies. It does not create a network connection. Normal protocol/maintenance filtering is executed using the real connection-intent methods. Both the regular and Smart generators inherit the patched candidate filter; free requests choose the regular strict generator. Generic backend fixtures independently test the conditional operations and restoration in Patchwork's self-tests.

These results establish file-transform correctness and selected real method behavior. They do not establish a full logged-in Proton UI run, a live VPN connection, DNS leak protection, NetShield server filtering, LAN reachability, split routing/kill-switch behavior, excluded/default location behavior, or profile connection behavior. Server account entitlements remain unchanged. AMOLED backgrounds, custom accent colors, and styled switches remain unimplemented. Free selection currently supports country rows; individual cities and servers are not unlocked.
