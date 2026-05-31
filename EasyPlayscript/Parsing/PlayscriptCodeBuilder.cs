using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace EasyPlayscript.Parsing;

/// <summary>
/// ANTLR visitor that walks the playscript AST and collects script/text blocks
/// into a <see cref="PlayscriptResult"/> for later merging into the final Registry.
/// </summary>
public class PlayscriptCodeBuilder(CancellationToken cancellationToken = default) : PlayscriptParserBaseVisitor<string>
{
    public PlayscriptResult Result { get; } = new();
    public ScriptBlock ContentResult { get; private set; }
    public List<(string identifier, string name, int line, int col)> DuplicateErrors { get; } = new();

    public override string VisitPlayscript(PlayscriptParser.PlayscriptContext context)
    {
        foreach (var statement in context.statement())
        {
            cancellationToken.ThrowIfCancellationRequested();
            Visit(statement);
        }
        return string.Empty;
    }

    public override string VisitStatement(PlayscriptParser.StatementContext context)
    {
        var compilerCall = context.compilerCall();
        var identifier = compilerCall.IDENTIFIER().GetText();
        var stringLiteral = compilerCall.STRING_LITERAL().Symbol;
        var cleanArg = stringLiteral.Text.Trim('"');
        var line = stringLiteral.Line;
        var col = stringLiteral.Column;

        if (context.scriptBlock() != null)
            ProcessScriptBlock(context.scriptBlock(), identifier, cleanArg, line, col);

        return string.Empty;
    }

    public override string VisitCompilerCall(PlayscriptParser.CompilerCallContext context)
    {
        return string.Empty;
    }

    private void ProcessScriptBlock(PlayscriptParser.ScriptBlockContext context, string identifier, string cleanArg, int line, int col)
    {
        var block = new ScriptBlock();
        var page = new Page();
        var paragraph = new Paragraph();
        page.Paragraphs.Add(paragraph);

        foreach (var content in context.scriptContent())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (content.sentence() != null)
            {
                var sentence = content.sentence();
                var parts = sentence.sentencePart();
                var sb = new StringBuilder();
                foreach (var part in parts)
                foreach (var text in part.TEXT())
                    sb.Append(text.GetText());
                var lineObj = new Line();
                lineObj.Items.Add(new TextItem(sb.ToString()));
                paragraph.Lines.Add(lineObj);
            }
            else if (content.consumerCall() != null)
            {
                var callIdentifier = content.consumerCall().IDENTIFIER().GetText();
                var callArg = content.consumerCall().STRING_LITERAL().GetText();
                var cleanCallArg = callArg.Trim('"');
                var lineObj = new Line();
                lineObj.Items.Add(new ConsumerCallItem(callIdentifier, cleanCallArg));
                paragraph.Lines.Add(lineObj);
            }
        }

        block.Pages.Add(page);

        Dictionary<string, ScriptBlock> target;
        Dictionary<string, (int line, int col)> locations;
        switch (identifier)
        {
            case "script":
                target = Result.Scripts;
                locations = Result.ScriptLocations;
                break;
            case "text":
                target = Result.Texts;
                locations = Result.TextLocations;
                break;
            default:
                return;
        }

        if (target.ContainsKey(cleanArg))
        {
            DuplicateErrors.Add((identifier, cleanArg, line, col));
        }
        else
        {
            locations[cleanArg] = (line, col);
        }

        // Duplicate names are detected and reported as errors (SCPT004) above.
        // We still assign the block here intentionally — the generator skips
        // code emission on errors, so duplicates never reach generated code.
        target[cleanArg] = block;
    }

    /// <summary>
    /// Builds a <see cref="ScriptBlock"/> from Pass 2 content AST.
    /// Used when processing raw content extracted by Pass 1.
    /// </summary>
    public void BuildFromContent(PlayscriptContentParser.ScriptContentContext context)
    {
        var block = new ScriptBlock();

        foreach (var pageCtx in context.page())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = new Page();

            foreach (var paragraphCtx in pageCtx.paragraph())
            {
                var paragraph = new Paragraph();

                foreach (var lineCtx in paragraphCtx.line())
                {
                    var line = new Line();

                    // Iterate children in order to preserve TEXT and consumerCall interleaving
                    foreach (var child in lineCtx.children)
                    {
                        if (child is PlayscriptContentParser.ConsumerCallContext callCtx)
                        {
                            var callIdentifier = callCtx.IDENTIFIER().GetText();
                            var callArg = callCtx.STRING_LITERAL().GetText().Trim('"');
                            line.Items.Add(new ConsumerCallItem(callIdentifier, callArg));
                        }
                        else if (child is Antlr4.Runtime.Tree.ITerminalNode terminal
                                 && terminal.Symbol.Type == PlayscriptContentLexer.TEXT)
                        {
                            line.Items.Add(new TextItem(terminal.GetText()));
                        }
                    }

                    paragraph.Lines.Add(line);
                }

                page.Paragraphs.Add(paragraph);
            }

            block.Pages.Add(page);
        }

        ContentResult = block;
    }
}