using System;
using System.IO;
using System.Runtime.InteropServices;
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
                var black = (Microsoft.UI.Xaml.Controls.Grid)XamlReader.Load("<Grid xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" RequestedTheme=\"Dark\"><Rectangle Fill=\"{ThemeResource BackgroundNormColorBrush}\"/></Grid>");
                var background = ((Microsoft.UI.Xaml.Shapes.Rectangle)black.Children[0]).Fill as SolidColorBrush;
                if (background == null || background.Color.R != 0 || background.Color.G != 0 || background.Color.B != 0) throw new Exception("AMOLED background lookup failed.");
                Console.WriteLine("PASS WinUI Dark theme resolves the black AMOLED surface.");
            } catch (Exception error) { Failure = error; Console.Error.WriteLine(error); }
            finally { Exit(); }
    }
}
