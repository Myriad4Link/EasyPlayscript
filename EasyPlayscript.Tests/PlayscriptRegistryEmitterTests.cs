using System.Collections.Generic;
using EasyPlayscript.Generator;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptRegistryEmitterTests
{
    private static readonly PlayscriptCompilationData EmptyData = new();

    [Fact]
    public void Generate_Empty_ProducesSealedClass()
    {
        var code = PlayscriptRegistryEmitter.Generate(EmptyData);
        Assert.Contains("public sealed class PlayscriptRegistry", code);
    }

    [Fact]
    public void Generate_Empty_HasDispatchCall()
    {
        var code = PlayscriptRegistryEmitter.Generate(EmptyData);
        Assert.Contains("internal void DispatchCall(ConsumerCallItem call, PlayscriptRuntimeSession session)", code);
    }

    [Fact]
    public void Generate_Empty_HasDefaultCase()
    {
        var code = PlayscriptRegistryEmitter.Generate(EmptyData);
        Assert.Contains("default:", code);
        Assert.Contains("throw new InvalidOperationException", code);
    }

    [Fact]
    public void Generate_WithImplementation_GeneratesDispatchCase()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.AudioSystem",
            MethodName = "play",
            ParameterTypeNames = new List<string> { "string", "double" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter>
            {
                new("sound", InterfaceType.String),
                new("volume", InterfaceType.Decimal)
            },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("case \"play\":", code);
        Assert.Contains("((StringArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((DoubleArgument)call.Arguments[1]).Value", code);
    }

    [Fact]
    public void Generate_VoidInterface_UsesSessionGet()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.System",
            MethodName = "do_thing",
            ParameterTypeNames = new List<string>(),
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("do_thing",
            new List<InterfaceParameter>(),
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("session.Get<global::Game.System>()", code);
        Assert.Contains("_system.do_thing()", code);
        Assert.DoesNotContain("call.Result", code);
        Assert.Contains("NullReferenceException", code);
    }

    [Fact]
    public void Generate_NonVoidInterface_StoresResult()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.System",
            MethodName = "get_name",
            ParameterTypeNames = new List<string>(),
            ReturnTypeName = "string"
        });
        data.Interfaces.Add(new InterfaceDeclaration("get_name",
            new List<InterfaceParameter>(),
            InterfaceType.String, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("call.Result = _system.get_name()", code);
        Assert.Contains("session.Get<global::Game.System>()", code);
        Assert.Contains("NullReferenceException", code);
    }

    [Fact]
    public void Generate_OverloadedInterface_WhenGuards()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.Audio",
            MethodName = "play",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.Audio",
            MethodName = "play",
            ParameterTypeNames = new List<string> { "string", "double" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter>
            {
                new("sound", InterfaceType.String)
            },
            InterfaceType.Void, 1, 0));
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter>
            {
                new("sound", InterfaceType.String),
                new("volume", InterfaceType.Decimal)
            },
            InterfaceType.Void, 2, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("when call.Arguments.Count == 1", code);
        Assert.Contains("when call.Arguments.Count == 2", code);
    }

    [Fact]
    public void Generate_Alias_UsesAliasForCase()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.Audio",
            MethodName = "PlayMusic",
            Alias = "play",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter>
            {
                new("sound", InterfaceType.String)
            },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("case \"play\":", code);
        Assert.Contains("PlayMusic(", code);
    }

    [Fact]
    public void Generate_BoolAndIntArgs_CorrectExtraction()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.Config",
            MethodName = "set",
            ParameterTypeNames = new List<string> { "bool", "int" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("set",
            new List<InterfaceParameter>
            {
                new("enabled", InterfaceType.Bool),
                new("count", InterfaceType.Int)
            },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("((BoolArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((IntArgument)call.Arguments[1]).Value", code);
    }

    [Fact]
    public void Dispatch_UsesSessionGet()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.SceneTransitioner",
            MethodName = "transition",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("transition",
            new List<InterfaceParameter> { new("type", InterfaceType.String) },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);

        Assert.Contains("session.Get<global::Game.SceneTransitioner>()", code);
        Assert.DoesNotContain("_globals", code);
        Assert.DoesNotContain("context.Get", code);
    }

    [Fact]
    public void DispatchCall_HasSessionParameter()
    {
        var code = PlayscriptRegistryEmitter.Generate(EmptyData);
        Assert.Contains("DispatchCall(ConsumerCallItem call, PlayscriptRuntimeSession session)", code);
    }

    [Fact]
    public void Generate_NoGlobalsDict()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.AudioSystem",
            MethodName = "play",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter> { new("s", InterfaceType.String) },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.DoesNotContain("_globals", code);
        Assert.DoesNotContain("RegisterGlobal", code);
        Assert.Contains("session.Get<global::Game.AudioSystem>()", code);
    }

    [Fact]
    public void MultipleImplementations_AllUseSessionGet()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.AudioSystem",
            MethodName = "play",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Implementations.Add(new ImplementationInfo
        {
            ClassName = "global::Game.UiSystem",
            MethodName = "transition",
            ParameterTypeNames = new List<string> { "string" },
            ReturnTypeName = "void"
        });
        data.Interfaces.Add(new InterfaceDeclaration("play",
            new List<InterfaceParameter> { new("s", InterfaceType.String) },
            InterfaceType.Void, 1, 0));
        data.Interfaces.Add(new InterfaceDeclaration("transition",
            new List<InterfaceParameter> { new("type", InterfaceType.String) },
            InterfaceType.Void, 1, 0));

        var code = PlayscriptRegistryEmitter.Generate(data);
        Assert.Contains("session.Get<global::Game.AudioSystem>()", code);
        Assert.Contains("session.Get<global::Game.UiSystem>()", code);
        Assert.DoesNotContain("_globals", code);
    }
}
