using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Patchwork;

// Compare resolved CIL names, branches, locals and exception regions, not build-local tokens.
class CompareTargets
{
    static bool normalize;
    static string Body(MethodDefinition method)
    {
        if (!method.HasBody) return "no body";
        var body = method.Body;
        Func<Instruction, string> index = i => i == null ? "end" : body.Instructions.IndexOf(i).ToString();
        var lines = new List<string> { method.Attributes + "|" + method.ImplAttributes + "|" + body.InitLocals + "|" + body.MaxStackSize };
        lines.AddRange(body.Variables.Select(v => v.VariableType.FullName));
        foreach (var instruction in body.Instructions)
        {
            object operand = instruction.Operand;
            string value = operand is Instruction ? index((Instruction)operand) : operand is Instruction[] ? String.Join(",", ((Instruction[])operand).Select(index)) : Convert.ToString(operand, CultureInfo.InvariantCulture);
            if (normalize && operand is string) value = System.Text.RegularExpressions.Regex.Replace(value, @"^C:\\gitlab\\builds\\[^\\]+\\0\\ProtonVPN\\Windows\\win-app\\", "<upstream-build>/");
            var called = operand as MethodReference;
            if (normalize && called != null && called.DeclaringType.FullName == "ProtonVPN.Client.Logic.Servers.Cache.ServersCache" && called.Name.StartsWith("<get_Countries>b__", StringComparison.Ordinal))
                value = "get_Countries lambda body|" + Body(called.Resolve());
            lines.Add(instruction.OpCode.Code + "|" + value);
        }
        lines.AddRange(body.ExceptionHandlers.Select(h => h.HandlerType + "|" + index(h.TryStart) + "|" + index(h.TryEnd) + "|" + index(h.HandlerStart) + "|" + index(h.HandlerEnd) + "|" + index(h.FilterStart) + "|" + h.CatchType));
        return String.Join("\n", lines);
    }
    static int Main(string[] args)
    {
        normalize = args.Length > 4 && args[4] == "--normalize-build-details";
        var bundle = PatchBundle.Parse(File.ReadAllText(args[0]));
        var results = new List<object>(); int same = 0, different = 0;
        foreach (var file in bundle.Patches.SelectMany(p => p.Operations).GroupBy(o => o.File))
        using (var oldModule = ModuleDefinition.ReadModule(Path.Combine(args[1], file.Key)))
        using (var newModule = ModuleDefinition.ReadModule(Path.Combine(args[2], file.Key)))
        {
            var oldMethods = ManagedPatches.Types(oldModule.Types).SelectMany(t => t.Methods).GroupBy(m => m.FullName).ToDictionary(g => g.Key, g => g.First());
            var newMethods = ManagedPatches.Types(newModule.Types).SelectMany(t => t.Methods).GroupBy(m => m.FullName).ToDictionary(g => g.Key, g => g.First());
            var names = new HashSet<string>(file.Select(o => o.Method));
            foreach (var method in oldMethods.Values)
                if (method.DeclaringType.FullName.StartsWith("ProtonVPN.Client.Logic.Auth.UserAuthenticator", StringComparison.Ordinal)
                    || method.DeclaringType.FullName.StartsWith("ProtonVPN.Client.UI.Login.Pages.SignInPageViewModel", StringComparison.Ordinal)) names.Add(method.FullName);
            foreach (var name in names.OrderBy(n => n))
            {
                MethodDefinition before, after;
                if (!oldMethods.TryGetValue(name, out before) || !newMethods.TryGetValue(name, out after)) throw new Exception("Missing target: " + name);
                bool equal = Body(before) == Body(after); if (equal) same++; else different++;
                results.Add(new { file = file.Key, method = name, resolvedCilEqual = equal });
                if (!equal) {
                    Console.WriteLine("CHANGED " + file.Key + ": " + name);
                    var oldLines = Body(before).Split('\n'); var newLines = Body(after).Split('\n');
                    foreach (int i in Enumerable.Range(0, Math.Min(oldLines.Length, newLines.Length)).Where(i => oldLines[i] != newLines[i]).Take(4))
                        Console.WriteLine("  " + i + " BEFORE " + oldLines[i] + " AFTER " + newLines[i]);
                }
            }
        }
        File.WriteAllText(args[3], new JavaScriptSerializer().Serialize(new { equalMethods = same, changedMethods = different, methods = results }));
        Console.WriteLine("Resolved CIL comparison: " + same + " equal, " + different + " changed.");
        return different == 0 ? 0 : 2;
    }
}
