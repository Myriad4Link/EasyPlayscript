using System;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptExecutionContextTests
{
    private class DummyNode
    {
        public string Name { get; set; } = "test";
    }

    private class OtherNode { }

    [Fact]
    public void Bind_AndGet_ReturnsInstance()
    {
        var context = new PlayscriptExecutionContext();
        var node = new DummyNode();
        context.Bind(node);

        Assert.Same(node, context.Get<DummyNode>());
    }

    [Fact]
    public void Get_UnboundType_ReturnsNull()
    {
        var context = new PlayscriptExecutionContext();
        Assert.Null(context.Get<DummyNode>());
    }

    [Fact]
    public void Bind_Null_ThrowsArgumentNullException()
    {
        var context = new PlayscriptExecutionContext();
        Assert.Throws<ArgumentNullException>(() => context.Bind<DummyNode>(null));
    }

    [Fact]
    public void Bind_OverwritesPrevious()
    {
        var context = new PlayscriptExecutionContext();
        var first = new DummyNode { Name = "first" };
        var second = new DummyNode { Name = "second" };
        context.Bind(first);
        context.Bind(second);

        Assert.Same(second, context.Get<DummyNode>());
    }

    [Fact]
    public void Get_DifferentTypes_Independent()
    {
        var context = new PlayscriptExecutionContext();
        var dummy = new DummyNode();
        var other = new OtherNode();
        context.Bind(dummy);
        context.Bind(other);

        Assert.Same(dummy, context.Get<DummyNode>());
        Assert.Same(other, context.Get<OtherNode>());
    }
}
