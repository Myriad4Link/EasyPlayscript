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
public readonly struct StructureResult(BlockType identifier, string name, string? rawContent, int line, int col)
{
    public BlockType Identifier { get; } = identifier;
    public string Name { get; } = name;
    public string? RawContent { get; } = rawContent;
    public int Line { get; } = line;
    public int Col { get; } = col;

    public void Deconstruct(out BlockType identifier, out string name, out string? rawContent, out int line,
        out int col)
    {
        identifier = Identifier;
        name = Name;
        rawContent = RawContent;
        line = Line;
        col = Col;
    }
}