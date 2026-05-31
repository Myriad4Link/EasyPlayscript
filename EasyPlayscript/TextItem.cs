using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
public class TextItem : LineItem
{
    [Key(0)]
    public string Text { get; set; }

    public TextItem() { }

    public TextItem(string text)
    {
        Text = text;
    }
}
