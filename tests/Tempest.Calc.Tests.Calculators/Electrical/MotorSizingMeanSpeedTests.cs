using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.9.0, chain.drive-train-03 (bearing life needs the mean speed over the cycle, not the peak speed) and chain.motor-circuit-04 (a supply sized on the duty can sit below the motor's rating: the rated shaft
/// power must be available on a point-to-point sheet). Expected values from python3: ka-ball-screw, 0.5 m in 0.5 s with f_acc 1/3 and a 0.5 s dwell, ω_pk 942.4778 rad/s (9000 rpm), t_acc 0.1667 s:
/// the motor turns ω_pk (t_move − t_acc) = 942.4778 × 0.3333 = 314.159 rad = 50 revolutions per 1.0 s cycle, so n_mean = 50 rev/s = 3000 rpm against the peak 9000 rpm; P_rated = T_rated n_rated = 2 N·m × 3000 rpm = 628.318 W.
/// </summary>
public sealed class MotorSizingMeanSpeedTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcInputSet Case(int index, params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Motor.Template.TestCases[index].Inputs]), (set, v) => set.With(v));

    [Fact]
    public void A_Point_To_Point_Cycle_Has_A_Mean_Speed_Over_The_Whole_Cycle_Dwell_Included()
    {
        var r = CalcRunner.Run(Motor, Case(0));

        Assert.Equal(9000, r.OutputSi("n_pk") * 60 / (2 * Math.PI), 6);
        Assert.Equal(3000, r.OutputSi("n_mean") * 60 / (2 * Math.PI), 6);
        Assert.Contains(Motor.Template.OutputPorts!, p => p.Symbol == "n_mean" && p.Unit == "rpm");
    }

    [Fact]
    public void The_Sine_Profile_Covers_The_Same_Travel_So_The_Mean_Speed_Is_The_Same()
    {
        var r = CalcRunner.Run(Motor, Case(0, CalcInputValue.Choice("profile", MotorSizingCalculator.ProfileSCurve)));

        Assert.Equal(3000, r.OutputSi("n_mean") * 60 / (2 * Math.PI), 6);
    }

    [Fact]
    public void In_Continuous_Duty_The_Mean_Speed_Is_The_Steady_Speed()
    {
        var r = CalcRunner.Run(Motor, new CalcInputSet([.. Motor.Template.TestCases[2].Inputs]));

        Assert.Equal(r.OutputSi("n_pk"), r.OutputSi("n_mean"), 9);
        Assert.Equal(2800, r.OutputSi("n_mean") * 60 / (2 * Math.PI), 6);
    }

    [Fact]
    public void A_Rated_Speed_On_A_Point_To_Point_Sheet_Gives_The_Rated_Power_Instead_Of_Being_Ignored()
    {
        var r = CalcRunner.Run(Motor, Case(0, CalcInputValue.Quantity("n_rated", 3000, "rpm")));

        Assert.Equal(2.0 * 3000 * 2 * Math.PI / 60, r.OutputSi("P_rated"), 6);
        Assert.Equal(628.3185307, r.OutputSi("P_rated"), 5);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "rated-speed-ignored");
    }

    [Fact]
    public void Without_A_Rated_Speed_There_Is_No_Rated_Power_And_No_Notice()
    {
        var r = CalcRunner.Run(Motor, Case(0));

        Assert.Null(r.FindOutput("P_rated"));
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "rated-speed-ignored");
    }
}
