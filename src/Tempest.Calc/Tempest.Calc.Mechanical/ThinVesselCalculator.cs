using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Thin cylindrical shell under internal pressure in the ASME VIII-1 UG-27
/// form: the thickness for circumferential and for longitudinal stress, the
/// governing required thickness with corrosion allowance and an optional
/// design factor, against the thickness provided. Ported from the legacy
/// <c>calc.pressure-vessel-wall-thickness</c> (ADR-0167); thick walls are
/// <c>mech.thick-cylinder</c>.
/// </summary>
public sealed class ThinVesselCalculator : ICalcCalculator
{
    public const string Id = "mech.thin-vessel";

    public CalcTemplate Template { get; } = new(
        Id, "1.1.0", CalcDiscipline.Mechanical, "Thin cylindrical shell under internal pressure",
        "Calculates the minimum thickness of a thin cylindrical shell under internal pressure, for circumferential and longitudinal stress. It adds corrosion allowance and a design factor, and compares the thickness provided with the requirement.",
        "Thickness needed for the circumferential stress: t_c = p_design·R_in/(S·E_j − 0.6·p_design).\nThickness needed for the longitudinal stress: t_l = p_design·R_in/(2·S·E_j + 0.4·p_design).\nRequired thickness, with the design factor and corrosion allowance: t_req = n_design·max(t_c, t_l) + c.\nHoop stress in the plate provided, after corrosion: σ_θ = p_design·(R_in + 0.6·t)/(E_j·t), at t = t_p − c.\nRange of validity of the thin-wall formulae: t_c ≤ R_in/2 and p_design ≤ 0.385·S·E_j.",
        [
            new CalcInputSpec("R_in", "Inside radius (corroded)", CalcInputKind.Quantity, "mm", Default: 500, Min: 5, Max: 10_000, HardMin: 1e-3, HardMax: 1e6, Group: "Geometry", Aliases: ["R"]),
            new CalcInputSpec("t_p", "Shell thickness provided (nominal, before corrosion)", CalcInputKind.Quantity, "mm", Default: 12, Min: 0.5, Max: 500, HardMin: 1e-3, HardMax: 1e5, Group: "Geometry"),
            new CalcInputSpec("S", "Allowable stress at design temperature", CalcInputKind.Quantity, "MPa", Default: 138, Min: 10, Max: 1000, HardMin: 1e-3, HardMax: 1e5, Group: "Material"),
            new CalcInputSpec("p_design", "Internal design pressure", CalcInputKind.Quantity, "MPa", Default: 2, Min: 0.01, Max: 100, HardMin: 1e-6, HardMax: 1e4, Group: "Loads", Aliases: ["P"]),
            new CalcInputSpec("c", "Corrosion allowance", CalcInputKind.Quantity, "mm", Default: 3, Min: 0, Max: 25, HardMin: 0, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("E_j", "Joint efficiency", CalcInputKind.Number, Default: 0.85, Min: 0.45, Max: 1, HardMin: 0.01, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("n_design", "Design factor on the computed thickness (legacy; 1 for none)", CalcInputKind.Number, Default: 1, Min: 1, Max: 5, HardMin: 1, HardMax: 20, Group: "Factors", Aliases: ["n"]),
        ],
        [
            new CalcAssumption("The shell is cylindrical, away from heads, nozzles and supports, and carries membrane stresses only.", "The scope of the UG-27 formulae."),
            new CalcAssumption("The joint efficiency reduces the allowable stress uniformly.", "Code-form simplification carried from the legacy module."),
        ],
        [
            new CalcClauseRef("ASME BPVC Section VIII Division 1", "cl. UG-27(c)(1)", "Thickness of shells under internal pressure, circumferential stress"),
            new CalcClauseRef("ASME BPVC Section VIII Division 1", "cl. UG-27(c)(2)", "Thickness of shells under internal pressure, longitudinal stress"),
        ],
        [
            new CalcApplicabilityRule("thin-wall", "The thin-shell formulae apply while t ≤ R_in/2 and p_design ≤ 0.385·S·E_j; beyond that use mech.thick-cylinder.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("corroded-through", "The provided thickness must exceed the corrosion allowance.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-ug27-hand", "UG-27 hand calculation: 2 MPa, R 500 mm, S 138 MPa, E 0.85, c 3 mm, t 12 mm",
                new CalcReferenceSource("Hand calculation, UG-27(c) forms", null, "t_c = 1000/(117.3 − 1.2) = 8.6133 mm; t_l = 1000/(234.6 + 0.8) = 4.2481 mm; t_req = 11.613 mm; σ_θ at 9 mm = 2·505.4/(0.85·9) = 132.13 MPa"),
                [CalcInputValue.Quantity("p_design", 2, "MPa"), CalcInputValue.Quantity("R_in", 500, "mm"), CalcInputValue.Quantity("S", 138, "MPa"), CalcInputValue.Of("E_j", 0.85), CalcInputValue.Of("n_design", 1), CalcInputValue.Quantity("c", 3, "mm"), CalcInputValue.Quantity("t_p", 12, "mm")],
                [new CalcExpectedValue("t_c", 8.6133, "mm", 5e-5), new CalcExpectedValue("t_l", 4.2481, "mm", 5e-5), new CalcExpectedValue("t_req", 11.613, "mm", 5e-5), new CalcExpectedValue("sigma_h", 132.13, "MPa", 5e-5), new CalcExpectedValue("u", 0.96777, "", 5e-5)]),
            new CalcTestCase("ka-ug27-thin-plate", "UG-27 hand calculation: 3 MPa, R 750 mm, S 138 MPa, E 1.0, c 1.5 mm, t 15 mm (fails)",
                new CalcReferenceSource("Hand calculation, UG-27(c) forms", null, "t_c = 2250/136.2 = 16.520 mm; t_l = 2250/277.2 = 8.1169 mm; t_req = 18.020 mm; σ_θ at 13.5 mm = 3·758.1/13.5 = 168.47 MPa"),
                [CalcInputValue.Quantity("p_design", 3, "MPa"), CalcInputValue.Quantity("R_in", 750, "mm"), CalcInputValue.Quantity("S", 138, "MPa"), CalcInputValue.Of("E_j", 1.0), CalcInputValue.Of("n_design", 1), CalcInputValue.Quantity("c", 1.5, "mm"), CalcInputValue.Quantity("t_p", 15, "mm")],
                [new CalcExpectedValue("t_c", 16.520, "mm", 5e-5), new CalcExpectedValue("t_l", 8.1169, "mm", 5e-5), new CalcExpectedValue("t_req", 18.020, "mm", 5e-5), new CalcExpectedValue("sigma_h", 168.47, "MPa", 5e-5), new CalcExpectedValue("u", 1.2013, "", 5e-5)]),
        ],
        Category: "Vessels",
        OutputPorts:
        [
            new CalcOutputPort("t_req", "mm", "Required nominal thickness, with design factor and corrosion allowance", "vessel.thickness_required"),
            new CalcOutputPort("sigma_h", "MPa", "Hoop stress at the corroded thickness provided", "vessel.stress_hoop"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u");
        foreach (var s in new[] { "p_design", "R_in", "S", "E_j", "n_design", "c", "t_p" })
            c.Input(s);

        if (c.Si("p_design") > 0.385 * c.Si("S") * c.Si("E_j"))
        {
            c.Block("thin-wall", "p_design: the pressure exceeds 0.385·S·E_j. The thin-shell formula does not apply, so use mech.thick-cylinder.");
            return;
        }

        if (!(c.Si("t_p") > c.Si("c")))
        {
            c.Block("corroded-through", "t_p: the provided thickness does not exceed the corrosion allowance.");
            return;
        }

        c.Assign("t_c", "p_design * R_in / (S * E_j - 0.6 * p_design)", "mm", "Thickness for circumferential stress", "ASME VIII-1 UG-27(c)(1)");
        c.Assign("t_l", "p_design * R_in / (2 * S * E_j + 0.4 * p_design)", "mm", "Thickness for longitudinal stress", "ASME VIII-1 UG-27(c)(2)");

        if (c.Si("t_c") > 0.5 * c.Si("R_in"))
        {
            c.Block("thin-wall", "p_design: the required thickness exceeds half the radius. The thin-shell formula does not apply, so use mech.thick-cylinder.");
            return;
        }

        c.Assign("t_req", "n_design * max(t_c, t_l) + c", "mm", "Required nominal thickness (design factor and corrosion allowance included)");
        c.Assign("t_corr", "t_p - c", "mm", "Corroded thickness provided");
        c.Assign("sigma_h", "p_design * (R_in + 0.6 * t_corr) / (E_j * t_corr)", "MPa", "Hoop stress at the corroded thickness provided", "ASME VIII-1 UG-27(c)(1)");
        c.Assign("u", "t_req / t_p", null, "Required over provided thickness");

        foreach (var (symbol, text) in new[] { ("t_c", "Thickness, circumferential stress"), ("t_l", "Thickness, longitudinal stress"), ("t_req", "Required nominal thickness"), ("sigma_h", "Hoop stress at the corroded thickness"), ("u", "Thickness utilisation") })
            c.Output(symbol, text);

        c.Check("Provided thickness against required", "t_req", "t_p", "ASME VIII-1 UG-27");
    }
}
