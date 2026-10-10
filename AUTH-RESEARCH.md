# Guest session research

Reviewed October 9, 2026. Pack 1.4.0 still requires sign-in. The new Android-identity probe succeeded through guest session, VPN credentials and connection certificate issuance; the Windows client has not yet been integrated with that workflow.

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

## Windows integration still needed

The [Windows 5.1.8 UserAuthenticator](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Client/Logic/Auth/ProtonVPN.Client.Logic.Auth/UserAuthenticator.cs) uses its SRP login path before CompleteLoginAsync. Guest-hole recovery still performs normal login. [ApiClient](https://github.com/ProtonVPN/win-app/blob/v5.1.8/src/Api/ProtonVPN.Api/ApiClient.cs) obtains VPN info and connection certificates through the current authorized session.

A complete port needs:

1. An explicit Continue as guest action that handles unavailable service, cancellation and human verification.
2. Integration of the guest session and credentialless account state into the Windows auth workflow, with protected token storage, refresh, revocation and recovery. Existing accounts must remain intact.
3. Use of the existing connection-key/certificate manager and service, preserving certificate checks and free-server/protocol eligibility.
4. Restart, expiry, refresh, reconnect, logout and transition-to-normal-sign-in behavior.
5. A real free-server tunnel and traffic/DNS verification on a test machine.

The current declarative patch operations cannot supply the complete asynchronous network/session lifecycle. A reviewable client-code delivery mechanism is required before shipping that integration. The compatibility probe is separate research source, never run by patch application and never bundled into the installer. The catalog entry remains planned, accurately indicating that the API experiment succeeded but the guest client is not implemented.
