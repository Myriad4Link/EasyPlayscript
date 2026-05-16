using System.Collections.Generic;
using System.Text;

namespace EasyPlayscript.Parsing;

/// <summary>
/// ANTLR visitor that walks the playscript AST and collects script/text blocks
/// into a <see cref="PlayscriptResult"/> for later merging into the final Registry.
/// </summary>
public class PlayscriptCodeBuilder : PlayscriptParserBaseVisitor<string>
{
    public PlayscriptResult Result { get; } = new PlayscriptResult();
    public List<(int line, int col, string msg)> Errors { get; } = new List<(int, int, string)>();

    public override string VisitPlayscript(PlayscriptParser.PlayscriptContext context)
    {
        string? pendingIdentifier = null;
        string? pendingArg = null;

        foreach (var statement in context.statement())
        {
            if (statement.scriptBlock() != null)
            {
                if (pendingIdentifier != null)
                {
                    ProcessScriptBlock(statement.scriptBlock(), pendingIdentifier, pendingArg);
                    pendingIdentifier = null;
                    pendingArg = null;
                }
                else
                {
                    var lbracket = statement.scriptBlock().LBRACKET().Symbol;
                    Errors.Add((lbracket.Line, lbracket.Column, "Script block must follow an external call"));
                }
            }
            else if (statement.externalCall() != null)
            {
                pendingIdentifier = statement.externalCall().IDENTIFIER().GetText();
                pendingArg = statement.externalCall().STRING_LITERAL().GetText().Trim('"');
            }
        }

        return string.Empty;
    }

    public override string VisitExternalCall(PlayscriptParser.ExternalCallContext context)
    {
        return string.Empty;
    }

    private void ProcessScriptBlock(PlayscriptParser.ScriptBlockContext context, string identifier, string cleanArg)
    {
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
                target = Result.Scripts;
                break;
            case "text":
                target = Result.Texts;
                break;
            default:
                return;
        }

        if (!target.TryGetValue(cleanArg, out var list))
        {
            list = new List<ScriptBlock>();
            target[cleanArg] = list;
        }
        list.Add(block);
    }
}
