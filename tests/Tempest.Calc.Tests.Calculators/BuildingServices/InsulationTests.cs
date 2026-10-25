using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class InsulationTests
{
    private static readonly InsulationCalculator Insulation = new();

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The new-sheet defaults: a 100 mm pipe at 80 °C in 20 °C air, λ 0.04, h_se 10, 25 W/m and 45 °C limits, 40 mm chosen.</summary>
    private static CalcInputSet Sheet(params CalcInputValue[] changes)
    {
        var inputs = CalcTemplateHasher.NewSheetInputs(Insulation.Template);

        foreach (var change in changes)
            inputs = inputs.With(change);

        return inputs;
    }

    /// <summary>The input emptied, so the optional input does not apply.</summary>
    private static CalcInputValue Clear(string symbol) => new(symbol);

    private static CalcRunResult Run(CalcInputSet inputs) => CalcRunner.Run(Insulation, inputs);

    private static bool Warned(CalcRunResult r, string rule) => r.Warnings.Any(w => w.RuleId == rule);

    // ------------------------------------------------------------------ physics

    [Fact]
    public void The_New_Sheet_Passes_At_40_Mm_And_Needs_37_3_Mm_For_The_Heat_Loss_Limit()
    {
        var r = Run(Sheet());
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(37.30359728106675, r.OutputSi("thickness") * 1000, 6);
        Assert.Equal(5.325913504887262, r.OutputSi("t_se") * 1000, 6);
        Assert.True(r.Passed);
        Assert.Equal("heat-loss limit", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("governing")!.Value).Value);
    }

    [Fact]
    public void Evaluating_At_The_Required_Thickness_Puts_The_Governing_Criterion_At_Its_Limit()
    {
        // A property: solve for the thickness, then evaluate there; the governing utilisation is 1.
        Gen.Select(Gen.Double[0.02, 0.1], Gen.Double[5, 25], Gen.Double[15, 90], Gen.Double[60, 400]).Sample((lambda, h, qMax, di) =>
        {
            var solve = Sheet(Q("lambda", lambda, "W/(m*K)"), Q("h_se", h, "W/(m^2*K)"), Q("q_max_m", qMax, "W/m"), Q("D_i", di, "mm"), Q("theta_se_max", 60, "degC"), Clear("t_chosen"));
            var first = Run(solve);

            if (first.IsBlocked)
                return;

            var t = first.OutputSi("thickness");
            var again = Run(solve.With(CalcInputValue.Quantity("t_chosen", t * 1000, "mm")));
            Assert.False(again.IsBlocked);
            Assert.Equal(1.0, again.Checks.Max(c => c.Utilisation), 6);
            Assert.All(again.Checks, c => Assert.True(c.Passed || c.Utilisation < 1 + 1e-6));
        }, iter: 60);
    }

    [Fact]
    public void More_Insulation_On_A_Large_Pipe_Never_Raises_The_Heat_Flow_Or_The_Surface_Temperature()
    {
        // D_i of 100 mm or more is above the critical diameter 2λ/h_se (at most 2·0.1/5 = 40 mm here).
        Gen.Select(Gen.Double[0.02, 0.1], Gen.Double[5, 25], Gen.Double[100, 800], Gen.Double[-150, 500], Gen.Double[0.0, 0.3], Gen.Double[0.0, 0.3]).Sample((lambda, h, di, ti, a, b) =>
        {
            const double ta = 20;

            if (Math.Abs(ti - ta) < 1)
                return;

            var setup = new InsulationModel.Setup(true, di / 1000, lambda, ti, ta, 0, new InsulationModel.Surface(h, 0, 0, 1));
            var thin = InsulationModel.Evaluate(setup, Math.Min(a, b));
            var thick = InsulationModel.Evaluate(setup, Math.Max(a, b));
            Assert.True(Math.Abs(thick.Q) <= Math.Abs(thin.Q) * (1 + 1e-12));
            Assert.True(Math.Abs(thick.Ts - ta) <= Math.Abs(thin.Ts - ta) * (1 + 1e-12) + 1e-12);
        }, iter: 100);
    }

    [Fact]
    public void A_Small_Pipe_Below_The_Critical_Diameter_Warns_That_Thin_Insulation_Raises_The_Loss()
    {
        // D_crit = 2·0.2/10 = 40 mm; a 10 mm pipe with 5 mm of insulation (D_e 20 mm) loses more than bare.
        var r = Run(Sheet(Q("D_i", 10, "mm"), Q("lambda", 0.2, "W/(m*K)"), Q("t_chosen", 5, "mm"), Q("q_max_m", 100, "W/m"), Clear("theta_se_max")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(40, r.OutputSi("D_crit") * 1000, 6);
        Assert.Equal(2, r.Warnings.Count(w => w.RuleId == "critical-radius"));
        Assert.Contains(r.Warnings, w => w.Message.StartsWith("t_chosen:", StringComparison.Ordinal));

        var bare = InsulationModel.Evaluate(new InsulationModel.Setup(true, 0.01, 0.2, 80, 20, 0, new InsulationModel.Surface(10, 0, 0, 1)), 0);
        var thin = InsulationModel.Evaluate(new InsulationModel.Setup(true, 0.01, 0.2, 80, 20, 0, new InsulationModel.Surface(10, 0, 0, 1)), 0.005);
        Assert.True(thin.Q > bare.Q);
    }

    [Fact]
    public void A_Heat_Loss_Limit_The_Bare_Pipe_Already_Meets_Needs_No_Insulation()
    {
        var r = Run(Sheet(Q("q_max_m", 400, "W/m"), Clear("theta_se_max"), Clear("t_chosen")));
        Assert.Equal(0, r.OutputSi("thickness"), 12);
        Assert.Equal("none: the bare surface complies", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("governing")!.Value).Value);
        Assert.Equal(188.49555921538757, r.OutputSi("q_per_m"), 6);
    }

    // ------------------------------------------------------------------ cold pipes and condensation

    [Fact]
    public void Anti_Condensation_Passes_At_The_Required_Thickness_And_Fails_Below_It()
    {
        var cold = Sheet(Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 70, "%"), Q("dew_margin", 1, "K"), Clear("q_max_m"), Clear("theta_se_max"), Clear("t_chosen"));
        var required = Run(cold).OutputSi("thickness") * 1000;
        Assert.Equal(11.290660460665531, required, 6);

        var pass = Run(cold.With(Q("t_chosen", required + 1, "mm")));
        Assert.True(pass.Passed);
        var fail = Run(cold.With(Q("t_chosen", required - 3, "mm")));
        Assert.False(fail.Passed);
        Assert.Contains(fail.Checks, c => c.Name.Contains("anti-condensation", StringComparison.Ordinal) && !c.Passed);

        // The dew point is the Magnus relation B7 uses.
        var dew = GlaserModel.DewPoint(0.7 * GlaserModel.SaturationPressure(25));
        Assert.Equal(dew, Run(cold).OutputSi("theta_dew") - 273.15, 9);
    }

    [Fact]
    public void A_Criterion_For_The_Wrong_Sign_Is_Ignored_With_A_Warning_And_No_Criterion_At_All_Blocks()
    {
        // A hot-surface limit on a cold pipe is ignored; the heat-flow limit still applies.
        var cold = Run(Sheet(Q("theta_i", 5, "degC"), Q("q_max_m", 30, "W/m")));
        Assert.False(cold.IsBlocked);
        Assert.True(Warned(cold, "criterion-na"));

        var none = Run(Sheet(Q("theta_i", 5, "degC"), Clear("q_max_m")));
        Assert.True(none.IsBlocked);
        Assert.Equal("criteria", none.Blocks[0].RuleId);
        Assert.StartsWith("q_max_m:", none.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("enter", none.Blocks[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_Unattainable_Limit_And_Impossible_Limits_Block_With_The_Symbol_First()
    {
        var tiny = Run(Sheet(Q("q_max_m", 0.01, "W/m")));
        Assert.True(tiny.IsBlocked);
        Assert.Equal("unattainable", tiny.Blocks[0].RuleId);
        Assert.StartsWith("q_max_m:", tiny.Blocks[0].Message, StringComparison.Ordinal);

        var below = Run(Sheet(Q("theta_se_max", 15, "degC")));
        Assert.Equal("surface-limit", below.Blocks[0].RuleId);

        var humid = Run(Sheet(Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 100, "%"), Q("dew_margin", 1, "K"), Clear("q_max_m"), Clear("theta_se_max")));
        Assert.Equal("dew", humid.Blocks[0].RuleId);
        Assert.StartsWith("dew_margin:", humid.Blocks[0].Message, StringComparison.Ordinal);

        var same = Run(Sheet(Q("theta_i", 20, "degC")));
        Assert.Equal("temperatures", same.Blocks[0].RuleId);
    }

    // ------------------------------------------------------------------ surface coefficient, wall and film

    [Fact]
    public void The_Correlation_Balances_Heat_At_The_Surface_And_Wind_Raises_h_se()
    {
        var still = Run(Sheet(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation), Q("t_chosen", 40, "mm")));
        Assert.False(still.IsBlocked, string.Join("; ", still.Blocks.Select(b => b.Message)));
        var h = still.OutputSi("h_se");
        var de = 0.1 + 0.08;
        var q = still.OutputSi("q_per_m");
        var ts = still.OutputSi("theta_se") - 273.15;
        Assert.Equal(q, h * Math.PI * de * (ts - 20), 6);
        Assert.Equal(h, still.OutputSi("h_c") + still.OutputSi("h_r"), 9);

        var windy = Run(Sheet(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation), Q("t_chosen", 40, "mm"), Q("v_wind", 5, "m/s")));
        Assert.True(windy.OutputSi("h_se") > h);
        Assert.True(windy.OutputSi("theta_se") < still.OutputSi("theta_se"));

        var shiny = Run(Sheet(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation), Q("t_chosen", 40, "mm"), CalcInputValue.Of("eps", 0.1)));
        Assert.True(shiny.OutputSi("h_r") < still.OutputSi("h_r") / 5);
    }

    [Fact]
    public void An_Inner_Film_And_A_Pipe_Wall_Add_Resistance_And_Need_Both_Wall_Values()
    {
        var plain = Run(Sheet());
        var withWall = Run(Sheet(Q("h_i", 500, "W/(m^2*K)"), Q("t_w", 3, "mm"), Q("lambda_w", 50, "W/(m*K)")));
        Assert.True(withWall.OutputSi("q_per_m") < plain.OutputSi("q_per_m"));
        Assert.True(withWall.OutputSi("thickness") < plain.OutputSi("thickness"));

        var half = Run(Sheet(Q("t_w", 3, "mm")));
        Assert.Equal("wall", half.Blocks[0].RuleId);
        Assert.StartsWith("lambda_w:", half.Blocks[0].Message, StringComparison.Ordinal);

        var thick = Run(Sheet(Q("t_w", 60, "mm"), Q("lambda_w", 50, "W/(m*K)")));
        Assert.Equal("wall", thick.Blocks[0].RuleId);
    }

    [Fact]
    public void A_Flat_Surface_Uses_Its_Own_Limit_And_The_Closed_Form()
    {
        var r = Run(Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Q("q_max_A", 20, "W/m^2"), Clear("q_max_m"), Clear("theta_se_max"), Clear("t_chosen")));
        Assert.Equal(116, r.OutputSi("thickness") * 1000, 6);
        Assert.Null(r.FindOutput("q_per_m"));
        Assert.Equal(20, r.OutputSi("q_area"), 6);

        // With no flat limit, the pipe's limit is not borrowed.
        var none = Run(Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Clear("theta_se_max")));
        Assert.Equal("criteria", none.Blocks[0].RuleId);
        Assert.StartsWith("q_max_A:", none.Blocks[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Mean_Temperature_Is_Noted_And_A_Far_Conductivity_Temperature_Warns()
    {
        var r = Run(Sheet(Q("theta_lambda", 10, "degC")));
        Assert.Equal((80 + r.OutputSi("theta_se") - 273.15) / 2, r.OutputSi("theta_m") - 273.15, 6);
        Assert.True(Warned(r, "lambda-temperature"));
        Assert.False(Warned(Run(Sheet(Q("theta_lambda", 52, "degC"))), "lambda-temperature"));
    }

    // ------------------------------------------------------------------ the template

    [Fact]
    public void The_Conductivity_Comes_From_A_W3_Material_And_The_Ports_Are_Declared()
    {
        var t = Insulation.Template;
        Assert.Equal("lambda", t.Inputs.Single(i => i.MaterialProperty is not null).MaterialProperty);
        Assert.Equal(["lambda"], t.RecordKinds!.Single(k => k.Kind == InsulationCalculator.MaterialKind).Symbols);
        Assert.Equal(["thickness", "q_per_m", "theta_se"], t.OutputPorts!.Select(p => p.Symbol));

        // Every declared port is a real output of the new-sheet run.
        var r = Run(Sheet());
        Assert.All(t.OutputPorts!, p => Assert.NotNull(r.FindOutput(p.Symbol)));
    }
}
