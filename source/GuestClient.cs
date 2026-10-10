using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ProtonVPN.Api.Contracts;
using ProtonVPN.Api.Contracts.Users;
using ProtonVPN.Client.Logic.Auth.Contracts.Enums;
using ProtonVPN.Client.Logic.Auth.Contracts.Models;
using ProtonVPN.Client.Settings.Contracts;

namespace Patchwork.ProtonGuest
{
    // Independent port of the official credentialless API workflow. No premium entitlement edits.
    // Local guest IDs isolate guest preferences in the existing encrypted settings store.
    public static class GuestClient
    {
        internal const string Prefix = "patchwork-guest:v1:";
        static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        internal static object Field(object target, string name)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType) {
                FieldInfo field = type.GetField(name, Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(target);
            }
            throw new InvalidOperationException("Guest client field mismatch.");
        }
        internal static object Call(object target, string name, params object[] args)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType) {
                MethodInfo method = type.GetMethod(name, Instance | BindingFlags.DeclaredOnly);
                if (method != null) return method.Invoke(target, args);
            }
            throw new InvalidOperationException("Guest client method mismatch.");
        }
        static object Get(object target, string name) { return target.GetType().GetProperty(name, Instance).GetValue(target); }
        static void Set(object target, string name, object value) { target.GetType().GetProperty(name, Instance).SetValue(target, value); }
        internal static bool IsGuest(ISettings settings) { return settings.UserId != null && settings.UserId.StartsWith(Prefix, StringComparison.Ordinal); }
        static void Status(object auth, AuthenticationStatus status) { Call(auth, "SetAuthenticationStatus", status, null); }
        static CancellationToken Token(object auth) { return ((CancellationTokenSource)Field(auth, "_cts")).Token; }

        public static object GuestUser(object auth, object[] args)
        {
            var settings = (ISettings)Field(auth, "_settings");
            if (!IsGuest(settings)) return null; // Preserve the original account path.
            settings.Username = "Guest"; settings.UserDisplayName = "Guest"; settings.UserEmail = null;
            if (!settings.UserCreationDateUtc.HasValue) settings.UserCreationDateUtc = DateTimeOffset.UtcNow;
            var user = new UserResponse { UserId = settings.UserId, Name = "Guest", DisplayName = "Guest", Email = null, CreateTime = settings.UserCreationDateUtc.Value.ToUnixTimeSeconds() };
            var response = ApiResponseResult<UsersResponse>.Ok(new HttpResponseMessage(HttpStatusCode.OK), new UsersResponse { Code = 1000, User = user });
            return Task.FromResult(response);
        }
        public static object Resume(object auth, object[] args)
        {
            var settings = (ISettings)Field(auth, "_settings");
            return IsGuest(settings) ? (object)ResumeAsync(auth, (bool)args[0]) : null;
        }
        static async Task RequireVpnAsync(object auth, CancellationToken token)
        {
            var api = (IApiClient)Field(auth, "_apiClient");
            var response = await api.GetVpnInfoResponse(token);
            if (!response.Success || response.Value == null || response.Value.Vpn == null || response.Value.Vpn.Status != 1 || response.Value.Vpn.MaxConnect < 1) throw new GuestFailure("The guest session could not be authorized for VPN use. Please try again or sign in normally.");
        }
        static async Task RequireCertificateAsync(object auth, CancellationToken token)
        {
            await (Task)Call(Field(auth, "_connectionCertificateManager"), "RequestNewCertificateAsync", token, null);
            var settings = (ISettings)Field(auth, "_settings");
            if (!settings.ConnectionCertificate.HasValue || string.IsNullOrEmpty(settings.ConnectionCertificate.Value.Pem) || settings.ConnectionCertificate.Value.ExpirationUtcDate <= DateTimeOffset.UtcNow) throw new GuestFailure("The guest connection certificate could not be obtained. Please try again.");
        }
        static async Task EndFailedSessionAsync(object auth)
        {
            try { await (Task)Call(auth, "LogoutAsync", LogoutReason.SessionExpired); }
            catch (Exception) {
                // Still revoke through the native HTTP pipeline when a service disconnect failed.
                try { await ((IApiClient)Field(auth, "_apiClient")).GetLogoutResponse(); } catch (Exception) { }
            }
            finally {
                try { Call(Field(auth, "_connectionCertificateManager"), "DeleteKeyPairAndCertificate"); }
                finally { Call(auth, "ClearAuthSessionDetails"); Status(auth, AuthenticationStatus.LoggedOut); }
            }
        }
        static async Task<AuthResult> ResumeAsync(object auth, bool startup)
        {
            var settings = (ISettings)Field(auth, "_settings");
            if (!(bool)Call(auth, "HasAuthenticatedSessionData")) { Call(auth, "ClearAuthSessionDetails"); return AuthResult.Ok(); }
            Status(auth, AuthenticationStatus.LoggingIn); Call(auth, "ResetCancellationTokenIfCancelled");
            try {
                await RequireVpnAsync(auth, Token(auth)); // Native HTTP pipeline handles refresh/rotation.
                var result = await (Task<AuthResult>)Call(auth, "CompleteLoginAsync", startup, false);
                if (!result.Success) { await EndFailedSessionAsync(auth); return result; }
                await RequireCertificateAsync(auth, Token(auth));
                Token(auth).ThrowIfCancellationRequested();
                Status(auth, AuthenticationStatus.LoggedIn);
                return result;
            } catch (Exception) {
                await EndFailedSessionAsync(auth);
                return AuthResult.Fail(AuthError.GetSessionDetailsFailed, "Guest session expired or could not be resumed. Continue as guest again or sign in.");
            }
        }
        internal static async Task<AuthResult> LoginAsync(object auth, GuestProtocol protocol)
        {
            var settings = (ISettings)Field(auth, "_settings");
            if ((bool)Call(auth, "HasAuthenticatedSessionData")) return AuthResult.Fail(AuthError.Unknown, "Sign out of the current session before continuing as guest.");
            Status(auth, AuthenticationStatus.LoggingIn); Call(auth, "ResetCancellationTokenIfCancelled");
            GuestSession session = null; bool accepted = false, stored = false;
            try {
                session = await protocol.CreateAsync(Token(auth));
                Token(auth).ThrowIfCancellationRequested();
                settings.UserId = Prefix + session.UserId;
                settings.AccessToken = session.AccessToken; settings.RefreshToken = session.RefreshToken; settings.UniqueSessionId = session.Uid;
                stored = true;
                await RequireVpnAsync(auth, Token(auth));
                var result = await (Task<AuthResult>)Call(auth, "CompleteLoginAsync", false, false);
                if (!result.Success) return result;
                await RequireCertificateAsync(auth, Token(auth));
                Token(auth).ThrowIfCancellationRequested();
                Status(auth, AuthenticationStatus.LoggedIn); accepted = true; return result;
            } catch (OperationCanceledException) { return AuthResult.Fail(AuthError.None, "Guest sign-in cancelled."); }
            catch (GuestFailure error) { return AuthResult.Fail(AuthError.Unknown, error.Message); }
            catch (Exception) { return AuthResult.Fail(AuthError.Unknown, "Guest sign-in failed. Check your connection and try again, or sign in normally."); }
            finally {
                if (!accepted) {
                    try {
                        if (stored) await EndFailedSessionAsync(auth);
                        else { Call(auth, "ClearAuthSessionDetails"); Status(auth, AuthenticationStatus.LoggedOut); }
                    } finally { if (session != null) await protocol.RevokeAsync(session); }
                }
            }
        }
        public static object AddButton(object page, object[] args)
        {
            var signIn = (Button)Field(page, "SignInButton"); var panel = signIn.Parent as StackPanel ?? FindPanel((UIElement)page, signIn, 0);
            if (panel == null) throw new InvalidOperationException("Guest button parent mismatch.");
            foreach (var child in panel.Children) if (child is FrameworkElement && ((FrameworkElement)child).Name == "PatchworkGuestButton") return null;
            var vm = Get(page, "ViewModel"); var auth = Field(vm, "_userAuthenticator"); bool busy = false;
            var button = new Button { Name = "PatchworkGuestButton", Content = "Continue as guest", IsEnabled = !(bool)Get(vm, "IsSigningIn"), HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center };
            var message = new TextBlock { TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button, "PatchworkGuestButton");
            button.Click += async delegate {
                if (busy) { Call(auth, "CancelAuth"); button.Content = "Cancelling…"; button.IsEnabled = false; return; }
                if ((bool)Get(vm, "IsSigningIn")) return;
                busy = true; Set(vm, "IsSigningIn", true); button.Content = "Cancel guest sign-in"; message.Visibility = Visibility.Collapsed;
                try {
                    using (var protocol = new GuestProtocol()) {
                        var result = await LoginAsync(auth, protocol);
                        busy = false;
                        if (result.Success) Call(vm, "HandleSuccess");
                        else { Call(vm, "HandleError", result); message.Text = result.Error; message.Visibility = Visibility.Visible; }
                    }
                } catch (Exception) { message.Text = "Guest mode could not start. Please try again or sign in normally."; message.Visibility = Visibility.Visible; }
                finally { busy = false; Set(vm, "IsSigningIn", false); button.Content = "Continue as guest"; button.IsEnabled = true; }
            };
            ((INotifyPropertyChanged)vm).PropertyChanged += delegate(object sender, PropertyChangedEventArgs e) { if (e.PropertyName == "IsSigningIn") button.IsEnabled = busy || !(bool)Get(vm, "IsSigningIn"); };
            ((FrameworkElement)page).Unloaded += delegate { if (busy && !(bool)Get(auth, "IsLoggedIn")) Call(auth, "CancelAuth"); };
            panel.Children.Add(button); panel.Children.Add(message); return null;
        }
        static StackPanel FindPanel(UIElement root, Button button, int depth)
        {
            if (root == null || depth > 32) return null;
            var panel = root as Panel;
            if (panel != null) {
                foreach (var child in panel.Children) {
                    if (ReferenceEquals(child, button)) return panel as StackPanel;
                    var found = FindPanel(child, button, depth + 1); if (found != null) return found;
                }
            }
            var page = root as Page; if (page != null) return FindPanel(page.Content as UIElement, button, depth + 1);
            var user = root as UserControl; if (user != null) return FindPanel(user.Content as UIElement, button, depth + 1);
            var content = root as ContentControl; if (content != null) return FindPanel(content.Content as UIElement, button, depth + 1);
            var border = root as Border; if (border != null) return FindPanel(border.Child, button, depth + 1);
            return null;
        }
    }
    internal sealed class GuestFailure : Exception { public GuestFailure(string message) : base(message) { } }
    internal sealed class GuestSession { public string Uid, UserId, AccessToken, RefreshToken; }
    internal sealed class GuestProtocol : IDisposable
    {
        internal const string BaseUrl = "https://vpn-api.proton.me";
        readonly HttpClient client;
        internal GuestProtocol() : this(new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })) { }
        internal GuestProtocol(HttpClient client) { this.client = client; client.Timeout = TimeSpan.FromSeconds(30); }
        static string Text(JsonElement body, string name) { JsonElement item; return body.TryGetProperty(name, out item) && item.ValueKind == JsonValueKind.String ? item.GetString() : null; }
        async Task<JsonDocument> RequestAsync(string endpoint, HttpMethod method, GuestSession session, object payload, CancellationToken token)
        {
            using (var request = new HttpRequestMessage(method, BaseUrl + endpoint)) {
                request.Headers.Add("x-pm-appversion", "android-vpn@5.20.57.0"); request.Headers.Add("x-pm-apiversion", "3"); request.Headers.Add("Accept", "application/json");
                request.Headers.Add("User-Agent", "Patchwork Proton guest client");
                if (session != null) { request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.AccessToken); request.Headers.Add("x-pm-uid", session.Uid); }
                if (payload != null) request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false)) {
                    if (response.Content.Headers.ContentLength > 256 * 1024) throw new GuestFailure("Unexpected guest service response.");
                    byte[] bytes;
                    using (var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false)) using (var buffer = new MemoryStream()) {
                        var block = new byte[4096]; int count;
                        while ((count = await input.ReadAsync(block, 0, block.Length, token).ConfigureAwait(false)) != 0) {
                            if (buffer.Length + count > 256 * 1024) throw new GuestFailure("Unexpected guest service response.");
                            buffer.Write(block, 0, count);
                        }
                        bytes = buffer.ToArray();
                    }
                    var body = JsonDocument.Parse(bytes); JsonElement code;
                    if (!response.IsSuccessStatusCode || !body.RootElement.TryGetProperty("Code", out code) || code.GetInt32() != 1000) {
                        bool human = body.RootElement.TryGetProperty("Code", out code) && code.GetInt32() == 9001;
                        body.Dispose(); throw new GuestFailure(human ? "Proton requires human verification. Guest mode stopped; use normal sign-in to complete verification." : "Proton did not authorize guest mode. Please try again later or sign in normally.");
                    }
                    return body;
                }
            }
        }
        static GuestSession Session(JsonElement body)
        {
            var session = new GuestSession { Uid = Text(body, "UID"), UserId = Text(body, "UserID"), AccessToken = Text(body, "AccessToken"), RefreshToken = Text(body, "RefreshToken") };
            return session;
        }
        static void ValidateSession(GuestSession session, bool userRequired)
        {
            if (string.IsNullOrEmpty(session.Uid) || string.IsNullOrEmpty(session.AccessToken) || string.IsNullOrEmpty(session.RefreshToken) || userRequired && string.IsNullOrEmpty(session.UserId)) throw new GuestFailure("Guest service returned incomplete session details.");
        }
        internal async Task<GuestSession> CreateAsync(CancellationToken token)
        {
            GuestSession bootstrap = null, guest = null; bool accepted = false;
            try {
                using (var body = await RequestAsync("/auth/v4/sessions", HttpMethod.Post, null, null, token)) bootstrap = Session(body.RootElement);
                ValidateSession(bootstrap, false);
                using (var body = await RequestAsync("/auth/v4/credentialless", HttpMethod.Post, bootstrap, new { Payload = new Dictionary<string, object>() }, token)) guest = Session(body.RootElement);
                ValidateSession(guest, true);
                token.ThrowIfCancellationRequested(); accepted = true; return guest;
            } finally {
                if (!accepted && guest != null && !string.IsNullOrEmpty(guest.Uid) && !string.IsNullOrEmpty(guest.AccessToken)) await RevokeAsync(guest);
                if (bootstrap != null && !string.IsNullOrEmpty(bootstrap.Uid) && !string.IsNullOrEmpty(bootstrap.AccessToken) && (!accepted || guest == null || guest.Uid != bootstrap.Uid)) await RevokeAsync(bootstrap);
            }
        }
        internal async Task RevokeAsync(GuestSession session)
        {
            try { using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10))) using (await RequestAsync("/auth", HttpMethod.Delete, session, null, timeout.Token)) { } }
            catch { /* Local credentials are discarded regardless; server expiry remains authoritative. */ }
        }
        public void Dispose() { client.Dispose(); }
    }
}
