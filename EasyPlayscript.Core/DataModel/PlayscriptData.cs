using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class PlayscriptData
{
    [Key(0)]
    public Dictionary<string, ScriptBlock> Scripts { get; set; } = new();

    [Key(1)]
    public Dictionary<string, ScriptBlock> Texts { get; set; } = new();
}
