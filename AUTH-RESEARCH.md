# Guest session research

Reviewed October 9, 2026. Experimental prerelease 1.5.0 integrates credentialless sessions with Windows authentication and certificate managers. The actual installed-client guest tunnel test remains pending. This is not yet ready for a stable release.

## Official Android workflow

The Android guest feature belongs to the original Proton app. Morphe's existing free-account and settings patches do not create tokens. Proton's [AuthenticationApi](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/data/src/main/kotlin/me/proton/core/auth/data/api/AuthenticationApi.kt) declares POST auth/v4/credentialless. [AuthRepositoryImpl](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/data/src/main/kotlin/me/proton/core/auth/data/repository/AuthRepositoryImpl.kt) maps its response to session information without a username. [CreateLoginLessSession](https://github.com/ProtonMail/protoncore_android/blob/1b87f94ebfdfaf5e67145e8668efc52dbb931e0b/auth/domain/src/main/kotlin/me/proton/core/auth/domain/usecase/CreateLoginLessSession.kt) delegates storage and account state to the account workflow. PostLoginLessAccountSetup creates a credentialless user representation and refreshes scopes before marking the account ready.

Guest use still needs an authenticated backend session and connection credentials. Hiding the login page alone does not provide them.

## Live compatibility results

The independent source/GuestCompatibilityProbe.cjs uses normal HTTPS validation, holds session tokens only in memory, prints status/code/credential-presence booleans, and revokes its temporary sessions. It never prints raw replies, identifiers, credentials, private keys or certificates.

Earlier Windows-identity attempt:

- POST /auth/v4/sessions using windows-vpn@5.1.8: HTTP 200, Code 1000.
- POST /auth/v4/credentialless with Payload {}: HTTP 422, Code 5003, requiring Android or iOS.
- Temporary session revoked with DELETE /auth: HTTP 200.

Requested Android-identity attempt, using android-vpn@5.20.57.0 (the [official Android release](https://github.com/ProtonVPN/android-app/releases/tag/5.20.57.0)):

- Unauthenticated bootstrap: HTTP 200, Code 1000.
- Credentialless request with Payload {}: HTTP 200, Code 1000; guest session issued.
- GET /vpn/v2 with that guest session and windows-vpn@5.1.8: HTTP 200, Code 1000; VPN info and connection credential fields present.
- POST /vpn/v1/certificate with Windows identity, a newly generated in-memory Ed25519 public key, EC/session mode and no optional features: HTTP 200, Code 1000; certificate present.
- Both guest and bootstrap sessions revoked with DELETE /auth: HTTP 200 each. Private key material was discarded; no credentials were persisted.

The change was the client identity header. No Android device telemetry or challenge answers were fabricated. The optional probe stops on human verification without solving or bypassing it. These results establish that the tested mobile session can authorize Windows API requests; they do not establish a working tunnel, certificate renewal, reconnect or restart behavior. The earlier conclusion that Windows-platform rejection alone prevented a port is superseded by this successful test.

## Windows integration in the local candidate

The [Windows 5.1.8 UserAuthenticator](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Auth/ProtonVPN.Client.Logic.Auth/UserAuthenticator.cs) uses its SRP login path before CompleteLoginAsync. Guest-hole recovery still performs normal login. [ApiClient](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Api/ProtonVPN.Api/ApiClient.cs) obtains VPN info and connection certificates through the current authorized session.

The independent [GuestClient.cs](source/GuestClient.cs) now implements:

1. Continue as guest beside normal sign-in, with native loading-page cancellation and generic errors. In r7, navigation unloading the sign-in page no longer cancels authentication. A single captured native token spans the attempt, and 30-second bootstrap deadlines cover headers and body reads. Timeout errors are distinct from user cancellation. Real WinUI tests cover insertion, accessibility, busy state, duplicate prevention, loading-page navigation, Cancel and retry.
2. Session storage through the existing protected ISettings contract. Only the local user ID receives a guest namespace for separate preferences; API credentials and UID remain unchanged. Active sessions cannot be overwritten.
3. The native CompleteLoginAsync workflow, real free VPN authorization and unexpired native certificate validation before publishing the logged-in event. Normal accounts fall through to their original methods.
4. Startup resume through the native refresh pipeline, fail-closed expiry, and native logout/disconnect/key removal. Temporary bootstrap and failed guest sessions are revoked. Token-prefix logging is suppressed when the guest patch is selected.

Patchwork 0.7.0 supplies managedEmbeddedHook: explicitly declared, hash-verified executable managed code embedded in the target assemblies. Library and preview identify it. Import, preview and patch application never execute the helper. The patcher installer includes neither this helper nor Proton patches.

GuestLifecycleProbe covers cancellation, incomplete credentials, certificate failure/expiry, failed authorization, resume, normal-account preservation and cleanup. An actual patched UserAuthenticator DLL passes guest-user dispatch, native CompleteLogin/resume and normal-user fallthrough. GuestLiveProbe passes real native Windows token rotation, free-plan authorization, key/certificate issuance, signed server retrieval, certificate renewal and logout. Live test sessions were revoked; tokens, keys and identifiers were held in memory and never logged.

Still required for stable release: an actual installed-client guest connection, restart/reconnect, normal sign-in transition, and traffic/DNS verification. The service requires its installed executable path; a copied client cannot test a tunnel, and service authorization has not been weakened. No production account files or installed assemblies have been changed by these probes.
