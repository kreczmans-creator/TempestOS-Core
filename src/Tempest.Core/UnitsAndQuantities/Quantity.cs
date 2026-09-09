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
/// <b>Same-dimension automatic conversion (`ADR-0147`).</b> Every
/// arithmetic operator (<c>+</c>, <c>-</c>), every comparison operator
/// (<c>&lt;</c>, <c>&gt;</c>, and so on) and equality convert automatically
/// between units of the same <typeparamref name="TDimension"/> — through
/// the dimension's own base unit, and for <c>+</c>/<c>-</c> back to the
/// left operand's own unit. 5 m and 500 cm are equal by <c>==</c>, and
/// <c>new Quantity&lt;Length&gt;(5, Metre) + new Quantity&lt;Length&gt;(500, Centimetre)</c>
/// returns 10 m, with no explicit <see cref="ConvertTo"/> required first.
/// This reverses `ADR-0054`'s original "exact same unit only" rule, which
/// this framework's own experience writing calculation code against this
/// type found added ceremony without adding safety — <typeparamref name="TDimension"/>
/// already guarantees dimensional compatibility at compile time, and unit
/// mismatch within a dimension is exactly the case a physical measurement
/// framework exists to resolve, not to refuse. Only a genuinely
/// cross-dimension combination remains impossible, and it remains
/// impossible at compile time via <typeparamref name="TDimension"/> itself
/// — <see cref="IncompatibleUnitsException"/> is no longer reachable from
/// these operators at all, and now guards only the affine-arithmetic
/// refusal below.
/// </para>
/// <para>
/// <b>Affine arithmetic is still refused (`ADR-0125`).</b> A quantity
/// expressed in an affine unit (degrees Celsius, degrees Fahrenheit)
/// cannot be added, subtracted or scaled — see <see cref="IsAffine"/>'s own
/// remarks on <see cref="Unit{TDimension}"/> — even though it can now be
/// added to or compared against a quantity in a <em>different</em>
/// non-affine unit of the same dimension. This is unchanged from
/// `ADR-0125`; only the exact-unit-match rule around it has widened.
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
    /// <remarks>
    /// Converts through the dimension's own base unit, honouring each
    /// unit's own offset as well as its factor, so an affine scale
    /// (degrees Celsius, `ADR-0125`) converts correctly in both
    /// directions. For the ordinary multiplicative case, where both
    /// offsets are zero, this is exactly the factor-only arithmetic it
    /// has always been.
    /// </remarks>
    public Quantity<TDimension> ConvertTo(Unit<TDimension> targetUnit)
    {
        if (Unit == targetUnit)
            return this;

        return new Quantity<TDimension>(targetUnit.FromBase(Unit.ToBase(Value)), targetUnit);
    }

    /// <summary>
    /// This quantity's own value expressed in its dimension's base unit —
    /// the form two quantities recorded in different units must be
    /// compared in.
    /// </summary>
    public double BaseValue => Unit.ToBase(Value);

    /// <summary>Adds two quantities of the same dimension, converting <paramref name="right"/> into <paramref name="left"/>'s own unit automatically if they differ.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/>'s own unit sits on an affine scale.</exception>
    public static Quantity<TDimension> operator +(Quantity<TDimension> left, Quantity<TDimension> right)
    {
        RequireNotAffine(left.Unit, "added");
        RequireNotAffine(right.Unit, "added");
        return new Quantity<TDimension>(left.Value + ValueInLeftUnit(left, right), left.Unit);
    }

    /// <summary>Subtracts two quantities of the same dimension, converting <paramref name="right"/> into <paramref name="left"/>'s own unit automatically if they differ.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/>'s own unit sits on an affine scale.</exception>
    public static Quantity<TDimension> operator -(Quantity<TDimension> left, Quantity<TDimension> right)
    {
        RequireNotAffine(left.Unit, "subtracted");
        RequireNotAffine(right.Unit, "subtracted");
        return new Quantity<TDimension>(left.Value - ValueInLeftUnit(left, right), left.Unit);
    }

    private static double ValueInLeftUnit(Quantity<TDimension> left, Quantity<TDimension> right) =>
        left.Unit == right.Unit ? right.Value : left.Unit.FromBase(right.Unit.ToBase(right.Value));

    /// <summary>Scales a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator *(Quantity<TDimension> quantity, double scalar)
    {
        RequireNotAffine(quantity.Unit, "scaled");
        return new Quantity<TDimension>(quantity.Value * scalar, quantity.Unit);
    }

    /// <summary>Scales a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator *(double scalar, Quantity<TDimension> quantity) =>
        quantity * scalar;

    /// <summary>Divides a quantity by a dimensionless factor, preserving its unit.</summary>
    public static Quantity<TDimension> operator /(Quantity<TDimension> quantity, double scalar)
    {
        RequireNotAffine(quantity.Unit, "scaled");
        return new Quantity<TDimension>(quantity.Value / scalar, quantity.Unit);
    }

    /// <inheritdoc />
    /// <remarks>Compares <see cref="BaseValue"/> — two quantities of this same, compile-time-guaranteed dimension are always comparable regardless of which unit either is expressed in (`ADR-0147`).</remarks>
    public int CompareTo(Quantity<TDimension> other) => BaseValue.CompareTo(other.BaseValue);

    /// <summary>Returns whether <paramref name="left"/> is less than <paramref name="right"/>.</summary>
    public static bool operator <(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) < 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    public static bool operator >(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) > 0;

    /// <summary>Returns whether <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) <= 0;

    /// <summary>Returns whether <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(Quantity<TDimension> left, Quantity<TDimension> right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Returns whether this quantity and <paramref name="other"/> represent
    /// the same physical magnitude, regardless of which unit either is
    /// expressed in (`ADR-0147`) — 5 m and 500 cm are equal.
    /// </summary>
    /// <remarks>Compares <see cref="BaseValue"/>, replacing the record structure's own field-by-field synthesized equality.</remarks>
    public bool Equals(Quantity<TDimension> other) => BaseValue.Equals(other.BaseValue);

    /// <inheritdoc cref="Equals(Quantity{TDimension})" />
    public override int GetHashCode() => BaseValue.GetHashCode();

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

    /// <summary>
    /// Refuses arithmetic that has no meaning on an affine scale
    /// (`ADR-0125`). Twenty degrees Celsius plus five degrees Celsius is
    /// not twenty-five degrees Celsius — the operands are positions on a
    /// scale, not magnitudes — and this framework fails loudly rather
    /// than returning a number that looks like an answer.
    /// </summary>
    private static void RequireNotAffine(Unit<TDimension> unit, string operation)
    {
        if (unit.IsAffine)
            throw new IncompatibleUnitsException(
                $"A quantity expressed in '{unit.Symbol}' cannot be {operation}: that unit sits on an affine scale, where the operation has no physical meaning. Convert to an absolute unit of the same dimension first.");
    }

    /// <summary>
    /// This quantity, re-expressed as a non-generic <see cref="UnitsAndQuantities.Quantity"/> —
    /// the same value and unit, carrying <typeparamref name="TDimension"/>'s
    /// own runtime <see cref="IDimension.Vector"/> rather than the
    /// compile-time type parameter itself.
    /// </summary>
    /// <remarks>`ADR-0147`. The typed-to-runtime half of the facade; see <see cref="FromQuantity"/> for the other direction.</remarks>
    public Quantity ToQuantity() => new(Value, Unit.UnitDefinition);

    /// <summary>
    /// Re-expresses a non-generic <see cref="UnitsAndQuantities.Quantity"/> as
    /// this typed facade, provided its own runtime dimension matches
    /// <typeparamref name="TDimension"/>'s own declared <see cref="IDimension.Vector"/>.
    /// </summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="quantity"/>'s own <see cref="UnitDefinition.Dimension"/> does not equal <typeparamref name="TDimension"/>'s own <see cref="IDimension.Vector"/>.</exception>
    public static Quantity<TDimension> FromQuantity(Quantity quantity)
    {
        if (quantity.Unit.Dimension != TDimension.Vector)
            throw new IncompatibleUnitsException(
                $"Cannot treat a quantity of dimension '{quantity.Unit.Dimension}' as {typeof(TDimension).Name} (dimension '{TDimension.Vector}'): the runtime dimension does not match.");

        return new Quantity<TDimension>(quantity.Value, new Unit<TDimension>(quantity.Unit.Symbol, quantity.Unit.ToBaseFactor, quantity.Unit.ToBaseOffset));
    }
}
