using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

// Runs copied, patched Proton assemblies under .NET 8. Fake caches/settings, no API or VPN calls.
public class SelectorProxy : DispatchProxy
{
    public Func<MethodInfo, object[], object> Handler;
    protected override object Invoke(MethodInfo method, object[] args) { return Handler(method, args); }
    public static object Make(Type type, Func<MethodInfo, object[], object> handler)
    {
        var value = DispatchProxy.Create(type, typeof(SelectorProxy)); ((SelectorProxy)value).Handler = handler; return value;
    }
}
class SelectorProbe
{
    static string root;
    static int checks;
    static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static Type T(string file, string name) { return Assembly.LoadFrom(Path.Combine(root, file)).GetType(name, true); }
    static object Blank(Type type) { return RuntimeHelpers.GetUninitializedObject(type); }
    static void Set(object value, string name, object field)
    {
        for (var type = value.GetType(); type != null; type = type.BaseType)
        {
            var match = type.GetField(name, Instance | BindingFlags.DeclaredOnly);
            if (match != null) { match.SetValue(value, field); return; }
        }
        throw new Exception("Missing test field: " + name);
    }
    static object Get(object value, string name) { return value.GetType().GetProperty(name, Instance).GetValue(value); }
    static void Property(object value, string name, object data) { value.GetType().GetProperty(name, Instance).SetValue(value, data); }
    static object Call(object value, string name, params object[] args) { return value.GetType().GetMethod(name, Instance).Invoke(value, args); }
    static Array Items(Type type, params object[] items) { var array = Array.CreateInstance(type, items.Length); for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i); return array; }
    static List<object> List(object values) { return ((IEnumerable)values).Cast<object>().ToList(); }
    static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static object Default(Type type) { return type.IsValueType ? Activator.CreateInstance(type) : null; }
    static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        const string serversFile = "ProtonVPN.Client.Logic.Servers.Contracts.dll";
        var planType = T("ProtonVPN.Client.Logic.Users.Contracts.dll", "ProtonVPN.Client.Logic.Users.Contracts.Messages.VpnPlan");
        bool paid = false, smart = true;
        var settingsType = T("ProtonVPN.Client.Settings.Contracts.dll", "ProtonVPN.Client.Settings.Contracts.ISettings");
        object settings = SelectorProxy.Make(settingsType, (method, data) => {
            if (method.Name == "get_VpnPlan") return Activator.CreateInstance(planType, new object[] { paid ? "Plus" : "Free", paid ? "vpnplus" : "free", (sbyte)(paid ? 2 : 0), false });
            if (method.Name == "get_IsSmartReconnectEnabled") return smart;
            return Default(method.ReturnType);
        });
        var countryType = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.Country");
        var freeType = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.FreeCountry");
        var free = Activator.CreateInstance(freeType); Property(free, "Code", "NL"); Property(free, "IsLocationUnderMaintenance", false);
        var offline = Activator.CreateInstance(freeType); Property(offline, "Code", "JP"); Property(offline, "IsLocationUnderMaintenance", true);
        var plusCountry = Activator.CreateInstance(countryType); Property(plusCountry, "Code", "DE");
        var cacheType = T("ProtonVPN.Client.Logic.Servers.dll", "ProtonVPN.Client.Logic.Servers.Cache.ServersCache");
        var cache = Blank(cacheType); Set(cache, "_settings", settings); Set(cache, "_lock", new ReaderWriterLockSlim());
        Set(cache, "_freeCountries", Items(freeType, free, offline)); Set(cache, "_countries", Items(countryType, plusCountry));
        var countryList = List(Get(cache, "Countries"));
        Check(countryList.Select(x => (string)Get(x, "Code")).SequenceEqual(new[] { "NL", "JP" }), "Free country list comes from free cache, including free-only locations");
        Check(!(bool)Get(countryList[0], "IsStandardUnderMaintenance") && (bool)Get(countryList[1], "IsStandardUnderMaintenance"), "Country maintenance flags are preserved");
        paid = true; Check(Object.ReferenceEquals(Get(cache, "Countries"), GetRawCountries(cache)), "Paid country list retains original objects");
        paid = false; Set(cache, "_freeCountries", Items(freeType)); Check(List(Get(cache, "Countries")).Count == 0, "Empty free cache yields no selectable countries");
        Set(cache, "_freeCountries", Items(freeType, free, offline));

        var serverType = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.Server");
        var physicalType = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.PhysicalServer");
        Func<string, string, int, long, bool, object> server = (id, country, tier, features, online) => {
            var value = Activator.CreateInstance(serverType); Property(value, "Id", id); Property(value, "Name", id); Property(value, "ExitCountry", country);
            Property(value, "Tier", Enum.ToObject(serverType.GetProperty("Tier").PropertyType, tier)); Property(value, "Features", Enum.ToObject(serverType.GetProperty("Features").PropertyType, features)); Property(value, "Status", (sbyte)(online ? 1 : 0));
            var physical = Activator.CreateInstance(physicalType); Property(physical, "Id", id); Property(physical, "EntryIp", "192.0.2.1"); Property(physical, "Status", (sbyte)1);
            Property(value, "Servers", Items(physicalType, physical)); return value;
        };
        long businessFeature = Convert.ToInt64(Enum.Parse(serverType.GetProperty("Features").PropertyType, "B2B"));
        object nl = server("nl-free", "NL", 0, 0, true), jp = server("jp-free", "JP", 0, 0, true), down = server("nl-offline", "NL", 0, 0, false), plus = server("nl-plus", "NL", 2, 0, true), business = server("nl-business", "NL", 0, businessFeature, true);
        var allServers = Items(serverType, nl, jp, down, plus, business);
        var cacheInterface = T("ProtonVPN.Client.Logic.Servers.dll", "ProtonVPN.Client.Logic.Servers.Cache.IServersCache");
        var fakeCache = SelectorProxy.Make(cacheInterface, (method, data) => method.Name == "get_Servers" ? allServers : method.Name == "get_Countries" ? Get(cache, "Countries") : Default(method.ReturnType));
        var loaderType = T("ProtonVPN.Client.Logic.Servers.dll", "ProtonVPN.Client.Logic.Servers.ServersLoader");
        var loader = Blank(loaderType); Set(loader, "_serversCache", fakeCache);
        Check(List(Call(loader, "GetFreeServers")).Select(x => (string)Get(x, "Id")).SequenceEqual(new[] { "nl-free", "jp-free", "nl-offline" }), "Real free-server loader excludes paid and business servers");
        var locationType = T("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Countries.SingleCountryLocationIntent");
        var location = Activator.CreateInstance(locationType, new object[] { "NL" });
        Check(!(bool)Get(location, "IsForPaidUsersOnly"), "Single-country intent is preserved for free accounts");
        var intentType = T("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.ConnectionIntent");
        var intent = Activator.CreateInstance(intentType, new object[] { location, null });
        var managerType = T("ProtonVPN.Client.Logic.Connection.dll", "ProtonVPN.Client.Logic.Connection.ConnectionManager"); var manager = Blank(managerType); Set(manager, "_settings", settings);
        var normalized = Call(manager, "CreateNewIntentIfUserPlanIsFree", intent);
        Check(Object.ReferenceEquals(Get(normalized, "Location"), location), "Actual connection manager keeps the selected NL country");
        Check(!(bool)Get(Call(settings, "get_VpnPlan"), "IsPaid"), "Account entitlement remains free");
        var generatorType = T("ProtonVPN.Client.Logic.Connection.dll", "ProtonVPN.Client.Logic.Connection.ServerListGenerators.ServerListGenerator"); var generator = Blank(generatorType); Set(generator, "Settings", settings); Set(generator, "ServersLoader", loader);
        var candidates = Call(generator, "GetAvailableServers", intent, false);
        Check(List(candidates).Count == 3, "Actual generator restricts free-account candidates to free servers");
        var protocolType = T("ProtonVPN.Common.Core.dll", "ProtonVPN.Common.Core.Networking.VpnProtocol"); var protocols = Array.CreateInstance(protocolType, 0);
        var selected = List(Call(normalized, "FilterAndSortServers", candidates, null, protocols, false));
        Check(selected.Count == 1 && (string)Get(selected[0], "Id") == "nl-free", "Selection chooses online NL free server and excludes other countries, paid, business and offline servers");
        var unavailable = Activator.CreateInstance(locationType, new object[] { "DE" }); var unavailableIntent = Activator.CreateInstance(intentType, new object[] { unavailable, null });
        Check(List(Call(unavailableIntent, "FilterAndSortServers", candidates, null, protocols, false)).Count == 0, "A country without free servers yields no connection candidate");
        paid = true; Check(List(Call(generator, "GetAvailableServers", intent, false)).Count == 5, "Paid-account generator retains its full original candidate list");

        var creatorType = T("ProtonVPN.Client.Logic.Connection.dll", "ProtonVPN.Client.Logic.Connection.RequestCreators.ConnectionRequestCreator"); var creator = Blank(creatorType); Set(creator, "Settings", settings);
        paid = false; smart = true; Check((bool)Call(creator, "IsToBypassSmartServerListGenerator", intent), "Free selection bypasses cross-country Smart reconnect fallback");
        paid = true; Check(!(bool)Call(creator, "IsToBypassSmartServerListGenerator", intent), "Paid Smart reconnect preference is preserved");
        smart = false; Check((bool)Call(creator, "IsToBypassSmartServerListGenerator", intent), "Paid disabled Smart reconnect preference is preserved");

        var itemType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.Models.Connections.Countries.CountryLocationItem"); var item = Blank(itemType);
        var host = itemType.BaseType.BaseType; var subItemsField = host.GetField("<SubItems>k__BackingField", Instance);
        var children = Activator.CreateInstance(subItemsField.FieldType); subItemsField.SetValue(item, children);
        var childType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.Models.Connections.Countries.ServerLocationItem"); var child = Blank(childType);
        children.GetType().GetMethod("Add").Invoke(children, new object[] { child });
        Call(item, "InvalidateIsRestricted", false); Check(!(bool)Get(item, "IsRestricted"), "Free country row enables its Connect action");
        Check((bool)Get(child, "IsRestricted"), "Free-country override keeps individual server child rows restricted");
        var lastPaid = host.GetField("_lastKnownIsPaidUser", Instance); Check(!(bool)lastPaid.GetValue(item), "Country override preserves free status for restricted city/server child rows");
        Call(item, "InvalidateIsRestricted", true); Check((bool)lastPaid.GetValue(item) && !(bool)Get(item, "IsRestricted"), "Paid country row preserves inherited paid propagation");
        Check(!(bool)Get(child, "IsRestricted"), "Paid child-row access is preserved");
        object clickedIntent = null;
        var managerInterface = T("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.IConnectionManager");
        var fakeManager = SelectorProxy.Make(managerInterface, (method, data) => {
            if (method.Name == "ConnectAsync") { clickedIntent = data.Last(); return Task.CompletedTask; }
            return Default(method.ReturnType);
        });
        Set(item, "ConnectionManager", fakeManager); Set(item, "<LocationIntent>k__BackingField", location);
        Call(item, "InvalidateIsRestricted", false); ((Task)Call(item, "ToggleConnectionAsync")).GetAwaiter().GetResult();
        Check(clickedIntent != null && Object.ReferenceEquals(Get(clickedIntent, "Location"), location), "Country Connect action submits the chosen country to the connection manager");
        Console.WriteLine(checks + " selector checks passed. No account, network, service, settings file or installed app was changed."); return 0;
    }
    static object GetRawCountries(object cache) { return cache.GetType().GetField("_countries", Instance).GetValue(cache); }
}
