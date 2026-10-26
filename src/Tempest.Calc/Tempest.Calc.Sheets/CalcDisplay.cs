using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 29 and 30: how a calculated number is shown. The decimals depend on the quantity type (its dimension and the unit it is shown
/// in), not on the template: a force is 2 decimals in kN, a stress 1 decimal in MPa, a utilisation 2 decimals. The table is the one list
/// (<see cref="Rules"/>); the glossary's "Decimals" column is generated from it and a test compares the two. The number printed is the
/// calculated value rounded for display only: nothing computed, stored or exported is rounded here.
/// </summary>
/// <remarks>
/// <para>A value in a unit the table does not list is shown to five significant figures, as before.</para>
/// <para>Two limits keep fixed decimals honest. A value is never shown with fewer than two significant figures (0.012 mm is not "0.0 mm"),
/// and never with more than five (321 190.4 kN/m is "321190", 2194.80 N·m is "2194.8"). Zero is "0".</para>
/// <para>A utilisation is shown to 2 decimals and, when it is above 1, rounded up, so a failing check never prints 1.00. A value more than
/// nine orders of magnitude below the largest value of its dimension in the same results is solver noise and is shown as 0
/// (<see cref="Texts"/>).</para>
/// </remarks>
public static class CalcDisplay
{
    /// <summary>One line of the precision table: a quantity type, the dimension and unit it is shown in, and its decimals.</summary>
    public sealed record Rule(string Quantity, Dimension Dimension, string Unit, int Decimals);

    /// <summary>The decimals of a utilisation and of the ratios named in <see cref="RatioDecimals"/>.</summary>
    public const int UtilisationDecimals = 2;

    /// <summary>A value below this fraction of the largest value of its dimension in the same results is shown as 0.</summary>
    public const double NoiseFraction = 1e-9;

    /// <summary>At least this many significant figures are always shown.</summary>
    public const int MinimumSignificantFigures = 2;

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
    /// The decimals of a dimensionless number that is a utilisation, a safety factor, an efficiency or a power factor, found by its symbol and
    /// its description (<c>u_life</c>, "Static safety factor"), or <see langword="null"/> for any other plain number (five significant figures).
    /// </summary>
    public static int? RatioDecimals(string? symbol, string? description)
    {
        if (IsUtilisation(symbol, description))
            return UtilisationDecimals;

        if (description is { Length: > 0 } d
            && (d.Contains("safety factor", StringComparison.OrdinalIgnoreCase) || d.Contains("factor of safety", StringComparison.OrdinalIgnoreCase)
                || d.Contains("efficiency", StringComparison.OrdinalIgnoreCase) || d.Contains("power factor", StringComparison.OrdinalIgnoreCase)))
        {
            return 2;
        }

        return null;
    }

    /// <summary>Whether a dimensionless result is a utilisation: its symbol is <c>u</c> or <c>u_…</c> (or ends <c>_util</c>), or its description says so.</summary>
    public static bool IsUtilisation(string? symbol, string? description)
    {
        if (symbol is { Length: > 0 } s && (s == "u" || s.StartsWith("u_", StringComparison.Ordinal) || s == "util" || s.EndsWith("_util", StringComparison.Ordinal)))
            return true;

        return description is { Length: > 0 } d && (d.Contains("utilisation", StringComparison.OrdinalIgnoreCase) || d.Contains("utilization", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A number to <paramref name="decimals"/> places, within the significant-figure limits; zero is "0".</summary>
    public static string Number(double value, int decimals)
    {
        if (!double.IsFinite(value))
            return CalcValue.FormatNumber(value);

        if (value == 0)
            return "0";

        var magnitude = (int)Math.Floor(Math.Log10(Math.Abs(value)));

        if (magnitude >= 15 || magnitude < -6)
            return CalcValue.FormatNumber(value, 5);

        var places = Math.Max(decimals, MinimumSignificantFigures - 1 - magnitude);
        places = Math.Min(places, Math.Max(0, MaximumSignificantFigures - 1 - magnitude));
        places = Math.Clamp(places, 0, 15);
        var rounded = Math.Round(value, places, MidpointRounding.ToEven);

        if (rounded == 0)
            return "0";

        return rounded.ToString("F" + places.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>A utilisation to 2 decimals, rounded up when it is above 1 so a failing check never prints 1.00.</summary>
    public static string Utilisation(double u)
    {
        if (!double.IsFinite(u))
            return CalcValue.FormatNumber(u);

        var shown = u > 1.0 + 1e-9 ? Math.Ceiling((u * 100) - 1e-9) / 100 : Math.Round(u, UtilisationDecimals, MidpointRounding.ToEven);
        return shown == 0 ? "0.00" : shown.ToString("F2", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A scalar as the sheet shows it: the number at the decimals of its quantity type, a space, the unit as an engineer writes it.
    /// <paramref name="decimals"/> overrides the table for one output where the template justifies it.
    /// </summary>
    public static string Format(ScalarValue value, string? symbol = null, string? description = null, int? decimals = null)
    {
        var unit = value.DisplayUnit.Symbol;
        var unitless = value.Dimension.IsDimensionless && unit.Length == 0;
        var shownUnit = CalcUnitText.Pretty(unit);

        if (unitless && decimals is null && IsUtilisation(symbol, description))
            return Utilisation(value.Si);

        var places = decimals ?? (unitless ? RatioDecimals(symbol, description) : DecimalsFor(value.Dimension, unit));
        var number = places is { } p ? Number(value.Displayed, p) : CalcValue.FormatNumber(value.Displayed, 5);
        return shownUnit.Length == 0 ? number : shownUnit == "°" ? number + shownUnit : $"{number} {shownUnit}";
    }

    /// <summary>The text of an output on its own, without the noise rule (which needs the other outputs; see <see cref="Texts"/>).</summary>
    public static string Format(CalcOutput output) => output.Value is ScalarValue s ? Format(s, output.Symbol, output.Description, output.Decimals) : output.Value.Format(5);

    /// <summary>
    /// The text of every output of a run, with the noise rule: a value more than nine orders of magnitude below the largest value of its
    /// dimension among the outputs (a stress that should be zero and is 1.9e-15 MPa beside 144.9 MPa) is shown as 0. The values themselves
    /// are not changed.
    /// </summary>
    public static IReadOnlyList<string> Texts(IReadOnlyList<CalcOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(outputs);

        var largest = new Dictionary<Dimension, double>();

        foreach (var o in outputs)
        {
            if (o.Value is ScalarValue { IsFinite: true } s && !s.Dimension.IsDimensionless)
                largest[s.Dimension] = Math.Max(largest.GetValueOrDefault(s.Dimension), Math.Abs(s.Si));
        }

        var texts = new List<string>(outputs.Count);

        foreach (var o in outputs)
        {
            if (o.Value is ScalarValue { IsFinite: true } s && s.Si != 0 && largest.TryGetValue(s.Dimension, out var max) && Math.Abs(s.Si) < NoiseFraction * max)
            {
                var unit = CalcUnitText.Pretty(s.DisplayUnit.Symbol);
                texts.Add(unit.Length == 0 ? "0" : unit == "°" ? "0°" : $"0 {unit}");
            }
            else
            {
                texts.Add(Format(o));
            }
        }

        return texts;
    }
}
