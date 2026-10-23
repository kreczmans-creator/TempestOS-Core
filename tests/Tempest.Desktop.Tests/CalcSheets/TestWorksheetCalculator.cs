using Tempest.Calc.Sheets;

namespace Tempest.Desktop.Tests.CalcSheets;

/// <summary>A small worksheet calculator for the Desktop tests: axial stress against an allowable.</summary>
internal sealed class TestWorksheetCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.desktop-stress", "1.0.0", CalcDiscipline.Structural, "Desktop stress check",
        "Axial stress in a member under load, compared with an allowable stress.", "σ = F / A; u = σ / σ_allow.",
        [
            new CalcInputSpec("F", "Axial load", CalcInputKind.Quantity, "kN", Default: 12, Min: 0, Max: 10_000, HardMin: -1e6, HardMax: 1e6),
            new CalcInputSpec("A", "Cross-section area", CalcInputKind.Quantity, "mm^2", Default: 200, Min: 1, HardMin: 1e-9),
            new CalcInputSpec("sigma_allow", "Allowable stress", CalcInputKind.Quantity, "MPa", Default: 160, Min: 1, HardMin: 1e-9),
            new CalcInputSpec("grade", "Steel grade", CalcInputKind.Choice, Choices: ["S235", "S275", "S355"], DefaultChoice: "S275"),
        ],
        [new CalcAssumption("The load acts through the centroid.")],
        [new CalcClauseRef("EN 1993-1-1", "6.2.3", "Tension")],
        [new CalcApplicabilityRule("high-stress", "Stress above 80 % of allowable is flagged.", CalcNoticeSeverity.Warning)],
        [
            new CalcTestCase("ka-1", "12 kN on 200 mm²", new CalcReferenceSource("Hand calculation", null, "σ = 60 MPa"),
                [CalcInputValue.Quantity("F", 12, "kN"), CalcInputValue.Quantity("A", 200, "mm^2"), CalcInputValue.Quantity("sigma_allow", 160, "MPa")],
                [new CalcExpectedValue("sigma", 60, "MPa", 1e-9)]),
        ]);

    public void Run(CalcRunContext c)
    {
        c.Input("F");
        c.Input("A");
        c.Input("sigma_allow");
        c.Input("grade");
        c.Assign("sigma", "F / A", "MPa", "Direct stress", "EN 1993-1-1 §6.2.3");
        c.Assign("u", "sigma / sigma_allow", null, "Utilisation");
        c.Output("sigma", "Direct stress");
        c.Output("u", "Utilisation");
        c.CheckUtilisation("Direct stress", "u", "EN 1993-1-1 §6.2.3");

        if (c.Si("u") > 0.8)
            c.Warn("high-stress", "u: the stress is above 80 % of the allowable.");
    }
}
