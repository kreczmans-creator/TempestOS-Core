using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// Fillet weld by the EN 1993-1-8 simplified method: resultant force per
/// unit length over the throat against the design shear strength. Ported
/// from the legacy <c>calc.fillet-weld-throat-stress</c> module (ADR-0167);
/// its three hand-worked examples run as known-answer cases.
/// </summary>
public sealed class FilletWeldCalculator : ICalcCalculator
{
    public const string Id = "struct.fillet-weld";

    /// <summary>A lap joint: its weld is reduced by the long-joint factor β_Lw beyond 150 throats (BS EN 1993-1-8 4.11(3)).</summary>
    public const string LapJoint = "Lap joint";

    /// <summary>A weld whose stress follows the stress in the base metal (a flange-to-web weld, say): 4.11(2), the long-joint reduction does not apply.</summary>
    public const string OtherJoint = "Other (stress follows the base metal)";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.fillet-weld-throat-stress.md, Example {n}");

    private static CalcInputValue[] Example(double fPar, double fTr, double fN, double l, double a, double fu, double beta) =>
    [
        CalcInputValue.Quantity("F_par", fPar, "kN"), CalcInputValue.Quantity("F_tr", fTr, "kN"), CalcInputValue.Quantity("F_n", fN, "kN"),
        CalcInputValue.Quantity("L", l, "mm"), CalcInputValue.Quantity("a", a, "mm"), CalcInputValue.Quantity("f_u", fu, "MPa"),
        CalcInputValue.Of("beta_w", beta), CalcInputValue.Of("gamma_M2", 1.25),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Structural, "Fillet weld (EN 1993-1-8)",
        "Calculates the utilisation of a fillet weld loaded parallel, transverse and normal to its axis, with the required throat and leg. The simplified method compares the resultant over the throat with the design shear strength.",
        "Resultant force on the weld: F = √(F_∥² + F_⊥² + F_n²).\nDesign shear strength of the weld, simplified method: f_vw,d = f_u/(√3·β_w·γ_M2).\nLong-joint reduction of a lap joint longer than 150·a (4.11(3)): β_Lw = min(1, 1.2 − 0.2·L/(150·a)); 1 for a shorter lap or another joint type.\nShear stress on the throat: τ_w = F/(a·L).\nWeld resistance and utilisation: F_w,Rd = β_Lw·f_vw,d·a·L; u = F/F_w,Rd.\nThroat needed (not less than 3 mm, with the β_Lw of the throat provided) and the matching leg: a_req = max(F/(β_Lw·f_vw,d·L), 3 mm); leg = √2·a.",
        [
            new CalcInputSpec("joint", "Joint type: a lap joint is reduced beyond 150 throats, and Other is for a weld whose stress follows the base metal", CalcInputKind.Choice, Choices: [LapJoint, OtherJoint], DefaultChoice: LapJoint, Group: "Setup"),
            new CalcInputSpec("L", "Effective weld length", CalcInputKind.Quantity, "mm", Default: 200, Min: 30, Max: 1e4, HardMin: 1e-3, HardMax: 1e6, Group: "Geometry"),
            new CalcInputSpec("a", "Throat thickness", CalcInputKind.Quantity, "mm", Default: 5, Min: 3, Max: 50, HardMin: 1e-3, HardMax: 1e3, Group: "Geometry"),
            new CalcInputSpec("f_u", "Ultimate tensile strength of the weaker part joined", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "UltimateTensileStrength", ReferenceDimension: "Pressure", Default: 490, Min: 300, Max: 1000, HardMin: 1e-3, HardMax: 1e5, Group: "Material"),
            new CalcInputSpec("F_par", "Force parallel to the weld axis", CalcInputKind.Quantity, "kN", Default: 150, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("F_tr", "Force transverse to the weld axis, in the plane of the parts", CalcInputKind.Quantity, "kN", Default: 0, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("F_n", "Force normal to the plane of the parts", CalcInputKind.Quantity, "kN", Default: 0, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("beta_w", "Correlation factor (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420 and S460)", CalcInputKind.Number, Default: 0.9, Min: 0.8, Max: 1.0, HardMin: 0.01, HardMax: 5, Group: "Factors"),
            new CalcInputSpec("gamma_M2", "Partial factor for the weld resistance", CalcInputKind.Number, Default: 1.25, Min: 1, Max: 2, HardMin: 1, HardMax: 10, Group: "Factors"),
        ],
        [
            new CalcAssumption("The resultant force is shared uniformly along the effective length.", "Simplified method."),
            new CalcAssumption("Equal-leg fillet: leg = √2 × throat.", "Geometry."),
            new CalcAssumption("A lap joint longer than 150·a is reduced by β_Lw = 1.2 − 0.2·L/(150·a), not above 1.", "BS EN 1993-1-8 4.11(3)."),
            new CalcAssumption("The weld length L is the length of the lap in the direction of the force.", "BS EN 1993-1-8 4.11(3)."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 4.5.3.3", "Simplified method for design resistance of fillet weld"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 4.5.2(2)", "Effective throat thickness: minimum 3 mm"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 4.5.1(2)", "Effective length: welds shorter than 30 mm or 6 throats not to carry load"),
            new CalcClauseRef("BS EN 1993-1-8:2005", "cl. 4.11", "Long joints"),
        ],
        [
            new CalcApplicabilityRule("throat", "Throats below 3 mm are outside the method.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("length", "Effective lengths below max(30 mm, 6·a) should not be designed to carry load.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("no-load", "At least one force must be non-zero.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("long-joint", "A lap-joint weld longer than 150·a is reduced by the long-joint factor β_Lw = 1.2 − 0.2·L/(150·a) (BS EN 1993-1-8 4.11(3)); the reduced resistance is the one checked.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("long-joint-range", "The long-joint factor 1.2 − 0.2·L/(150·a) is not positive for a lap longer than 900·a: the weld is outside the formula.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-s355-shear", "Legacy example 1: S355 lap weld in longitudinal shear", Legacy(1), Example(150, 0, 0, 200, 5, 490, 0.9),
                [new CalcExpectedValue("f_vw_d", 251.47, "MPa", 5e-5), new CalcExpectedValue("tau_w", 150.00, "MPa", 5e-5), new CalcExpectedValue("F_w_Rd", 251.468, "kN", 5e-5), new CalcExpectedValue("u", 0.59650, "", 5e-5), new CalcExpectedValue("a_calc", 2.9825, "mm", 5e-5), new CalcExpectedValue("a_req", 3.0, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-s235-combined", "Legacy example 2: S235, combined parallel and transverse load", Legacy(2), Example(40, 30, 0, 120, 4, 360, 0.8),
                [new CalcExpectedValue("F", 50, "kN", 5e-5), new CalcExpectedValue("f_vw_d", 207.85, "MPa", 5e-5), new CalcExpectedValue("tau_w", 104.17, "MPa", 5e-5), new CalcExpectedValue("u", 0.50117, "", 5e-5), new CalcExpectedValue("a_calc", 2.0047, "mm", 5e-5), new CalcExpectedValue("F_w_Rd", 99.766, "kN", 5e-5), new CalcExpectedValue("a_req", 3.0, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-overloaded", "Legacy example 3: overloaded normal weld", Legacy(3), Example(0, 0, 120, 100, 4, 490, 0.9),
                [new CalcExpectedValue("tau_w", 300.00, "MPa", 5e-5), new CalcExpectedValue("u", 1.1930, "", 5e-5), new CalcExpectedValue("a_calc", 4.7719, "mm", 5e-5), new CalcExpectedValue("F_w_Rd", 100.587, "kN", 5e-5), new CalcExpectedValue("a_req", 4.7719, "mm", 5e-5)]),
        ],
        Category: "Connections",
        OutputPorts:
        [
            new CalcOutputPort("F_w_Rd", "kN", "Design resistance of the weld", "weld.resistance_design"),
            new CalcOutputPort("u", "", "Utilisation of the weld", "weld.utilisation"),
            new CalcOutputPort("f_vw_d", "MPa", "Design shear strength of the weld", "weld.strength_shear_design"),
            new CalcOutputPort("tau_w", "MPa", "Stress on the throat", "weld.stress_throat"),
            new CalcOutputPort("a_req", "mm", "Required throat", "weld.throat_required"),
            new CalcOutputPort("s_leg", "mm", "Leg length of the throat provided", "weld.leg_provided"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u");
        foreach (var s in new[] { "F_par", "F_tr", "F_n", "L", "a", "f_u", "beta_w", "gamma_M2" })
            c.Input(s);

        if (c.Si("a") < 3e-3 - 1e-12)
        {
            c.Block("throat", "a: a throat below 3 mm is outside the method (BS EN 1993-1-8 4.5.2(2)).");
            return;
        }

        if (c.Si("L") < Math.Max(30e-3, 6 * c.Si("a")) - 1e-12)
        {
            c.Block("length", "L: an effective length below max(30 mm, 6·a) should not be designed to carry load (BS EN 1993-1-8 4.5.1(2)).");
            return;
        }

        c.Input("joint");
        var lap = c.InputText("joint") == LapJoint;
        var ratio = c.Si("L") / (150 * c.Si("a"));

        // BS EN 1993-1-8 4.11(3): a lap joint longer than 150·a is reduced by β_Lw = 1.2 − 0.2·L/(150·a), not above 1. It was a warning that said the reduction was not applied (1.0.2 and before).
        if (lap && ratio > 1 && 1.2 - 0.2 * ratio <= 0)
        {
            c.Block("long-joint-range", $"L: the lap length of {CalcValue.FormatNumber(c.Si("L") * 1e3, 5)} mm is {CalcValue.FormatNumber(ratio * 150, 5)} throats, beyond the 900 where the long-joint factor reaches zero. Shorten the lap, or split it into shorter welds.");
            return;
        }

        if (lap && ratio > 1)
        {
            c.Assign("beta_Lw", "1.2 - 0.2 * L / (150 * a)", null, "Long-joint reduction factor of the lap joint", "BS EN 1993-1-8 4.11(3)");
            c.Warn("long-joint", $"L: the lap length exceeds 150·a, so the resistance is reduced by β_Lw = {CalcValue.FormatNumber(c.Si("beta_Lw"), 5)} (BS EN 1993-1-8 4.11(3)).");
        }
        else
        {
            c.Record("beta_Lw", "no long-joint reduction", lap ? "lap not longer than 150·a" : "joint type: other", new ScalarValue(1), "Long-joint reduction factor (1: not reduced)", "BS EN 1993-1-8 4.11");
        }

        c.Assign("F", "sqrt(F_par^2 + F_tr^2 + F_n^2)", "kN", "Resultant force on the weld");

        if (!(c.Si("F") > 0))
        {
            c.Block("no-load", "F: every force is zero. There is nothing to check.");
            return;
        }

        c.Assign("f_vw_d", "f_u / (sqrt(3) * beta_w * gamma_M2)", "MPa", "Design shear strength of the weld", "BS EN 1993-1-8 4.5.3.3");
        c.Assign("tau_w", "F / (a * L)", "MPa", "Stress on the throat");
        c.Assign("F_w_Rd", "beta_Lw * f_vw_d * a * L", "kN", "Design resistance of the weld, reduced by the long-joint factor when it applies", "BS EN 1993-1-8 4.5.3.3");
        c.Assign("u", "F / F_w_Rd", null, "Utilisation");
        c.Assign("a_calc", "F / (beta_Lw * f_vw_d * L)", "mm", "Throat needed for the load (with the long-joint factor of the throat provided)");
        c.Assign("a_req", "max(a_calc, 3 mm)", "mm", "Required throat (not less than 3 mm)", "BS EN 1993-1-8 4.5.2(2)");
        c.Assign("s_leg", "sqrt(2) * a", "mm", "Leg length of the throat provided (equal legs)");

        foreach (var (symbol, text) in new[] { ("F", "Resultant force"), ("f_vw_d", "Design shear strength"), ("tau_w", "Throat stress"), ("beta_Lw", "Long-joint factor"), ("F_w_Rd", "Weld resistance"), ("u", "Utilisation"), ("a_calc", "Throat needed"), ("a_req", "Required throat"), ("s_leg", "Leg length provided") })
            c.Output(symbol, text);

        c.Check("Weld force against weld resistance", "F", "F_w_Rd", "BS EN 1993-1-8 4.5.3.3");
    }
}
