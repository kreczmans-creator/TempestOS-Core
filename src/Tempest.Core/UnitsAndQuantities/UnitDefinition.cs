using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// A named unit of measurement, carrying its own <see cref="Dimension"/>
/// at run time — the non-generic counterpart to <see cref="Unit{TDimension}"/>.
/// </summary>
/// <remarks>
/// `ADR-0147`. Where <see cref="Unit{TDimension}"/> relies on its own
/// generic parameter to say what dimension it belongs to,
/// <see cref="UnitDefinition"/> carries the answer as data
/// (<see cref="Dimension"/>), because <see cref="Quantity"/> arithmetic
/// needs to discover at run time whether two operands are combinable —
/// dividing a force by an area, for instance, where neither side's own
/// type declares "pressure" anywhere. See <see cref="Unit{TDimension}.UnitDefinition"/>,
/// the bridge every existing typed catalogue crosses to reach this type
/// without duplicating a single conversion factor.
/// </remarks>
public readonly record struct UnitDefinition
{
    /// <summary>
    /// Initialises a new instance of the <see cref="UnitDefinition"/> struct.
    /// </summary>
    /// <param name="symbol">The unit's display symbol (e.g., "m", "kg", "N").</param>
    /// <param name="dimension">The physical dimension this unit measures.</param>
    /// <param name="toBaseFactor">The multiplicative factor converting one of this unit into its dimension's base unit.</param>
    /// <param name="toBaseOffset">The offset, in base units, added after scaling by <paramref name="toBaseFactor"/> — zero for every ordinary multiplicative unit, non-zero only for a genuinely affine scale (`ADR-0125`).</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="toBaseFactor"/> is not a positive, finite number, or <paramref name="toBaseOffset"/> is not finite.
    /// </exception>
    [JsonConstructor]
    public UnitDefinition(string symbol, Dimension dimension, double toBaseFactor, double toBaseOffset = 0.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (!double.IsFinite(toBaseFactor) || toBaseFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(toBaseFactor), toBaseFactor, "A unit's conversion factor must be a positive, finite number.");
        if (!double.IsFinite(toBaseOffset))
            throw new ArgumentOutOfRangeException(nameof(toBaseOffset), toBaseOffset, "A unit's conversion offset must be a finite number.");

        Symbol = symbol;
        Dimension = dimension;
        ToBaseFactor = toBaseFactor;
        ToBaseOffset = toBaseOffset;
    }

    /// <summary>The unit's display symbol (e.g., "m", "kg", "N").</summary>
    public string Symbol { get; }

    /// <summary>The physical dimension this unit measures.</summary>
    public Dimension Dimension { get; }

    /// <summary>The multiplicative factor converting one of this unit into its dimension's base unit.</summary>
    public double ToBaseFactor { get; }

    /// <summary>The offset, in base units, added after scaling by <see cref="ToBaseFactor"/>. Zero for every ordinary multiplicative unit.</summary>
    public double ToBaseOffset { get; }

    /// <summary>Whether this unit sits on an affine scale — one whose zero is not its dimension's own zero.</summary>
    public bool IsAffine => ToBaseOffset != 0.0;

    /// <summary>Converts <paramref name="value"/>, expressed in this unit, into its dimension's base unit.</summary>
    public double ToBase(double value) => (value * ToBaseFactor) + ToBaseOffset;

    /// <summary>Converts <paramref name="baseValue"/>, expressed in this dimension's base unit, into this unit.</summary>
    public double FromBase(double baseValue) => (baseValue - ToBaseOffset) / ToBaseFactor;
}
