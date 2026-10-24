namespace Tempest.Calc.Engine.Units;

/// <summary>
/// A unit of measure: a symbol, the dimension it measures, the factor that
/// converts one of it into coherent SI, and — for the affine temperature
/// scales only — an offset. <c>si = value * Factor + Offset</c>.
/// </summary>
public sealed record Unit(string Symbol, Dimension Dimension, double Factor, double Offset = 0.0)
{
    /// <summary>The dimensionless unit "1".</summary>
    public static readonly Unit One = new("", Dimension.None, 1.0);

    public bool IsAffine => Offset != 0.0;

    public double ToSi(double value) => value * Factor + Offset;

    public double FromSi(double si) => (si - Offset) / Factor;

    /// <summary>Whether <paramref name="other"/> measures the same dimension.</summary>
    public bool IsCompatibleWith(Unit other) => Dimension == other.Dimension;

    public override string ToString() => Symbol;
}
