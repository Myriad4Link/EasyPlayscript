using System;
using System.Collections.Generic;
using System.Linq;
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
    public TextBlock TextResult { get; private set; }
    public List<PlayscriptError> Errors { get; } = [];

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
                                    var call = ParseConsumerCall(callCtx);
                                    if (call != null)
                                        line.Items.Add(call);
                                    break;
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

    public void BuildTextFromContent(PlayscriptContentParser.ScriptContentContext context)
    {
        var block = new TextBlock();
        if (context?.page() == null) { TextResult = block; return; }

        var firstPage = true;
        foreach (var pageCtx in context.page())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!firstPage) block.Items.Add(new TextItem("\n"));
            firstPage = false;

            var firstParagraph = true;
            foreach (var paragraphCtx in pageCtx.paragraph() ?? new PlayscriptContentParser.ParagraphContext[0])
            {
                if (!firstParagraph) block.Items.Add(new TextItem("\n"));
                firstParagraph = false;

                var firstLine = true;
                foreach (var lineCtx in paragraphCtx.line() ?? [])
                {
                    if (!firstLine) block.Items.Add(new TextItem("\n"));
                    firstLine = false;

                    foreach (var child in lineCtx.children ?? new IParseTree[0])
                    {
                        switch (child)
                        {
                            case PlayscriptContentParser.ConsumerCallContext callCtx:
                                var call = ParseConsumerCall(callCtx);
                                if (call != null)
                                    block.Items.Add(call);
                                break;
                            case ITerminalNode { Symbol.Type: PlayscriptContentLexer.TEXT } terminal:
                                block.Items.Add(new TextItem(terminal.GetText()));
                                break;
                        }
                    }
                }
            }
        }
        TextResult = block;
    }

    private ConsumerCallItem ParseConsumerCall(PlayscriptContentParser.ConsumerCallContext callCtx)
    {
        var identifier = callCtx.IDENTIFIER();
        if (identifier == null) return null;

        var callIdentifier = identifier.GetText();
        var args = new List<ArgumentValue>();

        foreach (var argCtx in callCtx.argument())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var arg = ParseArgument(argCtx);
            if (arg != null)
                args.Add(arg);
        }

        var startToken = callCtx.Start;
        return new ConsumerCallItem(callIdentifier, args)
        {
            Line = startToken.Line,
            Col = startToken.Column
        };
    }

    private ArgumentValue ParseArgument(PlayscriptContentParser.ArgumentContext argCtx)
    {
        if (argCtx.STRING_LITERAL() != null)
        {
            return new StringArgument(argCtx.STRING_LITERAL().GetText().Trim('"'));
        }

        if (argCtx.INTEGER_LITERAL() != null)
        {
            var text = argCtx.INTEGER_LITERAL().GetText();
            if (int.TryParse(text, out var intValue))
            {
                return new IntArgument(intValue);
            }
            else
            {
                var symbol = argCtx.INTEGER_LITERAL().Symbol;
                Errors.Add(new PlayscriptError(
                    symbol.Line,
                    symbol.Column,
                    $"Integer literal '{text}' is out of range for System.Int32 (expected value between -2147483648 and 2147483647).",
                    isLexer: false));
                return null;
            }
        }

        if (argCtx.FLOAT_LITERAL() != null)
        {
            var text = argCtx.FLOAT_LITERAL().GetText();
            if (double.TryParse(text, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var doubleValue)
                && !double.IsInfinity(doubleValue))
            {
                return new DoubleArgument(doubleValue);
            }

            var symbol = argCtx.FLOAT_LITERAL().Symbol;
            Errors.Add(new PlayscriptError(
                symbol.Line,
                symbol.Column,
                $"Float literal '{text}' is out of range for System.Double (expected value between ±1.7976931348623157E+308).",
                isLexer: false));
            return null;
        }

        if (argCtx.BOOLEAN_LITERAL() == null) return null;
        {
            var text = argCtx.BOOLEAN_LITERAL().GetText();
            return new BoolArgument(bool.Parse(text));
        }
    }
}