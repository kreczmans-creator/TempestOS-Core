using Tempest.Calc.Engine.Values;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class Iso286FitTests
{
    private static readonly Iso286FitCalculator Fit = new();

    private static CalcInputSet Seat() => CalcTemplateHasher.DefaultInputs(Fit.Template)
        .With(CalcInputValue.Quantity("D", 35, "mm")).With(CalcInputValue.Choice("shaft_letter", "k")).With(CalcInputValue.Of("shaft_grade", 6));

    private static string Kind(CalcRunResult r) => ((TextValue)r.Outputs.Single(o => o.Symbol == "fit").Value).Value;

    [Fact]
    public void An_Iso_286_H7_Hole_On_A_k6_Seat_Is_A_Transition_Fit()
    {
        // The class the drive-train example used for a bearing bore (Green G-A-04): H7/k6 at 35 mm has clearance at the loose end.
        var r = CalcRunner.Run(Fit, Seat().With(CalcInputValue.Choice("hole_kind", Iso286FitCalculator.IsoHole)).With(CalcInputValue.Choice("hole_letter", "H")).With(CalcInputValue.Of("hole_grade", 7)));
        Assert.Equal("transition", Kind(r));
        Assert.True(r.OutputSi("c_max") > 0);
    }

    [Fact]
    public void A_Bearing_Bore_Needs_Its_Deviations_From_The_Bearing_Record()
    {
        var r = CalcRunner.Run(Fit, Seat().With(CalcInputValue.Choice("hole_kind", Iso286FitCalculator.BearingBore)));
        Assert.True(r.IsBlocked);
        Assert.Equal("bearing-bore", r.Blocks[0].RuleId);
        Assert.StartsWith("ES_override:", r.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Bearing_Bore_Below_Nominal_On_A_k6_Seat_Is_An_Interference_Fit_At_Every_Size_Combination()
    {
        var r = CalcRunner.Run(Fit, Seat().With(CalcInputValue.Choice("hole_kind", Iso286FitCalculator.BearingBore)).With(CalcInputValue.Quantity("ES_override", 0, "µm")).With(CalcInputValue.Quantity("EI_override", -9, "µm")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal("interference", Kind(r));
        Assert.True(r.OutputSi("c_max") < 0);
    }
}
