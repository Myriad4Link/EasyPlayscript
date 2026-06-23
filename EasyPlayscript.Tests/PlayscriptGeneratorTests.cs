using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using EasyPlayscript.Parsing;
using EasyPlayscript.Tests.Utils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace EasyPlayscript.Tests;

public class PlayscriptGeneratorTests
{
    private const string ScriptBlockExample = """
        interface transition(type: string) : void
        script load_tooltip[
        你好。
        这里是……？

        啊、您好！

        请问你是？

        @transition("fade_out")
        ]
        """;

    private const string TextBlockExample = """
        text intro_text[
        你好，欢迎来到这个世界。
        ]
        """;

    private const string TestOutputPath = "test-scripts.bin";
    private const string TestAesKey = "test-key-1234567";
    private const string DefaultClassName = "PlayscriptBase";

    private static string GenerateCode(params (string name, string content)[] files)
    {
        return GenerateCodeWithKey(TestAesKey, files);
    }

    private static string GenerateCodeWithKey(string aesKey, params (string name, string content)[] files)
    {
        return GenerateCodeWithConfig(TestOutputPath, aesKey, DefaultClassName, files);
    }

    private static string GenerateCodeWithClassName(string className, params (string name, string content)[] files)
    {
        return GenerateCodeWithConfig(TestOutputPath, TestAesKey, className, files);
    }

    private static string GenerateCodeWithConfig(
        string outputPath, string aesKey, string className,
        params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", outputPath),
            ("build_property.PlayscriptAesKey", aesKey),
            ("build_property.PlayscriptBaseClass", className));

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var generatedFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == $"{className}.g.cs");

        return generatedFile.GetText().ToString();
    }

    [Fact]
    public void ScriptBlock_GeneratesScriptEnum()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("GetScript(ScriptKey", code);
    }

    [Fact]
    public void TextBlock_GeneratesTextEnum()
    {
        var code = GenerateCode(("TextExample", TextBlockExample));
        Assert.Contains("enum TextKey", code);
        Assert.Contains("GetText(TextKey", code);
    }

    [Fact]
    public void GeneratedCode_TextBlock_ReferencesTextBlockType()
    {
        var code = GenerateCode(("TextExample", TextBlockExample));
        Assert.Contains("Dictionary<string, TextBlock>", code);
        Assert.DoesNotContain("Dictionary<string, ScriptBlock> _texts", code);
    }

    [Fact]
    public void GeneratedCode_ContainsAbstractClass()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("public abstract class PlayscriptBase", code);
        Assert.DoesNotContain("public static class Registry", code);
    }

    [Fact]
    public void GeneratedCode_UsesEasyPlayscript()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript;", code);
    }

    [Fact]
    public void GeneratedCode_UsesGeneratedNamespace()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript.Generated;", code);
    }

    [Fact]
    public void ScriptBlock_ContentIsPopulated()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_ReferencesPlayscriptLoader()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_HasConstructor()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("protected PlayscriptBase()", code);
        Assert.Contains("LoadScripts", code);
        Assert.Contains("LoadTexts", code);
    }

    [Fact]
    public void GeneratedCode_HasLazyDeclarations()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    [Fact]
    public void GeneratedCode_EmbedsOutputPath()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains($"PlayscriptLoader.LoadScripts(\"{TestOutputPath}\"", code);
        Assert.Contains($"PlayscriptLoader.LoadTexts(\"{TestOutputPath}\"", code);
    }

    [Fact]
    public void GeneratedCode_EmbedsAesKey()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains($"\"{TestAesKey}\"", code);
    }

    [Fact]
    public void GeneratedCode_EmptyAesKey_EmbedsEmptyString()
    {
        var code = GenerateCodeWithKey("", ("Example", ScriptBlockExample));
        Assert.Contains("protected PlayscriptBase()", code);
        Assert.DoesNotContain("dev-key-change-me", code);
        Assert.Contains("LoadScripts(\"test-scripts.bin\", \"\")", code);
    }

    private static string GenerateCodeWithNoConfig(params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty);

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var generatedFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == $"{DefaultClassName}.g.cs");

        return generatedFile.GetText().ToString();
    }

    private static ImmutableArray<Diagnostic> GenerateDiagnostics(params (string name, string content)[] files)
    {
        return GenerateDiagnosticsWithKey(TestAesKey, files);
    }

    private static ImmutableArray<Diagnostic> GenerateDiagnosticsWithKey(string aesKey, params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", TestOutputPath),
            ("build_property.PlayscriptAesKey", aesKey),
            ("build_property.PlayscriptBaseClass", DefaultClassName));

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        return diagnostics;
    }

    [Fact]
    public void DuplicateScriptName_SameFile_ReportsSCPT004()
    {
        const string content = """
                               script foo[
                               Hello
                               ]
                               script foo[
                               World
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("dup", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateTextName_SameFile_ReportsSCPT004()
    {
        const string content = """
                               text intro[
                               Hello
                               ]
                               text intro[
                               World
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("dup", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateScriptName_CrossFile_ReportsSCPT004()
    {
        var fileA = """
            script shared[
            From file A
            ]
            """;
        var fileB = """
            script shared[
            From file B
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void DuplicateTextName_CrossFile_ReportsSCPT004()
    {
        const string fileA = """
                             text shared[
                             From file A
                             ]
                             """;
        const string fileB = """
                             text shared[
                             From file B
                             ]
                             """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.Contains(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void NoDuplicate_NoSCPT004()
    {
        var fileA = """
            script alpha[
            Alpha
            ]
            """;
        var fileB = """
            script beta[
            Beta
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT004");
    }

    [Fact]
    public void InvalidContent_ReportsDiagnostic()
    {
        const string content = """
                               script test[
                               @invalid(unclosed
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("bad", content));
        Assert.Contains(diagnostics, d => d.Id is "SCPT002" or "SCPT003");
    }

    // ─── Step 4: Interface Collection ─────────────────────────────────────────

    [Fact]
    public void InterfaceDeclaration_NoConsumerCalls_NoError()
    {
        var content = """
            interface transition(type: string) : void
            script foo[
            Hello world
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void InterfaceDeclaration_CrossFile_CollectsAll()
    {
        var fileA = "interface transition(type: string) : void";
        var fileB = """
            script foo[
            @transition("fade_out")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT005");
    }

    // ─── Step 6: SCPT005 Undeclared Consumer Call ─────────────────────────────

    [Fact]
    public void UndeclaredConsumerCall_ReportsSCPT005()
    {
        var content = """
            script foo[
            @transition("fade_out")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT005");
    }

    [Fact]
    public void DeclaredConsumerCall_NoSCPT005()
    {
        var content = """
            interface transition(type: string) : void
            script foo[
            @transition("fade_out")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT005");
    }

    [Fact]
    public void DeclaredConsumerCall_CrossFile_NoSCPT005()
    {
        var fileA = "interface transition(type: string) : void";
        var fileB = """
            script foo[
            @transition("fade_out")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT005");
    }

    [Fact]
    public void UndeclaredConsumerCall_PreventsCodeEmission()
    {
        var content = """
            script foo[
            @undeclared("x")
            ]
            """;
        Assert.Throws<InvalidOperationException>(() => GenerateCode(("file", content)));
    }

    // ─── Step 7: SCPT006 Duplicate Interface Signature ───────────────────────

    [Fact]
    public void DuplicateInterfaceSignature_SameFile_ReportsSCPT006()
    {
        var content = """
            interface transition(type: string) : void
            interface transition(type: string) : void
            script foo[
            @transition("x")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT006");
    }

    [Fact]
    public void DuplicateInterfaceSignature_CrossFile_ReportsSCPT006()
    {
        var fileA = "interface transition(type: string) : void";
        var fileB = """
            interface transition(type: string) : void
            script foo[
            @transition("x")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("fileA", fileA), ("fileB", fileB));
        Assert.Contains(diagnostics, d => d.Id == "SCPT006");
    }

    [Fact]
    public void SameNameDifferentSignature_NoSCPT006()
    {
        var content = """
            interface transition(type: string) : void
            interface transition(type: string, duration: decimal) : void
            script foo[
            @transition("x")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT006");
    }

    [Fact]
    public void SameNameDifferentReturnType_IsDifferentSignature()
    {
        var content = """
            interface f() : void
            interface f() : string
            script foo[
            @f()
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT006");
    }

    // ─── Step 8: SCPT007/SCPT008 Argument Type & Count Checking ─────────────

    [Fact]
    public void ArgumentTypeMatch_NoError()
    {
        var content = """
            interface transition(type: string, duration: decimal) : void
            script foo[
            @transition("fade_out", 1.0)
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id is "SCPT007" or "SCPT008");
    }

    [Fact]
    public void ArgumentCountMismatch_ReportsSCPT008()
    {
        var content = """
            interface transition(type: string, duration: decimal) : void
            script foo[
            @transition("fade_out")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT008");
    }

    [Fact]
    public void ArgumentTypeMismatch_ReportsSCPT007()
    {
        var content = """
            interface transition(type: string, duration: decimal) : void
            script foo[
            @transition("fade_out", "not_a_number")
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == "SCPT007");
    }

    [Fact]
    public void IntToDecimalCoercion_NoError()
    {
        var content = """
            interface transition(type: string, duration: decimal) : void
            script foo[
            @transition("fade_out", 1)
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id is "SCPT007" or "SCPT008");
    }

    [Fact]
    public void ZeroArgumentCall_MatchesZeroParamInterface()
    {
        var content = """
            interface on_complete() : void
            script foo[
            @on_complete()
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id is "SCPT007" or "SCPT008");
    }

    [Fact]
    public void OverloadedInterface_ResolvesCorrectly()
    {
        var content = """
            interface play(sound: string) : void
            interface play(sound: string, volume: decimal) : void
            script foo[
            @play("bgm")
            @play("sfx", 0.5)
            ]
            """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id is "SCPT007" or "SCPT008");
    }

    // ─── Phase 7: TextBlock Consumer Call ────────────────────────────────────

    [Fact]
    public void Generator_TextBlock_HasConsumerCall()
    {
        const string content = """
                               interface get_name() : string
                               text intro[
                               @get_name()
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT005");
    }

    // ─── Phase 10: Script Block Verification ────────────────────────────────

    [Fact]
    public void Generator_ScriptBlock_PreservesConsumerCallStructure()
    {
        const string content = """
                               interface get_name() : string
                               script load_tooltip[
                               Hello @get_name().
                               ]
                               """;
        var code = GenerateCode(("file", content));
        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("GetScript(ScriptKey", code);
    }

    [Fact]
    public void Generator_ScriptBlock_MixedTextAndCalls()
    {
        const string content = """
                               interface transition(type: string) : void
                               script load_tooltip[
                               Hello @transition("fade_out") world
                               ]
                               """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.DoesNotContain(diagnostics, d => d.Id == "SCPT005");
        var code = GenerateCode(("file", content));
        Assert.Contains("enum ScriptKey", code);
    }

    // ─── Config Fallbacks ───────────────────────────────────────────────────

    [Fact]
    public void MissingOutputPath_DefaultsToPlayscriptsBin()
    {
        var code = GenerateCodeWithNoConfig(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader.LoadScripts(\"playscripts.bin\"", code);
        Assert.Contains("PlayscriptLoader.LoadTexts(\"playscripts.bin\"", code);
    }

    [Fact]
    public void MissingAesKey_DefaultsToEmptyString()
    {
        var code = GenerateCodeWithNoConfig(("Example", ScriptBlockExample));
        Assert.DoesNotContain("dev-key-change-me", code);
        Assert.Contains("LoadScripts(\"playscripts.bin\", \"\")", code);
    }

    // ─── Phase 4: PlayscriptBase Integration ────────────────────────────────

    [Fact]
    public void GeneratedCode_InterfaceBecomesAbstractMethod()
    {
        var content = """
            interface transition(type: string) : void
            script foo[
            @transition("fade_out")
            ]
            """;
        var code = GenerateCode(("file", content));
        Assert.Contains("public abstract void transition(string type);", code);
    }

    [Fact]
    public void GeneratedCode_HasDispatchCall()
    {
        var content = """
            interface transition(type: string) : void
            script foo[
            @transition("fade_out")
            ]
            """;
        var code = GenerateCode(("file", content));
        Assert.Contains("DispatchCall", code);
        Assert.Contains("case \"transition\":", code);
    }

    [Fact]
    public void CustomClassName_FromConfig()
    {
        var code = GenerateCodeWithClassName("GameLoader", ("Example", ScriptBlockExample));
        Assert.Contains("public abstract class GameLoader", code);
    }

    [Fact]
    public void DefaultClassName_IsPlayscriptBase()
    {
        var code = GenerateCode(("Example", ScriptBlockExample));
        Assert.Contains("public abstract class PlayscriptBase", code);
    }
}
