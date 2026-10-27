using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// Worked examples for the mechanical templates ported from the legacy
/// calculation modules (ADR-0167). Inputs carry the legacy hand calculation's
/// figures; each input's source says where such a figure comes from on a job.
/// </summary>
public static class MechanicalPortWorkedExamples
{
    /// <summary>Gives every value a source: loads from the load schedule, criteria from the specification, the rest from the drawing.</summary>
    private static CalcInputSet Sourced(string drawing, string[] loads, string[] criteria, params CalcInputValue[] values) =>
        new([.. values.Select(v => v with
        {
            Provenance = loads.Contains(v.Symbol) ? CalcProvenance.Specification("LOAD-SCHEDULE-01")
                : criteria.Contains(v.Symbol) ? CalcProvenance.Specification("SPEC-MECH-03 design criteria")
                : CalcProvenance.Drawing(drawing, "A"),
        })]);

    private static CalcReferenceSource Legacy(string module, int n) =>
        new($"TempestOS {module} (v0.21.0) hand calculation", null, $"docs/engineering/calculations/{module}.md, Example {n}");

    private static readonly string[] LugCriteria = ["f_t", "f_br", "f_v", "f_b_pin", "f_v_pin"];

    private static CalcInputValue[] Lug(double p, double t, double w, double dh, double dp, double a, double ts, double g) =>
    [
        CalcInputValue.Quantity("F_lug", p, "kN"), CalcInputValue.Quantity("t_lug", t, "mm"), CalcInputValue.Quantity("W", w, "mm"), CalcInputValue.Quantity("d_h", dh, "mm"),
        CalcInputValue.Quantity("d_p", dp, "mm"), CalcInputValue.Quantity("a", a, "mm"), CalcInputValue.Quantity("t_s", ts, "mm"), CalcInputValue.Quantity("g", g, "mm"),
        CalcInputValue.Quantity("f_t", 150, "MPa"), CalcInputValue.Quantity("f_br", 200, "MPa"), CalcInputValue.Quantity("f_v", 90, "MPa"),
        CalcInputValue.Quantity("f_b_pin", 250, "MPa"), CalcInputValue.Quantity("f_v_pin", 150, "MPa"),
    ];

    private static CalcInputValue[] Thermal(double dT, double gap, double? ks) =>
    [
        CalcInputValue.Quantity("L", 2, "m"), CalcInputValue.Quantity("A", 500, "mm^2"), CalcInputValue.Quantity("E", 200, "GPa"), CalcInputValue.Quantity("alpha", 12e-6, "1/K"),
        CalcInputValue.Quantity("dT", dT, "K"), CalcInputValue.Quantity("g", gap, "mm"), CalcInputValue.Quantity("sigma_allow", 150, "MPa"),
        .. ks is { } k ? [CalcInputValue.Quantity("k_s", k, "kN/mm")] : Array.Empty<CalcInputValue>(),
    ];

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(LiftingLugCalculator.Id, "lug-50kN-passes", "Lifting lug, 50 kN — passes, pin bending governs", Legacy("calc.lifting-lug-pin-joint", 1),
            Sourced("DRG-LL-101", ["F_lug"], LugCriteria, Lug(50, 20, 100, 32, 30, 40, 12, 2)),
            [new CalcExpectedValue("sigma_t", 36.765, "MPa", 5e-5), new CalcExpectedValue("sigma_br", 83.333, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 122.61, "MPa", 5e-5), new CalcExpectedValue("u_b", 0.49043, "", 5e-5)],
            "A 20 mm lug on a 30 mm pin between 12 mm cheek plates. All five checks pass; the pin in bending governs at 0.49, as it often does — the lug plate itself is lightly stressed."),
        new(LiftingLugCalculator.Id, "lug-120kN-fails", "Lifting lug, 120 kN — fails in bearing, tear-out and pin bending", Legacy("calc.lifting-lug-pin-joint", 2),
            Sourced("DRG-LL-102", ["F_lug"], LugCriteria, Lug(120, 15, 90, 26, 25, 30, 10, 1)),
            [new CalcExpectedValue("sigma_br", 320.00, "MPa", 5e-5), new CalcExpectedValue("tau_to", 133.33, "MPa", 5e-5), new CalcExpectedValue("sigma_b", 381.36, "MPa", 5e-5), new CalcExpectedValue("u_br", 1.6000, "", 5e-5)],
            "The same detail scaled down and loaded to 120 kN. Bearing (1.60), tear-out (1.48) and pin bending (1.53) all fail; bearing governs. A thicker lug fixes the first two; the pin needs a larger diameter."),

        new(BearingLifeBasicCalculator.Id, "ball-bearing-radial", "Deep-groove ball bearing under radial load", Legacy("calc.bearing-rating-life", 1),
            Sourced("DRG-GB-210", ["F_r", "F_a", "n"], ["a_1", "L_req"], CalcInputValue.Choice("type", BearingLifeBasicCalculator.Ball), CalcInputValue.Quantity("C", 35.1, "kN"), CalcInputValue.Quantity("F_r", 4, "kN"), CalcInputValue.Quantity("F_a", 0, "kN"), CalcInputValue.Of("X", 1), CalcInputValue.Of("Y", 0), CalcInputValue.Quantity("n", 1500, "rpm"), CalcInputValue.Of("a_1", 1), CalcInputValue.Quantity("L_req", 5000, "h")),
            [new CalcExpectedValue("F_dyn", 4.000, "kN", 5e-5), new CalcExpectedValue("L_10", 675.68, "", 5e-5), new CalcExpectedValue("L_10h", 7507.6, "h", 5e-5)],
            "A 35.1 kN-rated ball bearing at 4 kN and 1,500 rev/min. Its basic rating life is 7,500 h against 5,000 h required."),
        new(BearingLifeBasicCalculator.Id, "ball-bearing-combined-95", "Ball bearing, combined load, 95 % reliability", Legacy("calc.bearing-rating-life", 3),
            Sourced("DRG-GB-211", ["F_r", "F_a", "n"], ["a_1", "L_req"], CalcInputValue.Choice("type", BearingLifeBasicCalculator.Ball), CalcInputValue.Quantity("C", 25, "kN"), CalcInputValue.Quantity("F_r", 3, "kN"), CalcInputValue.Quantity("F_a", 1.5, "kN"), CalcInputValue.Of("X", 0.56), CalcInputValue.Of("Y", 1.5), CalcInputValue.Quantity("n", 1000, "rpm"), CalcInputValue.Of("a_1", 0.64), CalcInputValue.Quantity("L_req", 2000, "h")),
            [new CalcExpectedValue("F_dyn", 3.930, "kN", 5e-5), new CalcExpectedValue("L_10h", 4290.3, "h", 5e-5), new CalcExpectedValue("L_nmh", 2745.8, "h", 5e-5)],
            "Radial and axial load combine through the catalogue X and Y factors into a 3.93 kN equivalent load. At 95 % reliability (a₁ = 0.64) the life is 2,746 h against 2,000 h required."),

        new(FatigueMinerCalculator.Id, "detail-71-spectrum", "Welded detail category 71 under a three-block spectrum", Legacy("calc.fatigue-miner", 1),
            Sourced("DRG-FT-301", ["blocks"], ["S_ref", "N_ref", "m"], CalcInputValue.Quantity("S_ref", 71, "MPa"), CalcInputValue.Of("N_ref", 2e6), CalcInputValue.Of("m", 3), FatigueMinerCalculator.Blocks((100, 1e5), (60, 5e5), (40, 2e6))),
            [new CalcExpectedValue("D_tot", 0.46939, "", 5e-5), new CalcExpectedValue("R_f", 2.1304, "", 5e-5)],
            "Three stress-range blocks on a category-71 detail. Each block's share of the damage is close to the others; the total 0.47 means the spectrum can be repeated about twice before failure."),
        new(FatigueMinerCalculator.Id, "slope-5-endurance-limit", "Slope-5 curve with an endurance limit — damage above one", Legacy("calc.fatigue-miner", 2),
            Sourced("DRG-FT-302", ["blocks"], ["S_ref", "N_ref", "m", "S_e"], CalcInputValue.Quantity("S_ref", 150, "MPa"), CalcInputValue.Of("N_ref", 1e6), CalcInputValue.Of("m", 5), CalcInputValue.Quantity("S_e", 60, "MPa"), FatigueMinerCalculator.Blocks((120, 2e6), (90, 1e7), (50, 1e7))),
            [new CalcExpectedValue("D_tot", 1.4330, "", 5e-5), new CalcExpectedValue("R_f", 0.69786, "", 5e-5)],
            "The 50 MPa block lies below the 60 MPa endurance limit and does no damage, but the other two sum to 1.43: the detail fails before one spectrum is complete."),

        new(ShaftStressCalculator.Id, "shaft-40mm", "40 mm shaft in bending and torsion", Legacy("calc.shaft-combined-stress", 1),
            Sourced("DRG-SH-401", ["M", "T"], ["n_req"], CalcInputValue.Quantity("d", 40, "mm"), CalcInputValue.Quantity("M", 500, "N*m"), CalcInputValue.Quantity("T", 800, "N*m"), CalcInputValue.Quantity("S_y", 350, "MPa"), CalcInputValue.Of("K_t", 1), CalcInputValue.Of("K_ts", 1), CalcInputValue.Of("n_req", 2)),
            [new CalcExpectedValue("sigma_vm", 135.98, "MPa", 5e-5), new CalcExpectedValue("n_T", 2.3311, "", 5e-5), new CalcExpectedValue("n_VM", 2.5739, "", 5e-5)],
            "A plain 40 mm shaft. Tresca gives the lower factor, 2.33, against 2 required."),
        new(ShaftStressCalculator.Id, "shaft-25mm-shoulder", "25 mm shaft at a shoulder fillet", Legacy("calc.shaft-combined-stress", 3),
            Sourced("DRG-SH-402", ["M", "T"], ["n_req", "K_t", "K_ts"], CalcInputValue.Quantity("d", 25, "mm"), CalcInputValue.Quantity("M", 120, "N*m"), CalcInputValue.Quantity("T", 60, "N*m"), CalcInputValue.Quantity("S_y", 250, "MPa"), CalcInputValue.Of("K_t", 1.6), CalcInputValue.Of("K_ts", 1.3), CalcInputValue.Of("n_req", 1.5)),
            [new CalcExpectedValue("sigma", 125.16, "MPa", 5e-5), new CalcExpectedValue("n_T", 1.8505, "", 5e-5), new CalcExpectedValue("n_VM", 1.8842, "", 5e-5)],
            "The shoulder fillet's concentration factors raise the bending stress by 60 %. The factor of safety, 1.85, still clears the 1.5 required."),

        new(ThermalExpansionCalculator.Id, "bar-with-gap", "Steel bar heated 60 K against a 0.5 mm gap", Legacy("calc.thermal-expansion-stress", 2),
            Sourced("DRG-TX-501", ["dT"], ["sigma_allow"], Thermal(60, 0.5, null)),
            [new CalcExpectedValue("F", 47_000, "N", 5e-5), new CalcExpectedValue("sigma", -94.000, "MPa", 5e-5), new CalcExpectedValue("delta", 0.50000, "mm", 5e-5)],
            "The bar wants to grow 1.44 mm; the first 0.5 mm closes the gap freely and the rest is resisted by rigid walls, giving 94 MPa compression."),
        new(ThermalExpansionCalculator.Id, "bar-elastic-restraint", "Steel bar heated 60 K against an elastic restraint", Legacy("calc.thermal-expansion-stress", 3),
            Sourced("DRG-TX-502", ["dT"], ["sigma_allow", "k_s"], Thermal(60, 0, 100)),
            [new CalcExpectedValue("F", 48_000, "N", 5e-5), new CalcExpectedValue("sigma", -96.000, "MPa", 5e-5), new CalcExpectedValue("delta", 0.48000, "mm", 5e-5)],
            "A 100 kN/mm restraint in series with the bar's own 50 kN/mm takes a third of the movement as deflection; the force is 48 kN instead of the rigid 72 kN."),

        new(ThinVesselCalculator.Id, "receiver-2MPa", "Air receiver shell, 2 MPa", new CalcReferenceSource("Hand calculation, ASME VIII-1 UG-27(c) forms", null, "t_c = 8.6133 mm, t_l = 4.2481 mm, t_req = 11.613 mm"),
            Sourced("DRG-PV-601", ["P"], ["S", "E_j", "n", "c"], CalcInputValue.Quantity("P", 2, "MPa"), CalcInputValue.Quantity("R", 500, "mm"), CalcInputValue.Quantity("S", 138, "MPa"), CalcInputValue.Of("E_j", 0.85), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("c", 3, "mm"), CalcInputValue.Quantity("t_p", 12, "mm")),
            [new CalcExpectedValue("t_c", 8.6133, "mm", 5e-5), new CalcExpectedValue("t_req", 11.613, "mm", 5e-5), new CalcExpectedValue("u", 0.96777, "", 5e-5)],
            "A 1 m diameter receiver at 2 MPa with spot-radiographed seams (E = 0.85) and 3 mm corrosion allowance needs 11.6 mm; the 12 mm plate provided passes at 0.97."),
        new(ThinVesselCalculator.Id, "vessel-3MPa-thin", "Vessel shell at 3 MPa — plate too thin", new CalcReferenceSource("Hand calculation, ASME VIII-1 UG-27(c) forms", null, "t_c = 2250/136.2 = 16.520 mm; t_req = 18.020 mm against 15 mm"),
            Sourced("DRG-PV-602", ["P"], ["S", "E_j", "n", "c"], CalcInputValue.Quantity("P", 3, "MPa"), CalcInputValue.Quantity("R", 750, "mm"), CalcInputValue.Quantity("S", 138, "MPa"), CalcInputValue.Of("E_j", 1.0), CalcInputValue.Of("n", 1), CalcInputValue.Quantity("c", 1.5, "mm"), CalcInputValue.Quantity("t_p", 15, "mm")),
            [new CalcExpectedValue("t_c", 16.520, "mm", 5e-5), new CalcExpectedValue("t_req", 18.020, "mm", 5e-5), new CalcExpectedValue("sigma_h", 168.47, "MPa", 5e-5), new CalcExpectedValue("u", 1.2013, "", 5e-5)],
            "A 1.5 m vessel at 3 MPa needs 18 mm; the 15 mm plate fails at 1.20, and its hoop stress at the corroded thickness, 168 MPa, is above the 138 MPa allowable."),

        new(MaterialMarginCalculator.Id, "bracket-steel-margin", "Bracket steel margin on yield and ultimate", new CalcReferenceSource("Hand calculation", null, "n_y = 2.5, n_u = 3.75, governing 2.5"),
            Sourced("DRG-BR-701", ["sigma"], ["k_u", "n_req"], CalcInputValue.Quantity("sigma", 120, "MPa"), CalcInputValue.Quantity("f_y", 300, "MPa"), CalcInputValue.Quantity("f_u", 450, "MPa"), CalcInputValue.Of("k_u", 1.5), CalcInputValue.Of("n_req", 1.5)),
            [new CalcExpectedValue("n_y", 2.5, "", 1e-12), new CalcExpectedValue("n_gov", 2.5, "", 1e-12), new CalcExpectedValue("u", 0.6, "", 1e-12)],
            "A 120 MPa working stress on a 300/450 MPa steel. Yield and ultimate margins agree once the ultimate is scaled by k_u = 1.5; the factor 2.5 clears 1.5."),
        new(MaterialMarginCalculator.Id, "s355-yield-only", "S355 at 250 MPa — margin below the requirement", new CalcReferenceSource("Hand calculation", null, "n_y = 355/250 = 1.42; u = 1.5/1.42 = 1.0563"),
            Sourced("DRG-BR-702", ["sigma"], ["k_u", "n_req"], CalcInputValue.Quantity("sigma", 250, "MPa"), CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Of("k_u", 1.5), CalcInputValue.Of("n_req", 1.5)),
            [new CalcExpectedValue("n_y", 1.42, "", 1e-12), new CalcExpectedValue("u", 1.0563, "", 5e-5)],
            "Only the yield strength is known; the 1.42 factor falls short of the 1.5 required."),
    ];
}
