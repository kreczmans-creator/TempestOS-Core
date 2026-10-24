using CsCheck;
using Tempest.Calc.Engine;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Mechanical.ToleranceStack;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>The public M1 stack engine (B-0): the worst-case, RSS, capability and seeded Monte Carlo arithmetic the worksheet and other calculators share.</summary>
public sealed class ToleranceStackEngineTests
{
    /// <summary>The housing example of the M1 tests, in mm: bore depth 40 +0.10/0, bearing 20 +0/−0.12 (subtracts), spacer 19.5 ±0.02 (subtracts).</summary>
    private static IReadOnlyList<Contributor> Housing() =>
    [
        Contributor.Size("Housing bore depth", +1, 40, 0.10, 0),
        Contributor.Size("Bearing width", -1, 20, 0, -0.12),
        Contributor.Size("Spacer", -1, 19.5, 0.02, -0.02),
    ];

    [Fact]
    public void The_Housing_Stack_Gives_The_Analytic_Worst_Case_Rss_And_Capability()
    {
        var r = ToleranceStackEngine.Analyse(Housing(), new StackRequirement(0.40, 0.80));
        Assert.Equal(0.5, r.Nominal, 12);
        Assert.Equal(0.61, r.Mean, 12);
        Assert.Equal(0.13, r.WorstCase, 12);
        Assert.Equal(0.48, r.WorstCaseMinimum, 12);
        Assert.Equal(0.74, r.WorstCaseMaximum, 12);
        Assert.Equal(0.080622577, r.Rss, 9);
        Assert.Equal(0.61 - 0.080622577, r.RssMinimum, 9);
        Assert.Equal(1.5 * 0.080622577, r.ModifiedRss, 8);
        Assert.Equal(0.080622577 / 3, r.Sigma, 9);
        Assert.NotNull(r.Capability);
        Assert.Equal(2.35666, r.Capability!.Cpk, 4);
        Assert.Equal(0.40 / (6 * (0.080622577 / 3)), r.Capability.Cp!.Value, 6);
        Assert.InRange(r.Capability.DefectsPerMillion, 7.5e-7, 8.0e-7);
        Assert.Equal(1 - r.Capability.DefectsPerMillion / 1e6, r.Capability.Yield, 15);
    }

    [Fact]
    public void Margins_And_The_Worst_Case_Verdict_Follow_The_Requirement()
    {
        var inside = ToleranceStackEngine.Analyse(Housing(), new StackRequirement(0.40, 0.80));
        Assert.Equal(0.08, inside.MarginToMinimum!.Value, 12);
        Assert.Equal(0.06, inside.MarginToMaximum!.Value, 12);
        Assert.True(inside.WorstCaseWithinRequirement);

        var tight = ToleranceStackEngine.Analyse(Housing(), new StackRequirement(0.50, null));
        Assert.False(tight.WorstCaseWithinRequirement);
        Assert.Equal(-0.02, tight.MarginToMinimum!.Value, 12);
        Assert.Null(tight.MarginToMaximum);
        Assert.NotNull(tight.Capability);
        Assert.Null(tight.Capability!.Cp);

        // A worst case that just touches the limit passes (rounding slack).
        var touching = ToleranceStackEngine.Analyse(Housing(), new StackRequirement(0.48, 0.74));
        Assert.True(touching.WorstCaseWithinRequirement);
    }

    [Fact]
    public void Without_A_Requirement_There_Is_No_Capability_And_The_Verdict_Is_Open()
    {
        var r = ToleranceStackEngine.Analyse(Housing());
        Assert.Null(r.Capability);
        Assert.Null(r.MarginToMinimum);
        Assert.Null(r.MarginToMaximum);
        Assert.True(r.WorstCaseWithinRequirement);
        Assert.False(r.Requirement.IsStated);
    }

    [Fact]
    public void Distributions_Set_The_Standard_Deviation_Of_Each_Contributor()
    {
        Assert.Equal(0.02 / 3, Contributor.Size("n", 1, 10, 0.02, -0.02).Sigma, 15);
        Assert.Equal(0.02 / 2, Contributor.Size("n", 1, 10, 0.02, -0.02, k: 2).Sigma, 15);
        Assert.Equal(0.02 / Math.Sqrt(3), Contributor.Size("u", 1, 10, 0.02, -0.02, distribution: ContributorDistribution.Uniform).Sigma, 15);
        Assert.Equal(0.02 / Math.Sqrt(6), Contributor.Size("t", 1, 10, 0.02, -0.02, distribution: ContributorDistribution.Triangular).Sigma, 15);
    }

    [Fact]
    public void A_Mean_Shift_Moves_The_Mean_And_Not_The_Spread()
    {
        var plain = Contributor.Size("a", 1, 10, 0.05, -0.05);
        var shifted = Contributor.Size("a", 1, 10, 0.05, -0.05, shift: 0.03);
        Assert.Equal(plain.Mid + 0.03, shifted.Mid, 15);
        Assert.Equal(plain.Half, shifted.Half, 15);
        Assert.Equal(plain.Sigma, shifted.Sigma, 15);
    }

    [Fact]
    public void A_Position_Zone_Contributes_Half_The_Zone_Plus_Half_The_Mmc_Bonus_About_Nominal()
    {
        var zone = Contributor.Zone("pos", -1, 0, 0.20, bonus: 0.06);
        Assert.Equal(0.13, zone.Half, 15);
        Assert.Equal(0.0, zone.Mid, 15);
        Assert.Equal(0.13 / 3, zone.Sigma, 15);
    }

    [Fact]
    public void Contribution_Shares_Add_To_A_Hundred_And_Name_The_Largest()
    {
        var r = ToleranceStackEngine.Analyse(Housing());
        Assert.Equal(100.0, r.WorstCaseShare.Sum(), 9);
        Assert.Equal(100.0, r.StatisticalShare.Sum(), 9);
        Assert.Equal(1, r.LargestStatisticalIndex); // the bearing, ±0.06
        Assert.Equal(100 * 0.06 / 0.13, r.WorstCaseShare[1], 9);
    }

    [Fact]
    public void An_Exact_Tie_For_Largest_Names_The_First()
    {
        var r = ToleranceStackEngine.Analyse([Contributor.Size("a", 1, 1, 0.1, -0.1), Contributor.Size("b", 1, 1, 0.1, -0.1)]);
        Assert.Equal(0, r.LargestStatisticalIndex);
    }

    [Fact]
    public void A_Zero_Band_Stack_Has_No_Spread_And_No_Capability_Figures()
    {
        var r = ToleranceStackEngine.Analyse([Contributor.Size("a", 1, 5, 0, 0)], new StackRequirement(4, 6));
        Assert.Equal(0.0, r.Sigma);
        Assert.Null(r.Capability);
        Assert.True(r.WorstCaseWithinRequirement);
        Assert.Equal(0.0, r.WorstCaseShare[0]);
    }

    [Fact]
    public void Bad_Input_Is_Refused_By_Name()
    {
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse([]));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Analyse(Housing(), new StackRequirement(1, 0)));
        Assert.Throws<CalcEvaluationException>(() => Contributor.Size("x", 1, 10, -0.1, 0.1));
        Assert.Throws<CalcEvaluationException>(() => Contributor.Size("x", 2, 10, 0.1, -0.1));
        Assert.Throws<CalcEvaluationException>(() => Contributor.Size("x", 1, 10, 0.1, -0.1, k: 0));
        Assert.Throws<CalcEvaluationException>(() => Contributor.Zone("x", 1, 0, -0.1));
        Assert.Throws<CalcLimitException>(() => ToleranceStackEngine.Analyse(Enumerable.Range(0, ToleranceStackEngine.MaxContributors + 1).Select(i => Contributor.Size("c" + i, 1, 1, 0.01, -0.01)).ToList()));
    }

    // ---- Monte Carlo ----------------------------------------------------------------------

    [Fact]
    public void The_Monte_Carlo_Is_Deterministic_For_A_Seed_And_Differs_Between_Seeds()
    {
        var req = new StackRequirement(0.40, 0.80);
        var a = ToleranceStackEngine.Simulate(Housing(), 5_000, 42, req);
        var b = ToleranceStackEngine.Simulate(Housing(), 5_000, 42, req);
        var other = ToleranceStackEngine.Simulate(Housing(), 5_000, 43, req);

        Assert.Equal(a.Mean, b.Mean);
        Assert.Equal(a.StandardDeviation, b.StandardDeviation);
        Assert.Equal(a.Percentile05, b.Percentile05);
        Assert.Equal(a.Percentile95, b.Percentile95);
        Assert.Equal(a.Histogram.Select(h => h.Count), b.Histogram.Select(h => h.Count));
        Assert.Equal(a.Yield, b.Yield);
        Assert.NotEqual(a.Mean, other.Mean);
    }

    [Fact]
    public void The_Monte_Carlo_Agrees_With_The_Predicted_Mean_And_Sigma()
    {
        var analysis = ToleranceStackEngine.Analyse(Housing());
        var mc = ToleranceStackEngine.Simulate(Housing(), 50_000, 7);
        Assert.Equal(analysis.Mean, mc.Mean, 3);
        Assert.Equal(analysis.Sigma, mc.StandardDeviation, 3);
        Assert.Equal(50_000, mc.Samples);
        Assert.Equal(7UL, mc.Seed);
    }

    [Fact]
    public void Uniform_And_Triangular_Contributors_Stay_Inside_Their_Bands()
    {
        var stack = new[]
        {
            Contributor.Size("u", 1, 10, 0.05, -0.05, distribution: ContributorDistribution.Uniform),
            Contributor.Size("t", -1, 4, 0.02, -0.02, distribution: ContributorDistribution.Triangular),
        };
        var a = ToleranceStackEngine.Analyse(stack);
        var mc = ToleranceStackEngine.Simulate(stack, 20_000, 3);
        Assert.InRange(mc.Minimum, a.WorstCaseMinimum - 1e-9, a.WorstCaseMaximum);
        Assert.InRange(mc.Maximum, a.WorstCaseMinimum, a.WorstCaseMaximum + 1e-9);
        Assert.Equal(a.Sigma, mc.StandardDeviation, 3);
    }

    [Fact]
    public void A_Run_Can_Be_Cancelled_And_Over_The_Cap_Is_Refused()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => ToleranceStackEngine.Simulate(Housing(), 100_000, 1, cancellationToken: cts.Token));
        Assert.Throws<CalcLimitException>(() => ToleranceStackEngine.Simulate(Housing(), 0, 1));
        Assert.Throws<CalcEvaluationException>(() => ToleranceStackEngine.Simulate([], 100, 1));
    }

    // ---- the worksheet is the engine -------------------------------------------------------

    [Fact]
    public void The_M1_Sheet_Reports_Exactly_What_The_Engine_Computes()
    {
        var inputs = new CalcInputSet(
        [
            CalcInputValue.Table("contributors",
            [
                CalcRow.Of(("name", new CalcCell(Text: "Housing bore depth")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(40, "mm")), ("upper", new CalcCell(0.10, "mm")), ("lower", new CalcCell(0, "mm"))),
                CalcRow.Of(("name", new CalcCell(Text: "Bearing width")), ("direction", new CalcCell(Text: "subtracts")), ("nominal", new CalcCell(20, "mm")), ("upper", new CalcCell(0, "mm")), ("lower", new CalcCell(-0.12, "mm"))),
                CalcRow.Of(("name", new CalcCell(Text: "Spacer")), ("direction", new CalcCell(Text: "subtracts")), ("nominal", new CalcCell(19.5, "mm")), ("upper", new CalcCell(0.02, "mm")), ("lower", new CalcCell(-0.02, "mm"))),
            ]),
            CalcInputValue.Quantity("minimum", 0.40, "mm"),
            CalcInputValue.Quantity("maximum", 0.80, "mm"),
            CalcInputValue.Of("k", 3),
            CalcInputValue.Of("n_mc", 4_000),
            CalcInputValue.Of("seed", 11),
        ]);
        var sheet = CalcRunner.Run(new ToleranceStackCalculator(), inputs);
        Assert.False(sheet.IsBlocked);

        var engine = ToleranceStackEngine.Analyse(Housing(), new StackRequirement(0.40, 0.80), 1.5);
        var mc = ToleranceStackEngine.Simulate(Housing(), 4_000, 11, new StackRequirement(0.40, 0.80));
        const double mm = 1e-3;

        Assert.Equal(engine.Mean * mm, sheet.OutputSi("x_mean"), 12);
        Assert.Equal(engine.WorstCase * mm, sheet.OutputSi("t_wc"), 12);
        Assert.Equal(engine.Rss * mm, sheet.OutputSi("t_rss"), 12);
        Assert.Equal(engine.ModifiedRss * mm, sheet.OutputSi("t_mrss"), 12);
        Assert.Equal(engine.Sigma * mm, sheet.OutputSi("sigma"), 12);
        Assert.Equal(engine.Capability!.Cpk, sheet.OutputSi("cpk"), 9);
        Assert.Equal(engine.Capability.Cp!.Value, sheet.OutputSi("cp"), 9);
        Assert.Equal(engine.MarginToMinimum!.Value * mm, sheet.OutputSi("m_min"), 12);
        Assert.Equal(mc.Mean * mm, sheet.OutputSi("mc_mean"), 12);
        Assert.Equal(mc.StandardDeviation * mm, sheet.OutputSi("mc_sigma"), 12);
    }

    // ---- properties -----------------------------------------------------------------------

    private static readonly Gen<Contributor> AnyContributor = Gen.Select(Gen.Double[-100, 100], Gen.Double[0.001, 1], Gen.Double[0.001, 1], Gen.OneOf(Gen.Const(1.0), Gen.Const(-1.0)), Gen.Int[0, 2])
        .Select((nominal, up, down, sign, kind) =>
            Contributor.Size("c", sign, nominal, up, -down, distribution: (ContributorDistribution)kind));

    [Fact]
    public void Property_Worst_Case_Is_Never_Below_Rss_Which_Is_Never_Below_The_Largest_Single_Band()
    {
        AnyContributor.List[1, 12].Sample(list =>
        {
            var r = ToleranceStackEngine.Analyse(list);
            return r.WorstCase + 1e-12 >= r.Rss && r.Rss + 1e-12 >= list.Max(x => x.Half);
        });
    }

    [Fact]
    public void Property_The_Order_Of_The_Contributors_Does_Not_Change_The_Answer()
    {
        AnyContributor.List[2, 10].Sample(list =>
        {
            var a = ToleranceStackEngine.Analyse(list);
            var reversed = ToleranceStackEngine.Analyse([.. list.AsEnumerable().Reverse()]);
            return Math.Abs(a.Mean - reversed.Mean) <= 1e-9 && Math.Abs(a.WorstCase - reversed.WorstCase) <= 1e-9
                && Math.Abs(a.Rss - reversed.Rss) <= 1e-9 && Math.Abs(a.Sigma - reversed.Sigma) <= 1e-9;
        });
    }

    [Fact]
    public void Property_Shares_Always_Total_A_Hundred_Unless_The_Stack_Has_No_Band()
    {
        AnyContributor.List[1, 12].Sample(list =>
        {
            var r = ToleranceStackEngine.Analyse(list);
            return r.WorstCase == 0 || Math.Abs(r.WorstCaseShare.Sum() - 100) <= 1e-9;
        });
    }

    [Fact]
    public void Property_The_Same_Seed_Always_Reproduces_The_Monte_Carlo()
    {
        Gen.Select(AnyContributor.List[1, 6], Gen.ULong).Sample((list, seed) =>
        {
            var a = ToleranceStackEngine.Simulate(list, 200, seed);
            var b = ToleranceStackEngine.Simulate(list, 200, seed);
            return a.Mean == b.Mean && a.StandardDeviation == b.StandardDeviation && a.Median == b.Median;
        }, iter: 40);
    }
}
