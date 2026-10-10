using System;
using System.IO;
using System.Linq;
using Patchwork;

public static class ValidatePack
{
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action, string expected)
    {
        try { action(); } catch (Exception error) { if (error.Message.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0) return; throw; }
        throw new Exception("Expected rejection: " + expected);
    }
    public static int Main(string[] args)
    {
        try
        {
            string packPath = Path.GetFullPath(args[0]); string originalPath = Path.GetFullPath(args[1]); string scratch = Path.GetFullPath(args[2]);
            if (Directory.Exists(scratch)) throw new Exception("Use a fresh isolated validation directory.");
            string target = Path.Combine(scratch, "target"); Directory.CreateDirectory(Path.Combine(target, "resources"));
            string destination = Path.Combine(target, "resources", "app.asar"); File.Copy(originalPath, destination);
            string icuSource = Path.GetFullPath(args[3]), icuTarget = Path.Combine(target, "icudtl.dat"); File.Copy(icuSource, icuTarget);
            byte[] originalIcu = File.ReadAllBytes(icuSource);
            byte[] original = File.ReadAllBytes(destination);
            var bundle = PatchBundle.Parse(File.ReadAllText(packPath)); var engine = new PatchEngine(Path.Combine(scratch, "data"));
            var all = bundle.Patches.Select(x => x.Id).ToArray();
            var preview = engine.Preview(bundle, target, all);
            Assert(File.ReadAllBytes(destination).SequenceEqual(original), "Preview changed files.");
            Directory.CreateDirectory(Path.Combine(scratch, "artifacts"));
            File.WriteAllText(Path.Combine(scratch, "artifacts", "before.txt"), preview.Files[0].BeforeText);
            File.WriteAllText(Path.Combine(scratch, "artifacts", "after.txt"), preview.Files[0].AfterText);
            var journal = engine.Apply(preview); byte[] applied = File.ReadAllBytes(destination);
            File.WriteAllBytes(Path.Combine(scratch, "artifacts", "both.asar"), applied);
            File.Copy(icuTarget, Path.Combine(scratch, "artifacts", "both.icudtl.dat"));
            Assert(applied.Length > 8 * 1024 * 1024 && applied.Length < AsarPatches.MaximumSize, "Unexpected archive size.");
            Reject(() => engine.Preview(bundle, target, all), "already modified");
            var compactOnly = engine.Preview(bundle, target, new[] { "compact-window" });
            File.WriteAllBytes(Path.Combine(scratch, "artifacts", "compact.asar"), compactOnly.Files[0].AfterBytes);
            File.WriteAllBytes(Path.Combine(scratch, "artifacts", "compact.icudtl.dat"), compactOnly.Files.Single(x => x.RelativePath == "icudtl.dat").AfterBytes);
            engine.BeforeWriteForTest = index => { throw new IOException("Injected update failure"); };
            Reject(() => engine.Apply(compactOnly), "previous patch version was restored");
            Assert(File.ReadAllBytes(destination).SequenceEqual(applied), "Rollback changed previous version.");
            engine.BeforeWriteForTest = null; var compactSession = engine.Apply(engine.Preview(bundle, target, new[] { "compact-window" }));
            var cleanSession = engine.Apply(engine.Preview(bundle, target, new[] { "clean-desktop" }));
            File.Copy(destination, Path.Combine(scratch, "artifacts", "clean.asar"));
            File.Copy(icuTarget, Path.Combine(scratch, "artifacts", "clean.icudtl.dat"));
            engine.Restore(cleanSession); Assert(File.ReadAllBytes(destination).SequenceEqual(original), "Restore did not reproduce original archive.");
            Assert(File.ReadAllBytes(icuTarget).SequenceEqual(originalIcu), "Restore did not reproduce the original companion file.");
            // Exercise the same serialized job boundary as the elevated helper without elevation.
            string job = Worker.CreateJob(engine, bundle, engine.Preview(bundle, target, all), null);
            Assert(Worker.Execute(job, engine.DataRoot) == 0, "Worker apply failed.");
            job = Worker.CreateJob(engine, null, null, engine.ActiveSession(target)); Assert(Worker.Execute(job, engine.DataRoot) == 0, "Worker restore failed.");
            Assert(File.ReadAllBytes(destination).SequenceEqual(original), "Worker restore mismatch.");
            Assert(File.ReadAllBytes(icuTarget).SequenceEqual(originalIcu), "Worker companion restore mismatch.");
            var stale = engine.Preview(bundle, target, all); File.AppendAllText(destination, "x"); Reject(() => engine.Apply(stale), "after preview"); File.WriteAllBytes(destination, original);
            Assert(File.ReadAllBytes(originalPath).SequenceEqual(original), "Installed archive changed.");
            Console.WriteLine("PASS real Blitz archive: preview, both selections, individual selections, direct updates, rollback, restore, worker transactions, stale preview and installed-original preservation.");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
