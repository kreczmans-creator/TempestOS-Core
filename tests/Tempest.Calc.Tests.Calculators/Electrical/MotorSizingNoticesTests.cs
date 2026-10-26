using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.9.0: elec.motor-sizing-03 (the S-curve peak power paired the mid-acceleration torque with the end-of-acceleration speed), -04 (a blank screw lead became 10 mm with no notice),
/// -R1 (one move and one dwell, so a vertical or one-way-force axis has no return stroke and no notice) and -R2 (the peak torque is compared with the stall peak at the peak speed).
/// Expected values from python3 on the ka-ball-screw slide with the sine profile: T_load 0.0867098, T_acc 3.914793 N·m, ω_pk 942.4778 rad/s; P(x) = ω_pk (1 − cos x)/2 · (T_load + T_acc sin x);
/// the maximum over x in [0, π] (4e6-point scan) is 2457.89 W at x = 2.1017 (the old product T_pk·ω_pk 3771.33 W); the braking power maximum ω_pk (1 + cos y)/2 · (T_acc sin y − T_load) is 2335.31 W.
/// </summary>
public sealed class MotorSizingNoticesTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcInputSet Case(int index, params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Motor.Template.TestCases[index].Inputs]), (set, v) => set.With(v));

    private static CalcRunResult Run(CalcInputSet inputs) => CalcRunner.Run(Motor, inputs);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void The_Sine_Profile_Peak_Power_Is_The_Maximum_Of_Torque_Times_Speed_Not_Their_Two_Peaks()
    {
        var r = Run(Case(0, CalcInputValue.Choice("profile", MotorSizingCalculator.ProfileSCurve)));

        Assert.Equal(2457.8906, r.OutputSi("P_pk"), 1);
        Assert.Equal(2335.3090, r.OutputSi("P_brake"), 1);
        Assert.True(r.OutputSi("P_pk") < r.OutputSi("T_pk") * r.OutputSi("n_pk"));
    }

    [Fact]
    public void The_Trapezoid_Peak_Power_Is_Unchanged()
    {
        var r = Run(Case(0));

        Assert.Equal(2430.597996309809, r.OutputSi("P_pk"), 6);
    }

    [Fact]
    public void A_Blank_Screw_Lead_Is_Warned_Because_It_Becomes_10_mm()
    {
        var withLead = Run(Case(0));
        Assert.DoesNotContain(withLead.Warnings, w => w.RuleId == "p_screw");

        var blank = Run(new CalcInputSet([.. Motor.Template.TestCases[0].Inputs.Where(v => v.Symbol != "p_screw")]));
        var warning = Assert.Single(blank.Warnings, w => w.RuleId == "p_screw");
        Assert.StartsWith("p_screw:", warning.Message, StringComparison.Ordinal);
        Assert.Contains("10 mm", warning.Message, StringComparison.Ordinal);
        Assert.Equal(withLead.OutputSi("T_pk"), blank.OutputSi("T_pk"), 9);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(90, 0, true)]
    [InlineData(-20, 0, true)]
    [InlineData(0, 150, true)]
    public void A_Tilted_Axis_Or_A_Process_Force_Is_Told_That_The_Return_Stroke_Is_Not_In_The_Cycle(double thetaDeg, double processForce, bool warned)
    {
        var r = Run(Case(0, Q("theta", thetaDeg, "deg"), Q("F_p", processForce, "N")));

        Assert.Equal(warned, r.Warnings.Any(w => w.RuleId == "return-move"));

        if (warned)
            Assert.Contains("return", r.Warnings.First(w => w.RuleId == "return-move").Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Peak_Speed_Near_The_Limit_Is_Told_To_Check_The_Peak_Torque_At_That_Speed()
    {
        // ka-ball-screw: 9000 rpm of 10 000 rpm is 0.9 of the limit; at 3000 rpm of 10 000 (0.3) the stall peak is a fair comparison.
        var fast = Run(Case(0));
        var warning = Assert.Single(fast.Warnings, w => w.RuleId == "peak-at-speed");
        Assert.Contains("n_pk", warning.Message, StringComparison.Ordinal);
        Assert.Contains("stall", warning.Message, StringComparison.Ordinal);

        var slow = Run(Case(0, Q("n_max", 20000, "rpm")));
        Assert.DoesNotContain(slow.Warnings, w => w.RuleId == "peak-at-speed");
    }
}
