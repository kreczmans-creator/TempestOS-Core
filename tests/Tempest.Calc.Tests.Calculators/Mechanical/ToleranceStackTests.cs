using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class ToleranceStackTests
{
    private static readonly ToleranceStackCalculator Calculator = new();

    private static CalcInputValue Contributors(string unit, params (string Name, string Direction, double Nominal, double Upper, double Lower)[] rows) =>
        CalcInputValue.Table("contributors", rows.Select(r => CalcRow.Of(
            ("name", new CalcCell(Text: r.Name)), ("direction", new CalcCell(Text: r.Direction)),
            ("nominal", new CalcCell(r.Nominal, unit)), ("upper", new CalcCell(r.Upper, unit)), ("lower", new CalcCell(r.Lower, unit)))).ToList());

    private static CalcInputSet Housing(double? min = 0.40, double? max = 0.80) => new(new[]
    {
        Contributors("mm", ("Housing bore depth", "adds", 40, 0.10, 0), ("Bearing width", "subtracts", 20, 0, -0.12), ("Spacer", "subtracts", 19.5, 0.02, -0.02)),
        min is { } lo ? CalcInputValue.Quantity("minimum", lo, "mm") : null,
        max is { } hi ? CalcInputValue.Quantity("maximum", hi, "mm") : null,
    }.OfType<CalcInputValue>());

    [Fact]
    public void Migrated_Housing_Example_Matches_The_V016_Vectors()
    {
        var r = CalcRunner.Run(Calculator, Housing());
        Assert.False(r.IsBlocked);
        Assert.Equal(0.5e-3, r.OutputSi("x_nom"), 12);
        Assert.Equal(0.61e-3, r.OutputSi("x_mean"), 12);
        Assert.Equal(0.13e-3, r.OutputSi("t_wc"), 12);
        Assert.Equal(0.48e-3, r.OutputSi("x_wc_min"), 12);
        Assert.Equal(0.74e-3, r.OutputSi("x_wc_max"), 12);
        Assert.Equal(0.080622577e-3, r.OutputSi("t_rss"), 10);
        Assert.Equal(0.026874192e-3, r.OutputSi("sigma"), 10);
        Assert.Equal(2.3566600, r.OutputSi("cpk"), 5);
        Assert.InRange(r.OutputSi("dpmo"), 7.5e-7, 8.0e-7);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Worst_Case_Failure_Is_Reported_With_Margins()
    {
        var r = CalcRunner.Run(Calculator, Housing(min: 0.55));
        Assert.False(r.Passed);
        Assert.Equal(-0.07e-3, r.OutputSi("m_min") , 12);
        Assert.Equal(0.74420841, r.OutputSi("cpk"), 5);
        Assert.Contains(r.Checks, c => c.Name == "Worst case within requirement" && !c.Passed);
    }

    [Fact]
    public void Without_A_Requirement_Capability_Is_Not_Computed_And_A_Warning_Says_So()
    {
        var r = CalcRunner.Run(Calculator, Housing(null, null));
        Assert.Null(r.FindOutput("cpk"));
        Assert.Contains(r.Warnings, w => w.RuleId == "no-requirement");
        Assert.NotNull(r.FindOutput("t_rss"));
    }

    [Fact]
    public void Modified_Rss_Is_The_Factor_Times_Rss()
    {
        var r = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Of("f_rss", 1.5)));
        Assert.Equal(1.5 * r.OutputSi("t_rss"), r.OutputSi("t_mrss"), 12);
    }

    [Fact]
    public void Uniform_And_Triangular_Contributors_Change_Sigma_Not_Worst_Case()
    {
        var normal = CalcRunner.Run(Calculator, Housing());
        var rows = Housing().Find("contributors")!.Rows!.Select(r => new CalcRow(new Dictionary<string, CalcCell>(r.Cells) { ["distribution"] = new CalcCell(Text: "uniform") })).ToList();
        var uniform = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Table("contributors", rows)));
        Assert.Equal(normal.OutputSi("t_wc"), uniform.OutputSi("t_wc"), 12);
        Assert.Equal(normal.OutputSi("t_rss"), uniform.OutputSi("t_rss"), 12);
        Assert.True(uniform.OutputSi("sigma") > normal.OutputSi("sigma"));
        Assert.Equal(Math.Sqrt(3.0 * 3 / 3), uniform.OutputSi("sigma") / normal.OutputSi("sigma"), 6);
    }

    [Fact]
    public void Mean_Shift_Moves_The_Mean_Only()
    {
        var rows = Housing().Find("contributors")!.Rows!.Select((r, i) => i == 0 ? new CalcRow(new Dictionary<string, CalcCell>(r.Cells) { ["shift"] = new CalcCell(0.01, "mm") }) : r).ToList();
        var shifted = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Table("contributors", rows)));
        var normal = CalcRunner.Run(Calculator, Housing());
        Assert.Equal(normal.OutputSi("x_mean") + 0.01e-3, shifted.OutputSi("x_mean"), 12);
        Assert.Equal(normal.OutputSi("t_wc"), shifted.OutputSi("t_wc"), 12);
    }

    [Fact]
    public void Position_Contributor_With_Bonus_Adds_Half_The_Zone_Plus_Half_The_Bonus()
    {
        var rows = Housing().Find("contributors")!.Rows!.ToList();
        rows.Add(CalcRow.Of(("name", new CalcCell(Text: "Hole position")), ("direction", new CalcCell(Text: "adds")), ("nominal", new CalcCell(0, "mm")), ("upper", new CalcCell(0.2, "mm")), ("kind", new CalcCell(Text: "position")), ("bonus", new CalcCell(0.1, "mm"))));
        var r = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Table("contributors", rows)));
        Assert.Equal(0.13e-3 + 0.15e-3, r.OutputSi("t_wc"), 12);
        Assert.Equal(0.61e-3, r.OutputSi("x_mean"), 12);
        Assert.Contains(r.Steps, s => s.Symbol == "c4" && s.Formula.Contains("position zone 0.2 Ⓜ + bonus 0.1", StringComparison.Ordinal));
    }

    [Fact]
    public void Asme_Notation_Changes_Labels_Only()
    {
        var bs = CalcRunner.Run(Calculator, Housing());
        var asme = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Choice("notation", "ASME Y14.5")));
        Assert.Equal(bs.OutputSi("t_wc"), asme.OutputSi("t_wc"));
        Assert.Contains(asme.Steps, s => s.Symbol == "c1" && s.Formula.Contains("40 +0.1/0", StringComparison.Ordinal));
        Assert.Contains(bs.Steps, s => s.Symbol == "c3" && s.Formula.Contains("19.5 ±0.02", StringComparison.Ordinal));
    }

    [Fact]
    public void Monte_Carlo_Agrees_With_Rss_And_Is_Reproducible()
    {
        var a = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Of("n_mc", 20_000)).With(CalcInputValue.Of("seed", 7)));
        var b = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Of("n_mc", 20_000)).With(CalcInputValue.Of("seed", 7)));
        Assert.Equal(a.OutputSi("mc_mean"), b.OutputSi("mc_mean"));
        Assert.Equal(a.OutputSi("x_mean"), a.OutputSi("mc_mean"), 6);
        Assert.Equal(a.OutputSi("sigma"), a.OutputSi("mc_sigma"), 6);
        Assert.InRange(a.OutputSi("mc_yield"), 99.9, 100.0);
        Assert.Contains(a.Series, s => s.Name == "Monte Carlo histogram" && s.Points.Count == 30);
        Assert.Contains(a.Series, s => s.Name == "Contribution, statistical" && Math.Abs(s.Points.Sum(p => p.Y) - 100) < 1e-9);
    }

    [Fact]
    public void Monte_Carlo_Sample_Cap_Is_A_Validation_Block()
    {
        var r = CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Of("n_mc", 5_000_000)));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("n_mc", StringComparison.Ordinal));
    }

    [Fact]
    public void Lower_Above_Upper_Blocks_With_The_Row_Named()
    {
        var r = CalcRunner.Run(Calculator, Housing().With(Contributors("mm", ("Bad", "adds", 10, -0.1, 0.1))));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.Contains("row 1 (Bad)", StringComparison.Ordinal));
    }

    [Fact]
    public void Minimum_Above_Maximum_Blocks() => Assert.Contains(CalcRunner.Run(Calculator, Housing(min: 0.9, max: 0.4)).Blocks, b => b.RuleId == "min-above-max");

    [Fact]
    public void Too_Many_Rows_Block() =>
        Assert.True(CalcRunner.Run(Calculator, Housing().With(CalcInputValue.Table("contributors", Enumerable.Range(0, 201).Select(_ => Housing().Find("contributors")!.Rows![0]).ToList()))).IsBlocked);

    [Fact]
    public void Property_Worst_Case_Is_Never_Tighter_Than_Rss_And_Scales_With_Unit()
    {
        Gen.Select(Gen.Int[1, 8], Gen.Double[0.001, 0.5], Gen.Double[1, 100]).Sample((count, tol, nominal) =>
        {
            var rows = Enumerable.Range(0, count).Select(i => (Name: $"p{i}", Direction: i % 2 == 0 ? "adds" : "subtracts", Nominal: nominal, Upper: tol, Lower: -tol)).ToArray();
            var mm = CalcRunner.Run(Calculator, new CalcInputSet([Contributors("mm", rows)]));
            var inch = CalcRunner.Run(Calculator, new CalcInputSet([Contributors("in", rows)]));
            var wc = mm.OutputSi("t_wc");
            var rss = mm.OutputSi("t_rss");
            return wc >= rss - 1e-15 && Math.Abs(wc - count * tol * 1e-3) < 1e-12 && Math.Abs(inch.OutputSi("t_wc") - count * tol * 0.0254) < 1e-12;
        }, iter: 100);
    }
}
