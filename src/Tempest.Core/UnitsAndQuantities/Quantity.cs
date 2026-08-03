using System.Globalization;
using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// An immutable numeric value paired with a unit of dimension <typeparamref name="TDimension"/>.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, allocation-free value type (`ADR-0054`) — constructed
/// directly by its own consumer, never resolved from the DI container, and
/// carrying no logger (mirrors <c>CommandResult</c>/<c>LicenseValidationResult</c>'s
/// own "not every public type is a DI-registered service" precedent).
/// </para>
/// <para>
/// <b>Never performs an implicit unit conversion.</b> Every arithmetic
/// operator (<c>+</c>, <c>-</c>) and every comparison operator (<c>&lt;</c>,
/// <c>&gt;</c>, and so on) requires both operands to share the exact same
/// <see cref="Unit"/> — not merely the same <typeparamref name="TDimension"/>
/// — throwing <see cref="IncompatibleUnitsException"/> otherwise. A caller
/// combining quantities expressed in different units of the same dimension
/// (5 m and 500 cm) must call <see cref="ConvertTo"/> explicitly first.
/// Equality (inherited record structure equality, comparing <see cref="Value"/>
/// and <see cref="Unit"/> exactly) follows the same rule: 5 m and 500 cm are
/// <em>not</em> equal by <c>==</c>, for the identical reason.
/// </para>
/// </remarks>
public readonly record struct Quantity<TDimension> : IComparable<Quantity<TDimension>>, IFormattable
    where TDimension : IDimension
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Quantity{TDimension}"/> struct.
    /// </summary>
    /// <param name="value">The numeric value, expressed in <paramref name="unit"/>.</param>
    /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is <see cref="double.NaN"/> or infinite. Zero and negative values are
    /// legitimate physical quantities and are accepted; only a non-finite value describes no physically
    /// possible quantity and fails loudly rather than being silently accepted.
    /// </exception>
    [JsonConstructor]
    public Quantity(double value, Unit<TDimension> unit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "A quantity's value must be a finite number.");

        Value = value;
        Unit = unit;
    }

    /// <summary>The numeric value, expressed in <see cref="Unit"/>.</summary>
    public double Value { get; }

    /// <summary>The unit <see cref="Value"/> is expressed in.</summary>
    public Unit<TDimension> Unit { get; }

    /// <summary>Returns an equivalent quantity expressed in <paramref name="targetUnit"/>.</summary>
    /// <param name="targetUnit">The unit to convert to.</param>
    public Quantity<TDimension> ConvertTo(Unit<TDimension> targetUnit)
    {
        if (Unit == targetUnit)
            return this;

        var baseValue = Value * Unit.ToBaseUnitFactor;
        var convertedValue = baseValue / targetUnit.ToBaseUnitFactor;
        return new Quantity<TDimension>(convertedValue, targetUnit);
    }

    /// <summary>Adds two quantities expressed in the exact same <see cref="Unit"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static Quantity<TDimension> operator +(Quantity<TDimension> left, Quantity<TDimension> right)
    {
        RequireSameUnit(left, right);
        return new Quantity<TDimension>(left.Value + right.Value, left.Unit);
    }

    /// <summary>Subtracts two quantities expressed in the exact same <see cref="Unit"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static Quantity<TDimension> operator -(Quantity<TDimension> left, Quantity<TDimension> right)
    {
        RequireSameUnit(left, right);
        return new Quantity<TDimension>(left.Value - right.Value, left.Unit);
    }

    /// <summary>Scales a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator *(Quantity<TDimension> quantity, double scalar) =>
        new(quantity.Value * scalar, quantity.Unit);

    /// <summary>Scales a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator *(double scalar, Quantity<TDimension> quantity) =>
        quantity * scalar;

    /// <summary>Divides a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator /(Quantity<TDimension> quantity, double scalar) =>
        new(quantity.Value / scalar, quantity.Unit);

    /// <inheritdoc />
    /// <exception cref="IncompatibleUnitsException">This instance and <paramref name="other"/> do not share the exact same <see cref="Unit"/>.</exception>
    public int CompareTo(Quantity<TDimension> other)
    {
        RequireSameUnit(this, other);
        return Value.CompareTo(other.Value);
    }

    /// <summary>Returns whether <paramref name="left"/> is less than <paramref name="right"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static bool operator <(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) < 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static bool operator >(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) > 0;

    /// <summary>Returns whether <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static bool operator <=(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) <= 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the exact same <see cref="Unit"/>.</exception>
    public static bool operator >=(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) >= 0;

    /// <inheritdoc />
    public override string ToString() => ToString(format: null, formatProvider: null);

    /// <inheritdoc />
    /// <remarks>Culture-invariant regardless of <paramref name="formatProvider"/>'s own numeric conventions — see <c>Engineering Principles.md</c>, "Conversion is deterministic."</remarks>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"{Value.ToString(format, CultureInfo.InvariantCulture)} {Unit.Symbol}";

    /// <summary>
    /// Parses a "&lt;number&gt; &lt;symbol&gt;" formatted quantity (e.g. "5 m"), matching <paramref name="knownUnits"/> by exact <see cref="Unit{TDimension}.Symbol"/>.
    /// </summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="knownUnits">The units eligible to match <paramref name="input"/>'s own symbol (typically a per-dimension catalogue's own <c>All</c> property).</param>
    /// <exception cref="FormatException"><paramref name="input"/> is not a recognised "&lt;number&gt; &lt;symbol&gt;" quantity for a symbol present in <paramref name="knownUnits"/>.</exception>
    public static Quantity<TDimension> Parse(string input, IReadOnlyList<Unit<TDimension>> knownUnits) =>
        TryParse(input, knownUnits, out var result)
            ? result
            : throw new FormatException($"'{input}' is not a recognised quantity.");

    /// <summary>
    /// Attempts to parse a "&lt;number&gt; &lt;symbol&gt;" formatted quantity (e.g. "5 m"), matching <paramref name="knownUnits"/> by exact <see cref="Unit{TDimension}.Symbol"/>.
    /// </summary>
    /// <param name="input">The text to parse.</param>
    /// <param name="knownUnits">The units eligible to match <paramref name="input"/>'s own symbol (typically a per-dimension catalogue's own <c>All</c> property).</param>
    /// <param name="result">The parsed quantity, if parsing succeeded; otherwise, the default value.</param>
    public static bool TryParse(string? input, IReadOnlyList<Unit<TDimension>> knownUnits, out Quantity<TDimension> result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        var trimmed = input.AsSpan().Trim();
        var separatorIndex = trimmed.LastIndexOf(' ');
        if (separatorIndex < 0)
            return false;

        var numberPart = trimmed[..separatorIndex];
        var symbolPart = trimmed[(separatorIndex + 1)..].Trim();

        if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            return false;

        foreach (var unit in knownUnits)
        {
            if (symbolPart.Equals(unit.Symbol, StringComparison.Ordinal))
            {
                result = new Quantity<TDimension>(value, unit);
                return true;
            }
        }

        return false;
    }

    private static void RequireSameUnit(Quantity<TDimension> left, Quantity<TDimension> right)
    {
        if (left.Unit != right.Unit)
            throw new IncompatibleUnitsException(
                $"Cannot combine a quantity expressed in '{left.Unit.Symbol}' with one expressed in '{right.Unit.Symbol}' without an explicit {nameof(ConvertTo)} first.");
    }
}
