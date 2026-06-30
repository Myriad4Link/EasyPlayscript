using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using EasyPlayscript.DataModel;
using EasyPlayscript.Generator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace EasyPlayscript.Tests;

public class ScriptRegistryTests
{
    private static GeneratorDriverRunResult RunGenerator()
    {
        var generator = new ScriptRegistry();
        var driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(nameof(ScriptRegistryTests),
            [CSharpSyntaxTree.ParseText("")],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        return driver.RunGenerators(compilation).GetRunResult();
    }

    [Fact]
    public void GeneratesScript_WithClassAndBlock()
    {
        var runResult = RunGenerator();

        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        var scriptText = scriptFile.GetText().ToString();

        Assert.Contains("public class Script", scriptText);
        Assert.Contains("public ScriptBlock Block { get; set; }", scriptText);
        Assert.DoesNotContain("Dispatch =", scriptText);
    }

    [Fact]
    public void GeneratesText_WithClassAndBlock()
    {
        var runResult = RunGenerator();

        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public class Text", textText);
        Assert.Contains("public TextBlock Block { get; set; }", textText);
        Assert.DoesNotContain("Dispatch =", textText);
    }

    [Fact]
    public void ScriptBlock_HasPages()
    {
        var block = new ScriptBlock();
        Assert.NotNull(block.Pages);
        Assert.Empty(block.Pages);
    }

    [Fact]
    public void Page_HasParagraphs()
    {
        var page = new Page();
        Assert.NotNull(page.Paragraphs);
        Assert.Empty(page.Paragraphs);
    }

    [Fact]
    public void Paragraph_HasLines()
    {
        var paragraph = new Paragraph();
        Assert.NotNull(paragraph.Lines);
        Assert.Empty(paragraph.Lines);
    }

    [Fact]
    public void Line_HasSegments()
    {
        var line = new Line();
        Assert.NotNull(line.Segments);
        Assert.Empty(line.Segments);
    }

    [Fact]
    public void TextItem_StoresText()
    {
        var item = new TextItem("Hello world");
        Assert.Equal("Hello world", item.Text);
    }

    [Fact]
    public void ConsumerCallItem_StoresIdentifierAndArguments()
    {
        var item = new ConsumerCallItem("transition", new List<ArgumentValue> { new StringArgument("fade_out") });
        Assert.Equal("transition", item.Identifier);
        Assert.Single(item.Arguments);
        Assert.IsType<StringArgument>(item.Arguments[0]);
        Assert.Equal("fade_out", ((StringArgument)item.Arguments[0]).Value);
    }

    [Fact]
    public void ConsumerCallItem_Result_DefaultsToNull()
    {
        var item = new ConsumerCallItem("test", new List<ArgumentValue>());
        Assert.Null(item.Result);
    }

    [Fact]
    public void ConsumerCallItem_CanStoreResult()
    {
        var item = new ConsumerCallItem("get_name", new List<ArgumentValue>());
        item.Result = "Player";
        Assert.Equal("Player", item.Result);
    }

    [Fact]
    public void GeneratesText_WithRenderSessionMethod()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public string Render(PlayscriptRegistry registry, PlayscriptRuntimeSession session)", textText);
    }

    [Fact]
    public void GeneratesText_WithRenderOverload()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public string Render(PlayscriptRuntimeSession session)", textText);
    }

    [Fact]
    public void GeneratesScript_WithSessionProperty()
    {
        var runResult = RunGenerator();
        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        var scriptText = scriptFile.GetText().ToString();

        Assert.Contains("internal PlayscriptRuntimeSession? Runtime { get; set; }", scriptText);
    }

    [Fact]
    public void GeneratesScript_WithRunMethod()
    {
        var runResult = RunGenerator();
        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        var scriptText = scriptFile.GetText().ToString();

        Assert.Contains("public void Run()", scriptText);
        Assert.Contains("Runtime.DispatchCall(call);", scriptText);
    }

    [Fact]
    public void GeneratesText_WithSessionProperty()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("internal PlayscriptRuntimeSession? Runtime { get; set; }", textText);
    }

    [Fact]
    public void GeneratesText_WithParameterlessRender()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.Contains("public string Render()", textText);
        Assert.Contains("Render(Runtime.Registry, Runtime);", textText);
    }

    [Fact]
    public void GeneratesText_NoTransientNodeContext()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        var textText = textFile.GetText().ToString();

        Assert.DoesNotContain("TransientNodeContext", textText);
    }

    // ─── Script Navigation: Generated Code Assertions ──────────────────────────

    private static string GetScriptSource()
    {
        var runResult = RunGenerator();
        var scriptFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Script.g.cs"));
        return scriptFile.GetText().ToString();
    }

    private static string GetTextSource()
    {
        var runResult = RunGenerator();
        var textFile = runResult.GeneratedTrees.Single(t => t.FilePath.EndsWith("Text.g.cs"));
        return textFile.GetText().ToString();
    }

    [Fact]
    public void GeneratesScript_WithNavigatorDelegation()
    {
        var source = GetScriptSource();

        Assert.Contains("private ScriptNavigator? _navigator;", source);
        Assert.Contains("private ScriptNavigator Navigator => _navigator ??= new ScriptNavigator(Block);", source);
    }

    [Fact]
    public void GeneratesScript_WithPointerProperty()
    {
        var source = GetScriptSource();

        Assert.Contains("public ScriptPointer Pointer", source);
    }

    [Fact]
    public void GeneratesScript_WithJumpToMethod()
    {
        var source = GetScriptSource();

        Assert.Contains("public void JumpTo(ScriptPointer pointer)", source);
        Assert.Contains("Navigator.JumpTo(pointer)", source);
    }

    [Fact]
    public void GeneratesScript_WithResetMethod()
    {
        var source = GetScriptSource();

        Assert.Contains("public void Reset()", source);
        Assert.Contains("Navigator.Reset()", source);
    }

    [Fact]
    public void GeneratesScript_WithRenderNextLine()
    {
        var source = GetScriptSource();

        Assert.Contains("public LineRenderResult? RenderNextLine()", source);
    }

    [Fact]
    public void GeneratesScript_WithRenderNextParagraph()
    {
        var source = GetScriptSource();

        Assert.Contains("public ParagraphRenderResult? RenderNextParagraph()", source);
    }

    [Fact]
    public void GeneratesScript_WithRenderNextPage()
    {
        var source = GetScriptSource();

        Assert.Contains("public PageRenderResult? RenderNextPage()", source);
    }

    [Fact]
    public void GeneratesScript_WithIsLastProperties()
    {
        var source = GetScriptSource();

        Assert.Contains("public bool IsLastLineOfParagraph", source);
        Assert.Contains("public bool IsLastParagraphOfPage", source);
        Assert.Contains("public bool IsLastPage", source);
        Assert.Contains("public bool IsLastLineOfPage", source);
        Assert.Contains("public bool IsLastLineOfScript", source);
        Assert.Contains("public bool IsLastParagraphOfScript", source);
    }

    [Fact]
    public void GeneratesScript_WithRenderLineHelper()
    {
        var source = GetScriptSource();

        Assert.Contains("private string RenderLine(Line line)", source);
        Assert.Contains("Navigator.RenderNextLine(RenderLine)", source);
        Assert.Contains("Navigator.RenderNextParagraph(RenderLine)", source);
        Assert.Contains("Navigator.RenderNextPage(RenderLine)", source);
    }

    // ─── Script Navigation: Structural Parity with ScriptNavigator ────────────

    private static string GetScriptNavigatorSource(
        [CallerFilePath] string testFilePath = "")
    {
        var testDir = Path.GetDirectoryName(testFilePath)!;
        var solutionDir = Path.GetFullPath(Path.Combine(testDir, ".."));
        return File.ReadAllText(Path.Combine(solutionDir, "EasyPlayscript.Core", "Runtime", "ScriptNavigator.cs"));
    }

    private static HashSet<string> GetPublicMembers(string source, string className)
    {
        var tree = CSharpSyntaxTree.ParseText(source);
        var root = tree.GetRoot();
        var classDecl = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First(c => c.Identifier.Text == className);

        var members = new HashSet<string>();
        foreach (var member in classDecl.Members)
        {
            var modifiers = member.Modifiers.Select(m => m.Text).ToList();
            if (!modifiers.Contains("public")) continue;

            switch (member)
            {
                case MethodDeclarationSyntax method:
                    var paramList = string.Join(", ",
                        method.ParameterList.Parameters.Select(p => $"{p.Type} {p.Identifier}"));
                    members.Add($"method {method.Identifier.Text}({paramList})");
                    break;
                case PropertyDeclarationSyntax prop:
                    members.Add($"property {prop.Identifier.Text}");
                    break;
            }
        }
        return members;
    }

    [Fact]
    public void GeneratedScript_NavigationAPIMatchesScriptNavigator()
    {
        var scriptSource = GetScriptSource();
        var navigatorSource = GetScriptNavigatorSource();

        var scriptMembers = GetPublicMembers(scriptSource, "Script");
        var navigatorMembers = GetPublicMembers(navigatorSource, "ScriptNavigator");

        // These navigation members must exist in both classes (same name, same purpose).
        // ScriptNavigator.Render* takes a Func<Line,string> callback; Script wraps it internally.
        var sharedNavigation = new HashSet<string>
        {
            "method JumpTo(ScriptPointer pointer)",
            "method Reset()",
            "property Pointer",
            "property IsLastLineOfParagraph",
            "property IsLastParagraphOfPage",
            "property IsLastPage",
            "property IsLastLineOfPage",
            "property IsLastLineOfScript",
            "property IsLastParagraphOfScript",
        };

        var missing = sharedNavigation.Except(scriptMembers).ToList();
        Assert.True(missing.Count == 0,
            $"Generated Script is missing shared navigation members from ScriptNavigator:\n  {string.Join("\n  ", missing)}");

        // ScriptNavigator should also have all these shared members (sanity check).
        var missingFromNav = sharedNavigation.Except(navigatorMembers).ToList();
        Assert.True(missingFromNav.Count == 0,
            $"ScriptNavigator is missing shared navigation members:\n  {string.Join("\n  ", missingFromNav)}");
    }

    [Fact]
    public void GeneratedScript_HasExpectedNavigationMethods()
    {
        var source = GetScriptSource();

        Assert.Contains("public LineRenderResult? RenderNextLine()", source);
        Assert.Contains("public ParagraphRenderResult? RenderNextParagraph()", source);
        Assert.Contains("public PageRenderResult? RenderNextPage()", source);
        Assert.Contains("public void Run()", source);
        Assert.Contains("public void JumpTo(ScriptPointer pointer)", source);
        Assert.Contains("public void Reset()", source);
        Assert.Contains("public ScriptPointer Pointer", source);
    }

    // ─── Async Script/Text Methods ───────────────────────────────────────────

    [Fact]
    public void GeneratedScript_HasRenderNextLineAsync()
    {
        var source = GetScriptSource();
        Assert.Contains("public async Task<LineRenderResult?> RenderNextLineAsync()", source);
    }

    [Fact]
    public void GeneratedScript_HasRenderNextParagraphAsync()
    {
        var source = GetScriptSource();
        Assert.Contains("public async Task<ParagraphRenderResult?> RenderNextParagraphAsync()", source);
    }

    [Fact]
    public void GeneratedScript_HasRenderNextPageAsync()
    {
        var source = GetScriptSource();
        Assert.Contains("public async Task<PageRenderResult?> RenderNextPageAsync()", source);
    }

    [Fact]
    public void GeneratedScript_HasRunAsync()
    {
        var source = GetScriptSource();
        Assert.Contains("public async Task RunAsync()", source);
    }

    [Fact]
    public void GeneratedScript_HasRenderLineAsync()
    {
        var source = GetScriptSource();
        Assert.Contains("private async Task<string> RenderLineAsync(Line line)", source);
        Assert.Contains("await Runtime.DispatchCallAsync(call)", source);
    }

    [Fact]
    public void GeneratedScript_RenderLineStillSyncDispatch()
    {
        var source = GetScriptSource();
        Assert.Contains("private string RenderLine(Line line)", source);
        Assert.Contains("Runtime.DispatchCall(call);", source);
    }

    [Fact]
    public void GeneratedScript_UsesUsingTask()
    {
        var source = GetScriptSource();
        Assert.Contains("using System.Threading.Tasks;", source);
    }

    [Fact]
    public void GeneratedText_HasRenderAsync()
    {
        var source = GetTextSource();
        Assert.Contains("public async Task<string> RenderAsync(PlayscriptRegistry registry, PlayscriptRuntimeSession session)", source);
        Assert.Contains("public async Task<string> RenderAsync(PlayscriptRuntimeSession session)", source);
        Assert.Contains("public async Task<string> RenderAsync()", source);
    }

    [Fact]
    public void GeneratedText_UsesUsingTask()
    {
        var source = GetTextSource();
        Assert.Contains("using System.Threading.Tasks;", source);
    }
}
