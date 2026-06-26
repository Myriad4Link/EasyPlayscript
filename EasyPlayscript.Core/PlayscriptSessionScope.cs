using System;
using System.Collections.Concurrent;

namespace EasyPlayscript;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class PlayscriptSessionScope
{
    private readonly ConcurrentDictionary<Type, object> _services = new();

    public PlayscriptSessionScope? Parent { get; private set; }

    public PlayscriptSessionScope()
    {
    }

    // ReSharper disable once MemberCanBePrivate.Global
    internal PlayscriptSessionScope(PlayscriptSessionScope? parent)
    {
        Parent = parent;
    }

    protected void SetParent(PlayscriptSessionScope parent)
    {
        Parent = parent;
    }

    public void Register<T>(T instance) where T : class
    {
        _services[typeof(T)] = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    public T? Get<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var svc))
            return (T)svc;

        return Parent?.Get<T>();
    }

    public virtual PlayscriptSessionScope CreateChild() => new(this);
}
