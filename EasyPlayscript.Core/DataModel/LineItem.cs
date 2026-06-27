using MessagePack;

namespace EasyPlayscript.DataModel;

[MessagePackObject]
[Union(0, typeof(TextItem))]
[Union(1, typeof(ConsumerCallItem))]
public abstract class LineItem
{
}