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
        MergeBlocks(diagnostics, source.Scripts, Scripts, source.ScriptLocations, ScriptLocations, "script");
        MergeBlocks(diagnostics, source.Texts, Texts, source.TextLocations, TextLocations, "text");
        Interfaces.AddRange(source.Interfaces);
        return diagnostics;
    }

    private static void MergeBlocks<T>(
        List<ValidationDiagnostic> diagnostics,
        Dictionary<string, T> sourceBlocks,
        Dictionary<string, T> targetBlocks,
        Dictionary<string, (string filePath, int line, int col)> sourceLocations,
        Dictionary<string, (string filePath, int line, int col)> targetLocations,
        string label)
    {
        foreach (var kvp in sourceBlocks)
        {
            if (targetBlocks.ContainsKey(kvp.Key))
            {
                var loc = targetLocations[kvp.Key];
                diagnostics.Add(new ValidationDiagnostic("SCPT004",
                    $"Duplicate {label} name \"{kvp.Key}\"",
                    loc.filePath, loc.line, loc.col, label, kvp.Key));
            }
            else
            {
                targetLocations[kvp.Key] = sourceLocations[kvp.Key];
                targetBlocks[kvp.Key] = kvp.Value;
            }
        }
    }
}
