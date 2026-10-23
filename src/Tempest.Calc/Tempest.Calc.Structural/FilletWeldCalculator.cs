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

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.fillet-weld-throat-stress.md, Example {n}");

    private static CalcInputValue[] Example(double fPar, double fTr, double fN, double l, double a, double fu, double beta) =>
    [
        CalcInputValue.Quantity("F_par", fPar, "kN"), CalcInputValue.Quantity("F_tr", fTr, "kN"), CalcInputValue.Quantity("F_n", fN, "kN"),
        CalcInputValue.Quantity("L", l, "mm"), CalcInputValue.Quantity("a", a, "mm"), CalcInputValue.Quantity("f_u", fu, "MPa"),
        CalcInputValue.Of("beta_w", beta), CalcInputValue.Of("gamma_M2", 1.25),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Structural, "Fillet weld (EN 1993-1-8 simplified method)",
        "A fillet weld carrying forces parallel to, transverse to and normal to its axis, by the simplified method: the resultant over the throat area against the design shear strength of the weld, with the required throat and leg.",
        "F = √(F_∥² + F_⊥² + F_n²); f_vw,d = f_u/(√3·β_w·γ_M2); τ_w = F/(a·L); F_w,Rd = f_vw,d·a·L; u = F/F_w,Rd; a_req = max(F/(f_vw,d·L), 3 mm); leg = √2·a.",
        [
            new CalcInputSpec("F_par", "Force parallel to the weld axis", CalcInputKind.Quantity, "kN", Default: 150, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("F_tr", "Force transverse to the weld axis, in the plane of the parts", CalcInputKind.Quantity, "kN", Default: 0, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("F_n", "Force normal to the plane of the parts", CalcInputKind.Quantity, "kN", Default: 0, Min: -1e5, Max: 1e5, HardMin: -1e8, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("L", "Effective weld length", CalcInputKind.Quantity, "mm", Default: 200, Min: 30, Max: 1e4, HardMin: 1e-3, HardMax: 1e6, Group: "Weld"),
            new CalcInputSpec("a", "Throat thickness", CalcInputKind.Quantity, "mm", Default: 5, Min: 3, Max: 50, HardMin: 1e-3, HardMax: 1e3, Group: "Weld"),
            new CalcInputSpec("f_u", "Ultimate strength of the weaker part joined", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "UltimateTensileStrength", ReferenceDimension: "Pressure", Default: 490, Min: 300, Max: 1000, HardMin: 1e-3, HardMax: 1e5, Group: "Material"),
            new CalcInputSpec("beta_w", "Correlation factor β_w (0.8 S235, 0.85 S275, 0.9 S355, 1.0 S420/S460)", CalcInputKind.Number, Default: 0.9, Min: 0.8, Max: 1.0, HardMin: 0.01, HardMax: 5, Group: "Material"),
            new CalcInputSpec("gamma_M2", "Partial factor γ_M2", CalcInputKind.Number, Default: 1.25, Min: 1, Max: 2, HardMin: 1, HardMax: 10, Group: "Material"),
        ],
        [
            new CalcAssumption("The resultant force is shared uniformly along the effective length.", "Simplified method."),
            new CalcAssumption("Equal-leg fillet: leg = √2 × throat.", "Geometry."),
        ],
        [
            new CalcClauseRef("BS EN 1993-1-8", "4.5.3.3", "Simplified method for design resistance of fillet weld"),
            new CalcClauseRef("BS EN 1993-1-8", "4.5.2(2)", "Effective throat thickness: minimum 3 mm"),
            new CalcClauseRef("BS EN 1993-1-8", "4.5.1(2)", "Effective length: welds shorter than 30 mm or 6 throats not to carry load"),
            new CalcClauseRef("BS EN 1993-1-8", "4.11", "Long joints"),
        ],
        [
            new CalcApplicabilityRule("throat", "Throats below 3 mm are outside the method.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("length", "Effective lengths below max(30 mm, 6·a) should not be designed to carry load.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("no-load", "At least one force must be non-zero.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("long-joint", "Lap-joint welds longer than 150·a need the long-joint reduction β_Lw, which this sheet does not apply.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-legacy-s355-shear", "Legacy example 1: S355 lap weld in longitudinal shear", Legacy(1), Example(150, 0, 0, 200, 5, 490, 0.9),
                [new CalcExpectedValue("f_vw_d", 251.47, "MPa", 5e-5), new CalcExpectedValue("tau_w", 150.00, "MPa", 5e-5), new CalcExpectedValue("F_w_Rd", 251.468, "kN", 5e-5), new CalcExpectedValue("u", 0.59650, "", 5e-5), new CalcExpectedValue("a_calc", 2.9825, "mm", 5e-5), new CalcExpectedValue("a_req", 3.0, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-s235-combined", "Legacy example 2: S235, combined parallel and transverse load", Legacy(2), Example(40, 30, 0, 120, 4, 360, 0.8),
                [new CalcExpectedValue("F", 50, "kN", 5e-5), new CalcExpectedValue("f_vw_d", 207.85, "MPa", 5e-5), new CalcExpectedValue("tau_w", 104.17, "MPa", 5e-5), new CalcExpectedValue("u", 0.50117, "", 5e-5), new CalcExpectedValue("a_calc", 2.0047, "mm", 5e-5)]),
            new CalcTestCase("ka-legacy-overloaded", "Legacy example 3: overloaded normal weld", Legacy(3), Example(0, 0, 120, 100, 4, 490, 0.9),
                [new CalcExpectedValue("tau_w", 300.00, "MPa", 5e-5), new CalcExpectedValue("u", 1.1930, "", 5e-5), new CalcExpectedValue("a_calc", 4.7719, "mm", 5e-5)]),
        ],
        Category: "Connections");

    public void Run(CalcRunContext c)
    {
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

        if (c.Si("L") > 150 * c.Si("a"))
            c.Warn("long-joint", "L: the effective length exceeds 150·a; in a lap joint the long-joint reduction β_Lw (BS EN 1993-1-8 4.11) applies and is not included here.");

        c.Assign("F", "sqrt(F_par^2 + F_tr^2 + F_n^2)", "kN", "Resultant force on the weld");

        if (!(c.Si("F") > 0))
        {
            c.Block("no-load", "F: every force is zero; there is nothing to check.");
            return;
        }

        c.Assign("f_vw_d", "f_u / (sqrt(3) * beta_w * gamma_M2)", "MPa", "Design shear strength of the weld", "BS EN 1993-1-8 4.5.3.3");
        c.Assign("tau_w", "F / (a * L)", "MPa", "Stress on the throat");
        c.Assign("F_w_Rd", "f_vw_d * a * L", "kN", "Design resistance of the weld", "BS EN 1993-1-8 4.5.3.3");
        c.Assign("u", "F / F_w_Rd", null, "Utilisation");
        c.Assign("a_calc", "F / (f_vw_d * L)", "mm", "Throat needed for the load");
        c.Assign("a_req", "max(a_calc, 3 mm)", "mm", "Required throat (not less than 3 mm)", "BS EN 1993-1-8 4.5.2(2)");
        c.Assign("s_leg", "sqrt(2) * a", "mm", "Leg length of the throat provided (equal legs)");

        foreach (var (symbol, text) in new[] { ("F", "Resultant force"), ("f_vw_d", "Design shear strength"), ("tau_w", "Throat stress"), ("F_w_Rd", "Weld resistance"), ("u", "Utilisation"), ("a_calc", "Throat needed"), ("a_req", "Required throat"), ("s_leg", "Leg length provided") })
            c.Output(symbol, text);

        c.CheckUtilisation("Weld resistance", "u", "BS EN 1993-1-8 4.5.3.3");
    }
}
