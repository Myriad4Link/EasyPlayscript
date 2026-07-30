using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class TextVariants
{
    [Key(0)] public TextBlock? Unversioned { get; set; }

    [Key(1)] public Dictionary<string, TextBlock> Numbered { get; set; } = new();
}
