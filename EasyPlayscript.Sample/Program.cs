using System;
using System.Text;
using System.Threading.Tasks;
using EasyPlayscript.Generated;
using EasyPlayscript.Runtime;

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
    [Implementation]
    public void transition(string type)
    {
        Console.WriteLine($"  [transition] type={type}");
    }
}

public class DataSystem
{
    [Implementation]
    public async Task<string> fetch_user_name(int user_id)
    {
        Console.WriteLine($"  [fetch_user_name] fetching user {user_id}...");
        await Task.Delay(500); // simulate async I/O
        Console.WriteLine($"  [fetch_user_name] done");
        return "旅行者";
    }

    [Implementation]
    public async Task log_event(string @event)
    {
        Console.WriteLine($"  [log_event] logging '{@event}'...");
        await Task.Delay(100); // simulate async I/O
        Console.WriteLine($"  [log_event] done");
    }
}

public static class Program
{
    public static async Task Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║      EasyPlayscript: Parent-Child Session Demo       ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");

        // ── Global session: shared services for the entire game ──
        var globalSession = new PlayscriptRuntimeSession();
        globalSession.Register(new AudioSystem());
        globalSession.Register(new UiSystem());
        globalSession.Register(new DataSystem());

        Console.WriteLine("\n=== 1. Global Session (base services) ===");
        RunAllScripts(globalSession);

        // ── Title Scene: inherits everything from global ──
        Console.WriteLine("\n=== 2. Title Scene (inherits all from global) ===");
        var titleScene = globalSession.CreateChild();
        RunAllScripts(titleScene);

        // ── Combat Scene: overrides the AudioSystem instance ──
        Console.WriteLine("\n=== 3. Combat Scene (overrides AudioSystem instance) ===");
        var combatScene = globalSession.CreateChild();
        combatScene.Register(new AudioSystem());
        RunAllScripts(combatScene);

        // ── Cutscene Scene: creates a fresh child, no overrides ──
        Console.WriteLine("\n=== 4. Cutscene Scene (pure inheritance, no overrides) ===");
        var cutsceneScene = globalSession.CreateChild();
        RunAllScripts(cutsceneScene);

        // ── Deep hierarchy: global → game → level → room ──
        Console.WriteLine("\n=== 5. Deep Hierarchy (global → game → level → room) ===");
        var gameSession = globalSession.CreateChild();
        var levelSession = gameSession.CreateChild();
        var roomSession = levelSession.CreateChild();
        Console.WriteLine("  Room session resolves AudioSystem from global (3 levels up):");
        var audio = roomSession.Get<AudioSystem>();
        Console.WriteLine($"  AudioSystem resolved: {audio != null}");
        audio?.play("ambient_wind", 0.3);

        // ── Service isolation: children don't affect parent ──
        Console.WriteLine("\n=== 6. Service Isolation ===");
        var parentAudio = globalSession.Get<AudioSystem>();
        var childAudio = combatScene.Get<AudioSystem>();
        Console.WriteLine($"  Parent AudioSystem: {parentAudio?.GetHashCode()}");
        Console.WriteLine($"  Child  AudioSystem: {childAudio?.GetHashCode()}");
        Console.WriteLine($"  Same instance? {ReferenceEquals(parentAudio, childAudio)}");
        Console.WriteLine("  Child override doesn't affect parent:");
        parentAudio?.play("menu_bgm", 0.5);

        // ── Multiple independent children ──
        Console.WriteLine("\n=== 7. Independent Children ===");
        var sceneA = globalSession.CreateChild();
        var sceneB = globalSession.CreateChild();
        sceneA.Register(new AudioSystem());
        var aAudio = sceneA.Get<AudioSystem>();
        var bAudio = sceneB.Get<AudioSystem>();
        Console.WriteLine($"  SceneA own instance?  {!ReferenceEquals(aAudio, parentAudio)}");
        Console.WriteLine($"  SceneB inherits?      {ReferenceEquals(bAudio, parentAudio)}");

        // ── Text rendering via session ──
        Console.WriteLine("\n=== 8. Texts (via global session) ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntimeSession.TextKey>())
        {
            Console.WriteLine($"\n  [{key}]");
            Console.WriteLine($"  {globalSession.GetText(key).Render()}");
        }

        // ── Navigation demo ──
        Console.WriteLine("\n=== 9. Script Navigation ===");
        foreach (var key in Enum.GetValues<PlayscriptRuntimeSession.ScriptKey>())
        {
            var script = globalSession.GetScript(key);
            if (script.Block.Pages.Count < 2) continue;

            Console.WriteLine($"\n  [{key}] jumping to page 2:");
            script.JumpTo(new ScriptPointer(1, 0, 0));
            while (script.RenderNextLine() is { } line)
                Console.WriteLine($"    {line.Text}");

            Console.WriteLine("    Resetting...");
            script.Reset();
            var first = script.RenderNextLine();
            Console.WriteLine($"    First line again: {first?.Text}");
        }

        // ── Async demo: async render (properly awaits async calls) ──
        Console.WriteLine("\n=== 10. Async Interfaces (async render — properly awaited) ===");
        if (Enum.TryParse<PlayscriptRuntimeSession.ScriptKey>("async_demo", out var asyncKey))
        {
            var script = globalSession.GetScript(asyncKey);

            Console.WriteLine($"\n  [async_demo] RenderNextLineAsync():");
            while (await script.RenderNextLineAsync() is { } line)
            {
                var at = line.Pointer;
                Console.WriteLine($"    ({at.PageIndex},{at.ParagraphIndex},{at.LineIndex}) {line.Text}");
            }
        }
    }

    private static void RunAllScripts(PlayscriptRuntimeSession session)
    {
        foreach (var key in Enum.GetValues<PlayscriptRuntimeSession.ScriptKey>())
        {
            if (key.ToString() == "async_demo") continue; // skip async demo in sync loop
            var script = session.GetScript(key);
            Console.WriteLine($"\n  [{key}] ({script.Block.Pages.Count} page(s))");

            while (script.RenderNextLine() is { } line)
            {
                var at = line.Pointer;
                var tag = line.IsLastLineOfScript ? " [last]" : "";
                Console.WriteLine($"    ({at.PageIndex},{at.ParagraphIndex},{at.LineIndex}) {line.Text}{tag}");
            }
        }
    }
}
