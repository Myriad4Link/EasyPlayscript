using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class ScriptVariants
{
    [Key(0)] public ScriptBlock? Unversioned { get; set; }

    [Key(1)] public Dictionary<string, ScriptBlock> Numbered { get; set; } = new();
}
