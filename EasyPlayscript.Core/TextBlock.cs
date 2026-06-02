using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class TextBlock
{
    [Key(0)]
    public List<LineItem> Items { get; set; } = new();
}
