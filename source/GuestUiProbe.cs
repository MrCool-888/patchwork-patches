using System; using System.ComponentModel; using System.Reflection; using System.Runtime.InteropServices;
using Microsoft.UI.Xaml; using Microsoft.UI.Xaml.Controls;
class GuestUiVm : INotifyPropertyChanged { public object _userAuthenticator=new object(); bool signing; public bool IsSigningIn { get{return signing;} set{signing=value;if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("IsSigningIn"));} } public event PropertyChangedEventHandler PropertyChanged; }
class GuestUiPage : Grid { public Button SignInButton=new Button{Content="Sign in"}; public GuestUiVm ViewModel{get;private set;} public StackPanel Panel=new StackPanel{Spacing=8}; public GuestUiPage(){ViewModel=new GuestUiVm();Panel.Children.Add(SignInButton);Children.Add(Panel);} }
class GuestUiApp : Application {
 public GuestUiApp(){UnhandledException+=delegate(object sender,Microsoft.UI.Xaml.UnhandledExceptionEventArgs e){GuestUiProbe.Log("FAIL "+e.Exception.GetType().Name);GuestUiProbe.Failure=e.Exception;};}
 protected override void OnLaunched(LaunchActivatedEventArgs args){try{
  var page=new GuestUiPage();var method=Assembly.LoadFrom("Patchwork.ProtonGuest.dll").GetType("Patchwork.ProtonGuest.GuestClient").GetMethod("AddButton");method.Invoke(null,new object[]{page,new object[0]});
  if(page.Panel.Children.Count!=3)throw new Exception("Guest controls missing");var button=(Button)page.Panel.Children[1];if((string)button.Content!="Continue as guest"||!button.IsEnabled)throw new Exception("Guest action unavailable");
  if(Microsoft.UI.Xaml.Automation.AutomationProperties.GetAutomationId(button)!="PatchworkGuestButton")throw new Exception("Guest automation ID missing");GuestUiProbe.Log("PASS real WinUI guest button and accessible label");
  page.ViewModel.IsSigningIn=true;if(button.IsEnabled)throw new Exception("Normal sign-in did not disable guest action");page.ViewModel.IsSigningIn=false;if(!button.IsEnabled)throw new Exception("Guest action did not recover");GuestUiProbe.Log("PASS guest button follows native sign-in busy state");
  method.Invoke(null,new object[]{page,new object[0]});if(page.Panel.Children.Count!=3)throw new Exception("Duplicate guest button");GuestUiProbe.Log("PASS native guest controls are not duplicated");
 }catch(Exception e){GuestUiProbe.Failure=e;GuestUiProbe.Log("FAIL "+e);}finally{Exit();}}
}
class GuestUiProbe {
 public static Exception Failure;
 public static void Log(string value){System.IO.File.AppendAllText("GuestUiProbe.result.txt",value+Environment.NewLine);Console.WriteLine(value);}
 [DllImport("Microsoft.WindowsAppRuntime.dll")]static extern int WindowsAppRuntime_EnsureIsLoaded();
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]static int Run(){WinRT.ComWrappersSupport.InitializeComWrappers();Application.Start(delegate{Log("Application callback");new GuestUiApp();});return Failure==null?0:1;}
 [STAThread]static int Main(){try{System.IO.File.WriteAllText("GuestUiProbe.result.txt","START\n");Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY",AppContext.BaseDirectory);int result=WindowsAppRuntime_EnsureIsLoaded();if(result<0)Marshal.ThrowExceptionForHR(result);Log("Runtime loaded");return Run();}catch(Exception e){Log("FAIL "+e);return 1;}}
}
