using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// A named unit of measurement for dimension <typeparamref name="TDimension"/>.
/// </summary>
/// <remarks>
/// Immutable, allocation-free value type (`ADR-0054`) — every instance is
/// constructed directly by its own consumer (typically one of the
/// per-dimension static catalogues, e.g. <see cref="LengthUnits"/>), never
/// resolved from the DI container. Two <see cref="Unit{TDimension}"/>
/// values are equal (record structure equality) only when both
/// <see cref="Symbol"/> and <see cref="ToBaseUnitFactor"/> match exactly —
/// no normalisation is performed.
/// </remarks>
public readonly record struct Unit<TDimension>
    where TDimension : IDimension
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Unit{TDimension}"/> struct.
    /// </summary>
    /// <param name="symbol">The unit's display symbol (e.g., "m", "kg", "N").</param>
    /// <param name="toBaseUnitFactor">The multiplicative factor converting one of this unit into this dimension's base unit.</param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="toBaseUnitFactor"/> is not a positive, finite number — a zero, negative, infinite, or
    /// <see cref="double.NaN"/> conversion factor describes no physically possible unit and fails loudly rather
    /// than being silently accepted (a Design Principle this framework's own controlling Work Package names).
    /// </exception>
    [JsonConstructor]
    public Unit(string symbol, double toBaseUnitFactor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (!double.IsFinite(toBaseUnitFactor) || toBaseUnitFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(toBaseUnitFactor), toBaseUnitFactor, "A unit's conversion factor must be a positive, finite number.");

        Symbol = symbol;
        ToBaseUnitFactor = toBaseUnitFactor;
    }

    /// <summary>The unit's display symbol (e.g., "m", "kg", "N").</summary>
    public string Symbol { get; }

    /// <summary>The multiplicative factor converting one of this unit into this dimension's base unit.</summary>
    public double ToBaseUnitFactor { get; }
}
