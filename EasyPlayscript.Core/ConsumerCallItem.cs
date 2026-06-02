using System.Collections.Generic;
using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class ConsumerCallItem : LineItem
{
    [Key(0)]
    public string Identifier { get; set; }

    [Key(1)]
    public List<ArgumentValue> Arguments { get; set; } = new List<ArgumentValue>();

    [IgnoreMember]
    public int Line { get; set; }

    [IgnoreMember]
    public int Col { get; set; }

    public ConsumerCallItem() { }

    public ConsumerCallItem(string identifier, List<ArgumentValue> arguments)
    {
        Identifier = identifier;
        Arguments = arguments ?? new List<ArgumentValue>();
    }
}
