using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

/// <summary>
/// Intermediate result from parsing a single .scpt file.
/// Contains collected script and text blocks before merging into the final Registry.
/// </summary>
public class PlayscriptResult
{
    public Dictionary<string, List<ScriptBlock>> Scripts { get; } = new Dictionary<string, List<ScriptBlock>>();
    public Dictionary<string, List<ScriptBlock>> Texts { get; } = new Dictionary<string, List<ScriptBlock>>();
}
