using System.Collections.Generic;
using EasyPlayscript.Generator;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptRuntimeEmitterTests
{
    private const string DefaultOutputPath = "playscripts.bin";
    private const string DefaultAesKey = "";
    private static readonly Dictionary<string, ScriptBlock> EmptyScripts = new();
    private static readonly Dictionary<string, TextBlock> EmptyTexts = new();

    // ── Class structure ──

    [Fact]
    public void Generate_ProducesSessionClass()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public class PlayscriptRuntimeSession", code);
        Assert.DoesNotContain("public sealed class PlayscriptRuntimeSession", code);
        Assert.DoesNotContain("public class PlayscriptRuntime\r\n", code);
        Assert.DoesNotContain("public class PlayscriptRuntime\n", code);
    }

    [Fact]
    public void Generate_InheritsFromBase()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains(": global::EasyPlayscript.PlayscriptSessionScope", code);
    }

    [Fact]
    public void Generate_HasDefaultConstructor()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public PlayscriptRuntimeSession() : this(new PlayscriptRegistry())", code);
    }

    [Fact]
    public void Generate_HasRegistryConstructor()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public PlayscriptRuntimeSession(PlayscriptRegistry registry) : base()", code);
    }

    [Fact]
    public void Generate_HasRegistryProperty()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public PlayscriptRegistry Registry { get; }", code);
    }

    [Fact]
    public void Generate_HasCreateChildOverride()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public override PlayscriptRuntimeSession CreateChild()", code);
        Assert.Contains("child.SetParent(this)", code);
    }

    // ── Fields ──

    [Fact]
    public void Generate_HasLazyDeclarations()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    // ── Path & key embedding ──

    [Fact]
    public void Generate_EmbedsOutputPath()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, "custom/path.bin", DefaultAesKey);
        Assert.Contains("ResolvePath(\"custom/path.bin\"", code);
    }

    [Fact]
    public void Generate_EmbedsAesKey()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, "my-secret-key");
        Assert.Contains("\"my-secret-key\"", code);
    }

    [Fact]
    public void Generate_BackslashPath_NormalizedToForwardSlash()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, "bin\\Debug\\net8.0\\playscripts.bin",
            DefaultAesKey);
        Assert.Contains("ResolvePath(\"bin/Debug/net8.0/playscripts.bin\"", code);
    }

    [Fact]
    public void Generate_EmptyAesKey_EmbedsEmptyString()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, "");
        Assert.Contains("ResolvePath(\"playscripts.bin\")", code);
        Assert.Contains("LoadScripts(ResolvePath(\"playscripts.bin\"), \"\")", code);
    }

    // ── Dispatch ──

    [Fact]
    public void Generate_HasDispatchCall()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("public void DispatchCall(ConsumerCallItem call)", code);
        Assert.Contains("Registry.DispatchCall(call, this)", code);
    }

    // ── ScriptKey enum & GetScript ──

    [Fact]
    public void Generate_ScriptEnum_GeneratedWithEntry()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("load_tooltip", code);
    }

    [Fact]
    public void Generate_EmptyScripts_NoScriptEnum()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.DoesNotContain("enum ScriptKey", code);
        Assert.DoesNotContain("GetScript(", code);
    }

    [Fact]
    public void Generate_GetScriptMethod()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["load_tooltip"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("GetScript(ScriptKey", code);
        Assert.Contains("_scripts.Value[ScriptKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_GetScript_SetsRuntime()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["intro"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("Runtime = this", code);
        Assert.DoesNotContain("public new Script GetScript", code);
    }

    [Fact]
    public void Generate_MultipleScripts_AllEnumEntries()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["alpha"] = new(),
            ["beta"] = new(),
            ["gamma"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

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
            ["intro"] = new(),
            ["outro"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("ScriptKeyToString", code);
        Assert.Contains("ScriptKey.intro => \"intro\"", code);
        Assert.Contains("ScriptKey.outro => \"outro\"", code);
        Assert.Contains("ArgumentOutOfRangeException", code);
    }

    [Fact]
    public void Generate_KeywordName_EscapedInEnum()
    {
        var scripts = new Dictionary<string, ScriptBlock>
        {
            ["class"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("@class", code);
        Assert.Contains("ScriptKey.@class => \"class\"", code);
    }

    // ── TextKey enum & GetText ──

    [Fact]
    public void Generate_TextEnum_GeneratedWithEntry()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("enum TextKey", code);
        Assert.Contains("intro", code);
    }

    [Fact]
    public void Generate_EmptyTexts_NoTextEnum()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, EmptyTexts, DefaultOutputPath, DefaultAesKey);

        Assert.DoesNotContain("enum TextKey", code);
        Assert.DoesNotContain("GetText(", code);
    }

    [Fact]
    public void Generate_GetTextMethod()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["intro"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("GetText(TextKey", code);
        Assert.Contains("_texts.Value[TextKeyToString(key)]", code);
    }

    [Fact]
    public void Generate_GetText_SetsRuntime()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["welcome"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);
        Assert.Contains("Runtime = this", code);
        Assert.DoesNotContain("public new Text GetText", code);
    }

    [Fact]
    public void Generate_TextSwitchMapping_MapsBackToOriginalKey()
    {
        var texts = new Dictionary<string, TextBlock>
        {
            ["credits"] = new()
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScripts, texts, DefaultOutputPath, DefaultAesKey);

        Assert.Contains("TextKeyToString", code);
        Assert.Contains("TextKey.credits => \"credits\"", code);
    }
}
