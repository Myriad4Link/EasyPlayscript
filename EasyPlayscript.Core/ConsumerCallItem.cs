using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class ConsumerCallItem : LineItem
{
    [Key(0)]
    public string Identifier { get; set; }

    [Key(1)]
    public string Argument { get; set; }

    public ConsumerCallItem() { }

    public ConsumerCallItem(string identifier, string argument)
    {
        Identifier = identifier;
        Argument = argument;
    }
}
