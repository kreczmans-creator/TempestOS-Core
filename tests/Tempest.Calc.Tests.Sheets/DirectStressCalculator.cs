using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>A small but complete calculator used by the Sheets tests: axial stress against an allowable, with one warning rule and one block rule.</summary>
public sealed class DirectStressCalculator : ICalcCalculator
{
    public static readonly CalcTemplate TemplateDefinition = new(
        "test.direct-stress", "1.0.0", CalcDiscipline.Structural, "Direct stress check",
        "Axial stress in a prismatic member under a tensile or compressive load, compared with an allowable stress.",
        "σ = F / A; utilisation = σ / σ_allow.",
        [
            new CalcInputSpec("F", "Axial load", CalcInputKind.Quantity, "kN", Default: 12, Min: 0, Max: 10_000, HardMin: -1e6, HardMax: 1e6),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "mm^2", Default: 200, Min: 1, HardMin: 1e-9),
            new CalcInputSpec("sigma_allow", "Allowable stress", CalcInputKind.Quantity, "MPa", Default: 160, Min: 1, HardMin: 1e-9),
            new CalcInputSpec("grade", "Steel grade", CalcInputKind.Choice, Choices: ["S235", "S275", "S355"], DefaultChoice: "S275"),
            new CalcInputSpec("slender", "Member is slender", CalcInputKind.Boolean, Default: 0),
            new CalcInputSpec("note", "Note", CalcInputKind.Text, IsOptional: true),
        ],
        [new CalcAssumption("The load acts through the centroid.", "No bending from eccentricity.")],
        [new CalcClauseRef("EN 1993-1-1", "6.2.3", "Tension")],
        [
            new CalcApplicabilityRule("high-stress", "Stress above 80 % of allowable is flagged.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("slender", "A slender member cannot be checked in direct compression here.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-1", "12 kN on 200 mm²", new CalcReferenceSource("Hand calculation", null, "σ = 12 000 / 200 = 60 MPa"),
                [CalcInputValue.Quantity("F", 12, "kN"), CalcInputValue.Quantity("A", 200, "mm^2"), CalcInputValue.Quantity("sigma_allow", 160, "MPa")],
                [new CalcExpectedValue("sigma", 60, "MPa", 1e-9), new CalcExpectedValue("u", 0.375, "", 1e-9)]),
            new CalcTestCase("ka-2", "1 kip on 1 in²", new CalcReferenceSource("Hand calculation", null, "σ = 1 ksi"),
                [CalcInputValue.Quantity("F", 1, "kip"), CalcInputValue.Quantity("A", 1, "in^2"), CalcInputValue.Quantity("sigma_allow", 20, "ksi")],
                [new CalcExpectedValue("sigma", 1, "ksi", 1e-9)], CalcReferenceStatus.Unconfirmed),
        ]);

    public CalcTemplate Template => TemplateDefinition;

    public void Run(CalcRunContext c)
    {
        c.Input("F");
        c.Input("A");
        c.Input("sigma_allow");
        var grade = c.InputText("grade");
        var slender = c.InputFlag("slender");

        if (slender && c.InputSi("F") < 0)
        {
            c.Block("slender", "slender: a slender member in compression needs a buckling check, which this calculation does not do.");
            return;
        }

        c.Assign("sigma", "F / A", "MPa", "Direct stress", "EN 1993-1-1 §6.2.3");
        c.Assign("u", "sigma / sigma_allow", null, "Utilisation");
        c.Record("note_grade", "grade", grade, new Engine.Values.TextValue(grade), "Steel grade as chosen");
        c.Output("sigma", "Direct stress");
        c.Output("u", "Utilisation");
        c.CheckUtilisation("Direct stress", "u", "EN 1993-1-1 §6.2.3");

        if (c.Si("u") > 0.8)
            c.Warn("high-stress", "u: the stress is above 80 % of the allowable.");

        c.Series(new CalcSeries("Stress vs load", "F", "kN", "σ", "MPa", [(0, 0), (c.InputSi("F") / 1000, c.Si("sigma") / 1e6)]));
    }
}
