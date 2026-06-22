using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class TextBlock
{
    [Key(0)]
    public List<Line> Lines { get; set; } = [];
}
