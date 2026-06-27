using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class Line
{
    [Key(0)] public List<LineItem> Items { get; set; } = [];
}