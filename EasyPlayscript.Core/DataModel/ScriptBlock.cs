using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class ScriptBlock
{
    [Key(0)] public List<Page> Pages { get; set; } = new();
}