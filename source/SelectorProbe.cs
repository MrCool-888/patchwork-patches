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
        Property(nl, "Name", "NL-FREE#100"); Property(jp, "Name", "JP-FREE#101");
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
        Set(child, "<Server>k__BackingField", nl);
        children.GetType().GetMethod("Add").Invoke(children, new object[] { child });
        Call(item, "InvalidateIsRestricted", false); Check(!(bool)Get(item, "IsRestricted"), "Free country row enables its Connect action");
        Check(!(bool)Get(child, "IsRestricted"), "Free server child row enables its individual Connect action");
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

        var serverIntentType = T("ProtonVPN.Client.Logic.Connection.Contracts.dll", "ProtonVPN.Client.Logic.Connection.Contracts.Models.Intents.Locations.Servers.SingleServerLocationIntent");
        var infoType = serverIntentType.GetProperty("Server").PropertyType;
        var info = infoType.GetMethod("From", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { "nl-free", "NL-FREE#100" });
        var specific = Activator.CreateInstance(serverIntentType, new object[] { location, info });
        Check(!(bool)Get(specific, "IsForPaidUsersOnly"), "Single server intent survives free-account normalization");
        var specificIntent = Activator.CreateInstance(intentType, new object[] { specific, null }); paid = false;
        var normalizedServer = Call(manager, "CreateNewIntentIfUserPlanIsFree", specificIntent);
        Check(Object.ReferenceEquals(Get(normalizedServer, "Location"), specific), "Actual manager preserves the selected server object");
        candidates = Call(generator, "GetAvailableServers", specificIntent, false);
        var otherNl = server("nl-other-free", "NL", 0, 0, true);
        var sameCountryCandidates = Items(serverType, nl, otherNl, jp, down);
        selected = List(Call(normalizedServer, "FilterAndSortServers", sameCountryCandidates, null, protocols, false));
        Check(selected.Count == 1 && (string)Get(selected[0], "Id") == "nl-free", "Exact selected server ID wins over other free servers in the same country");
        Check(List(Call(normalizedServer, "FilterAndSortServers", Items(serverType, otherNl, jp, down), null, protocols, false)).Count == 0, "Missing selected server does not fall back to another server");
        Property(nl, "Status", (sbyte)0);
        Check(List(Call(normalizedServer, "FilterAndSortServers", sameCountryCandidates, null, protocols, false)).Count == 0, "Offline selected server does not silently fall back"); Property(nl, "Status", (sbyte)1);
        Check((bool)Call(creator, "IsToBypassSmartServerListGenerator", specificIntent), "Individual free server selection bypasses Smart reconnect fallback");
        Set(child, "<LocationIntent>k__BackingField", specific); Set(child, "ConnectionManager", fakeManager);
        Call(child, "InvalidateIsRestricted", false); ((Task)Call(child, "ToggleConnectionAsync")).GetAwaiter().GetResult();
        Check(Object.ReferenceEquals(Get(clickedIntent, "Location"), specific), "Individual server Connect action submits the exact selected server");
        Set(child, "<Server>k__BackingField", plus); Call(child, "InvalidateIsRestricted", false);
        Check((bool)Get(child, "IsRestricted"), "Paid server rows remain restricted for free accounts");
        Call(child, "InvalidateIsRestricted", true); Check(!(bool)Get(child, "IsRestricted"), "Paid accounts retain individual paid server access");
        Set(child, "<Server>k__BackingField", nl); Call(child, "InvalidateIsRestricted", false);
        var gatewayType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.Models.Connections.Gateways.GatewayServerLocationItem");
        var gateway = Blank(gatewayType); Call(gateway, "InvalidateIsRestricted", false); Check((bool)Get(gateway, "IsRestricted"), "Business gateway rows retain their original restrictions");
        var cityType = T("ProtonVPN.Client.dll", "ProtonVPN.Client.Models.Connections.Countries.CityLocationItem"); var city = Blank(cityType);
        var cityHost = cityType.BaseType.BaseType; cityHost.GetField("<SubItems>k__BackingField", Instance).SetValue(city, Activator.CreateInstance(subItemsField.FieldType));
        Call(city, "InvalidateIsRestricted", false); Check((bool)Get(city, "IsRestricted"), "City rows retain their original restrictions");
        var searchType = T("ProtonVPN.Client.Logic.Searches.dll", "ProtonVPN.Client.Logic.Searches.GlobalSearch"); var search = Blank(searchType); Set(search, "_serversLoader", loader);
        Check(List(Call(search, "SearchServers", "NL-FREE#100", null)).Any(x => Object.ReferenceEquals(x, nl)), "Actual server search finds the selectable exact free server name");
        Check(List(Call(search, "SearchServers", "NL-FREE", null)).Count == 1, "Searching a free-server country prefix exposes its cached rows");
        var loaderInterface = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.IServersLoader");
        var stateModel = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.State"); var cityModel = T(serversFile, "ProtonVPN.Client.Logic.Servers.Contracts.Models.City");
        var cityData = Activator.CreateInstance(cityModel); bool usedPaidList = false;
        var countryLoader = SelectorProxy.Make(loaderInterface, (method, data) => {
            if (method.Name == "GetFreeServers") return Call(loader, "GetFreeServers");
            if (method.Name == "GetStatesByCountryCode") { usedPaidList = true; return Items(stateModel); }
            if (method.Name == "GetCitiesByCountryCode") { usedPaidList = true; return Items(cityModel, cityData); }
            return Default(method.ReturnType);
        });
        var factoryInterface = T("ProtonVPN.Client.dll", "ProtonVPN.Client.Factories.ILocationItemFactory"); bool searchArgument = false;
        var countryFactory = SelectorProxy.Make(factoryInterface, (method, data) => {
            if (method.Name == "GetServer") {
                searchArgument = (bool)data[1]; var row = Blank(childType); Set(row, "<Server>k__BackingField", data[0]);
                var serverInfo = infoType.GetMethod("From", BindingFlags.Public | BindingFlags.Static).Invoke(null, new object[] { Get(data[0], "Id"), Get(data[0], "Name") });
                Set(row, "<LocationIntent>k__BackingField", Activator.CreateInstance(serverIntentType, new object[] { location, serverInfo })); Set(row, "ConnectionManager", fakeManager); Call(row, "InvalidateIsRestricted", false); return row;
            }
            if (method.Name == "GetCity") return city;
            return Default(method.ReturnType);
        });
        Set(item, "ServersLoader", countryLoader); Set(item, "LocationItemFactory", countryFactory); Set(item, "<Country>k__BackingField", countryList[0]); Set(item, "<IsSearchItem>k__BackingField", true);
        Call(item, "InvalidateIsRestricted", false); var expanded = List(Call(item, "GetSubItems"));
        Check(expanded.Select(x => (string)Get(Get(x, "Server"), "Id")).SequenceEqual(new[] { "nl-free", "nl-offline" }), "Expanding NL on a free account returns its actual individual free-server rows");
        Check(!usedPaidList && searchArgument, "Free country expansion bypasses paid city/state lists and retains the row context");
        Check(expanded.All(x => !(bool)Get(x, "IsRestricted")), "Expanded free-server Connect actions have no local upgrade gate");
        ((Task)Call(expanded[0], "ToggleConnectionAsync")).GetAwaiter().GetResult();
        Check((string)Get(Get(Get(clickedIntent, "Location"), "Server"), "Id") == "nl-free", "Clicking an expanded free server sends its exact ID to the connection manager");
        Property(countryList[0], "Code", "DE"); Check(List(Call(item, "GetSubItems")).Count == 0, "Country expansion never substitutes another country's servers"); Property(countryList[0], "Code", "NL");
        Call(item, "InvalidateIsRestricted", true); expanded = List(Call(item, "GetSubItems"));
        Check(usedPaidList && expanded.Count == 1 && Object.ReferenceEquals(expanded[0], city), "Paid country expansion preserves the original city/state factory path");
        Console.WriteLine(checks + " selector checks passed. No account, network, service, settings file or installed app was changed."); return 0;
    }
    static object GetRawCountries(object cache) { return cache.GetType().GetField("_countries", Instance).GetValue(cache); }
}
