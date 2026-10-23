namespace Tempest.Calc.Engine.Parsing;

/// <summary>
/// Recursive-descent parser. Precedence, lowest first: <c>in</c> (unit
/// conversion), <c>or</c>, <c>and</c>, <c>not</c>, comparison, additive,
/// multiplicative, unary sign, power (right-associative), postfix index and
/// call. Nesting depth is capped.
/// </summary>
internal sealed class Parser
{
    private readonly List<Token> _tokens;
    private readonly CalcLimits _limits;
    private int _pos;
    private int _depth;

    private Parser(List<Token> tokens, CalcLimits limits)
    {
        _tokens = tokens;
        _limits = limits;
    }

    public static Node Parse(string text, CalcLimits limits)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(limits);

        var tokens = new Lexer(text, limits).Tokenize();
        var parser = new Parser(tokens, limits);
        var node = parser.ParseConversion();

        if (parser.Current.Kind != TokenKind.End)
            throw new CalcParseException($"Unexpected '{parser.Current.Text}'.", parser.Current.Position);

        return node;
    }

    private Token Current => _tokens[_pos];

    private Token Advance() => _tokens[_pos++];

    private bool IsOperator(string text) => Current.Kind == TokenKind.Operator && Current.Text == text;

    private void Expect(TokenKind kind, string what)
    {
        if (Current.Kind != kind)
            throw new CalcParseException($"Expected {what} but found '{(Current.Kind == TokenKind.End ? "end of expression" : Current.Text)}'.", Current.Position);

        _pos++;
    }

    private void Enter()
    {
        if (++_depth > _limits.MaxNestingDepth)
            throw new CalcLimitException(nameof(CalcLimits.MaxNestingDepth), $"An expression nests at most {_limits.MaxNestingDepth} levels deep.");
    }

    private void Leave() => _depth--;

    private Node ParseConversion()
    {
        var node = ParseOr();

        while (IsOperator("in"))
        {
            var at = Advance().Position;

            if (Current.Kind != TokenKind.Identifier && Current.Kind != TokenKind.Number)
                throw new CalcParseException("Expected a unit after 'in'.", Current.Position);

            // The unit is written as one identifier-like token run; re-lex it greedily from the source position.
            var unitText = ReadUnitText();
            node = new ConvertNode(node, unitText, at);
        }

        return node;
    }

    private string ReadUnitText()
    {
        // Units after 'in' are lexed as identifiers and operators; stitch them back until a token that cannot be part of a unit.
        var parts = new System.Text.StringBuilder();
        var parenDepth = 0;

        while (true)
        {
            var t = Current;

            if (t.Kind == TokenKind.Identifier || (t.Kind == TokenKind.Number && parts.Length > 0))
            {
                parts.Append(t.Text);
                _pos++;
            }
            else if (t.Kind == TokenKind.Operator && (t.Text is "*" or "/" or "^") && parts.Length > 0)
            {
                parts.Append(t.Text);
                _pos++;

                if (t.Text == "^" && Current.Kind == TokenKind.Operator && Current.Text == "-")
                {
                    parts.Append('-');
                    _pos++;
                }
            }
            else if (t.Kind == TokenKind.LeftParen && (parts.Length == 0 || parts[^1] is '*' or '/'))
            {
                parts.Append('(');
                parenDepth++;
                _pos++;
            }
            else if (t.Kind == TokenKind.RightParen && parenDepth > 0)
            {
                parts.Append(')');
                parenDepth--;
                _pos++;
            }
            else
            {
                break;
            }
        }

        if (parts.Length == 0 || parenDepth != 0)
            throw new CalcParseException("Expected a unit after 'in'.", Current.Position);

        return parts.ToString();
    }

    private Node ParseOr()
    {
        var left = ParseAnd();

        while (IsOperator("or"))
        {
            var at = Advance().Position;
            left = new BinaryNode("or", left, ParseAnd(), at);
        }

        return left;
    }

    private Node ParseAnd()
    {
        var left = ParseNot();

        while (IsOperator("and"))
        {
            var at = Advance().Position;
            left = new BinaryNode("and", left, ParseNot(), at);
        }

        return left;
    }

    private Node ParseNot()
    {
        if (IsOperator("not"))
        {
            var at = Advance().Position;
            Enter();
            var operand = ParseNot();
            Leave();
            return new UnaryNode("not", operand, at);
        }

        return ParseComparison();
    }

    private Node ParseComparison()
    {
        var left = ParseAdditive();

        if (Current.Kind == TokenKind.Operator && Current.Text is "<" or "<=" or ">" or ">=" or "==" or "!=")
        {
            var op = Advance();
            var right = ParseAdditive();
            left = new BinaryNode(op.Text, left, right, op.Position);

            if (Current.Kind == TokenKind.Operator && Current.Text is "<" or "<=" or ">" or ">=" or "==" or "!=")
                throw new CalcParseException("Chain comparisons with 'and': write a < b and b < c.", Current.Position);
        }
        else if (IsOperator("="))
        {
            throw new CalcParseException("Use '==' to compare; '=' assigns a symbol.", Current.Position);
        }

        return left;
    }

    private Node ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Current.Kind == TokenKind.Operator && Current.Text is "+" or "-")
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseMultiplicative(), op.Position);
        }

        return left;
    }

    private Node ParseMultiplicative()
    {
        var left = ParseUnary();

        while (Current.Kind == TokenKind.Operator && Current.Text is "*" or "/" or "%")
        {
            var op = Advance();
            left = new BinaryNode(op.Text, left, ParseUnary(), op.Position);
        }

        return left;
    }

    private Node ParseUnary()
    {
        if (Current.Kind == TokenKind.Operator && Current.Text is "-" or "+")
        {
            var op = Advance();
            Enter();
            var operand = ParseUnary();
            Leave();
            return op.Text == "-" ? new UnaryNode("-", operand, op.Position) : operand;
        }

        return ParsePower();
    }

    private Node ParsePower()
    {
        var left = ParsePostfix();

        if (IsOperator("^"))
        {
            var op = Advance();
            Enter();
            var right = ParseUnary();
            Leave();
            return new BinaryNode("^", left, right, op.Position);
        }

        return left;
    }

    private Node ParsePostfix()
    {
        var node = ParsePrimary();

        while (Current.Kind == TokenKind.LeftBracket)
        {
            var at = Advance().Position;
            Enter();
            var indices = new List<Node> { ParseConversion() };

            if (Current.Kind == TokenKind.Comma)
            {
                _pos++;
                indices.Add(ParseConversion());
            }

            Leave();
            Expect(TokenKind.RightBracket, "']'");
            node = new IndexNode(node, indices, at);
        }

        return node;
    }

    private Node ParsePrimary()
    {
        var t = Current;

        switch (t.Kind)
        {
            case TokenKind.Number:
            {
                _pos++;
                var value = double.Parse(t.Text, System.Globalization.CultureInfo.InvariantCulture);
                return new NumberNode(value, t.Unit, t.Text, t.Position);
            }

            case TokenKind.Text:
                _pos++;
                return new TextNode(t.Text, t.Position);

            case TokenKind.Identifier:
            {
                _pos++;

                if (Current.Kind == TokenKind.LeftParen)
                {
                    _pos++;
                    Enter();
                    var args = new List<Node>();

                    if (Current.Kind != TokenKind.RightParen)
                    {
                        args.Add(ParseConversion());

                        while (Current.Kind == TokenKind.Comma)
                        {
                            _pos++;

                            if (args.Count >= _limits.MaxFunctionArguments)
                                throw new CalcLimitException(nameof(CalcLimits.MaxFunctionArguments), $"A function call takes at most {_limits.MaxFunctionArguments} arguments.");

                            args.Add(ParseConversion());
                        }
                    }

                    Leave();
                    Expect(TokenKind.RightParen, "')'");
                    return new CallNode(t.Text, args, t.Position);
                }

                return new IdentifierNode(t.Text, t.Position);
            }

            case TokenKind.Operator when t.Text is "true" or "false":
                _pos++;
                return new BoolNode(t.Text == "true", t.Position);

            case TokenKind.LeftParen:
            {
                _pos++;
                Enter();
                var inner = ParseConversion();
                Leave();
                Expect(TokenKind.RightParen, "')'");
                return inner;
            }

            case TokenKind.LeftBracket:
            {
                _pos++;
                Enter();
                var rows = new List<IReadOnlyList<Node>>();
                var row = new List<Node>();
                var elements = 0;

                if (Current.Kind != TokenKind.RightBracket)
                {
                    while (true)
                    {
                        row.Add(ParseConversion());

                        if (++elements > _limits.MaxArrayElements)
                            throw new CalcLimitException(nameof(CalcLimits.MaxArrayElements), $"An array literal holds at most {_limits.MaxArrayElements} elements.");

                        if (Current.Kind == TokenKind.Comma)
                        {
                            _pos++;
                            continue;
                        }

                        if (Current.Kind == TokenKind.Semicolon)
                        {
                            _pos++;
                            rows.Add(row);
                            row = [];
                            continue;
                        }

                        break;
                    }
                }

                if (row.Count > 0 || rows.Count == 0)
                    rows.Add(row);

                Leave();
                Expect(TokenKind.RightBracket, "']'");
                return new ArrayNode(rows, t.Position);
            }

            case TokenKind.End:
                throw new CalcParseException("The expression ends unexpectedly.", t.Position);

            default:
                throw new CalcParseException($"Unexpected '{t.Text}'.", t.Position);
        }
    }
}
