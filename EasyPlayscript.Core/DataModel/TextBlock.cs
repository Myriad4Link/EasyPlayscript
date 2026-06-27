using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class TextBlock
{
    [Key(0)] public List<Line> Lines { get; set; } = [];
}