# Proton VPN 5.1.8 patch validation

Pack **1.2.0**, release r3, requires **Patchwork 0.4.2+**. Promotions and free selection are patch version **1.1.0**; other available patches are **1.0.0**. Tested October 9, 2026.

## File transformations

All eleven available patches previewed independently. Selecting all eleven produced six changed client assemblies; repeat previews had identical hashes. The combined plan applied to a complete workspace copy and restored exact original bytes. Separate sessions tested free selection alone and promotions/accelerator without the other feature patches, then restored their original bytes.

The version launcher and original six assembly fingerprints remain the same as r2. The user's live Proton installation was not modified. Tests validate workspace files against the journal's original hashes; they do not require the user's installed files to be unpatched.
| File | SHA-256 |
| --- | --- |
| ProtonVPN.StatisticalEvents.dll | 55b53b4a1cc777cecfd17f5fb1012df1e292e0f3d5aff0ebe94da63cc2e17644 |
| ProtonVPN.Client.dll | a07dd312e8997c99e335d871b206f180d78fa0d950d929a234194148635fd8d4 |
| ProtonVPN.Client.Logic.Connection.dll | d3bd9d21a04c2c16c24430ee1c3fa1a8a351e122a9981a188dea447350ec76bc |
| ProtonVPN.Client.Settings.dll | e5193706fda534c6c87a2418fb8c9de083b45a8647222a4c00b3d134fd2ded5d |
| ProtonVPN.Client.Logic.Servers.dll | e133e5654ace39139d15c04c0f7bed6acbffd2afd4852ba3c13960ea9e7d147a |
| ProtonVPN.Client.Logic.Connection.Contracts.dll | 05c6253a7555518be1eb87bc628398330af3bd0d998ca52348150ed62ee43058 |


## Runtime evidence

The actual copied .NET 8 assemblies passed **66 checks** in total:

- **19 earlier method checks:** telemetry, cooldown, UI availability, unchanged free account entitlement, and saved false/true values for LAN, DNS, NetShield and split tunneling.
- **34 selector checks:** previous free-country/cache/maintenance/strict-candidate behavior plus individual free-server Connect access; exact server intent preserved by the real connection manager; exact server ID wins over another online free server in the same country; missing/offline selections have no fallback; Smart reconnect bypass; the real row Connect action submits the exact server; paid server rows remain restricted for free accounts; paid access is preserved; city and business gateway restrictions remain; real search finds exact names and country/free prefixes. These 34 also passed with only free selection applied.
- **13 settings checks:** accelerator stays enabled with saved off/on values; free account entitlement unchanged; accelerator page reports enabled; three settings promotion branches use the normal-control presentation; accelerator, NetShield, split-tunneling and port-forwarding navigation avoid the upsell activator; connection settings labels accelerator On; the real service settings creator receives SplitTcp=true. These 13 also passed with only promotions and accelerator applied.

Fixtures use in-memory settings, fake navigation/cache interfaces, reserved documentation IP 192.0.2.1 and complete workspace copies. They do not contact an API, sign in, connect a VPN, start a service or write real user settings. Backend fixtures test conditional setter execution, original inherited argument propagation, invalid condition rejection and exact restoration.

## Limits

These checks establish selected actual method behavior and file-transform correctness. They do not establish a real logged-in WinUI session, VPN tunnel, accelerator speed, DNS leak protection, NetShield server filtering, LAN reachability, split-routing/kill-switch behavior, default/excluded locations or profiles. Server account entitlements remain unchanged.

Individual free-server selection is exposed through Proton Search, using a full server name or its prefix such as NL-FREE. A country tree/server-browser redesign is not included. City and paid/business rows remain restricted for free accounts. Maintenance and protocol checks remain in the actual intent filter.

Hide promotions changes settings presentation/navigation; effective feature settings are separate patches. Accelerator is deliberately forced on, including the displayed toggle; restore the patch to regain off-toggle control. Normal informational, account and maintenance notices remain.

Guest sessions are not implemented in this pack. Official Android sources implement credentialless authentication and return real session tokens; a Windows port still needs guest authentication, persistence, refresh and connection credential handling. Windows 5.1.8's normal connection flow obtains an account-issued certificate through an authenticated API. Existing guest-hole recovery is used for login/API access rather than the Android guest-session flow. Sign-in is retained and the planned catalog entry has no operations. See AUTH-RESEARCH.md. AMOLED backgrounds, accent colors and styled switches also remain unimplemented.
