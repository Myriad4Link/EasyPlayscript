using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
public class TextItem(string text) : LineItem
{
    [Key(0)] public string Text { get; set; } = text;
}