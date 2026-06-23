using System;
using System.Collections.Generic;
using EasyPlayscript;
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
        Console.WriteLine($"  [on_complete] called");
    }

    [Implementation]
    public string get_name()
    {
        Console.WriteLine($"  [get_name] returning '旅行者'");
        return "旅行者";
    }
}

public class UiSystem
{
    [Implementation]
    public void transition(string type)
    {
        Console.WriteLine($"  [transition] type={type}");
    }
}

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var registry = new PlayscriptRegistry();
        registry.Register(new AudioSystem());
        registry.Register(new UiSystem());

        var context = new PlayscriptContext(registry);

        Console.WriteLine("=== Scripts ===");
        foreach (var key in Enum.GetValues<PlayscriptContext.ScriptKey>())
        {
            var script = context.GetScript(key);
            Console.WriteLine($"\n[{key}] ({script.Block.Pages.Count} page(s))");
            PrintScript(script, registry);
        }

        Console.WriteLine("\n=== Texts ===");
        foreach (var key in Enum.GetValues<PlayscriptContext.TextKey>())
        {
            var text = context.GetText(key);
            Console.WriteLine($"\n[{key}]");
            PrintText(text, registry);
        }
    }

    private static void PrintScript(Script script, PlayscriptRegistry registry)
    {
        for (var pi = 0; pi < script.Block.Pages.Count; pi++)
        {
            Console.WriteLine($"  Page {pi + 1}:");
            foreach (var paragraph in script.Block.Pages[pi].Paragraphs)
            {
                foreach (var line in paragraph.Lines)
                {
                    var parts = new List<string>();
                    foreach (var item in line.Items)
                    {
                        switch (item)
                        {
                            case TextItem text:
                                parts.Add(text.Text);
                                break;
                            case ConsumerCallItem call:
                                registry.DispatchCall(call);
                                if (call.Result != null)
                                    parts.Add($"[{call.Result}]");
                                else
                                    parts.Add($"[@{call.Identifier}]");
                                break;
                        }
                    }
                    Console.WriteLine($"    {string.Join("", parts)}");
                }
            }
        }
    }

    private static void PrintText(Text text, PlayscriptRegistry registry)
    {
        foreach (var line in text.Block.Lines)
        {
            if (line.Items.Count == 0)
            {
                Console.WriteLine("    (blank)");
                continue;
            }
            var parts = new List<string>();
            foreach (var item in line.Items)
            {
                switch (item)
                {
                    case TextItem textItem:
                        parts.Add(textItem.Text);
                        break;
                    case ConsumerCallItem call:
                        registry.DispatchCall(call);
                        if (call.Result != null)
                            parts.Add($"[{call.Result}]");
                        else
                            parts.Add($"[@{call.Identifier}]");
                        break;
                }
            }
            Console.WriteLine($"    {string.Join("", parts)}");
        }
    }
}
