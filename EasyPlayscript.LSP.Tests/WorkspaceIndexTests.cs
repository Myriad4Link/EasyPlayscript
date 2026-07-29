using EasyPlayscript.LSP.Parsing;
using EasyPlayscript.LSP.Services;
using EasyPlayscript.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace EasyPlayscript.LSP.Tests;

public class WorkspaceIndexTests
{
    private static readonly DocumentUri UriA = DocumentUri.From("/a.scpt");
    private static readonly DocumentUri UriB = DocumentUri.From("/b.scpt");
    private static readonly DocumentUri UriC = DocumentUri.From("/c.scpt");

    private static void Register(WorkspaceIndex index, DocumentUri uri, string text)
    {
        var doc = PlayscriptDocumentParser.ParseIncremental(text, null, uri.ToString());
        Assert.NotNull(doc.CompilationData);
        index.Register(uri, doc.ValidationDiagnostics, doc.CompilationData!,
            text.GetHashCode(StringComparison.Ordinal));
    }

    // ── Single-file SCPT005 (undeclared consumer call) ─────────────────────

    [Fact]
    public void Register_UndeclaredCall_SurfacesInThatFile()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            script a[
            @unknown_call(42)
            ]
            """);

        var diags = index.GetAllDiagnostics(UriA);
        var scpt005 = diags.FirstOrDefault(d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
        Assert.Equal(DiagnosticCodes.UndeclaredConsumerCall, scpt005.Code);
        Assert.Equal(UriA.ToString(), scpt005.FilePath);
        Assert.Equal("unknown_call", scpt005.MessageArgs[0]);
    }

    [Fact]
    public void Register_DeclaredCall_ProducesNoScpt005()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface greet(name: string) : void
            script a[
            @greet("hi")
            ]
            """);

        var diags = index.GetAllDiagnostics(UriA);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
    }

    // ── Cross-file: undeclared call references interface in another file ──

    [Fact]
    public void CrossFile_InterfaceInA_CallInB_NoScpt005()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface greet(name: string) : void
            """);
        Register(index, UriB, """
            script a[
            @greet("hi")
            ]
            """);

        Assert.DoesNotContain(index.GetAllDiagnostics(UriA), d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
        Assert.DoesNotContain(index.GetAllDiagnostics(UriB), d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
    }

    [Fact]
    public void CrossFile_InterfaceRemoved_ReintroducesScpt005()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface greet(name: string) : void
            """);
        Register(index, UriB, """
            script a[
            @greet("hi")
            ]
            """);
        Assert.DoesNotContain(index.GetAllDiagnostics(UriB), d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);

        index.Remove(UriA);

        var diags = index.GetAllDiagnostics(UriB);
        var scpt005 = diags.SingleOrDefault(d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
        Assert.NotNull(scpt005);
        Assert.Equal(UriB.ToString(), scpt005!.FilePath);
    }

    // ── SCPT006 duplicate interface signature ─────────────────────────────

    [Fact]
    public void CrossFile_DuplicateInterfaceSignature_SurfacesInSecondSeenFile()
    {
        // InterfaceValidator.ValidateDuplicateSignatures attributes the SCPT006
        // to the second-seen interface declaration. This is the opposite of
        // SCPT004 (script/text duplicate), which MergeFrom attributes to the
        // first-seen file.
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface play(sound: string) : void
            """);
        Register(index, UriB, """
            interface play(sound: string) : void
            """);

        var diagsB = index.GetAllDiagnostics(UriB);
        var scpt006B = diagsB.FirstOrDefault(d => d.Code == DiagnosticCodes.DuplicateInterfaceSignature);
        Assert.Equal(DiagnosticCodes.DuplicateInterfaceSignature, scpt006B.Code);
        Assert.Equal(UriB.ToString(), scpt006B.FilePath);

        // A is the first-seen, so it doesn't get the SCPT006.
        Assert.DoesNotContain(index.GetAllDiagnostics(UriA),
            d => d.Code == DiagnosticCodes.DuplicateInterfaceSignature);
    }

    // ── SCPT004 duplicate script/text name ────────────────────────────────

    [Fact]
    public void CrossFile_DuplicateScriptName_SurfacesInFirstSeenFile()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            script opening[
            hi
            ]
            """);
        Register(index, UriB, """
            script opening[
            hello
            ]
            """);

        var diagsA = index.GetAllDiagnostics(UriA);
        var scpt004A = diagsA.FirstOrDefault(d => d.Code == DiagnosticCodes.DuplicateScriptName);
        Assert.Equal(DiagnosticCodes.DuplicateScriptName, scpt004A.Code);
        Assert.Equal(UriA.ToString(), scpt004A.FilePath);
        Assert.Contains("script", scpt004A.Message);
        Assert.Contains("opening", scpt004A.Message);
    }

    [Fact]
    public void CrossFile_DuplicateTextName_SurfacesInFirstSeenFile()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            text intro[
            hi
            ]
            """);
        Register(index, UriB, """
            text intro[
            hello
            ]
            """);

        var diagsA = index.GetAllDiagnostics(UriA);
        var scpt004A = diagsA.FirstOrDefault(d => d.Code == DiagnosticCodes.DuplicateScriptName);
        Assert.Equal(DiagnosticCodes.DuplicateScriptName, scpt004A.Code);
        Assert.Contains("text", scpt004A.Message);
    }

    [Fact]
    public void CrossFile_DuplicateResolvedByRemovingLaterFile_DropsScpt004()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            script opening[
            hi
            ]
            """);
        Register(index, UriB, """
            script opening[
            hello
            ]
            """);
        Assert.Contains(index.GetAllDiagnostics(UriA),
            d => d.Code == DiagnosticCodes.DuplicateScriptName);

        index.Remove(UriB);

        Assert.DoesNotContain(index.GetAllDiagnostics(UriA),
            d => d.Code == DiagnosticCodes.DuplicateScriptName);
    }

    // ── SCPT007 argument type mismatch (cross-file interface) ────────────

    [Fact]
    public void CrossFile_TypeMismatch_SurfacesInCallersFile()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface play(sound: string) : void
            """);
        Register(index, UriB, """
            script a[
            @play(42)
            ]
            """);

        var diags = index.GetAllDiagnostics(UriB);
        var scpt007 = diags.FirstOrDefault(d => d.Code == DiagnosticCodes.ArgumentTypeMismatch);
        Assert.Equal(DiagnosticCodes.ArgumentTypeMismatch, scpt007.Code);
        Assert.Equal(UriB.ToString(), scpt007.FilePath);
    }

    // ── SCPT008 argument count mismatch ───────────────────────────────────

    [Fact]
    public void CrossFile_CountMismatch_SurfacesInCallersFile()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface play(sound: string, volume: int) : void
            """);
        Register(index, UriB, """
            script a[
            @play("hi")
            ]
            """);

        var diags = index.GetAllDiagnostics(UriB);
        var scpt008 = diags.FirstOrDefault(d => d.Code == DiagnosticCodes.ArgumentCountMismatch);
        Assert.Equal(DiagnosticCodes.ArgumentCountMismatch, scpt008.Code);
        Assert.Equal(UriB.ToString(), scpt008.FilePath);
    }

    // ── Workspace bookkeeping ─────────────────────────────────────────────

    [Fact]
    public void FileCount_ReflectsRegisteredFiles()
    {
        var index = new WorkspaceIndex();
        Assert.Equal(0, index.FileCount);

        Register(index, UriA, "script a[hi]");
        Assert.Equal(1, index.FileCount);

        Register(index, UriB, "script b[hi]");
        Assert.Equal(2, index.FileCount);

        index.Remove(UriA);
        Assert.Equal(1, index.FileCount);

        index.Remove(UriB);
        Assert.Equal(0, index.FileCount);
    }

    [Fact]
    public void GetAllDiagnostics_UnknownUri_ReturnsEmpty()
    {
        var index = new WorkspaceIndex();
        Assert.Empty(index.GetAllDiagnostics(UriC));
    }

    [Fact]
    public void GetCompilationData_ReturnsRegisteredData()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface greet(name: string) : void
            script hello[
            @greet("world")
            ]
            """);

        var data = index.GetCompilationData(UriA);
        Assert.NotNull(data);
        Assert.True(data!.Interfaces.Any(i => i.Name == "greet"));
        Assert.True(data.Scripts.ContainsKey("hello"));
    }

    [Fact]
    public void GetCompilationData_UnknownUri_ReturnsNull()
    {
        var index = new WorkspaceIndex();
        Assert.Null(index.GetCompilationData(UriC));
    }

    // ── Async surface (no SCPT012 in the LSP — see WorkspaceIndex remarks) ─

    [Fact]
    public void AsyncInterface_SyncImpl_MismatchIsNotSurfacedByLsp()
    {
        // The LSP has no access to [Implementation] methods, so async/sync
        // mismatch (SCPT012/013) is never reported here. This test pins that
        // behavior; the Roslyn source generator still reports it at compile time.
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface fetch(id: int) : string
            script a[
            @fetch(1)
            ]
            """);

        var diags = index.GetAllDiagnostics(UriA);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCodes.AsyncSyncMismatch);
        Assert.DoesNotContain(diags, d => d.Code == DiagnosticCodes.SyncAsyncMismatch);
    }

    // ── Dirty-set short-circuit ────────────────────────────────────────────

    [Fact]
    public void Register_SameTextHash_SkipsRecompute()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, "script a[hi]");
        Register(index, UriB, "text b[hello]");
        var countBefore = index.RecomputeCount;

        Register(index, UriA, "script a[hi]");

        Assert.Equal(countBefore, index.RecomputeCount);
    }

    [Fact]
    public void Register_DifferentTextHash_TriggersRecompute()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, "script a[hi]");
        var countBefore = index.RecomputeCount;

        Register(index, UriA, "script a[hello]");

        Assert.Equal(countBefore + 1, index.RecomputeCount);
    }

    [Fact]
    public void Register_NewFile_AlwaysRecomputes()
    {
        var index = new WorkspaceIndex();
        // First registration — no prior entry, so always recompute.
        var countBefore = index.RecomputeCount;
        Register(index, UriA, "script a[hi]");
        Assert.Equal(countBefore + 1, index.RecomputeCount);

        // Second file — new URI, always recompute.
        countBefore = index.RecomputeCount;
        Register(index, UriB, "script b[hi]");
        Assert.Equal(countBefore + 1, index.RecomputeCount);
    }

    [Fact]
    public void Remove_AlwaysRecomputes()
    {
        var index = new WorkspaceIndex();
        Register(index, UriA, "script a[hi]");
        var removed = index.RecomputeCount;

        index.Remove(UriA);

        Assert.Equal(removed + 1, index.RecomputeCount);
    }

    [Fact]
    public void Register_SameTextHash_UpdatesStoredData()
    {
        // When the short-circuit fires, the FileCompilation entry is still
        // updated so a subsequent Recompute triggered by another file picks
        // up the latest data.
        var index = new WorkspaceIndex();
        Register(index, UriA, """
            interface greet(name: string) : void
            """);
        Register(index, UriB, """
            script s[
            @greet("hi")
            ]
            """);
        var countBefore = index.RecomputeCount;

        // Re-register A with same text — should short-circuit.
        Register(index, UriA, """
            interface greet(name: string) : void
            """);
        Assert.Equal(countBefore, index.RecomputeCount);

        // A's cross-file diagnostics are still valid (B's call resolved).
        Assert.DoesNotContain(index.GetAllDiagnostics(UriB),
            d => d.Code == DiagnosticCodes.UndeclaredConsumerCall);
    }
}
