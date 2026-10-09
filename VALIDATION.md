# Proton VPN 5.1.8 patch validation

## Proton pack on actual copied assemblies

- Each of the nine available patches passed preview independently.
- Selecting all nine produced four changed client assemblies. Repeated previews produced identical output hashes.
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

The pack includes the separate original launcher fingerprint used as its version guard.

## Limits

These results establish file-transform correctness and selected real method behavior. They do not establish a full logged-in Proton UI run, a live VPN connection, DNS leak protection, NetShield server filtering, LAN reachability, split routing/kill-switch behavior, excluded/default location behavior, or profile connection behavior. Server account entitlements remain unchanged. AMOLED backgrounds, custom accent colors, styled switches, and eligible free-location selection remain unimplemented.
