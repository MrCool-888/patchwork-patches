# Managed assembly fixture

`FixtureAssembly.cs` is the source for tiny synthetic IL targets. The builder compiles it as both a DLL and an EXE with the installed .NET Framework compiler, then fingerprints each generated file in its recipe; it does not check in either compiled file. Separate patch IDs edit one getter in each assembly so the smoke run exercises selection and deselection across both formats.
