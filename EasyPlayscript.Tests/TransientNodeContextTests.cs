using System;
using Xunit;

namespace EasyPlayscript.Tests;

public class TransientNodeContextTests
{
    [Fact]
    public void Bind_AndGet_ReturnsInstance()
    {
        var context = new TransientNodeContext();
        var node = new DummyNode();
        context.Bind(node);

        Assert.Same(node, context.Get<DummyNode>());
    }

    [Fact]
    public void Get_UnboundType_ReturnsNull()
    {
        var context = new TransientNodeContext();
        Assert.Null(context.Get<DummyNode>());
    }

    [Fact]
    public void Bind_Null_ThrowsArgumentNullException()
    {
        var context = new TransientNodeContext();
        Assert.Throws<ArgumentNullException>(() => context.Bind<DummyNode>(null!));
    }

    [Fact]
    public void Bind_OverwritesPrevious()
    {
        var context = new TransientNodeContext();
        var first = new DummyNode { Name = "first" };
        var second = new DummyNode { Name = "second" };
        context.Bind(first);
        context.Bind(second);

        Assert.Same(second, context.Get<DummyNode>());
    }

    [Fact]
    public void Get_DifferentTypes_Independent()
    {
        var context = new TransientNodeContext();
        var dummy = new DummyNode();
        var other = new OtherNode();
        context.Bind(dummy);
        context.Bind(other);

        Assert.Same(dummy, context.Get<DummyNode>());
        Assert.Same(other, context.Get<OtherNode>());
    }

    private class DummyNode
    {
        public string Name { get; set; } = "test";
    }

    private class OtherNode
    {
    }
}