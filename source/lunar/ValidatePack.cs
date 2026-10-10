using System;
using System.IO;
using System.Linq;
using Patchwork;

class ValidatePack
{
    static void Assert(bool ok, string message) { if (!ok) throw new Exception(message); }
    static void Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Usage: ValidatePack PACK_JSON ORIGINALS_DIR NEW_SCRATCH_DIR");
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
        var plan = engine.Preview(bundle, target, new[] { "clean-launcher", "local-cosmetics" });
        Assert(plan.Files.Count == 3, "Expected three file changes.");
        var game = plan.Files.Single(x => x.RelativePath.EndsWith("lunar.jar")); string before, after;
        byte[] repeated = JarPatches.Transform(game.BeforeBytes, bundle.Patches[1].Operations.Where(x => x.Kind.StartsWith("jar")).ToList(), out before, out after);
        Assert(game.AfterBytes.SequenceEqual(repeated), "JAR output is not deterministic.");
        foreach (var file in plan.Files) File.WriteAllBytes(Path.Combine(stage, Path.GetFileName(file.RelativePath)), file.AfterBytes);
        Console.WriteLine("PASS three-file preview and deterministic JAR; staged copies are private target binaries.");
        engine.BeforeWriteForTest = i => { if (i == 1) throw new IOException("injected failure"); };
        try { engine.Apply(plan); throw new Exception("Expected injected failure."); } catch (IOException) { }
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.BeforeHash, "Rollback mismatch.");
        Console.WriteLine("PASS partial-write rollback."); engine.BeforeWriteForTest = null;
        engine.Apply(engine.Preview(bundle, target, new[] { "clean-launcher", "local-cosmetics" }));
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.AfterHash, "Apply mismatch.");
        var update = engine.Preview(bundle, target, new[] { "clean-launcher" });
        Assert(update.Files.Single(x => x.RelativePath.EndsWith("lunar.jar")).AfterHash == game.BeforeHash, "Removing cosmetics did not restore JAR.");
        var journal = engine.Apply(update); Console.WriteLine("PASS full apply and selection removal.");
        engine.Restore(journal);
        foreach (var file in plan.Files) Assert(PatchEngine.Hash(PatchEngine.Read(PatchEngine.Resolve(target, file.RelativePath))) == file.BeforeHash, "Final restore mismatch.");
        Console.WriteLine("PASS exact restore of all three original files.");
    }
}
