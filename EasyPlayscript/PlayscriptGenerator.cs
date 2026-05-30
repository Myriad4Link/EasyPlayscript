using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EasyPlayscript.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace EasyPlayscript;

/// <summary>
/// Incremental source generator that reads all <c>.scpt</c> files, parses them via ANTLR,
/// and emits a single <c>Registry</c> class with static properties for each script/text.
/// </summary>
[Generator]
public class PlayscriptGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var scptProvider = context.AdditionalTextsProvider
            .Where(static f => f.Path.EndsWith(".scpt"))
            .Select(static (file, ct) =>
            {
                var text = file.GetText(ct);
                var content = text?.ToString() ?? string.Empty;
                var filePath = file.Path;

                ct.ThrowIfCancellationRequested();
                var (parser, parseErrors) = PlayscriptParserHelper.Parse(content);

                ct.ThrowIfCancellationRequested();
                var tree = parser.playscript();

                ct.ThrowIfCancellationRequested();
                var builder = new PlayscriptCodeBuilder(ct);
                builder.Visit(tree);

                var diagnostics = new List<Diagnostic>();

                foreach (var (line, col, msg, isLexer) in parseErrors)
                {
                    ct.ThrowIfCancellationRequested();
                    var descriptor = isLexer
                        ? PlayscriptDiagnostics.UnexpectedToken
                        : PlayscriptDiagnostics.MismatchedInput;
                    var location = MakeLocation(filePath, line, col);
                    diagnostics.Add(Diagnostic.Create(descriptor, location, msg));
                }

                foreach (var (identifier, name, dupLine, dupCol) in builder.DuplicateErrors)
                {
                    ct.ThrowIfCancellationRequested();
                    var location = MakeLocation(filePath, dupLine, dupCol);
                    diagnostics.Add(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName, location, identifier, name));
                }

                return (builder.Result, filePath, diagnostics);
            });

        var allFilesProvider = scptProvider.Collect();

        context.RegisterSourceOutput(allFilesProvider, static (spc, allResults) =>
        {
            var hasErrors = false;
            foreach (var diag in allResults.SelectMany(result => result.diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                if (diag.Severity == DiagnosticSeverity.Error)
                    hasErrors = true;
            }

            var merged = new PlayscriptResult();
            foreach (var result in allResults)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                foreach (var kvp in result.Result.Scripts)
                {
                    if (merged.Scripts.ContainsKey(kvp.Key))
                    {
                        var (dupLine, dupCol) = result.Result.ScriptLocations[kvp.Key];
                        var location = MakeLocation(result.filePath, dupLine, dupCol);
                        spc.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName, location, "script", kvp.Key));
                        hasErrors = true;
                    }
                    else
                    {
                        merged.ScriptLocations[kvp.Key] = result.Result.ScriptLocations[kvp.Key];
                    }
                    merged.Scripts[kvp.Key] = kvp.Value;
                }
                foreach (var kvp in result.Result.Texts)
                {
                    if (merged.Texts.ContainsKey(kvp.Key))
                    {
                        var (dupLine, dupCol) = result.Result.TextLocations[kvp.Key];
                        var location = MakeLocation(result.filePath, dupLine, dupCol);
                        spc.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName, location, "text", kvp.Key));
                        hasErrors = true;
                    }
                    else
                    {
                        merged.TextLocations[kvp.Key] = result.Result.TextLocations[kvp.Key];
                    }
                    merged.Texts[kvp.Key] = kvp.Value;
                }
            }

            if (hasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();
            var code = GenerateRegistryClass(merged);
            
            spc.CancellationToken.ThrowIfCancellationRequested();
            spc.AddSource("Registry.g.cs", SourceText.From(code, Encoding.UTF8));
        });
    }

    private static Location MakeLocation(string filePath, int line, int col)
    {
        var linePosition = new LinePosition(line - 1, col);
        return Location.Create(filePath, default, new LinePositionSpan(linePosition, linePosition));
    }

    private static string ToScreamingSnakeCase(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name)
        {
            if (c is ' ' or '-')
                sb.Append('_');
            else
                sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    private static string GenerateRegistryClass(PlayscriptResult result)
    {
        using var writer = new StringWriter();
        var indented = new IndentedTextWriter(writer);

        indented.WriteLine("// <auto-generated/>");
        indented.WriteLine();
        indented.WriteLine("using EasyPlayscript;");
        indented.WriteLine("using EasyPlayscript.Generated;");
        indented.WriteLine();
        indented.WriteLine("public static class Registry");
        indented.WriteLine("{");
        indented.Indent++;

        var sortedScripts = result.Scripts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedScripts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.Write($"public static Script {propName} {{ get; }} = ");
            WriteScriptInitializer(indented, "Script", kvp.Value);
            indented.WriteLine();
        }

        var sortedTexts = result.Texts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedTexts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.Write($"public static Text {propName} {{ get; }} = ");
            WriteScriptInitializer(indented, "Text", kvp.Value);
            indented.WriteLine();
        }

        indented.Indent--;
        indented.WriteLine("}");

        indented.Flush();
        return writer.ToString();
    }

    private static void WriteScriptInitializer(IndentedTextWriter indented, string typeName, ScriptBlock block)
    {
        indented.WriteLine($"new {typeName}");
        indented.WriteLine("{");
        indented.Indent++;
        indented.Write("Block = new ScriptBlock { Content = { ");
        for (int i = 0; i < block.Content.Count; i++)
        {
            if (i > 0) indented.Write(", ");
            indented.Write($"\"{block.Content[i]}\"");
        }
        indented.WriteLine(" } }");
        indented.Indent--;
        indented.WriteLine("}");
    }
}
