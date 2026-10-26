using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Structural;

/// <summary>
/// Pin-ended strut by the Perry–Robertson formula: Euler's elastic critical
/// stress reduced for initial imperfection, with the Robertson constant
/// selecting the strut curve. Ported from the legacy
/// <c>calc.column-buckling</c> module (ADR-0167); its three hand-worked
/// examples run as known-answer cases.
/// </summary>
public sealed class ColumnBucklingCalculator : ICalcCalculator
{
    public const string Id = "struct.column-buckling";

    public const string CurveA = "a";
    public const string CurveB = "b";
    public const string CurveC = "c";
    public const string CurveD = "d";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.column-buckling (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.column-buckling.md, Example {n}");

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Structural, "Column buckling (Perry–Robertson)",
        "Compressive strength and resistance of a pin-ended strut of a given effective length, from Euler's critical stress reduced for initial imperfection by the Perry–Robertson formula, with the Robertson constant chosen by strut curve; utilisation against an applied axial load.",
        "Radius of gyration and slenderness: r = √(I/A); λ = L_E/r.\nEuler buckling stress and load: p_E = π²E/λ²; P_E = p_E·A.\nLimiting slenderness, below which the strut is not reduced for buckling: λ_0 = 0.2·√(π²E/p_y).\nPerry factor (Robertson constant a = 2.0, 3.5, 5.5, 8.0 for curves a–d): η = max(0.001·a·(λ − λ_0), 0).\nCompressive strength by the Perry–Robertson formula: φ = (p_y + (η + 1)·p_E)/2; p_c = p_E·p_y/(φ + √(φ² − p_E·p_y)).\nCompression resistance and utilisation: P_c = p_c·A; u = P/P_c.",
        [
            new CalcInputSpec("L_E", "Effective length", CalcInputKind.Quantity, "m", Default: 3, Min: 0.1, Max: 50, HardMin: 1e-4, HardMax: 1e3),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "mm^2", Default: 2000, Min: 10, Max: 1e6, HardMin: 1e-6, HardMax: 1e9),
            new CalcInputSpec("I", "Second moment of area about the weaker axis", CalcInputKind.Quantity, "cm^4", Default: 200, Min: 0.01, Max: 1e7, HardMin: 1e-9, HardMax: 1e12),
            new CalcInputSpec("E", "Modulus of elasticity", CalcInputKind.Quantity, "GPa", ReferenceLibrary: "Materials", ReferenceProperty: "YoungsModulus", ReferenceDimension: "Pressure", Default: 205, Min: 50, Max: 450, HardMin: 0.1, HardMax: 1e4),
            new CalcInputSpec("p_y", "Design strength (yield)", CalcInputKind.Quantity, "MPa", ReferenceLibrary: "Materials", ReferenceProperty: "YieldStrength", ReferenceDimension: "Pressure", Default: 275, Min: 100, Max: 1000, HardMin: 1e-3, HardMax: 1e5),
            new CalcInputSpec("curve", "Strut curve (selects the Robertson constant)", CalcInputKind.Choice, Choices: [CurveA, CurveB, CurveC, CurveD], DefaultChoice: CurveC),
            new CalcInputSpec("P", "Applied axial load", CalcInputKind.Quantity, "kN", Default: 150, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8),
        ],
        [
            new CalcAssumption("Pin-ended, prismatic strut under axial load only; buckling about the axis of the given second moment.", "Effective length carries the end restraint."),
            new CalcAssumption("Initial imperfection represented by the Perry factor η = 0.001·a·(λ − λ_0), with a from the strut curve.", "Robertson's fit to strut tests."),
        ],
        [
            new CalcClauseRef("BS 5950-1:2000", "Annex C", "Compressive strength (Perry strut formula)"),
            new CalcClauseRef("BS 5950-1:2000", "4.7.3.2", "Maximum slenderness"),
            new CalcClauseRef("BS 5950-1:2000", "Table 23", "Allocation of strut curve"),
        ],
        [
            new CalcApplicabilityRule("slenderness", "Slenderness above 180 is outside the method (members resisting loads other than wind).", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-curve-c", "Legacy example 1: curve c, intermediate slenderness", Legacy(1),
                [CalcInputValue.Quantity("L_E", 3, "m"), CalcInputValue.Quantity("A", 2000, "mm^2"), CalcInputValue.Quantity("I", 200, "cm^4"), CalcInputValue.Quantity("E", 205, "GPa"), CalcInputValue.Quantity("p_y", 275, "MPa"), CalcInputValue.Choice("curve", CurveC), CalcInputValue.Quantity("P", 150, "kN")],
                [new CalcExpectedValue("r", 31.623, "mm", 5e-5), new CalcExpectedValue("lambda", 94.868, "", 5e-5), new CalcExpectedValue("p_E", 224.81, "MPa", 5e-5), new CalcExpectedValue("lambda_0", 17.155, "", 5e-5), new CalcExpectedValue("eta", 0.42742, "", 5e-5), new CalcExpectedValue("p_c", 133.78, "MPa", 5e-5), new CalcExpectedValue("P_c", 267.56, "kN", 5e-5), new CalcExpectedValue("u", 0.56062, "", 5e-5)]),
            new CalcTestCase("ka-legacy-slender-b", "Legacy example 2: slender, curve b, S355 (fails)", Legacy(2),
                [CalcInputValue.Quantity("L_E", 1.5, "m"), CalcInputValue.Quantity("A", 1200, "mm^2"), CalcInputValue.Quantity("I", 12, "cm^4"), CalcInputValue.Quantity("E", 205, "GPa"), CalcInputValue.Quantity("p_y", 355, "MPa"), CalcInputValue.Choice("curve", CurveB), CalcInputValue.Quantity("P", 100, "kN")],
                [new CalcExpectedValue("r", 10.000, "mm", 5e-5), new CalcExpectedValue("lambda", 150.00, "", 5e-5), new CalcExpectedValue("p_E", 89.923, "MPa", 5e-5), new CalcExpectedValue("lambda_0", 15.099, "", 5e-5), new CalcExpectedValue("eta", 0.47215, "", 5e-5), new CalcExpectedValue("p_c", 77.973, "MPa", 5e-5), new CalcExpectedValue("P_c", 93.567, "kN", 5e-5), new CalcExpectedValue("u", 1.0688, "", 5e-5)]),
            new CalcTestCase("ka-legacy-stocky-c", "Legacy example 3: stocky, curve c", Legacy(3),
                [CalcInputValue.Quantity("L_E", 1, "m"), CalcInputValue.Quantity("A", 2000, "mm^2"), CalcInputValue.Quantity("I", 200, "cm^4"), CalcInputValue.Quantity("E", 205, "GPa"), CalcInputValue.Quantity("p_y", 275, "MPa"), CalcInputValue.Choice("curve", CurveC), CalcInputValue.Quantity("P", 300, "kN")],
                [new CalcExpectedValue("lambda", 31.623, "", 5e-5), new CalcExpectedValue("p_E", 2023.3, "MPa", 5e-5), new CalcExpectedValue("eta", 0.079573, "", 5e-5), new CalcExpectedValue("p_c", 252.09, "MPa", 5e-5), new CalcExpectedValue("P_c", 504.17, "kN", 5e-5), new CalcExpectedValue("u", 0.59504, "", 5e-5)]),
        ],
        Category: "Members");

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u");
        c.Input("L_E");
        c.Input("A");
        c.Input("I");
        c.Input("E");
        c.Input("p_y");
        var curve = c.InputText("curve");
        c.Input("P");

        var robertson = curve switch
        {
            CurveA => "2.0",
            CurveB => "3.5",
            CurveC => "5.5",
            CurveD => "8.0",
            _ => null,
        };

        if (robertson is null)
        {
            c.Block("curve", $"curve: '{curve}' is not a strut curve (a, b, c or d).");
            return;
        }

        c.Assign("a_R", robertson, null, $"Robertson constant for strut curve {curve}", "BS 5950-1 Annex C");
        c.Assign("r", "sqrt(I / A)", "mm", "Radius of gyration");
        c.Assign("lambda", "L_E / r", null, "Slenderness");

        if (c.Si("lambda") > 180)
        {
            c.Block("slenderness", $"lambda: the slenderness {CalcValue.FormatNumber(c.Si("lambda"), 4)} exceeds 180, outside the method (BS 5950-1 4.7.3.2).");
            return;
        }

        c.Assign("p_E", "pi^2 * E / lambda^2", "MPa", "Euler critical stress", "BS 5950-1 Annex C");
        c.Assign("P_E", "p_E * A", "kN", "Euler critical load");
        c.Assign("lambda_0", "0.2 * sqrt(pi^2 * E / p_y)", null, "Limiting slenderness", "BS 5950-1 Annex C");
        c.Assign("eta", "max(0.001 * a_R * (lambda - lambda_0), 0)", null, "Perry factor (initial imperfection)", "BS 5950-1 Annex C");
        c.Assign("phi", "(p_y + (eta + 1) * p_E) / 2", "MPa", "Perry–Robertson φ");
        c.Assign("p_c", "p_E * p_y / (phi + sqrt(phi^2 - p_E * p_y))", "MPa", "Compressive strength", "BS 5950-1 Annex C");
        c.Assign("P_c", "p_c * A", "kN", "Compression resistance");
        c.Assign("u", "P / P_c", null, "Utilisation");

        foreach (var (symbol, text) in new[] { ("r", "Radius of gyration"), ("lambda", "Slenderness"), ("p_E", "Euler stress"), ("P_E", "Euler load"), ("lambda_0", "Limiting slenderness"), ("eta", "Perry factor"), ("p_c", "Compressive strength"), ("P_c", "Compression resistance"), ("u", "Utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Axial load against compression resistance", "u", "BS 5950-1 Annex C");
    }
}
