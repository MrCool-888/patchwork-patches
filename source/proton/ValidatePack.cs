using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Script.Serialization;
using Patchwork;

public static class ValidatePack
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Pass(string message) { Console.WriteLine("PASS " + message); }
    static string Hash(byte[] bytes) { return PatchEngine.Hash(bytes); }
    static string FileHash(string path) { return Hash(File.ReadAllBytes(path)); }
    static string[] Paths(PatchBundle bundle) { return bundle.Patches.SelectMany(p => p.Operations).Select(o => o.File).Concat(new[] { bundle.VersionFile }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }

    static void CopyTracked(string source, string target, IEnumerable<string> paths)
    {
        Directory.CreateDirectory(target);
        foreach (string relative in paths)
        {
            string from = Path.GetFullPath(Path.Combine(source, relative));
            string to = Path.GetFullPath(Path.Combine(target, relative));
            if (!from.StartsWith(Path.GetFullPath(source).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Tracked path escapes source root: " + relative);
            if (File.Exists(from)) { Directory.CreateDirectory(Path.GetDirectoryName(to)); File.Copy(from, to); }
        }
    }
    static string[] CopyManagedDependencies(string source, string target, IEnumerable<string> operationFiles)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in Directory.GetFiles(source, "*.dll", SearchOption.AllDirectories).OrderBy(x => x.Length))
            if (!byName.ContainsKey(Path.GetFileName(file))) byName[Path.GetFileName(file)] = file;
        foreach (string file in Directory.GetFiles(source, "*.dll", SearchOption.TopDirectoryOnly)) byName[Path.GetFileName(file)] = file;
        var pending = new Queue<string>(operationFiles.Where(x => x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var copied = new List<string>();
        while (pending.Count != 0)
        {
            string name = pending.Dequeue();
            if (!seen.Add(name)) continue;
            string sourceFile;
            if (!byName.TryGetValue(name, out sourceFile)) continue;
            AssemblyName[] references;
            try
            {
                using (var module = Mono.Cecil.ModuleDefinition.ReadModule(sourceFile))
                    references = module.AssemblyReferences.Select(x => new AssemblyName(x.Name)).ToArray();
            }
            catch { continue; }
            foreach (AssemblyName reference in references)
            {
                string dependencyName = reference.Name + ".dll", dependencySource;
                if (!byName.TryGetValue(dependencyName, out dependencySource)) continue;
                string destination = Path.Combine(target, dependencyName);
                if (!File.Exists(destination)) { File.Copy(dependencySource, destination); copied.Add(dependencyName); }
                pending.Enqueue(dependencyName);
            }
        }
        return copied.ToArray();
    }
    static Dictionary<string, string> Snapshot(string root, IEnumerable<string> paths)
    {
        return paths.ToDictionary(p => p, p => { string f = Path.Combine(root, p); return File.Exists(f) ? FileHash(f) : "<absent>"; }, StringComparer.OrdinalIgnoreCase);
    }
    static void Same(Dictionary<string, string> expected, Dictionary<string, string> actual, string message)
    {
        foreach (var item in expected) Assert(actual.ContainsKey(item.Key) && actual[item.Key] == item.Value, message + ": " + item.Key);
    }
    static void Reject(Action action, string expected, string message)
    {
        try { action(); } catch (Exception error) { if (error.Message.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0) return; throw new Exception(message + " (unexpected error: " + error.Message + ")", error); }
        throw new Exception("Expected rejection: " + message);
    }
    static void AssertPlanEqual(PatchPlan a, PatchPlan b)
    {
        Assert(a.BundleId == b.BundleId && a.PackVersion == b.PackVersion && a.PatchIds.SequenceEqual(b.PatchIds), "Repeated preview metadata differed.");
        Assert(a.Files.Count == b.Files.Count, "Repeated preview file count differed.");
        foreach (var file in a.Files) Assert(b.Files.Any(x => x.RelativePath == file.RelativePath && x.BeforeHash == file.BeforeHash && x.AfterHash == file.AfterHash), "Repeated preview hashes differed: " + file.RelativePath);
    }
    static void ApplyAndCheck(PatchEngine engine, PatchBundle bundle, string target, string[] ids, Dictionary<string, string> options, out Journal journal)
    {
        var plan = engine.Preview(bundle, target, ids, options);
        journal = engine.Apply(plan);
        Assert(journal.BundleId == bundle.Id && journal.PackVersion == bundle.PackVersion && journal.PatchIds.SequenceEqual(ids), "History pack, version, or patch IDs did not match the applied selection.");
        foreach (var change in plan.Files)
        {
            string file = Path.Combine(target, change.RelativePath);
            Assert(change.AfterExists ? File.Exists(file) && FileHash(file) == change.AfterHash : !File.Exists(file), "Applied bytes differed from preview: " + change.RelativePath);
        }
    }
    static Dictionary<string, object> Run(string[] args)
    {
        if (args.Length < 3 || args.Length > 4) throw new ArgumentException("Usage: ValidatePack PACK_JSON ORIGINAL_APP_ROOT NEW_SCRATCH_DIR [WRONG_VERSION_ROOT]");
        string packPath = Path.GetFullPath(args[0]), original = Path.GetFullPath(args[1]), scratch = Path.GetFullPath(args[2]);
        Assert(!Directory.Exists(scratch) && !File.Exists(scratch), "Scratch path must not already exist.");
        var bundle = PatchBundle.Parse(File.ReadAllText(packPath));
        var ready = bundle.Patches.Where(p => p.Ready).ToArray();
        Assert(ready.Length == 15, "Expected 15 ready Proton patches; found " + ready.Length + ".");
        // This small sibling assembly is not patched, but Cecil resolves it while rewriting ProtonVPN.Client.dll.
        string[] tracked = Paths(bundle);
        byte[] versionBytes = File.ReadAllBytes(Path.Combine(original, bundle.VersionFile));
        Assert(Hash(versionBytes) == bundle.VersionSha256, "Original root does not match the bundle version fingerprint.");
        Directory.CreateDirectory(scratch);
        string target = Path.Combine(scratch, "target"); CopyTracked(original, target, tracked);
        tracked = tracked.Concat(CopyManagedDependencies(original, target, bundle.Patches.SelectMany(p => p.Operations).Select(o => o.File)).Select(Path.GetFileName)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var engine = new PatchEngine(Path.Combine(scratch, "data"));
        var ids = ready.Select(p => p.Id).ToArray();
        var clean = Snapshot(target, tracked);
        foreach (var patch in ready)
        {
            var individual = engine.Preview(bundle, target, new[] { patch.Id });
            Same(clean, Snapshot(target, tracked), "Individual preview wrote a target file");
            Assert(individual.PatchIds.SequenceEqual(new[] { patch.Id }), "Individual preview selected an unexpected patch.");
        }
        Pass("15 ready individual previews are read-only");

        var first = engine.Preview(bundle, target, ids);
        var second = engine.Preview(bundle, target, ids);
        AssertPlanEqual(first, second); Same(clean, Snapshot(target, tracked), "Combined preview wrote a target file");
        Pass("combined preview is deterministic and read-only");
        var initial = engine.Apply(first);
        Assert(initial.BundleId == bundle.Id && initial.PackVersion == bundle.PackVersion && initial.PatchIds.SequenceEqual(ids), "Initial history metadata mismatch.");
        foreach (var change in first.Files) Assert(change.AfterExists ? FileHash(Path.Combine(target, change.RelativePath)) == change.AfterHash : !File.Exists(Path.Combine(target, change.RelativePath)), "Apply did not match preview: " + change.RelativePath);
        Pass("apply matches preview and history identifies the bundle and all patches");

        var optionPatches = ready.Where(p => p.Options.Count > 0).ToArray();
        Assert(optionPatches.Length > 0 && optionPatches.SelectMany(p => p.Options).Count() >= 2, "Expected Proton color options were missing.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var patch in optionPatches) foreach (var option in patch.Options) options[patch.Id + "." + option.Id] = option.Default == "#1267AB" ? "#E58B21" : "#1267AB";
        Journal colored; ApplyAndCheck(engine, bundle, target, ids, options, out colored);
        Assert(options.All(x => colored.Options.ContainsKey(x.Key) && colored.Options[x.Key] == x.Value), "History did not store nondefault color choices.");
        Pass("nondefault color choices apply and persist in history");
        string[] withoutOptions = ready.Where(p => p.Options.Count == 0).Select(p => p.Id).ToArray();
        Journal deselected; ApplyAndCheck(engine, bundle, target, withoutOptions, null, out deselected);
        Assert(!optionPatches.SelectMany(p => p.Options).Any(o => deselected.Options.ContainsKey(optionPatches.Single(p => p.Options.Contains(o)).Id + "." + o.Id)), "Deselected color options remained in history.");
        Journal reselected; ApplyAndCheck(engine, bundle, target, ids, options, out reselected);
        Assert(options.All(x => reselected.Options.ContainsKey(x.Key) && reselected.Options[x.Key] == x.Value), "Reselected color options were not retained.");
        Pass("all color-option patches deselect and reselect with stored values");

        while (true)
        {
            var active = engine.ActiveSession(target);
            if (active == null) break;
            engine.Restore(active);
        }
        var restored = Snapshot(target, tracked); Same(clean, restored, "Restore did not exactly reproduce originals");
        bool restorationVerified = true;
        Pass("restore returned every tracked file to its original bytes");

        if (args.Length == 4)
        {
            string wrongRoot = Path.GetFullPath(args[3]), wrongTarget = Path.Combine(scratch, "wrong-version", "target");
            CopyTracked(original, wrongTarget, tracked);
            string suppliedVersion = Path.Combine(wrongRoot, bundle.VersionFile);
            Assert(File.Exists(suppliedVersion), "Wrong-version root does not contain " + bundle.VersionFile + ".");
            File.Copy(suppliedVersion, Path.Combine(wrongTarget, bundle.VersionFile), true);
            var beforeWrong = Snapshot(wrongTarget, tracked);
            var wrongEngine = new PatchEngine(Path.Combine(scratch, "wrong-version", "data"));
            Reject(() => wrongEngine.Preview(bundle, wrongTarget, ids), "fingerprint does not match", "Wrong-version target was not rejected");
            Same(beforeWrong, Snapshot(wrongTarget, tracked), "Wrong-version rejection wrote a target file");
            Pass("wrong-version target is rejected before writes");
        }

        string tamperedTarget = Path.Combine(scratch, "tampered", "target"); CopyTracked(original, tamperedTarget, tracked);
        string relevant = ready.SelectMany(p => p.Operations).Select(o => o.File).First(f => File.Exists(Path.Combine(tamperedTarget, f)));
        File.AppendAllText(Path.Combine(tamperedTarget, relevant), "\0tampered");
        var beforeTamper = Snapshot(tamperedTarget, tracked);
        var tamperEngine = new PatchEngine(Path.Combine(scratch, "tampered", "data"));
        Reject(() => tamperEngine.Preview(bundle, tamperedTarget, ids), "unsupported or already modified file", "Tampered original was not rejected");
        Same(beforeTamper, Snapshot(tamperedTarget, tracked), "Tamper rejection wrote a target file");
        Pass("tampered relevant original is rejected without writes");

        Journal final; ApplyAndCheck(engine, bundle, target, ids, null, out final);
        Assert(engine.ActiveSession(target).Id == final.Id, "Final all-selected target is not left patched.");
        Pass("all-selected target left patched for runtime probes");

        var hashes = new Dictionary<string, object>();
        foreach (string path in tracked) hashes[path] = new Dictionary<string, string> {
            { "original", clean[path] }, { "restored", restored[path] },
            { "final", Snapshot(target, new[] { path })[path] }
        };
        Assembly engineAssembly = typeof(PatchEngine).Assembly;
        return new Dictionary<string, object> {
            { "bundleId", bundle.Id }, { "packVersion", bundle.PackVersion }, { "appVersion", bundle.AppVersion },
            { "packSha256", FileHash(packPath) }, { "enginePath", engineAssembly.Location },
            { "engineVersion", engineAssembly.GetName().Version.ToString() }, { "engineSha256", FileHash(engineAssembly.Location) },
            { "target", target }, { "historyId", final.Id }, { "patchIds", ids }, { "fileHashes", hashes },
            { "restorationVerified", restorationVerified }, { "wrongVersionTested", args.Length == 4 }
        };
    }
    public static int Main(string[] args)
    {
        try
        {
            var result = Run(args);
            string report = Path.Combine(Path.GetFullPath(args[2]), "results.json");
            File.WriteAllText(report, new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(result));
            Console.WriteLine("RESULT " + report);
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("FAIL " + error); return 1; }
    }
}
