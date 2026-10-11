using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Patchwork;

// Builds tiny synthetic inputs and exercises the public production parser/engine.
// No application binaries or third-party payloads are included in this repository.
internal static class FixtureHarness
{
    static string PatchExe, Root;
    static string H(byte[] bytes) { return PatchEngine.Hash(bytes).ToLowerInvariant(); }
    static byte[] B(string value) { return Encoding.UTF8.GetBytes(value); }
    static string B64(byte[] value) { return Convert.ToBase64String(value); }
    static Dictionary<string, object> D(params object[] pairs)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int i = 0; i < pairs.Length; i += 2) result.Add((string)pairs[i], pairs[i + 1]);
        return result;
    }
    static string Target(string name) { return Path.Combine(Root, name, "target"); }
    static void Put(string target, string path, byte[] bytes)
    {
        string full = Path.Combine(target, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)); File.WriteAllBytes(full, bytes);
    }
    static byte[] Get(string target, string path) { return File.ReadAllBytes(Path.Combine(target, path.Replace('/', Path.DirectorySeparatorChar))); }
    static Dictionary<string, object> Op(string kind, string file, byte[] original)
    {
        return D("kind", kind, "file", file, "sha256", H(original));
    }
    static Dictionary<string, object> Bundle(string id, string app, string versionFile, byte[] marker, string packVersion, params object[] patches)
    {
        return D("schemaVersion", 1, "id", id, "appId", app, "appName", app + " fixture", "appVersion", "1.0",
            "versionFile", versionFile, "versionSha256", H(marker), "author", "Patchwork fixture examples",
            "source", "Synthetic local fixture; no live app compatibility claim", "packVersion", packVersion,
            "minimumPatcherVersion", "1.0.0", "patches", patches);
    }
    static Dictionary<string, object> Patch(string id, string name, params object[] operations)
    {
        return D("id", id, "name", name, "description", "Synthetic format demonstration on an isolated fixture.",
            "dependencies", new object[0], "conflicts", new object[0], "operations", operations);
    }
    static void SavePack(string name, string version, Dictionary<string, object> bundle)
    {
        string folder = Path.Combine(Root, name); Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "pack-" + version + ".json"), Json.Pretty(bundle), new UTF8Encoding(false));
    }
    static string Csc { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Microsoft.NET\\Framework64\\v4.0.30319\\csc.exe"); } }
    static void CompileManaged(string target)
    {
        string source = Path.Combine(Root, "managed-assembly", "FixtureAssembly.cs");
        foreach (string output in new[] { "FixtureAssembly.dll", "FixtureAssembly.exe" })
        {
            string targetType = output.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "library" : "exe";
            var start = new ProcessStartInfo(Csc, "/nologo /target:" + targetType + " /out:\"" + Path.Combine(target, output) + "\" \"" + source + "\"")
            { UseShellExecute = false, CreateNoWindow = true };
            using (var process = Process.Start(start)) { process.WaitForExit(); if (process.ExitCode != 0) throw new Exception("Could not compile the managed fixture source for " + output + "."); }
        }
    }

    static void GenerateConfig()
    {
        string target = Target("config-resources"); Directory.CreateDirectory(target);
        byte[] marker = B("synthetic-config-v1\n"), original = B("{\"theme\":\"light\"}\n");
        Put(target, "app.version", marker); Put(target, "settings.json", original);
        byte[] first = B("{\"theme\":\"dark\"}\n"), second = B("{\"theme\":\"contrast\"}\n");
        byte[] resource = B("accent=#7B61FF\n");
        var a1 = Op("fileReplace", "settings.json", original); a1["dataBase64"] = B64(first); a1["resultSha256"] = H(first);
        var a2 = Op("fileReplace", "settings.json", original); a2["dataBase64"] = B64(second); a2["resultSha256"] = H(second);
        var add = D("kind", "fileAdd", "file", "resources/theme.txt", "dataBase64", B64(resource), "resultSha256", H(resource));
        var b1 = Bundle("config-resources-demo", "config-resources-demo", "app.version", marker, "1.0.0",
            Patch("a", "Replace a setting", a1), Patch("b", "Add a resource", add));
        var b2 = Bundle("config-resources-demo", "config-resources-demo", "app.version", marker, "1.1.0",
            Patch("a", "Replace a setting", a2), Patch("b", "Add a resource", add));
        var b3 = Bundle("config-resources-demo", "config-resources-demo", "app.version", marker, "1.2.0",
            Patch("a", "Replace a setting", a2), Patch("b", "Add a resource", add));
        SavePack("config-resources", "1.0.0", b1); SavePack("config-resources", "1.1.0", b2); SavePack("config-resources", "1.2.0", b3);
    }

    static void GenerateManaged()
    {
        string target = Target("managed-assembly"); Directory.CreateDirectory(target); CompileManaged(target);
        byte[] marker = B("managed-fixture-v1\n"); Put(target, "build-id.txt", marker);
        byte[] dll = Get(target, "FixtureAssembly.dll"), exe = Get(target, "FixtureAssembly.exe");
        Func<string, string, byte[], bool, object> method = (file, name, assembly, value) => {
            var op = Op("managedReturn", file, assembly); op["method"] = "System.Boolean Demo.Feature::get_" + name + "()";
            op["returnType"] = "boolean"; op["value"] = value; return op;
        };
        var b1 = Bundle("managed-assembly-demo", "managed-assembly-demo", "build-id.txt", marker, "1.0.0",
            Patch("a", "Change DLL getter", method("FixtureAssembly.dll", "Alpha", dll, false)), Patch("b", "Change EXE getter", method("FixtureAssembly.exe", "Beta", exe, false)));
        var b2 = Bundle("managed-assembly-demo", "managed-assembly-demo", "build-id.txt", marker, "1.1.0",
            Patch("a", "Change DLL getter", method("FixtureAssembly.dll", "Alpha", dll, true)), Patch("b", "Change EXE getter", method("FixtureAssembly.exe", "Beta", exe, false)));
        var b3 = Bundle("managed-assembly-demo", "managed-assembly-demo", "build-id.txt", marker, "1.2.0",
            Patch("a", "Change DLL getter", method("FixtureAssembly.dll", "Alpha", dll, true)), Patch("b", "Change EXE getter", method("FixtureAssembly.exe", "Beta", exe, false)));
        SavePack("managed-assembly", "1.0.0", b1); SavePack("managed-assembly", "1.1.0", b2); SavePack("managed-assembly", "1.2.0", b3);
    }

    static byte[] MakeAsar(string text, byte[] icon)
    {
        byte[] js = B(text); byte[] packed = js.Concat(icon).ToArray();
        Func<byte[], object> integrity = data => D("algorithm", "SHA256", "hash", H(data), "blockSize", 4194304, "blocks", new[] { H(data) });
        var files = D(
            "main.js", D("size", js.Length, "offset", "0", "integrity", integrity(js)),
            "assets", D("files", D("icon.bin", D("size", icon.Length, "offset", js.Length.ToString(), "integrity", integrity(icon)))));
        byte[] header = Encoding.UTF8.GetBytes(Json.Serializer.Serialize(D("files", files)));
        int padded = (8 + header.Length + 3) & ~3;
        using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
        {
            writer.Write(4); writer.Write(padded); writer.Write(padded - 4); writer.Write(header.Length); writer.Write(header);
            writer.Write(new byte[padded - 8 - header.Length]); writer.Write(packed); return stream.ToArray();
        }
    }
    static Dictionary<string, object> Checksum(byte[] archive, byte[] companion)
    {
        var op = D("kind", "asarChecksum", "file", "resource.dat", "sha256", H(companion), "archive", "app.asar",
            "archiveSha256", H(archive), "algorithm", "xxhash32", "offset", companion.Length - 4);
        return op;
    }
    static void GenerateAsar()
    {
        string target = Target("electron-asar"); Directory.CreateDirectory(target);
        byte[] marker = MakeAsar("console.log('hello');\n", new byte[] { 1, 2, 3 });
        byte[] companion = new byte[12]; Buffer.BlockCopy(BitConverter.GetBytes(AsarChecksum.Hash(marker)), 0, companion, companion.Length - 4, 4);
        Put(target, "app.asar", marker); Put(target, "resource.dat", companion);
        byte[] icon1 = new byte[] { 3, 2, 1, 4 }, icon2 = new byte[] { 9, 8, 7, 6 };
        byte[] addition = B("Synthetic ASAR resource\n");
        Func<string, byte[], object[]> archiveOps = (replacement, icon) => {
            var text = Op("asarTextReplace", "app.asar", marker); text["entry"] = "main.js"; text["entrySha256"] = H(B("console.log('hello');\n"));
            text["find"] = "hello"; text["replacement"] = replacement; text["count"] = 1;
            var replace = Op("asarEntryReplace", "app.asar", marker); replace["entry"] = "assets/icon.bin"; replace["entrySha256"] = H(new byte[] { 1, 2, 3 });
            replace["dataBase64"] = B64(icon); replace["resultSha256"] = H(icon);
            return new object[] { text, replace, Checksum(marker, companion) };
        };
        var add = Op("asarEntryAdd", "app.asar", marker); add["entry"] = "assets/help.txt"; add["dataBase64"] = B64(addition); add["resultSha256"] = H(addition);
        var noteBytes = B("synthetic sidecar\n"); var sidecar = D("kind", "fileAdd", "file", "fixture-note.txt", "dataBase64", B64(noteBytes), "resultSha256", H(noteBytes));
        object[] A1 = archiveOps("welcome", icon1), A2 = archiveOps("patched", icon2);
        var b1 = Bundle("electron-asar-demo", "electron-asar-demo", "app.asar", marker, "1.0.0",
            Patch("a", "Edit packed resources", A1), Patch("b", "Add sidecar", add, sidecar, Checksum(marker, companion)));
        var b2 = Bundle("electron-asar-demo", "electron-asar-demo", "app.asar", marker, "1.1.0",
            Patch("a", "Edit packed resources", A2), Patch("b", "Add sidecar", add, sidecar, Checksum(marker, companion)));
        var b3 = Bundle("electron-asar-demo", "electron-asar-demo", "app.asar", marker, "1.2.0",
            Patch("a", "Edit packed resources", A2), Patch("b", "Add sidecar", add, sidecar, Checksum(marker, companion)));
        SavePack("electron-asar", "1.0.0", b1); SavePack("electron-asar", "1.1.0", b2); SavePack("electron-asar", "1.2.0", b3);
    }

    static byte[] MakeClass(ushort sourceFileIndex)
    {
        using (var stream = new MemoryStream())
        {
            Action<byte> w1 = value => stream.WriteByte(value);
            Action<ushort> w2 = value => { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); };
            Action<uint> w4 = value => { w2((ushort)(value >> 16)); w2((ushort)value); };
            Action<string> utf = value => { byte[] bytes = Encoding.UTF8.GetBytes(value); w1(1); w2((ushort)bytes.Length); stream.Write(bytes, 0, bytes.Length); };
            w4(0xcafebabe); w2(0); w2(52); w2(15);
            utf("java/lang/Object"); w1(7); w2(1);
            utf("Demo"); w1(7); w2(3);
            utf("<init>"); utf("()V"); utf("Code");
            w1(10); w2(2); w2(9); w1(12); w2(5); w2(6);
            utf("SourceFile"); utf("Fixture.java"); utf("Demo.java"); utf("main"); utf("([Ljava/lang/String;)V");
            w2(0x0021); w2(4); w2(2); w2(0); w2(0);
            w2(2); w2(0x0001); w2(5); w2(6); w2(1);
            w2(7); w4(17); w2(1); w2(1); w4(5);
            byte[] code = new byte[] { 0x2a, 0xb7, 0x00, 0x08, 0xb1 }; stream.Write(code, 0, code.Length); w2(0); w2(0);
            w2(0x0009); w2(13); w2(14); w2(1); w2(7); w4(13); w2(0); w2(1); w4(1);
            w1(0xb1); w2(0); w2(0);
            w2(1); w2(10); w4(2); w2(sourceFileIndex);
            return stream.ToArray();
        }
    }
    static byte[] MakeZip(byte[] settings, byte[] data, byte[] classFile)
    {
        using (var output = new MemoryStream())
        {
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            {
                foreach (var pair in new[] { new KeyValuePair<string, byte[]>("settings.properties", settings), new KeyValuePair<string, byte[]>("data.bin", data), new KeyValuePair<string, byte[]>("Demo.class", classFile) })
                { var entry = zip.CreateEntry(pair.Key, CompressionLevel.Optimal); entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero); using (var stream = entry.Open()) stream.Write(pair.Value, 0, pair.Value.Length); }
            }
            return output.ToArray();
        }
    }
    static void GenerateZip()
    {
        string target = Target("java-zip"); Directory.CreateDirectory(target);
        byte[] settings = B("mode=default\n"), data = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 }, classFile = MakeClass(12), marker = MakeZip(settings, data, classFile);
        Put(target, "fixture.jar", marker);
        Func<string, object> textOp = replacement => {
            var op = Op("zipTextReplace", "fixture.jar", marker); op["entry"] = "settings.properties"; op["entrySha256"] = H(settings);
            op["find"] = "mode=default"; op["replacement"] = replacement; op["count"] = 1; return op;
        };
        Func<byte, object> spliceOp = value => {
            var op = Op("zipEntrySplice", "fixture.jar", marker); op["entry"] = "data.bin"; op["entrySha256"] = H(data);
            byte[] result = data.ToArray(); result[3] = value; op["resultSha256"] = H(result);
            op["edits"] = new object[] { D("offset", 3, "length", 1, "dataBase64", B64(new[] { value })) }; return op;
        };
        byte[] resource = B("Synthetic archive resource\n");
        var add = Op("zipEntryAdd", "fixture.jar", marker); add["entry"] = "META-INF/example.txt"; add["dataBase64"] = B64(resource); add["resultSha256"] = H(resource);
        Func<ushort, object> classSplice = sourceFile => {
            byte[] changed = MakeClass(sourceFile); var op = Op("jarEntrySplice", "fixture.jar", marker); op["entry"] = "Demo.class";
            op["entrySha256"] = H(classFile); op["resultSha256"] = H(changed);
            op["edits"] = new object[] { D("offset", classFile.Length - 2, "length", 2, "dataBase64", B64(new[] { (byte)(sourceFile >> 8), (byte)sourceFile })) }; return op;
        };
        var b1 = Bundle("java-zip-demo", "java-zip-demo", "fixture.jar", marker, "1.0.0",
            Patch("a", "Edit Java source metadata and ZIP text", classSplice(11), textOp("mode=safe")), Patch("b", "Edit and add ZIP data resources", spliceOp(127), add));
        var b2 = Bundle("java-zip-demo", "java-zip-demo", "fixture.jar", marker, "1.1.0",
            Patch("a", "Edit Java source metadata and ZIP text", classSplice(12), textOp("mode=balanced")), Patch("b", "Edit and add ZIP data resources", spliceOp(127), add));
        var b3 = Bundle("java-zip-demo", "java-zip-demo", "fixture.jar", marker, "1.2.0",
            Patch("a", "Edit Java source metadata and ZIP text", classSplice(12), textOp("mode=balanced")), Patch("b", "Edit and add ZIP data resources", spliceOp(127), add));
        SavePack("java-zip", "1.0.0", b1); SavePack("java-zip", "1.1.0", b2); SavePack("java-zip", "1.2.0", b3);
    }

    static void GenerateRaw()
    {
        string target = Target("raw-binary"); Directory.CreateDirectory(target);
        byte[] marker = Encoding.ASCII.GetBytes("RAW-DEMO-0000-DATA-FIXTURE-32BYTE!");
        byte[] second = Encoding.ASCII.GetBytes("RAW-SECOND-000-DATA-FIXTURE-32BYTE");
        Put(target, "table.bin", marker); Put(target, "payload.bin", second);
        Func<string, byte[], int, byte, object> splice = (file, original, offset, value) => {
            byte[] result = original.ToArray(); result[offset] = value;
            var op = Op("binarySplice", file, original); op["resultSha256"] = H(result);
            op["edits"] = new object[] { D("offset", offset, "length", 1, "dataBase64", B64(new[] { value })) }; return op;
        };
        var b1 = Bundle("raw-binary-demo", "raw-binary-demo", "table.bin", marker, "1.0.0",
            Patch("a", "Change synthetic header byte", splice("table.bin", marker, 4, (byte)'X')), Patch("b", "Change synthetic payload byte", splice("payload.bin", second, 18, (byte)'Y')));
        var b2 = Bundle("raw-binary-demo", "raw-binary-demo", "table.bin", marker, "1.1.0",
            Patch("a", "Change synthetic header byte", splice("table.bin", marker, 4, (byte)'Q')), Patch("b", "Change synthetic payload byte", splice("payload.bin", second, 18, (byte)'Y')));
        var b3 = Bundle("raw-binary-demo", "raw-binary-demo", "table.bin", marker, "1.2.0",
            Patch("a", "Change synthetic header byte", splice("table.bin", marker, 4, (byte)'Q')), Patch("b", "Change synthetic payload byte", splice("payload.bin", second, 18, (byte)'Y')));
        SavePack("raw-binary", "1.0.0", b1); SavePack("raw-binary", "1.1.0", b2); SavePack("raw-binary", "1.2.0", b3);
    }

    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void VerifyJavaClass(byte[] archiveBytes, string name)
    {
        string verifyRoot = Path.Combine(Root, "java-zip", "verify"); Directory.CreateDirectory(verifyRoot);
        using (var input = new MemoryStream(archiveBytes)) using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
        using (var source = zip.GetEntry("Demo.class").Open()) using (var output = File.Create(Path.Combine(verifyRoot, "Demo.class"))) source.CopyTo(output);
        var start = new ProcessStartInfo("java", "-Xverify:all -cp \"" + verifyRoot + "\" Demo") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        try
        {
            using (var process = Process.Start(start))
            {
                if (!process.WaitForExit(5000))
                {
                    process.Kill(); Console.WriteLine("Java runtime unavailable or did not return; skipped optional -Xverify:all class check."); return;
                }
                string stdout = process.StandardOutput.ReadToEnd(), stderr = process.StandardError.ReadToEnd();
                if (process.ExitCode != 0) throw new Exception(name + ": Java rejected generated class: " + stdout + stderr);
            }
        }
        catch (System.ComponentModel.Win32Exception) { Console.WriteLine("Java runtime unavailable; skipped optional -Xverify:all class check."); }
    }
    static byte[] SnapshotMarker(string name, string marker) { return Get(Target(name), marker); }
    static Dictionary<string, byte[]> Snapshot(string target)
    {
        return Directory.GetFiles(target, "*", SearchOption.AllDirectories).ToDictionary(x => x.Substring(target.Length + 1), File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
    }
    static PatchBundle ReadBundle(string name, string version)
    { return PatchBundle.Parse(File.ReadAllText(Path.Combine(Root, name, "pack-" + version + ".json"), Encoding.UTF8)); }
    static Journal ApplyAndCheck(PatchEngine engine, PatchPlan plan)
    {
        Journal journal = engine.Apply(plan);
        foreach (var change in plan.Files)
        {
            string full = Path.Combine(plan.TargetRoot, change.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert(File.Exists(full) == change.AfterExists, plan.BundleId + ": applied file-existence state differs for " + change.RelativePath);
            if (change.AfterExists) Assert(File.ReadAllBytes(full).SequenceEqual(change.AfterBytes), plan.BundleId + ": applied bytes differ for " + change.RelativePath);
        }
        return journal;
    }
    static void Exercise(string name)
    {
        Console.WriteLine("Lifecycle fixture: " + name);
        string target = Target(name), data = Path.Combine(Root, name, "d"); var originals = Snapshot(target);
        var engine = new PatchEngine(data); var v1 = ReadBundle(name, "1.0.0");
        var first = ApplyAndCheck(engine, engine.Preview(v1, target, new[] { "a", "b" }));
        Assert(first.State == "Applied", name + ": initial apply did not commit.");
        if (name == "config-resources")
        {
            Assert(B("{\"theme\":\"dark\"}\n").SequenceEqual(Get(target, "settings.json")), "config: patch a did not set the dark theme");
            Assert(File.Exists(Path.Combine(target, "resources", "theme.txt")), "config: patch b did not add its resource");
        }
        if (name == "managed-assembly")
        {
            Assert(!Get(target, "FixtureAssembly.dll").SequenceEqual(originals["FixtureAssembly.dll"]), "managed: DLL operation made no change");
            Assert(!Get(target, "FixtureAssembly.exe").SequenceEqual(originals["FixtureAssembly.exe"]), "managed: EXE operation made no change");
        }
        if (name == "java-zip") VerifyJavaClass(Get(target, "fixture.jar"), "initial Java member splice");
        var v2 = ReadBundle(name, "1.1.0");
        ApplyAndCheck(engine, engine.Preview(v2, target, new[] { "a" }));
        if (name == "config-resources")
        {
            Assert(!File.Exists(Path.Combine(target, "resources", "theme.txt")), "config: deselecting b did not remove its added resource");
            Assert(B("{\"theme\":\"contrast\"}\n").SequenceEqual(Get(target, "settings.json")), "config: pack update did not install a's new setting");
        }
        if (name == "managed-assembly")
        {
            Assert(!Get(target, "FixtureAssembly.dll").SequenceEqual(originals["FixtureAssembly.dll"]), "managed: selected DLL operation was lost on update");
            Assert(Get(target, "FixtureAssembly.exe").SequenceEqual(originals["FixtureAssembly.exe"]), "managed: deselected EXE operation remained after update");
        }
        if (name == "electron-asar") Assert(!File.Exists(Path.Combine(target, "fixture-note.txt")), "ASAR: deselecting b did not remove its sidecar");
        if (name == "raw-binary") Assert(Get(target, "payload.bin").SequenceEqual(originals["payload.bin"]), "raw binary: deselected payload edit remained after update");
        var v3 = ReadBundle(name, "1.2.0");
        var latest = ApplyAndCheck(engine, engine.Preview(v3, target, new[] { "b" }));
        if (name == "config-resources")
        {
            Assert(B("{\"theme\":\"light\"}\n").SequenceEqual(Get(target, "settings.json")), "config: deselecting a did not restore the original setting");
            Assert(File.Exists(Path.Combine(target, "resources", "theme.txt")), "config: selected b did not add its resource");
        }
        if (name == "managed-assembly")
        {
            Assert(Get(target, "FixtureAssembly.dll").SequenceEqual(originals["FixtureAssembly.dll"]), "managed: deselected DLL operation remained after update");
            Assert(!Get(target, "FixtureAssembly.exe").SequenceEqual(originals["FixtureAssembly.exe"]), "managed: selected EXE operation was not applied");
        }
        if (name == "electron-asar") Assert(File.Exists(Path.Combine(target, "fixture-note.txt")), "ASAR: selected b did not add its sidecar");
        if (name == "raw-binary") Assert(Get(target, "table.bin").SequenceEqual(originals["table.bin"]), "raw binary: deselected header edit remained after update");
        engine.Restore(latest);
        var restored = Snapshot(target);
        Assert(restored.Count == originals.Count, name + ": restore changed the fixture file set.");
        foreach (var item in originals) Assert(restored.ContainsKey(item.Key) && restored[item.Key].SequenceEqual(item.Value), name + ": restore was not byte-exact for " + item.Key);
    }
    static void Smoke()
    {
        Exercise("config-resources");
        Exercise("managed-assembly");
        Exercise("electron-asar");
        Exercise("java-zip");
        Exercise("raw-binary");
    }
    static void Generate()
    {
        Directory.CreateDirectory(Root); GenerateConfig(); GenerateManaged(); GenerateAsar(); GenerateZip(); GenerateRaw();
    }
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length != 3) throw new ArgumentException("Usage: FixtureHarness.exe PATCHWORK_EXE OUTPUT_DIR generate|smoke");
            PatchExe = Path.GetFullPath(args[0]); Root = Path.GetFullPath(args[1]);
            if (args[2] == "generate") Generate(); else if (args[2] == "smoke") Smoke(); else throw new ArgumentException("Unknown mode.");
            Console.WriteLine(args[2] + " passed for five synthetic fixtures."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
