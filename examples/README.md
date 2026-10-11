# Synthetic Patchwork examples

These five fixtures teach the schema and provide isolated targets for Patchwork's production recipe validator. Each example directory includes a complete `pack.json` with real SHA-256 values and the source fixtures needed to reproduce its target. The managed assembly's generated DLL/EXE are omitted from the repository; the builder compiles them and writes matching hashes into fresh recipes. The fixtures contain no third-party application binaries and make no claims about live application behavior. Each pack uses a synthetic `appId`, stable patch IDs and Patchwork 0.10.0 minimum version.

| Example | Demonstrates |
| --- | --- |
| `config-resources` | Whole-file replacement and adding an absent local resource file. |
| `managed-assembly` | Tiny source-built managed DLL and EXE fixtures with hash-pinned IL method operations. |
| `electron-asar` | Packed ASAR text/resource edits and an added member. |
| `java-zip` | A valid minimal JVM class member splice plus ZIP text replacement, a resource addition and bounded resource splicing. `Demo.java` is the corresponding source; no JDK is needed to generate or smoke-test the class bytes. |
| `raw-binary` | Bounded byte splices in a synthetic `.bin` data file. The bytes are a data-format demonstration, not a native executable patch. |

Use the PowerShell builder to generate disposable targets and complete JSON recipes with actual hashes. It compiles the harness against the production executable, runs the production CLI validator for each recipe version, then exercises apply, pack update, deselection and byte-exact restore through the public production engine API:

```powershell
.\build-and-smoke.ps1 -PatchworkExe C:\path\to\Patchwork.exe
```

By default, output goes to a new temporary folder. To keep it elsewhere, supply a new or empty `-OutputDirectory`; the script will not overwrite a non-empty folder. The Windows .NET Framework compiler builds the tiny managed fixture. The Java fixture class is generated from its documented class-file layout and matching source, so no JDK, Node package or NuGet install is required. The smoke script tries an optional `java -Xverify:all` check when a Java runtime is available; it skips that check if the runtime is missing or does not return. Never use generated fixtures as official release assets. The separate patcher repository's [format guide](https://github.com/MrCool-888/patchwork/blob/main/PATCH-FORMAT.md) defines the operations and bounds.
