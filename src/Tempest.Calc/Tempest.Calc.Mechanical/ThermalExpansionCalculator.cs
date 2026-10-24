using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Thermal expansion of a bar between restraints: free movement, the part
/// taken up by a gap, the restraint force for a rigid or elastic restraint,
/// the resulting stress and the actual movement. Ported from the legacy
/// <c>calc.thermal-expansion-stress</c> module (ADR-0167); its five
/// hand-worked examples run as known-answer cases.
/// </summary>
public sealed class ThermalExpansionCalculator : ICalcCalculator
{
    public const string Id = "mech.thermal-expansion";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.thermal-expansion-stress (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.thermal-expansion-stress.md, Example {n}");

    private static CalcInputValue[] Steel(double dT, double gap, double? ks = null) =>
    [
        CalcInputValue.Quantity("L", 2, "m"), CalcInputValue.Quantity("A", 500, "mm^2"), CalcInputValue.Quantity("E", 200, "GPa"),
        CalcInputValue.Quantity("alpha", 12e-6, "1/K"), CalcInputValue.Quantity("dT", dT, "K"), CalcInputValue.Quantity("g", gap, "mm"),
        CalcInputValue.Quantity("sigma_allow", 150, "MPa"),
        .. ks is { } k ? [CalcInputValue.Quantity("k_s", k, "kN/mm")] : Array.Empty<CalcInputValue>(),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Mechanical, "Thermal expansion of a restrained bar",
        "A bar heated or cooled between two restraints: its free movement, the part closed by a gap before the restraint engages, the restraint force for a rigid or an elastic restraint, the axial stress that results and the movement that actually occurs, with the stress against an allowable.",
        "Free expansion of the bar: δ_free = α·L·ΔT.\nExpansion actually restrained, once any gap g has closed: δ_e = sign(δ_free)·max(|δ_free| − g, 0).\nAxial stiffness of the bar: k_bar = E·A/L.\nRestraint force, from rigid supports or an elastic restraint k_s: F = δ_e·k_bar (rigid); F = δ_e/(1/k_bar + 1/k_s) (elastic).\nThermal stress in the bar, compressive on heating: σ = −F/A.\nNet movement of the bar end: δ = δ_free − F/k_bar.",
        [
            new CalcInputSpec("L", "Bar length", CalcInputKind.Quantity, "m", Default: 2, Min: 0.01, Max: 1000, HardMin: 1e-6, HardMax: 1e5),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "mm^2", Default: 500, Min: 1, Max: 1e6, HardMin: 1e-6, HardMax: 1e9),
            new CalcInputSpec("E", "Modulus of elasticity", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 200, Min: 1, Max: 500, HardMin: 1e-3, HardMax: 1e4),
            new CalcInputSpec("alpha", "Coefficient of thermal expansion", CalcInputKind.Quantity, "1/K", ReferenceLibrary: "Materials", ReferenceProperty: "ThermalExpansionCoefficient", ReferenceDimension: "ThermalExpansion", Default: 12e-6, Min: 1e-7, Max: 1e-4, HardMin: 1e-12, HardMax: 1e-2),
            new CalcInputSpec("dT", "Temperature change (positive heating)", CalcInputKind.Quantity, "K", Default: 60, Min: -300, Max: 1000, HardMin: -1e4, HardMax: 1e4),
            new CalcInputSpec("g", "Gap closed before the restraint engages", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e6),
            new CalcInputSpec("k_s", "Restraint stiffness (leave empty for a rigid restraint)", CalcInputKind.Quantity, "kN/mm", Min: 0.001, Max: 1e7, HardMin: 1e-12, HardMax: 1e12, IsOptional: true),
            new CalcInputSpec("sigma_allow", "Allowable stress", CalcInputKind.Quantity, "MPa", Default: 150, Min: 1, Max: 2000, HardMin: 1e-3, HardMax: 1e5),
        ],
        [
            new CalcAssumption("The expansion coefficient is constant over the temperature change and the bar stays elastic.", "Linear thermoelasticity."),
            new CalcAssumption("The bar is uniformly heated and does not buckle under the compressive force.", "Check buckling separately (struct.column-buckling)."),
        ],
        [
            new CalcClauseRef("Roark's Formulas for Stress and Strain", "Ch. 15", "Thermal stresses"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§3-17", "Thermal stresses and strains"),
        ],
        [],
        [
            new CalcTestCase("ka-legacy-rigid", "Legacy example 1: rigid walls, no gap, +60 K", Legacy(1), Steel(60, 0),
                [new CalcExpectedValue("delta_free", 1.4400, "mm", 5e-5), new CalcExpectedValue("F", 72_000, "N", 5e-5), new CalcExpectedValue("sigma", -144.00, "MPa", 5e-5), new CalcExpectedValue("delta", 0, "mm", 1e-9)]),
            new CalcTestCase("ka-legacy-gap", "Legacy example 2: rigid walls, 0.5 mm gap, +60 K", Legacy(2), Steel(60, 0.5),
                [new CalcExpectedValue("delta_e", 0.94, "mm", 5e-5), new CalcExpectedValue("F", 47_000, "N", 5e-5), new CalcExpectedValue("sigma", -94.000, "MPa", 5e-5), new CalcExpectedValue("delta", 0.50000, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-elastic", "Legacy example 3: elastic restraint 100 kN/mm, +60 K", Legacy(3), Steel(60, 0, 100),
                [new CalcExpectedValue("F", 48_000, "N", 5e-5), new CalcExpectedValue("sigma", -96.000, "MPa", 5e-5), new CalcExpectedValue("delta", 0.48000, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-cooling", "Legacy example 4: cooling −40 K, rigid", Legacy(4), Steel(-40, 0),
                [new CalcExpectedValue("delta_free", -0.96000, "mm", 5e-5), new CalcExpectedValue("F", -48_000, "N", 5e-5), new CalcExpectedValue("sigma", 96.000, "MPa", 5e-5), new CalcExpectedValue("delta", 0, "mm", 1e-9)]),
            new CalcTestCase("ka-legacy-gap-open", "Legacy example 5: +10 K against a 0.5 mm gap (no contact)", Legacy(5), Steel(10, 0.5),
                [new CalcExpectedValue("F", 0, "N", 1e-9), new CalcExpectedValue("sigma", 0, "MPa", 1e-9), new CalcExpectedValue("delta", 0.24000, "mm", 5e-5)]),
        ],
        Category: "Thermal");

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "L", "A", "E", "alpha", "dT", "g", "sigma_allow" })
            c.Input(s);

        var elastic = c.Has("k_s");

        if (elastic)
            c.Input("k_s");

        c.Assign("delta_free", "alpha * L * dT", "mm", "Free thermal movement (positive on heating)", "Shigley §3-17");
        var free = c.Si("delta_free");
        var gap = c.Si("g");

        if (Math.Abs(free) <= gap)
            c.Assign("delta_e", "0 mm", "mm", "Movement after the gap: the gap is not closed");
        else
            c.Assign("delta_e", free > 0 ? "delta_free - g" : "delta_free + g", "mm", "Movement after the gap closes (engaged movement)");

        c.Assign("k_bar", "E * A / L", "kN/mm", "Axial stiffness of the bar");
        c.Assign("F", elastic ? "delta_e / (1 / k_bar + 1 / k_s)" : "delta_e * k_bar", "N", elastic ? "Restraint force (elastic restraint in series with the bar)" : "Restraint force (rigid restraint)", "Roark Ch. 15");
        c.Assign("sigma", "-F / A", "MPa", "Axial stress (negative compressive)");
        c.Assign("delta", "delta_free - F / k_bar", "mm", "Actual movement");
        c.Assign("u", "abs(sigma) / sigma_allow", null, "Stress utilisation");

        foreach (var (symbol, text) in new[] { ("delta_free", "Free movement"), ("delta_e", "Engaged movement"), ("k_bar", "Bar stiffness"), ("F", "Restraint force"), ("sigma", "Axial stress"), ("delta", "Actual movement"), ("u", "Stress utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Thermal stress against allowable", "u");
    }
}
