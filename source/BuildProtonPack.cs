using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Patchwork;

// Compile separately, referencing Patchwork.exe and Mono.Cecil.dll. Reads original files only.
class BuildProtonPack
{
    static string root;
    static readonly Dictionary<string, ModuleDefinition> modules = new Dictionary<string, ModuleDefinition>();
    static readonly List<object> patches = new List<object>();
    static Dictionary<string, object> Op(string file, string type, string method, string kind)
    {
        if (!modules.ContainsKey(file)) modules[file] = ModuleDefinition.ReadModule(Path.Combine(root, file));
        var target = ManagedPatches.Types(modules[file].Types).Single(x => x.FullName == type).Methods.Single(x => x.Name == method);
        return new Dictionary<string, object> { { "kind", kind }, { "file", file }, { "sha256", PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, file))) }, { "method", target.FullName } };
    }
    static object Return(string file, string type, string method, string returnType, object value)
    {
        var op = Op(file, type, method, "managedReturn"); op["returnType"] = returnType;
        if (value != null) op["value"] = value; return op;
    }
    static object Call(string file, string type, string method, string calledName, bool suppress = false)
    {
        var op = Op(file, type, method, suppress ? "managedSuppressCall" : "managedBooleanCall");
        var target = ManagedPatches.Types(modules[file].Types).SelectMany(x => x.Methods).Single(x => x.FullName == (string)op["method"]);
        var calls = target.Body.Instructions.Where(x => (x.OpCode == OpCodes.Call || x.OpCode == OpCodes.Callvirt) && x.Operand is MethodReference && ((MethodReference)x.Operand).Name == calledName).ToList();
        var fullNames = calls.Select(x => ((MethodReference)x.Operand).FullName).Distinct().ToList();
        if (fullNames.Count != 1) throw new Exception("Call did not match: " + type + "." + method + " -> " + calledName);
        op["calledMethod"] = fullNames[0]; op["count"] = calls.Count; if (!suppress) op["value"] = true; return op;
    }
    static object Override(string type, bool argument)
    {
        string parent = argument ? "ProtonVPN.Client.Models.Connections.ConnectionItemBase" : "ProtonVPN.Client.UI.Main.Features.Bases.FeatureWidgetViewModelBase";
        var op = Op("ProtonVPN.Client.dll", parent, argument ? "InvalidateIsRestricted" : "get_IsRestricted", argument ? "managedOverrideBooleanArgument" : "managedOverrideBoolean");
        op["type"] = type; op["value"] = argument; return op;
    }
    static void Add(string id, string name, string description, string category, params object[] operations)
    {
        patches.Add(new Dictionary<string, object> { { "id", id }, { "name", name }, { "description", description }, { "category", category }, { "status", operations.Length == 0 ? "planned" : "ready" }, { "operations", operations } });
    }
    static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        string version = System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(root, "ProtonVPN.Client.exe")).FileVersion;
        if (version != "5.1.8.0") throw new Exception("Pack generator supports the original Windows 5.1.8.0 release only.");
        const string client = "ProtonVPN.Client.dll", settings = "ProtonVPN.Client.Settings.dll", statistical = "ProtonVPN.StatisticalEvents.dll", logic = "ProtonVPN.Client.Logic.Connection.dll";
        const string advanced = "ProtonVPN.Client.UI.Main.Settings.Pages.AdvancedSettingsPageViewModel";
        const string connSettings = "ProtonVPN.Client.UI.Main.Settings.Pages.Connection.ConnectionSettingsViewModel";
        const string userSettings = "ProtonVPN.Client.Settings.UserSettings";
        const string corrector = "ProtonVPN.Client.Settings.SettingsCorrector";
        Add("disable-telemetry", "Disable usage telemetry", "Stops both authenticated and pre-login statistical event senders. Diagnostic reports submitted by the user are separate.", "Privacy",
            Return(statistical, "ProtonVPN.StatisticalEvents.Events.Senders.AuthenticatedStatisticalEventSender", "get_CanSendTelemetryEvents", "boolean", false),
            Return(statistical, "ProtonVPN.StatisticalEvents.Events.Senders.AuthenticatedStatisticalEventSender", "get_IsShareStatisticsEnabled", "boolean", false),
            Return(statistical, "ProtonVPN.StatisticalEvents.Events.Senders.UnauthenticatedStatisticalEventSender", "get_CanSendTelemetryEvents", "boolean", false),
            Return(statistical, "ProtonVPN.StatisticalEvents.Events.Senders.UnauthenticatedStatisticalEventSender", "get_IsShareStatisticsEnabled", "boolean", false));
        Add("hide-promotions", "Hide upgrade promotions", "Hides the connection-card, sidebar, profile-page and tray upsell banners. Service and information notices remain available.", "Interface",
            Return(client, "ProtonVPN.Client.UI.Main.Components.Banners.BannerComponent", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Bases.ViewModels.CountriesComponentViewModelBase", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Profiles.ProfilesPageViewModel", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Dialogs.Tray.Pages.TrayMainPageViewModel", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Home.Upsell.ConnectionCardUpsellBannerViewModel", "get_IsBannerVisible", "boolean", false));
        Add("server-delay", "Remove local server change delay", "Makes the desktop cooldown check accept a server change and report zero remaining time. Server limits can still apply.", "Connections",
            Return(logic, "ProtonVPN.Client.Logic.Connection.ChangeServerModerator", "CanChangeServer", "boolean", true),
            Return(logic, "ProtonVPN.Client.Logic.Connection.ChangeServerModerator", "GetRemainingDelayUntilNextAttempt", "timeSpanZero", null));
        Add("lan-connections", "LAN connections controls", "Exposes the shared advanced-settings screen and uses the saved LAN access toggle. The same screen also displays custom DNS and NAT controls; their effective settings have separate checks.", "Networking",
            Return(client, advanced, "get_IsPaidUser", "boolean", true),
            Call(settings, userSettings, "get_IsLocalAreaNetworkAccessEnabled", "get_IsPaid"),
            Call(settings, corrector, "Correct", "set_IsLocalAreaNetworkAccessEnabled", true));
        Add("custom-dns", "Custom DNS controls", "Exposes the shared advanced-settings screen and uses the saved custom DNS toggle and resolver list. Test DNS behavior on your network before relying on it.", "Networking",
            Return(client, advanced, "get_IsPaidUser", "boolean", true),
            Call(settings, userSettings, "get_IsCustomDnsServersEnabled", "get_IsPaid"));
        Add("netshield", "NetShield client controls", "Opens the NetShield widget and settings and uses the saved toggle. This changes client checks; Proton's servers still decide whether DNS filtering is available for the account.", "Networking",
            Override("ProtonVPN.Client.UI.Main.Features.NetShield.NetShieldWidgetViewModel", false),
            Call(client, connSettings, "NavigateToNetShieldPageAsync", "get_IsPaidUser"),
            Call(settings, userSettings, "get_IsNetShieldEnabled", "get_IsPaid"),
            Call(settings, corrector, "Correct", "set_IsNetShieldEnabled", true));
        Add("split-tunneling", "Split tunneling client controls", "Opens the split-tunneling widget and settings and uses the saved routing toggle. VPN routing and kill-switch interaction still need a live network test.", "Networking",
            Override("ProtonVPN.Client.UI.Main.Features.SplitTunneling.SplitTunnelingWidgetViewModel", false),
            Call(client, connSettings, "NavigateToSplitTunnelingPageAsync", "get_IsPaidUser"),
            Call(settings, userSettings, "get_IsSplitTunnelingEnabled", "get_IsPaid"));
        Add("connection-preferences", "Connection preference controls", "Shows the default-connection and excluded-location controls and reads the saved default connection. Account-eligible servers and normal connection validation still apply.", "Connections",
            Return(client, "ProtonVPN.Client.UI.Main.Settings.Pages.ConnectionPreferences.ConnectionPreferencesSettingsPageViewModel", "get_IsPaidUser", "boolean", true),
            Call(settings, userSettings, "get_DefaultConnection", "get_IsPaid"));
        Add("profiles", "Connection profile controls", "Shows profile creation/editing and clears the profile row's local restriction. Connections still pass through Proton's account and location validation.", "Connections",
            Return(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Profiles.ProfilesPageViewModel", "get_IsUpsellBannerVisible", "boolean", false),
            Override("ProtonVPN.Client.Models.Connections.Profiles.ProfileConnectionItem", true));
        Add("free-locations", "Free server location selection", "Not implemented: Windows needs a server-aware selector for eligible free locations. This pack does not unlock paid servers.", "Connections");
        Add("amoled-theme", "AMOLED dark theme", "Not implemented: the Windows client uses compiled WinUI resources rather than Android theme resources.", "Appearance");
        Add("accent-color", "Custom accent color", "Not implemented for Windows compiled WinUI resources.", "Appearance");
        Add("switch-style", "Styled switches", "Not implemented for native Windows WinUI switches.", "Appearance");
        var pack = new Dictionary<string, object> {
            { "schemaVersion", 1 }, { "id", "proton-vpn-win-5-1-8-r1" }, { "appId", "proton-vpn" }, { "appName", "Proton VPN" }, { "appVersion", "5.1.8" },
            { "author", "Patchwork" }, { "source", "Windows source v5.1.8, d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9; experimental local client modifications" },
            { "versionFile", "ProtonVPN.Client.exe" }, { "versionSha256", PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, "ProtonVPN.Client.exe"))) }, { "patches", patches }
        };
        string content = Json.Pretty(pack); PatchBundle.Parse(content);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))); File.WriteAllText(args[1], content, new UTF8Encoding(false));
        foreach (var module in modules.Values) module.Dispose();
        Console.WriteLine("Built standalone pack: 9 available, 4 planned."); return 0;
    }
}
