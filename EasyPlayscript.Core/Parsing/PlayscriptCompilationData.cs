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
}
