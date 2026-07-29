using System.Linq;
using System.Text;
using EasyPlayscript.Parsing;

namespace EasyPlayscript.LSP.Services;

internal class PlayscriptFormatter
{
    public string Format(string text)
    {
        var (structureResult, _) = PlayscriptStructureHelper.ParseStructureWithErrors(text);
        var blocks = structureResult.Results
            .OrderByDescending(b => b.StartChar)
            .ToList();

        var result = text;
        foreach (var block in blocks)
            result = ReformatBlock(result, block);

        return result;
    }

    private static string ReformatBlock(string text, StructureResult block)
    {
        var baseIndent = GetLineIndent(text, block.StartChar);
        var keyword = block.Identifier == BlockType.Script ? "script" : "text";

        var sb = new StringBuilder();
        sb.Append(keyword);
        sb.Append(' ');
        sb.Append(block.Name);
        sb.Append('\n');
        sb.Append(baseIndent);
        sb.Append('[');

        if (block.RawContent != null)
        {
            var content = block.RawContent.TrimStart('\r', '\n').TrimEnd('\r', '\n');
            if (content.Length > 0)
            {
                sb.Append('\n');
                var normalized = content.Replace("\r\n", "\n");
                var lines = normalized.Split('\n');
                foreach (var line in lines)
                {
                    sb.Append(baseIndent);
                    sb.Append('\t');
                    sb.Append(line.TrimEnd());
                    sb.Append('\n');
                }
            }
        }

        sb.Append('\n');
        sb.Append(baseIndent);
        sb.Append(']');

        var replacement = sb.ToString();
        return text.Remove(block.StartChar, block.EndChar - block.StartChar)
            .Insert(block.StartChar, replacement);
    }

    private static string GetLineIndent(string text, int charIndex)
    {
        var lineStart = 0;
        for (var i = charIndex - 1; i >= 0; i--)
        {
            if (text[i] == '\n')
            {
                lineStart = i + 1;
                break;
            }
        }

        return text.Substring(lineStart, charIndex - lineStart);
    }
}
