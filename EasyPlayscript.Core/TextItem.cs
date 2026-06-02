using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class TextItem(string text) : LineItem
{
    [Key(0)]
    public string Text { get; set; } = text;
}
