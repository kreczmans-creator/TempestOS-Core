using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// Basic rating life of a rolling bearing (ISO 281 form): equivalent dynamic
/// load, L₁₀ in millions of revolutions and hours, and the life modified for
/// reliability. Ported from the legacy <c>calc.bearing-rating-life</c> module
/// (ADR-0167); it also covers the legacy <c>calc.bearing-load-capacity</c>.
/// Its three hand-worked examples run as known-answer cases.
/// </summary>
public sealed class BearingLifeBasicCalculator : ICalcCalculator
{
    public const string Id = "mech.bearing-life-basic";
    public const string Ball = "ball";
    public const string Roller = "roller";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.bearing-rating-life (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.bearing-rating-life.md, Example {n}");

    public CalcTemplate Template { get; } = new(
        Id, "1.1.0", CalcDiscipline.Mechanical, "Rolling bearing basic rating life (simple form)",
        "Calculates the equivalent dynamic load, the basic rating life L₁₀ and the life modified for reliability of a rolling bearing, against a required life. It takes the reliability factor as entered and has no a_ISO factor.",
        "Equivalent dynamic load from the radial and axial loads: F_dyn = X·F_r + Y·F_a.\nBasic rating life in millions of revolutions (p = 3 for ball and 10/3 for roller bearings): L₁₀ = (C/F_dyn)^p.\nRating life in hours, at speed n: L₁₀h = L₁₀·10⁶ revolutions at speed n.\nLife adjusted for the required reliability: L_nm = a₁·L₁₀; L_nmh = a₁·L₁₀h.\nRange of validity of the life equation: F_dyn ≤ 0.5·C.",
        [
            new CalcInputSpec("type", "Bearing type (selects the life exponent)", CalcInputKind.Choice, Choices: [Ball, Roller], DefaultChoice: Ball, Group: "Setup"),
            new CalcInputSpec("C", "Basic dynamic load rating", CalcInputKind.Quantity, "kN", ReferenceLibrary: "Bearings", ReferenceProperty: "BasicDynamicRadial", ReferenceDimension: "Force", Default: 35.1, Min: 0.1, Max: 1e5, HardMin: 1e-9, HardMax: 1e8, Group: "Bearing"),
            new CalcInputSpec("F_r", "Radial load", CalcInputKind.Quantity, "kN", Default: 4, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("F_a", "Axial load", CalcInputKind.Quantity, "kN", Default: 0, Min: 0, Max: 1e5, HardMin: 0, HardMax: 1e8, Group: "Loads"),
            new CalcInputSpec("n", "Speed", CalcInputKind.Quantity, "rpm", Default: 1500, Min: 1, Max: 1e5, HardMin: 1e-6, HardMax: 1e7, Group: "Loads"),
            new CalcInputSpec("X", "Radial load factor (bearing catalogue)", CalcInputKind.Number, Default: 1, Min: 0, Max: 2, HardMin: 0, HardMax: 10, Group: "Factors"),
            new CalcInputSpec("Y", "Axial load factor (bearing catalogue)", CalcInputKind.Number, Default: 0, Min: 0, Max: 5, HardMin: 0, HardMax: 20, Group: "Factors"),
            new CalcInputSpec("a_1", "Life modification factor for reliability", CalcInputKind.Number, Default: 1, Min: 0.05, Max: 1, HardMin: 1e-6, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("L_req", "Required life (0 for no criterion)", CalcInputKind.Quantity, "h", Default: 5000, Min: 0, Max: 1e6, HardMin: 0, HardMax: 1e8, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Load and speed are steady, and the bearing is correctly fitted, lubricated and free of contamination.", "These are the conditions of the basic rating life."),
            new CalcAssumption("X and Y are the catalogue factors for this bearing and load ratio.", "Supplied by the user."),
        ],
        [
            new CalcClauseRef("ISO 281:2007", "cl. 6", "Basic dynamic load rating and basic rating life"),
            new CalcClauseRef("Budynas and Nisbett, Shigley's Mechanical Engineering Design", "Ch. 11, 11th ed.", "Rolling-contact bearings"),
        ],
        [
            new CalcApplicabilityRule("no-load", "The equivalent load must be positive.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("light-load", "P below a millionth of C: the life is effectively unlimited.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("heavy-load", "P above half the dynamic load rating is outside the basic rating life equation.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-legacy-ball-radial", "Legacy example 1: deep-groove ball bearing, pure radial load", Legacy(1),
                [CalcInputValue.Choice("type", Ball), CalcInputValue.Quantity("C", 35.1, "kN"), CalcInputValue.Quantity("F_r", 4, "kN"), CalcInputValue.Quantity("F_a", 0, "kN"), CalcInputValue.Of("X", 1), CalcInputValue.Of("Y", 0), CalcInputValue.Quantity("n", 1500, "rpm"), CalcInputValue.Of("a_1", 1), CalcInputValue.Quantity("L_req", 5000, "h")],
                [new CalcExpectedValue("F_dyn", 4.000, "kN", 5e-5), new CalcExpectedValue("k_P", 0.11396, "", 5e-5), new CalcExpectedValue("L_10", 675.68, "", 5e-5), new CalcExpectedValue("L_10h", 7507.6, "h", 5e-5)]),
            new CalcTestCase("ka-legacy-roller", "Legacy example 2: cylindrical roller bearing (fails the required life)", Legacy(2),
                [CalcInputValue.Choice("type", Roller), CalcInputValue.Quantity("C", 78, "kN"), CalcInputValue.Quantity("F_r", 12, "kN"), CalcInputValue.Quantity("F_a", 0, "kN"), CalcInputValue.Of("X", 1), CalcInputValue.Of("Y", 0), CalcInputValue.Quantity("n", 3000, "rpm"), CalcInputValue.Of("a_1", 1), CalcInputValue.Quantity("L_req", 4000, "h")],
                [new CalcExpectedValue("F_dyn", 12.000, "kN", 5e-5), new CalcExpectedValue("k_P", 0.15385, "", 5e-5), new CalcExpectedValue("L_10", 512.52, "", 5e-5), new CalcExpectedValue("L_10h", 2847.3, "h", 5e-5)]),
            new CalcTestCase("ka-legacy-combined-95", "Legacy example 3: ball bearing, combined load, 95 % reliability", Legacy(3),
                [CalcInputValue.Choice("type", Ball), CalcInputValue.Quantity("C", 25, "kN"), CalcInputValue.Quantity("F_r", 3, "kN"), CalcInputValue.Quantity("F_a", 1.5, "kN"), CalcInputValue.Of("X", 0.56), CalcInputValue.Of("Y", 1.5), CalcInputValue.Quantity("n", 1000, "rpm"), CalcInputValue.Of("a_1", 0.64), CalcInputValue.Quantity("L_req", 2000, "h")],
                [new CalcExpectedValue("F_dyn", 3.930, "kN", 5e-5), new CalcExpectedValue("k_P", 0.15720, "", 5e-5), new CalcExpectedValue("L_10", 257.42, "", 5e-5), new CalcExpectedValue("L_10h", 4290.3, "h", 5e-5), new CalcExpectedValue("L_nm", 164.75, "", 5e-5), new CalcExpectedValue("L_nmh", 2745.8, "h", 5e-5)]),
        ],
        Category: "Bearings",
        OutputPorts:
        [
            new CalcOutputPort("L_nmh", "h", "Modified rating life in hours", "bearing.life_modified"),
            new CalcOutputPort("L_10h", "h", "Basic rating life in hours", "bearing.life_basic"),
            new CalcOutputPort("F_dyn", "kN", "Equivalent dynamic load", "bearing.load_dynamic", ["P"]),
            new CalcOutputPort("k_P", "", "Load ratio of the equivalent load to the dynamic load rating", "bearing.load_ratio"),
        ]);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_life");
        var type = c.InputText("type");

        foreach (var s in new[] { "C", "F_r", "F_a", "X", "Y", "n", "a_1", "L_req" })
            c.Input(s);

        c.Assign("p", type == Roller ? "10 / 3" : "3", null, $"Life exponent ({type} bearing)", "ISO 281:2007 cl. 6");
        c.Assign("F_dyn", "X * F_r + Y * F_a", "kN", "Equivalent dynamic load", "ISO 281:2007 cl. 6");

        if (!(c.Si("F_dyn") > 0))
        {
            c.Block("no-load", "F_r: the equivalent load is zero. Enter a radial or an axial load.");
            return;
        }

        c.Assign("k_P", "F_dyn / C", null, "Load ratio of the equivalent load to the rating");

        if (c.Si("k_P") < 1e-6)
        {
            c.Block("light-load", "F_r: the load is below a millionth of the rating C. The life is effectively unlimited and outside the rating-life equation.");
            return;
        }

        if (c.Si("k_P") > 0.5)
        {
            c.Block("heavy-load", "F_r: the equivalent load exceeds half the dynamic load rating. The basic rating life equation is not used there.");
            return;
        }

        c.Assign("L_10", "(C / F_dyn)^p", null, "Basic rating life (millions of revolutions)", "ISO 281:2007 cl. 6");
        c.Assign("L_10h", "L_10 * 1e6 * 2 * pi / n", "h", "Basic rating life in hours (10⁶ revolutions of 2π rad at speed n)");
        c.Assign("L_nm", "a_1 * L_10", null, "Modified rating life (millions of revolutions)");
        c.Assign("L_nmh", "a_1 * L_10h", "h", "Modified rating life in hours");

        foreach (var (symbol, text) in new[] { ("F_dyn", "Equivalent dynamic load"), ("k_P", "Load ratio"), ("L_10", "L₁₀ (millions of revolutions)"), ("L_10h", "L₁₀ in hours"), ("L_nm", "Modified life (millions of revolutions)"), ("L_nmh", "Modified life in hours") })
            c.Output(symbol, text);

        if (c.Si("L_req") > 0)
        {
            c.Assign("u_life", "L_req / L_nmh", null, "Required over modified life");
            c.Check("Required life against modified life", "L_req", "L_nmh", "ISO 281:2007 cl. 6", resultSymbol: "u_life");
        }
    }
}
