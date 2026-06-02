using System.Collections.Generic;
using System.Linq;
using EasyPlayscript.Parsing;
using Xunit;

namespace EasyPlayscript.Tests;

public class InterfaceValidatorTests
{
    private static ScriptBlock BuildScriptBlock(string input)
    {
        var (parser, errors) = PlayscriptContentHelper.Parse(input);
        Assert.Empty(errors);
        var builder = new PlayscriptCodeBuilder();
        builder.BuildFromContent(parser.scriptContent());
        return builder.ContentResult;
    }

    private static InterfaceDeclaration MakeInterface(string name, InterfaceType returnType, params (string n, InterfaceType t)[] parameters)
    {
        var parms = parameters.Select(p => new InterfaceParameter(p.n, p.t)).ToList();
        return new InterfaceDeclaration(name, parms, returnType, 1, 0) { FilePath = "test" };
    }

    // ─── GetConsumerCalls ─────────────────────────────────────────────────────

    [Fact]
    public void GetConsumerCalls_YieldsAllCalls()
    {
        var block = BuildScriptBlock("@a() @b(\"x\")");
        var calls = InterfaceValidator.GetConsumerCalls(block).ToList();
        Assert.Equal(2, calls.Count);
        Assert.Equal("a", calls[0].Identifier);
        Assert.Equal("b", calls[1].Identifier);
    }

    [Fact]
    public void GetConsumerCalls_EmptyBlock_ReturnsEmpty()
    {
        var block = BuildScriptBlock("Hello world");
        var calls = InterfaceValidator.GetConsumerCalls(block).ToList();
        Assert.Empty(calls);
    }

    // ─── GetArgumentType ─────────────────────────────────────────────────────

    [Fact]
    public void GetArgumentType_StringArgument_ReturnsString()
    {
        Assert.Equal(InterfaceType.String, InterfaceValidator.GetArgumentType(new StringArgument("x")));
    }

    [Fact]
    public void GetArgumentType_IntArgument_ReturnsInt()
    {
        Assert.Equal(InterfaceType.Int, InterfaceValidator.GetArgumentType(new IntArgument(1)));
    }

    [Fact]
    public void GetArgumentType_DoubleArgument_ReturnsDecimal()
    {
        Assert.Equal(InterfaceType.Decimal, InterfaceValidator.GetArgumentType(new DoubleArgument(1.0)));
    }

    [Fact]
    public void GetArgumentType_BoolArgument_ReturnsBool()
    {
        Assert.Equal(InterfaceType.Bool, InterfaceValidator.GetArgumentType(new BoolArgument(true)));
    }

    // ─── IsAssignableTo ──────────────────────────────────────────────────────

    [Fact]
    public void IsAssignableTo_SameType_ReturnsTrue()
    {
        Assert.True(InterfaceValidator.IsAssignableTo(InterfaceType.String, InterfaceType.String));
        Assert.True(InterfaceValidator.IsAssignableTo(InterfaceType.Int, InterfaceType.Int));
        Assert.True(InterfaceValidator.IsAssignableTo(InterfaceType.Decimal, InterfaceType.Decimal));
        Assert.True(InterfaceValidator.IsAssignableTo(InterfaceType.Bool, InterfaceType.Bool));
    }

    [Fact]
    public void IsAssignableTo_IntToDecimal_ReturnsTrue()
    {
        Assert.True(InterfaceValidator.IsAssignableTo(InterfaceType.Int, InterfaceType.Decimal));
    }

    [Fact]
    public void IsAssignableTo_Incompatible_ReturnsFalse()
    {
        Assert.False(InterfaceValidator.IsAssignableTo(InterfaceType.String, InterfaceType.Int));
        Assert.False(InterfaceValidator.IsAssignableTo(InterfaceType.Int, InterfaceType.String));
        Assert.False(InterfaceValidator.IsAssignableTo(InterfaceType.Bool, InterfaceType.Decimal));
    }

    // ─── MakeSignatureKey ────────────────────────────────────────────────────

    [Fact]
    public void MakeSignatureKey_FormatsCorrectly()
    {
        var decl = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        Assert.Equal("transition(string,decimal):void", InterfaceValidator.MakeSignatureKey(decl));
    }

    [Fact]
    public void MakeSignatureKey_NoParams_FormatsCorrectly()
    {
        var decl = MakeInterface("on_complete", InterfaceType.Void);
        Assert.Equal("on_complete():void", InterfaceValidator.MakeSignatureKey(decl));
    }

    // ─── ValidateUndeclaredCalls ─────────────────────────────────────────────

    [Fact]
    public void ValidateUndeclaredCalls_NoInterface_ReturnsError()
    {
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateUndeclaredCalls(
            new List<InterfaceDeclaration>(), scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Single(errors);
        Assert.Equal("SCPT005", errors[0].Code);
    }

    [Fact]
    public void ValidateUndeclaredCalls_Declared_ReturnsEmpty()
    {
        var iface = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String));
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateUndeclaredCalls(
            new List<InterfaceDeclaration> { iface }, scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Empty(errors);
    }

    // ─── ValidateDuplicateSignatures ─────────────────────────────────────────

    [Fact]
    public void ValidateDuplicateSignatures_Duplicate_ReturnsError()
    {
        var a = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String));
        var b = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String));
        var errors = InterfaceValidator.ValidateDuplicateSignatures(new List<InterfaceDeclaration> { a, b });
        Assert.Single(errors);
        Assert.Equal("SCPT006", errors[0].Code);
    }

    [Fact]
    public void ValidateDuplicateSignatures_DifferentSignature_ReturnsEmpty()
    {
        var a = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String));
        var b = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        var errors = InterfaceValidator.ValidateDuplicateSignatures(new List<InterfaceDeclaration> { a, b });
        Assert.Empty(errors);
    }

    // ─── ValidateArgumentTypes ───────────────────────────────────────────────

    [Fact]
    public void ValidateArgumentTypes_CountMismatch_ReturnsError()
    {
        var iface = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        var block = BuildScriptBlock("@transition(\"fade_out\")");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateArgumentTypes(
            new List<InterfaceDeclaration> { iface }, scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Single(errors);
        Assert.Equal("SCPT008", errors[0].Code);
    }

    [Fact]
    public void ValidateArgumentTypes_TypeMismatch_ReturnsError()
    {
        var iface = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        var block = BuildScriptBlock("@transition(\"fade_out\", \"not_a_number\")");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateArgumentTypes(
            new List<InterfaceDeclaration> { iface }, scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Single(errors);
        Assert.Equal("SCPT007", errors[0].Code);
    }

    [Fact]
    public void ValidateArgumentTypes_IntToDecimal_ReturnsEmpty()
    {
        var iface = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        var block = BuildScriptBlock("@transition(\"fade_out\", 1)");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateArgumentTypes(
            new List<InterfaceDeclaration> { iface }, scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Empty(errors);
    }

    [Fact]
    public void ValidateArgumentTypes_AllMatch_ReturnsEmpty()
    {
        var iface = MakeInterface("transition", InterfaceType.Void, ("type", InterfaceType.String), ("duration", InterfaceType.Decimal));
        var block = BuildScriptBlock("@transition(\"fade_out\", 1.0)");
        var scripts = new Dictionary<string, ScriptBlock> { ["foo"] = block };
        var scriptLocs = new Dictionary<string, (string, int, int)> { ["foo"] = ("file", 1, 0) };
        var errors = InterfaceValidator.ValidateArgumentTypes(
            new List<InterfaceDeclaration> { iface }, scripts, scriptLocs,
            new Dictionary<string, ScriptBlock>(), new Dictionary<string, (string, int, int)>());
        Assert.Empty(errors);
    }
}
