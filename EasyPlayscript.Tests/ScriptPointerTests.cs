using Xunit;

namespace EasyPlayscript.Tests;

public class ScriptPointerTests
{
    [Fact]
    public void Constructor_SetsIndices()
    {
        var pointer = new ScriptPointer(1, 2, 3);

        Assert.Equal(1, pointer.PageIndex);
        Assert.Equal(2, pointer.ParagraphIndex);
        Assert.Equal(3, pointer.LineIndex);
    }

    [Fact]
    public void Default_IsZeroZeroZero()
    {
        var pointer = default(ScriptPointer);

        Assert.Equal(0, pointer.PageIndex);
        Assert.Equal(0, pointer.ParagraphIndex);
        Assert.Equal(0, pointer.LineIndex);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ScriptPointer(1, 2, 3);
        var b = new ScriptPointer(1, 2, 3);

        Assert.Equal(a, b);
        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new ScriptPointer(1, 2, 3);
        var b = new ScriptPointer(1, 2, 4);

        Assert.NotEqual(a, b);
        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void ToString_ReturnsReadableFormat()
    {
        var pointer = new ScriptPointer(1, 2, 3);

        Assert.Equal("(1, 2, 3)", pointer.ToString());
    }
}
