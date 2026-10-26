using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.6.0: the torque split a shaft's fatigue check needs (chain.drive-train-01), the output-side torques behind the ratio (chain.drive-train-02)
/// and the axial thrust of a ball screw (chain.drive-train-05). Expected values from python3 over the ka-ball-screw, ka-geared-index-table and
/// ka-continuous-pump cases of the template.
/// </summary>
public sealed class MotorTorqueSplitTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcRunResult Run(int index) => CalcRunner.Run(Motor, new CalcInputSet([.. Motor.Template.TestCases[index].Inputs]));

    [Fact]
    public void A_Point_To_Point_Cycle_Alternates_About_The_Load_Torque_By_The_Acceleration_Torque()
    {
        // ka-ball-screw: T_load 0.086710, T_acc 2.492235, T_pk 2.578945, T_dec -2.405525 N·m (python3); (T_pk - T_dec)/2 = 2.492235.
        var r = Run(0);
        Assert.False(r.IsBlocked);
        Assert.Equal((r.OutputSi("T_pk") - r.OutputSi("T_dec")) / 2, r.OutputSi("T_alt"), 12);
        Assert.Equal(2.4922347, r.OutputSi("T_alt"), 1e-6);
        Assert.Equal(r.OutputSi("T_load"), (r.OutputSi("T_pk") + r.OutputSi("T_dec")) / 2, 12);
    }

    [Fact]
    public void Continuous_Running_Is_Steady_With_No_Alternating_Torque()
    {
        var r = Run(2);
        Assert.False(r.IsBlocked);
        Assert.Equal(0, r.OutputSi("T_alt"), 12);
        Assert.Equal(r.OutputSi("T_load"), r.OutputSi("T_rms"), 12);
    }

    [Fact]
    public void A_Ball_Screw_Puts_Its_Load_Plus_The_Accelerated_Mass_On_The_Support_Bearings()
    {
        // ka-ball-screw: F_load = m g mu = 50 x 9.80665 x 0.1 = 49.03325 N; m a_pk = 50 x 9 = 450 N; sum 499.03325 N (python3).
        var r = Run(0);
        Assert.Equal(499.03325, r.OutputSi("F_ax_pk"), 3);
        Assert.Equal(0, Run(1).OutputSi("F_ax_pk"), 12);
        Assert.Equal(0, Run(2).OutputSi("F_ax_pk"), 12);
    }

    [Fact]
    public void The_Output_Shaft_Behind_The_Ratio_Carries_The_Motor_Torque_Times_Ratio_And_Efficiency()
    {
        // ka-geared-index-table: i 10, eta 0.85, T_pk 5.012688 N·m, 750 rpm at the motor: T_out_pk = 5.012688 x 8.5 = 42.60785 N·m, n_out = 75 rpm (python3).
        var table = Run(1);
        Assert.Equal(42.607850, table.OutputSi("T_out_pk"), 1e-5);
        Assert.Equal(75, table.OutputSi("n_out") * 60 / (2 * Math.PI), 6);
        Assert.Equal(table.OutputSi("T_alt") * 8.5, table.OutputSi("T_out_a"), 12);
        Assert.Equal(table.OutputSi("T_load") * 8.5, table.OutputSi("T_out_m"), 12);

        // ka-continuous-pump: T_L 3 N·m at the output through 4:1 at 0.9: the output shaft carries T_load x 4 x 0.9 = T_L (python3: 0.83333 x 3.6 = 3.0).
        var pump = Run(2);
        Assert.Equal(3.0, pump.OutputSi("T_out_m"), 9);
        Assert.Equal(0, pump.OutputSi("T_out_a"), 12);
        Assert.Equal(700, pump.OutputSi("n_out") * 60 / (2 * Math.PI), 6);

        // A linear mechanism has no output shaft.
        Assert.Null(Run(0).FindOutput("T_out_pk"));
    }
}
