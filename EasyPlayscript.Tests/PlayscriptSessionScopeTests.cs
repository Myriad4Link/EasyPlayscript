using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptSessionScopeTests
{
    private class Foo
    {
        public string Name { get; set; } = "foo";
    }

    private class Bar
    {
        public string Name { get; set; } = "bar";
    }

    private class Baz { }

    // ── Basic registration and resolution ──

    [Fact]
    public void Get_LocalRegistered_ReturnsInstance()
    {
        var session = new PlayscriptSessionScope();
        var foo = new Foo { Name = "local" };
        session.Register(foo);

        Assert.Same(foo, session.Get<Foo>());
    }

    [Fact]
    public void Get_Unregistered_ReturnsNull()
    {
        var session = new PlayscriptSessionScope();
        Assert.Null(session.Get<Foo>());
    }

    [Fact]
    public void Register_Null_ThrowsArgumentNullException()
    {
        var session = new PlayscriptSessionScope();
        Assert.Throws<ArgumentNullException>(() => session.Register<Foo>(null!));
    }

    [Fact]
    public void Register_OverwritesPrevious()
    {
        var session = new PlayscriptSessionScope();
        var first = new Foo { Name = "first" };
        var second = new Foo { Name = "second" };
        session.Register(first);
        session.Register(second);

        Assert.Same(second, session.Get<Foo>());
    }

    [Fact]
    public void Get_DifferentTypes_Independent()
    {
        var session = new PlayscriptSessionScope();
        var foo = new Foo();
        var bar = new Bar();
        session.Register(foo);
        session.Register(bar);

        Assert.Same(foo, session.Get<Foo>());
        Assert.Same(bar, session.Get<Bar>());
    }

    // ── Parent-child chain ──

    [Fact]
    public void Get_FromParent_ReturnsParentService()
    {
        var parent = new PlayscriptSessionScope();
        var foo = new Foo { Name = "parent" };
        parent.Register(foo);

        var child = parent.CreateChild();
        Assert.Same(foo, child.Get<Foo>());
    }

    [Fact]
    public void Get_ChildShadowsParent_ReturnsChildService()
    {
        var parent = new PlayscriptSessionScope();
        var parentFoo = new Foo { Name = "parent" };
        parent.Register(parentFoo);

        var child = parent.CreateChild();
        var childFoo = new Foo { Name = "child" };
        child.Register(childFoo);

        Assert.Same(childFoo, child.Get<Foo>());
    }

    [Fact]
    public void Get_UnregisteredInBoth_ReturnsNull()
    {
        var parent = new PlayscriptSessionScope();
        var child = parent.CreateChild();

        Assert.Null(child.Get<Foo>());
    }

    [Fact]
    public void Get_ParentHasBar_ChildHasFoo_OnlyResolvesOwn()
    {
        var parent = new PlayscriptSessionScope();
        parent.Register(new Bar());

        var child = parent.CreateChild();
        child.Register(new Foo());

        Assert.Same(child.Get<Foo>(), child.Get<Foo>());
        Assert.NotNull(child.Get<Bar>());
        Assert.Null(child.Get<Baz>());
    }

    [Fact]
    public void Get_GrandparentChain_WalksFullDepth()
    {
        var grandparent = new PlayscriptSessionScope();
        var foo = new Foo { Name = "grandparent" };
        grandparent.Register(foo);

        var parent = grandparent.CreateChild();
        var child = parent.CreateChild();

        Assert.Same(foo, child.Get<Foo>());
    }

    [Fact]
    public void Get_MiddleLevelShadowsGrandparent()
    {
        var grandparent = new PlayscriptSessionScope();
        grandparent.Register(new Foo { Name = "grandparent" });

        var parent = grandparent.CreateChild();
        var parentFoo = new Foo { Name = "parent" };
        parent.Register(parentFoo);

        var child = parent.CreateChild();

        Assert.Same(parentFoo, child.Get<Foo>());
    }

    // ── CreateChild ──

    [Fact]
    public void CreateChild_ReturnsSessionWithParent()
    {
        var parent = new PlayscriptSessionScope();
        var child = parent.CreateChild();

        Assert.Same(parent, child.Parent);
    }

    [Fact]
    public void CreateChild_ParentIsNull_ForRootSession()
    {
        var root = new PlayscriptSessionScope();
        Assert.Null(root.Parent);
    }

    [Fact]
    public void CreateChild_MultipleChildrenAreIndependent()
    {
        var parent = new PlayscriptSessionScope();
        parent.Register(new Foo { Name = "parent" });

        var child1 = parent.CreateChild();
        var child2 = parent.CreateChild();

        child1.Register(new Foo { Name = "child1" });

        Assert.Equal("child1", child1.Get<Foo>()!.Name);
        Assert.Equal("parent", child2.Get<Foo>()!.Name);
    }

    // ── Deep chain (unlimited depth) ──

    [Fact]
    public void Get_FiveLevelChain_ResolvesFromRoot()
    {
        var root = new PlayscriptSessionScope();
        var foo = new Foo { Name = "root" };
        root.Register(foo);

        var l1 = root.CreateChild();
        var l2 = l1.CreateChild();
        var l3 = l2.CreateChild();
        var l4 = l3.CreateChild();
        var l5 = l4.CreateChild();

        Assert.Same(foo, l5.Get<Foo>());
    }

    [Fact]
    public void Get_FiveLevelChain_MiddleOverride_ShadowsCorrectly()
    {
        var root = new PlayscriptSessionScope();
        root.Register(new Foo { Name = "root" });

        var l1 = root.CreateChild();
        var l2 = l1.CreateChild();
        var l3 = l2.CreateChild();
        l3.Register(new Foo { Name = "l3" });
        var l4 = l3.CreateChild();
        var l5 = l4.CreateChild();

        Assert.Equal("l3", l5.Get<Foo>()!.Name);
    }

    // ── Thread safety ──

    [Fact]
    public async Task ConcurrentRegisterAndGet_DoesNotThrow()
    {
        var session = new PlayscriptSessionScope();
        var tasks = new List<Task>();

        for (int i = 0; i < 100; i++)
        {
            var idx = i;
            tasks.Add(Task.Run(() =>
            {
                session.Register(new Foo { Name = $"t{idx}" });
                _ = session.Get<Foo>();
            }));
        }

        await Task.WhenAll(tasks);
        Assert.NotNull(session.Get<Foo>());
    }

    [Fact]
    public async Task ConcurrentCreateChild_AndResolve_ParentChainIntact()
    {
        var parent = new PlayscriptSessionScope();
        parent.Register(new Foo { Name = "parent" });

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var child = parent.CreateChild();
                Assert.Equal("parent", child.Get<Foo>()!.Name);
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ConcurrentDispatch_MultipleChildren_SharedParent()
    {
        var parent = new PlayscriptSessionScope();
        parent.Register(new Foo { Name = "shared" });

        var tasks = new List<Task>();
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var child = parent.CreateChild();
                child.Register(new Bar { Name = $"bar-{i}" });

                Assert.Equal("shared", child.Get<Foo>()!.Name);
                Assert.StartsWith("bar-", child.Get<Bar>()!.Name);
            }));
        }

        await Task.WhenAll(tasks);
    }
}
