using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class Paragraph
{
    [Key(0)] public List<Line> Lines { get; set; } = [];
}