using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class LoadCombinationsTests
{
    private static readonly LoadCombinationsCalculator Combos = new();

    private static CalcInputSet With(params CalcInputValue[] values)
    {
        var set = CalcTemplateHasher.DefaultInputs(Combos.Template);

        foreach (var v in values)
            set = set.With(v);

        return set;
    }

    [Fact]
    public void Every_Variable_Action_Is_Tried_As_Leading()
    {
        var r = CalcRunner.Run(Combos, With(LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, 10), ("Q", "B office", 5), ("W", "wind", 4), ("S", "snow ≤ 1000 m", 2))));
        var uls = r.Steps.Where(s => s.Description?.StartsWith("ULS 6.10,", StringComparison.Ordinal) == true && s.Description.EndsWith("(max)", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, uls.Count);
    }

    [Fact]
    public void Envelope_Bounds_Every_Uls_Combination()
    {
        Gen.Select(Gen.Double[-50, 50], Gen.Double[-50, 50], Gen.Double[-50, 50]).Sample((g, q, w) =>
        {
            var r = CalcRunner.Run(Combos, With(LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, g), ("Q", "B office", q), ("W", "wind", w))));
            var max = r.OutputSi("E_d_max") / 1e3;
            var min = r.OutputSi("E_d_min") / 1e3;
            var worst = 1.35 * Math.Abs(g) + 1.5 * (Math.Abs(q) + Math.Abs(w));
            return min <= max + 1e-9 && max <= worst + 1e-6 && min >= -worst - 1e-6;
        }, iter: 60);
    }

    [Fact]
    public void Method_610ab_Never_Exceeds_610()
    {
        Gen.Select(Gen.Double[0, 50], Gen.Double[0, 50], Gen.Double[0, 50]).Sample((g, q, w) =>
        {
            var actions = LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, g), ("Q", "B office", q), ("W", "wind", w));
            var single = CalcRunner.Run(Combos, With(actions)).OutputSi("E_d_max");
            var pair = CalcRunner.Run(Combos, With(actions, CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610ab))).OutputSi("E_d_max");
            return pair <= single + 1e-9;
        }, iter: 40);
    }

    [Fact]
    public void K_FI_Scales_Every_Unfavourable_Action_But_Not_The_Favourable_Ones()
    {
        // EN 1990 Annex B, B3.3: K_FI multiplies the partial factors of unfavourable actions (Opus review, finding 3).
        var r = CalcRunner.Run(Combos, With(CalcInputValue.Of("K_FI", 1.1)));
        Assert.Equal(1.1 * (1.35 * 10 + 1.5 * 5), r.OutputSi("E_d_max") / 1e3, 9);
        Assert.Equal(1.0 * 10, r.OutputSi("E_d_min") / 1e3, 9);
    }

    [Fact]
    public void A_Psi_Record_Replaces_The_Recommended_Set_And_Must_Cover_Every_Category()
    {
        var psi = CalcInputValue.Table("psi", [CalcRow.Of(("category", new CalcCell(Text: "B office")), ("psi_0", new CalcCell(0.5)), ("psi_1", new CalcCell(0.4)), ("psi_2", new CalcCell(0.2)))]);
        var r = CalcRunner.Run(Combos, With(psi));
        Assert.Equal(10 + 0.2 * 5, r.OutputSi("E_qp_max") / 1e3, 9);

        var missing = CalcRunner.Run(Combos, With(psi, LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, 10), ("W", "wind", 4))));
        Assert.True(missing.IsBlocked);
        Assert.Equal("psi-missing", missing.Blocks[0].RuleId);
    }

    [Fact]
    public void Accidental_Combination_Appears_Only_With_An_Accidental_Action()
    {
        Assert.Null(CalcRunner.Run(Combos, With()).FindOutput("E_acc_max"));
        var r = CalcRunner.Run(Combos, With(LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, 10), ("Q", "B office", 5), ("Impact", LoadCombinationsCalculator.Accidental, 30))));
        Assert.Equal(10 + 30 + 0.5 * 5, r.OutputSi("E_acc_max") / 1e3, 9);
    }

    [Fact]
    public void An_Empty_Action_Table_Blocks()
    {
        var r = CalcRunner.Run(Combos, With(CalcInputValue.Table("actions", [])));
        Assert.True(r.IsBlocked);
    }

    [Fact]
    public void Combinations_Push_To_The_S2_Frame_As_Load_Cases()
    {
        // Opus review finding 8: each action row n is S2 load case n; the frame's envelope then equals S5's.
        var inputs = With(LoadCombinationsCalculator.Actions(("Dead", LoadCombinationsCalculator.Permanent, 10), ("Office", "B office", 5)));
        var rows = LoadCombinationsCalculator.FrameCombinations(inputs).Rows!;
        Assert.Contains(rows, r => r.Get("name")!.Text == "C1" && r.Get("case")!.Number == 1 && r.Get("factor")!.Number == 1.35);
        Assert.Contains(rows, r => r.Get("name")!.Text == "C1" && r.Get("case")!.Number == 2 && r.Get("factor")!.Number == 1.5);

        var frame = new FrameCalculator();
        var model = CalcTemplateHasher.DefaultInputs(frame.Template)
            .With(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 4, 0, FrameCalculator.SupportRollerY)))
            .With(FrameCalculator.Elements((1, 1, 2, 50, 5000)))
            .With(FrameCalculator.Loads((1, "point", 1, 2, 0, 0, 0, 0, -10, 0), (2, "point", 1, 2, 0, 0, 0, 0, -5, 0)))
            .With(LoadCombinationsCalculator.FrameCombinations(inputs));
        var solved = CalcRunner.Run(frame, model);
        Assert.False(solved.IsBlocked, string.Join("; ", solved.Blocks.Select(b => b.Message)));
        Assert.Equal(21e3 * 4 / 4, Math.Max(Math.Abs(solved.OutputSi("M_max_env")), Math.Abs(solved.OutputSi("M_min_env"))), 3);
    }
}
