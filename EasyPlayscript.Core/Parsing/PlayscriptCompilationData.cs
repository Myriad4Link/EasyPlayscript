using System.Collections.Generic;
using EasyPlayscript.DataModel;

namespace EasyPlayscript.Parsing;

public class PlayscriptCompilationData
{
    public Dictionary<string, ScriptVariants> Scripts { get; } = new();
    public Dictionary<string, TextVariants> Texts { get; } = new();
    public Dictionary<(string Name, string Variation), (string filePath, int line, int col)> ScriptLocations { get; } = new();
    public Dictionary<(string Name, string Variation), (string filePath, int line, int col)> TextLocations { get; } = new();
    public List<InterfaceDeclaration> Interfaces { get; } = [];
    public List<ImplementationInfo> Implementations { get; } = [];
    public bool HasErrors { get; set; }

    public List<ValidationDiagnostic> MergeFrom(PlayscriptCompilationData source)
    {
        var diagnostics = new List<ValidationDiagnostic>();
        MergeScriptVariants(diagnostics, source);
        MergeTextVariants(diagnostics, source);
        Interfaces.AddRange(source.Interfaces);
        return diagnostics;
    }

    private void MergeScriptVariants(
        List<ValidationDiagnostic> diagnostics,
        PlayscriptCompilationData source)
    {
        foreach (var kvp in source.Scripts)
        {
            var name = kvp.Key;
            var srcVar = kvp.Value;

            if (!Scripts.TryGetValue(name, out var tgtVar))
            {
                Scripts[name] = srcVar;
                foreach (var lkv in source.ScriptLocations)
                    if (lkv.Key.Name == name)
                        ScriptLocations[lkv.Key] = lkv.Value;
                continue;
            }

            if (srcVar.Unversioned != null)
            {
                if (tgtVar.Unversioned != null)
                {
                    var key = (name, "");
                    var sl = ScriptLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "script", name));
                }
                else
                {
                    tgtVar.Unversioned = srcVar.Unversioned;
                    var key = (name, "");
                    if (source.ScriptLocations.TryGetValue(key, out var s))
                        ScriptLocations[key] = s;
                }
            }

            foreach (var vk in srcVar.Numbered)
            {
                var varKey = vk.Key;
                if (tgtVar.Numbered.ContainsKey(varKey))
                {
                    var key = (name, varKey);
                    var sl = ScriptLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "script", $"{name} variation {varKey}"));
                }
                else
                {
                    tgtVar.Numbered[varKey] = vk.Value;
                    var key = (name, varKey);
                    if (source.ScriptLocations.TryGetValue(key, out var s))
                        ScriptLocations[key] = s;
                }
            }
        }
    }

    private void MergeTextVariants(
        List<ValidationDiagnostic> diagnostics,
        PlayscriptCompilationData source)
    {
        foreach (var kvp in source.Texts)
        {
            var name = kvp.Key;
            var srcVar = kvp.Value;

            if (!Texts.TryGetValue(name, out var tgtVar))
            {
                Texts[name] = srcVar;
                foreach (var lkv in source.TextLocations)
                    if (lkv.Key.Name == name)
                        TextLocations[lkv.Key] = lkv.Value;
                continue;
            }

            if (srcVar.Unversioned != null)
            {
                if (tgtVar.Unversioned != null)
                {
                    var key = (name, "");
                    var sl = TextLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "text", name));
                }
                else
                {
                    tgtVar.Unversioned = srcVar.Unversioned;
                    var key = (name, "");
                    if (source.TextLocations.TryGetValue(key, out var s))
                        TextLocations[key] = s;
                }
            }

            foreach (var vk in srcVar.Numbered)
            {
                var varKey = vk.Key;
                if (tgtVar.Numbered.ContainsKey(varKey))
                {
                    var key = (name, varKey);
                    var sl = TextLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "text", $"{name} variation {varKey}"));
                }
                else
                {
                    tgtVar.Numbered[varKey] = vk.Value;
                    var key = (name, varKey);
                    if (source.TextLocations.TryGetValue(key, out var s))
                        TextLocations[key] = s;
                }
            }
        }
    }
}
