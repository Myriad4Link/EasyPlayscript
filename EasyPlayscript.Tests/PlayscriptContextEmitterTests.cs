using System.Collections.Generic;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptContextEmitterTests
{
    private static readonly Dictionary<string, ScriptBlock> EmptyScripts = new();
    private static readonly Dictionary<string, TextBlock> EmptyTexts = new();

    private const string DefaultOutputPath = "playscripts.bin";
    private const string DefaultAesKey = "";

    [Fact]
    public void Generate_Empty_ProducesSealedClass()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public sealed class PlayscriptContext", code);
    }

    [Fact]
    public void Generate_HasConstructor()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public PlayscriptContext(PlayscriptRegistry registry)", code);
    }

    [Fact]
    public void Generate_HasLazyDeclarations()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    [Fact]
    public void Generate_EmbedsOutputPath()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, "custom/path.bin", DefaultAesKey);
        Assert.Contains("PlayscriptLoader.LoadScripts(\"custom/path.bin\"", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(\"custom/path.bin\"", code);
    }

    [Fact]
    public void Generate_EmbedsAesKey()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, "my-secret-key");
        Assert.Contains("\"my-secret-key\"", code);
    }

    [Fact]
    public void Generate_BackslashPath_NormalizedToForwardSlash()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, "bin\\Debug\\net8.0\\playscripts.bin", DefaultAesKey);
        Assert.Contains("PlayscriptLoader.LoadScripts(\"bin/Debug/net8.0/playscripts.bin\"", code);
    }

    [Fact]
    public void Generate_ScriptEnum_GeneratedWithEntry()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptContextEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

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
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum TextKey", code);
        Assert.Contains("intro", code);
    }

    [Fact]
    public void Generate_GetScriptMethod()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new ScriptBlock()
        };
        var code = PlayscriptContextEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("GetScript(ScriptKey", code);
        Assert.Contains("_scripts.Value[ScriptKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_GetTextMethod()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new TextBlock()
        };
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("GetText(TextKey", code);
        Assert.Contains("_texts.Value[TextKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_EmptyScripts_NoScriptEnum()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.DoesNotContain("enum ScriptKey", code);
        Assert.DoesNotContain("GetScript(", code);
    }

    [Fact]
    public void Generate_EmptyTexts_NoTextEnum()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

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
        var code = PlayscriptContextEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("alpha,", code);
        Assert.Contains("beta,", code);
        Assert.Contains("gamma", code);
    }

    [Fact]
    public void Generate_SwitchMapping_MapsBackToOriginalKey()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["intro"] = new ScriptBlock(),
            ["outro"] = new ScriptBlock()
        };
        var code = PlayscriptContextEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

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
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("TextKeyToString", code);
        Assert.Contains("TextKey.credits => \"credits\"", code);
    }

    [Fact]
    public void Generate_KeywordName_EscapedInEnum()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["class"] = new ScriptBlock()
        };
        var code = PlayscriptContextEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("@class", code);
        Assert.Contains("ScriptKey.@class => \"class\"", code);
    }

    [Fact]
    public void Generate_EmptyAesKey_EmbedsEmptyString()
    {
        var code = PlayscriptContextEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, "");
        Assert.Contains("LoadScripts(\"playscripts.bin\", \"\")", code);
    }
}
