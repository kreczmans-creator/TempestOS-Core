using CsCheck;
using Tempest.Calc.Engine.Values;
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
    public void The_National_Annex_Factor_Set_Needs_A_Record_Pinned_On_Every_Factor_And_Prints_Its_Provenance()
    {
        // DC-07 S5 (owner decision DC-21). With "National Annex record" chosen and no record applied, the sheet refuses in one sentence naming factor_set.
        var chosen = CalcRunner.Run(Combos, With(CalcInputValue.Choice("factor_set", LoadCombinationsCalculator.FactorSetNationalAnnex)));
        Assert.True(chosen.IsBlocked);
        var block = Assert.Single(chosen.Blocks);
        Assert.Equal("factor-set", block.RuleId);
        Assert.StartsWith("factor_set:", block.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(block.Message));
        Assert.Single(block.Message.TrimEnd('.').Split(". "));
        Assert.Contains("psi", block.Message, StringComparison.Ordinal);

        // A pin on some of the factors only still refuses, naming the ones without it.
        var pin = CalcProvenance.FromReference($"record:{Guid.NewGuid():N}@2 · Table NA.A1.2(B)") with { Revision = "2002+A1:2005, NA 2009" };
        var partly = CalcRunner.Run(Combos, With(CalcInputValue.Choice("factor_set", LoadCombinationsCalculator.FactorSetNationalAnnex), CalcInputValue.Of("gamma_G_sup", 1.35, pin), CalcInputValue.Of("gamma_Q", 1.5, pin)));
        Assert.True(partly.IsBlocked);
        Assert.Contains("gamma_G_inf, xi, psi", partly.Blocks[0].Message, StringComparison.Ordinal);
        Assert.DoesNotContain("gamma_Q", partly.Blocks[0].Message, StringComparison.Ordinal);

        // Every factor and the ψ table pinned to a record: the sheet runs, prints the set name and the record's provenance (its table or clause and edition), and the
        // values are the record's (the numbers here are invented; nothing from a National Annex is typed).
        var psi = CalcInputValue.Table("psi", [CalcRow.Of(("category", new CalcCell(Text: "B office")), ("psi_0", new CalcCell(0.6)), ("psi_1", new CalcCell(0.4)), ("psi_2", new CalcCell(0.2)))], pin);
        var pinned = CalcRunner.Run(Combos, With(
            CalcInputValue.Choice("factor_set", LoadCombinationsCalculator.FactorSetNationalAnnex), CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610ab),
            CalcInputValue.Of("gamma_G_sup", 1.4, pin), CalcInputValue.Of("gamma_G_inf", 1.0, pin), CalcInputValue.Of("gamma_Q", 1.6, pin), CalcInputValue.Of("xi", 0.9, pin), psi));
        Assert.False(pinned.IsBlocked, string.Join("; ", pinned.Blocks.Select(b => b.Message)));
        var set = Assert.IsType<TextValue>(pinned.FindOutput("factor_set_used")!.Value).Value;
        Assert.StartsWith("National Annex record", set, StringComparison.Ordinal);
        Assert.Contains("Table NA.A1.2(B)", set, StringComparison.Ordinal);
        Assert.Contains("2002+A1:2005, NA 2009", set, StringComparison.Ordinal);
        Assert.DoesNotContain("record:", set, StringComparison.Ordinal);
        Assert.Contains(pinned.RunAssumptions, a => a.Statement.StartsWith("Factor set:", StringComparison.Ordinal) && a.Statement.Contains("Table NA.A1.2(B)", StringComparison.Ordinal));
        // 6.10a: 1.4×10 + 1.6×0.6×5 = 18.8; 6.10b: 0.9×1.4×10 + 1.6×5 = 20.6 governs.
        Assert.Equal(20.6, pinned.OutputSi("E_d_max") / 1e3, 9);

        // "EN recommended" (the default) runs as before, pinned or not, and names its set.
        var recommended = CalcRunner.Run(Combos, With());
        Assert.False(recommended.IsBlocked);
        Assert.StartsWith("EN recommended", Assert.IsType<TextValue>(recommended.FindOutput("factor_set_used")!.Value).Value, StringComparison.Ordinal);
        Assert.Equal(21, recommended.OutputSi("E_d_max") / 1e3, 9);
        var recommendedPinned = CalcRunner.Run(Combos, With(CalcInputValue.Of("xi", 0.9, pin)));
        Assert.False(recommendedPinned.IsBlocked);
    }

    [Fact]
    public void A_Calculation_Saved_Before_The_Factor_Set_Existed_Reads_As_EN_Recommended()
    {
        // Saved data compatibility (brief C-2): an input set with no factor_set, as every calculation saved on 1.2.0 has, gives today's values and the EN recommended set.
        var old = new CalcInputSet([LoadCombinationsCalculator.Actions(("Dead load", LoadCombinationsCalculator.Permanent, 10), ("Office imposed", "B office", 5)), CalcInputValue.Of("xi", 0.925), CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610ab)]);
        Assert.Null(old.Find("factor_set"));
        var r = CalcRunner.Run(Combos, old);
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(LoadCombinationsCalculator.FactorSetRecommended, Combos.Template.FindInput("factor_set")!.DefaultChoice);
        Assert.StartsWith("EN recommended", Assert.IsType<TextValue>(r.FindOutput("factor_set_used")!.Value).Value, StringComparison.Ordinal);
        // 6.10a: 1.35×10 + 1.5×0.7×5 = 18.75; 6.10b: 0.925×1.35×10 + 1.5×5 = 19.9875 governs (the typed ξ is used, as before).
        Assert.Equal(19.9875, r.OutputSi("E_d_max") / 1e3, 9);
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
        var (combinations, notes) = LoadCombinationsCalculator.FrameCombinations(inputs);
        var rows = combinations.Rows!;
        Assert.Single(notes);
        Assert.Contains(rows, r => r.Get("name")!.Text == "C1" && r.Get("case")!.Number == 1 && r.Get("factor")!.Number == 1.35);
        Assert.Contains(rows, r => r.Get("name")!.Text == "C1" && r.Get("case")!.Number == 2 && r.Get("factor")!.Number == 1.5);

        var frame = new FrameCalculator();
        var model = CalcTemplateHasher.DefaultInputs(frame.Template)
            .With(FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportPin), (2, 4, 0, FrameCalculator.SupportRollerY)))
            .With(FrameCalculator.Elements((1, 1, 2, 50, 5000)))
            .With(FrameCalculator.Loads((1, "point", 1, 2, 0, 0, 0, 0, -10, 0), (2, "point", 1, 2, 0, 0, 0, 0, -5, 0)))
            .With(combinations);
        var solved = CalcRunner.Run(frame, model);
        Assert.False(solved.IsBlocked, string.Join("; ", solved.Blocks.Select(b => b.Message)));
        Assert.Equal(21e3 * 4 / 4, Math.Max(Math.Abs(solved.OutputSi("M_max_env")), Math.Abs(solved.OutputSi("M_min_env"))), 3);
    }

    [Fact]
    public void Frame_Combinations_Report_What_They_Leave_Out()
    {
        var many = LoadCombinationsCalculator.Actions([("G", LoadCombinationsCalculator.Permanent, 10), .. Enumerable.Range(1, 30).Select(i => ($"Q{i}", "B office", i % 2 == 0 ? 1.0 : -1.0))]);
        var (_, notes) = LoadCombinationsCalculator.FrameCombinations(With(many));
        Assert.Contains(notes, n => n.Contains("left out", StringComparison.Ordinal));

        var psi = CalcInputValue.Table("psi", [CalcRow.Of(("category", new CalcCell(Text: "B office")), ("psi_0", new CalcCell(0.7)), ("psi_1", new CalcCell(0.5)), ("psi_2", new CalcCell(0.3)))]);
        var (_, missing) = LoadCombinationsCalculator.FrameCombinations(With(psi, LoadCombinationsCalculator.Actions(("G", LoadCombinationsCalculator.Permanent, 10), ("W", "wind", 4))));
        Assert.Contains(missing, n => n.Contains("no ψ factors", StringComparison.Ordinal));
    }
}
