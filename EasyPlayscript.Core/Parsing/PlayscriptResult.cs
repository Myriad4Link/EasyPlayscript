using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Intermediate result from parsing a single .scpt file.
/// Contains collected script and text blocks before merging into the final Registry.
/// </summary>
public class PlayscriptResult
{
    public Dictionary<string, ScriptBlock> Scripts { get; } = new();
    public Dictionary<string, ScriptBlock> Texts { get; } = new();
    public Dictionary<string, (int line, int col)> ScriptLocations { get; } = new();
    public Dictionary<string, (int line, int col)> TextLocations { get; } = new();
}
