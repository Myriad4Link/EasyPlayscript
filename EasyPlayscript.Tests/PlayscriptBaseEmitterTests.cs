using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptBaseEmitterTests
{
    private static readonly Dictionary<string, ScriptBlock> EmptyScripts = new Dictionary<string, ScriptBlock>();
    private static readonly Dictionary<string, TextBlock> EmptyTexts = new Dictionary<string, TextBlock>();
    private static readonly List<InterfaceDeclaration> EmptyInterfaces = new List<InterfaceDeclaration>();

    private const string DefaultOutputPath = "playscripts.bin";
    private const string DefaultAesKey = "";

    [Fact]
    public void Generate_Empty_ProducesAbstractClass()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("public abstract class PlayscriptBase", code);
    }

    [Fact]
    public void Generate_NoArgConstructor()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("protected PlayscriptBase()", code);
    }

    [Fact]
    public void Generate_HardcodesOutputPath()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            "custom/path.bin", DefaultAesKey, "PlayscriptBase");

        Assert.Contains("PlayscriptLoader.LoadScripts(\"custom/path.bin\"", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(\"custom/path.bin\"", code);
    }

    [Fact]
    public void Generate_HardcodesAesKey()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, "my-secret-key", "PlayscriptBase");

        Assert.Contains("PlayscriptLoader.LoadScripts(\"playscripts.bin\", \"my-secret-key\")", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(\"playscripts.bin\", \"my-secret-key\")", code);
    }

    [Fact]
    public void Generate_EmptyAesKey_HardcodesEmptyString()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, "", "PlayscriptBase");

        Assert.Contains("PlayscriptLoader.LoadScripts(\"playscripts.bin\", \"\")", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(\"playscripts.bin\", \"\")", code);
    }

    [Fact]
    public void Generate_BackslashPath_NormalizedToForwardSlash()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            "bin\\Debug\\net8.0\\playscripts.bin", DefaultAesKey, "PlayscriptBase");

        Assert.Contains("PlayscriptLoader.LoadScripts(\"bin/Debug/net8.0/playscripts.bin\"", code);
    }

    [Fact]
    public void Generate_ScriptEnum_GeneratedWithEntry()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("load_tooltip", code);
    }

    [Fact]
    public void Generate_TextEnum_GeneratedWithEntry()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new TextBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, texts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("enum TextKey", code);
        Assert.Contains("intro", code);
    }

    [Fact]
    public void Generate_GetScriptMethod_SetsDispatch()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("GetScript(ScriptKey", code);
        Assert.Contains("Dispatch = DispatchCall", code);
        Assert.Contains("_scripts.Value[ScriptKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_GetTextMethod_SetsDispatch()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new TextBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, texts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("GetText(TextKey", code);
        Assert.Contains("Dispatch = DispatchCall", code);
        Assert.Contains("_texts.Value[TextKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_EmptyScripts_NoScriptEnum()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.DoesNotContain("enum ScriptKey", code);
        Assert.DoesNotContain("GetScript(", code);
    }

    [Fact]
    public void Generate_EmptyTexts_NoTextEnum()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.DoesNotContain("enum TextKey", code);
        Assert.DoesNotContain("GetText(", code);
    }

    [Fact]
    public void Generate_MultipleScripts_AllEnumEntries()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["alpha"] = new ScriptBlock(),
            ["beta"] = new ScriptBlock(),
            ["gamma"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("alpha,", code);
        Assert.Contains("beta,", code);
        Assert.Contains("gamma", code);
    }

    [Fact]
    public void Generate_NoOldScreamingSnakeProperties()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.DoesNotContain("public Script LOAD_TOOLTIP", code);
    }

    [Fact]
    public void Generate_KeywordName_EscapedInEnum()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["class"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("@class", code);
        Assert.Contains("ScriptKey.@class => \"class\"", code);
    }

    [Fact]
    public void Generate_SwitchMapping_MapsBackToOriginalKey()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["intro"] = new ScriptBlock(),
            ["outro"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("ScriptKeyToString", code);
        Assert.Contains("ScriptKey.intro => \"intro\"", code);
        Assert.Contains("ScriptKey.outro => \"outro\"", code);
        Assert.Contains("ArgumentOutOfRangeException", code);
    }

    [Fact]
    public void Generate_TextSwitchMapping_MapsBackToOriginalKey()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["credits"] = new TextBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, texts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("TextKeyToString", code);
        Assert.Contains("TextKey.credits => \"credits\"", code);
    }

    [Fact]
    public void Generate_VoidInterface_GeneratesAbstractMethod()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("transition",
                new List<InterfaceParameter>
                {
                    new InterfaceParameter("type", InterfaceType.String)
                },
                InterfaceType.Void, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("public abstract void transition(string type);", code);
    }

    [Fact]
    public void Generate_NonVoidInterface_GeneratesAbstractMethod()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("get_name",
                new List<InterfaceParameter>(),
                InterfaceType.String, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("public abstract string get_name();", code);
    }

    [Fact]
    public void Generate_VoidInterface_GeneratesDispatchCase()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("transition",
                new List<InterfaceParameter>
                {
                    new InterfaceParameter("type", InterfaceType.String)
                },
                InterfaceType.Void, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("case \"transition\":", code);
        Assert.Contains("transition(", code);
        Assert.Contains("((StringArgument)call.Arguments[0]).Value", code);
    }

    [Fact]
    public void Generate_NonVoidInterface_GeneratesDispatchCaseWithResult()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("get_name",
                new List<InterfaceParameter>(),
                InterfaceType.String, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("case \"get_name\":", code);
        Assert.Contains("call.Result = get_name()", code);
    }

    [Fact]
    public void Generate_MultipleArgs_ExtractsTypedArgs()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("play",
                new List<InterfaceParameter>
                {
                    new InterfaceParameter("sound", InterfaceType.String),
                    new InterfaceParameter("volume", InterfaceType.Decimal)
                },
                InterfaceType.Void, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("((StringArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((DoubleArgument)call.Arguments[1]).Value", code);
    }

    [Fact]
    public void Generate_ZeroArgInterface_NoArgExtraction()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("on_complete",
                new List<InterfaceParameter>(),
                InterfaceType.Void, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("public abstract void on_complete();", code);
        Assert.Contains("on_complete()", code);
        Assert.DoesNotContain("call.Arguments[", code);
    }

    [Fact]
    public void Generate_BoolAndIntArgs_CorrectExtraction()
    {
        var interfaces = new List<InterfaceDeclaration>
        {
            new InterfaceDeclaration("set_config",
                new List<InterfaceParameter>
                {
                    new InterfaceParameter("enabled", InterfaceType.Bool),
                    new InterfaceParameter("count", InterfaceType.Int)
                },
                InterfaceType.Void, 1, 0)
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, interfaces,
            DefaultOutputPath, DefaultAesKey, "PlayscriptBase");

        Assert.Contains("((BoolArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((IntArgument)call.Arguments[1]).Value", code);
    }

    [Fact]
    public void Generate_CustomClassName_UsedInCode()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            DefaultOutputPath, DefaultAesKey, "MyGameBase");

        Assert.Contains("public abstract class MyGameBase", code);
        Assert.Contains("protected MyGameBase()", code);
    }

}
