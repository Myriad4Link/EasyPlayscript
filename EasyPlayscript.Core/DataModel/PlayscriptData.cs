using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class PlayscriptData
{
    [Key(0)] public Dictionary<string, ScriptVariants> Scripts { get; set; } = new();

    [Key(1)] public Dictionary<string, TextVariants> Texts { get; set; } = new();
}