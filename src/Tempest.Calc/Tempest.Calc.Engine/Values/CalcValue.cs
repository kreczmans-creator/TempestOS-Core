using System.Globalization;
using System.Text;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Engine.Values;

/// <summary>
/// A value the engine computes with. Every numeric value is held in
/// coherent SI with its dimension; the display unit only governs how it is
/// shown and how a step's substitution reads. Immutable.
/// </summary>
public abstract record CalcValue
{
    /// <summary>The dimension of every numeric value; <see cref="Dimension.None"/> for a boolean.</summary>
    public abstract Dimension Dimension { get; }

    /// <summary>The unit this value is displayed in.</summary>
    public abstract Unit DisplayUnit { get; }

    /// <summary>The same value shown in <paramref name="unit"/>, which must measure the same dimension.</summary>
    public abstract CalcValue In(Unit unit);

    /// <summary>The value as text with its display unit, formatted to <paramref name="significantFigures"/>.</summary>
    public abstract string Format(int significantFigures = 5);

    public override string ToString() => Format();

    /// <summary>Formats a number to <paramref name="sig"/> significant figures, trailing zeros trimmed, invariant culture, never "NaN" without saying so.</summary>
    public static string FormatNumber(double value, int sig = 5)
    {
        if (double.IsNaN(value))
            return "undefined";

        if (double.IsPositiveInfinity(value))
            return "∞";

        if (double.IsNegativeInfinity(value))
            return "−∞";

        if (value == 0)
            return "0";

        sig = Math.Clamp(sig, 1, 15);
        var magnitude = Math.Floor(Math.Log10(Math.Abs(value)));

        if (magnitude >= 15 || magnitude < -6)
            return value.ToString("0." + new string('#', sig - 1) + "e+0", CultureInfo.InvariantCulture);

        var decimals = (int)Math.Max(0, sig - 1 - magnitude);
        double rounded;

        if (sig - 1 - magnitude < 0)
        {
            var scale = Math.Pow(10, magnitude - sig + 1);
            rounded = Math.Round(value / scale, MidpointRounding.ToEven) * scale;
        }
        else
        {
            rounded = Math.Round(value, Math.Min(decimals, 15), MidpointRounding.ToEven);
        }

        if (rounded == 0)
            return "0";

        var text = rounded.ToString("F" + Math.Min(decimals, 15).ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

        if (text.Contains('.', StringComparison.Ordinal))
            text = text.TrimEnd('0').TrimEnd('.');

        return text == "-0" ? "0" : text;
    }

    internal static string WithUnit(string number, Unit unit) => unit.Symbol.Length == 0 ? number : $"{number} {unit.Symbol}";
}

/// <summary>A single number with a dimension.</summary>
public sealed record ScalarValue : CalcValue
{
    public ScalarValue(double si, Dimension dimension, Unit? displayUnit = null)
    {
        Si = si;
        Dimension = dimension;
        DisplayUnit = displayUnit is { } u && u.Dimension == dimension ? u : UnitCatalog.PreferredFor(dimension);
    }

    /// <summary>A dimensionless number.</summary>
    public ScalarValue(double value) : this(value, Dimension.None, Unit.One) { }

    /// <summary>A number read in <paramref name="unit"/>, stored in SI.</summary>
    public static ScalarValue Of(double value, Unit unit) => new(unit.ToSi(value), unit.Dimension, unit);

    public static ScalarValue Of(double value, string unit) => Of(value, UnitCatalog.Parse(unit));

    /// <summary>The value in coherent SI.</summary>
    public double Si { get; }

    public override Dimension Dimension { get; }

    public override Unit DisplayUnit { get; }

    /// <summary>The value in its display unit.</summary>
    public double Displayed => DisplayUnit.FromSi(Si);

    public bool IsFinite => double.IsFinite(Si);

    public override CalcValue In(Unit unit)
    {
        if (unit.Dimension != Dimension)
            throw new CalcDimensionException($"Cannot show a value of dimension [{Dimension}] in '{unit.Symbol}' [{unit.Dimension}].");

        return new ScalarValue(Si, Dimension, unit);
    }

    public override string Format(int significantFigures = 5) => WithUnit(FormatNumber(Displayed, significantFigures), DisplayUnit);
}

/// <summary>A one-dimensional array sharing one dimension and display unit. Indexed from 1 in expressions.</summary>
public sealed record VectorValue : CalcValue
{
    private readonly double[] _si;

    public VectorValue(IReadOnlyList<double> si, Dimension dimension, Unit? displayUnit = null)
    {
        ArgumentNullException.ThrowIfNull(si);
        _si = [.. si];
        Dimension = dimension;
        DisplayUnit = displayUnit is { } u && u.Dimension == dimension ? u : UnitCatalog.PreferredFor(dimension);
    }

    public static VectorValue Of(IReadOnlyList<double> values, Unit unit) => new(values.Select(unit.ToSi).ToArray(), unit.Dimension, unit);

    public static VectorValue Of(IReadOnlyList<double> values, string unit) => Of(values, UnitCatalog.Parse(unit));

    public IReadOnlyList<double> Si => _si;

    public int Length => _si.Length;

    public override Dimension Dimension { get; }

    public override Unit DisplayUnit { get; }

    public IReadOnlyList<double> Displayed => _si.Select(DisplayUnit.FromSi).ToArray();

    public ScalarValue this[int index] => new(_si[index], Dimension, DisplayUnit);

    public override CalcValue In(Unit unit)
    {
        if (unit.Dimension != Dimension)
            throw new CalcDimensionException($"Cannot show a vector of dimension [{Dimension}] in '{unit.Symbol}'.");

        return new VectorValue(_si, Dimension, unit);
    }

    public override string Format(int significantFigures = 5)
    {
        var sb = new StringBuilder("[");
        var shown = Math.Min(_si.Length, 12);

        for (var i = 0; i < shown; i++)
        {
            if (i > 0)
                sb.Append(", ");

            sb.Append(FormatNumber(DisplayUnit.FromSi(_si[i]), significantFigures));
        }

        if (_si.Length > shown)
            sb.Append(CultureInfo.InvariantCulture, $", … ({_si.Length} items)");

        sb.Append(']');
        return WithUnit(sb.ToString(), DisplayUnit);
    }

    public bool Equals(VectorValue? other) => other is not null && Dimension == other.Dimension && _si.AsSpan().SequenceEqual(other._si);

    public override int GetHashCode() => HashCode.Combine(Dimension, _si.Length);
}

/// <summary>A dense two-dimensional array sharing one dimension and display unit. Indexed from 1 in expressions.</summary>
public sealed record MatrixValue : CalcValue
{
    private readonly double[,] _si;

    public MatrixValue(double[,] si, Dimension dimension, Unit? displayUnit = null)
    {
        ArgumentNullException.ThrowIfNull(si);
        _si = (double[,])si.Clone();
        Dimension = dimension;
        DisplayUnit = displayUnit is { } u && u.Dimension == dimension ? u : UnitCatalog.PreferredFor(dimension);
    }

    public int Rows => _si.GetLength(0);

    public int Columns => _si.GetLength(1);

    public override Dimension Dimension { get; }

    public override Unit DisplayUnit { get; }

    public double Si(int row, int column) => _si[row, column];

    public double[,] ToSiArray() => (double[,])_si.Clone();

    public ScalarValue this[int row, int column] => new(_si[row, column], Dimension, DisplayUnit);

    public override CalcValue In(Unit unit)
    {
        if (unit.Dimension != Dimension)
            throw new CalcDimensionException($"Cannot show a matrix of dimension [{Dimension}] in '{unit.Symbol}'.");

        return new MatrixValue(_si, Dimension, unit);
    }

    public override string Format(int significantFigures = 5)
    {
        var sb = new StringBuilder("[");
        var rows = Math.Min(Rows, 6);
        var cols = Math.Min(Columns, 6);

        for (var r = 0; r < rows; r++)
        {
            if (r > 0)
                sb.Append("; ");

            for (var c = 0; c < cols; c++)
            {
                if (c > 0)
                    sb.Append(", ");

                sb.Append(FormatNumber(DisplayUnit.FromSi(_si[r, c]), significantFigures));
            }

            if (Columns > cols)
                sb.Append(", …");
        }

        if (Rows > rows)
            sb.Append("; …");

        sb.Append(CultureInfo.InvariantCulture, $"] ({Rows}×{Columns})");
        return WithUnit(sb.ToString(), DisplayUnit);
    }

    public bool Equals(MatrixValue? other)
    {
        if (other is null || Dimension != other.Dimension || Rows != other.Rows || Columns != other.Columns)
            return false;

        for (var r = 0; r < Rows; r++)
        {
            for (var c = 0; c < Columns; c++)
            {
                if (_si[r, c] != other._si[r, c])
                    return false;
            }
        }

        return true;
    }

    public override int GetHashCode() => HashCode.Combine(Dimension, Rows, Columns);
}

/// <summary>A truth value from a comparison or a logical operator.</summary>
public sealed record BoolValue(bool Value) : CalcValue
{
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    public override Dimension Dimension => Dimension.None;

    public override Unit DisplayUnit => Unit.One;

    public override CalcValue In(Unit unit) => throw new CalcDimensionException("A truth value has no unit.");

    public override string Format(int significantFigures = 5) => Value ? "true" : "false";
}

/// <summary>A short text, used for choices (for example a fit letter or a profile name). Never evaluated.</summary>
public sealed record TextValue(string Value) : CalcValue
{
    public override Dimension Dimension => Dimension.None;

    public override Unit DisplayUnit => Unit.One;

    public override CalcValue In(Unit unit) => throw new CalcDimensionException("A text has no unit.");

    public override string Format(int significantFigures = 5) => $"\"{Value}\"";
}
