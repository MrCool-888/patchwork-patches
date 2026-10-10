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
    static object GuestHook(string file, string type, string method, string entry, string mode, byte[] module)
    {
        var op = Op(file, type, method, "managedEmbeddedHook"); op["moduleBase64"] = Convert.ToBase64String(module); op["moduleSha256"] = PatchEngine.Hash(module);
        op["entryType"] = "Patchwork.ProtonGuest.GuestClient"; op["entryMethod"] = entry; op["mode"] = mode; return op;
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
    static Dictionary<string, object> Ui(string file, MethodDefinition method, string field = "", bool parameter = false)
    {
        return new Dictionary<string, object> { { "kind", "managedUiVisibility" }, { "file", file }, { "sha256", PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, file))) }, { "method", method.FullName }, { "field", field }, { "fromParameter", parameter } };
    }
    static IEnumerable<object> PromotionControls()
    {
        const string common = "ProtonVPN.Client.Common.UI.dll";
        var seed = Op(common, "ProtonVPN.Client.Common.UI.Controls.Custom.UpsellBanner", ".ctor", "managedUiVisibility");
        yield return seed;
        yield return Op(common, "ProtonVPN.Client.Common.UI.Controls.UpsellBannerControl", ".ctor", "managedUiVisibility");
        yield return Op(common, "ProtonVPN.Client.Common.UI.Controls.Custom.UpsellFeatureContentControl", ".ctor", "managedUiVisibility");
        yield return Return(common, "ProtonVPN.Client.Common.UI.Controls.Custom.GridSettingsCard", "get_IsSubscriptionBadgeVisible", "boolean", false);
        yield return Return("ProtonVPN.Client.dll", "ProtonVPN.Client.UI.Overlays.WhatsNew.WhatsNewOverlayViewModel", "get_IsSubscriptionBadgeVisible", "boolean", false);
        const string announcementsFile = "ProtonVPN.Client.Logic.Announcements.dll", announcementsType = "ProtonVPN.Client.Logic.Announcements.AnnouncementsProvider";
        var offerList = Op(announcementsFile, announcementsType, "GetAllActive", "managedEnumFilter");
        offerList["elementGetter"] = "ProtonVPN.Client.Logic.Announcements.Contracts.Entities.AnnouncementType ProtonVPN.Client.Logic.Announcements.Contracts.Entities.Announcement::get_Type()"; offerList["value"] = 4; yield return offerList;
        var offerPopup = Op(announcementsFile, announcementsType, "GetActiveAndUnseenByType", "managedEnumGuardNull"); offerPopup["values"] = new[] { 0, 1, 2, 3 }; yield return offerPopup;
        var row = ManagedPatches.Types(modules[common].Types).Single(x => x.FullName == "ProtonVPN.Client.Common.UI.Controls.Bases.ConnectionRowButtonBase").Methods.Single(x => x.Name == "set_IsRestricted");
        yield return Ui(common, row, "", true);
        int buttons = 0;
        foreach (var method in ManagedPatches.Types(modules["ProtonVPN.Client.dll"].Types).SelectMany(x => x.Methods).Where(x => x.HasBody && x.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr && (string)i.Operand == "Common_Actions_Upgrade")))
        {
            // Exact compiled button field used by the localized Upgrade content binding.
            var fields = method.Body.Instructions.Where(x => x.OpCode == OpCodes.Ldfld).Select(x => (FieldReference)x.Operand).Where(x => x.DeclaringType.FullName == method.DeclaringType.FullName && x.FieldType.FullName == "Microsoft.UI.Xaml.Controls.Button").GroupBy(x => x.FullName).Select(x => x.First()).ToList();
            foreach (var field in fields) { buttons++; yield return Ui("ProtonVPN.Client.dll", method, field.FullName); }
        }
        if (buttons != 4) throw new Exception("Upgrade button inventory changed: " + buttons);
    }
    static void Add(string id, string name, string description, string category, params object[] operations)
    {
        var patch = new Dictionary<string, object> { { "id", id }, { "name", name }, { "description", description }, { "category", category }, { "status", operations.Length == 0 ? "planned" : "ready" }, { "operations", operations } };
        if (operations.Length != 0) patch["version"] = "1.0.0";
        patches.Add(patch);
    }
    static string Field(string type, string name) { return ManagedPatches.Types(modules["ProtonVPN.Client.dll"].Types).Single(x => x.FullName == type).Fields.Single(x => x.Name == name).FullName; }
    static object Resource(string theme, string key, string type, string value) { return new Dictionary<string, object> { { "theme", theme }, { "key", key }, { "type", type }, { "value", value } }; }
    static Dictionary<string, object> Theme(params object[] colors)
    {
        var op = Op("ProtonVPN.Client.dll", "ProtonVPN.Client.App", "LoadTypographyResourceDictionary", "managedThemeResources"); op["resources"] = colors; return op;
    }
    static void ColorPair(List<object> resources, string theme, string key, string value) { resources.Add(Resource(theme, key, "color", value)); resources.Add(Resource(theme, key + "Brush", "brush", value)); }
    static void Options(params string[] fields)
    {
        var options = new List<object>(); for (int i = 0; i < fields.Length; i += 3) options.Add(new Dictionary<string, object> { { "id", fields[i] }, { "label", fields[i + 1] }, { "type", "color" }, { "default", fields[i + 2] } }); ((Dictionary<string, object>)patches.Last())["options"] = options;
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
        Add("hide-promotions", "Hide upgrade promotions", "Hides the connection-card, sidebar, profile-page and tray upsell banners, plus settings upgrade cards and subscription badges. Settings links open their normal pages; effective feature/account checks remain separate. Service and information notices remain available.", "Interface",
            Return(client, "ProtonVPN.Client.UI.Main.Components.Banners.BannerComponent", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Bases.ViewModels.CountriesComponentViewModelBase", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Profiles.ProfilesPageViewModel", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Dialogs.Tray.Pages.TrayMainPageViewModel", "get_IsUpsellBannerVisible", "boolean", false),
            Return(client, "ProtonVPN.Client.UI.Main.Home.Upsell.ConnectionCardUpsellBannerViewModel", "get_IsBannerVisible", "boolean", false),
            Return(client, connSettings, "get_IsPaidUser", "boolean", true),
            Return(client, advanced, "get_IsPaidUser", "boolean", true),
            Return(client, "ProtonVPN.Client.UI.Main.Settings.Pages.ConnectionPreferences.ConnectionPreferencesSettingsPageViewModel", "get_IsPaidUser", "boolean", true));
        var promotion = (Dictionary<string, object>)patches.Last();
        promotion["operations"] = ((object[])promotion["operations"]).Concat(PromotionControls()).ToArray();
        promotion["version"] = "1.2.0";
        promotion["description"] = "Hides shared upgrade banners/feature cards, subscription badges, four explicit Upgrade buttons (including change-server and network notices), promotional announcement offers/popups, and restricted server-row upgrade actions. Available rows keep Connect and maintenance checks. NPS surveys and normal service notices remain available; actual account plan and backend eligibility are unchanged.";
        Add("vpn-accelerator", "Keep VPN Accelerator enabled", "Keeps VPN Accelerator on, opens its settings without an upgrade prompt, and shows its enabled state. This overrides the saved off toggle; restore this patch to return toggle control. Live speed improvements are not verified.", "Networking",
            Return(settings, userSettings, "get_IsVpnAcceleratorEnabled", "boolean", true),
            Return(client, "ProtonVPN.Client.UI.Main.Settings.Pages.Connection.VpnAcceleratorSettingsPageViewModel", "get_IsVpnAcceleratorEnabled", "boolean", true),
            Call(client, connSettings, "NavigateToVpnAcceleratorPageAsync", "get_IsPaidUser"),
            Call(client, connSettings, "get_VpnAcceleratorSettingsState", "get_IsPaidUser"));
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
        const string cache = "ProtonVPN.Client.Logic.Servers.Cache.ServersCache";
        const string generator = "ProtonVPN.Client.Logic.Connection.ServerListGenerators.ServerListGeneratorBase";
        const string planGetter = "ProtonVPN.Client.Logic.Users.Contracts.Messages.VpnPlan ProtonVPN.Client.Settings.Contracts.IUserSettings::get_VpnPlan()";
        const string paidGetter = "System.Boolean ProtonVPN.Client.Logic.Users.Contracts.Messages.VpnPlan::get_IsPaid()";
        var freeCountries = Op("ProtonVPN.Client.Logic.Servers.dll", cache, "get_Countries", "managedConditionalProjection");
        freeCountries["condition"] = new[] { "ProtonVPN.Client.Settings.Contracts.ISettings " + cache + "::_settings", planGetter, paidGetter };
        freeCountries["sourceMethod"] = "System.Collections.Generic.IReadOnlyList`1<ProtonVPN.Client.Logic.Servers.Contracts.Models.FreeCountry> " + cache + "::get_FreeCountries()";
        freeCountries["mappings"] = new object[] {
            new Dictionary<string, object> { { "getter", "System.String ProtonVPN.Client.Logic.Servers.Contracts.Models.FreeCountry::get_Code()" }, { "setter", "System.Void modreq(System.Runtime.CompilerServices.IsExternalInit) ProtonVPN.Client.Logic.Servers.Contracts.Models.Country::set_Code(System.String)" } },
            new Dictionary<string, object> { { "getter", "System.Boolean ProtonVPN.Client.Logic.Servers.Contracts.Models.StandardLocationBase::get_IsLocationUnderMaintenance()" }, { "setter", "System.Void modreq(System.Runtime.CompilerServices.IsExternalInit) ProtonVPN.Client.Logic.Servers.Contracts.Models.FeatureLocationBase::set_IsStandardUnderMaintenance(System.Boolean)" } }
        };
        var freeServers = Op(logic, generator, "GetAvailableServers", "managedConditionalCall");
        freeServers["condition"] = new[] { "ProtonVPN.Client.Settings.Contracts.ISettings " + generator + "::Settings", planGetter, paidGetter };
        freeServers["calledMethod"] = "System.Collections.Generic.IEnumerable`1<ProtonVPN.Client.Logic.Servers.Contracts.Models.Server> ProtonVPN.Client.Logic.Servers.Contracts.IServersLoader::GetServers()";
        freeServers["replacementMethod"] = "System.Collections.Generic.IEnumerable`1<ProtonVPN.Client.Logic.Servers.Contracts.Models.Server> ProtonVPN.Client.Logic.Servers.Contracts.IServersLoader::GetFreeServers()";
        freeServers["count"] = 1;
        var countryRow = Op(client, "ProtonVPN.Client.Models.Connections.ConnectionItemBase", "InvalidateIsRestricted", "managedOverrideBooleanSetter");
        countryRow["type"] = "ProtonVPN.Client.Models.Connections.Countries.CountryLocationItem";
        countryRow["setterMethod"] = "System.Void ProtonVPN.Client.Models.Connections.ConnectionItemBase::set_IsRestricted(System.Boolean)"; countryRow["value"] = false;
        var countryIntent = Op("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Countries.CountryLocationIntentBase", "get_IsForPaidUsersOnly", "managedOverrideBoolean");
        countryIntent["type"] = "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Countries.SingleCountryLocationIntent"; countryIntent["value"] = false;
        const string requestBase = "ProtonVPN.Client.Logic.Connection.RequestCreators.ConnectionRequestCreatorBase";
        var strictCountry = Op(logic, requestBase, "IsToBypassSmartServerListGenerator", "managedConditionalBooleanCall");
        strictCountry["condition"] = new[] { "ProtonVPN.Client.Settings.Contracts.ISettings ProtonVPN.Client.Logic.Connection.RequestCreators.RequestCreatorBase::Settings", planGetter, paidGetter };
        strictCountry["calledMethod"] = "System.Boolean ProtonVPN.Client.Settings.Contracts.IUserSettings::get_IsSmartReconnectEnabled()"; strictCountry["value"] = false; strictCountry["count"] = 1;
        var serverRow = Op(client, "ProtonVPN.Client.Models.Connections.ConnectionItemBase", "InvalidateIsRestricted", "managedOverrideConditionalBooleanSetter");
        serverRow["type"] = "ProtonVPN.Client.Models.Connections.Countries.ServerLocationItem";
        serverRow["setterMethod"] = "System.Void ProtonVPN.Client.Models.Connections.ConnectionItemBase::set_IsRestricted(System.Boolean)";
        serverRow["condition"] = new[] { "System.Boolean ProtonVPN.Client.Models.Connections.ServerLocationItemBase::get_IsFree()" }; serverRow["value"] = false;
        var serverIntent = Op("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Servers.ServerLocationIntentBase", "get_IsForPaidUsersOnly", "managedOverrideBoolean");
        serverIntent["type"] = "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Servers.SingleServerLocationIntent"; serverIntent["value"] = false;
        var countryServers = Op(client, "ProtonVPN.Client.Models.Connections.Countries.CountryLocationItem", "GetSubItems", "managedEnumerableFactory");
        countryServers["condition"] = new[] { Field("ProtonVPN.Client.Models.Connections.HostLocationItemBase`1", "_lastKnownIsPaidUser") };
        countryServers["source"] = new[] { Field("ProtonVPN.Client.Models.Connections.ConnectionItemBase", "ServersLoader"), "System.Collections.Generic.IEnumerable`1<ProtonVPN.Client.Logic.Servers.Contracts.Models.Server> ProtonVPN.Client.Logic.Servers.Contracts.IServersLoader::GetFreeServers()" };
        countryServers["filter"] = new[] { "System.String ProtonVPN.Client.Models.Connections.CountryLocationItemBase::get_ExitCountryCode()" };
        countryServers["factory"] = new[] { Field("ProtonVPN.Client.Models.Connections.HostLocationItemBase`1", "LocationItemFactory") };
        countryServers["argument"] = new[] { "System.Boolean ProtonVPN.Client.Models.Connections.ConnectionItemBase::get_IsSearchItem()" };
        countryServers["elementGetter"] = "System.String ProtonVPN.Client.Logic.Servers.Contracts.Models.Server::get_ExitCountry()";
        countryServers["factoryMethod"] = "ProtonVPN.Client.Models.Connections.Countries.ServerLocationItem ProtonVPN.Client.Factories.ILocationItemFactory::GetServer(ProtonVPN.Client.Logic.Servers.Contracts.Models.Server,System.Boolean)";
        const string recentType = "ProtonVPN.Client.Models.Connections.Recents.RecentConnectionItem";
        var recentRow = Op(client, "ProtonVPN.Client.Models.Connections.ConnectionItemBase", "InvalidateIsRestricted", "managedOverrideConditionalBooleanSetter");
        recentRow["type"] = recentType; recentRow["setterMethod"] = "System.Void ProtonVPN.Client.Models.Connections.ConnectionItemBase::set_IsRestricted(System.Boolean)"; recentRow["value"] = false; recentRow["conditionExpected"] = false;
        recentRow["condition"] = new[] { "ProtonVPN.Client.Logic.Recents.Contracts.IRecentConnection " + recentType + "::get_RecentConnection()", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.IConnectionIntent ProtonVPN.Client.Logic.Recents.Contracts.IRecentConnection::get_ConnectionIntent()", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.ILocationIntent ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.IConnectionIntent::get_Location()", "System.Boolean ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.IIntent::get_IsForPaidUsersOnly()" };
        recentRow["exclusions"] = new[] { "IsSecureCore", "IsP2P", "IsTor", "IsB2B", "IsProfileIntent" }.Select(x => new[] { "System.Boolean " + recentType + "::get_" + x + "()" }).ToArray();
        Add("free-locations", "Free country and individual server selection", "Expand a free country to see individual free servers; Search and standard Recents keep the selected server ID and show Connect. Candidate filtering remains free/non-business with no server fallback. Paid-only recent features/profiles keep their checks. Paid accounts retain city/state browsing. Maintenance remains active. Requires a signed-in or guest VPN session.", "Connections", freeCountries, freeServers, countryRow, countryIntent, strictCountry, serverRow, serverIntent, countryServers, recentRow);
        ((Dictionary<string, object>)patches.Last())["version"] = "1.3.0";
        var modes = Op(client, "ProtonVPN.Client.UI.Main.Sidebar.Connections.Countries.CountriesPageViewModel", "get_CountriesComponents", "managedEnumFilter");
        modes["elementGetter"] = "ProtonVPN.Client.Core.Enums.CountriesConnectionType ProtonVPN.Client.UI.Main.Sidebar.Connections.Bases.Contracts.ICountriesComponent::get_ConnectionType()"; modes["value"] = 0;
        modes["condition"] = new[] { "ProtonVPN.Client.Settings.Contracts.ISettings ProtonVPN.Client.UI.Main.Sidebar.Bases.ConnectionListViewModelBase`1::get_Settings()", planGetter, paidGetter };
        Add("hide-paid-modes", "Hide paid country modes", "Hides Secure Core, P2P and Tor country tabs on free accounts, keeping All countries and free server selection. Paid accounts retain their mode tabs. This changes navigation only; it does not grant access to paid servers. Restart Proton after an account-plan change.", "Interface", modes);
        byte[] guestModule = File.ReadAllBytes(args[2]);
        const string authFile = "ProtonVPN.Client.Logic.Auth.dll", authType = "ProtonVPN.Client.Logic.Auth.UserAuthenticator";
        Add("no-sign-in", "Guest VPN session", "Adds Continue as guest with cancellation, encrypted session storage and native VPN authorization, certificate renewal, refresh, restart and logout. Contains executable managed client code; source: source/GuestClient.cs. Normal sign-in remains available. Guest accounts retain backend free-server limits.", "Guest client code",
            GuestHook(client, "ProtonVPN.Client.UI.Login.Pages.SignInPageView", ".ctor", "AddButton", "after", guestModule),
            GuestHook(authFile, authType, "GetUserAsync", "GuestUser", "fallback", guestModule),
            GuestHook(authFile, authType, "AutoLoginUserAsync", "Resume", "fallback", guestModule),
            Return("ProtonVPN.Api.dll", "ProtonVPN.Api.TokenClient", "LogRefreshToken", "void", null));
        var accent = new List<object>();
        foreach (string theme in new[] { "Light", "Dark" })
        {
            foreach (string key in new[] { "PrimaryColor", "LinkNormColor", "LinkHoverColor", "LinkActiveColor", "InteractionNormColor", "InteractionNormHoverColor", "InteractionNormActiveColor", "BorderFocusColor" }) ColorPair(accent, theme, key, "$accent");
            ColorPair(accent, theme, "TextOnPrimaryColor", "$foreground");
        }
        Add("accent-color", "Custom accent color", "Choose the color used by primary buttons, links, focus borders and accent brushes, plus its foreground text color. Applies to Light and Dark themes. Choose contrasting colors for readable controls.", "Appearance", Theme(accent.ToArray()));
        Options("accent", "Accent", "#8A66FF", "foreground", "Button text", "#FFFFFF");
        var switches = new List<object>();
        foreach (string theme in new[] { "Light", "Dark" })
        {
            foreach (string key in new[] { "ToggleSwitchFillOn", "ToggleSwitchFillOnPointerOver", "ToggleSwitchFillOnPressed", "ToggleSwitchStrokeOn", "ToggleSwitchStrokeOnPointerOver", "ToggleSwitchStrokeOnPressed" }) switches.Add(Resource(theme, key, "brush", "$track"));
            foreach (string key in new[] { "ToggleSwitchKnobFillOn", "ToggleSwitchKnobFillOnPointerOver", "ToggleSwitchKnobFillOnPressed" }) switches.Add(Resource(theme, key, "brush", "$knob"));
        }
        Add("switch-style", "Custom switch colors", "Choose enabled toggle-switch track and knob colors while keeping the native switch layout, disabled states and keyboard controls. Applies to Light and Dark themes.", "Appearance", Theme(switches.ToArray()));
        Options("track", "Switch track", "#36C9A0", "knob", "Switch knob", "#FFFFFF");
        var pack = new Dictionary<string, object> {
            { "schemaVersion", 1 }, { "id", "proton-vpn-win-5-1-8-r1" }, { "appId", "proton-vpn" }, { "appName", "Proton VPN" }, { "appVersion", "5.1.8" },
            { "author", "Patchwork" }, { "source", "Windows source v5.1.8, d2a4f8bc92a0fd296943a7cdd15f4f870c8a87f9; experimental local client modifications" },
            { "packVersion", "1.5.0" }, { "minimumPatcherVersion", "0.7.0" },
            { "versionFile", "ProtonVPN.Client.exe" }, { "versionSha256", PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, "ProtonVPN.Client.exe"))) }, { "patches", patches }
        };
        string content = Json.Pretty(pack); PatchBundle.Parse(content);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[1]))); File.WriteAllText(args[1], content, new UTF8Encoding(false));
        foreach (var module in modules.Values) module.Dispose();
        Console.WriteLine("Built local candidate pack v1.5.0: 15 available; guest requires validation before publication."); return 0;
    }
}
