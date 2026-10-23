using System.Globalization;

namespace Tempest.Calc.Engine.Parsing;

/// <summary>
/// Splits an expression into tokens. A number may be followed, after
/// optional spaces, by a unit written without inner spaces (<c>40 mm</c>,
/// <c>9.81 m/s^2</c>, <c>2 kN*m</c>); the unit text is attached to the
/// number token and resolved by the parser.
/// </summary>
internal sealed class Lexer(string text, CalcLimits limits)
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal) { "and", "or", "not", "in", "true", "false" };

    private readonly string _text = text;
    private readonly CalcLimits _limits = limits;
    private int _pos;

    public List<Token> Tokenize()
    {
        if (_text.Length > _limits.MaxExpressionLength)
            throw new CalcLimitException(nameof(CalcLimits.MaxExpressionLength), $"An expression is at most {_limits.MaxExpressionLength} characters; this one is {_text.Length}.");

        var tokens = new List<Token>();

        while (true)
        {
            SkipWhitespace();

            if (tokens.Count >= _limits.MaxTokens)
                throw new CalcLimitException(nameof(CalcLimits.MaxTokens), $"An expression is at most {_limits.MaxTokens} tokens.");

            if (_pos >= _text.Length)
            {
                tokens.Add(new Token(TokenKind.End, string.Empty, _pos));
                return tokens;
            }

            var c = _text[_pos];
            var start = _pos;

            if (char.IsAsciiDigit(c) || (c == '.' && _pos + 1 < _text.Length && char.IsAsciiDigit(_text[_pos + 1])))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (IsIdentifierStart(c))
            {
                while (_pos < _text.Length && IsIdentifierPart(_text[_pos]))
                    _pos++;

                var word = _text[start.._pos];

                if (word.Length > _limits.MaxSymbolLength)
                    throw new CalcLimitException(nameof(CalcLimits.MaxSymbolLength), $"A symbol is at most {_limits.MaxSymbolLength} characters.");

                tokens.Add(Keywords.Contains(word) ? new Token(TokenKind.Operator, word, start) : new Token(TokenKind.Identifier, word, start));
                continue;
            }

            if (c == '"')
            {
                _pos++;
                var textStart = _pos;

                while (_pos < _text.Length && _text[_pos] != '"')
                    _pos++;

                if (_pos >= _text.Length)
                    throw new CalcParseException("Unterminated text: missing closing '\"'.", start);

                var value = _text[textStart.._pos];
                _pos++;

                if (value.Length > _limits.MaxTextLength)
                    throw new CalcLimitException(nameof(CalcLimits.MaxTextLength), $"A text is at most {_limits.MaxTextLength} characters.");

                tokens.Add(new Token(TokenKind.Text, value, start));
                continue;
            }

            _pos++;

            switch (c)
            {
                case '(': tokens.Add(new Token(TokenKind.LeftParen, "(", start)); break;
                case ')': tokens.Add(new Token(TokenKind.RightParen, ")", start)); break;
                case '[': tokens.Add(new Token(TokenKind.LeftBracket, "[", start)); break;
                case ']': tokens.Add(new Token(TokenKind.RightBracket, "]", start)); break;
                case ',': tokens.Add(new Token(TokenKind.Comma, ",", start)); break;
                case ';': tokens.Add(new Token(TokenKind.Semicolon, ";", start)); break;
                case '+' or '-' or '*' or '/' or '^' or '·' or '−' or '×' or '÷':
                    tokens.Add(new Token(TokenKind.Operator, Normalise(c), start));
                    break;
                case '<' or '>' or '=' or '!':
                {
                    if (_pos < _text.Length && _text[_pos] == '=')
                    {
                        _pos++;
                        tokens.Add(new Token(TokenKind.Operator, c + "=", start));
                    }
                    else if (c == '=')
                    {
                        tokens.Add(new Token(TokenKind.Operator, "=", start));
                    }
                    else if (c == '!')
                    {
                        throw new CalcParseException("Use 'not' or '!=' — a lone '!' means nothing.", start);
                    }
                    else
                    {
                        tokens.Add(new Token(TokenKind.Operator, c.ToString(), start));
                    }

                    break;
                }
                case '≤': tokens.Add(new Token(TokenKind.Operator, "<=", start)); break;
                case '≥': tokens.Add(new Token(TokenKind.Operator, ">=", start)); break;
                case '≠': tokens.Add(new Token(TokenKind.Operator, "!=", start)); break;
                case '&' when _pos < _text.Length && _text[_pos] == '&':
                    _pos++;
                    tokens.Add(new Token(TokenKind.Operator, "and", start));
                    break;
                case '|' when _pos < _text.Length && _text[_pos] == '|':
                    _pos++;
                    tokens.Add(new Token(TokenKind.Operator, "or", start));
                    break;
                default:
                    throw new CalcParseException($"Unexpected character '{c}'.", start);
            }
        }
    }

    private Token ReadNumber()
    {
        var start = _pos;

        while (_pos < _text.Length && (char.IsAsciiDigit(_text[_pos]) || _text[_pos] == '.' || _text[_pos] == '_'))
            _pos++;

        if (_pos < _text.Length && (_text[_pos] == 'e' || _text[_pos] == 'E'))
        {
            var save = _pos;
            _pos++;

            if (_pos < _text.Length && (_text[_pos] == '+' || _text[_pos] == '-'))
                _pos++;

            if (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
            {
                while (_pos < _text.Length && char.IsAsciiDigit(_text[_pos]))
                    _pos++;
            }
            else
            {
                _pos = save;
            }
        }

        var literal = _text[start.._pos].Replace("_", string.Empty, StringComparison.Ordinal);

        if (literal.Length > _limits.MaxLiteralLength)
            throw new CalcLimitException(nameof(CalcLimits.MaxLiteralLength), $"A number literal is at most {_limits.MaxLiteralLength} characters.");

        if (!double.TryParse(literal, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new CalcParseException($"'{literal}' is not a number.", start);

        // A unit directly after the number, with at most one run of spaces.
        var save2 = _pos;

        while (_pos < _text.Length && _text[_pos] == ' ')
            _pos++;

        if (_pos < _text.Length && IsUnitStart(_text[_pos]) && !LooksLikeKeyword())
        {
            var unitStart = _pos;
            var parenDepth = 0;

            while (_pos < _text.Length)
            {
                var u = _text[_pos];

                if (u == '(')
                {
                    // A bracket belongs to the unit only right after '/' or '*' and only when closed before a space.
                    if (_pos == unitStart || _text[_pos - 1] is not ('/' or '*' or '·'))
                        break;

                    var close = _text.IndexOf(')', _pos);
                    var space = _text.IndexOfAny([' ', ',', ';', ']'], _pos);

                    if (close < 0 || (space >= 0 && space < close))
                        break;

                    parenDepth++;
                }
                else if (u == ')')
                {
                    if (parenDepth == 0)
                        break;

                    parenDepth--;
                }
                else if (!IsUnitChar(u))
                {
                    break;
                }

                _pos++;
            }

            // Do not swallow a trailing operator that was meant for the expression ("2 m *3" vs "2 m*s").
            var unit = _text[unitStart.._pos].TrimEnd('*', '/', '·', '^', '.', '-');
            _pos = unitStart + unit.Length;

            if (unit.Length > 0)
                return new Token(TokenKind.Number, literal, start, unit);
        }

        _pos = save2;
        return new Token(TokenKind.Number, literal, start);
    }

    private bool LooksLikeKeyword()
    {
        foreach (var k in Keywords)
        {
            if (string.CompareOrdinal(_text, _pos, k, 0, k.Length) == 0 && (_pos + k.Length >= _text.Length || !IsIdentifierPart(_text[_pos + k.Length])))
                return true;
        }

        return false;
    }

    private static string Normalise(char c) => c switch
    {
        '·' or '×' => "*",
        '−' => "-",
        '÷' => "/",
        _ => c.ToString(),
    };

    private static bool IsIdentifierStart(char c) => char.IsLetter(c) || c == '_';

    private static bool IsIdentifierPart(char c) => char.IsLetterOrDigit(c) || c is '_' or '\'' || IsSubscriptOrGreek(c);

    private static bool IsSubscriptOrGreek(char c) => c is >= '₀' and <= 'ₜ';

    private static bool IsUnitStart(char c) => char.IsLetter(c) || c is '°' or 'µ' or 'Ω' or '%' or '‰';

    private static bool IsUnitChar(char c) => char.IsLetterOrDigit(c) || c is '°' or 'µ' or 'Ω' or '%' or '‰' or '/' or '*' or '·' or '^' or '-' or '⁻' or '²' or '³' or '⁴';

    private void SkipWhitespace()
    {
        while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
            _pos++;
    }
}
