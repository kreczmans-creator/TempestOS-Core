namespace Tempest.Calc.Engine.Parsing;

internal enum TokenKind
{
    Number,
    Identifier,
    Text,
    Operator,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    Comma,
    Semicolon,
    End,
}

/// <param name="Unit">For a number: the unit text written directly after it, or <see langword="null"/>.</param>
internal readonly record struct Token(TokenKind Kind, string Text, int Position, string? Unit = null);
