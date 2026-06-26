using System;
using System.Collections.Generic;
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

        var registry = new PlayscriptRegistry();
        registry.Register(new AudioSystem());

        var context = new PlayscriptContext(registry);
        var sceneContext = new TransientNodeContext();
        sceneContext.Bind(new UiSystem());

        Console.WriteLine("=== Scripts ===");
        foreach (var key in Enum.GetValues<PlayscriptContext.ScriptKey>())
        {
            var script = context.GetScript(key);
            Console.WriteLine($"\n[{key}] ({script.Block.Pages.Count} page(s))");
            PrintScript(script, registry, sceneContext);
        }

        Console.WriteLine("\n=== Texts ===");
        foreach (var key in Enum.GetValues<PlayscriptContext.TextKey>())
        {
            var text = context.GetText(key);
            Console.WriteLine($"\n[{key}]");
            PrintText(text, registry, sceneContext);
        }
    }

    private static void PrintScript(Script script, PlayscriptRegistry registry, TransientNodeContext sceneContext)
    {
        for (var pi = 0; pi < script.Block.Pages.Count; pi++)
        {
            Console.WriteLine($"  Page {pi + 1}:");
            foreach (var paragraph in script.Block.Pages[pi].Paragraphs)
            foreach (var line in paragraph.Lines)
            {
                var parts = new List<string>();
                foreach (var item in line.Items)
                    switch (item)
                    {
                        case TextItem text:
                            parts.Add(text.Text);
                            break;
                        case ConsumerCallItem call:
                            registry.DispatchCall(call, sceneContext);
                            parts.Add(call.Result != null ? $"[{call.Result}]" : $"[@{call.Identifier}]");
                            break;
                    }

                Console.WriteLine($"    {string.Join("", parts)}");
            }
        }
    }

    private static void PrintText(Text text, PlayscriptRegistry registry, TransientNodeContext sceneContext)
    {
        Console.WriteLine(text.Render(registry, sceneContext));
    }
}