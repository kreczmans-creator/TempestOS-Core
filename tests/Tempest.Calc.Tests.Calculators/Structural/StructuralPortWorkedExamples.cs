using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

/// <summary>Worked examples for the structural templates ported from the legacy calculation modules (ADR-0167).</summary>
public static class StructuralPortWorkedExamples
{
    private static CalcInputSet Sourced(string drawing, string[] loads, string[] criteria, params CalcInputValue[] values) =>
        new([.. values.Select(v => v with
        {
            Provenance = loads.Contains(v.Symbol) ? CalcProvenance.Specification("LOAD-SCHEDULE-01")
                : criteria.Contains(v.Symbol) ? CalcProvenance.Specification("SPEC-STR-02 design criteria")
                : CalcProvenance.Drawing(drawing, "A"),
        })]);

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.fillet-weld-throat-stress (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.fillet-weld-throat-stress.md, Example {n}");

    private static readonly string[] Loads = ["F_par", "F_tr", "F_n"];
    private static readonly string[] Criteria = ["f_u", "beta_w", "gamma_M2"];

    private static CalcInputValue[] Weld(double fPar, double fTr, double fN, double l, double a, double fu, double beta) =>
    [
        CalcInputValue.Quantity("F_par", fPar, "kN"), CalcInputValue.Quantity("F_tr", fTr, "kN"), CalcInputValue.Quantity("F_n", fN, "kN"),
        CalcInputValue.Quantity("L", l, "mm"), CalcInputValue.Quantity("a", a, "mm"), CalcInputValue.Quantity("f_u", fu, "MPa"),
        CalcInputValue.Of("beta_w", beta), CalcInputValue.Of("gamma_M2", 1.25),
    ];

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(FilletWeldCalculator.Id, "s355-lap-shear", "S355 lap weld, 150 kN in longitudinal shear", Legacy(1),
            Sourced("DRG-WD-701", Loads, Criteria, Weld(150, 0, 0, 200, 5, 490, 0.9)).With(CalcInputValue.Choice("joint", FilletWeldCalculator.LapJoint, CalcProvenance.Drawing("DRG-WD-701", "A"))),
            [new CalcExpectedValue("f_vw_d", 251.47, "MPa", 5e-5), new CalcExpectedValue("tau_w", 150.00, "MPa", 5e-5), new CalcExpectedValue("F_w_Rd", 251.468, "kN", 5e-5), new CalcExpectedValue("u", 0.59650, "", 5e-5)],
            "A 5 mm throat, 200 mm long, on S355. The throat stress, 150 MPa, is 60 % of the 251 MPa design shear strength; a 3 mm throat would carry the load, so the minimum governs the required size."),
        new(FilletWeldCalculator.Id, "normal-load-overloaded", "4 mm weld, 120 kN normal to the parts — fails", Legacy(3),
            Sourced("DRG-WD-702", Loads, Criteria, Weld(0, 0, 120, 100, 4, 490, 0.9)).With(CalcInputValue.Choice("joint", FilletWeldCalculator.LapJoint, CalcProvenance.Assumed("Lap joint (template default); the weld is shorter than 150 throats, so the long-joint reduction does not apply."))),
            [new CalcExpectedValue("tau_w", 300.00, "MPa", 5e-5), new CalcExpectedValue("u", 1.1930, "", 5e-5), new CalcExpectedValue("a_calc", 4.7719, "mm", 5e-5)],
            "A 100 mm weld with a 4 mm throat under 120 kN. The throat stress, 300 MPa, exceeds the design strength by 19 %; a 5 mm throat (4.77 mm needed) would pass."),
    ];
}
