using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.2.0: the RMS mechanical power over the cycle, the thermal-equivalent continuous power a supply sees (Green G-B-11). The motor-circuit pack
/// used to link the instantaneous peak power (2.43 kW) as a continuous demand; the cycle's RMS power is 0.784 kW.
/// </summary>
public sealed class MotorRmsPowerTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcInputSet Case(int index, params CalcInputValue[] overrides)
    {
        var d = Motor.Template.TestCases[index].Inputs.ToDictionary(v => v.Symbol);

        foreach (var o in overrides)
            d[o.Symbol] = o;

        return new CalcInputSet([.. d.Values]);
    }

    /// <summary>The RMS of P = T·ω over one cycle by composite Simpson integration of each continuous phase of the motion.</summary>
    private static double NumericRms(CalcRunResult r, bool sCurve, double tMove, double fAcc, double tDwell)
    {
        var tLoad = r.OutputSi("T_load");
        var tAcc = r.OutputSi("T_acc");
        var wPk = r.OutputSi("n_pk");
        var ta = fAcc * tMove;

        double Torque(double t, bool accel) => tLoad + (accel ? 1 : -1) * tAcc * (sCurve ? Math.Sin(Math.PI * t / ta) : 1);
        double Speed(double t) => sCurve ? wPk / 2 * (1 - Math.Cos(Math.PI * t / ta)) : wPk * t / ta;

        static double Simpson(Func<double, double> f, double a, double b)
        {
            const int n = 2000;
            var h = (b - a) / n;
            var sum = f(a) + f(b);

            for (var i = 1; i < n; i++)
                sum += f(a + i * h) * (i % 2 == 0 ? 2 : 4);

            return sum * h / 3;
        }

        var accel = Simpson(t => Math.Pow(Torque(t, true) * Speed(t), 2), 0, ta);
        var decel = Simpson(t => Math.Pow(Torque(t, false) * Speed(t), 2), 0, ta);
        var cruise = tLoad * tLoad * wPk * wPk * (tMove - 2 * ta);
        return Math.Sqrt((accel + decel + cruise) / (tMove + tDwell));
    }

    [Fact]
    public void The_Ball_Screw_Cycle_Has_A_Fraction_Of_Its_Peak_Power_As_RMS_Power()
    {
        // Analytic (python3): P_rms = 784.1424 W against P_pk = 2430.60 W; the sine profile gives 843.1893 W.
        var trap = CalcRunner.Run(Motor, Case(0));
        Assert.Equal(0.7841424404208093, trap.OutputSi("P_rms") / 1e3, 9);
        Assert.Equal(2.430597996309809, trap.OutputSi("P_pk") / 1e3, 9);

        var sine = CalcRunner.Run(Motor, Case(0, CalcInputValue.Choice("profile", MotorSizingCalculator.ProfileSCurve)));
        Assert.Equal(0.8431893041992, sine.OutputSi("P_rms") / 1e3, 9);
    }

    [Fact]
    public void The_Closed_Form_Matches_Numerical_Integration_For_Both_Profiles()
    {
        Gen.Select(Gen.Double[5, 400], Gen.Double[0.2, 3], Gen.Double[0.1, 0.45], Gen.Double[0, 2], Gen.Double[0, 0.4], Gen.Bool).Sample((mass, tMove, fAcc, dwell, mu, sine) =>
        {
            var r = CalcRunner.Run(Motor, Case(0,
                CalcInputValue.Quantity("m", mass, "kg"), CalcInputValue.Quantity("t_move", tMove, "s"), CalcInputValue.Of("f_acc", fAcc), CalcInputValue.Quantity("t_dwell", dwell, "s"), CalcInputValue.Of("mu", mu),
                CalcInputValue.Quantity("n_max", 1e6, "rpm"), CalcInputValue.Choice("profile", sine ? MotorSizingCalculator.ProfileSCurve : MotorSizingCalculator.ProfileTrapezoidal)));
            Assert.False(r.IsBlocked);
            var expected = NumericRms(r, sine, tMove, fAcc, dwell);
            Assert.Equal(expected, r.OutputSi("P_rms"), expected * 1e-6);
        }, iter: 60);
    }

    [Fact]
    public void RMS_Power_Never_Exceeds_The_Peak_And_Is_Not_The_Peak()
    {
        Gen.Select(Gen.Double[5, 400], Gen.Double[0.2, 3], Gen.Double[0.1, 0.45]).Sample((mass, tMove, fAcc) =>
        {
            var r = CalcRunner.Run(Motor, Case(0, CalcInputValue.Quantity("m", mass, "kg"), CalcInputValue.Quantity("t_move", tMove, "s"), CalcInputValue.Of("f_acc", fAcc), CalcInputValue.Quantity("n_max", 1e6, "rpm")));
            return r.OutputSi("P_rms") < r.OutputSi("P_pk") && r.OutputSi("P_rms") > 0;
        }, iter: 60);
    }

    [Fact]
    public void In_Continuous_Duty_The_RMS_Power_Is_The_Steady_Power()
    {
        var r = CalcRunner.Run(Motor, Case(2));
        Assert.Equal(r.OutputSi("P_cont"), r.OutputSi("P_rms"), 9);
        Assert.Equal(244.34609527920608, r.OutputSi("P_rms"), 6);
    }

    [Fact]
    public void P_Rms_Is_A_Linkable_Port_In_Kilowatts()
    {
        var port = Assert.Single(Motor.Template.OutputPorts!, p => p.Symbol == "P_rms");
        Assert.Equal("kW", port.Unit);
    }

    [Fact]
    public void A_Direct_Coupled_Load_With_A_Leftover_Efficiency_Is_Questioned()
    {
        // Green G-B-18(c): a direct drive has no transmission, but the default overall efficiency 0.9 still divides the load torque.
        var direct = Case(1, CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechDirectRotary), CalcInputValue.Of("i", 1));
        var leftover = CalcRunner.Run(Motor, direct.With(CalcInputValue.Of("eta", 0.9)));
        Assert.Contains(leftover.Warnings, w => w.RuleId == "eta");
        Assert.DoesNotContain(CalcRunner.Run(Motor, direct.With(CalcInputValue.Of("eta", 1))).Warnings, w => w.RuleId == "eta");
        Assert.DoesNotContain(CalcRunner.Run(Motor, direct.With(CalcInputValue.Of("eta", 0.98))).Warnings, w => w.RuleId == "eta");
        Assert.DoesNotContain(CalcRunner.Run(Motor, Case(1, CalcInputValue.Of("eta", 0.85))).Warnings, w => w.RuleId == "eta");
    }
}
