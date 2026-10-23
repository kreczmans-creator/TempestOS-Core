using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M2c — thick-walled cylinder (Lamé) under internal and external pressure, migrated from `calc.thick-walled-cylinder` (v0.16) onto the worksheet engine; the old definition and its tests stay.</summary>
public sealed class ThickCylinderCalculator : ICalcCalculator
{
    public const string Id = "mech.thick-cylinder";

    public CalcTemplate Template { get; } = new(
        Id, "2.0.0", CalcDiscipline.Mechanical, "Thick-walled cylinder stresses (Lamé)",
        "Radial, hoop and axial stresses at the bore and the outer surface of a thick-walled cylinder under internal and external pressure, the von Mises and Tresca equivalents, utilisation against an allowable, and the thin-wall estimate for comparison.",
        "A = (a² p_i − b² p_o)/(b² − a²), B = a² b² (p_i − p_o)/(b² − a²); σ_r = A − B/r², σ_θ = A + B/r²; σ_z = A for closed ends, 0 for open. von Mises from the three principal stresses; Tresca = max principal difference.",
        [
            new CalcInputSpec("a", "Inner radius", CalcInputKind.Quantity, "mm", Default: 50, Min: 0.1, Max: 5000, HardMin: 1e-6, HardMax: 1e6),
            new CalcInputSpec("b", "Outer radius", CalcInputKind.Quantity, "mm", Default: 100, Min: 0.1, Max: 5000, HardMin: 1e-6, HardMax: 1e6),
            new CalcInputSpec("p_i", "Internal pressure", CalcInputKind.Quantity, "MPa", Default: 50, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5),
            new CalcInputSpec("p_o", "External pressure", CalcInputKind.Quantity, "MPa", Default: 0, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e5),
            new CalcInputSpec("closed", "Closed ends (axial stress carried by the wall)", CalcInputKind.Boolean, Default: 1),
            new CalcInputSpec("sigma_allow", "Allowable stress", CalcInputKind.Quantity, "MPa", Default: 200, Min: 1, Max: 3000, HardMin: 1e-3, HardMax: 1e5),
        ],
        [
            new CalcAssumption("Long cylinder, elastic, isotropic; stresses vary with radius only.", "Lamé solution; end effects neglected."),
            new CalcAssumption("Closed ends carry the pressure end load as a uniform axial stress.", "σ_z = A."),
        ],
        [
            new CalcClauseRef("Roark's Formulas for Stress and Strain", "Ch. 13, Table 13.5", "Thick-walled cylinders"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "§3-14", "Stresses in pressurised cylinders"),
        ],
        [
            new CalcApplicabilityRule("geometry", "The outer radius must exceed the inner radius.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("no-pressure", "Both pressures are zero: nothing to compute.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("thin-wall", "Wall thinner than 5 % of the inner radius: the thin-wall estimate is within a few percent of Lamé.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-internal-closed", "Internal pressure, closed ends (v0.16 vector 1)",
                new CalcReferenceSource("Analytic (Lamé), cross-checked against the v0.16 calc.thick-walled-cylinder vectors", null, "a = 50, b = 100 mm, p_i = 50 MPa: σ_θ,bore = 83.333 MPa, σ_z = 16.667 MPa, von Mises bore = 115.47 MPa"),
                [CalcInputValue.Quantity("a", 50, "mm"), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("p_i", 50, "MPa"), CalcInputValue.Quantity("p_o", 0, "MPa"), CalcInputValue.Switch("closed", true), CalcInputValue.Quantity("sigma_allow", 200, "MPa")],
                [new CalcExpectedValue("sigma_t_a", 83.333, "MPa", 5e-5), new CalcExpectedValue("sigma_r_a", -50, "MPa", 1e-6), new CalcExpectedValue("sigma_t_b", 33.333, "MPa", 5e-5), new CalcExpectedValue("sigma_z", 16.667, "MPa", 5e-5), new CalcExpectedValue("sigma_vm_a", 115.47, "MPa", 5e-5), new CalcExpectedValue("sigma_tr_a", 133.33, "MPa", 5e-5), new CalcExpectedValue("u", 0.57735, "", 5e-5), new CalcExpectedValue("sigma_thin", 75, "MPa", 1e-6), new CalcExpectedValue("sigma_vm_b", 28.868, "MPa", 5e-5)]),
            new CalcTestCase("ka-external-closed", "External pressure, closed ends (v0.16 vector 2)",
                new CalcReferenceSource("Analytic (Lamé), cross-checked against the v0.16 vectors", null, "a = 40, b = 60 mm, p_o = 20 MPa: σ_θ,bore = −72 MPa, σ_θ,outer = −52 MPa, σ_z = −36 MPa, von Mises bore = 62.354 MPa"),
                [CalcInputValue.Quantity("a", 40, "mm"), CalcInputValue.Quantity("b", 60, "mm"), CalcInputValue.Quantity("p_i", 0, "MPa"), CalcInputValue.Quantity("p_o", 20, "MPa"), CalcInputValue.Switch("closed", true), CalcInputValue.Quantity("sigma_allow", 100, "MPa")],
                [new CalcExpectedValue("sigma_t_a", -72, "MPa", 1e-6), new CalcExpectedValue("sigma_t_b", -52, "MPa", 1e-6), new CalcExpectedValue("sigma_z", -36, "MPa", 1e-6), new CalcExpectedValue("sigma_vm_a", 62.354, "MPa", 5e-5), new CalcExpectedValue("sigma_vm_b", 27.713, "MPa", 5e-5), new CalcExpectedValue("u", 0.62354, "", 5e-5), new CalcExpectedValue("sigma_thin", -50, "MPa", 5e-5)]),
            new CalcTestCase("ka-internal-open", "Internal pressure, open ends (v0.16 vector 3)",
                new CalcReferenceSource("Analytic (Lamé)", null, "as vector 1 with σ_z = 0: von Mises bore = 116.67 MPa, outer = 33.333 MPa"),
                [CalcInputValue.Quantity("a", 50, "mm"), CalcInputValue.Quantity("b", 100, "mm"), CalcInputValue.Quantity("p_i", 50, "MPa"), CalcInputValue.Quantity("p_o", 0, "MPa"), CalcInputValue.Switch("closed", false), CalcInputValue.Quantity("sigma_allow", 200, "MPa")],
                [new CalcExpectedValue("sigma_vm_a", 116.67, "MPa", 5e-5), new CalcExpectedValue("sigma_vm_b", 33.333, "MPa", 5e-5), new CalcExpectedValue("u", 0.58333, "", 5e-5), new CalcExpectedValue("sigma_t_a", 83.333, "MPa", 5e-5), new CalcExpectedValue("sigma_z", 0, "MPa", 1e-9), new CalcExpectedValue("sigma_thin", 75, "MPa", 5e-5)]),
        ],
        DiagramKey: "thick-walled-cylinder",
        Category: "Pressure Systems");

    public void Run(CalcRunContext c)
    {
        c.Input("a");
        c.Input("b");
        c.Input("p_i");
        c.Input("p_o");
        var closed = c.InputFlag("closed");
        c.Input("sigma_allow");

        if (!(c.Si("b") > c.Si("a")))
        {
            c.Block("geometry", "b: the outer radius must exceed the inner radius.");
            return;
        }

        if (c.Si("p_i") == 0 && c.Si("p_o") == 0)
        {
            c.Block("no-pressure", "p_i/p_o: both pressures are zero; there is nothing to compute.");
            return;
        }

        if (c.Si("b") - c.Si("a") < 0.05 * c.Si("a"))
            c.Warn("thin-wall", "b: the wall is thinner than 5 % of the inner radius; the thin-wall estimate is close to Lamé here.");

        c.Assign("A", "(a^2 * p_i - b^2 * p_o) / (b^2 - a^2)", "MPa", "Lamé constant A", "Roark Table 13.5");
        c.Assign("B", "a^2 * b^2 * (p_i - p_o) / (b^2 - a^2)", "MPa*mm^2", "Lamé constant B", "Roark Table 13.5");
        c.Assign("sigma_r_a", "A - B / a^2", "MPa", "Radial stress at the bore");
        c.Assign("sigma_t_a", "A + B / a^2", "MPa", "Hoop stress at the bore");
        c.Assign("sigma_r_b", "A - B / b^2", "MPa", "Radial stress at the outer surface");
        c.Assign("sigma_t_b", "A + B / b^2", "MPa", "Hoop stress at the outer surface");
        c.Assign("sigma_z", closed ? "A" : "0 MPa", "MPa", closed ? "Axial stress (closed ends)" : "Axial stress (open ends)");
        c.Assign("sigma_vm_a", "sqrt(((sigma_t_a - sigma_r_a)^2 + (sigma_r_a - sigma_z)^2 + (sigma_z - sigma_t_a)^2) / 2)", "MPa", "von Mises stress at the bore");
        c.Assign("sigma_vm_b", "sqrt(((sigma_t_b - sigma_r_b)^2 + (sigma_r_b - sigma_z)^2 + (sigma_z - sigma_t_b)^2) / 2)", "MPa", "von Mises stress at the outer surface");
        c.Assign("sigma_tr_a", "max(sigma_t_a, sigma_r_a, sigma_z) - min(sigma_t_a, sigma_r_a, sigma_z)", "MPa", "Tresca stress at the bore");
        c.Assign("sigma_tr_b", "max(sigma_t_b, sigma_r_b, sigma_z) - min(sigma_t_b, sigma_r_b, sigma_z)", "MPa", "Tresca stress at the outer surface");
        c.Assign("sigma_vm_max", "max(sigma_vm_a, sigma_vm_b)", "MPa", "Maximum von Mises stress");
        c.Assign("u", "sigma_vm_max / sigma_allow", null, "Utilisation");
        c.Assign("sigma_thin", "(p_i - p_o) * (a + b) / 2 / (b - a)", "MPa", "Thin-wall hoop estimate at the mean radius");
        c.Assign("k_wall", "b / a", null, "Wall ratio b/a");

        foreach (var (symbol, text) in new[] { ("sigma_r_a", "Radial stress, bore"), ("sigma_t_a", "Hoop stress, bore"), ("sigma_r_b", "Radial stress, outer"), ("sigma_t_b", "Hoop stress, outer"), ("sigma_z", "Axial stress"), ("sigma_vm_a", "von Mises, bore"), ("sigma_vm_b", "von Mises, outer"), ("sigma_tr_a", "Tresca, bore"), ("sigma_tr_b", "Tresca, outer"), ("sigma_vm_max", "Maximum von Mises"), ("u", "Utilisation"), ("sigma_thin", "Thin-wall hoop estimate"), ("k_wall", "Wall ratio") })
            c.Output(symbol, text);

        c.CheckUtilisation("von Mises against allowable", "u");
    }
}
