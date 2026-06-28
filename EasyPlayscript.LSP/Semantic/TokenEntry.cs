namespace EasyPlayscript.LSP.Semantic;

internal readonly record struct TokenEntry(int Line, int Col, int Length, int TokenType, int TokenModifiers = 0);