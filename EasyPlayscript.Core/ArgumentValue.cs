using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
[Union(0, typeof(StringArgument))]
[Union(1, typeof(IntArgument))]
[Union(2, typeof(DoubleArgument))]
[Union(3, typeof(BoolArgument))]
public abstract class ArgumentValue { }

[MessagePackObject]
public class StringArgument(string value) : ArgumentValue
{
    [Key(0)]
    public string Value { get; set; } = value;
}

[MessagePackObject]
public class IntArgument(int value) : ArgumentValue
{
    [Key(0)]
    public int Value { get; set; } = value;
}

[MessagePackObject]
public class DoubleArgument(double value) : ArgumentValue
{
    [Key(0)]
    public double Value { get; set; } = value;
}

[MessagePackObject]
public class BoolArgument(bool value) : ArgumentValue
{
    [Key(0)]
    public bool Value { get; set; } = value;
}
