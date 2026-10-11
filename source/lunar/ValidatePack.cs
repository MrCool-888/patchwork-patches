using System;
using System.IO;
using System.Linq;
using Patchwork;

class ValidatePack
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main(string[] args)
    {
        if (args.Length < 3 || args.Length > 4) throw new ArgumentException("Usage: ValidatePack PACK_JSON ORIGINALS_DIR NEW_SCRATCH_DIR [PREVIOUS_PACK_JSON]");
        var bundle = PatchBundle.Parse(File.ReadAllText(args[0]));
        string originals = Path.GetFullPath(args[1]), scratch = Path.GetFullPath(args[2]);
        if (Directory.Exists(scratch) || File.Exists(scratch)) throw new IOException("Choose a fresh scratch directory.");
        string target = Path.Combine(scratch, "target"), stage = Path.Combine(scratch, "stage");
        Directory.CreateDirectory(target); Directory.CreateDirectory(stage);
        foreach (string path in bundle.Patches.SelectMany(x => x.Operations).Select(x => x.File).Distinct())
        {
            string dest = PatchEngine.Resolve(target, path); Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(Path.Combine(originals, Path.GetFileName(path)), dest);
        }
        var engine = new PatchEngine(Path.Combine(scratch, "history"));
        var selected = bundle.Patches.Select(x => x.Id).ToArray();
        var plan = engine.Preview(bundle, target, selected);
        Assert(plan.Files.Count == bundle.Patches.SelectMany(x => x.Operations).Select(x => x.File).Distinct().Count(), "Unexpected file change count.");
        var game = plan.Files.Single(x => x.RelativePath.EndsWith("lunar.jar")); string before, after;
        byte[] repeated = JarPatches.Transform(game.BeforeBytes, bundle.Patches[1].Operations.Where(x => x.Kind.StartsWith("jar")).ToList(), out before, out after);
        Assert(game.AfterBytes.SequenceEqual(repeated), "JAR output is not deterministic.");
        foreach (var file in plan.Files) File.WriteAllBytes(Path.Combine(stage, Path.GetFileName(file.RelativePath)), file.AfterBytes);
        Console.WriteLine("PASS complete preview and deterministic JAR; staged copies are private target binaries.");
        engine.BeforeWriteForTest = i => { if (i == 1) throw new IOException("injected failure"); };
        try { engine.Apply(plan); throw new Exception("Expected injected failure."); } catch (IOException) { }
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.BeforeHash, "Rollback mismatch.");
        Console.WriteLine("PASS partial-write rollback."); engine.BeforeWriteForTest = null;
        if (args.Length == 4)
        {
            var previous = PatchBundle.Parse(File.ReadAllText(args[3]));
            engine.Apply(engine.Preview(previous, target, previous.Patches.Select(x => x.Id).ToArray()));
        }
        engine.Apply(engine.Preview(bundle, target, selected));
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.AfterHash, "Apply mismatch.");
        if (selected.Contains("disable-auto-updates"))
        {
            var withoutUpdaterIds = selected.Where(x => x != "disable-auto-updates").ToArray();
            var removal = engine.Preview(bundle, target, withoutUpdaterIds);
            var archive = removal.Files.Single(x => x.RelativePath.EndsWith("app.asar"));
            Assert(archive.AfterHash != archive.BeforeHash, "Removing update patch did not change launcher.");
            if (args.Length == 4)
            {
                var cleanRoot = Path.Combine(scratch, "original-target");
                foreach (var file in plan.Files)
                {
                    string dest = PatchEngine.Resolve(cleanRoot, file.RelativePath); Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    File.WriteAllBytes(dest, file.BeforeBytes);
                }
                var withoutUpdater = new PatchEngine(Path.Combine(scratch, "selection-history")).Preview(bundle, cleanRoot, withoutUpdaterIds);
                Assert(archive.AfterHash == withoutUpdater.Files.Single(x => x.RelativePath.EndsWith("app.asar")).AfterHash, "Deselection did not recover the selected launcher patch.");
            }
            engine.Apply(removal);
            engine.Apply(engine.Preview(bundle, target, selected));
            Console.WriteLine("PASS direct pack upgrade and updater deselection/reselection.");
        }
        if (selected.Contains("bootstrap-download-fallback"))
        {
            var genesis = plan.Files.Single(x => x.RelativePath.EndsWith("genesis-0.1.0-SNAPSHOT-all.jar"));
            var withoutStartup = engine.Preview(bundle, target, selected.Where(x => x != "bootstrap-download-fallback"));
            Assert(withoutStartup.Files.Single(x => x.RelativePath == genesis.RelativePath).AfterHash == genesis.BeforeHash, "Removing startup patch did not restore Genesis.");
            if (args.Length == 4)
            {
                var previous = PatchBundle.Parse(File.ReadAllText(args[3]));
                var expected = new PatchEngine(Path.Combine(scratch, "startup-removal-history")).Preview(previous, Path.Combine(scratch, "original-target"), previous.Patches.Select(x => x.Id));
                Assert(withoutStartup.Files.Single(x => x.RelativePath.EndsWith("app.asar")).AfterHash == expected.Files.Single(x => x.RelativePath.EndsWith("app.asar")).AfterHash, "Removing startup patch did not recover the previous launcher without its bootstrap exception.");
            }
            engine.Apply(withoutStartup);
            engine.Apply(engine.Preview(bundle, target, selected));
            foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.AfterHash, "Startup reselection mismatch.");
            Console.WriteLine("PASS startup patch deselection restores original Genesis and reselection matches staged output.");
        }
        var remaining = selected.Where(x => x != "local-cosmetics").ToArray();
        var update = engine.Preview(bundle, target, remaining);
        Assert(update.Files.Single(x => x.RelativePath.EndsWith("lunar.jar")).AfterHash == game.BeforeHash, "Removing cosmetics did not restore JAR.");
        var journal = engine.Apply(update); Console.WriteLine("PASS full apply and selection removal.");
        engine.Restore(journal);
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.BeforeHash, "Final restore mismatch.");
        Console.WriteLine("PASS exact restore of all original files.");
    }
}
