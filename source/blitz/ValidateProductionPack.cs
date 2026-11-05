using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Web.Script.Serialization;
using Patchwork;

public static class ValidateProductionPack
{
    static string Sha(byte[] bytes) { using (var h = SHA256.Create()) return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    static void Need(bool ok, string message) { if (!ok) throw new Exception(message); }
    static string AppOriginal;
    static string IcuOriginal;
    static string Root;
    static void CheckOriginal(string target, byte[] app, byte[] icu, string where)
    {
        Need(File.ReadAllBytes(Path.Combine(target, "resources", "app.asar")).SequenceEqual(app), where + ": archive bytes differ");
        Need(File.ReadAllBytes(Path.Combine(target, "icudtl.dat")).SequenceEqual(icu), where + ": companion bytes differ");
    }
    static void SavePair(string folder, string label, byte[] app, byte[] icu)
    {
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, label + ".asar"), app);
        File.WriteAllBytes(Path.Combine(folder, label + ".icudtl.dat"), icu);
    }
    static Dictionary<string, object> ApplyCombo(PatchBundle bundle, string[] ids, byte[] app, byte[] icu, string runName, string stageFolder)
    {
        string run = Path.Combine(Root, runName);
        string target = Path.Combine(run, "target");
        Directory.CreateDirectory(Path.Combine(target, "resources"));
        string appPath = Path.Combine(target, "resources", "app.asar"), icuPath = Path.Combine(target, "icudtl.dat");
        File.WriteAllBytes(appPath, app); File.WriteAllBytes(icuPath, icu);
        var engine = new PatchEngine(Path.Combine(run, "engine-data"));
        var plan = engine.Preview(bundle, target, ids);
        CheckOriginal(target, app, icu, runName + " preview");
        var journal = engine.Apply(plan);
        byte[] afterApp = File.ReadAllBytes(appPath), afterIcu = File.ReadAllBytes(icuPath);
        Need(afterApp.Length > 8 * 1024 * 1024, runName + ": archive unexpectedly small");
        SavePair(stageFolder, String.Join("+", ids), afterApp, afterIcu);
        engine.Restore(journal);
        CheckOriginal(target, app, icu, runName + " restore");
        return new Dictionary<string, object> {
            { "ids", ids }, { "archiveSha256", Sha(afterApp) }, { "icuSha256", Sha(afterIcu) },
            { "archiveBytes", afterApp.Length }, { "icuBytes", afterIcu.Length }, { "previewLeftTargetUnchanged", true }, { "byteExactTwoFileRestore", true }
        };
    }
    public static int Main(string[] args)
    {
        try {
            Need(args.Length == 4, "Usage: ValidateProductionPack PACK_JSON ORIGINAL_ASAR ORIGINAL_ICU NEW_SCRATCH");
            string packPath = Path.GetFullPath(args[0]);
            AppOriginal = Path.GetFullPath(args[1]); IcuOriginal = Path.GetFullPath(args[2]); Root = Path.GetFullPath(args[3]);
            Need(!Directory.Exists(Root) && !File.Exists(Root), "Use a fresh isolated scratch directory.");
            Directory.CreateDirectory(Root);
            byte[] originalApp = File.ReadAllBytes(AppOriginal), originalIcu = File.ReadAllBytes(IcuOriginal);
            string appBefore = Sha(originalApp), icuBefore = Sha(originalIcu);
            var bundle = PatchBundle.Parse(File.ReadAllText(packPath));
            string[] ids = bundle.Patches.Select(p => p.Id).ToArray();
            Need(ids.Length == 3, "Expected exactly three selectable patches for seven combinations.");
            string stages = Path.Combine(Root, "staged-pairs"); Directory.CreateDirectory(stages);
            var combinations = new List<Dictionary<string, object>>();
            var deterministic = new List<Dictionary<string, object>>();
            for (int mask = 1; mask < (1 << ids.Length); mask++) {
                string[] selected = ids.Where((id, i) => (mask & (1 << i)) != 0).ToArray();
                var first = ApplyCombo(bundle, selected, originalApp, originalIcu, "combo-" + mask, stages);
                var second = ApplyCombo(bundle, selected, originalApp, originalIcu, "determinism-" + mask, Path.Combine(Root, "determinism-scratch"));
                Need((string)first["archiveSha256"] == (string)second["archiveSha256"], "Nondeterministic archive output for " + String.Join(",", selected));
                Need((string)first["icuSha256"] == (string)second["icuSha256"], "Nondeterministic ICU output for " + String.Join(",", selected));
                combinations.Add(first);
                deterministic.Add(new Dictionary<string, object> { { "ids", selected }, { "archiveSha256Stable", true }, { "icuSha256Stable", true } });
                Console.WriteLine("PASS combination " + mask + ": " + String.Join(",", selected));
            }

            var transitionLog = new List<Dictionary<string, object>>();
            foreach (string drop in ids) {
                string transitionRoot = Path.Combine(Root, "transitions", drop);
                string targetRoot = Path.Combine(transitionRoot, "target");
                Directory.CreateDirectory(Path.Combine(targetRoot, "resources"));
                string targetApp = Path.Combine(targetRoot, "resources", "app.asar"), targetIcu = Path.Combine(targetRoot, "icudtl.dat");
                File.WriteAllBytes(targetApp, originalApp); File.WriteAllBytes(targetIcu, originalIcu);
                var transitionEngine = new PatchEngine(Path.Combine(transitionRoot, "engine-data"));
                transitionEngine.Apply(transitionEngine.Preview(bundle, targetRoot, ids));
                string[] reduced = ids.Where(id => id != drop).ToArray();
                transitionEngine.Apply(transitionEngine.Preview(bundle, targetRoot, reduced));
                Need(transitionEngine.ActiveSession(targetRoot).PatchIds.OrderBy(x => x).SequenceEqual(reduced.OrderBy(x => x)), "Deselect update did not select expected patches.");
                transitionEngine.Apply(transitionEngine.Preview(bundle, targetRoot, ids));
                Need(transitionEngine.ActiveSession(targetRoot).PatchIds.OrderBy(x => x).SequenceEqual(ids.OrderBy(x => x)), "Reselect update did not select all patches.");
                transitionEngine.Restore(transitionEngine.ActiveSession(targetRoot));
                CheckOriginal(targetRoot, originalApp, originalIcu, "transition restore " + drop);
                transitionLog.Add(new Dictionary<string, object> { { "deselected", drop }, { "reselected", drop }, { "passed", true } });
                Console.WriteLine("PASS deselect/reselect " + drop);
            }

            string appAfter = Sha(File.ReadAllBytes(AppOriginal)), icuAfter = Sha(File.ReadAllBytes(IcuOriginal));
            Need(appBefore == appAfter && icuBefore == icuAfter, "Installed original hashes changed during validation.");
            var result = new Dictionary<string, object> {
                { "result", "PASS" }, { "engineAssemblyVersion", typeof(PatchEngine).Assembly.GetName().Version.ToString() },
                { "engineAssemblySha256", Sha(File.ReadAllBytes(typeof(PatchEngine).Assembly.Location)) },
                { "monoCecilSha256", Sha(File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(typeof(PatchEngine).Assembly.Location), "Mono.Cecil.dll"))) },
                { "candidateSha256", Sha(File.ReadAllBytes(packPath)) }, { "patchIds", ids },
                { "installedOriginals", new Dictionary<string, object> { { "appAsarSha256Before", appBefore }, { "appAsarSha256After", appAfter }, { "icudtlSha256Before", icuBefore }, { "icudtlSha256After", icuAfter }, { "unchanged", true } } },
                { "sevenNonemptyCombinations", combinations }, { "determinism", deterministic }, { "deselectReselectTransitions", transitionLog },
                { "transitionTwoFileRestore", true }, { "stagedPairDirectory", stages }
            };
            File.WriteAllText(Path.Combine(Root, "results.json"), new JavaScriptSerializer().Serialize(result));
            Console.WriteLine("PASS: 7 combinations, 7 deterministic replays, 6 deselect/reselect updates, byte-exact two-file restores, installed originals unchanged.");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
