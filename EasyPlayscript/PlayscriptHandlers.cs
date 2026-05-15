using EasyPlayscript.core.playscript.definition;

namespace EasyPlayscript;

public static class PlayscriptHandlers
{
    public static string Script(string name, PlayscriptParser.ScriptBlockContext block)
    {
        // TODO: Generate C# code for @script("name")[...] block
        return $"// TODO: Script(\"{name}\")";
    }

    public static string Text(string name, PlayscriptParser.ScriptBlockContext block)
    {
        // TODO: Generate C# code for @text("name")[...] block
        return $"// TODO: Text(\"{name}\")";
    }
}
