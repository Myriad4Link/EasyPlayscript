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
/// Incremental source generator that reads all <c>.scpt</c> files, parses them via two-pass ANTLR,
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
                var structureResults = PlayscriptStructureHelper.ParseStructureWithErrors(content);

                var diagnostics = new List<Diagnostic>();
                var blocks = new List<(string identifier, string name, ScriptBlock block, int line, int col)>();

                foreach (var (line, col, msg, isLexer) in structureResults.errors)
                {
                    ct.ThrowIfCancellationRequested();
                    var descriptor = isLexer
                        ? PlayscriptDiagnostics.UnexpectedToken
                        : PlayscriptDiagnostics.MismatchedInput;
                    var location = MakeLocation(filePath, line, col);
                    diagnostics.Add(Diagnostic.Create(descriptor, location, msg));
                }

                foreach (var (identifier, name, rawContent, line, col) in structureResults.results)
                {
                    ct.ThrowIfCancellationRequested();
                    if (rawContent == null) continue;

                    var trimmedContent = rawContent.Trim('\r', '\n');
                    var (parser, contentErrors) = PlayscriptContentHelper.Parse(trimmedContent);
                    var tree = parser.scriptContent();

                    foreach (var error in contentErrors)
                    {
                        ct.ThrowIfCancellationRequested();
                        var descriptor = error.IsLexer
                            ? PlayscriptDiagnostics.UnexpectedToken
                            : PlayscriptDiagnostics.MismatchedInput;
                        var location = MakeLocation(filePath, error.Line, error.Col);
                        diagnostics.Add(Diagnostic.Create(descriptor, location, error.Msg));
                    }

                    if (contentErrors.Count > 0) continue;

                    ct.ThrowIfCancellationRequested();
                    var builder = new PlayscriptCodeBuilder(ct);
                    builder.BuildFromContent(tree);

                    blocks.Add((identifier, name, builder.ContentResult, line, col));
                }

                return (blocks, filePath, diagnostics);
            });

        var allFilesProvider = scptProvider.Collect();
        var combinedProvider = context.AnalyzerConfigOptionsProvider.Combine(allFilesProvider);

        context.RegisterSourceOutput(combinedProvider, static (spc, combined) =>
        {
            var (configOptions, allResults) = combined;

            var hasErrors = false;
            foreach (var diag in allResults.SelectMany(result => result.diagnostics))
            {
                spc.CancellationToken.ThrowIfCancellationRequested();
                spc.ReportDiagnostic(diag);
                hasErrors = diag.Severity == DiagnosticSeverity.Error;
            }

            var mergedScripts = new Dictionary<string, ScriptBlock>();
            var mergedTexts = new Dictionary<string, ScriptBlock>();
            var scriptLocations = new Dictionary<string, (string filePath, int line, int col)>();
            var textLocations = new Dictionary<string, (string filePath, int line, int col)>();

            foreach (var result in allResults)
            {
                spc.CancellationToken.ThrowIfCancellationRequested();

                foreach (var (identifier, name, block, line, col) in result.blocks)
                {
                    switch (identifier)
                    {
                        case "script":
                        {
                            if (mergedScripts.ContainsKey(name))
                            {
                                var loc = scriptLocations[name];
                                var location = MakeLocation(loc.filePath, loc.line, loc.col);
                                spc.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName, location,
                                    "script", name));
                                hasErrors = true;
                            }
                            else
                                scriptLocations[name] = (result.filePath, line, col);

                            mergedScripts[name] = block;
                            break;
                        }
                        case "text":
                        {
                            if (mergedTexts.ContainsKey(name))
                            {
                                var loc = textLocations[name];
                                var location = MakeLocation(loc.filePath, loc.line, loc.col);
                                spc.ReportDiagnostic(Diagnostic.Create(PlayscriptDiagnostics.DuplicateScriptName, location,
                                    "text", name));
                                hasErrors = true;
                            }
                            else
                                textLocations[name] = (result.filePath, line, col);

                            mergedTexts[name] = block;
                            break;
                        }
                    }
                }
            }

            if (hasErrors)
                return;

            spc.CancellationToken.ThrowIfCancellationRequested();

            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptOutputPath", out var outputPath);
            configOptions.GlobalOptions.TryGetValue("build_property.PlayscriptAesKey", out var aesKey);

            outputPath = string.IsNullOrEmpty(outputPath) ? "playscripts.bin" : outputPath;
            aesKey = string.IsNullOrEmpty(aesKey) ? "dev-key-change-me" : aesKey;

            var code = GenerateRegistryClass(mergedScripts, mergedTexts, outputPath!, aesKey!);

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

    private static string GenerateRegistryClass(Dictionary<string, ScriptBlock> scripts, Dictionary<string, ScriptBlock> texts, string outputPath, string aesKey)
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

        indented.WriteLine($"private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, ScriptBlock>> _scripts =");
        indented.Indent++;
        indented.WriteLine($"new(() => PlayscriptLoader.LoadScripts(\"{outputPath}\", \"{aesKey}\"));");
        indented.Indent--;
        indented.WriteLine();
        indented.WriteLine($"private static readonly System.Lazy<System.Collections.Generic.Dictionary<string, ScriptBlock>> _texts =");
        indented.Indent++;
        indented.WriteLine($"new(() => PlayscriptLoader.LoadTexts(\"{outputPath}\", \"{aesKey}\"));");
        indented.Indent--;
        indented.WriteLine();

        var sortedScripts = scripts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedScripts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.WriteLine($"public static Script {propName} => new Script {{ Block = _scripts.Value[\"{kvp.Key}\"] }};");
        }

        var sortedTexts = texts.OrderBy(kvp => kvp.Key, System.StringComparer.Ordinal);
        foreach (var kvp in sortedTexts)
        {
            var propName = ToScreamingSnakeCase(kvp.Key);
            indented.WriteLine($"public static Text {propName} => new Text {{ Block = _texts.Value[\"{kvp.Key}\"] }};");
        }

        indented.Indent--;
        indented.WriteLine("}");

        indented.Flush();
        return writer.ToString();
    }
}
