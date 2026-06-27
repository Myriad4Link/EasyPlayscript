using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using EasyPlayscript.Generator;
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

    private const string ImplementationAttributeSource = """
                                                         namespace EasyPlayscript.Runtime
                                                         {
                                                             [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
                                                             public sealed class ImplementationAttribute : System.Attribute
                                                             {
                                                                 public string? Alias { get; }
                                                                 public ImplementationAttribute(string? alias = null) => Alias = alias;
                                                             }
                                                         }
                                                         """;

    private static string GenerateRegistryCode(params (string name, string content)[] files)
    {
        return GenerateCodeForKey("PlayscriptRegistry.g.cs", TestOutputPath, TestAesKey, files);
    }

    private static string GenerateRuntimeCode(params (string name, string content)[] files)
    {
        return GenerateCodeForKey("PlayscriptRuntime.g.cs", TestOutputPath, TestAesKey, files);
    }

    private static string GenerateRegistryCodeWithKey(string aesKey, params (string name, string content)[] files)
    {
        return GenerateCodeForKey("PlayscriptRegistry.g.cs", TestOutputPath, aesKey, files);
    }

    private static string GenerateRuntimeCodeWithKey(string aesKey, params (string name, string content)[] files)
    {
        return GenerateCodeForKey("PlayscriptRuntime.g.cs", TestOutputPath, aesKey, files);
    }

    private static string GenerateCodeForKey(
        string fileName, string outputPath, string aesKey,
        params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", outputPath),
            ("build_property.PlayscriptAesKey", aesKey));

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
            .Single(t => Path.GetFileName(t.FilePath) == fileName);

        return generatedFile.GetText().ToString();
    }

    private static string GenerateRegistryCodeWithSource(
        string sourceCode,
        params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", TestOutputPath),
            ("build_property.PlayscriptAesKey", TestAesKey));

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = files
            .Select(f => new TestAdditionalFile($"./{f.name}.scpt", f.content))
            .ToImmutableArray<AdditionalText>();

        driver = driver.AddAdditionalTexts(additionalFiles);

        var tree = CSharpSyntaxTree.ParseText(sourceCode);
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location)
        };
        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests), [tree], references);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out var diagnostics);

        var generatedFile = newCompilation.SyntaxTrees
            .SingleOrDefault(t => Path.GetFileName(t.FilePath) == "PlayscriptRegistry.g.cs");

        if (generatedFile == null)
        {
            var diagMessages = string.Join("\n", diagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"));
            throw new InvalidOperationException(
                $"PlayscriptRegistry.g.cs was not generated. Diagnostics:\n{diagMessages}");
        }

        return generatedFile.GetText().ToString();
    }

    private static ImmutableArray<Diagnostic> GenerateDiagnostics(params (string name, string content)[] files)
    {
        return GenerateDiagnosticsWithKey(TestAesKey, files);
    }

    private static ImmutableArray<Diagnostic> GenerateDiagnosticsWithKey(string aesKey,
        params (string name, string content)[] files)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ("build_property.PlayscriptOutputPath", TestOutputPath),
            ("build_property.PlayscriptAesKey", aesKey));

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

    // ─── ScriptKey/TextKey Enums ────────────────────────────────────────────

    [Fact]
    public void ScriptBlock_GeneratesScriptEnum()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("enum ScriptKey", code);
        Assert.Contains("GetScript(ScriptKey", code);
    }

    [Fact]
    public void TextBlock_GeneratesTextEnum()
    {
        var code = GenerateRuntimeCode(("TextExample", TextBlockExample));
        Assert.Contains("enum TextKey", code);
        Assert.Contains("GetText(TextKey", code);
    }

    [Fact]
    public void GeneratedCode_ContextReferencesTextBlockType()
    {
        var code = GenerateRuntimeCode(("TextExample", TextBlockExample));
        Assert.Contains("Dictionary<string, TextBlock>", code);
        Assert.DoesNotContain("Dictionary<string, ScriptBlock> _texts", code);
    }

    // ─── PlayscriptRuntimeSession Structure ────────────────────────────────────

    [Fact]
    public void GeneratedCode_ContainsSessionClass()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("public class PlayscriptRuntimeSession", code);
        Assert.DoesNotContain("public sealed class PlayscriptRuntimeSession", code);
    }

    [Fact]
    public void GeneratedCode_UsesEasyPlayscript()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("using EasyPlayscript;", code);
    }

    [Fact]
    public void GeneratedCode_UsesGeneratedNamespace()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("namespace EasyPlayscript.Generated;", code);
    }

    [Fact]
    public void ScriptBlock_ContentIsPopulated()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_ReferencesPlayscriptLoader()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("PlayscriptLoader", code);
    }

    [Fact]
    public void GeneratedCode_HasConstructor()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("public PlayscriptRuntimeSession(PlayscriptRegistry registry)", code);
        Assert.Contains("LoadScripts", code);
        Assert.Contains("LoadTexts", code);
    }

    [Fact]
    public void GeneratedCode_HasLazyDeclarations()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("_scripts", code);
        Assert.Contains("_texts", code);
    }

    [Fact]
    public void GeneratedCode_HasRegistryProperty()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains("public PlayscriptRegistry Registry", code);
    }

    [Fact]
    public void GeneratedCode_EmbedsOutputPath()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains($"ResolvePath(\"{TestOutputPath}\"", code);
        Assert.Contains($"ResolvePath(\"{TestOutputPath}\"", code);
    }

    [Fact]
    public void GeneratedCode_EmbedsAesKey()
    {
        var code = GenerateRuntimeCode(("Example", ScriptBlockExample));
        Assert.Contains($"\"{TestAesKey}\"", code);
    }

    [Fact]
    public void GeneratedCode_EmptyAesKey_EmbedsEmptyString()
    {
        var code = GenerateRuntimeCodeWithKey("", ("Example", ScriptBlockExample));
        Assert.Contains("public PlayscriptRuntimeSession(PlayscriptRegistry", code);
        Assert.DoesNotContain("dev-key-change-me", code);
        Assert.Contains("ResolvePath(\"test-scripts.bin\")", code);
        Assert.Contains("LoadScripts(ResolvePath(\"test-scripts.bin\"), \"\")", code);
    }

    [Fact]
    public void MissingOutputPath_DefaultsToPlayscriptsBin()
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty);

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalFile("./Example.scpt", ScriptBlockExample));

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var sessionFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == "PlayscriptRuntime.g.cs");
        var code = sessionFile.GetText().ToString();

        Assert.Contains("ResolvePath(\"playscripts.bin\"", code);
        Assert.Contains("ResolvePath(\"playscripts.bin\"", code);
    }

    [Fact]
    public void MissingAesKey_DefaultsToEmptyString()
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(
            ImmutableDictionary<string, string>.Empty);

        var generator = new PlayscriptGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [generator.AsSourceGenerator()],
            optionsProvider: optionsProvider);

        var additionalFiles = ImmutableArray.Create<AdditionalText>(
            new TestAdditionalFile("./Example.scpt", ScriptBlockExample));

        driver = driver.AddAdditionalTexts(additionalFiles);

        var compilation = CSharpCompilation.Create(nameof(PlayscriptGeneratorTests));
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        var sessionFile = newCompilation.SyntaxTrees
            .Single(t => Path.GetFileName(t.FilePath) == "PlayscriptRuntime.g.cs");
        var code = sessionFile.GetText().ToString();

        Assert.DoesNotContain("dev-key-change-me", code);
        Assert.Contains("ResolvePath(\"playscripts.bin\")", code);
        Assert.Contains("LoadScripts(ResolvePath(\"playscripts.bin\"), \"\")", code);
    }

    // ─── PlayscriptRegistry Structure ──────────────────────────────────────

    [Fact]
    public void GeneratedCode_ContainsSealedRegistry()
    {
        var code = GenerateRegistryCode(("Example", ScriptBlockExample));
        Assert.Contains("public sealed class PlayscriptRegistry", code);
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
        var code = GenerateRegistryCode(("file", content));
        Assert.Contains("DispatchCall", code);
        Assert.Contains("PlayscriptRegistry", code);
    }

    // ─── Duplicate Detection ───────────────────────────────────────────────

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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateScriptName);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateScriptName);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateScriptName);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateScriptName);
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.DuplicateScriptName);
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
        Assert.Contains(diagnostics, d => d.Id is DiagnosticCodes.UnexpectedToken or DiagnosticCodes.MismatchedInput);
    }

    // ─── Interface Collection ─────────────────────────────────────────────

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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
    }

    // ─── SCPT005 Undeclared Consumer Call ─────────────────────────────────

    [Fact]
    public void UndeclaredConsumerCall_ReportsSCPT005()
    {
        var content = """
                      script foo[
                      @transition("fade_out")
                      ]
                      """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
    }

    [Fact]
    public void UndeclaredConsumerCall_PreventsCodeEmission()
    {
        var content = """
                      script foo[
                      @undeclared("x")
                      ]
                      """;
        var diagnostics = GenerateDiagnostics(("file", content));
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
        Assert.Contains(diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    // ─── SCPT006 Duplicate Interface Signature ───────────────────────────

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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateInterfaceSignature);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.DuplicateInterfaceSignature);
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.DuplicateInterfaceSignature);
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.DuplicateInterfaceSignature);
    }

    // ─── SCPT007/SCPT008 Argument Type & Count Checking ─────────────────

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
        Assert.DoesNotContain(diagnostics,
            d => d.Id is DiagnosticCodes.ArgumentTypeMismatch or DiagnosticCodes.ArgumentCountMismatch);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.ArgumentCountMismatch);
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
        Assert.Contains(diagnostics, d => d.Id == DiagnosticCodes.ArgumentTypeMismatch);
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
        Assert.DoesNotContain(diagnostics,
            d => d.Id is DiagnosticCodes.ArgumentTypeMismatch or DiagnosticCodes.ArgumentCountMismatch);
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
        Assert.DoesNotContain(diagnostics,
            d => d.Id is DiagnosticCodes.ArgumentTypeMismatch or DiagnosticCodes.ArgumentCountMismatch);
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
        Assert.DoesNotContain(diagnostics,
            d => d.Id is DiagnosticCodes.ArgumentTypeMismatch or DiagnosticCodes.ArgumentCountMismatch);
    }

    // ─── TextBlock Consumer Call ────────────────────────────────────────────

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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
    }

    // ─── Script Block Verification ─────────────────────────────────────────

    [Fact]
    public void Generator_ScriptBlock_PreservesConsumerCallStructure()
    {
        const string content = """
                               interface get_name() : string
                               script load_tooltip[
                               Hello @get_name().
                               ]
                               """;
        var code = GenerateRuntimeCode(("file", content));
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
        Assert.DoesNotContain(diagnostics, d => d.Id == DiagnosticCodes.UndeclaredConsumerCall);
        var code = GenerateRuntimeCode(("file", content));
        Assert.Contains("enum ScriptKey", code);
    }

    // ─── ImplementationScanner: Extract ────────────────────────────────────

    [Fact]
    public void ImplementationScanner_BasicVoidMethod_GeneratesRegisterAndDispatch()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Effects
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void fade() { }
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("fade", "interface fade() : void\nscript s[\n@fade()\n]"));
        Assert.Contains("session.Get<global::TestNs.Effects>()", code);
        Assert.Contains("case \"fade\":", code);
    }

    [Fact]
    public void ImplementationScanner_StringParam_TypeMapped()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Effects
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void fade(string type) { }
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("fade", "interface fade(type: string) : void\nscript s[\n@fade(\"out\")\n]"));
        Assert.Contains("((StringArgument)call.Arguments[0]).Value", code);
    }

    [Fact]
    public void ImplementationScanner_AllPrimitiveParams_TypeMapped()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Effects
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void config(string name, int count, double volume, bool enabled) { }
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("cfg",
                "interface config(name: string, count: int, volume: decimal, enabled: bool) : void\nscript s[\n@config(\"x\", 1, 1.0, true)\n]"));
        Assert.Contains("((StringArgument)call.Arguments[0]).Value", code);
        Assert.Contains("((IntArgument)call.Arguments[1]).Value", code);
        Assert.Contains("((DoubleArgument)call.Arguments[2]).Value", code);
        Assert.Contains("((BoolArgument)call.Arguments[3]).Value", code);
    }

    [Fact]
    public void ImplementationScanner_NonVoidReturn_StoresResult()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Effects
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public string get_name() => "test";
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("gn", "interface get_name() : string\nscript s[\n@get_name()\n]"));
        Assert.Contains("call.Result = ", code);
    }

    [Fact]
    public void ImplementationScanner_Alias_UsesAliasForCase()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Effects
                                                         {
                                                              [EasyPlayscript.Runtime.Implementation("fade")]
                                                             public void DoFade() { }
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("fade", "interface fade() : void\nscript s[\n@fade()\n]"));
        Assert.Contains("case \"fade\":", code);
        Assert.Contains("DoFade(", code);
    }

    [Fact]
    public void ImplementationScanner_NestedClass_FullyQualified()
    {
        var source = ImplementationAttributeSource + """
                                                     namespace TestNs
                                                     {
                                                         public class Outer
                                                         {
                                                             public class Inner
                                                             {
                                                                 [EasyPlayscript.Runtime.Implementation]
                                                                 public void fade() { }
                                                             }
                                                         }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("fade", "interface fade() : void\nscript s[\n@fade()\n]"));
        Assert.Contains("session.Get<global::TestNs.Inner>()", code);
    }

    [Fact]
    public void ImplementationScanner_GlobalNamespace_NoPrefix()
    {
        var source = ImplementationAttributeSource + """
                                                     public class GlobalEffects
                                                     {
                                                         [EasyPlayscript.Runtime.Implementation]
                                                         public void fade() { }
                                                     }
                                                     """;
        var code = GenerateRegistryCodeWithSource(source,
            ("fade", "interface fade() : void\nscript s[\n@fade()\n]"));
        Assert.Contains("session.Get<GlobalEffects>()", code);
        Assert.DoesNotContain("global::", code);
    }

    // ─── Unified Dispatch (session.Get) ──────────────────────────────────

    [Fact]
    public void Generator_UnifiedDispatch_SessionGet()
    {
        var scpt = """
                   interface transition(type: string) : void
                   script test_script[
                   @transition("fade")
                   ]
                   """;

        var source = ImplementationAttributeSource + """
                                                     namespace Game
                                                     {
                                                         public class Transitioner
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void transition(string type) { }
                                                         }
                                                     }
                                                     """;

        var code = GenerateRegistryCodeWithSource(source, ("test", scpt));

        Assert.Contains("session.Get<global::Game.Transitioner>()", code);
        Assert.DoesNotContain("_globals", code);
    }

    [Fact]
    public void Generator_MultipleImplementations_AllUseSessionGet()
    {
        var scpt = """
                   interface play(sound: string, volume: decimal) : void
                   interface transition(type: string) : void
                   script test_script[
                   @play("bgm", 0.8)
                   @transition("fade")
                   ]
                   """;

        var source = ImplementationAttributeSource + """
                                                     namespace Game
                                                     {
                                                         public class AudioSystem
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void play(string sound, double volume) { }
                                                         }
                                                         public class Transitioner
                                                         {
                                                             [EasyPlayscript.Runtime.Implementation]
                                                             public void transition(string type) { }
                                                         }
                                                     }
                                                     """;

        var code = GenerateRegistryCodeWithSource(source, ("test", scpt));

        Assert.Contains("session.Get<global::Game.AudioSystem>()", code);
        Assert.Contains("session.Get<global::Game.Transitioner>()", code);
        Assert.DoesNotContain("_globals", code);
        Assert.DoesNotContain("context.Get", code);
    }
}