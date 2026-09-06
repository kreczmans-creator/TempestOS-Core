using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// A named unit of measurement for dimension <typeparamref name="TDimension"/>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, allocation-free value type (`ADR-0054`) — every instance is
/// constructed directly by its own consumer (typically one of the
/// per-dimension static catalogues, e.g. <see cref="LengthUnits"/>), never
/// resolved from the DI container. Two <see cref="Unit{TDimension}"/>
/// values are equal (record structure equality) only when
/// <see cref="Symbol"/>, <see cref="ToBaseUnitFactor"/> and
/// <see cref="ToBaseUnitOffset"/> all match exactly — no normalisation is
/// performed.
/// </para>
/// <para>
/// <b>Affine units</b> (`ADR-0125`, closing `FCR-0034`). Most units are
/// purely multiplicative: one kilonewton is one thousand newtons, and zero
/// of either is the same zero. A temperature scale is not — zero degrees
/// Celsius is 273.15 kelvin, not zero kelvin — so an affine unit needs an
/// offset as well as a factor. <see cref="ToBaseUnitOffset"/> supplies it,
/// defaulting to zero, so every unit written before this existed behaves
/// exactly as it did and deserialises unchanged.
/// </para>
/// <para>
/// An affine unit's own arithmetic is deliberately restricted — see
/// <see cref="IsAffine"/> and <see cref="Quantity{TDimension}"/>'s own
/// operators. Adding two Celsius readings is not a temperature, and this
/// framework refuses rather than returning a number that looks like one.
/// </para>
/// </remarks>
public readonly record struct Unit<TDimension>
    where TDimension : IDimension
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Unit{TDimension}"/> struct.
    /// </summary>
    /// <param name="symbol">The unit's display symbol (e.g., "m", "kg", "N").</param>
    /// <param name="toBaseUnitFactor">The multiplicative factor converting one of this unit into this dimension's base unit.</param>
    /// <param name="toBaseUnitOffset">
    /// The offset, in base units, added after scaling by
    /// <paramref name="toBaseUnitFactor"/> — zero for every ordinary
    /// multiplicative unit, and non-zero only for a genuinely affine scale
    /// such as degrees Celsius (`ADR-0125`).
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="symbol"/> is empty or consists only of whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="symbol"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="toBaseUnitFactor"/> is not a positive, finite number — a zero, negative, infinite, or
    /// <see cref="double.NaN"/> conversion factor describes no physically possible unit and fails loudly rather
    /// than being silently accepted (a Design Principle this framework's own controlling Work Package names);
    /// or <paramref name="toBaseUnitOffset"/> is not finite.
    /// </exception>
    [JsonConstructor]
    public Unit(string symbol, double toBaseUnitFactor, double toBaseUnitOffset = 0.0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        if (!double.IsFinite(toBaseUnitFactor) || toBaseUnitFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(toBaseUnitFactor), toBaseUnitFactor, "A unit's conversion factor must be a positive, finite number.");
        if (!double.IsFinite(toBaseUnitOffset))
            throw new ArgumentOutOfRangeException(nameof(toBaseUnitOffset), toBaseUnitOffset, "A unit's conversion offset must be a finite number.");

        Symbol = symbol;
        ToBaseUnitFactor = toBaseUnitFactor;
        ToBaseUnitOffset = toBaseUnitOffset;
    }

    /// <summary>The unit's display symbol (e.g., "m", "kg", "N").</summary>
    public string Symbol { get; }

    /// <summary>The multiplicative factor converting one of this unit into this dimension's base unit.</summary>
    public double ToBaseUnitFactor { get; }

    /// <summary>The offset, in base units, added after scaling by <see cref="ToBaseUnitFactor"/>. Zero for every ordinary multiplicative unit.</summary>
    public double ToBaseUnitOffset { get; }

    /// <summary>
    /// Whether this unit sits on an affine scale — one whose zero is not
    /// the dimension's own zero. Affine quantities may be converted and
    /// compared but not added, subtracted or scaled.
    /// </summary>
    public bool IsAffine => ToBaseUnitOffset != 0.0;

    /// <summary>Converts <paramref name="value"/>, expressed in this unit, into this dimension's base unit.</summary>
    public double ToBase(double value) => (value * ToBaseUnitFactor) + ToBaseUnitOffset;

    /// <summary>Converts <paramref name="baseValue"/>, expressed in this dimension's base unit, into this unit.</summary>
    public double FromBase(double baseValue) => (baseValue - ToBaseUnitOffset) / ToBaseUnitFactor;
}
