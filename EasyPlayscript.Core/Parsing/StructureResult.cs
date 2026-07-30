using System.Collections.Generic;

namespace EasyPlayscript.Parsing;

public class StructureParseResult(List<StructureResult> results, List<InterfaceDeclaration> interfaces)
{
    public List<StructureResult> Results { get; } = results;
    public List<InterfaceDeclaration> Interfaces { get; } = interfaces;
}

/// <summary>
///     Represents a parsed structure result from Pass 1, containing compiler call info and optional raw block content.
/// </summary>
public readonly struct StructureResult(BlockType identifier, string name, string? variation, string? rawContent, int line, int col, int startChar, int endChar)
{
    public BlockType Identifier { get; } = identifier;
    public string Name { get; } = name;
    public string? Variation { get; } = variation;
    public string? RawContent { get; } = rawContent;
    public int Line { get; } = line;
    public int Col { get; } = col;
    public int StartChar { get; } = startChar;
    public int EndChar { get; } = endChar;

    public void Deconstruct(out BlockType identifier, out string name, out string? variation, out string? rawContent, out int line,
        out int col)
    {
        identifier = Identifier;
        name = Name;
        variation = Variation;
        rawContent = RawContent;
        line = Line;
        col = Col;
    }
}