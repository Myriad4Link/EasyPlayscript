using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptBaseEmitterTests
{
    private static readonly Dictionary<string, ScriptBlock> EmptyScripts = new Dictionary<string, ScriptBlock>();
    private static readonly Dictionary<string, TextBlock> EmptyTexts = new Dictionary<string, TextBlock>();
    private static readonly List<InterfaceDeclaration> EmptyInterfaces = new List<InterfaceDeclaration>();

    [Fact]
    public void Generate_Empty_ProducesAbstractClass()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public abstract class PlayscriptBase", code);
    }

    [Fact]
    public void Generate_HasConstructorWithParams()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "my-key", "PlayscriptBase");

        Assert.Contains("protected PlayscriptBase(string outputPath, string aesKey)", code);
        Assert.Contains("PlayscriptLoader.LoadScripts(outputPath, aesKey)", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(outputPath, aesKey)", code);
    }

    [Fact]
    public void Generate_ScriptProperty_SetsDispatch()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public Script LOAD_TOOLTIP", code);
        Assert.Contains("Dispatch = DispatchCall", code);
        Assert.Contains("_scripts.Value[\"load_tooltip\"]", code);
    }

    [Fact]
    public void Generate_TextProperty_SetsDispatch()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new TextBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, texts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public Text INTRO", code);
        Assert.Contains("Dispatch = DispatchCall", code);
        Assert.Contains("_texts.Value[\"intro\"]", code);
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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

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
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("((BoolArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((IntArgument)call.Arguments[1]).Value", code);
    }

    [Fact]
    public void Generate_CustomClassName_UsedInCode()
    {
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "", "MyGameBase");

        Assert.Contains("public abstract class MyGameBase", code);
        Assert.Contains("protected MyGameBase(", code);
    }

    [Fact]
    public void Generate_HyphenatedName_ConvertedToScreamingSnake()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load-tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public Script LOAD_TOOLTIP", code);
    }

    [Fact]
    public void Generate_SpaceInName_ConvertedToScreamingSnake()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            scripts, EmptyTexts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public Script LOAD_TOOLTIP", code);
    }

    [Fact]
    public void Generate_MixedCaseAndHyphen_UpperWithUnderscores()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro-text"] = new TextBlock()
        };
        var code = PlayscriptBaseEmitter.Generate(
            EmptyScripts, texts, EmptyInterfaces,
            "out.bin", "", "PlayscriptBase");

        Assert.Contains("public Text INTRO_TEXT", code);
    }
}
