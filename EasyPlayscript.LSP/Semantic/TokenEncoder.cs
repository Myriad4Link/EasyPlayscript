namespace EasyPlayscript.LSP.Semantic;

internal static class TokenEncoder
{
    public static int[] Encode(IReadOnlyList<TokenEntry> tokens)
    {
        if (tokens.Count == 0) return [];

        var data = new int[tokens.Count * 5];
        var prevLine = 0;
        var prevCol = 0;

        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            var deltaLine = t.Line - prevLine;
            var deltaStart = deltaLine == 0 ? t.Col - prevCol : t.Col;

            var offset = i * 5;
            data[offset] = deltaLine;
            data[offset + 1] = deltaStart;
            data[offset + 2] = t.Length;
            data[offset + 3] = t.TokenType;
            data[offset + 4] = t.TokenModifiers;

            prevLine = t.Line;
            prevCol = t.Col;
        }

        return data;
    }
}