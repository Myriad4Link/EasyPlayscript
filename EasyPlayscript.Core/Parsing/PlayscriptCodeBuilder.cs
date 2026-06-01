using System.Collections.Generic;
using System.Threading;
using Antlr4.Runtime.Tree;

namespace EasyPlayscript.Parsing;

/// <summary>
/// ANTLR visitor that walks the Pass 2 content AST and builds a <see cref="ScriptBlock"/>
/// with proper page/paragraph/line structure.
/// </summary>
public class PlayscriptCodeBuilder(CancellationToken cancellationToken = default)
    : PlayscriptContentParserBaseVisitor<string>
{
    public ScriptBlock ContentResult { get; private set; }

    /// <summary>
    /// Builds a <see cref="ScriptBlock"/> from Pass 2 content AST.
    /// Used when processing raw content extracted by Pass 1.
    /// </summary>
    public void BuildFromContent(PlayscriptContentParser.ScriptContentContext context)
    {
        var block = new ScriptBlock();

        var pages = context?.page();
        if (pages == null)
        {
            ContentResult = block;
            return;
        }

        foreach (var pageCtx in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = new Page();

            var paragraphs = pageCtx?.paragraph();
            if (paragraphs == null) continue;

            foreach (var paragraphCtx in paragraphs)
            {
                var paragraph = new Paragraph();

                var lines = paragraphCtx?.line();
                if (lines == null) continue;

                foreach (var lineCtx in lines)
                {
                    var line = new Line();

                    var children = lineCtx?.children;
                    if (children != null)
                    {
                        foreach (var child in children)
                        {
                            switch (child)
                            {
                                case PlayscriptContentParser.ConsumerCallContext callCtx:
                                {
                                    var identifier = callCtx.IDENTIFIER();
                                    var stringLit = callCtx.STRING_LITERAL();
                                    if (identifier != null && stringLit != null)
                                    {
                                        var callIdentifier = identifier.GetText();
                                        var callArg = stringLit.GetText().Trim('"');
                                        line.Items.Add(new ConsumerCallItem(callIdentifier, callArg));
                                    }
                                    break;
                                }
                                case ITerminalNode { Symbol.Type: PlayscriptContentLexer.TEXT } terminal:
                                    line.Items.Add(new TextItem(terminal.GetText()));
                                    break;
                            }
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