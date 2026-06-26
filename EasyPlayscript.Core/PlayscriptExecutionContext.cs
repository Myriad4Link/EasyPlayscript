using System;
using System.Collections.Generic;

namespace EasyPlayscript;

public class PlayscriptExecutionContext
{
    private readonly Dictionary<Type, object> _sceneNodes = new();

    public void Bind<TNode>(TNode instance) where TNode : class
    {
        _sceneNodes[typeof(TNode)] = instance ?? throw new ArgumentNullException(nameof(instance));
    }

    public TNode Get<TNode>() where TNode : class
    {
        return _sceneNodes.TryGetValue(typeof(TNode), out var node) ? (TNode)node : null;
    }
}
