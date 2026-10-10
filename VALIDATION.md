# Proton VPN 5.1.8 patch validation

## Experimental 1.5.1 / r7 prerelease — October 10, 2026

The native WinUI navigation regression first reproduced r6's exact `Guest sign-in cancelled.` failure with fake HTTP: LoggingIn switches to a loading control and unloads the sign-in page, whose old handler cancels the shared authenticator token. The baseline differs only by injecting the fake protocol; no real sessions are created by the regression test.

The fixed helper passes **21 native WinUI checks** covering controls/accessibility, duplicate prevention, normal sign-in busy state, a real Unloaded transition while the login remains pending, the native Authenticating message, one attempt per click sequence, login completion after navigation, loading-page Cancel, cleanup and retry on the cached page. It preserves the native UI synchronization context.

**52 lifecycle checks** pass, including request-header and response-body deadlines, timeout distinction from deliberate cancellation, native API timeout cleanup, resetting a previously cancelled token before LoggingIn and retaining cancellation delivered by that notification. Request deadlines default to 30 seconds; fixtures use 80 milliseconds. Failed bootstrap/guest sessions are revoked, and errors contain no credentials.

A direct **1.5.0-to-1.5.1** update against copied 5.1.8 originals passes with the legacy journal format and no manual restore. Ten output assemblies match the preview, parent history is Superseded, both embedded helper hashes match, AMOLED is absent, original theme selection remains intact, and byte-exact restoration plus deterministic re-preview pass. The actual copied DLL checks pass: **55 selector**, **13 settings**, **19 runtime** and **3 auth-hook** checks.

The rebuilt helper passes **7 live API checks**: credentialless creation, native Windows token rotation, real authorized free plan, native key/certificate issuance, signed server retrieval, certificate renewal and logout/revocation. All temporary test sessions are revoked; credentials and keys remain in memory and are never logged.

Installed client inspection found an active session. Its installed files, account and connection were not changed for these tests. A real installed-client guest tunnel, restart/reconnect, transition back to normal sign-in and traffic/DNS checks remain pending. This patch pack remains prerelease; Patchwork 0.7.0 stays stable.

## Experimental 1.5.0 / r6 prerelease

Requires Patchwork 0.7.0. Guest sessions are implemented in independently written GuestClient.cs; AMOLED is absent from the candidate. Stable release is held pending a real guest tunnel, restart/reconnect, normal sign-in transition and traffic/DNS tests from the installed client.

All 15 patches preview independently and apply together to ten copied assemblies. A direct 1.4.0-to-1.5.0 update, including an older journal without patch IDs, passes without manual restore; previous history becomes Superseded and output hashes match. The copied actual methods pass **55 selector checks**, **13 settings checks**, **19 earlier runtime checks** and **3 native authentication hook checks**. No installed app, production account files or services were changed.

The guest lifecycle probe passes **40 checks** for login, certificate validation before success, protected settings contract usage, resume, normal-account fallthrough, active-session preservation, cancellation, challenge refusal, missing/partial credentials, failed authorization, expired/missing certificates and revocation. An isolated native WinUI host passes guest button insertion, accessible label, normal-login busy state and duplicate prevention. The fixture found and fixed the unavailable button Parent during construction.

The real API probe passes **7 checks**: guest session creation, native Windows token refresh/rotation, authorized free plan, native key/certificate issuance, signed logical-server retrieval, native certificate renewal and native logout. Session and key material stays in memory; all temporary sessions are revoked. This is API integration evidence, not an actual tunnel test. The service's installed-process-path authorization remains intact.

## Published 1.4.0 / r5 checks

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
