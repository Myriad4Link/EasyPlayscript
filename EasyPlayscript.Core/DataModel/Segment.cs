using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class Segment
{
    [Key(0)] public List<LineItem> Items { get; set; } = [];
}
