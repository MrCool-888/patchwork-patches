using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
class ThemeLoadProbe
{
    [DllImport("Microsoft.WindowsAppRuntime.dll")] static extern int WindowsAppRuntime_EnsureIsLoaded();
    [STAThread] static int Main(string[] args)
    {
        Console.WriteLine("Starting isolated WinUI theme probe.");
        Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
        int result = WindowsAppRuntime_EnsureIsLoaded(); if (result < 0) Marshal.ThrowExceptionForHR(result);
        Console.WriteLine("Native Windows App Runtime loaded: " + result);
        try { return Run(args[0]); } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    static int Run(string path)
    {
        ProbeApp.Path = path;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(delegate { new ProbeApp(); });
        return ProbeApp.Failure == null ? 0 : 1;
    }
}
class ProbeApp : Application
{
    public static string Path;
    public static Exception Failure;
    public ProbeApp() { RequestedTheme = ApplicationTheme.Dark; }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        int checks = 0;
            try {
                Resources = new ResourceDictionary();
                Resources.MergedDictionaries.Add((ResourceDictionary)XamlReader.Load("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><ResourceDictionary.ThemeDictionaries><ResourceDictionary x:Key=\"Light\"><SolidColorBrush x:Key=\"PrimaryColorBrush\" Color=\"#6D4AFF\"/></ResourceDictionary><ResourceDictionary x:Key=\"Dark\"><SolidColorBrush x:Key=\"PrimaryColorBrush\" Color=\"#6D4AFF\"/></ResourceDictionary></ResourceDictionary.ThemeDictionaries></ResourceDictionary>"));
                foreach (string file in Directory.GetFiles(Path, "*.xaml")) {
                    var dictionary = (ResourceDictionary)XamlReader.Load(File.ReadAllText(file));
                    Resources.MergedDictionaries.Add(dictionary);
                    foreach (var theme in dictionary.ThemeDictionaries) {
                        var entries = (ResourceDictionary)theme.Value;
                        foreach (var pair in entries) { if (pair.Value == null) throw new Exception("Empty resource: " + pair.Key); checks++; }
                    }
                }
                Console.WriteLine("PASS Real WinUI XamlReader loaded " + checks + " generated color and brush resources.");
                foreach (string theme in new[] { "Light", "Dark" }) {
                    var grid = (Microsoft.UI.Xaml.Controls.Grid)XamlReader.Load("<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" RequestedTheme=\"" + theme + "\"><Rectangle Fill=\"{ThemeResource PrimaryColorBrush}\"/></Grid>");
                    var brush = ((Microsoft.UI.Xaml.Shapes.Rectangle)grid.Children[0]).Fill as SolidColorBrush;
                    if (brush == null || brush.Color.R != 0x8A || brush.Color.G != 0x66 || brush.Color.B != 0xFF) throw new Exception("Accent resource lookup failed for " + theme);
                    Console.WriteLine("PASS WinUI " + theme + " theme resolves the custom accent brush.");
                    grid = (Microsoft.UI.Xaml.Controls.Grid)XamlReader.Load("<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" RequestedTheme=\"" + theme + "\"><Rectangle Fill=\"{ThemeResource ToggleSwitchFillOn}\"/></Grid>");
                    brush = ((Microsoft.UI.Xaml.Shapes.Rectangle)grid.Children[0]).Fill as SolidColorBrush;
                    if (brush == null || brush.Color.R != 0x36 || brush.Color.G != 0xC9 || brush.Color.B != 0xA0) throw new Exception("Switch resource lookup failed for " + theme);
                    Console.WriteLine("PASS WinUI " + theme + " theme resolves the custom switch track.");
                }
                VerifyPromotions();
            } catch (Exception error) { Failure = error; Console.Error.WriteLine(error); }
            finally { Exit(); }
    }
    static void Set(object value, string name, object field)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        { var member = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly); if (member != null) { member.SetValue(value, field); return; } }
        throw new Exception("Missing UI fixture field: " + name);
    }
    static void VerifyPromotions()
    {
        var common = Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "ProtonVPN.Client.Common.UI.dll"));
        foreach (string name in new[] { "UpsellBanner", "UpsellFeatureContentControl" })
        {
            var control = (FrameworkElement)Activator.CreateInstance(common.GetType("ProtonVPN.Client.Common.UI.Controls.Custom." + name, true));
            if (control.Visibility != Visibility.Collapsed || control.Width != 0 || control.Height != 0 || control.IsHitTestVisible) throw new Exception("Shared promotional control remains visible.");
            Console.WriteLine("PASS Real WinUI hides shared " + name + " with no hit target or layout size.");
        }
        var rowType = common.GetType("ProtonVPN.Client.Common.UI.Controls.Custom.ServerConnectionRowButton", true);
        var row = (FrameworkElement)Activator.CreateInstance(rowType); rowType.GetProperty("IsRestricted").SetValue(row, true);
        if (row.Visibility != Visibility.Collapsed || row.IsHitTestVisible) throw new Exception("Restricted row remains visible.");
        rowType.GetProperty("IsRestricted").SetValue(row, false);
        if (row.Visibility != Visibility.Visible || !row.IsHitTestVisible) throw new Exception("Available row was not restored.");
        Console.WriteLine("PASS Real WinUI hides restricted row actions and restores available rows.");
        var client = Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "ProtonVPN.Client.dll"));
        var localizationType = Assembly.LoadFrom(System.IO.Path.Combine(AppContext.BaseDirectory, "ProtonVPN.Client.Localization.Contracts.dll")).GetType("ProtonVPN.Client.Localization.Contracts.ILocalizationProvider", true);
        var localizer = DispatchProxy.Create(localizationType, typeof(UiProbeLocalizer));
        foreach (string viewName in new[] { "ProtonVPN.Client.UI.Main.Home.Upsell.ChangeServerComponentView", "ProtonVPN.Client.UI.Dialogs.Upsell.UpsellCarouselShellView", "ProtonVPN.Client.UI.Dialogs.Upsell.P2PDetectionShellView", "ProtonVPN.Client.UI.Dialogs.Upsell.StreamingDetectionShellView" })
        {
            var viewType = client.GetType(viewName, true); var view = RuntimeHelpers.GetUninitializedObject(viewType);
            var vm = RuntimeHelpers.GetUninitializedObject(viewType.GetProperty("ViewModel").PropertyType); Set(vm, "<Localizer>k__BackingField", localizer); Set(view, "<ViewModel>k__BackingField", vm);
            var bindingType = viewType.GetNestedType(viewType.Name + "_obj1_Bindings", BindingFlags.Public | BindingFlags.NonPublic); var binding = RuntimeHelpers.GetUninitializedObject(bindingType); Set(binding, "dataRoot", view);
            var buttons = bindingType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Where(x => x.FieldType == typeof(Microsoft.UI.Xaml.Controls.Button)).ToList();
            foreach (var field in buttons) field.SetValue(binding, new Microsoft.UI.Xaml.Controls.Button());
            var hook = bindingType.GetMethod("Invoke_ViewModel_Localizer_M_Get_1992939572", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); hook.Invoke(binding, new object[] { Int32.MaxValue });
            if (!buttons.Any(x => { var button = (FrameworkElement)x.GetValue(binding); return button.Visibility == Visibility.Collapsed && button.Width == 0 && button.Height == 0 && !button.IsHitTestVisible; })) throw new Exception("Explicit Upgrade button remains visible: " + viewName);
            Console.WriteLine("PASS Real WinUI hides explicit Upgrade button in " + viewType.Name + ".");
        }
    }
}
public class UiProbeLocalizer : DispatchProxy
{
    protected override object Invoke(MethodInfo method, object[] args) { return method.ReturnType == typeof(string) ? "Upgrade" : method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null; }
}
