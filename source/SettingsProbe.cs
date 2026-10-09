using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

// Uses copied assemblies and fake settings/navigation. No account, network or service calls.
public class SettingsProbeProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler;
    protected override object Invoke(MethodInfo method, object[] args) { return Handler(method, args); }
    public static object Make(Type type, Func<MethodInfo, object[], object> handler)
    {
        var proxy = DispatchProxy.Create(type, typeof(SettingsProbeProxy)); ((SettingsProbeProxy)proxy).Handler = handler; return proxy;
    }
}
class SettingsProbe
{
    static string root; static int checks;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T(string file, string name) { return Assembly.LoadFrom(Path.Combine(root, file)).GetType(name, true); }
    static object Blank(Type type) { return RuntimeHelpers.GetUninitializedObject(type); }
    static object Get(object value, string name) { return value.GetType().GetProperty(name, Flags).GetValue(value); }
    static void Set(object value, string name, object field)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var found = type.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (found != null) { found.SetValue(value, field); return; }
        }
        throw new Exception("Missing fixture field " + name);
    }
    static object Default(Type type) { return type.IsValueType ? Activator.CreateInstance(type) : null; }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        var userType = T("ProtonVPN.Client.Settings.dll", "ProtonVPN.Client.Settings.UserSettings"); var user = Blank(userType);
        bool saved = false;
        var cacheField = userType.GetField("_userCache", Flags);
        cacheField.SetValue(user, SettingsProbeProxy.Make(cacheField.FieldType, (method, data) => method.IsGenericMethod && method.GetGenericArguments()[0] == typeof(bool) ? (object)saved : Default(method.ReturnType)));
        foreach (bool value in new[] { false, true }) { saved = value; Check((bool)Get(user, "IsVpnAcceleratorEnabled"), "Accelerator effective setting stays on with saved toggle " + value); }
        Check(!(bool)Get(Get(user, "VpnPlan"), "IsPaid"), "Settings patch leaves the real account entitlement free");
        var pageType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.UI.Main.Settings.Pages.Connection.VpnAcceleratorSettingsPageViewModel");
        Check((bool)Get(Blank(pageType), "IsVpnAcceleratorEnabled"), "Accelerator settings page reports enabled");
        foreach (string name in new[] { "AdvancedSettingsPageViewModel", "Connection.ConnectionSettingsViewModel", "ConnectionPreferences.ConnectionPreferencesSettingsPageViewModel" })
        {
            var view = Blank(T("ProtonVPN.Client.dll", "ProtonVPN.Client.UI.Main.Settings.Pages." + name));
            Check((bool)Get(view, "IsPaidUser"), "Settings promotion branch hidden on " + name);
        }
        var settingsType = T("ProtonVPN.Client.Settings.Contracts.dll", "ProtonVPN.Client.Settings.Contracts.ISettings");
        object settings = SettingsProbeProxy.Make(settingsType, (method, data) => method.Name == "get_VpnPlan" ? Get(user, "VpnPlan") : method.Name == "get_IsVpnAcceleratorEnabled" ? Get(user, "IsVpnAcceleratorEnabled") : Default(method.ReturnType));
        var connectionType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.UI.Main.Settings.Pages.Connection.ConnectionSettingsViewModel"); var connection = Blank(connectionType); Set(connection, "_settings", settings);
        string navigated = null;
        var navigatorField = connectionType.GetField("_settingsViewNavigator", Flags);
        navigatorField.SetValue(connection, SettingsProbeProxy.Make(navigatorField.FieldType, (method, data) => { navigated = method.Name; return method.ReturnType == typeof(Task<bool>) ? (object)Task.FromResult(true) : Task.CompletedTask; }));
        var connectionManagerField = connectionType.GetField("_connectionManager", Flags);
        connectionManagerField.SetValue(connection, SettingsProbeProxy.Make(connectionManagerField.FieldType, (method, data) => Default(method.ReturnType)));
        var upsellField = connectionType.GetField("_upsellCarouselWindowActivator", Flags);
        upsellField.SetValue(connection, SettingsProbeProxy.Make(upsellField.FieldType, (method, data) => { throw new Exception("Settings opened an upgrade promotion."); }));
        foreach (string method in new[] { "NavigateToVpnAcceleratorPageAsync", "NavigateToNetShieldPageAsync", "NavigateToSplitTunnelingPageAsync", "NavigateToPortForwardingPageAsync" })
        {
            ((Task)connectionType.GetMethod(method, Flags).Invoke(connection, null)).GetAwaiter().GetResult();
            Check(navigated != null && navigated.Contains("SettingsView"), "Connection settings navigates without promotion: " + method); navigated = null;
        }
        var localizerField = connectionType.BaseType.BaseType.GetField("<Localizer>k__BackingField", Flags);
        if (localizerField == null)
        {
            for (var type = connectionType.BaseType; type != null && localizerField == null; type = type.BaseType) localizerField = type.GetField("<Localizer>k__BackingField", Flags | BindingFlags.DeclaredOnly);
        }
        Set(connection, "<Localizer>k__BackingField", SettingsProbeProxy.Make(localizerField.FieldType, (method, data) => method.ReturnType == typeof(string) ? (object)(string)data[0] : Default(method.ReturnType)));
        Check(((string)Get(connection, "VpnAcceleratorSettingsState")).Contains("On"), "Main connection settings labels accelerator On");
        var mainType = T("ProtonVPN.Client.Logic.Connection.dll", "ProtonVPN.Client.Logic.Connection.RequestCreators.MainSettingsRequestCreator"); var main = Blank(mainType); Set(main, "_settings", settings);
        var mapperField = mainType.GetField("_entityMapper", Flags); mapperField.SetValue(main, SettingsProbeProxy.Make(mapperField.FieldType, (method, data) => Default(method.ReturnType)));
        var request = mainType.GetMethod("Create", Flags, null, new Type[0], null).Invoke(main, null);
        Check((bool)Get(request, "SplitTcp"), "Actual service settings request receives accelerator SplitTcp enabled");
        Console.WriteLine(checks + " settings checks passed. No live settings, account, VPN service or network was changed."); return 0;
    }
}
