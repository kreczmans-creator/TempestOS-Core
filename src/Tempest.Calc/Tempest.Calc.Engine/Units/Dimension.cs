using System.Text;

namespace Tempest.Calc.Engine.Units;

/// <summary>
/// A physical dimension as integer exponents of the seven SI base quantities:
/// length (m), mass (kg), time (s), electric current (A), thermodynamic
/// temperature (K), amount of substance (mol), luminous intensity (cd).
/// Plane angle is dimensionless (radian = 1), so <c>sin(30 deg)</c> works
/// without a special case.
/// </summary>
public readonly record struct Dimension(sbyte Length, sbyte Mass, sbyte Time, sbyte Current, sbyte Temperature, sbyte Amount, sbyte Luminosity)
{
    public static readonly Dimension None = new(0, 0, 0, 0, 0, 0, 0);
    public static readonly Dimension L = new(1, 0, 0, 0, 0, 0, 0);
    public static readonly Dimension M = new(0, 1, 0, 0, 0, 0, 0);
    public static readonly Dimension T = new(0, 0, 1, 0, 0, 0, 0);
    public static readonly Dimension I = new(0, 0, 0, 1, 0, 0, 0);
    public static readonly Dimension Theta = new(0, 0, 0, 0, 1, 0, 0);
    public static readonly Dimension N = new(0, 0, 0, 0, 0, 1, 0);
    public static readonly Dimension J = new(0, 0, 0, 0, 0, 0, 1);

    public static readonly Dimension Area = L * L;
    public static readonly Dimension Volume = L * L * L;
    public static readonly Dimension Velocity = L / T;
    public static readonly Dimension Acceleration = L / (T * T);
    public static readonly Dimension Force = M * L / (T * T);
    public static readonly Dimension Pressure = Force / Area;
    public static readonly Dimension Energy = Force * L;
    public static readonly Dimension Power = Energy / T;
    public static readonly Dimension Density = M / Volume;
    public static readonly Dimension SecondMomentOfArea = L.Pow(4);
    public static readonly Dimension Frequency = None / T;
    public static readonly Dimension Voltage = Power / I;
    public static readonly Dimension Resistance = Voltage / I;
    public static readonly Dimension Charge = I * T;
    public static readonly Dimension DynamicViscosity = Pressure * T;
    public static readonly Dimension KinematicViscosity = Area / T;
    public static readonly Dimension VolumeFlow = Volume / T;
    public static readonly Dimension MassFlow = M / T;
    public static readonly Dimension Torque = Force * L;
    public static readonly Dimension MomentOfInertia = M * Area;
    public static readonly Dimension AngularVelocity = None / T;
    public static readonly Dimension SpecificHeat = Energy / (M * Theta);
    public static readonly Dimension ThermalConductivity = Power / (L * Theta);
    public static readonly Dimension HeatTransferCoefficient = Power / (Area * Theta);
    public static readonly Dimension Stiffness = Force / L;

    public bool IsDimensionless => this == None;

    public static Dimension operator *(Dimension a, Dimension b) => new(
        Add(a.Length, b.Length), Add(a.Mass, b.Mass), Add(a.Time, b.Time), Add(a.Current, b.Current),
        Add(a.Temperature, b.Temperature), Add(a.Amount, b.Amount), Add(a.Luminosity, b.Luminosity));

    public static Dimension operator /(Dimension a, Dimension b) => new(
        Add(a.Length, -b.Length), Add(a.Mass, -b.Mass), Add(a.Time, -b.Time), Add(a.Current, -b.Current),
        Add(a.Temperature, -b.Temperature), Add(a.Amount, -b.Amount), Add(a.Luminosity, -b.Luminosity));

    /// <summary>Raises every exponent to <paramref name="n"/>. Exponents are capped at ±40 to keep any expression finite.</summary>
    public Dimension Pow(int n) => new(
        Mul(Length, n), Mul(Mass, n), Mul(Time, n), Mul(Current, n), Mul(Temperature, n), Mul(Amount, n), Mul(Luminosity, n));

    /// <summary>The dimension whose <paramref name="n"/>th power is this one, or <see langword="null"/> where an exponent does not divide.</summary>
    public Dimension? Root(int n)
    {
        if (n <= 0)
            return null;

        var e = new[] { Length, Mass, Time, Current, Temperature, Amount, Luminosity };
        var r = new sbyte[7];

        for (var i = 0; i < 7; i++)
        {
            if (e[i] % n != 0)
                return null;

            r[i] = (sbyte)(e[i] / n);
        }

        return new Dimension(r[0], r[1], r[2], r[3], r[4], r[5], r[6]);
    }

    /// <summary>The dimension written in SI base symbols, e.g. <c>kg·m/s²</c>; empty for a dimensionless value.</summary>
    public string BaseSymbol()
    {
        var num = new StringBuilder();
        var den = new StringBuilder();
        Append(num, den, "m", Length);
        Append(num, den, "kg", Mass);
        Append(num, den, "s", Time);
        Append(num, den, "A", Current);
        Append(num, den, "K", Temperature);
        Append(num, den, "mol", Amount);
        Append(num, den, "cd", Luminosity);

        if (num.Length == 0 && den.Length == 0)
            return string.Empty;

        if (den.Length == 0)
            return num.ToString();

        var n = num.Length == 0 ? "1" : num.ToString();
        return den.ToString().Contains('·', StringComparison.Ordinal) ? $"{n}/({den})" : $"{n}/{den}";
    }

    public override string ToString() => IsDimensionless ? "1" : BaseSymbol();

    private static void Append(StringBuilder num, StringBuilder den, string symbol, int exponent)
    {
        if (exponent == 0)
            return;

        var target = exponent > 0 ? num : den;
        var magnitude = Math.Abs(exponent);

        if (target.Length > 0)
            target.Append('·');

        target.Append(symbol);

        if (magnitude != 1)
            target.Append(Superscript(magnitude));
    }

    internal static string Superscript(int n)
    {
        const string digits = "⁰¹²³⁴⁵⁶⁷⁸⁹";
        var s = n.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var sb = new StringBuilder();

        foreach (var c in s)
            sb.Append(c == '-' ? '⁻' : digits[c - '0']);

        return sb.ToString();
    }

    private static sbyte Add(int a, int b) => (sbyte)Math.Clamp(a + b, -40, 40);

    private static sbyte Mul(int a, int b) => (sbyte)Math.Clamp((long)a * b, -40, 40);
}
