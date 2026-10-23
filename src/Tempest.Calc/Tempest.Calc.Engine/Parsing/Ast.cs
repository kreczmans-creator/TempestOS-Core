namespace Tempest.Calc.Engine.Parsing;

internal abstract record Node(int Position);

internal sealed record NumberNode(double Value, string? Unit, string Literal, int Position) : Node(Position);

internal sealed record TextNode(string Value, int Position) : Node(Position);

internal sealed record BoolNode(bool Value, int Position) : Node(Position);

internal sealed record IdentifierNode(string Name, int Position) : Node(Position);

internal sealed record UnaryNode(string Operator, Node Operand, int Position) : Node(Position);

internal sealed record BinaryNode(string Operator, Node Left, Node Right, int Position) : Node(Position);

internal sealed record ConvertNode(Node Operand, string Unit, int Position) : Node(Position);

internal sealed record CallNode(string Name, IReadOnlyList<Node> Arguments, int Position) : Node(Position);

internal sealed record IndexNode(Node Target, IReadOnlyList<Node> Indices, int Position) : Node(Position);

/// <summary>A literal array: one row is a vector, several rows a matrix.</summary>
internal sealed record ArrayNode(IReadOnlyList<IReadOnlyList<Node>> Rows, int Position) : Node(Position);
