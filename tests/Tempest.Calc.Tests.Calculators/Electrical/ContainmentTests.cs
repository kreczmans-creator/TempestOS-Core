using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using static Tempest.Calc.Electrical.ContainmentCalculator;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>E8 — containment fill, bend radius and cable pulling: behaviour beyond the known-answer cases.</summary>
public sealed class ContainmentTests
{
    private static readonly ContainmentCalculator Cont = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Cont.Template);

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Cont, overrides.Aggregate(Defaults(), (set, value) => set.With(value)));

    private static string Direction(CalcRunResult r) => ((TextValue)r.FindOutput("direction")!.Value).Value;

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void Three_Ten_Millimetre_Cables_In_A_Twenty_Five_Millimetre_Conduit_Fill_48_Percent_And_Are_Checked_Against_The_Factor()
    {
        var r = Run();
        Assert.Equal(0.48, r.OutputSi("fill"), 12);
        Assert.Equal(0.96, r.OutputSi("u_fill"), 12);

        var at = Run(CalcInputValue.Of("f_max", 0.48));
        var over = Run(CalcInputValue.Of("f_max", 0.40));
        Assert.Contains(at.Checks, k => k.Name.StartsWith("Fill", StringComparison.Ordinal) && k.Passed);
        Assert.Contains(over.Checks, k => k.Name.StartsWith("Fill", StringComparison.Ordinal) && !k.Passed);
    }

    [Fact]
    public void Fill_Is_The_Sum_Of_n_d_Squared_Over_D_Squared_For_Any_Cables_In_A_Conduit()
    {
        // Property: fill = Σ n_i·d_i² / D² (the π/4 cancels), for any cables and conduit.
        Gen.Select(Gen.Double[60, 120], Gen.Double[4, 12], Gen.Int[1, 4], Gen.Double[4, 12], Gen.Int[1, 4]).Sample((conduit, d1, n1, d2, n2) =>
        {
            var r = Run(Cables(("A", d1, n1, 0.1, 100), ("B", d2, n2, 0.1, 100)), Q("D_c", conduit, "mm"), CalcInputValue.Of("f_max", 1));
            var expected = (n1 * d1 * d1 + n2 * d2 * d2) / (conduit * conduit);
            return Math.Abs(r.OutputSi("fill") - expected) <= 1e-9 * expected;
        }, iter: 80);
    }

    [Fact]
    public void A_Single_Bend_Follows_The_Capstan_Equation_And_Both_Directions_Agree()
    {
        Gen.Select(Gen.Double[0.05, 0.8], Gen.Double[0, 2000], Gen.Double[10, 360], Gen.Double[100, 2000]).Sample((mu, start, degrees, radius) =>
        {
            var r = Run(Route(RB(degrees, radius)), Q("T_1", start, "N"), CalcInputValue.Of("mu", mu), CalcInputValue.Of("n_bend", 1));
            var expected = start * Math.Exp(mu * degrees * Math.PI / 180);
            return Math.Abs(r.OutputSi("T_max_a") - expected) <= 1e-9 * Math.Max(expected, 1)
                && Math.Abs(r.OutputSi("T_max_b") - expected) <= 1e-9 * Math.Max(expected, 1)
                && Math.Abs(r.OutputSi("P_max") - expected / (radius / 1000)) <= 1e-9 * Math.Max(expected / (radius / 1000), 1);
        }, iter: 80);
    }

    [Fact]
    public void Reversing_The_Route_Swaps_The_Two_Directions()
    {
        Gen.Select(Gen.Double[2, 40], Gen.Double[2, 40], Gen.Double[2, 40], Gen.Double[0.2, 0.7], Gen.Double[0, 300]).Sample((l1, l2, l3, mu, start) =>
        {
            var forward = Run(Route(RS(l1), RB(90, 400), RS(l2), RB(45, 600), RS(l3)), CalcInputValue.Of("mu", mu), Q("T_1", start, "N"));
            var backward = Run(Route(RS(l3), RB(45, 600), RS(l2), RB(90, 400), RS(l1)), CalcInputValue.Of("mu", mu), Q("T_1", start, "N"));
            return Math.Abs(forward.OutputSi("T_max_a") - backward.OutputSi("T_max_b")) <= 1e-9 * forward.OutputSi("T_max_a")
                && Math.Abs(forward.OutputSi("T_max_b") - backward.OutputSi("T_max_a")) <= 1e-9 * forward.OutputSi("T_max_b")
                && Math.Abs(forward.OutputSi("max_tension") - backward.OutputSi("max_tension")) <= 1e-9 * forward.OutputSi("max_tension");
        }, iter: 60);
    }

    [Fact]
    public void The_Lower_Tension_Direction_Is_Recommended_In_Pull_Order_And_A_Straight_Route_Has_No_Preference()
    {
        var r = Run();
        Assert.True(r.OutputSi("T_max_b") < r.OutputSi("T_max_a"));
        Assert.Equal(r.OutputSi("T_max_b"), r.OutputSi("max_tension"), 12);
        Assert.StartsWith("Reverse: pull from segment 5 to segment 1", Direction(r), StringComparison.Ordinal);

        var flipped = Run(Route(RS(8), RB(90, 300), RS(18), RB(90, 400), RS(12)));
        Assert.StartsWith("As listed: pull from segment 1 to segment 5", Direction(flipped), StringComparison.Ordinal);
        Assert.Equal(r.OutputSi("max_tension"), flipped.OutputSi("max_tension"), 12);

        // No bends: T = T₁ + w·g·μ·L with w = 0.6 kg/m, the same both ways, and no sidewall pressure.
        var straight = Run(Route(RS(10), RS(15)), CalcInputValue.Of("mu", 0.4), Q("T_1", 50, "N"));
        Assert.Equal(50 + 0.6 * 9.80665 * 0.4 * 25, straight.OutputSi("max_tension"), 9);
        Assert.Equal(straight.OutputSi("T_max_a"), straight.OutputSi("T_max_b"), 9);
        Assert.StartsWith("Either direction", Direction(straight), StringComparison.Ordinal);
        Assert.Null(straight.FindOutput("P_max"));
        Assert.DoesNotContain(straight.Checks, k => k.Name.StartsWith("Sidewall", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Bend_Tighter_Than_The_Largest_Cables_Multiple_Fails_And_One_At_It_Passes()
    {
        var tight = Run(Route(RS(5), RB(90, 100)), CalcInputValue.Of("mu", 0.3));
        Assert.Equal(1.2, tight.OutputSi("u_bend"), 12);
        Assert.Contains(tight.Checks, k => k.Name.StartsWith("Bend radius", StringComparison.Ordinal) && !k.Passed);

        var exact = Run(Route(RS(5), RB(90, 120)), CalcInputValue.Of("mu", 0.3));
        Assert.Equal(1.0, exact.OutputSi("u_bend"), 12);
        Assert.Contains(exact.Checks, k => k.Name.StartsWith("Bend radius", StringComparison.Ordinal) && k.Passed);

        var mixed = Run(Cables(("Small", 6, 4, 0.1, 100), ("Large", 14, 1, 0.5, 300)), CalcInputValue.Of("n_bend", 10));
        Assert.Equal(0.14, mixed.OutputSi("R_min"), 12);
    }

    [Fact]
    public void Missing_Or_Unusable_Inputs_Block_And_Say_What_To_Enter()
    {
        var noFriction = CalcRunner.Run(Cont, Defaults().With(new CalcInputValue("mu")));
        Assert.Contains(noFriction.Blocks, b => b.Message.StartsWith("mu: enter", StringComparison.Ordinal));

        var noConduit = CalcRunner.Run(Cont, Defaults().With(new CalcInputValue("D_c")));
        Assert.Contains(noConduit.Blocks, b => b.Message.StartsWith("D_c: enter", StringComparison.Ordinal));

        var noTray = Run(CalcInputValue.Choice("cont_type", Trunking));
        Assert.Contains(noTray.Blocks, b => b.Message.StartsWith("W_c: enter", StringComparison.Ordinal));

        var fractional = Run(CalcInputValue.Table("cables", [CalcRow.Of(("od", new CalcCell(10, "mm")), ("count", new CalcCell(2.5)), ("mass", new CalcCell(0.2, "kg/m")))]));
        Assert.Contains(fractional.Blocks, b => b.Message.StartsWith("cables[1].count: enter", StringComparison.Ordinal));

        // 50 bends of 720° at μ = 2 run past any cable; the sheet refuses it rather than showing infinity.
        var runaway = Run(Route([.. Enumerable.Repeat(RB(720, 100), 50)]), CalcInputValue.Of("mu", 2), Q("T_1", 1000, "N"));
        Assert.Contains(runaway.Blocks, b => b.Message.StartsWith("route: ", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Cable_Limit_Is_N_Times_The_Weakest_Cable_Not_The_Sum_Of_The_Limits()
    {
        // H1: 2 × 700 N and 1 × 150 N cables share the pull equally; the 150 N cable carries T/3 and governs.
        var mixed = Run(Cables(("Power", 12, 2, 0.6, 700), ("Control", 8, 1, 0.15, 150)), CalcInputValue.Choice("cont_type", Trunking), Q("W_c", 100, "mm"), Q("H_c", 50, "mm"),
            CalcInputValue.Of("n_bend", 8), Route(RS(20), RB(90, 500), RS(30), RB(90, 600), RS(10)), Q("T_1", 100, "N"), CalcInputValue.Of("mu", 0.4));
        Assert.Equal(450, mixed.OutputSi("T_cab"), 9);
        Assert.Null(mixed.FindOutput("T_lim"));
        Assert.Equal(mixed.OutputSi("max_tension") / 450, mixed.OutputSi("u_T"), 12);
        Assert.Equal(mixed.OutputSi("max_tension") / 3 / 150, mixed.Steps.Single(st => st.Symbol == "u_Tc2").Result is ScalarValue sv ? sv.Si : double.NaN, 12);
        Assert.True(mixed.OutputSi("u_T") > 2);
        Assert.Contains(mixed.Checks, k => k.Name.StartsWith("Pulling tension against the weakest", StringComparison.Ordinal) && !k.Passed);

        // Identical cables: the same as the old sum.
        Assert.Equal(1200, Run().OutputSi("T_cab"), 9);
    }

    [Fact]
    public void A_Derating_For_More_Than_Three_Cables_And_A_Separate_Grip_Limit_Apply_Only_When_Entered()
    {
        var six = Cables(("Control", 8, 6, 0.1, 200));
        Assert.Equal(1200, Run(six).OutputSi("T_cab"), 9);
        Assert.Equal(960, Run(six, CalcInputValue.Of("k_n", 0.8)).OutputSi("T_cab"), 9);

        // Three cables or fewer ignore the derating.
        Assert.Equal(1200, Run(CalcInputValue.Of("k_n", 0.5)).OutputSi("T_cab"), 9);

        var grip = Run(Q("T_grip", 100, "N"));
        Assert.Equal(grip.OutputSi("max_tension") / 100, grip.OutputSi("u_grip"), 12);
        Assert.Contains(grip.Checks, k => k.Name.Contains("grip", StringComparison.Ordinal) && !k.Passed);
        Assert.Null(Run().FindOutput("u_grip"));
    }

    [Fact]
    public void A_Zero_Or_Missing_Cable_Limit_Is_Refused_Or_Warned_Never_Silently_Skipped()
    {
        var zero = Run(CalcInputValue.Table("cables", [CalcRow.Of(("od", new CalcCell(10, "mm")), ("count", new CalcCell(3)), ("mass", new CalcCell(0.2, "kg/m")), ("T_max", new CalcCell(0, "N")))]));
        Assert.True(zero.IsBlocked);

        var missing = Run(CalcInputValue.Table("cables", [CalcRow.Of(("od", new CalcCell(10, "mm")), ("count", new CalcCell(3)), ("mass", new CalcCell(0.2, "kg/m")))]));
        Assert.False(missing.IsBlocked);
        Assert.Contains(missing.Notices, n => n.RuleId == "T_cab" && n.Severity == CalcNoticeSeverity.Warning);
        Assert.DoesNotContain(missing.Checks, k => k.Name.StartsWith("Pulling tension", StringComparison.Ordinal));

        // No route: the fill is worked, there is no tension port and nothing blocks; route-only values warn that they are unused.
        var fillOnly = Run(CalcInputValue.Table("route", []), Q("T_grip", 500, "N"));
        Assert.False(fillOnly.IsBlocked);
        Assert.NotNull(fillOnly.FindOutput("fill"));
        Assert.Null(fillOnly.FindOutput("max_tension"));
        Assert.Contains(fillOnly.Notices, n => n.RuleId == "route-limits" && n.Message.StartsWith("T_grip", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Direction_That_Passes_Every_Check_Is_Recommended_Even_With_Higher_Tension()
    {
        // As listed: 1166.9 N (lower) but 1716 N/m on the 600 mm bend against a 1500 N/m limit; reversed: 1281.1 N and 1010 N/m.
        var r = Run(Cables(("Single", 20, 1, 1.0, 5000)), Q("D_c", 100, "mm"), CalcInputValue.Of("n_bend", 6),
            Route(RS(20), RB(90, 1200), RS(40), RB(180, 600), RS(40)), CalcInputValue.Of("mu", 0.35), Q("T_1", 50, "N"), Q("P_lim", 1500, "N/m"));
        Assert.Equal(1166.9420719154186, r.OutputSi("T_max_a"), 8);
        Assert.Equal(1281.1417720539894, r.OutputSi("T_max_b"), 8);
        Assert.Equal(1281.1417720539894, r.OutputSi("max_tension"), 8);
        Assert.Equal(1010.4126850449912, r.OutputSi("P_max"), 8);
        Assert.StartsWith("Reverse: pull from segment 5 to segment 1", Direction(r), StringComparison.Ordinal);
        Assert.Contains("only direction that passes every check", Direction(r), StringComparison.Ordinal);
        Assert.All(r.Checks, k => Assert.True(k.Passed, k.Name));

        // With no sidewall limit, the lower tension wins again.
        var free = Run(Cables(("Single", 20, 1, 1.0, 5000)), Q("D_c", 100, "mm"), CalcInputValue.Of("n_bend", 6),
            Route(RS(20), RB(90, 1200), RS(40), RB(180, 600), RS(40)), CalcInputValue.Of("mu", 0.35), Q("T_1", 50, "N"));
        Assert.StartsWith("As listed", Direction(free), StringComparison.Ordinal);
    }

    [Fact]
    public void A_Cable_That_Does_Not_Fit_The_Containment_Fails_Whatever_The_Fill()
    {
        // One 25 mm cable in a 200 × 20 mm tray: fill is only 12 %, but the cable is deeper than the tray.
        var tray = Run(Cables(("Big", 25, 1, 1.0, 1000)), CalcInputValue.Choice("cont_type", Trunking), Q("W_c", 200, "mm"), Q("H_c", 20, "mm"), CalcInputValue.Table("route", []));
        Assert.True(tray.OutputSi("u_fill") < 1);
        Assert.Contains(tray.Checks, k => k.Name.StartsWith("Largest cable fits", StringComparison.Ordinal) && !k.Passed);
        Assert.False(tray.Passed);

        var conduit = Run(Cables(("Big", 25, 1, 1.0, 1000)), Q("D_c", 25, "mm"), CalcInputValue.Table("route", []));
        Assert.Contains(conduit.Checks, k => k.Name.StartsWith("Largest cable fits", StringComparison.Ordinal) && !k.Passed);

        Assert.Contains(Run().Checks, k => k.Name.StartsWith("Largest cable fits", StringComparison.Ordinal) && k.Passed);
    }

    [Fact]
    public void Three_Equal_Cables_Near_The_Jam_Ratio_Warn()
    {
        // 3 × 10 mm: D/d = 3.0 in a 30 mm conduit may jam; 2.5 (25 mm) and 3.5 (35 mm) do not warn.
        Assert.Contains(Run(Q("D_c", 30, "mm")).Notices, n => n.RuleId == "jam");
        Assert.DoesNotContain(Run().Notices, n => n.RuleId == "jam");
        Assert.DoesNotContain(Run(Q("D_c", 35, "mm")).Notices, n => n.RuleId == "jam");
    }

    [Fact]
    public void The_Weight_Correction_Factor_Scales_The_Straight_Term_Only()
    {
        // w = 0.6 kg/m, μ 0.4, 25 m straight, T₁ 50 N: 50 + 2 × 0.6 × 9.80665 × 0.4 × 25 = 167.6798 N with w_c = 2.
        var one = Run(Route(RS(10), RS(15)), CalcInputValue.Of("mu", 0.4), Q("T_1", 50, "N"));
        var two = Run(Route(RS(10), RS(15)), CalcInputValue.Of("mu", 0.4), Q("T_1", 50, "N"), CalcInputValue.Of("w_c", 2));
        Assert.Equal(167.6798, two.OutputSi("max_tension"), 9);
        Assert.Equal(50 + (one.OutputSi("max_tension") - 50) * 2, two.OutputSi("max_tension"), 9);

        var bend = Run(Route(RB(90, 400)), CalcInputValue.Of("mu", 0.4), Q("T_1", 50, "N"), CalcInputValue.Of("w_c", 2));
        Assert.Equal(50 * Math.Exp(0.4 * Math.PI / 2), bend.OutputSi("max_tension"), 9);
    }

    [Fact]
    public void The_Ports_Are_Fill_And_Maximum_Tension_And_No_Factor_Value_Ships_As_A_Default()
    {
        var r = Run();
        Assert.Equal(["fill", "max_tension"], Cont.Template.OutputPorts!.Select(p => p.Symbol));
        Assert.All(Cont.Template.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));

        Assert.Contains(Cont.Template.RecordKinds!, k => k.Kind == "containment.space-factor" && k.Symbols.Contains("f_max"));

        foreach (var symbol in new[] { "f_max", "n_bend", "mu", "P_lim", "T_grip", "k_n" })
            Assert.Null(Cont.Template.Inputs.Single(i => i.Symbol == symbol).Default);
    }
}
