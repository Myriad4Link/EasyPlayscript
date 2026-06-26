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

        Console.WriteLine("=== Scripts ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.ScriptKey>())
        {
            var script = session.GetScript(key);
            Console.WriteLine($"\n[{key}] ({script.Block.Pages.Count} page(s))");
            PrintScript(script);
        }

        Console.WriteLine("\n=== Texts ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntime.TextKey>())
        {
            Console.WriteLine($"\n[{key}]");
            Console.WriteLine(session.GetText(key).Render());
        }
    }

    private static void PrintScript(Script script)
    {
        for (var pi = 0; pi < script.Block.Pages.Count; pi++)
        {
            Console.WriteLine($"  Page {pi + 1}:");
            foreach (var paragraph in script.Block.Pages[pi].Paragraphs)
            foreach (var line in paragraph.Lines)
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var item in line.Items)
                    switch (item)
                    {
                        case TextItem text:
                            parts.Add(text.Text);
                            break;
                        case ConsumerCallItem call:
                            script.Runtime!.DispatchCall(call);
                            parts.Add(call.Result != null ? $"[{call.Result}]" : $"[@{call.Identifier}]");
                            break;
                    }

                Console.WriteLine($"    {string.Join("", parts)}");
            }
        }
    }
}
