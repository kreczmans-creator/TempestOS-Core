using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Verification;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// ADR-0167 Amendment 2: the port of the legacy bearing module could not take the id <c>mech.bearing-life</c> because Addendum A's M5 already
/// has it. These tests are the evidence for keeping the port as <c>mech.bearing-life-basic</c> rather than dropping it: M5 agrees with the
/// legacy vectors where it can, and differs from them in three stated ways. If M5 ever grows an entered a₁, a load ratio and the legacy blocks,
/// the third test fails and the port can be retired.
/// </summary>
public sealed class BearingLifeBasicTests
{
    private static readonly BearingLifeCalculator Full = new();
    private static readonly BearingLifeBasicCalculator Basic = new();

    private static CalcInputSet Defaults(ICalcCalculator calc) => CalcTemplateHasher.DefaultInputs(calc.Template);

    /// <summary>The legacy example as M5 is asked: reliability 90 % gives a₁ = 1 exactly, so the unmodified life is what is compared.</summary>
    private static CalcRunResult RunFull(string type, double c, double fr, double fa, double x, double y, double n, double reliability) =>
        CalcRunner.Run(Full, Defaults(Full).With(CalcInputValue.Choice("type", type))
            .With(CalcInputValue.Quantity("C", c, "kN")).With(CalcInputValue.Quantity("F_r", fr, "kN")).With(CalcInputValue.Quantity("F_a", fa, "kN"))
            .With(CalcInputValue.Of("e_lim", 0)).With(CalcInputValue.Of("X", x)).With(CalcInputValue.Of("Y", y))
            .With(CalcInputValue.Quantity("n", n, "rpm")).With(CalcInputValue.Quantity("R", reliability, "%")));

    [Fact]
    public void M5_Reproduces_Legacy_Examples_One_And_Two_For_The_Unmodified_Life()
    {
        var ball = RunFull(BearingLifeCalculator.Ball, 35.1, 4, 0, 1, 0, 1500, 90);
        Assert.Equal(4.000, ball.OutputSi("P") / 1e3, 3);
        Assert.Equal(675.68, ball.OutputSi("L_10"), 2);
        Assert.Equal(7507.6, ball.OutputSi("L_10h") / 3600, 1);

        var roller = RunFull(BearingLifeCalculator.Roller, 78, 12, 0, 1, 0, 3000, 90);
        Assert.Equal(12.000, roller.OutputSi("P") / 1e3, 3);
        Assert.Equal(512.52, roller.OutputSi("L_10"), 2);
        Assert.Equal(2847.3, roller.OutputSi("L_10h") / 3600, 1);
    }

    [Fact]
    public void The_Basic_Port_Carries_All_Three_Legacy_Examples_Including_The_Entered_Reliability_Factor()
    {
        foreach (var testCase in Basic.Template.TestCases)
            Assert.True(KnownAnswerRunner.RunCase(Basic, testCase).Passed, testCase.Id);

        // Example 3: a₁ is entered (0.64), and L_nm, L_nmh follow from it.
        var third = Basic.Template.TestCases.Single(t => t.Id == "ka-legacy-combined-95");
        var run = CalcRunner.Run(Basic, new CalcInputSet(third.Inputs));
        Assert.Equal(164.75, run.OutputSi("L_nm"), 2);
        Assert.Equal(2745.8, run.OutputSi("L_nmh") / 3600, 1);
    }

    [Fact]
    public void M5_Cannot_Reproduce_The_Rest_Of_Legacy_Example_Three_The_Load_Ratio_Or_The_Legacy_Blocks()
    {
        // (a) M5 derives a₁ from the reliability (0.6379 at 95 %), where the legacy module was given 0.64: the modified life differs by 0.3 %.
        var m5 = RunFull(BearingLifeCalculator.Ball, 25, 3, 1.5, 0.56, 1.5, 1000, 95);
        Assert.False(m5.IsBlocked, string.Join("; ", m5.Blocks.Select(b => b.Message)));
        Assert.NotEqual(2745.8, m5.OutputSi("L_nmh") / 3600, 1);
        Assert.InRange(Math.Abs(m5.OutputSi("L_nmh") / 3600 / 2745.8 - 1), 1e-3, 1e-2);

        // (b) M5 reports no load ratio P/C, which the legacy module (and the port) reports.
        Assert.Null(m5.FindOutput("k_P"));
        Assert.NotNull(CalcRunner.Run(Basic, Defaults(Basic)).FindOutput("k_P"));

        // (c) At P above half the dynamic rating the legacy module refuses and M5 only warns.
        var heavy = RunFull(BearingLifeCalculator.Ball, 35.1, 20, 0, 1, 0, 1500, 90);
        Assert.False(heavy.IsBlocked);
        Assert.Contains(CalcRunner.Run(Basic, Defaults(Basic).With(CalcInputValue.Quantity("F_r", 20, "kN"))).Blocks, b => b.RuleId == "heavy-load");
    }

    [Fact]
    public void The_Two_Bearing_Templates_Have_Distinct_Ids_And_Both_Are_Shipped()
    {
        Assert.NotEqual(Full.Template.Id, Basic.Template.Id);
        Assert.Equal("mech.bearing-life", Full.Template.Id);
        Assert.Equal("mech.bearing-life-basic", Basic.Template.Id);
        Assert.Contains(MechanicalCalculators.All, c => c.Template.Id == "mech.bearing-life");
        Assert.Contains(MechanicalCalculators.All, c => c.Template.Id == "mech.bearing-life-basic");
    }
}
