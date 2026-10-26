using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>E1 1.1.0: continuous rotary duty (a pump or fan) beside the point-to-point move cycle.</summary>
public sealed class MotorContinuousDutyTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcInputSet Continuous(params CalcInputValue[] overrides)
    {
        var d = Motor.Template.TestCases[2].Inputs.ToDictionary(v => v.Symbol);

        foreach (var o in overrides)
            d[o.Symbol] = o;

        return new CalcInputSet([.. d.Values]);
    }

    [Fact]
    public void Continuous_Duty_Follows_The_Closed_Form_For_Any_Load()
    {
        Gen.Select(Gen.Double[0.1, 50], Gen.Double[50, 3000], Gen.Double[0.2, 20], Gen.Double[1, 10], Gen.Double[0.5, 1]).Sample((torque, rpm, tStart, ratio, eta) =>
        {
            var r = CalcRunner.Run(Motor, Continuous(CalcInputValue.Quantity("T_L", torque, "N*m"), CalcInputValue.Quantity("n_L", rpm, "rpm"), CalcInputValue.Quantity("t_start", tStart, "s"), CalcInputValue.Of("i", ratio), CalcInputValue.Of("eta", eta), CalcInputValue.Quantity("n_max", 1e6, "rpm")));
            Assert.False(r.IsBlocked);
            var omega = 2 * Math.PI * rpm * ratio / 60;
            var load = torque / (ratio * eta);
            var jTotal = 5e-4 + 0.01 / (ratio * ratio * eta);
            Assert.Equal(load, r.OutputSi("T_rms"), load * 1e-9);
            Assert.Equal(load, r.OutputSi("T_load"), load * 1e-9);
            Assert.Equal(jTotal * omega / tStart + load, r.OutputSi("T_pk"), r.OutputSi("T_pk") * 1e-9);
            Assert.Equal(load * omega, r.OutputSi("P_cont"), load * omega * 1e-9);
            Assert.Equal(rpm * ratio, r.OutputSi("n_pk") * 60 / (2 * Math.PI), rpm * ratio * 1e-9);
        }, iter: 80);
    }

    [Fact]
    public void The_Rated_Power_Is_The_Candidate_Motors_Rated_Torque_Times_Its_Rated_Speed_And_Not_The_Running_Speed()
    {
        // E1 1.4.0: P_rated = T_rated·ω_rated from the nameplate rated speed n_rated, for sizing a supply on the motor rather than the load.
        var r = CalcRunner.Run(Motor, Continuous());
        Assert.Equal(2.0 * 2 * Math.PI * 2850 / 60, r.OutputSi("P_rated"), 1e-9);
        Assert.Equal(596.9026042, r.OutputSi("P_rated"), 1e-6);
        Assert.True(r.OutputSi("P_rated") > r.OutputSi("P_cont"));
        Assert.DoesNotContain(r.Notices, n => n.RuleId == "rated-speed");
        Assert.Equal("W", Motor.Template.OutputPorts!.Single(p => p.Symbol == "P_rated").Unit);
        Assert.Equal("1.9.0", Motor.Template.Version);
        Assert.Contains(Motor.Template.RecordKinds!, k => k.Kind == "motor.rating" && k.Symbols.Contains("n_rated") && k.Symbols.Contains("T_rated"));

        // The running speed (2800 rpm) is not the rated speed: changing the load speed leaves the rated power alone.
        var slower = CalcRunner.Run(Motor, Continuous(CalcInputValue.Quantity("n_L", 500, "rpm")));
        Assert.Equal(r.OutputSi("P_rated"), slower.OutputSi("P_rated"), 1e-9);
        Assert.NotEqual(r.OutputSi("P_cont"), slower.OutputSi("P_cont"));

        Gen.Select(Gen.Double[0.1, 500], Gen.Double[50, 3000], Gen.Double[50, 5000]).Sample((rated, rpm, nameplate) =>
        {
            var run = CalcRunner.Run(Motor, Continuous(CalcInputValue.Quantity("T_rated", rated, "N*m"), CalcInputValue.Quantity("n_L", rpm, "rpm"), CalcInputValue.Quantity("n_rated", nameplate, "rpm"), CalcInputValue.Quantity("n_max", 1e6, "rpm"), CalcInputValue.Quantity("T_peak", 1e6, "N*m")));
            var expected = rated * 2 * Math.PI * nameplate / 60;
            Assert.Equal(expected, run.OutputSi("P_rated"), expected * 1e-9);
        }, iter: 60);

        // Without n_rated there is no P_rated, and the sheet says it needs the rated speed: the running speed is not substituted.
        var without = CalcRunner.Run(Motor, new CalcInputSet([.. Continuous().Values.Where(v => v.Symbol != "n_rated")]));
        Assert.False(without.IsBlocked);
        Assert.Null(without.FindOutput("P_rated"));
        var warning = Assert.Single(without.Notices, n => n.RuleId == "rated-speed");
        Assert.StartsWith("n_rated:", warning.Message, StringComparison.Ordinal);

        // A rated speed on a point-to-point sheet gives the rated power too (chain.motor-circuit-04; it was ignored, with a notice, before 1.9.0): T_rated 2 N·m × 1450 rpm; the notice is gone.
        var pointToPoint = CalcTemplateHasher.DefaultInputs(Motor.Template).With(CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyPointToPoint)).With(CalcInputValue.Quantity("n_rated", 1450, "rpm"));
        var pointToPointRun = CalcRunner.Run(Motor, pointToPoint);
        Assert.Equal(2.0 * 1450 * 2 * Math.PI / 60, pointToPointRun.OutputSi("P_rated"), 6);
        Assert.DoesNotContain(pointToPointRun.Notices, n => n.RuleId == "rated-speed-ignored");
        Assert.DoesNotContain(CalcRunner.Run(Motor, Continuous()).Notices, n => n.RuleId == "rated-speed-ignored");
        Assert.DoesNotContain(CalcRunner.Run(Motor, CalcTemplateHasher.DefaultInputs(Motor.Template).With(CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyPointToPoint)).With(new CalcInputValue("n_rated"))).Notices, n => n.RuleId == "rated-speed-ignored");

        // Point-to-point duty has no rated power without a rated speed.
        Assert.Null(CalcRunner.Run(Motor, CalcTemplateHasher.DefaultInputs(Motor.Template).With(CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyPointToPoint))).FindOutput("P_rated"));
    }

    [Fact]
    public void The_Move_Inputs_Are_Ignored_And_Said_To_Be()
    {
        var baseline = CalcRunner.Run(Motor, Continuous());
        var moved = CalcRunner.Run(Motor, Continuous(CalcInputValue.Of("s", 77), CalcInputValue.Quantity("t_move", 9, "s"), CalcInputValue.Of("f_acc", 0.4), CalcInputValue.Quantity("t_dwell", 123, "s"), CalcInputValue.Choice("profile", MotorSizingCalculator.ProfileSCurve)));
        Assert.Contains(baseline.Notices, n => n.RuleId == "continuous-ignored");

        foreach (var symbol in new[] { "T_rms", "T_pk", "n_pk", "P_cont", "u_rms" })
            Assert.Equal(baseline.OutputSi(symbol), moved.OutputSi(symbol), 1e-12);
    }

    [Fact]
    public void A_Missing_Speed_Run_Up_Inertia_Or_A_Linear_Mechanism_Is_Blocked_With_The_Symbol_First()
    {
        static CalcInputSet Without(string symbol) => new([.. Continuous().Values.Where(v => v.Symbol != symbol)]);

        foreach (var symbol in new[] { "n_L", "t_start", "J_L" })
        {
            var r = CalcRunner.Run(Motor, Without(symbol));
            Assert.True(r.IsBlocked, symbol);
            Assert.Contains(r.Blocks, b => b.RuleId == "continuous-input" && b.Message.StartsWith(symbol + ":", StringComparison.Ordinal));
        }

        var linear = CalcRunner.Run(Motor, Continuous(CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechScrew), CalcInputValue.Quantity("m", 5, "kg")));
        Assert.True(linear.IsBlocked);
        Assert.Contains(linear.Blocks, b => b.Message.StartsWith("duty_mode:", StringComparison.Ordinal));
    }

    [Fact]
    public void Point_To_Point_Is_The_Default_And_Its_Numbers_Are_Unchanged()
    {
        var withoutMode = CalcRunner.Run(Motor, new CalcInputSet(Motor.Template.TestCases[0].Inputs));
        var explicitMode = CalcRunner.Run(Motor, new CalcInputSet([.. Motor.Template.TestCases[0].Inputs, CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyPointToPoint)]));
        Assert.False(withoutMode.IsBlocked);

        foreach (var symbol in new[] { "T_pk", "T_rms", "n_pk", "J_refl", "T_acc" })
            Assert.Equal(withoutMode.OutputSi(symbol), explicitMode.OutputSi(symbol), 1e-12);

        Assert.Equal(2.5790, withoutMode.OutputSi("T_pk"), 2.579 * 1e-3);
        Assert.DoesNotContain(withoutMode.Outputs, o => o.Symbol == "P_cont");
        Assert.DoesNotContain(withoutMode.Notices, n => n.RuleId == "continuous-ignored");
    }

    [Fact]
    public void The_Ports_Offer_What_Other_Calculations_Link_To()
    {
        var ports = Motor.Template.OutputPorts!.Select(p => p.Symbol).ToHashSet();

        foreach (var symbol in new[] { "T_rms", "n_pk", "P_pk", "P_cont", "T_pk", "T_load" })
            Assert.Contains(symbol, ports);

        Assert.Equal("W", Motor.Template.OutputPorts!.Single(p => p.Symbol == "P_cont").Unit);
    }

    [Fact]
    public void A_Motor_Speed_Above_The_Rated_Speed_Warns_And_A_Speed_At_Or_Below_It_Does_Not()
    {
        // The pump case runs at 2800 rpm on a 2850 rpm nameplate: below rated, silent.
        Assert.DoesNotContain(CalcRunner.Run(Motor, Continuous()).Notices, n => n.RuleId == "speed-above-rated");

        // Exactly at the nameplate (n_L · i = n_rated, which floating point may land a hair above) is silent.
        Assert.DoesNotContain(CalcRunner.Run(Motor, Continuous(CalcInputValue.Quantity("n_L", 360, "rpm"), CalcInputValue.Quantity("n_rated", 1440, "rpm"))).Notices, n => n.RuleId == "speed-above-rated");

        // 3000 rpm against the 2850 rpm nameplate: past the nameplate point, so the sheet says so; the message names both speeds.
        var above = CalcRunner.Run(Motor, Continuous(CalcInputValue.Quantity("n_L", 750, "rpm")));
        Assert.False(above.IsBlocked);
        var warning = Assert.Single(above.Notices, n => n.RuleId == "speed-above-rated");
        Assert.Equal(CalcNoticeSeverity.Warning, warning.Severity);
        Assert.StartsWith("n_L:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("3000", warning.Message, StringComparison.Ordinal);
        Assert.Contains("2850", warning.Message, StringComparison.Ordinal);
        Assert.Contains("speed-above-rated", Motor.Template.ApplicabilityRules.Select(r => r.Id));

        // Without n_rated there is nothing to compare with (the rated-speed notice already says so); a point-to-point sheet ignores n_rated altogether.
        var without = CalcRunner.Run(Motor, new CalcInputSet([.. Continuous(CalcInputValue.Quantity("n_L", 750, "rpm")).Values.Where(v => v.Symbol != "n_rated")]));
        Assert.DoesNotContain(without.Notices, n => n.RuleId == "speed-above-rated");
        var pointToPoint = CalcTemplateHasher.DefaultInputs(Motor.Template).With(CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyPointToPoint)).With(CalcInputValue.Quantity("n_rated", 100, "rpm"));
        Assert.DoesNotContain(CalcRunner.Run(Motor, pointToPoint).Notices, n => n.RuleId == "speed-above-rated");
    }
}
