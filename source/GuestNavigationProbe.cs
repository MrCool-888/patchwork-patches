using System; using System.ComponentModel; using System.Reflection; using System.Runtime.InteropServices;
using System.Net.Http; using System.Threading; using System.Threading.Tasks;
using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls; using Microsoft.UI.Xaml.Automation.Peers; using Microsoft.UI.Xaml.Automation.Provider;
using ProtonVPN.Client.EventMessaging.Contracts; using ProtonVPN.Client.Core.Messages; using ProtonVPN.Client.Core.Enums;
using ProtonVPN.Client.Logic.Auth.Contracts.Enums; using ProtonVPN.Client.Logic.Auth.Contracts.Models; using Patchwork.ProtonGuest;

class GuestUiSender : IEventMessageSender {
 public int Starts;
 public void Send<T>(T message) where T:class { var state=message as LoginStateChangedMessage; if(state!=null&&state.Value==LoginState.Authenticating)Starts++; }
 public void Send<T>() where T:class { }
}
class GuestUiVm : INotifyPropertyChanged {
 public GuestFakeAuth _userAuthenticator=GuestLifecycleProbe.Auth(); public GuestUiSender _eventMessageSender=new GuestUiSender();
 public TaskCompletionSource<AuthResult> Completed=new TaskCompletionSource<AuthResult>(); bool signing;
 public bool IsSigningIn { get{return signing;} set{signing=value; if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("IsSigningIn"));} }
 public event PropertyChangedEventHandler PropertyChanged;
 public void HandleSuccess(){Completed.TrySetResult(AuthResult.Ok());}
 public void HandleError(AuthResult result){Completed.TrySetResult(result);}
}
class GuestUiPage : Grid {
 public Button SignInButton=new Button{Content="Sign in"}; public GuestUiVm ViewModel{get;private set;} public StackPanel Panel=new StackPanel{Spacing=8};
 public GuestUiPage(){ViewModel=new GuestUiVm();Panel.Children.Add(SignInButton);Children.Add(Panel);}
}
class GuestUiHttp : HttpMessageHandler {
 public TaskCompletionSource<bool> Release=new TaskCompletionSource<bool>(); public int Creates;
 readonly GuestFakeHttp responses=new GuestFakeHttp();
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
  if(request.Method==HttpMethod.Post&&request.RequestUri.AbsolutePath.EndsWith("sessions")){Creates++;await Release.Task.WaitAsync(token);}
  using(var invoker=new HttpMessageInvoker(responses,false))return await invoker.SendAsync(request,token);
 }
}
class GuestUiApp : Application {
 Window window; Frame frame;
 public GuestUiApp(){UnhandledException+=delegate(object sender,Microsoft.UI.Xaml.UnhandledExceptionEventArgs e){GuestUiProbe.Failure=e.Exception;GuestUiProbe.Log("FAIL "+e.Exception.GetType().Name);e.Handled=true;Exit();};}
 static void Check(bool value,string label){if(!value)throw new Exception(label);GuestUiProbe.Log("PASS "+label);}
 static void Invoke(Button button){((IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)).Invoke();}
 static async Task Wait(Task task){await task.WaitAsync(TimeSpan.FromSeconds(10));}
 async Task Scenario(bool cancel,bool baseline){
  var page=new GuestUiPage();var vm=page.ViewModel;var auth=vm._userAuthenticator;var http=new GuestUiHttp();GuestUiProbe.CurrentHttp=http;
  var core=typeof(GuestClient).GetMethod("AddButtonCore",BindingFlags.NonPublic|BindingFlags.Static);
  if(core==null)GuestClient.AddButton(page,new object[0]);else core.Invoke(null,new object[]{page,(Func<GuestProtocol>)GuestUiProbe.CreateProtocol});
  var button=(Button)page.Panel.Children[1];Check(page.Panel.Children.Count==3&&Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(button)=="PatchworkGuestButton","native WinUI guest controls and automation ID");
  GuestClient.AddButton(page,new object[0]);Check(page.Panel.Children.Count==3,"cached page does not duplicate guest controls");
  vm.IsSigningIn=true;Check(!button.IsEnabled,"normal sign-in disables guest button");vm.IsSigningIn=false;Check(button.IsEnabled,"normal sign-in completion restores guest button");
  var loaded=new TaskCompletionSource<bool>();var unloaded=new TaskCompletionSource<bool>();page.Loaded+=delegate{loaded.TrySetResult(true);};page.Unloaded+=delegate{unloaded.TrySetResult(true);};
  var loadingCancel=new Button{Content="Cancel sign-in"};loadingCancel.Click+=delegate{auth.CancelAuth();};
  auth.OnStatus=delegate(AuthenticationStatus status){if(status==AuthenticationStatus.LoggingIn)page.DispatcherQueue.TryEnqueue(delegate{frame.Content=loadingCancel;});};
  frame.Content=page;await Wait(loaded.Task);Invoke(button);await Wait(unloaded.Task);
  if(baseline){var failure=await vm.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));Check(!failure.Success&&failure.Error=="Guest sign-in cancelled."&&auth.Cancellations>0,"r6 reproduces self-cancellation after navigation unload");return;}
  Check(!auth._cts.IsCancellationRequested&&!vm.Completed.Task.IsCompleted&&vm.IsSigningIn,"navigation unload preserves active guest login");
  Check(vm._eventMessageSender.Starts==1,"native Authenticating message sent once");
  Check(!button.IsEnabled,"guest button cannot start another attempt while loading");
  if(cancel){Invoke(loadingCancel);}else{http.Release.TrySetResult(true);}
  var result=await vm.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));await Task.Delay(30);
  Check(result.Success==!cancel&&(cancel?result.Error=="Guest sign-in cancelled.":auth.IsLoggedIn),cancel?"native loading-page Cancel cancels guest login":"guest login completes across page transition");
  Check(!vm.IsSigningIn&&button.IsEnabled,"busy state cleared after completion and cleanup");
  if(cancel){
   vm.Completed=new TaskCompletionSource<AuthResult>();var reloaded=new TaskCompletionSource<bool>();page.Loaded+=delegate{reloaded.TrySetResult(true);};frame.Content=page;await Wait(reloaded.Task);Invoke(button);
   await Task.Delay(30);http.Release.TrySetResult(true);var retry=await vm.Completed.Task.WaitAsync(TimeSpan.FromSeconds(10));Check(retry.Success&&auth.IsLoggedIn,"cancelled attempt can retry on cached page");
  }
  Check(http.Creates==(cancel?2:1),"one bootstrap per intentional attempt");
 }
 protected override async void OnLaunched(LaunchActivatedEventArgs args){try{
  frame=new Frame();window=new Window{Content=frame};window.Activate();
  await Scenario(false,GuestUiProbe.Baseline);if(!GuestUiProbe.Baseline)await Scenario(true,false);
 }catch(Exception e){GuestUiProbe.Failure=e;GuestUiProbe.Log("FAIL "+e);}finally{if(window!=null)window.Close();Exit();}}
}
class GuestUiProbe {
 public static Exception Failure; public static bool Baseline; internal static GuestUiHttp CurrentHttp;
 internal static GuestProtocol CreateProtocol(){return new GuestProtocol(new HttpClient(CurrentHttp,false));}
 public static void Log(string value){System.IO.File.AppendAllText("GuestUiProbe.result.txt",value+Environment.NewLine);Console.WriteLine(value);}
 [DllImport("Microsoft.WindowsAppRuntime.dll")]static extern int WindowsAppRuntime_EnsureIsLoaded();
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]static int Run(){WinRT.ComWrappersSupport.InitializeComWrappers();Application.Start(delegate{SynchronizationContext.SetSynchronizationContext(new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()));Log("Application callback");new GuestUiApp();});return Failure==null?0:1;}
 [STAThread]static int Main(string[] args){try{Baseline=args.Length>0&&args[0]=="--expect-r6-failure";System.IO.File.WriteAllText("GuestUiProbe.result.txt","START\n");Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",AppContext.BaseDirectory);int result=WindowsAppRuntime_EnsureIsLoaded();if(result<0)Marshal.ThrowExceptionForHR(result);Log("Runtime loaded");return Run();}catch(Exception e){Log("FAIL "+e);return 1;}}
}
