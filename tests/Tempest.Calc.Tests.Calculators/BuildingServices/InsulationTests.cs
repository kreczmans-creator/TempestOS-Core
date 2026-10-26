using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class InsulationTests
{
    private static readonly InsulationCalculator Insulation = new();

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The new-sheet defaults: a 100 mm pipe at 80 °C in 20 °C air, λ 0.04, h_se 10, a 25 W/m limit, 40 mm chosen (no surface-temperature limit, no dew-point margin).</summary>
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
        var r = Run(Sheet(Q("theta_se_max", 45, "degC")));
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
        var solved = 0;
        Gen.Select(Gen.Double[0.02, 0.1], Gen.Double[5, 25], Gen.Double[15, 90], Gen.Double[60, 400]).Sample((lambda, h, qMax, di) =>
        {
            var solve = Sheet(Q("lambda", lambda, "W/(m*K)"), Q("h_se", h, "W/(m^2*K)"), Q("q_max_m", qMax, "W/m"), Q("D_i", di, "mm"), Q("theta_se_max", 60, "degC"), Clear("t_chosen"));
            var first = Run(solve);

            if (first.IsBlocked)
                return;

            solved++;
            var t = first.OutputSi("thickness");
            var again = Run(solve.With(CalcInputValue.Quantity("t_chosen", t * 1000, "mm")));
            Assert.False(again.IsBlocked);
            Assert.Equal(1.0, again.Checks.Max(c => c.Utilisation), 6);
        }, iter: 60);

        Assert.True(solved >= 50, $"only {solved} of 60 samples solved");
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
    public void Black_B_B_04_A_Fixed_H_Se_On_A_Cold_Pipe_Warns_That_The_Correlation_Needs_More_Insulation_Against_Condensation()
    {
        // The chilled-water slip: the generic 10 W/(m²K) holds the surface nearest the air temperature, the most favourable
        // case for condensation, so a layer that passes at h_se 10 can fail with the real (low-emissivity) surface.
        CalcInputSet Cold(params CalcInputValue[] more) => Sheet([Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 70, "%"), Q("dew_margin", 1, "K"), Clear("q_max_m"), Clear("t_chosen"), .. more]);

        var given = Run(Cold());
        Assert.False(given.IsBlocked, string.Join("; ", given.Blocks.Select(b => b.Message)));
        var tGiven = given.OutputSi("thickness") * 1000;
        Assert.Equal(11.290660460665531, tGiven, 6);

        double Correlation(double eps) => Run(Cold(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation), CalcInputValue.Of("eps", eps))).OutputSi("thickness") * 1000;
        var ordinary = Correlation(0.9);
        var bright = Correlation(0.1);
        Assert.True(bright > ordinary && ordinary > tGiven, $"{tGiven} {ordinary} {bright}");

        var warning = Assert.Single(given.Warnings, w => w.RuleId == "h-se-condensation");
        Assert.StartsWith("h_se:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("under-sizes", warning.Message, StringComparison.Ordinal);
        Assert.Contains("correlation", warning.Message, StringComparison.Ordinal);
        Assert.Contains($"{Tempest.Calc.Engine.Values.CalcValue.FormatNumber(tGiven, 5)} mm", warning.Message, StringComparison.Ordinal);
        Assert.Contains($"{Tempest.Calc.Engine.Values.CalcValue.FormatNumber(ordinary, 5)} mm", warning.Message, StringComparison.Ordinal);
        Assert.Contains($"{Tempest.Calc.Engine.Values.CalcValue.FormatNumber(bright, 5)} mm", warning.Message, StringComparison.Ordinal);

        // With a chosen thickness (the 13 mm layer of the finding) the warning still shows, next to the PASS it qualifies.
        var chosen = Run(Sheet(Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 70, "%"), Q("dew_margin", 1, "K"), Clear("q_max_m"), Q("t_chosen", 13, "mm")));
        Assert.True(chosen.Passed);
        Assert.True(Warned(chosen, "h-se-condensation"));

        // The correlation, a hot pipe, no dew margin and a fixed h_se that is already low raise no such warning.
        Assert.False(Warned(Run(Cold(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation))), "h-se-condensation"));
        Assert.False(Warned(Run(Sheet()), "h-se-condensation"));
        Assert.False(Warned(Run(Sheet(Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Clear("q_max_m"), Q("t_chosen", 20, "mm"))), "h-se-condensation"));
        Assert.False(Warned(Run(Cold(Q("h_se", 3, "W/(m^2*K)"))), "h-se-condensation"));
        Assert.Contains(Insulation.Template.ApplicabilityRules, r => r.Id == "h-se-condensation" && r.Severity == CalcNoticeSeverity.Warning);
        Assert.Contains("condensation", Insulation.Template.FindInput("h_se")!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Sub_Zero_Air_With_The_Dew_Point_From_RH_Warns_That_RH_Is_Read_Over_Ice()
    {
        // bs.insulation-01: -30 C surface in -10 C air, 90 % RH, margin 0. RH read over ice gives -11.179 C (63.9 mm); a weather-station 90 % over water is a frost point of -10.100 C
        // (independent python: 610.5 exp(17.269 t/(237.3 + t)), inverse over ice), 1 K away, and needs far more.
        var flat = Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Q("theta_i", -30, "degC"), Q("theta_a", -10, "degC"), Q("RH_a", 90, "%"), Q("dew_margin", 0, "K"), Clear("q_max_m"), Clear("t_chosen"));
        var cold = Run(flat);
        var warning = Assert.Single(cold.Warnings, w => w.RuleId == "rh-over-ice");
        Assert.Contains("-10.1", warning.Message, StringComparison.Ordinal);
        Assert.Equal(-11.178796330412547 + 273.15, cold.OutputSi("theta_dew"), 9);

        // Above 0 C, with the dew point entered (as the cold-pipe pack links it from B2), or without a dew-point margin, no such warning.
        Assert.False(Warned(Run(flat.With(Q("theta_i", 5, "degC")).With(Q("theta_a", 25, "degC"))), "rh-over-ice"));
        Assert.False(Warned(Run(flat.With(Q("theta_dew", -11.2, "degC"))), "rh-over-ice"));
        Assert.False(Warned(Run(flat.With(Clear("dew_margin")).With(Q("q_max_A", 20, "W/m^2"))), "rh-over-ice"));
        Assert.Contains(Insulation.Template.ApplicabilityRules, r => r.Id == "rh-over-ice" && r.Severity == CalcNoticeSeverity.Warning);
    }

    [Fact]
    public void Black_B_B_08_The_Condensation_Warning_Judges_A_Fixed_H_Se_Against_The_Correlation_At_The_Entered_Emissivity()
    {
        // The chilled-water worked example: h_se 8 and ε 0.9. The given value needs 13.16 mm, the correlation at ε 0.9 only 12.03 mm, so the
        // given value is already conservative for this surface and must not warn (it did, against the bright-cladding 25.18 mm).
        var chilled = WorkedExamples.Get(InsulationCalculator.Id + "/chilled-water-pipe-condensation").Inputs;
        var run = Run(chilled);
        Assert.False(run.IsBlocked);
        Assert.Equal(13.161558769442829, run.OutputSi("t_dew") * 1000, 6);
        Assert.False(Warned(run, "h-se-condensation"));

        double Correlation(double eps) => Run(chilled.With(CalcInputValue.Choice("h_mode", InsulationCalculator.HCorrelation)).With(CalcInputValue.Of("eps", eps))).OutputSi("t_dew") * 1000;
        Assert.True(Correlation(0.9) < 13.1615 && Correlation(0.1) > 25, $"{Correlation(0.9)} {Correlation(0.1)}");

        // The same h_se on a bright surface (ε 0.1 entered) is not conservative: it warns, and says which emissivity it judged against.
        var bright = Run(chilled.With(CalcInputValue.Of("eps", 0.1)));
        var warning = Assert.Single(bright.Warnings, w => w.RuleId == "h-se-condensation");
        Assert.Contains("ε = 0.1,", warning.Message, StringComparison.Ordinal);
        Assert.Contains($"{Tempest.Calc.Engine.Values.CalcValue.FormatNumber(Correlation(0.1), 5)} mm", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bright cladding", warning.Message, StringComparison.Ordinal);

        // A lower h_se is the conservative direction (the surface sits nearer the pipe): no warning at ε 0.9.
        Assert.False(Warned(Run(chilled.With(Q("h_se", 5, "W/(m^2*K)"))), "h-se-condensation"));

        // A higher h_se than the surface gives holds the surface too near the air: it warns at ε 0.9 too, and then adds the bright-cladding thickness as a note.
        var high = Run(chilled.With(Q("h_se", 15, "W/(m^2*K)")));
        var highWarning = Assert.Single(high.Warnings, w => w.RuleId == "h-se-condensation");
        Assert.Contains("ε = 0.9 (bright cladding, ε = 0.1, would need", highWarning.Message, StringComparison.Ordinal);
        Assert.Contains($"{Tempest.Calc.Engine.Values.CalcValue.FormatNumber(Correlation(0.9), 5)} mm", highWarning.Message, StringComparison.Ordinal);

        // The emissivity input says it is read for this judgement.
        Assert.Contains("only judges", Insulation.Template.FindInput("eps")!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Criterion_For_The_Wrong_Sign_Is_Ignored_With_A_Warning_And_No_Criterion_At_All_Blocks()
    {
        // A hot-surface limit on a cold pipe is ignored; the heat-flow limit still applies.
        var cold = Run(Sheet(Q("theta_i", 5, "degC"), Q("q_max_m", 30, "W/m"), Q("theta_se_max", 45, "degC")));
        Assert.False(cold.IsBlocked);
        Assert.True(Warned(cold, "criterion-na"));

        // A cold sheet with no dew-point margin says that condensation is not checked.
        Assert.True(Warned(cold, "no-condensation-check"));
        Assert.StartsWith("dew_margin:", cold.Warnings.Single(w => w.RuleId == "no-condensation-check").Message, StringComparison.Ordinal);
        Assert.False(Warned(Run(Sheet(Q("theta_i", 5, "degC"), Q("dew_margin", 1, "K"))), "no-condensation-check"));
        Assert.False(Warned(Run(Sheet()), "no-condensation-check"));
        Assert.False(Warned(Run(Sheet()), "criterion-na"));

        var none = Run(Sheet(Q("theta_i", 5, "degC"), Clear("q_max_m"), Clear("t_chosen")));
        Assert.True(none.IsBlocked);
        Assert.Equal("criteria", none.Blocks[0].RuleId);
        Assert.StartsWith("q_max_m:", none.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("enter", none.Blocks[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_Unattainable_Limit_Blocks_Only_When_No_Thickness_Is_Chosen()
    {
        var tiny = Run(Sheet(Q("q_max_m", 0.01, "W/m"), Clear("t_chosen")));
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
        var none = Run(Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Clear("theta_se_max"), Clear("t_chosen")));
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

    // ------------------------------------------------------------------ review fixes (1.1.0)

    [Fact]
    public void With_A_Thickness_Chosen_An_Unattainable_Criterion_Warns_Shows_Over_1000_Mm_And_Fails_Instead_Of_Blocking()
    {
        // A 3 m pipe: 1 m of insulation still loses 29 W/m, over a 20 W/m limit; the 45 °C surface limit is easy.
        var big = Run(Sheet(Q("D_i", 3000, "mm"), Q("q_max_m", 20, "W/m"), Q("theta_se_max", 45, "degC"), Q("t_chosen", 100, "mm")));
        Assert.False(big.IsBlocked, string.Join("; ", big.Blocks.Select(b => b.Message)));
        Assert.True(Warned(big, "unattainable"));
        Assert.Equal("> 1000 mm", ((Tempest.Calc.Engine.Values.TextValue)big.FindOutput("t_q")!.Value).Value);
        Assert.Equal("> 1000 mm", ((Tempest.Calc.Engine.Values.TextValue)big.FindOutput("thickness")!.Value).Value);
        Assert.True(big.OutputSi("t_se") > 0);
        Assert.Contains(big.Checks, c => c.Name.Contains("heat-loss limit", StringComparison.Ordinal) && !c.Passed);
        Assert.Contains(big.Checks, c => c.Name.Contains("surface-temperature limit", StringComparison.Ordinal) && c.Passed);
        Assert.False(big.Passed);

        // The surface-temperature and dew-point criteria behave the same way.
        var surface = Run(Sheet(Q("theta_se_max", 20.0001, "degC"), Clear("q_max_m")));
        Assert.False(surface.IsBlocked);
        Assert.True(Warned(surface, "unattainable"));
        Assert.StartsWith("theta_se_max:", surface.Warnings.Single(w => w.RuleId == "unattainable").Message, StringComparison.Ordinal);

        var dew = Run(Sheet(Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 99.99, "%"), Q("dew_margin", 0, "K"), Clear("q_max_m")));
        Assert.False(dew.IsBlocked);
        Assert.StartsWith("dew_margin:", dew.Warnings.Single(w => w.RuleId == "unattainable").Message, StringComparison.Ordinal);
        Assert.Contains(dew.Checks, c => c.Name.Contains("anti-condensation margin", StringComparison.Ordinal) && !c.Passed);

        // With no thickness chosen there is nothing to evaluate, so the sheet blocks.
        Assert.Equal("unattainable", Run(Sheet(Q("theta_se_max", 20.0001, "degC"), Clear("q_max_m"), Clear("t_chosen"))).Blocks[0].RuleId);
    }

    [Fact]
    public void A_Chosen_Thickness_With_No_Criterion_Reports_The_Heat_Flow_And_Surface_Temperature_With_A_Warning()
    {
        var r = Run(Sheet(Clear("q_max_m"), Clear("theta_se_max")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.True(Warned(r, "no-criteria"));
        Assert.Null(r.FindOutput("thickness"));
        Assert.Empty(r.Checks);

        var expected = InsulationModel.Evaluate(new InsulationModel.Setup(true, 0.1, 0.04, 80, 20, 0, new InsulationModel.Surface(10, 0, 0, 1)), 0.04);
        Assert.Equal(expected.Q, r.OutputSi("q_per_m"), 9);
        Assert.Equal(expected.Ts, r.OutputSi("theta_se") - 273.15, 9);
        Assert.NotNull(r.FindOutput("theta_m"));
        Assert.NotNull(r.FindOutput("D_crit"));
        Assert.Contains("chosen thickness only", ((Tempest.Calc.Engine.Values.TextValue)r.FindOutput("governing")!.Value).Value, StringComparison.Ordinal);

        // Both blank still blocks.
        Assert.Equal("criteria", Run(Sheet(Clear("q_max_m"), Clear("theta_se_max"), Clear("t_chosen"))).Blocks[0].RuleId);
    }

    [Fact]
    public void A_Limit_The_Bare_Pipe_Meets_But_A_Window_Of_Thin_Layers_Breaks_Is_Said_To_Be_So()
    {
        // D_i 10 mm, λ 0.2, h 10: bare 18.85 W/m ≤ 25, layers from 2.76 mm to 74.29 mm exceed it.
        var r = Run(Sheet(Q("D_i", 10, "mm"), Q("lambda", 0.2, "W/(m*K)"), Q("q_max_m", 25, "W/m"), Clear("t_chosen")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(74.29172044012402, r.OutputSi("thickness") * 1000, 6);
        var note = r.Warnings.Single(w => w.RuleId == "bare-complies").Message;
        Assert.StartsWith("q_max_m:", note, StringComparison.Ordinal);
        Assert.Contains("bare surface also meets", note, StringComparison.Ordinal);
        Assert.Contains("2.7646", note, StringComparison.Ordinal);
        Assert.Contains("74.292", note, StringComparison.Ordinal);

        // A monotone case does not say it.
        Assert.False(Warned(Run(Sheet()), "bare-complies"));

        // The model returns the window's start.
        var setup = new InsulationModel.Setup(true, 0.01, 0.2, 80, 20, 0, new InsulationModel.Surface(10, 0, 0, 1));
        var (outcome, t, from) = InsulationModel.RequiredThickness(setup, InsulationModel.Scan(setup), st => Math.Abs(st.Q) - 25);
        Assert.Equal(InsulationModel.SearchOutcome.Found, outcome);
        Assert.Equal(0.07429172044012402, t, 9);
        Assert.Equal(0.0027646038552035495, from!.Value, 9);
    }

    [Fact]
    public void A_Dew_Point_Entered_Replaces_The_Magnus_Value_And_Ignores_The_Humidity()
    {
        var cold = Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("dew_margin", 1, "K"), Clear("q_max_m"), Clear("theta_se_max"), Clear("t_chosen"));
        var entered = Run(cold.With(Q("theta_dew", 20, "degC")).With(Q("RH_a", 30, "%")));
        Assert.False(entered.IsBlocked, string.Join("; ", entered.Blocks.Select(b => b.Message)));
        Assert.Equal(20, entered.OutputSi("theta_dew") - 273.15, 9);
        Assert.Equal(16, entered.OutputSi("thickness") * 1000, 6);

        var magnus = Run(cold.With(Q("RH_a", 30, "%")));
        Assert.True(magnus.OutputSi("thickness") < entered.OutputSi("thickness"));

        var impossible = Run(cold.With(Q("theta_dew", 24.5, "degC")));
        Assert.Equal("dew", impossible.Blocks[0].RuleId);
        Assert.StartsWith("theta_dew:", impossible.Blocks[0].Message, StringComparison.Ordinal);
        Assert.Contains("entered", impossible.Blocks[0].Message, StringComparison.Ordinal);

        // Computed from the humidity, the block still points to the margin and humidity.
        var humid = Run(cold.With(Q("RH_a", 100, "%")));
        Assert.StartsWith("dew_margin:", humid.Blocks[0].Message, StringComparison.Ordinal);

        // An entered dew point on a hot sheet is ignored, with a warning.
        var hot = Run(Sheet(Q("theta_dew", 12, "degC")));
        Assert.False(hot.IsBlocked);
        Assert.StartsWith("theta_dew:", hot.Warnings.Single(w => w.RuleId == "criterion-na").Message, StringComparison.Ordinal);
        Assert.False(Warned(Run(Sheet()), "criterion-na"));
    }

    [Fact]
    public void A_Flat_Surface_Has_Its_Own_Port_And_Says_Heat_Gain_For_A_Cold_One()
    {
        var t = Insulation.Template;
        Assert.Contains(t.OutputPorts!, p => p is { Symbol: "q_area", Unit: "W/m^2" });

        var cold = Run(Sheet(CalcInputValue.Choice("geometry", InsulationCalculator.Flat), Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("q_max_A", 15, "W/m^2"), Clear("q_max_m"), Clear("theta_se_max"), Clear("t_chosen")));
        Assert.False(cold.IsBlocked, string.Join("; ", cold.Blocks.Select(b => b.Message)));
        Assert.Contains("heat gain", cold.FindOutput("q_area")!.Description, StringComparison.Ordinal);
        Assert.Equal(15, cold.OutputSi("q_area"), 6);
        Assert.Null(cold.FindOutput("q_per_m"));

        // A cold pipe's heat-flow criterion is a heat-gain limit; a hot one's is a heat-loss limit.
        Assert.Contains(Run(Sheet(Q("theta_i", 5, "degC"), Q("q_max_m", 30, "W/m"))).Checks, c => c.Name.Contains("heat-gain limit", StringComparison.Ordinal));
        Assert.Contains(Run(Sheet()).Checks, c => c.Name.Contains("heat-loss limit", StringComparison.Ordinal));
    }

    [Fact]
    public void A_Warning_Names_The_Required_Thickness_When_None_Was_Chosen_And_The_Mean_Temperature_Starts_At_The_Insulation_Face()
    {
        // No chosen thickness: any critical-radius line about the evaluated thickness does not start with t_chosen.
        var r = Run(Sheet(Q("D_i", 10, "mm"), Q("lambda", 0.2, "W/(m*K)"), Q("theta_se_max", 25, "degC"), Clear("q_max_m"), Clear("t_chosen")));
        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.DoesNotContain(r.Warnings, w => w.Message.StartsWith("t_chosen:", StringComparison.Ordinal));

        var wall = Run(Sheet(Q("h_i", 500, "W/(m^2*K)"), Q("t_w", 3, "mm"), Q("lambda_w", 50, "W/(m*K)")));
        var face = 80 - wall.OutputSi("q_per_m") * wall.OutputSi("R_oth");
        Assert.Equal((face + wall.OutputSi("theta_se") - 273.15) / 2, wall.OutputSi("theta_m") - 273.15, 9);
        Assert.Contains("bare-surface", wall.FindOutput("D_crit")!.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Flat_Plate_Convection_Is_Continuous_Across_The_Laminar_To_Turbulent_Switch_And_The_Dew_Point_Uses_The_Ice_Branch_Below_Zero()
    {
        var surface = new InsulationModel.Surface(null, 0.9, 10, 1);
        var film = (30 + 273.15 + 20 + 273.15) / 2;
        var switchLength = 5e5 * InsulationModel.AirKinematicViscosity(film) / 10;
        var below = surface.Coefficients(30, 20, switchLength * 0.999, false);
        var above = surface.Coefficients(30, 20, switchLength * 1.001, false);
        Assert.InRange(above.Convection / below.Convection, 0.99, 1.01);

        var ice = GlaserModel.DewPoint(0.9 * GlaserModel.SaturationPressure(-10));
        Assert.Equal(-11.178796330412547, ice, 9);
    }

    // ------------------------------------------------------------------ the template

    [Fact]
    public void The_Conductivity_Comes_From_A_W3_Material_And_The_Ports_Are_Declared()
    {
        var t = Insulation.Template;
        Assert.Equal("lambda", t.Inputs.Single(i => i.MaterialProperty is not null).MaterialProperty);
        Assert.Equal(["lambda"], t.RecordKinds!.Single(k => k.Kind == InsulationCalculator.MaterialKind).Symbols);
        Assert.Equal(["thickness", "q_per_m", "q_area", "theta_se"], t.OutputPorts!.Select(p => p.Symbol));

        // A pipe sheet gives every port except the flat one.
        var r = Run(Sheet());
        Assert.All(t.OutputPorts!.Where(p => p.Symbol != "q_area"), p => Assert.NotNull(r.FindOutput(p.Symbol)));
    }

    [Fact]
    public void The_Surface_Coefficient_And_Heat_Loss_Limit_Defaults_Are_Labelled_Generic_And_No_Text_Says_No_Value_Ships()
    {
        // h_se opens on 10 W/(m²K) and q_max_m on 25 W/m: the defaults stay, and their wording says they are generic values the user replaces.
        var h = Insulation.Template.Inputs.Single(i => i.Symbol == "h_se");
        var q = Insulation.Template.Inputs.Single(i => i.Symbol == "q_max_m");
        Assert.Equal(10, h.Default);
        Assert.Equal(25, q.Default);
        Assert.Contains("generic default", h.Description, StringComparison.Ordinal);
        Assert.Contains("replace", h.Description, StringComparison.Ordinal);
        Assert.Contains("generic default", q.Description, StringComparison.Ordinal);
        Assert.Contains("replace", q.Description, StringComparison.Ordinal);
        Assert.DoesNotContain(Insulation.Template.RecordKinds!, k => k.Guidance.Contains("No value is shipped", StringComparison.Ordinal));
        Assert.Contains("generic 10 W/(m²K)", Insulation.Template.RecordKinds!.Single(k => k.Kind == "surface.coefficient").Guidance, StringComparison.Ordinal);
    }
}
