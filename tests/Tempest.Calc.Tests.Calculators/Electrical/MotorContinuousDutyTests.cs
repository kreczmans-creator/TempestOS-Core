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
}
