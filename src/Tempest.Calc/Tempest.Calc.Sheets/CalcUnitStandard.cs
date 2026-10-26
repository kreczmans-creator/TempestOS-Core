using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 20 to 28: the unit a quantity type is entered and shown in. One list for every template, so the output of one sheet and the
/// input it feeds on the next are in the same unit (the audit's linked-sheet hazards: N·m against kN·m, m³/s against m³/h, kPa against
/// bar). A unit is listed by its symbol as it is shown (<see cref="CalcUnitText.Pretty"/>), so <c>kN*m</c> and <c>kN·m</c> are one.
/// A quantity type may name a template that keeps another unit for a reason (<see cref="Exception"/>); an exception is a decision, written in
/// <c>docs/calcs/sheet-standard.md</c>, not a way round the rule.
/// </summary>
public static class CalcUnitStandard
{
    /// <summary>A template that keeps a different unit for a quantity type, and why.</summary>
    public sealed record Exception(string TemplateIdPrefix, IReadOnlyList<string> Units, string Reason);

    /// <summary>One quantity type: the dimension it measures, the units it is written in, and the exceptions.</summary>
    public sealed record Quantity(string Name, Dimension Dimension, IReadOnlyList<string> Units, IReadOnlyList<Exception> Exceptions);

    private static Quantity Q(string name, Dimension dimension, string[] units, params Exception[] exceptions) => new(name, dimension, units, exceptions);

    private static Exception X(string prefix, string reason, params string[] units) => new(prefix, units, reason);

    private static readonly Dimension LineForce = Dimension.Force / Dimension.L;
    private static readonly Dimension Resistivity = Dimension.Resistance * Dimension.L;
    private static readonly Dimension SixthMoment = Dimension.L.Pow(6);

    /// <summary>The quantity types the standard fixes a unit for. A dimension not listed (a velocity, a density, a conductivity) has no standard unit.</summary>
    public static IReadOnlyList<Quantity> Quantities { get; } =
    [
        Q("Force", Dimension.Force, ["kN"],
            X("mech.spring", "small spring and latch forces are below 1 kN", "N"), X("mech.snap-fit", "latch forces are a few newtons", "N"), X("mech.mechanism", "linkage and cam forces are small", "N"),
            X("elec.motor-sizing", "drive sizing works in newtons and N·m", "N"), X("elec.containment", "cable pulling tension is in newtons", "N"),
            X("elec.enclosure-heat", "the door force from a pressure difference is in newtons", "N"), X("fp.", "hydraulic load tables are in kN or N", "N")),
        Q("Line force, distributed load, stiffness", LineForce, ["kN/m"],
            X("mech.spring", "spring rates are in N/mm", "N/mm"), X("mech.weld-group", "weld stress per unit length is in N/mm, the unit of a weld throat", "N/mm"),
            X("elec.containment", "sidewall pressure is in N/m", "N/m")),
        Q("Moment, torque", Dimension.Torque, ["kN·m"],
            X("elec.motor-sizing", "drive sizing is the one place that works in N·m", "N·m"), X("mech.mechanism", "drive torque in N·m, as drive sizing", "N·m"),
            X("fp.", "hydraulic drive torque in N·m", "N·m"), X("mech.spring", "spring torque in N·m", "N·m"), X("mech.snap-fit", "latch torque in N·m", "N·m")),
        Q("Energy", Dimension.Energy, ["J", "kJ", "MJ", "Wh", "kWh"]),
        Q("Moment per unit length", Dimension.Force, ["kN·m/m"]),
        Q("Stress, pressure", Dimension.Pressure, ["Pa", "kPa", "MPa", "GPa"],
            X("fp.", "hydraulic practice states pressure in bar, absolute (a) or gauge (g)", "bar", "bar a", "bar g")),
        Q("Length", Dimension.L, ["m", "mm", "µm"], X("elec.pcb-trace", "PCB practice is mil", "mil")),
        Q("Area", Dimension.Area, ["m²", "cm²", "mm²"], X("elec.pcb-trace", "PCB practice is mil²", "mil²")),
        Q("Second moment of area", Dimension.SecondMomentOfArea, ["cm⁴", "mm⁴"]),
        Q("Warping constant", SixthMoment, ["cm⁶"]),
        Q("Volume, section modulus", Dimension.Volume, ["L", "m³", "cm³", "mm³"], X("fp.", "displacement per revolution is in cm³/rev", "cm³/rev")),
        Q("Volumetric flow", Dimension.VolumeFlow, ["m³/h"], X("fp.", "hydraulic flow is in L/min", "L/min")),
        Q("Power, heat flow", Dimension.Power, ["kW", "MW"], X("elec.pcb-trace", "PCB loss is in milliwatts", "mW", "W")),
        Q("Apparent and reactive power", Dimension.Power, ["kVA", "MVA", "kvar"]),
        Q("Temperature", Dimension.Theta, ["°C"]),
        Q("Temperature, temperature difference", Dimension.Theta, ["K"]),
        Q("Resistivity", Resistivity, ["Ω·mm²/m"]),
    ];

    /// <summary>
    /// The problem with <paramref name="unit"/> as the unit of a quantity of <paramref name="dimension"/> in the template
    /// <paramref name="templateId"/>, or <see langword="null"/> when the standard accepts it (or has no standard for the dimension).
    /// </summary>
    public static string? Problem(string templateId, Dimension dimension, string unit)
    {
        if (string.IsNullOrEmpty(unit))
            return null;

        var shown = CalcUnitText.Pretty(unit);

        foreach (var quantity in Quantities.Where(q => q.Dimension == dimension))
        {
            if (quantity.Units.Contains(shown, StringComparer.Ordinal))
                return null;

            if (quantity.Exceptions.Any(e => templateId.StartsWith(e.TemplateIdPrefix, StringComparison.Ordinal) && e.Units.Contains(shown, StringComparer.Ordinal)))
                return null;
        }

        var types = Quantities.Where(q => q.Dimension == dimension).ToList();
        return types.Count == 0 ? null : $"{shown} is not a standard unit of {types[0].Name} ({string.Join(", ", types.SelectMany(q => q.Units))})";
    }
}
