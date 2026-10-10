# Guest session research

Reviewed October 9, 2026. This is research for a future Windows implementation; pack 1.3.0 retains sign-in and does not implement guest sessions.

## Android and Morphe

Proton VPN officially supports [Continue as guest on Android](https://protonvpn.com/support/best-android-vpn-app). This feature belongs to the original Android app. No separate sign-in-removal implementation was found in the current Morphe Proton patch directory or its credentialless/login/guest code search results.

Morphe's [FreeAccountState.kt](https://github.com/hxreborn/morphe-patches/blob/main/patches/src/main/kotlin/app/morphe/patches/protonvpn/misc/restrictions/FreeAccountState.kt) identifies the existing user-info/VPN-user fields and observes updates. It does not create session tokens. The [settings patch](https://github.com/hxreborn/morphe-patches/blob/main/patches/src/main/kotlin/app/morphe/patches/protonvpn/misc/settings/PatchesSettingsPatch.kt) inserts patch settings and initializes its extension; it does not supply guest credentials.

Official Proton source shows the actual mechanism:

- [AuthenticationApi](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/data/src/main/kotlin/me/proton/core/auth/data/api/AuthenticationApi.kt) declares POST auth/v4/credentialless.
- [AuthRepositoryImpl](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/data/src/main/kotlin/me/proton/core/auth/data/repository/AuthRepositoryImpl.kt) constructs the request with challenge-frame data and maps the reply to session information without a username.
- [CreateLoginLessSession](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/domain/src/main/kotlin/me/proton/core/auth/domain/usecase/CreateLoginLessSession.kt) creates an account/session using the returned user ID, session ID, access token, refresh token and scopes, then delegates session handling to the account workflow. Post-login account setup follows.
- The Android app's [AccountViewModel](https://github.com/ProtonVPN/android-app/blob/fd1cb1dd108888e57b36ce8d7cc1dcdc25517c61/app/src/main/java/com/protonvpn/android/auth/ui/AccountViewModel.kt) handles the credentialless account workflow and becomes ready when account setup is ready.

Guest use still has an authenticated backend session. Hiding a sign-in page is not equivalent to implementing that session.

## Windows 5.1.8

The Windows source inspected is tag v5.1.8, commit d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9.

[ApiClient](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Api/ProtonVPN.Api/ApiClient.cs) issues normal connection certificates through an authorized request. [UserAuthenticator](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Auth/ProtonVPN.Client.Logic.Auth/UserAuthenticator.cs) still runs normal username/password login inside guest-hole recovery. Guest-hole bootstrap is temporary API recovery, separate from Android's credentialless account workflow.

## Live Windows compatibility attempt

On October 9, 2026, the independent source/GuestCompatibilityProbe.cjs used HTTPS with normal certificate validation and the honest Windows client identity windows-vpn@5.1.8. POST /auth/v4/sessions returned HTTP 200, Code 1000, providing a temporary unauthenticated session. POST /auth/v4/credentialless with that session and {Payload:{}} returned **HTTP 422, Code 5003**, with **Platform expected to be in [Android, iOS]**. The temporary session was revoked with DELETE /auth (HTTP 200). Only status/code/field names were retained; no access tokens, refresh tokens or account identifiers are distributed.

This is a platform rejection from the tested endpoint, before a usable Windows guest session was obtained. The probe supplies an empty challenge payload to test that boundary; it is not a complete guest client. No mobile identity was impersonated. Windows guest support therefore remains unavailable in the catalog, and existing sign-in and credential/certificate validation stay active. A future backend change requires another explicit compatibility check and the client work below.

## Work needed for a functioning Windows port

1. Implement a guest authentication client with Proton's request/challenge handling and platform compatibility. The tested endpoint currently rejects the Windows platform; backend support must change or a supported Windows flow must be identified.
2. Integrate the returned session and VPN-user state with Windows authentication, protected credential storage, refresh, revocation and recovery. Keep existing signed-in accounts intact.
3. Obtain and renew the connection certificate/key material through the guest session using the Windows connection service. Preserve certificate verification and server/protocol eligibility checks.
4. Add an explicit Continue as guest action and handle unavailable service, cancellation, restart, expiry and transition to normal sign-in.
5. Test an actual free-server tunnel, reconnect, process restart, token expiry and logout. Copied-method tests alone cannot validate this feature.

Patchwork's current declarative operations change existing return values, calls and selectors. They cannot add this network/session lifecycle. The Windows implementation therefore needs additional client code and a reviewable delivery mechanism before an executable guest-session patch can be supplied. No Android or Morphe implementation code is copied into this pack.
