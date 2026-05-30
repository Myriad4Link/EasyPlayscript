using System.Collections.Generic;

namespace EasyPlayscript;

/// <summary>
/// Built-in container holding the lines of content inside a <c>@script</c> or <c>@text</c> block.
/// </summary>
public class ScriptBlock
{
    public List<string> Content { get; } = [];
}
