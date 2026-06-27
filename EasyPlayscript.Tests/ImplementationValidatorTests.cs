using System.Linq;
using EasyPlayscript.DataModel;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class ImplementationValidatorTests
{
    private static ScriptBlock BuildScriptBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.ParseScript(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildScriptFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    private static TextBlock BuildTextBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.ParseText(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildTextFromContent(parser.textContent());
        return builder.TextResult;
    }

    private static InterfaceDeclaration MakeInterface(string name, InterfaceType returnType,
        params (string n, InterfaceType t)[] parameters)
    {
        var parms = parameters.Select(p => new InterfaceParameter(p.n, p.t)).ToList();
        return new InterfaceDeclaration(name, parms, returnType, 1, 0) { FilePath = "test" };
    }

    private static InterfaceDeclaration MakeAsyncInterface(string name, InterfaceType returnType,
        params (string n, InterfaceType t)[] parameters)
    {
        var parms = parameters.Select(p => new InterfaceParameter(p.n, p.t)).ToList();
        return new InterfaceDeclaration(name, parms, returnType, 1, 0, isAsync: true) { FilePath = "test" };
    }

    private static ImplementationInfo MakeImplementation(string className, string methodName,
        string? alias = null, params string[] paramTypeNames)
    {
        return new ImplementationInfo
        {
            ClassName = className,
            MethodName = methodName,
            Alias = alias,
            ParameterTypeNames = paramTypeNames.ToList(),
            FilePath = "test.cs",
            Line = 1
        };
    }

    private static ImplementationInfo MakeAsyncImplementation(string className, string methodName,
        string? alias = null, params string[] paramTypeNames)
    {
        return new ImplementationInfo
        {
            ClassName = className,
            MethodName = methodName,
            Alias = alias,
            ParameterTypeNames = paramTypeNames.ToList(),
            IsAsync = true,
            FilePath = "test.cs",
            Line = 1
        };
    }

    // ─── ValidateMissingImplementations ────────────────────────────────────

    [Fact]
    public void ValidateMissingImplementations_Matching_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("fade", InterfaceType.Void, ("type", InterfaceType.String)));
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateMissingImplementations_NoMatch_ReturnsSCPT009()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("fade", InterfaceType.Void, ("type", InterfaceType.String)));
        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.MissingImplementation, errors[0].Code);
    }

    [Fact]
    public void ValidateMissingImplementations_MatchesByAlias()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("fade", InterfaceType.Void));
        data.Implementations.Add(MakeImplementation("Effects", "DoFade", "fade"));
        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateMissingImplementations_MultipleInterfaces_OnlyMissingReported()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("fade", InterfaceType.Void));
        data.Interfaces.Add(MakeInterface("slide", InterfaceType.Void, ("dir", InterfaceType.String)));
        data.Implementations.Add(MakeImplementation("Effects", "fade"));
        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Single(errors);
        Assert.Contains("slide", errors[0].Message);
    }

    [Fact]
    public void ValidateMissingImplementations_ParamCountMismatch_ReturnsSCPT009()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("fade", InterfaceType.Void, ("a", InterfaceType.String)));
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string", "string"));
        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.MissingImplementation, errors[0].Code);
    }

    // ─── ValidateDuplicateImplementations ──────────────────────────────────

    [Fact]
    public void ValidateDuplicateImplementations_SingleImpl_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        var errors = ImplementationValidator.ValidateDuplicateImplementations(data);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateDuplicateImplementations_DuplicateAcrossClasses_ReturnsSCPT010()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("EffectsA", "fade", null, "string"));
        data.Implementations.Add(MakeImplementation("EffectsB", "fade", null, "string"));
        var errors = ImplementationValidator.ValidateDuplicateImplementations(data);
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.DuplicateImplementation, errors[0].Code);
    }

    [Fact]
    public void ValidateDuplicateImplementations_DuplicateInSameClass_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        var errors = ImplementationValidator.ValidateDuplicateImplementations(data);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateDuplicateImplementations_DuplicateByAlias_ReturnsSCPT010()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("EffectsA", "DoFade", "fade"));
        data.Implementations.Add(MakeImplementation("EffectsB", "DoFade", "fade"));
        var errors = ImplementationValidator.ValidateDuplicateImplementations(data);
        Assert.Single(errors);
        Assert.Equal(DiagnosticCodes.DuplicateImplementation, errors[0].Code);
    }

    [Fact]
    public void ValidateDuplicateImplementations_DifferentParamCount_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("EffectsA", "fade", null, "string"));
        data.Implementations.Add(MakeImplementation("EffectsB", "fade", null, "string", "string"));
        var errors = ImplementationValidator.ValidateDuplicateImplementations(data);
        Assert.Empty(errors);
    }

    // ─── ValidateUnusedImplementations ─────────────────────────────────────

    [Fact]
    public void ValidateUnusedImplementations_UsedInScript_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        data.Scripts["s"] = BuildScriptBlock("@fade(\"out\")");
        data.ScriptLocations["s"] = ("test.scpt", 1, 0);
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateUnusedImplementations_UsedInText_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        data.Texts["t"] = BuildTextBlock("@fade(\"out\")");
        data.TextLocations["t"] = ("test.scpt", 1, 0);
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateUnusedImplementations_Unused_ReturnsSCPT011()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade", null, "string"));
        data.Scripts["s"] = BuildScriptBlock("Hello world");
        data.ScriptLocations["s"] = ("test.scpt", 1, 0);
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.UnusedImplementation, warnings[0].Code);
    }

    [Fact]
    public void ValidateUnusedImplementations_AliasUsedInScript_ReturnsEmpty()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "DoFade", "fade", "string"));
        data.Scripts["s"] = BuildScriptBlock("@fade(\"out\")");
        data.ScriptLocations["s"] = ("test.scpt", 1, 0);
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Empty(warnings);
    }

    [Fact]
    public void ValidateUnusedImplementations_AliasNotUsed_MethodNameUsed_ReturnsSCPT011()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "DoFade", "fade", "string"));
        data.Scripts["s"] = BuildScriptBlock("@DoFade(\"out\")");
        data.ScriptLocations["s"] = ("test.scpt", 1, 0);
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.UnusedImplementation, warnings[0].Code);
    }

    [Fact]
    public void ValidateUnusedImplementations_EmptyScriptsAndTexts_ReturnsSCPT011()
    {
        var data = new PlayscriptCompilationData();
        data.Implementations.Add(MakeImplementation("Effects", "fade"));
        var warnings = ImplementationValidator.ValidateUnusedImplementations(data);
        Assert.Single(warnings);
        Assert.Equal(DiagnosticCodes.UnusedImplementation, warnings[0].Code);
    }

    // ─── Async/Sync Mismatch Validation ──────────────────────────────────────

    [Fact]
    public void ValidateMissingImplementations_AsyncInterface_SyncImpl_ReportsSCPT012()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeAsyncInterface("load", InterfaceType.String,
            ("id", InterfaceType.Int)));
        data.Implementations.Add(MakeImplementation("Game.Data", "load", null, "int"));

        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.AsyncSyncMismatch);
    }

    [Fact]
    public void ValidateMissingImplementations_SyncInterface_AsyncImpl_ReportsSCPT013()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("play", InterfaceType.Void,
            ("sound", InterfaceType.String)));
        data.Implementations.Add(MakeAsyncImplementation("Game.Audio", "play", null, "string"));

        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Contains(errors, e => e.Code == DiagnosticCodes.SyncAsyncMismatch);
    }

    [Fact]
    public void ValidateMissingImplementations_AsyncInterface_AsyncImpl_Passes()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeAsyncInterface("load", InterfaceType.String,
            ("id", InterfaceType.Int)));
        data.Implementations.Add(MakeAsyncImplementation("Game.Data", "load", null, "int"));

        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateMissingImplementations_SyncInterface_SyncImpl_Passes()
    {
        var data = new PlayscriptCompilationData();
        data.Interfaces.Add(MakeInterface("play", InterfaceType.Void,
            ("sound", InterfaceType.String)));
        data.Implementations.Add(MakeImplementation("Game.Audio", "play", null, "string"));

        var errors = ImplementationValidator.ValidateMissingImplementations(data);
        Assert.Empty(errors);
    }
}