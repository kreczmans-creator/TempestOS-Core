using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 29 and 30: how a calculated number is shown. The decimals depend on the quantity type (its dimension and the unit it is shown
/// in), not on the template: a force is 2 decimals in kN, a stress 1 decimal in MPa, a utilisation 2 decimals. The table is the one list
/// (<see cref="Rules"/>); the glossary's "Decimals" column is generated from it and a test compares the two. The number printed is the
/// calculated value rounded for display only: nothing computed, stored or exported is rounded here. Every place that shows a result (the
/// sheet, the PDF, the Desktop form, the governing banner, the register) uses this one formatter.
/// </summary>
/// <remarks>
/// <para>A value in a unit the table does not list is shown to five significant figures.</para>
/// <para>A result is shown with at least four and at most five significant figures (the table's decimals are the least decimals shown, and are
/// raised where four figures need more); a whole number is printed exactly; zero is "0" (C2-21).</para>
/// <para>No displayed value appears on the passing side of a limit its check fails (C2-21). A utilisation is shown to 2 decimals (2 significant
/// figures below 0.005) and rounded up when it is above its limit; a value that must be at least a limit (a safety factor, a margin) is
/// rounded down; a failing check line shows its demand rounded up and its capacity rounded down.</para>
/// <para>A utilisation is recognised by what the template declares, never by its symbol or description: an output flagged
/// <see cref="CalcOutputKind.Utilisation"/> and the symbol a utilisation check is made on (C2-21). A value is shown as 0 for being solver noise
/// only when its template flagged it noise-prone (C2-20).</para>
/// </remarks>
public static class CalcDisplay
{
    /// <summary>One line of the precision table: a quantity type, the dimension and unit it is shown in, and its decimals.</summary>
    public sealed record Rule(string Quantity, Dimension Dimension, string Unit, int Decimals);

    /// <summary>The decimals of a utilisation.</summary>
    public const int UtilisationDecimals = 2;

    /// <summary>A noise-prone output below this fraction of the largest working value of its dimension is shown as 0.</summary>
    public const double NoiseFraction = 1e-9;

    /// <summary>At least this many significant figures are always shown (a result of 3.1595 cm³ is never "3.2 cm³").</summary>
    public const int MinimumSignificantFigures = 4;

    /// <summary>At most this many significant figures are shown, however many decimals the table says.</summary>
    public const int MaximumSignificantFigures = 5;

    private static readonly Dimension LineForce = Dimension.Force / Dimension.L;
    private static readonly Dimension Resistivity = Dimension.Resistance * Dimension.L;
    private static readonly Dimension FrictionRate = Dimension.Pressure / Dimension.L;
    private static readonly Dimension ThermalConductance = Dimension.Power / Dimension.Theta;
    private static readonly Dimension HeatFlux = Dimension.Power / Dimension.Area;
    private static readonly Dimension HeatPerLength = Dimension.Power / Dimension.L;
    private static readonly Dimension ThermalResistanceArea = Dimension.Area * Dimension.Theta / Dimension.Power;
    private static readonly Dimension ThermalResistance = Dimension.Theta / Dimension.Power;
    private static readonly Dimension ThermalResistanceLength = Dimension.L * Dimension.Theta / Dimension.Power;
    private static readonly Dimension SpecificVolume = Dimension.Volume / Dimension.M;
    private static readonly Dimension MassPerArea = Dimension.M / Dimension.Area;
    private static readonly Dimension MassPerLength = Dimension.M / Dimension.L;
    private static readonly Dimension AngularAcceleration = Dimension.None / (Dimension.T * Dimension.T);
    private static readonly Dimension SixthMoment = Dimension.L.Pow(6);
    private static readonly Dimension SpecificEnergy = Dimension.Energy / Dimension.M;

    /// <summary>The precision table: the one place the decimals of a quantity type are written.</summary>
    public static IReadOnlyList<Rule> Rules { get; } =
    [
        new("Force", Dimension.Force, "N", 1),
        new("Force", Dimension.Force, "kN", 2),
        new("Force", Dimension.Force, "MN", 3),
        new("Force", Dimension.Force, "lbf", 1),
        new("Line force, distributed load, stiffness", LineForce, "N/m", 0),
        new("Line force, distributed load, stiffness", LineForce, "N/mm", 1),
        new("Line force, distributed load, stiffness", LineForce, "kN/m", 1),
        new("Line force, distributed load, stiffness", LineForce, "kN/mm", 2),
        new("Moment per unit length", Dimension.Force, "N·m/m", 2),
        new("Moment per unit length", Dimension.Force, "kN·m/m", 2),
        new("Moment, torque", Dimension.Torque, "N·mm", 0),
        new("Moment, torque", Dimension.Torque, "N·m", 2),
        new("Moment, torque", Dimension.Torque, "kN·m", 2),
        new("Moment, torque", Dimension.Torque, "MN·m", 3),
        new("Energy", Dimension.Energy, "J", 1),
        new("Energy", Dimension.Energy, "kJ", 2),
        new("Energy", Dimension.Energy, "MJ", 3),
        new("Energy", Dimension.Energy, "Wh", 1),
        new("Energy", Dimension.Energy, "kWh", 2),
        new("Energy", Dimension.Energy, "Btu", 1),
        new("Stress, pressure", Dimension.Pressure, "Pa", 0),
        new("Stress, pressure", Dimension.Pressure, "kPa", 1),
        new("Stress, pressure", Dimension.Pressure, "MPa", 1),
        new("Stress, pressure", Dimension.Pressure, "GPa", 0),
        new("Stress, pressure", Dimension.Pressure, "N/mm²", 1),
        new("Stress, pressure", Dimension.Pressure, "mbar", 1),
        new("Stress, pressure", Dimension.Pressure, "bar", 2),
        new("Stress, pressure", Dimension.Pressure, "psi", 1),
        new("Stress, pressure", Dimension.Pressure, "ksi", 2),
        new("Friction rate", FrictionRate, "Pa/m", 2),
        new("Length", Dimension.L, "µm", 1),
        new("Length", Dimension.L, "mm", 1),
        new("Length", Dimension.L, "cm", 1),
        new("Length", Dimension.L, "m", 2),
        new("Length", Dimension.L, "km", 3),
        new("Length", Dimension.L, "mil", 1),
        new("Length", Dimension.L, "in", 2),
        new("Length", Dimension.L, "ft", 3),
        new("Area", Dimension.Area, "mm²", 1),
        new("Area", Dimension.Area, "cm²", 2),
        new("Area", Dimension.Area, "m²", 2),
        new("Area", Dimension.Area, "in²", 3),
        new("Area", Dimension.Area, "mil²", 0),
        new("Second moment of area", Dimension.SecondMomentOfArea, "mm⁴", 0),
        new("Second moment of area", Dimension.SecondMomentOfArea, "cm⁴", 1),
        new("Second moment of area", Dimension.SecondMomentOfArea, "m⁴", 6),
        new("Second moment of area", Dimension.SecondMomentOfArea, "in⁴", 2),
        new("Warping constant", SixthMoment, "cm⁶", 0),
        new("Warping constant", SixthMoment, "dm⁶", 3),
        new("Volume, section modulus", Dimension.Volume, "mm³", 0),
        new("Volume, section modulus", Dimension.Volume, "cm³", 1),
        new("Volume, section modulus", Dimension.Volume, "dm³", 1),
        new("Volume, section modulus", Dimension.Volume, "L", 1),
        new("Volume, section modulus", Dimension.Volume, "mL", 1),
        new("Volume, section modulus", Dimension.Volume, "m³", 3),
        new("Volume, section modulus", Dimension.Volume, "cm³/rev", 2),
        new("Volumetric flow", Dimension.VolumeFlow, "m³/h", 1),
        new("Volumetric flow", Dimension.VolumeFlow, "m³/s", 4),
        new("Volumetric flow", Dimension.VolumeFlow, "L/s", 2),
        new("Volumetric flow", Dimension.VolumeFlow, "L/min", 1),
        new("Volumetric flow", Dimension.VolumeFlow, "L/h", 0),
        new("Mass flow", Dimension.MassFlow, "kg/s", 3),
        new("Mass flow", Dimension.MassFlow, "g/s", 2),
        new("Mass flow", Dimension.MassFlow, "kg/h", 1),
        new("Mass", Dimension.M, "g", 1),
        new("Mass", Dimension.M, "kg", 2),
        new("Mass", Dimension.M, "t", 3),
        new("Mass", Dimension.M, "lb", 2),
        new("Mass per length", MassPerLength, "kg/m", 3),
        new("Mass per area", MassPerArea, "g/m²", 1),
        new("Mass per area", MassPerArea, "kg/m²", 3),
        new("Density", Dimension.Density, "kg/m³", 1),
        new("Density", Dimension.Density, "g/cm³", 3),
        new("Specific volume", SpecificVolume, "m³/kg", 4),
        new("Time", Dimension.T, "ms", 1),
        new("Time", Dimension.T, "s", 2),
        new("Time", Dimension.T, "min", 1),
        new("Time, rating life", Dimension.T, "h", 0),
        new("Time", Dimension.T, "d", 1),
        new("Rotational speed, frequency", Dimension.Frequency, "rpm", 0),
        new("Rotational speed, frequency", Dimension.Frequency, "Hz", 2),
        new("Rotational speed, frequency", Dimension.Frequency, "rad/s", 2),
        new("Rotational speed, frequency", Dimension.Frequency, "1/s", 3),
        new("Angular acceleration", AngularAcceleration, "rad/s²", 2),
        new("Angle", Dimension.None, "°", 2),
        new("Angle (slope)", Dimension.None, "rad", 4),
        new("Angle", Dimension.None, "rev", 3),
        new("Percent quantity", Dimension.None, "%", 1),
        new("Velocity", Dimension.Velocity, "mm/s", 1),
        new("Velocity", Dimension.Velocity, "m/s", 2),
        new("Velocity", Dimension.Velocity, "km/h", 1),
        new("Acceleration", Dimension.Acceleration, "mm/s²", 0),
        new("Acceleration", Dimension.Acceleration, "m/s²", 2),
        new("Temperature", Dimension.Theta, "°C", 1),
        new("Temperature", Dimension.Theta, "°F", 1),
        new("Temperature, temperature difference", Dimension.Theta, "K", 1),
        new("Power, heat flow", Dimension.Power, "mW", 1),
        new("Power, heat flow", Dimension.Power, "W", 1),
        new("Power, heat flow", Dimension.Power, "kW", 2),
        new("Power, heat flow", Dimension.Power, "MW", 3),
        new("Power, heat flow", Dimension.Power, "hp", 2),
        new("Apparent and reactive power", Dimension.Power, "VA", 1),
        new("Apparent and reactive power", Dimension.Power, "kVA", 2),
        new("Apparent and reactive power", Dimension.Power, "MVA", 3),
        new("Apparent and reactive power", Dimension.Power, "var", 1),
        new("Apparent and reactive power", Dimension.Power, "kvar", 2),
        new("Voltage", Dimension.Voltage, "mV", 1),
        new("Voltage", Dimension.Voltage, "V", 1),
        new("Voltage", Dimension.Voltage, "kV", 3),
        new("Current", Dimension.I, "mA", 1),
        new("Current", Dimension.I, "A", 2),
        new("Current", Dimension.I, "kA", 3),
        new("Charge", Dimension.Charge, "A·h", 2),
        new("Resistance, impedance", Dimension.Resistance, "mΩ", 2),
        new("Resistance, impedance", Dimension.Resistance, "Ω", 4),
        new("Resistance, impedance", Dimension.Resistance, "kΩ", 3),
        new("Resistance, impedance", Dimension.Resistance, "MΩ", 3),
        new("Resistivity", Resistivity, "Ω·mm²/m", 5),
        new("Resistivity", Resistivity, "Ω·m", 8),
        new("Heat transfer coefficient, U-value", Dimension.HeatTransferCoefficient, "W/(m²·K)", 2),
        new("Thermal conductivity", Dimension.ThermalConductivity, "W/(m·K)", 3),
        new("Thermal conductance", ThermalConductance, "W/K", 2),
        new("Thermal resistance", ThermalResistanceArea, "m²·K/W", 3),
        new("Thermal resistance", ThermalResistance, "K/W", 3),
        new("Thermal resistance", ThermalResistanceLength, "m·K/W", 3),
        new("Heat flux", HeatFlux, "W/m²", 1),
        new("Heat flow per length", HeatPerLength, "W/m", 1),
        new("Specific heat", Dimension.SpecificHeat, "J/(kg·K)", 0),
        new("Specific heat", Dimension.SpecificHeat, "kJ/(kg·K)", 3),
        new("Specific energy", SpecificEnergy, "kJ/kg", 1),
        new("Dynamic viscosity", Dimension.DynamicViscosity, "Pa·s", 4),
        new("Dynamic viscosity", Dimension.DynamicViscosity, "mPa·s", 2),
        new("Moment of inertia (mass)", Dimension.MomentOfInertia, "kg·m²", 4),
        new("Moment of inertia (mass)", Dimension.MomentOfInertia, "kg·cm²", 2),
    ];

    private static readonly Dictionary<(Dimension, string), int> Table = Rules.ToDictionary(r => (r.Dimension, r.Unit), r => r.Decimals);

    /// <summary>The unit symbol a rule is looked up under: the symbol in the Unicode form, without a pressure's basis label ("bar a" is "bar").</summary>
    private static string KeyOf(string unitSymbol)
    {
        var pretty = CalcUnitText.Pretty(unitSymbol);
        return pretty is "bar a" or "bar g" ? "bar" : pretty;
    }

    /// <summary>The decimals the table gives a value of <paramref name="dimension"/> shown in <paramref name="unitSymbol"/>, or <see langword="null"/> when the table does not list the pair.</summary>
    public static int? DecimalsFor(Dimension dimension, string unitSymbol) => Table.TryGetValue((dimension, KeyOf(unitSymbol)), out var d) ? d : null;

    /// <summary>
    /// How a number is rounded for display. <see cref="Nearest"/> is the default; a value shown against a limit is rounded so that it never
    /// appears on the passing side of a limit its check fails (C2-21): a value checked against a maximum is rounded <see cref="Up"/> when it
    /// exceeds the limit, a value that must be at least a minimum is rounded <see cref="Down"/>.
    /// </summary>
    public enum Rounding
    {
        /// <summary>To the nearest digit (ties to even).</summary>
        Nearest,

        /// <summary>Towards +infinity: 1.004 is shown 1.01.</summary>
        Up,

        /// <summary>Towards -infinity: 1.4999 is shown 1.499.</summary>
        Down,

        /// <summary>Away from zero (the magnitude is rounded up): a negative utilisation is shown more negative, never closer to zero.</summary>
        AwayFromZero,

        /// <summary>Towards zero (the magnitude is rounded down).</summary>
        TowardZero,
    }

    /// <summary>A value more than this relative amount from a whole number is not a whole number.</summary>
    private const double WholeTolerance = 1e-9;

    /// <summary>A utilisation below this is shown to two significant figures instead of two decimals (0.00022, not 0.00).</summary>
    public const double SmallUtilisation = 0.005;

    /// <summary>The number of significant figures a utilisation below <see cref="SmallUtilisation"/> is shown with.</summary>
    public const int SmallUtilisationSignificantFigures = 2;

    /// <summary>Whether <paramref name="value"/> is a whole number to within rounding noise: it is then printed exactly, without decimals.</summary>
    public static bool IsWhole(double value) =>
        double.IsFinite(value) && Math.Abs(value) >= 1 && Math.Abs(value) < 1e15 && Math.Abs(value - Math.Round(value)) <= WholeTolerance * Math.Abs(value);

    /// <summary>
    /// <paramref name="value"/> rounded to a multiple of 1/<paramref name="scale"/> in the direction <paramref name="mode"/>. A value that is a hair
    /// from a multiple (2.3 held as 2.2999999999999998) counts as the multiple, so rounding up or down never moves a clean number.
    /// </summary>
    private static double RoundScaled(double value, double scale, Rounding mode)
    {
        var scaled = value * scale;
        var near = Math.Round(scaled, MidpointRounding.ToEven);

        if (mode == Rounding.Nearest || Math.Abs(scaled - near) <= 1e-9 * Math.Max(1.0, Math.Abs(near)))
            return near / scale;

        var rounded = mode switch
        {
            Rounding.Up => Math.Ceiling(scaled),
            Rounding.Down => Math.Floor(scaled),
            Rounding.AwayFromZero => Math.Sign(scaled) * Math.Ceiling(Math.Abs(scaled)),
            _ => Math.Sign(scaled) * Math.Floor(Math.Abs(scaled)),
        };
        return rounded / scale;
    }

    private static int MagnitudeOf(double value) => (int)Math.Floor(Math.Log10(Math.Abs(value)));

    /// <summary><paramref name="value"/> rounded to <paramref name="significantFigures"/> significant figures in the direction <paramref name="mode"/>.</summary>
    private static double RoundSignificant(double value, int significantFigures, Rounding mode)
    {
        if (value == 0 || !double.IsFinite(value))
            return value;

        var decimals = significantFigures - 1 - MagnitudeOf(value);
        return decimals >= 0 ? RoundScaled(value, Math.Pow(10, Math.Min(decimals, 300)), mode) : RoundScaled(value, 1 / Math.Pow(10, -decimals), mode);
    }

    /// <summary>
    /// A number as a result prints it: at least <see cref="MinimumSignificantFigures"/> and at most <see cref="MaximumSignificantFigures"/>
    /// significant figures, with at least <paramref name="decimals"/> decimals (the table of <see cref="Rules"/>) where those fit. A whole number
    /// is printed exactly and zero is "0". The rounding direction is <paramref name="mode"/>.
    /// </summary>
    public static string Number(double value, int decimals, Rounding mode = Rounding.Nearest)
    {
        if (!double.IsFinite(value))
            return CalcValue.FormatNumber(value);

        if (value == 0)
            return "0";

        if (IsWhole(value))
            return Math.Round(value).ToString("F0", CultureInfo.InvariantCulture);

        var magnitude = MagnitudeOf(value);

        if (magnitude >= 15 || magnitude < -6)
            return CalcValue.FormatNumber(RoundSignificant(value, MaximumSignificantFigures, mode), MaximumSignificantFigures);

        var places = Math.Max(decimals, MinimumSignificantFigures - 1 - magnitude);
        places = Math.Min(places, Math.Max(0, MaximumSignificantFigures - 1 - magnitude));
        places = Math.Clamp(places, 0, 15);
        var rounded = RoundScaled(value, Math.Pow(10, places), mode);

        if (rounded == 0)
            return "0";

        return rounded.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>A plain number (no unit rule): five significant figures, trailing zeros dropped, in the direction <paramref name="mode"/>.</summary>
    public static string Plain(double value, Rounding mode = Rounding.Nearest) =>
        mode == Rounding.Nearest || !double.IsFinite(value) ? CalcValue.FormatNumber(value, MaximumSignificantFigures) : CalcValue.FormatNumber(RoundSignificant(value, MaximumSignificantFigures, mode), MaximumSignificantFigures);

    /// <summary>
    /// A utilisation to 2 decimals (2 significant figures below 0.005, so 0.00022 is not shown as 0.00). A value above <paramref name="limit"/> is
    /// rounded up, so a failing check never prints at or below its limit (1.004 is 1.01, not 1.00); a negative utilisation (a capacity that is
    /// negative or inverted, which never passes) is rounded away from zero.
    /// </summary>
    public static string Utilisation(double u, double limit = 1.0)
    {
        if (!double.IsFinite(u))
            return CalcValue.FormatNumber(u);

        if (u == 0)
            return "0.00";

        var mode = u < 0 ? Rounding.Down : u > limit + 1e-9 ? Rounding.Up : Rounding.Nearest;

        if (Math.Abs(u) <= SmallUtilisation)
        {
            var small = RoundSignificant(u, SmallUtilisationSignificantFigures, mode);
            var places = Math.Clamp(SmallUtilisationSignificantFigures - 1 - MagnitudeOf(small), 0, 15);
            return small.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        var shown = RoundScaled(u, Math.Pow(10, UtilisationDecimals), mode);
        return shown == 0 ? "0.00" : shown.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A utilisation as a saved calculation stores it: an infinite one (a failed yes/no check, a zero capacity) is kept as 1e9, and a check with
    /// no ratio is kept as 0. The register shows it the way the sheet does: two decimals, rounded up when it fails, and "—" for the stored infinity.
    /// </summary>
    public static string StoredUtilisation(double stored) => stored >= 1e9 ? "—" : Utilisation(stored);

    /// <summary>
    /// A scalar as the sheet shows it: the number at the decimals of its quantity type, a space, the unit as an engineer writes it.
    /// <paramref name="kind"/> says how the number is checked (a utilisation is shown to two decimals and rounded up when it fails; a value that
    /// must be at least a limit is rounded down); <paramref name="decimals"/> overrides the table for one output where the template justifies it.
    /// </summary>
    public static string Format(ScalarValue value, CalcOutputKind kind = CalcOutputKind.Value, int? decimals = null, Rounding mode = Rounding.Nearest)
    {
        var unit = value.DisplayUnit.Symbol;
        var unitless = value.Dimension.IsDimensionless && unit.Length == 0;
        var shownUnit = CalcUnitText.Pretty(unit);

        if (unitless && decimals is null && kind == CalcOutputKind.Utilisation)
            return Utilisation(value.Si);

        if (kind == CalcOutputKind.AtLeast && mode == Rounding.Nearest)
            mode = Rounding.Down;

        var places = decimals ?? (unitless ? null : DecimalsFor(value.Dimension, unit));
        var number = places is { } p ? Number(value.Displayed, p, mode) : Plain(value.Displayed, mode);
        return shownUnit.Length == 0 ? number : shownUnit == "°" ? number + shownUnit : $"{number} {shownUnit}";
    }

    /// <summary>
    /// The direction the form rounds an output in: a safety factor (<see cref="CalcOutputKind.AtLeast"/>) down, a utilisation above its limit up (a negative one away from
    /// zero), everything else to the nearest. A reader comparing what the form shows with a reference value may differ from it by a whole last digit, not half of one, in the
    /// first two cases (the guided test mode allows it).
    /// </summary>
    public static Rounding RoundingOf(CalcOutput output)
    {
        if (output.DisplayText is not null || output.Value is not ScalarValue s || !double.IsFinite(s.Si))
            return Rounding.Nearest;

        if (output.Kind == CalcOutputKind.AtLeast)
            return Rounding.Down;

        if (output.Kind == CalcOutputKind.Utilisation && output.Decimals is null && s.Dimension.IsDimensionless)
            return s.Si < 0 ? Rounding.Down : s.Si > 1.0 + 1e-9 ? Rounding.Up : Rounding.Nearest;

        return Rounding.Nearest;
    }

    /// <summary>The text of an output on its own: its kind and decimals decide the form. A value the run recognised as solver noise carries its own text (<see cref="CalcOutput.DisplayText"/>).</summary>
    public static string Format(CalcOutput output) => output.Value is ScalarValue s ? Format(s, output.Kind, output.Decimals) : output.Value.Format(5);

    /// <summary>
    /// The size of the last digit of a shown number: "144.9 MPa" is 0.1, "0.003400 N·m" is 0.000001, "150000 h" is 1, "1.8626e-15 MPa" is 1e-19, so a reader who
    /// types what the form shows can be allowed half of it. <see langword="null"/> when the text does not open with a finite number. The guided test mode takes its
    /// allowance from this, not from a fixed count of figures: the form shows 4 or 5 significant figures, and a whole number as it is.
    /// </summary>
    public static double? ShownResolution(string? shownText) => ShownResolutionExponent(shownText) is { } exponent ? Math.Pow(10, exponent) : null;

    /// <summary>The power of ten of the last digit <see cref="ShownResolution"/> measures ("144.9 MPa" is −1, "150000 h" is 0), as an exact integer for a comparison that must not use floating point.</summary>
    public static int? ShownResolutionExponent(string? shownText)
    {
        if (string.IsNullOrWhiteSpace(shownText))
            return null;

        var m = System.Text.RegularExpressions.Regex.Match(shownText.Trim(), @"^[-−+]?(?<int>\d+)(?:\.(?<frac>\d+))?(?:e(?<exp>[-+]?\d+))?(?![\d.])", System.Text.RegularExpressions.RegexOptions.CultureInvariant | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!m.Success)
            return null;

        var decimals = m.Groups["frac"].Success ? m.Groups["frac"].Length : 0;
        var exponent = m.Groups["exp"].Success && int.TryParse(m.Groups["exp"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var e) ? e : 0;
        return exponent - decimals;
    }

    /// <summary>"0" with the unit of <paramref name="value"/>: how a computed zero that came out as noise is shown.</summary>
    public static string ZeroText(ScalarValue value)
    {
        var unit = CalcUnitText.Pretty(value.DisplayUnit.Symbol);
        return unit.Length == 0 ? "0" : unit == "°" ? "0°" : $"0 {unit}";
    }

    /// <summary>
    /// Whether an output flagged as noise-prone is solver noise: it is more than nine orders of magnitude below the largest working value of its
    /// own dimension in the run (a radial stress that should be zero and is 1.9e-15 MPa beside 144.9 MPa). It is the template that declares an
    /// output noise-prone (a solver residual, an equilibrium sum, a computed zero); an output nobody flagged is never shown as 0 for being small.
    /// </summary>
    public static bool IsNoise(ScalarValue value, double largestWorkingValueSi) =>
        value.IsFinite && value.Si != 0 && largestWorkingValueSi > 0 && Math.Abs(value.Si) < NoiseFraction * largestWorkingValueSi;
}
