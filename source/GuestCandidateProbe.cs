using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Patchwork;

// Run only against a disposable copied installation with the candidate already applied.
class GuestCandidateProbe
{
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
    static MethodDefinition Method(ModuleDefinition module, string type, string name) { return ManagedPatches.Types(module.Types).Single(x => x.FullName == type).Methods.Single(x => x.Name == name); }
    static string Body(MethodDefinition method) { return string.Join("\n", method.Body.Instructions.Select(x => x.OpCode + " " + x.Operand)); }
    static int Main(string[] args) { try {
        var bundle = PatchBundle.Parse(File.ReadAllText(args[0])); string root = Path.GetFullPath(args[1]);
        var engine = new PatchEngine(args[2]); var current = engine.ActiveSession(root);
        Check(current != null && current.PackVersion == "1.5.1", "candidate applied history identifies pack 1.5.1");
        Check(!bundle.Patches.Any(x => x.Id == "amoled-theme" || x.Name.IndexOf("AMOLED", StringComparison.OrdinalIgnoreCase) >= 0), "AMOLED absent from patch catalog");
        var entry = current.Files.Single(x => x.RelativePath == "ProtonVPN.Client.dll");
        using (var original = ModuleDefinition.ReadModule(Path.Combine(current.DirectoryPath, entry.BackupFile))) using (var client = ModuleDefinition.ReadModule(Path.Combine(root, entry.RelativePath))) {
            string selector = "ProtonVPN.Client.Services.Selection.ApplicationThemeSelector";
            Check(Body(Method(original, selector, "GetTheme")) == Body(Method(client, selector, "GetTheme")), "direct update restores original theme selection instead of forcing Dark");
            var dictionaries = Method(client, "ProtonVPN.Client.App", "LoadTypographyResourceDictionary").Body.Instructions.Select(x => x.Operand).OfType<string>().Where(x => x.StartsWith("<ResourceDictionary")).ToList();
            Check(dictionaries.Count == 2 && dictionaries.All(x => !x.Contains("BackgroundNorm")), "direct update removes AMOLED background resources and retains accent/switch dictionaries");
        }
        var hooks = bundle.Patches.SelectMany(x => x.Operations).Where(x => x.Kind == "managedEmbeddedHook").ToList();
        foreach (var group in hooks.GroupBy(x => x.File)) using (var module = ModuleDefinition.ReadModule(Path.Combine(root, group.Key))) {
            var resources = module.Resources.OfType<EmbeddedResource>().Where(x => x.Name.StartsWith("Patchwork.ClientCode.")).ToList();
            Check(resources.Count == 1 && PatchEngine.Hash(resources[0].GetResourceData()) == group.First().ModuleSha256, "embedded executable helper hash matches in " + group.Key);
        }
        using (var api = ModuleDefinition.ReadModule(Path.Combine(root, "ProtonVPN.Api.dll"))) Check(Method(api, "ProtonVPN.Api.TokenClient", "LogRefreshToken").Body.Instructions.Count == 1, "native token-prefix logging removed");
        var fingerprints = current.Files.ToDictionary(x => x.RelativePath, x => x.BeforeHash);
        var expected = current.Files.ToDictionary(x => x.RelativePath, x => x.AfterHash);
        engine.Restore(current);
        Check(fingerprints.All(x => PatchEngine.Hash(File.ReadAllBytes(Path.Combine(root, x.Key))) == x.Value), "all ten tracked original assemblies restore byte for byte");
        var again = engine.Preview(bundle, root, bundle.Patches.Where(x => x.Ready).Select(x => x.Id));
        Check(again.Files.Count == expected.Count && again.Files.All(x => expected[x.RelativePath] == x.AfterHash), "candidate output is deterministic against restored originals");
        return 0;
    } catch(Exception error) { Console.Error.WriteLine(error); return 1; } }
}
