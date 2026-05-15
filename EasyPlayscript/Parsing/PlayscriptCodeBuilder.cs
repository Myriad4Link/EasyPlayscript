using System.Collections.Generic;
using System.Text;

namespace EasyPlayscript.Parsing;

/// <summary>
/// ANTLR visitor that walks the playscript AST and collects script/text blocks
/// into a <see cref="PlayscriptResult"/> for later merging into the final Registry.
/// </summary>
public class PlayscriptCodeBuilder : PlayscriptParserBaseVisitor<string>
{
    private readonly PlayscriptResult _result = new PlayscriptResult();

    public PlayscriptResult Result => _result;

    public override string VisitPlayscript(PlayscriptParser.PlayscriptContext context)
    {
        foreach (var statement in context.statement())
        {
            Visit(statement);
        }
        return string.Empty;
    }

    public override string VisitStatement(PlayscriptParser.StatementContext context)
    {
        if (context.scriptBlock() != null)
            return Visit(context.scriptBlock());
        if (context.externalCall() != null)
            return Visit(context.externalCall());
        return string.Empty;
    }

    public override string VisitScriptBlock(PlayscriptParser.ScriptBlockContext context)
    {
        var externalCall = context.externalCall();
        var identifier = externalCall.IDENTIFIER().GetText();
        var arg = externalCall.STRING_LITERAL().GetText();
        var cleanArg = arg.Trim('"');

        var block = new ScriptBlock();

        foreach (var content in context.scriptContent())
        {
            if (content.sentence() != null)
            {
                var sentence = content.sentence();
                var parts = sentence.sentencePart();
                var sb = new StringBuilder();
                foreach (var part in parts)
                {
                    foreach (var text in part.TEXT())
                    {
                        sb.Append(text.GetText());
                    }
                }
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
        switch (identifier)
        {
            case "script":
                target = _result.Scripts;
                break;
            case "text":
                target = _result.Texts;
                break;
            default:
                return string.Empty;
        }

        if (!target.TryGetValue(cleanArg, out var list))
        {
            list = new List<ScriptBlock>();
            target[cleanArg] = list;
        }
        list.Add(block);

        return string.Empty;
    }

    public override string VisitExternalCall(PlayscriptParser.ExternalCallContext context)
    {
        return string.Empty;
    }
}
