using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
    public ScriptBlock ContentResult { get; private set; } = null!;
    public TextBlock TextResult { get; private set; } = null!;
    public List<PlayscriptError> Errors { get; } = [];

    public void Build(BlockType type, IParseTree tree)
    {
        if (type == BlockType.Script)
            BuildScriptFromContent((PlayscriptContentParser.ScriptContentContext)tree);
        else
            BuildTextFromContent((PlayscriptContentParser.TextContentContext)tree);
    }

    public void BuildScriptFromContent(PlayscriptContentParser.ScriptContentContext? context)
    {
        var block = new ScriptBlock();
        Page? page = null;
        Paragraph? paragraph = null;

        ProcessContent(context,
            onPage: _ =>
            {
                page = new Page();
                block.Pages.Add(page);
            },
            onParagraph: _ =>
            {
                paragraph = new Paragraph();
                // `paragraph` is always evaluated after `page` gets evaluated.
                // So `page` here is guaranteed non-null. 
                page!.Paragraphs.Add(paragraph);
            },
            onLine: lineCtx => paragraph!.Lines.Add(new Line { Items = ParseLineItems(lineCtx) }));

        ContentResult = block;
    }

    public void BuildTextFromContent(PlayscriptContentParser.TextContentContext? context)
    {
        var block = new TextBlock();
        if (context?.textParagraph() == null)
        {
            TextResult = block;
            return;
        }

        var firstParagraph = true;
        foreach (var paraCtx in context.textParagraph())
        {
            if (!firstParagraph) block.Lines.Add(new Line());
            firstParagraph = false;

            foreach (var lineCtx in paraCtx.textLine())
            {
                var items = ParseTextLineItems(lineCtx);
                if (items.Count > 0)
                    block.Lines.Add(new Line { Items = items });
            }
        }

        TextResult = block;
    }

    private List<LineItem> ParseLineItems(PlayscriptContentParser.LineContext? lineCtx)
    {
        var items = new List<LineItem>();
        if (lineCtx?.children == null) return items;

        foreach (var child in lineCtx.children)
        {
            switch (child)
            {
                case PlayscriptContentParser.ConsumerCallContext callCtx:
                    var call = ParseConsumerCall(callCtx);
                    if (call != null)
                        items.Add(call);
                    break;
                case ITerminalNode { Symbol.Type: PlayscriptContentLexer.TEXT } terminal:
                    items.Add(new TextItem(Unescape(terminal.GetText())));
                    break;
            }
        }

        return items;
    }

    private List<LineItem> ParseTextLineItems(PlayscriptContentParser.TextLineContext? lineCtx)
    {
        var items = new List<LineItem>();
        if (lineCtx?.children == null) return items;

        foreach (var child in lineCtx.children)
        {
            switch (child)
            {
                case PlayscriptContentParser.ConsumerCallContext callCtx:
                    var call = ParseConsumerCall(callCtx);
                    if (call != null)
                        items.Add(call);
                    break;
                case ITerminalNode { Symbol.Type: PlayscriptContentLexer.TEXT } terminal:
                    items.Add(new TextItem(Unescape(terminal.GetText())));
                    break;
                case ITerminalNode { Symbol.Type: PlayscriptContentLexer.SLASH } terminal:
                    items.Add(new TextItem(terminal.GetText()));
                    break;
            }
        }

        return items;
    }

    private static string Unescape(string text)
    {
        if (text.IndexOf('\\') < 0) return text;
        var sb = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                i++;
                switch (text[i])
                {
                    case '@': sb.Append('@'); break;
                    case '#': sb.Append('#'); break;
                    case '/': sb.Append('/'); break;
                    case '\\': sb.Append('\\'); break;
                    case 'n': sb.Append('\n'); break;
                    default:
                        sb.Append('\\');
                        sb.Append(text[i]);
                        break;
                }
            }
            else
                sb.Append(text[i]);
        }

        return sb.ToString();
    }

    private void ProcessContent(
        PlayscriptContentParser.ScriptContentContext? context,
        Action<PlayscriptContentParser.PageContext> onPage,
        Action<PlayscriptContentParser.ParagraphContext> onParagraph,
        Action<PlayscriptContentParser.LineContext> onLine)
    {
        var pages = context?.page();
        if (pages == null) return;

        foreach (var pageCtx in pages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onPage(pageCtx);

            foreach (var paragraphCtx in pageCtx.paragraph() ?? [])
            {
                onParagraph(paragraphCtx);

                foreach (var lineCtx in paragraphCtx.line() ?? []) onLine(lineCtx);
            }
        }
    }

    private ConsumerCallItem? ParseConsumerCall(PlayscriptContentParser.ConsumerCallContext callCtx)
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

    private ArgumentValue? ParseArgument(PlayscriptContentParser.ArgumentContext argCtx)
    {
        if (argCtx.STRING_LITERAL() != null)
            return new StringArgument(argCtx.STRING_LITERAL().GetText().Trim('"'));

        if (argCtx.INTEGER_LITERAL() != null)
        {
            var text = argCtx.INTEGER_LITERAL().GetText();
            if (int.TryParse(text, out var intValue))
            {
                return new IntArgument(intValue);
            }

            var symbol = argCtx.INTEGER_LITERAL().Symbol;
            Errors.Add(new PlayscriptError(
                symbol.Line,
                symbol.Column,
                $"Integer literal '{text}' is out of range for System.Int32 (expected value between -2147483648 and 2147483647).",
                isLexer: false));
            return null;
        }

        if (argCtx.FLOAT_LITERAL() != null)
        {
            var text = argCtx.FLOAT_LITERAL().GetText();
            if (double.TryParse(text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var doubleValue)
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