using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Sheets;

/// <summary>
/// A life check whose demand is an input (the required life) and whose capacity is the life achieved: it shows both as values with units, and
/// <c>resultSymbol</c> names the utilisation output the sheet marks as governing and shows by the check's verdict (C2-21, C2-31).
/// </summary>
public sealed class ResultSymbolCalculator : ICalcCalculator
{
    public CalcTemplate Template { get; } = new(
        "test.result-symbol", "1.0.0", CalcDiscipline.Mechanical, "Life against requirement (test)",
        "Calculates the life of a part against a required life. It is a test sheet.",
        "Utilisation: u = L_req/L_ach.",
        [
            new CalcInputSpec("L_req", "Required life", CalcInputKind.Quantity, "h", Default: 5000, Min: 1, Max: 1e6, HardMin: 1e-3, HardMax: 1e7),
            new CalcInputSpec("L_ach", "Life achieved", CalcInputKind.Quantity, "h", Default: 7500, Min: 1, Max: 1e6, HardMin: 1e-3, HardMax: 1e7),
        ],
        [new CalcAssumption("The life achieved is given.", "It is an input of this test.")],
        [new CalcClauseRef("ISO 281:2007", "cl. 9", "Modified rating life")],
        [],
        [new CalcTestCase("ka-1", "5000 h against 7500 h", new CalcReferenceSource("Hand calculation", null, "u = 5000/7500"), [CalcInputValue.Quantity("L_req", 5000, "h"), CalcInputValue.Quantity("L_ach", 7500, "h")], [new CalcExpectedValue("u", 0.666667, "", 1e-5)])],
        Category: "Bearings");

    public void Run(CalcRunContext c)
    {
        c.Input("L_req");
        c.Input("L_ach");
        c.Assign("u", "L_req / L_ach", null, "Life utilisation");
        c.Output("L_ach", "Life achieved");
        c.Output("u", "Life utilisation");
        c.Check("Required life against life achieved", "L_req", "L_ach", "ISO 281:2007 cl. 9", resultSymbol: "u");
    }
}

public sealed class CheckResultSymbolTests
{
    private static readonly ResultSymbolCalculator Calculator = new();

    private static CalcRunResult Run(double achieved) =>
        CalcRunner.Run(Calculator, new CalcInputSet([CalcInputValue.Quantity("L_req", 5000, "h"), CalcInputValue.Quantity("L_ach", achieved, "h")]));

    [Fact]
    public void A_Check_Made_On_An_Input_Names_The_Utilisation_Output_It_Governs_And_Shows_Its_Two_Values()
    {
        var run = Run(7500);
        var check = Assert.Single(run.Checks);

        Assert.Equal("u", check.StepSymbol);
        Assert.Equal("5000 h", check.DemandText);
        Assert.Equal("7500 h", check.CapacityText);
        Assert.True(check.Passed);
        Assert.Equal(5000.0 / 7500.0, check.Utilisation, 12);
        Assert.Equal("0.67", run.FindOutput("u")!.ValueText);
    }

    [Fact]
    public void The_Sheet_Marks_That_Utilisation_As_Governing_And_Prints_The_Check_Without_A_Ratio_Text()
    {
        var run = Run(7500);
        var sheet = CalcSheetBuilder.Build(Calculator.Template, new CalcInputSet([CalcInputValue.Quantity("L_req", 5000, "h"), CalcInputValue.Quantity("L_ach", 7500, "h")]), run, GoldenFiles.FixedHeader("x", "x"), GoldenFiles.FixedFooter(Calculator.Template, 1, 1));
        var text = CalcSheetTextRenderer.Render(sheet);

        Assert.Equal("u", sheet.GoverningOutputSymbol);
        Assert.Contains("u = 0.67    Life utilisation (governing)", text, StringComparison.Ordinal);
        Assert.Contains("PASS  Required life against life achieved: 5000 h vs 7500 h  utilisation 0.67", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Failing_Check_Rounds_The_Utilisation_Output_Up_And_Never_Prints_It_At_Its_Limit()
    {
        var run = Run(4999.9);
        var check = Assert.Single(run.Checks);

        Assert.False(check.Passed);
        Assert.Equal("1.01", run.FindOutput("u")!.ValueText);
        Assert.Equal("1.01", check.UtilisationText);
    }
}
