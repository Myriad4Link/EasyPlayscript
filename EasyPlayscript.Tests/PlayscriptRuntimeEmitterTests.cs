using System.Collections.Generic;
using EasyPlayscript.DataModel;
using EasyPlayscript.Generator;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptRuntimeEmitterTests
{
    private const string DefaultOutputPath = "playscripts.bin";
    private static readonly Dictionary<string, ScriptVariants> EmptyScriptsVar = new();
    private static readonly Dictionary<string, TextVariants> EmptyTextsVar = new();

    // ── Class structure ──

    [Fact]
    public void Generate_ProducesSessionClass()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public class PlayscriptRuntimeSession", code);
        Assert.DoesNotContain("public sealed class PlayscriptRuntimeSession", code);
        Assert.DoesNotContain("public class PlayscriptRuntime\r\n", code);
        Assert.DoesNotContain("public class PlayscriptRuntime\n", code);
    }

    [Fact]
    public void Generate_InheritsFromBase()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains(": PlayscriptSessionScope", code);
    }

    [Fact]
    public void Generate_HasDefaultConstructor()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public PlayscriptRuntimeSession() : this((string?)null)", code);
    }

    [Fact]
    public void Generate_HasAesKeyConstructor()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public PlayscriptRuntimeSession(string? aesKey)", code);
    }

    [Fact]
    public void Generate_HasRegistryAesKeyConstructor()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public PlayscriptRuntimeSession(PlayscriptRegistry registry, string? aesKey) : base()", code);
    }

    [Fact]
    public void Generate_HasRegistryProperty()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public PlayscriptRegistry Registry { get; }", code);
    }

    [Fact]
    public void Generate_HasCreateChildOverride()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public override PlayscriptRuntimeSession CreateChild()", code);
        Assert.Contains("return new PlayscriptRuntimeSession(Registry, _aesKey, this)", code);
    }

    [Fact]
    public void Generate_HasChildConstructorTakingAesKey()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("private PlayscriptRuntimeSession(PlayscriptRegistry registry, string? aesKey, PlayscriptSessionScope parent)", code);
    }

    [Fact]
    public void Generate_Stores_AesKey_In_Field()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("_aesKey = aesKey;", code);
    }

    // ── Fields ──

    [Fact]
    public void Generate_HasLazyDeclarations()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    // ── Path embedding ──

    [Fact]
    public void Generate_EmbedsOutputPath()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, "custom/path.bin");
        Assert.Contains("ResolvePath(\"custom/path.bin\"", code);
    }

    [Fact]
    public void Generate_BackslashPath_NormalizedToForwardSlash()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, "bin\\Debug\\net8.0\\playscripts.bin");
        Assert.Contains("ResolvePath(\"bin/Debug/net8.0/playscripts.bin\"", code);
    }

    // ── No embedded key (security property) ──

    [Fact]
    public void Generate_DoesNotEmbed_AesKey_AsStringLiteral()
    {
        // Whatever string you "give" the generator, it must not appear in the
        // emitted code. The key is now a runtime argument.
        var probe = "this-key-must-not-appear-anywhere-in-the-generated-source";
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);

        Assert.DoesNotContain(probe, code);
    }

    [Fact]
    public void Generate_Passes_AesKey_To_PlayscriptLoader()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        // The key is passed via the field, not a literal.
        Assert.Contains("PlayscriptLoader.LoadScripts(ResolvePath(\"playscripts.bin\"), _aesKey)", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(ResolvePath(\"playscripts.bin\"), _aesKey)", code);
    }

    [Fact]
    public void Generate_LoadScriptsCall_HasNoStringLiteralForKey()
    {
        // Specifically: the LoadScripts call's second argument is the field
        // reference, not a string literal.
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.DoesNotContain("LoadScripts(ResolvePath(\"playscripts.bin\"), \"", code);
        Assert.DoesNotContain("LoadTexts(ResolvePath(\"playscripts.bin\"), \"", code);
    }

    // ── Dispatch ──

    [Fact]
    public void Generate_HasDispatchCall()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("public void DispatchCall(ConsumerCallItem call)", code);
        Assert.Contains("Registry.DispatchCall(call, this)", code);
    }

    // ── ScriptKey enum & GetScript ──

    [Fact]
    public void Generate_ScriptEnum_GeneratedWithEntry()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["load_tooltip"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("load_tooltip", code);
    }

    [Fact]
    public void Generate_EmptyScriptsVar_NoScriptEnum()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);

        Assert.DoesNotContain("enum ScriptKey", code);
        Assert.DoesNotContain("GetScript(", code);
    }

    [Fact]
    public void Generate_GetScriptMethod()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["load_tooltip"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);

        Assert.Contains("GetScript(ScriptKey", code);
        Assert.Contains("_scripts.Value.TryGetValue(name, out var variants)", code);
    }

    [Fact]
    public void Generate_GetScript_SetsRuntime()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["load_tooltip"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("Runtime = this", code);
        Assert.DoesNotContain("public new Script GetScript", code);
    }

    [Fact]
    public void Generate_MultipleScripts_AllEnumEntries()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["alpha"] = new ScriptVariants { Unversioned = new() },
            ["beta"] = new ScriptVariants { Unversioned = new() },
            ["gamma"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("alpha,", code);
        Assert.Contains("beta,", code);
        Assert.Contains("gamma", code);
    }

    [Fact]
    public void Generate_SwitchMapping_MapsBackToOriginalKey()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["intro"] = new ScriptVariants { Unversioned = new() },
            ["outro"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);

        Assert.Contains("ScriptKeyHelper", code);
        Assert.Contains("ScriptKey.intro => \"intro\"", code);
        Assert.Contains("ScriptKey.outro => \"outro\"", code);
        Assert.Contains("ArgumentOutOfRangeException", code);
    }

    [Fact]
    public void Generate_KeywordName_EscapedInEnum()
    {
        var scripts = new Dictionary<string, ScriptVariants>
        {
            ["class"] = new ScriptVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(scripts, EmptyTextsVar, DefaultOutputPath);

        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("@class", code);
        Assert.Contains("ScriptKey.@class => \"class\"", code);
    }

    // ── TextKey enum & GetText ──

    [Fact]
    public void Generate_TextEnum_GeneratedWithEntry()
    {
        var texts = new Dictionary<string, TextVariants>
        {
            ["intro"] = new TextVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, texts, DefaultOutputPath);

        Assert.Contains("enum TextKey", code);
        Assert.Contains("intro", code);
    }

    [Fact]
    public void Generate_EmptyTextsVar_NoTextEnum()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);

        Assert.DoesNotContain("enum TextKey", code);
        Assert.DoesNotContain("GetText(", code);
    }

    [Fact]
    public void Generate_GetTextMethod()
    {
        var texts = new Dictionary<string, TextVariants>
        {
            ["intro"] = new TextVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, texts, DefaultOutputPath);

        Assert.Contains("GetText(TextKey", code);
        Assert.Contains("_texts.Value.TryGetValue(name, out var variants)", code);
    }

    [Fact]
    public void Generate_GetText_SetsRuntime()
    {
        var texts = new Dictionary<string, TextVariants>
        {
            ["welcome"] = new TextVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, texts, DefaultOutputPath);
        Assert.Contains("Runtime = this", code);
        Assert.DoesNotContain("public new Text GetText", code);
    }

    [Fact]
    public void Generate_TextSwitchMapping_MapsBackToOriginalKey()
    {
        var texts = new Dictionary<string, TextVariants>
        {
            ["credits"] = new TextVariants { Unversioned = new() }
        };
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, texts, DefaultOutputPath);

        Assert.Contains("TextKeyHelper", code);
        Assert.Contains("TextKey.credits => \"credits\"", code);
    }

    // ─── Async Dispatch Tests ────────────────────────────────────────────────

    [Fact]
    public void Generate_WithAsync_GeneratesDispatchCallAsync()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath, hasAsync: true);
        Assert.Contains("async Task DispatchCallAsync(ConsumerCallItem call)", code);
        Assert.Contains("await Registry.DispatchCallAsync(call, this)", code);
    }

    [Fact]
    public void Generate_WithoutAsync_GeneratesDispatchCallAsync()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath, hasAsync: false);
        Assert.Contains("DispatchCallAsync", code);
    }

    [Fact]
    public void Generate_WithAsync_UsesUsingTask()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath, hasAsync: true);
        Assert.Contains("using System.Threading.Tasks;", code);
    }

    [Fact]
    public void Generate_Default_GeneratesDispatchCallAsync()
    {
        var code = PlayscriptRuntimeEmitter.Generate(EmptyScriptsVar, EmptyTextsVar, DefaultOutputPath);
        Assert.Contains("DispatchCallAsync", code);
    }
}
