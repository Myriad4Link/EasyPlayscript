using System;
using System.Collections.Generic;
using EasyPlayscript;
using EasyPlayscript.Generated;

namespace EasyPlayscript.Sample;

public class GameLoader : PlayscriptBase
{
    public override void transition(string type)
    {
        Console.WriteLine($"  [transition] type={type}");
    }

    public override void play(string sound, double volume)
    {
        Console.WriteLine($"  [play] sound={sound}, volume={volume}");
    }

    public override void on_complete()
    {
        Console.WriteLine($"  [on_complete] called");
    }

    public override string get_name()
    {
        Console.WriteLine($"  [get_name] returning '旅行者'");
        return "旅行者";
    }
}

public static class Program
{
    public static void Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var loader = new GameLoader();

        Console.WriteLine("=== Scripts ===");
        foreach (var key in Enum.GetValues<PlayscriptBase.ScriptKey>())
        {
            var script = loader.GetScript(key);
            Console.WriteLine($"\n[{key}] ({script.Block.Pages.Count} page(s))");
            PrintScript(script);
        }

        Console.WriteLine("\n=== Texts ===");
        foreach (var key in Enum.GetValues<PlayscriptBase.TextKey>())
        {
            var text = loader.GetText(key);
            Console.WriteLine($"\n[{key}]");
            PrintText(text);
        }
    }

    private static void PrintScript(Script script)
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
                                script.Dispatch?.Invoke(call);
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

    private static void PrintText(Text text)
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
                        text.Dispatch?.Invoke(call);
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
