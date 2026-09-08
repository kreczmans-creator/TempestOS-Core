using System.Text;
using System.Text.Json.Serialization;

namespace Tempest.Core.UnitsAndQuantities;

/// <summary>
/// A runtime vector of the seven SI base-quantity exponents — the
/// dimensional "shape" of a physical quantity, independent of any unit it
/// might be expressed in.
/// </summary>
/// <remarks>
/// <para>
/// `ADR-0147`. Every existing dimension in this framework
/// (<see cref="Length"/>, <see cref="Mass"/>, and so on) is a compile-time
/// marker type carrying no runtime information of its own — two marker
/// types are either the exact same type or entirely unrelated ones, and
/// nothing in between. <see cref="Dimension"/> is the runtime counterpart:
/// a plain value that can be multiplied, divided and compared, so that
/// <see cref="Quantity"/> arithmetic can discover at run time that
/// newtons divided by square metres is pascals, without either side
/// having been declared in a catalogue that says so in advance.
/// </para>
/// <para>
/// The seven exponents follow the ISO 80000/SI base-quantity order: length
/// (L), mass (M), time (T), thermodynamic temperature (Θ), electric
/// current (I), amount of substance (N), and luminous intensity (J).
/// <see cref="sbyte"/> is used for each — a component's own magnitude
/// never approaches even a fraction of its ±127 range for any physically
/// realistic derived quantity, and the narrow type keeps the whole vector
/// at 7 bytes, cheap to copy, compare and hash.
/// </para>
/// <para>
/// The named vectors for every dimension this framework already has, plus
/// the ones this Work Package adds, live in the companion static class
/// <see cref="Dimensions"/> rather than as static members here — C# does
/// not allow an instance member and a static member to share a name on
/// the same type, and three of the seven exponent properties
/// (<see cref="Length"/>, <see cref="Mass"/>, <see cref="Temperature"/>)
/// would collide with the identically-named single-exponent vector a
/// caller would otherwise expect at <c>Dimension.Length</c>. Splitting the
/// catalogue into its own type mirrors this framework's own existing
/// convention of a marker type (<see cref="UnitsAndQuantities.Length"/>)
/// paired with a separate catalogue type (<see cref="LengthUnits"/>).
/// </para>
/// </remarks>
public readonly record struct Dimension
{
    /// <summary>
    /// Initialises a new instance of the <see cref="Dimension"/> struct.
    /// </summary>
    /// <param name="length">The length (L) exponent.</param>
    /// <param name="mass">The mass (M) exponent.</param>
    /// <param name="time">The time (T) exponent.</param>
    /// <param name="temperature">The thermodynamic temperature (Θ) exponent.</param>
    /// <param name="electricCurrent">The electric current (I) exponent.</param>
    /// <param name="amountOfSubstance">The amount of substance (N) exponent.</param>
    /// <param name="luminousIntensity">The luminous intensity (J) exponent.</param>
    [JsonConstructor]
    public Dimension(
        sbyte length = 0,
        sbyte mass = 0,
        sbyte time = 0,
        sbyte temperature = 0,
        sbyte electricCurrent = 0,
        sbyte amountOfSubstance = 0,
        sbyte luminousIntensity = 0)
    {
        Length = length;
        Mass = mass;
        Time = time;
        Temperature = temperature;
        ElectricCurrent = electricCurrent;
        AmountOfSubstance = amountOfSubstance;
        LuminousIntensity = luminousIntensity;
    }

    /// <summary>The length (L) exponent.</summary>
    public sbyte Length { get; }

    /// <summary>The mass (M) exponent.</summary>
    public sbyte Mass { get; }

    /// <summary>The time (T) exponent.</summary>
    public sbyte Time { get; }

    /// <summary>The thermodynamic temperature (Θ) exponent.</summary>
    public sbyte Temperature { get; }

    /// <summary>The electric current (I) exponent.</summary>
    public sbyte ElectricCurrent { get; }

    /// <summary>The amount of substance (N) exponent.</summary>
    public sbyte AmountOfSubstance { get; }

    /// <summary>The luminous intensity (J) exponent.</summary>
    public sbyte LuminousIntensity { get; }

    /// <summary>Whether every exponent is zero — a pure number, ratio or angle.</summary>
    public bool IsDimensionless =>
        Length == 0 && Mass == 0 && Time == 0 && Temperature == 0
        && ElectricCurrent == 0 && AmountOfSubstance == 0 && LuminousIntensity == 0;

    /// <summary>Combines two dimensions as they combine when their quantities are multiplied — exponents add.</summary>
    /// <exception cref="OverflowException">A resulting exponent would fall outside the range of <see cref="sbyte"/>.</exception>
    public static Dimension operator *(Dimension left, Dimension right) =>
        checked(new Dimension(
            (sbyte)(left.Length + right.Length),
            (sbyte)(left.Mass + right.Mass),
            (sbyte)(left.Time + right.Time),
            (sbyte)(left.Temperature + right.Temperature),
            (sbyte)(left.ElectricCurrent + right.ElectricCurrent),
            (sbyte)(left.AmountOfSubstance + right.AmountOfSubstance),
            (sbyte)(left.LuminousIntensity + right.LuminousIntensity)));

    /// <summary>Combines two dimensions as they combine when their quantities are divided — exponents subtract.</summary>
    /// <exception cref="OverflowException">A resulting exponent would fall outside the range of <see cref="sbyte"/>.</exception>
    public static Dimension operator /(Dimension left, Dimension right) =>
        checked(new Dimension(
            (sbyte)(left.Length - right.Length),
            (sbyte)(left.Mass - right.Mass),
            (sbyte)(left.Time - right.Time),
            (sbyte)(left.Temperature - right.Temperature),
            (sbyte)(left.ElectricCurrent - right.ElectricCurrent),
            (sbyte)(left.AmountOfSubstance - right.AmountOfSubstance),
            (sbyte)(left.LuminousIntensity - right.LuminousIntensity)));

    /// <summary>Raises this dimension to an integer power — every exponent multiplied by <paramref name="exponent"/>.</summary>
    /// <exception cref="OverflowException">A resulting exponent would fall outside the range of <see cref="sbyte"/>.</exception>
    public Dimension Pow(int exponent) =>
        checked(new Dimension(
            (sbyte)(Length * exponent),
            (sbyte)(Mass * exponent),
            (sbyte)(Time * exponent),
            (sbyte)(Temperature * exponent),
            (sbyte)(ElectricCurrent * exponent),
            (sbyte)(AmountOfSubstance * exponent),
            (sbyte)(LuminousIntensity * exponent)));

    /// <summary>
    /// A stable, deterministic rendering of this dimension's own exponents
    /// — e.g. <c>"L^1 M^1 T^-2"</c> for force — in fixed L, M, T, Θ, I, N, J
    /// order, omitting every zero exponent. The unit one (every exponent
    /// zero) renders as <c>"1"</c>.
    /// </summary>
    public override string ToString()
    {
        if (IsDimensionless)
            return "1";

        var builder = new StringBuilder();
        AppendComponent(builder, "L", Length);
        AppendComponent(builder, "M", Mass);
        AppendComponent(builder, "T", Time);
        AppendComponent(builder, "Θ", Temperature);
        AppendComponent(builder, "I", ElectricCurrent);
        AppendComponent(builder, "N", AmountOfSubstance);
        AppendComponent(builder, "J", LuminousIntensity);
        return builder.ToString();
    }

    private static void AppendComponent(StringBuilder builder, string symbol, sbyte exponent)
    {
        if (exponent == 0)
            return;

        if (builder.Length > 0)
            builder.Append(' ');

        builder.Append(symbol).Append('^').Append(exponent);
    }
}
