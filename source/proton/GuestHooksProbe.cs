using System; using System.Collections; using System.Collections.Generic; using System.Reflection; using System.Runtime.CompilerServices; using System.Threading; using System.Threading.Tasks; using System.Net; using System.Net.Http;
using ProtonVPN.Api.Contracts; using ProtonVPN.Api.Contracts.Auth; using ProtonVPN.Api.Contracts.Users; using ProtonVPN.Client.Settings.Contracts; using ProtonVPN.Client.Logic.Auth.Contracts.Models;
public class HookProxy : DispatchProxy { public Func<MethodInfo,object[],object> Handler; protected override object Invoke(MethodInfo m,object[] a){return Handler(m,a);} public static object Make(Type type,Func<MethodInfo,object[],object> handler){var p=DispatchProxy.Create(type,typeof(HookProxy));((HookProxy)p).Handler=handler;return p;} }
class GuestHooksProbe {
 static Dictionary<string,object> settings=new Dictionary<string,object>(); static int requests,certificates,checks; static BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
 static void Check(bool value,string label){if(!value)throw new Exception(label);checks++;Console.WriteLine("PASS "+label);}
 static object Default(Type type){return type==typeof(void)?null:type.IsValueType?Activator.CreateInstance(type):null;}
 static object Settings(MethodInfo m,object[] a){string name=m.Name.Substring(4);if(m.Name.StartsWith("set_")){settings[name]=a[0];return null;}object value;if(settings.TryGetValue(name,out value))return value;if(name=="Ipv6Fragments")return Activator.CreateInstance(typeof(List<>).MakeGenericType(m.ReturnType.GetGenericArguments()));return Default(m.ReturnType);}
 static object Service(MethodInfo m,object[] a){
  if(m.Name=="get_IsDisconnected")return true;
  if(m.Name=="GetUserAsync"){requests++;return Task.FromResult(ApiResponseResult<UsersResponse>.Ok(new HttpResponseMessage(HttpStatusCode.OK),new UsersResponse{Code=1000,User=new UserResponse{UserId="NORMAL",Name="Normal",CreateTime=0}}));}
  if(m.Name=="GetVpnInfoResponse")return Task.FromResult(ApiResponseResult<VpnInfoWrapperResponse>.Ok(new HttpResponseMessage(HttpStatusCode.OK),new VpnInfoWrapperResponse{Code=1000,Vpn=new VpnInfoResponse{Status=1,MaxConnect=1,MaxTier=0}}));
  if(m.Name=="RequestNewCertificateAsync"){certificates++;settings["ConnectionCertificate"]=new ConnectionCertificate{Pem="TEST CERT",RequestUtcDate=DateTimeOffset.UtcNow,RefreshUtcDate=DateTimeOffset.UtcNow.AddHours(1),ExpirationUtcDate=DateTimeOffset.UtcNow.AddHours(2)};}
  if(m.ReturnType==typeof(Task))return Task.CompletedTask;
  if(m.ReturnType.IsGenericType&&m.ReturnType.GetGenericTypeDefinition()==typeof(Task<>)){var t=m.ReturnType.GetGenericArguments()[0];var value=t.IsClass?Activator.CreateInstance(t):Default(t);return typeof(Task).GetMethod("FromResult").MakeGenericMethod(t).Invoke(null,new[]{value});}
  return Default(m.ReturnType);
 }
 static int Main(){try{
  var type=Assembly.LoadFrom("ProtonVPN.Client.Logic.Auth.dll").GetType("ProtonVPN.Client.Logic.Auth.UserAuthenticator",true);var auth=RuntimeHelpers.GetUninitializedObject(type);
  foreach(var field in type.GetFields(flags)){if(field.FieldType.IsInterface)field.SetValue(auth,HookProxy.Make(field.FieldType,field.Name=="_settings"?(Func<MethodInfo,object[],object>)Settings:Service));else if(field.FieldType==typeof(CancellationTokenSource))field.SetValue(auth,new CancellationTokenSource());}
  settings["UserId"]="patchwork-guest:v1:TEST";settings["AccessToken"]="TEST_ACCESS";settings["RefreshToken"]="TEST_REFRESH";settings["UniqueSessionId"]="TEST_UID";
  var userTask=(Task<ApiResponseResult<UsersResponse>>)type.GetMethod("GetUserAsync",flags).Invoke(auth,null);var user=userTask.GetAwaiter().GetResult();
  Check(user.Success&&user.Value.User.Name=="Guest"&&requests==0,"actual auth DLL embedded guest-user hook executes under .NET 8");
  var resume=(Task<AuthResult>)type.GetMethod("AutoLoginUserAsync").Invoke(auth,new object[]{true});Check(resume.GetAwaiter().GetResult().Success&&certificates>0&&(bool)type.GetProperty("IsLoggedIn").GetValue(auth),"actual native CompleteLogin path resumes guest and requests certificate");
  settings["UserId"]="NORMAL";var normal=((Task<ApiResponseResult<UsersResponse>>)type.GetMethod("GetUserAsync",flags).Invoke(auth,null)).GetAwaiter().GetResult();Check(normal.Success&&requests==1&&normal.Value.User.Name=="Normal","normal account reaches original native user API");
  Console.WriteLine(checks+" actual guest hook checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e);return 1;}}
}
