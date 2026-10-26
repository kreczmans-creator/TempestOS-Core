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

    private static CalcRunResult Fit286(double dMm, string holeLetter, int holeGrade, string shaftLetter, int shaftGrade) =>
        CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template).With(CalcInputValue.Quantity("D", dMm, "mm")).With(CalcInputValue.Choice("hole_letter", holeLetter)).With(CalcInputValue.Of("hole_grade", holeGrade))
            .With(CalcInputValue.Choice("shaft_letter", shaftLetter)).With(CalcInputValue.Of("shaft_grade", shaftGrade)));

    [Fact]
    public void A_Size_On_A_Step_Limit_Takes_The_Step_Below_It()
    {
        // mech.iso286-fit-01: 18 mm x 0.001 x 1000 = 18.000000000000004 fell into the 18 to 30 mm step: IT7 20.918 um (the 25 mm value) instead of the 10 to 18 mm step's.
        // python3: D = sqrt(10 x 18) = 13.4164 mm, i = 0.45 D^(1/3) + 0.001 D = 1.082696 um, IT7 = 16 i = 17.3231 um (the standard's tabulated 18), IT6 = 10.8270 um.
        var r = Fit286(18, "H", 7, "h", 6);
        Assert.Equal(17.3231, r.OutputSi("IT_hole") * 1e6, 3);
        Assert.Equal(10.8270, r.OutputSi("IT_shaft") * 1e6, 3);
        Assert.Equal(18.0, Iso286.Step(18.0).Upper);
        Assert.Equal(18.0, Iso286.Step(18.001).Lower);

        // Every step limit, however it was arrived at by a unit conversion, belongs to the step it closes.
        foreach (var upper in new[] { 3.0, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150 })
        {
            Assert.Equal(upper, Iso286.Step(upper * 0.001 * 1000).Upper);
            Assert.Equal(upper, Iso286.Step(upper * (1 + 4e-16)).Upper);
        }
    }

    [Fact]
    public void A_Hole_N_Above_IT8_Has_A_Zero_Upper_Deviation()
    {
        // mech.iso286-fit-02: ISO 286-1 sets ES = 0 for N above IT8 (sizes over 3 mm); the sheet used ES = -5 D^0.34: 25 mm N9 read ES -14.571, EI -66.866 um, against 0 and -IT9.
        // python3: D = sqrt(18 x 30) = 23.2379 mm, i = 1.307375 um, IT9 = 40 i = 52.2950 um.
        var n9 = Fit286(25, "N", 9, "h", 9);
        Assert.Equal(0.0, n9.OutputSi("ES") * 1e6, 9);
        Assert.Equal(-52.2950, n9.OutputSi("EI") * 1e6, 3);

        // N up to IT8 keeps its formula (N8 at 25 mm: -5 D^0.34 + (IT8 - IT7) = -2.804 um, the tabulated -3), and the rule holds at the top of the range.
        Assert.Equal(-2.8042, Fit286(25, "N", 8, "h", 8).OutputSi("ES") * 1e6, 3);
        Assert.Equal(0.0, Fit286(500, "N", 10, "h", 9).OutputSi("ES") * 1e6, 9);
    }

    [Fact]
    public void An_Interference_Letter_From_The_Formula_Warns_That_The_Tabulated_Value_Can_Be_Several_Micrometres_Larger()
    {
        // mech.iso286-fit-05: p6 at 450 mm gave ei 62.216 um against the tabulated 68 (the standard's tables add their own increments and sub-steps for p to zc), with no notice.
        var p6 = Fit286(450, "H", 7, "p", 6);
        Assert.Equal(62.2156, p6.OutputSi("ei") * 1e6, 3);
        var warning = Assert.Single(p6.Warnings, w => w.RuleId == "interference-letter");
        Assert.Contains("62.2", warning.Message, StringComparison.Ordinal);
        Assert.Contains("68", warning.Message, StringComparison.Ordinal);
        Assert.Contains("override", warning.Message, StringComparison.Ordinal);
        Assert.Contains(Fit.Template.ApplicabilityRules, r => r.Id == "interference-letter" && r.Severity == CalcNoticeSeverity.Warning);

        // The hole letters are covered too; a letter below p is silent, and so is a shaft whose deviations come from a reference record.
        Assert.Contains(Fit286(100, "S", 7, "h", 6).Warnings, w => w.RuleId == "interference-letter");
        Assert.DoesNotContain(Fit286(25, "H", 7, "g", 6).Warnings, w => w.RuleId == "interference-letter");
        Assert.DoesNotContain(Fit286(25, "K", 7, "n", 6).Warnings, w => w.RuleId == "interference-letter");
        var overridden = CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template).With(CalcInputValue.Quantity("D", 450, "mm")).With(CalcInputValue.Choice("shaft_letter", "p")).With(CalcInputValue.Of("shaft_grade", 6))
            .With(CalcInputValue.Quantity("es_override", 131, "µm")).With(CalcInputValue.Quantity("ei_override", 68, "µm")));
        Assert.DoesNotContain(overridden.Warnings, w => w.RuleId == "interference-letter");

        // Above 500 mm the published formula for p is 0.072 D + 37.8 um (the 500 to 630 mm step: 78.2 um, the tabulated 78), not IT7.
        Assert.Equal(78.2099, Fit286(565, "H", 7, "p", 6).OutputSi("ei") * 1e6, 3);
    }
}
