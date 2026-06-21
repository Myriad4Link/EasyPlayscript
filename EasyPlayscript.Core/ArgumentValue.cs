using MessagePack;

namespace EasyPlayscript;

[MessagePackObject]
[Union(0, typeof(StringArgument))]
[Union(1, typeof(IntArgument))]
[Union(2, typeof(DoubleArgument))]
[Union(3, typeof(BoolArgument))]
public abstract class ArgumentValue { }

[MessagePackObject]
public class StringArgument : ArgumentValue
{
    [Key(0)]
    public string Value { get; set; } = null!;

    public StringArgument(string value)
    {
        Value = value;
    }
}

[MessagePackObject]
public class IntArgument : ArgumentValue
{
    [Key(0)]
    public int Value { get; set; }

    public IntArgument() { }

    public IntArgument(int value)
    {
        Value = value;
    }
}

[MessagePackObject]
public class DoubleArgument : ArgumentValue
{
    [Key(0)]
    public double Value { get; set; }

    public DoubleArgument() { }

    public DoubleArgument(double value)
    {
        Value = value;
    }
}

[MessagePackObject]
public class BoolArgument : ArgumentValue
{
    [Key(0)]
    public bool Value { get; set; }

    public BoolArgument() { }

    public BoolArgument(bool value)
    {
        Value = value;
    }
}
