using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class Page
{
    [Key(0)] public List<Paragraph> Paragraphs { get; set; } = new();
}