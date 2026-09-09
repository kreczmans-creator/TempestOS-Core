using System.Globalization;
using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// An immutable numeric value paired with a <see cref="UnitDefinition"/> —
/// the non-generic, runtime-dimensioned counterpart to
/// <see cref="Quantity{TDimension}"/>.
/// </summary>
/// <remarks>
/// <para>
/// `ADR-0147`. Where <see cref="Quantity{TDimension}"/> can only ever be
/// combined with another quantity of the exact same compile-time
/// <c>TDimension</c>, this type carries its own <see cref="Dimension"/> at
/// run time (via <see cref="UnitDefinition.Dimension"/>) precisely so it
/// <em>can</em> be multiplied and divided across dimensions — a force
/// divided by an area becomes a pressure, discovered at run time, with
/// nothing declaring in advance that force-over-area is a pressure.
/// </para>
/// <para>
/// <b>Same-dimension automatic conversion.</b> Unlike
/// <see cref="Quantity{TDimension}"/>'s own original "exact same unit
/// only" rule (`ADR-0054`), <c>+</c>, <c>-</c>, every comparison operator,
/// and equality between two quantities of the <em>same</em> dimension
/// convert automatically — through the dimension's own base unit and back
/// to the left operand's own unit for <c>+</c>/<c>-</c> — rather than
/// throwing. Two quantities of <em>different</em> dimensions still throw
/// <see cref="IncompatibleUnitsException"/> for every one of those
/// operators, including equality: this type's own <c>==</c> can throw,
/// which is not the ordinary <see cref="object.Equals(object?)"/> contract
/// — a deliberate, disclosed choice (`ADR-0147`) continuing this
/// framework's existing "fail loudly rather than return a number, or a
/// boolean, that looks like an answer" discipline, at the cost of this
/// type being unsafe as a <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>/<see cref="System.Collections.Generic.HashSet{T}"/>
/// key across mixed dimensions.
/// </para>
/// <para>
/// <b>Affine temperature.</b> An absolute temperature (degrees Celsius or
/// Fahrenheit) may be converted and compared but not scaled, exactly as
/// <see cref="Quantity{TDimension}"/>'s own affine rule (`ADR-0125`)
/// already refuses. This type adds two carve-outs neither the generic
/// facade nor `ADR-0125` had a dimension to express: subtracting two
/// temperatures where at least one is affine yields a
/// <see cref="TemperatureDeltaUnits"/>-typed result in kelvin (a genuine
/// temperature <em>difference</em>, not a position on the scale), and
/// adding a delta back onto an affine absolute temperature is permitted.
/// Where neither operand is affine (kelvin, degrees Rankine, or a delta
/// unit — every one of them a zero-offset, purely multiplicative unit),
/// ordinary same-dimension addition/subtraction applies with no special
/// casing at all, exactly matching <see cref="Quantity{TDimension}"/>'s
/// own long-standing "kelvin arithmetic is permitted" behaviour.
/// </para>
/// <para>
/// <b>Cross-dimension <c>*</c>/<c>/</c>.</b> The result's own unit is the
/// literal product/quotient of the two operand units — factor and symbol
/// both composed together (e.g. <c>3 kN</c> times <c>2 m</c> is <c>6
/// kN.m</c>, not silently reduced to newton-metres). When both operands
/// already happen to be expressed in their own dimension's base unit —
/// the common case — the composed unit already <em>is</em> the derived
/// base unit, and the composed symbol names it exactly (a force in
/// newtons over an area in square metres composes to <c>N/m^2</c>).
/// <see cref="BaseValue"/> always recovers the true SI-coherent magnitude
/// regardless of which case applied, so no information is lost either way.
/// </para>
/// </remarks>
public readonly struct Quantity : IEquatable<Quantity>, IComparable<Quantity>, IFormattable
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Quantity"/> struct.
    /// </summary>
    /// <param name="value">The numeric value, expressed in <paramref name="unit"/>.</param>
    /// <param name="unit">The unit <paramref name="value"/> is expressed in.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is <see cref="double.NaN"/> or infinite.</exception>
    [JsonConstructor]
    public Quantity(double value, UnitDefinition unit)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), value, "A quantity's value must be a finite number.");

        Value = value;
        Unit = unit;
    }

    /// <summary>The numeric value, expressed in <see cref="Unit"/>.</summary>
    public double Value { get; }

    /// <summary>The unit <see cref="Value"/> is expressed in.</summary>
    public UnitDefinition Unit { get; }

    /// <summary>This quantity's own value expressed in its dimension's base unit.</summary>
    public double BaseValue => Unit.ToBase(Value);

    /// <summary>Returns an equivalent quantity expressed in <paramref name="targetUnit"/>.</summary>
    /// <exception cref="IncompatibleUnitsException"><paramref name="targetUnit"/> does not share this quantity's own <see cref="Dimension"/>.</exception>
    public Quantity ConvertTo(UnitDefinition targetUnit)
    {
        RequireSameDimension(Unit.Dimension, targetUnit.Dimension);

        if (Unit == targetUnit)
            return this;

        return new Quantity(targetUnit.FromBase(Unit.ToBase(Value)), targetUnit);
    }

    // ------------------------------------------------------------
    // Addition / subtraction — same-dimension automatic conversion,
    // plus the two temperature-delta carve-outs described above.
    // ------------------------------------------------------------

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>, or the combination has no physical meaning on an affine scale.</exception>
    public static Quantity operator +(Quantity left, Quantity right) => AddOrSubtract(left, right, isAddition: true);

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static Quantity operator -(Quantity left, Quantity right) => AddOrSubtract(left, right, isAddition: false);

    private static Quantity AddOrSubtract(Quantity left, Quantity right, bool isAddition)
    {
        RequireSameDimension(left.Unit.Dimension, right.Unit.Dimension);

        var leftAffine = left.Unit.IsAffine;
        var rightAffine = right.Unit.IsAffine;

        if (!leftAffine && !rightAffine)
        {
            // Neither operand sits on an affine scale (this is the
            // overwhelming majority of every dimension, and includes
            // kelvin, degrees Rankine and every TemperatureDeltaUnits
            // member) — ordinary same-dimension arithmetic applies.
            var rightInLeftUnit = left.Unit == right.Unit ? right.Value : left.Unit.FromBase(right.Unit.ToBase(right.Value));
            return new Quantity(isAddition ? left.Value + rightInLeftUnit : left.Value - rightInLeftUnit, left.Unit);
        }

        if (!isAddition)
        {
            // Subtracting where at least one side is an absolute affine
            // position (degrees Celsius/Fahrenheit): the result is a
            // genuine temperature difference, not a position on the
            // scale, so it is returned as a TemperatureDelta in kelvin
            // rather than in either operand's own unit.
            return new Quantity(left.BaseValue - right.BaseValue, TemperatureDeltaUnits.Kelvin.UnitDefinition);
        }

        if (leftAffine && rightAffine)
        {
            // Two absolute affine positions added together (twenty
            // degrees Celsius plus five degrees Celsius) is not a
            // temperature — the identical refusal ADR-0125 already makes
            // for Quantity<TDimension>.
            throw new IncompatibleUnitsException(
                $"Cannot add two absolute temperatures expressed in '{left.Unit.Symbol}' and '{right.Unit.Symbol}': the sum has no physical meaning on an affine scale. Subtract to obtain a TemperatureDelta, or convert one operand to a delta unit first.");
        }

        // Exactly one side is affine: the non-affine side is treated as a
        // delta and added onto the affine side's own base value, in the
        // affine operand's own unit.
        var absolute = leftAffine ? left : right;
        var delta = leftAffine ? right : left;
        return new Quantity(absolute.Unit.FromBase(absolute.BaseValue + delta.BaseValue), absolute.Unit);
    }

    // ------------------------------------------------------------
    // Scalar multiplication / division
    // ------------------------------------------------------------

    /// <exception cref="IncompatibleUnitsException"><paramref name="quantity"/>'s own unit sits on an affine scale.</exception>
    public static Quantity operator *(Quantity quantity, double scalar)
    {
        RequireNotAffine(quantity.Unit, "scaled");
        return new Quantity(quantity.Value * scalar, quantity.Unit);
    }

    /// <exception cref="IncompatibleUnitsException"><paramref name="quantity"/>'s own unit sits on an affine scale.</exception>
    public static Quantity operator *(double scalar, Quantity quantity) => quantity * scalar;

    /// <exception cref="IncompatibleUnitsException"><paramref name="quantity"/>'s own unit sits on an affine scale.</exception>
    public static Quantity operator /(Quantity quantity, double scalar)
    {
        RequireNotAffine(quantity.Unit, "scaled");
        return new Quantity(quantity.Value / scalar, quantity.Unit);
    }

    // ------------------------------------------------------------
    // Cross-dimension multiplication / division
    // ------------------------------------------------------------

    /// <exception cref="IncompatibleUnitsException">Either operand's own unit sits on an affine scale.</exception>
    public static Quantity operator *(Quantity left, Quantity right)
    {
        RequireNotAffine(left.Unit, "multiplied");
        RequireNotAffine(right.Unit, "multiplied");

        var unit = new UnitDefinition(
            ComposeSymbol(left.Unit.Symbol, right.Unit.Symbol, '.'),
            left.Unit.Dimension * right.Unit.Dimension,
            left.Unit.ToBaseFactor * right.Unit.ToBaseFactor);

        return new Quantity(left.Value * right.Value, unit);
    }

    /// <exception cref="IncompatibleUnitsException">Either operand's own unit sits on an affine scale.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="right"/>'s own value is zero.</exception>
    public static Quantity operator /(Quantity left, Quantity right)
    {
        RequireNotAffine(left.Unit, "divided");
        RequireNotAffine(right.Unit, "divided");

        if (right.Value == 0.0)
            throw new ArgumentOutOfRangeException(nameof(right), right.Value, "Cannot divide a quantity by a quantity whose own value is zero.");

        var unit = new UnitDefinition(
            ComposeSymbol(left.Unit.Symbol, right.Unit.Symbol, '/'),
            left.Unit.Dimension / right.Unit.Dimension,
            left.Unit.ToBaseFactor / right.Unit.ToBaseFactor);

        return new Quantity(left.Value / right.Value, unit);
    }

    private static string ComposeSymbol(string leftSymbol, string rightSymbol, char op) => $"{leftSymbol}{op}{rightSymbol}";

    // ------------------------------------------------------------
    // Comparison
    // ------------------------------------------------------------

    /// <exception cref="IncompatibleUnitsException">This instance and <paramref name="other"/> do not share the same <see cref="Dimension"/>.</exception>
    public int CompareTo(Quantity other)
    {
        RequireSameDimension(Unit.Dimension, other.Unit.Dimension);
        return BaseValue.CompareTo(other.BaseValue);
    }

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator <(Quantity left, Quantity right) => left.CompareTo(right) < 0;

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator >(Quantity left, Quantity right) => left.CompareTo(right) > 0;

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator <=(Quantity left, Quantity right) => left.CompareTo(right) <= 0;

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator >=(Quantity left, Quantity right) => left.CompareTo(right) >= 0;

    // ------------------------------------------------------------
    // Equality — see this type's own remarks for why this can throw.
    // ------------------------------------------------------------

    /// <exception cref="IncompatibleUnitsException">This instance and <paramref name="other"/> do not share the same <see cref="Dimension"/>.</exception>
    public bool Equals(Quantity other)
    {
        RequireSameDimension(Unit.Dimension, other.Unit.Dimension);
        return BaseValue.Equals(other.BaseValue);
    }

    /// <exception cref="IncompatibleUnitsException"><paramref name="obj"/> is a <see cref="Quantity"/> that does not share this instance's own <see cref="Dimension"/>.</exception>
    public override bool Equals(object? obj) => obj is Quantity other && Equals(other);

    /// <inheritdoc />
    /// <remarks>Combines <see cref="UnitsAndQuantities.Dimension"/> into the hash so that two quantities of different dimensions do not collide, even though comparing them throws rather than returning <see langword="false"/>.</remarks>
    public override int GetHashCode() => HashCode.Combine(Unit.Dimension, BaseValue);

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator ==(Quantity left, Quantity right) => left.Equals(right);

    /// <exception cref="IncompatibleUnitsException"><paramref name="left"/> and <paramref name="right"/> do not share the same <see cref="Dimension"/>.</exception>
    public static bool operator !=(Quantity left, Quantity right) => !left.Equals(right);

    // ------------------------------------------------------------
    // Formatting / parsing
    // ------------------------------------------------------------

    /// <inheritdoc />
    public override string ToString() => ToString(format: null, formatProvider: null);

    /// <inheritdoc />
    /// <remarks>Culture-invariant regardless of <paramref name="formatProvider"/>'s own numeric conventions.</remarks>
    public string ToString(string? format, IFormatProvider? formatProvider) =>
        $"{Value.ToString(format, CultureInfo.InvariantCulture)} {Unit.Symbol}";

    /// <summary>Parses a "&lt;number&gt; &lt;symbol&gt;" formatted quantity, matching <paramref name="knownUnits"/> by exact <see cref="UnitDefinition.Symbol"/>.</summary>
    /// <exception cref="FormatException"><paramref name="input"/> is not a recognised quantity for a symbol present in <paramref name="knownUnits"/>.</exception>
    public static Quantity Parse(string input, IReadOnlyList<UnitDefinition> knownUnits) =>
        TryParse(input, knownUnits, out var result) ? result : throw new FormatException($"'{input}' is not a recognised quantity.");

    /// <summary>Attempts to parse a "&lt;number&gt; &lt;symbol&gt;" formatted quantity, matching <paramref name="knownUnits"/> by exact <see cref="UnitDefinition.Symbol"/>.</summary>
    public static bool TryParse(string? input, IReadOnlyList<UnitDefinition> knownUnits, out Quantity result)
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
                result = new Quantity(value, unit);
                return true;
            }
        }

        return false;
    }

    private static void RequireNotAffine(UnitDefinition unit, string operation)
    {
        if (unit.IsAffine)
            throw new IncompatibleUnitsException(
                $"A quantity expressed in '{unit.Symbol}' cannot be {operation}: that unit sits on an affine scale, where the operation has no physical meaning. Convert to an absolute unit of the same dimension first.");
    }

    private static void RequireSameDimension(Dimension left, Dimension right)
    {
        if (left != right)
            throw new IncompatibleUnitsException(
                $"Cannot combine a quantity of dimension '{left}' with one of dimension '{right}': they are not the same physical dimension.");
    }
}
