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
        var externalCall = context.externalCall();
        var identifier = externalCall.IDENTIFIER().GetText();
        var stringLiteral = externalCall.STRING_LITERAL().Symbol;
        var cleanArg = stringLiteral.Text.Trim('"');
        var line = stringLiteral.Line;
        var col = stringLiteral.Column;

        if (context.scriptBlock() != null)
            ProcessScriptBlock(context.scriptBlock(), identifier, cleanArg, line, col);

        return string.Empty;
    }

    public override string VisitExternalCall(PlayscriptParser.ExternalCallContext context)
    {
        return string.Empty;
    }

    private void ProcessScriptBlock(PlayscriptParser.ScriptBlockContext context, string identifier, string cleanArg, int line, int col)
    {
        var block = new ScriptBlock();

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
                block.Content.Add(sb.ToString());
            }
            else if (content.internalCall() != null)
            {
                var callIdentifier = content.internalCall().IDENTIFIER().GetText();
                var callArg = content.internalCall().STRING_LITERAL().GetText();
                var cleanCallArg = callArg.Trim('"');
                block.Content.Add($"@{callIdentifier}(\"{cleanCallArg}\")");
            }
        }

        Dictionary<string, List<ScriptBlock>> target;
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

        if (!target.TryGetValue(cleanArg, out var list))
        {
            list = [];
            target[cleanArg] = list;
            locations[cleanArg] = (line, col);
        }
        else if (list.Count > 0)
        {
            DuplicateErrors.Add((identifier, cleanArg, line, col));
        }

        // Duplicate names are detected and reported as errors (SCPT004) above.
        // We still accumulate the block here intentionally — the generator skips
        // code emission on errors, so duplicates never reach generated code.
        list.Add(block);
    }
}