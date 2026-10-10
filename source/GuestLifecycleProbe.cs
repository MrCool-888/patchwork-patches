using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ProtonVPN.Api.Contracts;
using ProtonVPN.Api.Contracts.Auth;
using ProtonVPN.Client.Settings.Contracts;
using ProtonVPN.Client.Logic.Auth.Contracts.Enums;
using ProtonVPN.Client.Logic.Auth.Contracts.Models;
using Patchwork.ProtonGuest;

// Only fake HTTP and settings. No installed client, registry, service or real sessions.
public class GuestSettingsProxy : DispatchProxy
{
    public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
    protected override object Invoke(MethodInfo method, object[] args) {
        string key = method.Name.Substring(4);
        if (method.Name.StartsWith("set_")) { Values[key] = args[0]; return null; }
        object value; return Values.TryGetValue(key, out value) ? value : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null;
    }
}
public class GuestApiProxy : DispatchProxy
{
    public bool Authorized = true;
    protected override object Invoke(MethodInfo method, object[] args) {
        if (method.Name == "GetVpnInfoResponse") return Task.FromResult(Authorized
            ? ApiResponseResult<VpnInfoWrapperResponse>.Ok(new HttpResponseMessage(HttpStatusCode.OK), new VpnInfoWrapperResponse { Code = 1000, Vpn = new VpnInfoResponse { Status = 1, MaxConnect = 1, MaxTier = 0 } })
            : ApiResponseResult<VpnInfoWrapperResponse>.Fail(new HttpResponseMessage(HttpStatusCode.Unauthorized), "Expired"));
        throw new Exception("Unexpected fake API method.");
    }
}
class GuestFakeCertificate
{
    public ISettings Settings; public bool Available = true, Expired; public int Requests;
    public void DeleteKeyPairAndCertificate() { Settings.ConnectionCertificate = null; }
    public Task RequestNewCertificateAsync(CancellationToken token, string expiredCertificate) {
        token.ThrowIfCancellationRequested(); Requests++;
        if (Available) Settings.ConnectionCertificate = new ConnectionCertificate { Pem = "TEST CERTIFICATE", RequestUtcDate = DateTimeOffset.UtcNow, RefreshUtcDate = DateTimeOffset.UtcNow.AddHours(1), ExpirationUtcDate = DateTimeOffset.UtcNow.AddHours(Expired ? -2 : 2) };
        return Task.CompletedTask;
    }
}
class GuestFakeAuth
{
    public ISettings _settings; public IApiClient _apiClient; public GuestFakeCertificate _connectionCertificateManager;
    public CancellationTokenSource _cts = new CancellationTokenSource();
    public AuthenticationStatus AuthenticationStatus; public bool IsLoggedIn { get { return AuthenticationStatus == AuthenticationStatus.LoggedIn; } }
    public int Completions, Logouts; public bool CompleteSuccess = true;
    public void SetAuthenticationStatus(AuthenticationStatus status, LogoutReason? reason) { AuthenticationStatus = status; }
    public void ResetCancellationTokenIfCancelled() { if (_cts.IsCancellationRequested) { _cts.Dispose(); _cts = new CancellationTokenSource(); } }
    public bool HasAuthenticatedSessionData() { return !string.IsNullOrEmpty(_settings.AccessToken) && !string.IsNullOrEmpty(_settings.RefreshToken) && !string.IsNullOrEmpty(_settings.UniqueSessionId); }
    public void ClearAuthSessionDetails() { _settings.UserId = null; _settings.UniqueSessionId = null; _settings.AccessToken = null; _settings.RefreshToken = null; }
    public Task<AuthResult> CompleteLoginAsync(bool startup, bool send) { Completions++; if(send)throw new Exception("Guest login published logged-in before certificate validation"); GuestClient.GuestUser(this, new object[0]); return Task.FromResult(CompleteSuccess ? AuthResult.Ok() : AuthResult.Fail(AuthError.Unknown)); }
    public Task LogoutAsync(LogoutReason reason) { Logouts++; _settings.ConnectionCertificate = null; ClearAuthSessionDetails(); AuthenticationStatus = AuthenticationStatus.LoggedOut; return Task.CompletedTask; }
}
class GuestFakeHttp : HttpMessageHandler
{
    public int Creates, Guests, Revokes; public bool Challenge, Incomplete, Partial, Cancel, Fail, SameUid, MissingAccess;
    public CancellationTokenSource Cancellation;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
        if (request.Headers.GetValues("x-pm-appversion").GetEnumerator().MoveNext() == false) throw new Exception("No app identity");
        string body;
        if (request.Method == HttpMethod.Delete) { Revokes++; body = "{\"Code\":1000}"; }
        else if (request.RequestUri.AbsolutePath.EndsWith("credentialless")) {
            Guests++;
            if (Fail) throw new HttpRequestException("FAKE NETWORK FAILURE");
            if (Cancel) Cancellation.Cancel();
            body = Challenge ? "{\"Code\":9001}" : Incomplete ? "{\"Code\":1000}" : "{\"Code\":1000,\"UID\":\"" + (SameUid ? "BOOTSTRAP" : "GUEST") + "\",\"UserID\":\"" + (Partial ? "" : "TEST_USER") + "\",\"AccessToken\":\"" + (MissingAccess ? "" : "TEST_GUEST_ACCESS") + "\",\"RefreshToken\":\"TEST_GUEST_REFRESH\"}";
        } else { Creates++; body = "{\"Code\":1000,\"UID\":\"BOOTSTRAP\",\"AccessToken\":\"TEST_BOOTSTRAP_ACCESS\",\"RefreshToken\":\"TEST_BOOTSTRAP_REFRESH\"}"; }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
    }
}
class GuestLifecycleProbe
{
    static int checks;
    static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; Console.WriteLine("PASS " + label); }
    static GuestFakeAuth Auth() {
        var settings = DispatchProxy.Create<ISettings, GuestSettingsProxy>(); var api = DispatchProxy.Create<IApiClient, GuestApiProxy>();
        return new GuestFakeAuth { _settings = settings, _apiClient = api, _connectionCertificateManager = new GuestFakeCertificate { Settings = settings } };
    }
    static async Task Run() {
        var auth = Auth(); var http = new GuestFakeHttp();
        using (var protocol = new GuestProtocol(new HttpClient(http))) {
            var result = await GuestClient.LoginAsync(auth, protocol);
            Check(result.Success && auth.IsLoggedIn, "guest login enters native logged-in state");
            Check(auth._settings.UserId == GuestClient.Prefix + "TEST_USER" && auth._settings.Username == "Guest", "guest preferences use isolated local ID");
            Check(auth._settings.RefreshToken == "TEST_GUEST_REFRESH" && auth._settings.UniqueSessionId == "GUEST", "session stored through native settings contract");
            Check(auth._connectionCertificateManager.Requests == 1 && auth._settings.ConnectionCertificate.HasValue, "native certificate requested before success");
            Check(http.Creates == 1 && http.Guests == 1 && http.Revokes == 1, "bootstrap revoked and successful guest retained");
            var resume = await (Task<AuthResult>)GuestClient.Resume(auth, new object[] { true });
            Check(resume.Success && http.Guests == 1 && auth.Completions == 2, "restart resumes existing session without creating another guest");
            Check(GuestClient.GuestUser(Auth(), new object[0]) == null && GuestClient.Resume(Auth(), new object[] { true }) == null, "normal accounts fall through to original methods");
            var again = await GuestClient.LoginAsync(auth, protocol);
            Check(!again.Success && http.Guests == 1 && auth._settings.UserId == GuestClient.Prefix + "TEST_USER", "existing session cannot be overwritten");
            await auth.LogoutAsync(LogoutReason.UserAction);
            Check(!auth.HasAuthenticatedSessionData() && !auth._settings.ConnectionCertificate.HasValue, "native logout clears session and certificate");
        }
        foreach (string scenario in new[] { "challenge", "incomplete", "partial", "network", "cancel", "certificate", "expired-certificate", "complete", "unauthorized" }) {
            auth = Auth(); http = new GuestFakeHttp { Challenge = scenario == "challenge", Incomplete = scenario == "incomplete", Partial = scenario == "partial", Fail = scenario == "network", Cancel = scenario == "cancel", Cancellation = auth._cts };
            auth._connectionCertificateManager.Available = scenario != "certificate"; auth._connectionCertificateManager.Expired = scenario == "expired-certificate"; auth.CompleteSuccess = scenario != "complete"; ((GuestApiProxy)auth._apiClient).Authorized = scenario != "unauthorized";
            using (var protocol = new GuestProtocol(new HttpClient(http))) {
                var result = await GuestClient.LoginAsync(auth, protocol);
                Check(!result.Success && !auth.IsLoggedIn && !auth.HasAuthenticatedSessionData(), scenario + " fails closed and clears session");
                Check(http.Revokes >= 1, scenario + " revokes created temporary sessions");
                if (scenario == "partial") Check(http.Revokes == 2, "incomplete issued guest credentials are revoked too");
                Check(!result.Error.Contains("TEST_GUEST") && !result.Error.Contains("TEST_BOOTSTRAP"), scenario + " error contains no credentials");
            }
        }
        auth = Auth(); http = new GuestFakeHttp { SameUid = true };
        using (var protocol = new GuestProtocol(new HttpClient(http))) { Check((await GuestClient.LoginAsync(auth, protocol)).Success && http.Revokes == 0, "promoted bootstrap UID is not revoked on success"); }
        ((GuestApiProxy)auth._apiClient).Authorized = false;
        Check(!(await (Task<AuthResult>)GuestClient.Resume(auth, new object[] { true })).Success && auth.Logouts == 1 && !auth.HasAuthenticatedSessionData(), "expired guest resumes fail closed through native logout");
        auth = Auth(); http = new GuestFakeHttp { SameUid = true, MissingAccess = true };
        using (var protocol = new GuestProtocol(new HttpClient(http))) Check(!(await GuestClient.LoginAsync(auth, protocol)).Success && http.Revokes == 1 && !auth.HasAuthenticatedSessionData(), "incomplete promoted session still revokes its bootstrap");
        Console.WriteLine(checks + " guest lifecycle checks passed.");
    }
    static int Main() { try { Run().GetAwaiter().GetResult(); return 0; } catch (Exception error) { Console.Error.WriteLine(error); return 1; } }
}
