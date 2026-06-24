using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Container for parsed playscript data collected across multiple .scpt files.
/// Holds scripts, texts, their source locations, and interface declarations.
/// </summary>
public class PlayscriptCompilationData
{
    public Dictionary<string, ScriptBlock> Scripts { get; } = new();
    public Dictionary<string, TextBlock> Texts { get; } = new();
    public Dictionary<string, (string filePath, int line, int col)> ScriptLocations { get; } = new();
    public Dictionary<string, (string filePath, int line, int col)> TextLocations { get; } = new();
    public List<InterfaceDeclaration> Interfaces { get; } = [];
    public List<ImplementationInfo> Implementations { get; } = [];
    public bool HasErrors { get; set; }

    public List<ValidationDiagnostic> MergeFrom(PlayscriptCompilationData source)
    {
        var diagnostics = new List<ValidationDiagnostic>();

        foreach (var kvp in source.Scripts)
        {
            if (Scripts.ContainsKey(kvp.Key))
            {
                var loc = ScriptLocations[kvp.Key];
                diagnostics.Add(new ValidationDiagnostic("SCPT004",
                    $"Duplicate script name \"{kvp.Key}\"",
                    loc.filePath, loc.line, loc.col, "script", kvp.Key));
            }
            else
            {
                ScriptLocations[kvp.Key] = source.ScriptLocations[kvp.Key];
                Scripts[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in source.Texts)
        {
            if (Texts.ContainsKey(kvp.Key))
            {
                var loc = TextLocations[kvp.Key];
                diagnostics.Add(new ValidationDiagnostic("SCPT004",
                    $"Duplicate text name \"{kvp.Key}\"",
                    loc.filePath, loc.line, loc.col, "text", kvp.Key));
            }
            else
            {
                TextLocations[kvp.Key] = source.TextLocations[kvp.Key];
                Texts[kvp.Key] = kvp.Value;
            }
        }

        Interfaces.AddRange(source.Interfaces);

        return diagnostics;
    }
}
