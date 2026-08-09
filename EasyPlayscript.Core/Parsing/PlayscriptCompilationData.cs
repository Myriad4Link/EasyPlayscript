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

    public static string GetQualifiedName(string? ns, string name) =>
        ns != null ? $"{ns}.{name}" : name;

    public static (string? ns, string name) ParseQualifiedName(string qualifiedName)
    {
        var lastDot = qualifiedName.LastIndexOf('.');
        if (lastDot < 0) return (null, qualifiedName);
        return (qualifiedName.Substring(0, lastDot), qualifiedName.Substring(lastDot + 1));
    }

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
            var qualifiedName = kvp.Key;
            var srcVar = kvp.Value;

            if (!Scripts.TryGetValue(qualifiedName, out var tgtVar))
            {
                Scripts[qualifiedName] = srcVar;
                foreach (var lkv in source.ScriptLocations)
                    if (lkv.Key.Name == qualifiedName)
                        ScriptLocations[lkv.Key] = lkv.Value;
                continue;
            }

            if (srcVar.Unversioned != null)
            {
                if (tgtVar.Unversioned != null)
                {
                    var key = (qualifiedName, "");
                    var sl = ScriptLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "script", qualifiedName));
                }
                else
                {
                    tgtVar.Unversioned = srcVar.Unversioned;
                    var key = (qualifiedName, "");
                    if (source.ScriptLocations.TryGetValue(key, out var s))
                        ScriptLocations[key] = s;
                }
            }

            foreach (var vk in srcVar.Numbered)
            {
                var varKey = vk.Key;
                if (tgtVar.Numbered.ContainsKey(varKey))
                {
                    var key = (qualifiedName, varKey);
                    var sl = ScriptLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "script", $"{qualifiedName} variation {varKey}"));
                }
                else
                {
                    tgtVar.Numbered[varKey] = vk.Value;
                    var key = (qualifiedName, varKey);
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
            var qualifiedName = kvp.Key;
            var srcVar = kvp.Value;

            if (!Texts.TryGetValue(qualifiedName, out var tgtVar))
            {
                Texts[qualifiedName] = srcVar;
                foreach (var lkv in source.TextLocations)
                    if (lkv.Key.Name == qualifiedName)
                        TextLocations[lkv.Key] = lkv.Value;
                continue;
            }

            if (srcVar.Unversioned != null)
            {
                if (tgtVar.Unversioned != null)
                {
                    var key = (qualifiedName, "");
                    var sl = TextLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "text", qualifiedName));
                }
                else
                {
                    tgtVar.Unversioned = srcVar.Unversioned;
                    var key = (qualifiedName, "");
                    if (source.TextLocations.TryGetValue(key, out var s))
                        TextLocations[key] = s;
                }
            }

            foreach (var vk in srcVar.Numbered)
            {
                var varKey = vk.Key;
                if (tgtVar.Numbered.ContainsKey(varKey))
                {
                    var key = (qualifiedName, varKey);
                    var sl = TextLocations[key];
                    diagnostics.Add(new ValidationDiagnostic(DiagnosticCodes.DuplicateScriptName,
                        DiagnosticCodes.DuplicateScriptNameFormat,
                        sl.filePath, sl.line, sl.col, "text", $"{qualifiedName} variation {varKey}"));
                }
                else
                {
                    tgtVar.Numbered[varKey] = vk.Value;
                    var key = (qualifiedName, varKey);
                    if (source.TextLocations.TryGetValue(key, out var s))
                        TextLocations[key] = s;
                }
            }
        }
    }
}
