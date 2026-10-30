using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.7.0: elec.motor-sizing-01 (the dwell ran at zero torque), -02 (the peak check used T_pk only), -C1 (continuous duty with no load torque passed) and
/// -G1 (no consistency check on the nameplate rated power). Expected values from python3 (a re-implementation of the relations in the template text).
/// </summary>
public sealed class MotorSizingChecksTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcInputSet Case(int index, params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Motor.Template.TestCases[index].Inputs]), (set, v) => set.With(v));

    private static CalcRunResult Run(CalcInputSet inputs) => CalcRunner.Run(Motor, inputs);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The ka-ball-screw slide made a 500 kg vertical axis with a 10 s dwell and a bigger candidate motor.</summary>
    private static CalcInputSet VerticalAxis(params CalcInputValue[] more) => Case(0, [Q("m", 500, "kg"), Q("theta", 90, "deg"), Q("t_dwell", 10, "s"), Q("T_rated", 5, "N*m"), Q("T_peak", 30, "N*m"), .. more]);

    [Fact]
    public void A_Vertical_Axis_Holds_Its_Weight_In_The_Dwell_And_The_Rms_Torque_Shows_It()
    {
        // m g k = 500 x 9.80665 x 0.010/(2 pi) = 7.80388 N.m (python3); T_rms over the 10.5 s cycle with the hold = 8.03366 N.m, u_rms = 1.25 x 8.03366/5 = 2.00842.
        // Without the hold (the brake holds the load) the dwell is at zero torque: T_rms 2.55717, u_rms 0.63929.
        var held = Run(VerticalAxis());
        Assert.Equal(7.803884113, held.OutputSi("T_hold"), 6);
        Assert.Equal(8.033661002, held.OutputSi("T_rms"), 6);
        Assert.Equal(2.008415250, held.OutputSi("u_rms"), 6);
        Assert.Contains(held.Checks, c => c.Name.StartsWith("RMS torque", StringComparison.Ordinal) && !c.Passed);

        var braked = Run(VerticalAxis(CalcInputValue.Switch("brake", true)));
        Assert.Equal(0, braked.OutputSi("T_hold"), 12);
        Assert.Equal(2.557172414, braked.OutputSi("T_rms"), 6);
        Assert.Equal(0.639293103, braked.OutputSi("u_rms"), 6);
        Assert.Contains(braked.Checks, c => c.Name.StartsWith("RMS torque", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void A_Horizontal_Axis_Needs_No_Holding_Torque_And_Keeps_Its_Rms()
    {
        var r = Run(Case(0));
        Assert.Equal(0, r.OutputSi("T_hold"), 12);
        Assert.Equal(1.4402, r.OutputSi("T_rms"), 3);
    }

    [Fact]
    public void A_Downhill_Move_Is_Checked_At_Its_Braking_Torque()
    {
        // 200 kg at -20 degrees, trapezoid, T_peak 6 N.m: T_pk 4.01922, T_dec -5.73990 N.m (python3), so SF |T_dec|/T_peak = 1.25 x 5.7399/6 = 1.19581 fails
        // where SF T_pk/T_peak = 0.837 passed. Braking power at the peak speed |T_dec| omega_pk = 5409.7 W.
        var r = Run(Case(0, Q("m", 200, "kg"), Q("theta", -20, "deg"), Q("T_rated", 5.5, "N*m"), Q("T_peak", 6, "N*m"), CalcInputValue.Switch("brake", true)));

        Assert.Equal(4.019220970, r.OutputSi("T_pk"), 6);
        Assert.Equal(-5.739896819, r.OutputSi("T_dec"), 6);
        Assert.Equal(1.195811837, r.OutputSi("u_peak"), 6);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Peak torque", StringComparison.Ordinal) && !c.Passed);
        Assert.Equal(5.409725304, r.OutputSi("P_brake") / 1e3, 6);
    }

    [Fact]
    public void An_Uphill_Move_Keeps_The_Peak_Check_On_T_pk()
    {
        // ka-ball-screw: |T_dec| 2.40553 < T_pk 2.57894, so u_peak = 1.25 x 2.57894/6 as before; braking power 2.405525 x 942.4778 = 2267.15 W (python3).
        var r = Run(Case(0));
        Assert.Equal(1.25 * 2.578945 / 6, r.OutputSi("u_peak"), 5);
        Assert.Equal(2.405525 * 942.477796, r.OutputSi("P_brake"), 2);
    }

    [Fact]
    public void Continuous_Duty_With_No_Load_Torque_Is_Refused_Not_Passed()
    {
        // The pump case with T_L removed gave T_load 0, u_rms 0 and three passes.
        var blank = Run(new CalcInputSet([.. Motor.Template.TestCases[2].Inputs.Where(v => v.Symbol != "T_L")]));
        var block = Assert.Single(blank.Blocks);
        Assert.Equal("continuous-input", block.RuleId);
        Assert.StartsWith("T_L:", block.Message, StringComparison.Ordinal);
        Assert.Empty(blank.Checks);

        var zero = Run(Case(2, Q("T_L", 0, "N*m")));
        Assert.Equal("continuous-input", Assert.Single(zero.Blocks).RuleId);

        var entered = Run(Case(2));
        Assert.False(entered.IsBlocked);
    }

    [Fact]
    public void A_Rated_Speed_Far_Below_The_Running_Speed_Gives_A_Rated_Power_Below_The_Load_And_Fails()
    {
        // ka-continuous-pump: P_cont = 0.833333 x 293.2153 = 244.346 W. n_rated 100 rpm: P_rated = 2.0 x 2 pi x 100/60 = 20.944 W, u_P = 11.667 (python3).
        var r = Run(Case(2, Q("n_rated", 100, "rpm")));

        Assert.Equal(20.943951, r.OutputSi("P_rated"), 5);
        Assert.Equal(244.3461 / 20.943951, r.OutputSi("u_power"), 4);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Continuous power against", StringComparison.Ordinal) && !c.Passed);

        var nameplate = Run(Case(2));
        Assert.Equal(244.3461 / 596.9026, nameplate.OutputSi("u_power"), 5);
        Assert.Contains(nameplate.Checks, c => c.Name.StartsWith("Continuous power against", StringComparison.Ordinal) && c.Passed);
    }

    [Fact]
    public void A_Rated_Speed_Above_The_Maximum_Speed_Warns()
    {
        // n_max 3500 rpm, n_rated 5000 rpm: P_rated = 2.0 x 2 pi x 5000/60 = 1047.2 W, a rating the motor cannot reach.
        var r = Run(Case(2, Q("n_rated", 5000, "rpm")));
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "rated-speed-range");
        Assert.Contains("maximum speed", warning.Message, StringComparison.Ordinal);
        Assert.Equal(1047.1975512, r.OutputSi("P_rated"), 4);
        Assert.DoesNotContain(Run(Case(2)).Warnings, w => w.RuleId == "rated-speed-range");
    }
}
