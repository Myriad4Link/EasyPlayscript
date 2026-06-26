using System.Collections.Generic;
using EasyPlayscript.Generator;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptSessionEmitterTests
{
    private static readonly PlayscriptCompilationData EmptyData = new();
    private static readonly Dictionary<string, ScriptBlock> EmptyScripts = new();
    private static readonly Dictionary<string, TextBlock> EmptyTexts = new();

    [Fact]
    public void Generate_InheritsPlayscriptContext()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("class PlayscriptSession : PlayscriptContext", code);
    }

    [Fact]
    public void Generate_HasSceneContextProperty()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("public TransientNodeContext SceneContext { get; }", code);
    }

    [Fact]
    public void Generate_HasDefaultConstructor()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("public PlayscriptSession() : base(new PlayscriptRegistry())", code);
    }

    [Fact]
    public void Generate_HasRegisterMethod()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("public void Register<T>(T instance, ActionScope scope) where T : class", code);
    }

    [Fact]
    public void Generate_Register_RoutesGlobalService()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("case ActionScope.GlobalService:", code);
        Assert.Contains("Registry.RegisterGlobal(instance);", code);
    }

    [Fact]
    public void Generate_Register_RoutesTransientNode()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("case ActionScope.TransientNode:", code);
        Assert.Contains("SceneContext.Bind(instance);", code);
    }

    [Fact]
    public void Generate_HasDispatchCall()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("public void DispatchCall(ConsumerCallItem call)", code);
        Assert.Contains("Registry.DispatchCall(call, SceneContext);", code);
    }

    [Fact]
    public void Generate_WithScripts_HasGetScriptOverride()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["intro"] = new()
        };
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, scripts, EmptyTexts);
        Assert.Contains("public new Script GetScript(ScriptKey key)", code);
        Assert.Contains("script.Session = this;", code);
    }

    [Fact]
    public void Generate_WithTexts_HasGetTextOverride()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["welcome"] = new()
        };
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, texts);
        Assert.Contains("public new Text GetText(TextKey key)", code);
        Assert.Contains("text.Session = this;", code);
    }

    [Fact]
    public void Generate_EmptyScripts_NoGetScriptOverride()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.DoesNotContain("GetScript(", code);
    }

    [Fact]
    public void Generate_EmptyTexts_NoGetTextOverride()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.DoesNotContain("GetText(", code);
    }

    [Fact]
    public void Generate_Register_HasDefaultCase()
    {
        var code = PlayscriptSessionEmitter.Generate(EmptyData.Implementations, EmptyScripts, EmptyTexts);
        Assert.Contains("throw new ArgumentOutOfRangeException(nameof(scope))", code);
    }
}
