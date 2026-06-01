using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
[Union(0, typeof(TextItem))]
[Union(1, typeof(ConsumerCallItem))]
public abstract class LineItem { }
