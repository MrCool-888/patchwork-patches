using System; using System.Collections.Generic; using System.Linq; using System.Net.Http; using System.Reflection; using System.Threading; using System.Threading.Tasks;
using ProtonVPN.Api; using ProtonVPN.Api.Contracts; using ProtonVPN.Client.Settings.Contracts; using ProtonVPN.Client.Logic.Auth.Contracts;
using Patchwork.ProtonGuest;
// Explicit live integration probe. Native Windows API/token/certificate code, in-memory settings.
// No account files, service changes or tunnel; no secrets in output. Always revoke at the end.
public class LiveGuestProxy : DispatchProxy {
 public Func<MethodInfo,object[],object> Handler;
 protected override object Invoke(MethodInfo m,object[] a){return Handler(m,a);}
 public static object Make(Type type,Func<MethodInfo,object[],object> handler){var p=DispatchProxy.Create(type,typeof(LiveGuestProxy));((LiveGuestProxy)p).Handler=handler;return p;}
}
class GuestLiveProbe {
 static string stage="startup";
 static Dictionary<string,object> values = new Dictionary<string,object>();
 static object Default(Type type){return type==typeof(void)?null:type.IsValueType?Activator.CreateInstance(type):null;}
 static object Settings(MethodInfo m,object[] a){string key=m.Name.Substring(4);if(m.Name.StartsWith("set_")){values[key]=a[0];return null;}object value;return values.TryGetValue(key,out value)?value:Default(m.ReturnType);}
 static object Logger(MethodInfo m,object[] a){return Default(m.ReturnType);}
 static object Config(MethodInfo m,object[] a){if(m.Name=="get_ApiVersion")return "3";if(m.Name=="get_Urls")return LiveGuestProxy.Make(m.ReturnType,(method,args)=>method.Name=="get_ApiUrl"?"https://vpn-api.proton.me/":Default(method.ReturnType));return Default(m.ReturnType);}
 static object Construct(Type type,Func<ParameterInfo,object> argument){var ctor=type.GetConstructors().Single();return ctor.Invoke(ctor.GetParameters().Select(argument).ToArray());}
 static async Task Run(){
  var settings=(ISettings)LiveGuestProxy.Make(typeof(ISettings),Settings);values["Language"]="en-US";
  using(var protocol=new GuestProtocol())using(var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){BaseAddress=new Uri(GuestProtocol.BaseUrl+"/"),Timeout=TimeSpan.FromSeconds(30)}) {
   GuestSession session=null; IApiClient api=null;
   try {
    session=await protocol.CreateAsync(CancellationToken.None);Console.WriteLine("PASS guest client credentialless bootstrap and creation");
    settings.UserId=GuestClient.Prefix+session.UserId;settings.UniqueSessionId=session.Uid;settings.AccessToken=session.AccessToken;settings.RefreshToken=session.RefreshToken;
    var apiAssembly=typeof(ApiClient).Assembly;
    Func<ParameterInfo,object> args=p=>p.ParameterType==typeof(ISettings)?settings:p.ParameterType.Name=="IApiAppVersion"?LiveGuestProxy.Make(p.ParameterType,(m,a)=>m.Name=="get_AppVersion"?"windows-vpn@5.1.8":"Patchwork guest validation"):p.ParameterType.Name=="IConfiguration"?LiveGuestProxy.Make(p.ParameterType,Config):p.ParameterType.Name.Contains("HttpClientFactory")?LiveGuestProxy.Make(p.ParameterType,(m,a)=>http):LiveGuestProxy.Make(p.ParameterType,Logger);
    api=(IApiClient)Construct(typeof(ApiClient),args);
    var tokens=(ITokenClient)Construct(typeof(TokenClient),args);
    var refresh=await tokens.RefreshTokenAsync(CancellationToken.None);
    if(!refresh.Success||string.IsNullOrEmpty(refresh.Value.AccessToken)||string.IsNullOrEmpty(refresh.Value.RefreshToken))throw new Exception("Native Windows token refresh failed.");
    settings.AccessToken=refresh.Value.AccessToken;settings.RefreshToken=refresh.Value.RefreshToken;session.AccessToken=settings.AccessToken;session.RefreshToken=settings.RefreshToken;
    Console.WriteLine("PASS native Windows TokenClient refresh/rotation");
    var vpn=await api.GetVpnInfoResponse(CancellationToken.None);
    if(!vpn.Success||vpn.Value.Vpn==null||vpn.Value.Vpn.Status!=1||vpn.Value.Vpn.MaxTier!=0)throw new Exception("Native guest VPN info is unavailable or not free.");
    Console.WriteLine("PASS native Windows VPN plan reports authorized free access");
    stage="native key generation"; var crypto=Assembly.LoadFrom("ProtonVPN.Crypto.dll");var generator=Activator.CreateInstance(crypto.GetType("ProtonVPN.Crypto.Ed25519Asn1KeyGenerator",true));
    var auth=Assembly.LoadFrom("ProtonVPN.Client.Logic.Auth.dll");
    var keyManager=Construct(auth.GetType("ProtonVPN.Client.Logic.Auth.ConnectionKeyManager",true),p=>p.ParameterType==typeof(ISettings)?settings:p.ParameterType.Name=="IEd25519Asn1KeyGenerator"?generator:LiveGuestProxy.Make(p.ParameterType,Logger));
    stage="native certificate parser"; var parser=Construct(crypto.GetType("ProtonVPN.Crypto.CertificateParser",true),p=>LiveGuestProxy.Make(p.ParameterType,Logger));
    var manager=(IConnectionCertificateManager)Construct(auth.GetType("ProtonVPN.Client.Logic.Auth.ConnectionCertificateManager",true),p=>p.ParameterType==typeof(ISettings)?settings:p.ParameterType==typeof(IApiClient)?api:p.ParameterType.Name=="IConnectionKeyManager"?keyManager:p.ParameterType.Name=="ICertificateParser"?parser:LiveGuestProxy.Make(p.ParameterType,Logger));
    stage="native certificate issuance"; await manager.ForceRequestNewKeyPairAndCertificateAsync();
    if(!settings.ConnectionCertificate.HasValue||string.IsNullOrEmpty(settings.ConnectionCertificate.Value.Pem)||settings.ConnectionCertificate.Value.ExpirationUtcDate<=DateTimeOffset.UtcNow)throw new Exception("Native guest certificate issuance failed.");
    Console.WriteLine("PASS native Windows key and certificate managers issue an unexpired guest certificate");
    var servers=await api.GetServersAsync(null,null,CancellationToken.None);
    if(!servers.Success||servers.Value==null)throw new Exception("Native signed server list request failed.");
    Console.WriteLine("PASS native Windows signed server list request");
    await manager.ForceRequestNewCertificateAsync(CancellationToken.None);
    Console.WriteLine("PASS native Windows guest certificate renewal");
    manager.DeleteKeyPairAndCertificate();
   } finally {
    if(session!=null){if(api!=null){var result=await api.GetLogoutResponse();if(!result.Success)throw new Exception("Native logout request failed.");Console.WriteLine("PASS native Windows guest logout/revocation");}else await protocol.RevokeAsync(session);}
    values.Clear();
   }
  }
 }
 static int Main(){try{Run().GetAwaiter().GetResult();return 0;}catch(Exception error){Console.Error.WriteLine("Live guest integration probe failed at "+stage+" ("+error.GetType().Name+"); no credentials or raw responses logged.");return 1;}}
}
