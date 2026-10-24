namespace Tempest.Calc.Engine;

/// <summary>The base of every failure the engine reports. The message is safe to show an engineer; it never echoes more than a short fragment of the offending text.</summary>
public class CalcException : Exception
{
    public CalcException(string message) : base(message) { }

    public CalcException(string message, Exception inner) : base(message, inner) { }

    public CalcException() { }
}

/// <summary>An expression could not be read.</summary>
public sealed class CalcParseException : CalcException
{
    public int Position { get; }

    public CalcParseException(string message, int position) : base(message) => Position = position;

    public CalcParseException(string message) : base(message) { }

    public CalcParseException() { }

    public CalcParseException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>A unit could not be read or combined.</summary>
public sealed class CalcUnitException : CalcException
{
    public CalcUnitException(string message) : base(message) { }

    public CalcUnitException() { }

    public CalcUnitException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>Two operands do not share the dimension the operation needs.</summary>
public sealed class CalcDimensionException : CalcException
{
    public CalcDimensionException(string message) : base(message) { }

    public CalcDimensionException() { }

    public CalcDimensionException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>An expression evaluated to something invalid (division by zero, a non-finite result, an index out of range, an unknown symbol).</summary>
public sealed class CalcEvaluationException : CalcException
{
    public CalcEvaluationException(string message) : base(message) { }

    public CalcEvaluationException() { }

    public CalcEvaluationException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>A resource cap was reached: expression length, nesting depth, step count, array size, iterations.</summary>
public sealed class CalcLimitException : CalcException
{
    public string Limit { get; } = string.Empty;

    public CalcLimitException(string limit, string message) : base(message) => Limit = limit;

    public CalcLimitException(string message) : base(message) { }

    public CalcLimitException() { }

    public CalcLimitException(string message, Exception inner) : base(message, inner) { }
}
