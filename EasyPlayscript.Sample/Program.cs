using System;
using System.Text;
using EasyPlayscript.Generated;

namespace EasyPlayscript.Sample;

public class AudioSystem
{
    [Implementation]
    public void play(string sound, double volume)
    {
        Console.WriteLine($"  [play] sound={sound}, volume={volume}");
    }

    [Implementation]
    public void on_complete()
    {
        Console.WriteLine("  [on_complete] called");
    }

    [Implementation]
    public string get_name()
    {
        Console.WriteLine("  [get_name] returning '旅行者'");
        return "旅行者";
    }
}

public class UiSystem
{
    [Implementation(Scope = ActionScope.TransientNode)]
    public void transition(string type)
    {
        Console.WriteLine($"  [transition] type={type}");
    }
}

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        var session = new PlayscriptRuntime();
        session.Register(new AudioSystem(), ActionScope.GlobalService);
        session.Register(new UiSystem(), ActionScope.TransientNode);

        Console.WriteLine("=== Scripts (line-by-line) ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.ScriptKey>())
        {
            var script = session.GetScript(key);
            Console.WriteLine($"\n[{key}] ({script.Block.Pages.Count} page(s))");

            while (script.RenderNextLine() is { } line)
            {
                var at = script.Pointer;
                var tag = script.IsLastLineOfScript ? " [last]" : "";
                Console.WriteLine($"  ({at.PageIndex},{at.ParagraphIndex},{at.LineIndex}) {line}{tag}");
            }
        }

        Console.WriteLine("\n=== Scripts (page-by-page) ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.ScriptKey>())
        {
            var script = session.GetScript(key);
            Console.WriteLine($"\n[{key}]");

            while (script.RenderNextPage() is { } page)
                Console.WriteLine(page);
        }

        Console.WriteLine("\n=== Scripts (JumpTo demo) ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.ScriptKey>())
        {
            var script = session.GetScript(key);
            if (script.Block.Pages.Count < 2) continue;

            Console.WriteLine($"\n[{key}] jumping to page 2:");
            script.JumpTo(new ScriptPointer(1, 0, 0));
            while (script.RenderNextLine() is { } line)
                Console.WriteLine($"  {line}");

            Console.WriteLine("  Resetting...");
            script.Reset();
            Console.WriteLine($"  First line again: {script.RenderNextLine()}");
        }

        Console.WriteLine("\n=== Texts ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.TextKey>())
        {
            Console.WriteLine($"\n[{key}]");
            Console.WriteLine(session.GetText(key).Render());
        }
    }
}
