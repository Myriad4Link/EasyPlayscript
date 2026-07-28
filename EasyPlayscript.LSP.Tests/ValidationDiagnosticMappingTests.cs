using EasyPlayscript.LSP.Mapping;
using EasyPlayscript.Parsing;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace EasyPlayscript.LSP.Tests;

public class ValidationDiagnosticMappingTests
{
    private static ValidationDiagnostic Diag(int line, int col, string code = "SCPT005",
        string format = "x", params object[] args) =>
        new(code, format, "/test.scpt", line, col, args);

    // ── Line conversion (1-based ANTLR → 0-based LSP) ─────────────────────

    [Fact]
    public void Line_OneBased_ConvertsToZeroBased()
    {
        var diag = Diag(1, 0);
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal(0, d.Range.Start.Line);
        Assert.Equal(0, d.Range.End.Line);
    }

    [Fact]
    public void Line_LargeValue_ConvertsCorrectly()
    {
        var diag = Diag(42, 5);
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal(41, d.Range.Start.Line);
    }

    // ── Column clamping ────────────────────────────────────────────────────

    [Fact]
    public void Column_Negative_ClampsToZero()
    {
        var diag = Diag(1, -3);
        var d = PositionMapper.ToLspDiagnostic(diag, "any text");
        Assert.Equal(0, d.Range.Start.Character);
    }

    [Fact]
    public void Column_PastEndOfLine_ClampsToLineLength()
    {
        const string text = "interface greet(name: string) : void\nscript a[hi]";
        // Line 1 in ANTLR = "interface greet(name: string) : void" (length 36)
        var diag = Diag(1, 999, "SCPT006");
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        Assert.Equal(36, d.Range.Start.Character);
        Assert.Equal(36, d.Range.End.Character);
    }

    [Fact]
    public void Column_AtEndOfLine_ClampsToLineLength()
    {
        const string text = "interface greet(name: string) : void\nscript a[hi]";
        var diag = Diag(1, 36, "SCPT006");
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        Assert.Equal(36, d.Range.Start.Character);
    }

    [Fact]
    public void Column_InsideLine_PreservedAsIs()
    {
        const string text = "interface greet(name: string) : void\nscript a[hi]";
        // "greet" starts at column 10 on line 1
        var diag = Diag(1, 10, "SCPT005");
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        Assert.Equal(10, d.Range.Start.Character);
    }

    [Fact]
    public void Column_LineBeyondText_ClampsToZero()
    {
        const string text = "single line\n";
        var diag = Diag(99, 5);
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        // line 99 is past EOF; we treat as "not in text" → clamp to 0.
        Assert.Equal(0, d.Range.Start.Character);
    }

    [Fact]
    public void Column_FileTextNull_FallsBackToUnclampedColumn()
    {
        var diag = Diag(1, 42);
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal(42, d.Range.Start.Character);
    }

    [Fact]
    public void Column_HandlesCrlfLineEndings()
    {
        const string text = "line1\r\nline2\r\nline3";
        // Line 1 is "line1" (length 5); the \r\n is the line break.
        var diag = Diag(1, 999, "SCPT006");
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        Assert.Equal(5, d.Range.Start.Character);
    }

    // ── Severity ───────────────────────────────────────────────────────────

    [Fact]
    public void Severity_Default_IsError()
    {
        var diag = Diag(1, 0, "SCPT005");
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
    }

    [Fact]
    public void Severity_UnusedImplementation_IsWarning()
    {
        var diag = Diag(1, 0, DiagnosticCodes.UnusedImplementation, "x");
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
    }

    [Fact]
    public void Severity_AllOtherCodes_AreError()
    {
        var codes = new[]
        {
            DiagnosticCodes.UnexpectedToken,      // SCPT002
            DiagnosticCodes.MismatchedInput,      // SCPT003
            DiagnosticCodes.DuplicateScriptName,  // SCPT004
            DiagnosticCodes.UndeclaredConsumerCall,// SCPT005
            DiagnosticCodes.DuplicateInterfaceSignature,// SCPT006
            DiagnosticCodes.ArgumentTypeMismatch, // SCPT007
            DiagnosticCodes.ArgumentCountMismatch,// SCPT008
            DiagnosticCodes.MissingImplementation,// SCPT009
            DiagnosticCodes.DuplicateImplementation,// SCPT010
            DiagnosticCodes.AsyncSyncMismatch,    // SCPT012
            DiagnosticCodes.SyncAsyncMismatch,    // SCPT013
        };
        foreach (var code in codes)
        {
            var d = PositionMapper.ToLspDiagnostic(Diag(1, 0, code, "x"), null);
            Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        }
    }

    // ── Code, Source, Message ─────────────────────────────────────────────

    [Fact]
    public void Code_IsPreserved()
    {
        var diag = Diag(1, 0, "SCPT006");
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal("SCPT006", d.Code);
    }

    [Fact]
    public void Source_IsEasyPlayscript()
    {
        var diag = Diag(1, 0, "SCPT005");
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Equal("EasyPlayscript", d.Source);
    }

    [Fact]
    public void Message_IsFormatted()
    {
        var diag = new ValidationDiagnostic(
            DiagnosticCodes.ArgumentTypeMismatch,
            DiagnosticCodes.ArgumentTypeMismatchFormat,
            "/test.scpt", 3, 4,
            1, "play", "int", "string", "");
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        Assert.Contains("play", d.Message);
        Assert.Contains("int", d.Message);
        Assert.Contains("string", d.Message);
    }

    [Fact]
    public void Range_HasNonZeroWidth()
    {
        var diag = Diag(5, 10);
        var d = PositionMapper.ToLspDiagnostic(diag, null);
        var start = d.Range.Start;
        var end = d.Range.End;
        Assert.True(end.Character > start.Character || end.Line > start.Line,
            $"Expected a non-zero-width range, got [{start.Line},{start.Character}]-[{end.Line},{end.Character}]");
    }

    [Fact]
    public void Range_AtEndOfLine_DoesNotOvershoot()
    {
        // Text where the line is exactly the length of the column + 1 character
        const string text = "abcdef";
        var diag = Diag(1, 6); // past end-of-line (length 6, valid indices 0..5)
        var d = PositionMapper.ToLspDiagnostic(diag, text);
        Assert.Equal(new Range(0, 6, 0, 6), d.Range);
    }
}
