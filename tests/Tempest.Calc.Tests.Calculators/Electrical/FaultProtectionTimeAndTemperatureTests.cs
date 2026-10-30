using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E5 1.3.2: elec.fault-protection-05 (t_req is a free input, so a 5 s time on a 230 V final circuit, or 0.4 s at a higher voltage to earth, raised no notice) and
/// -06 (Z_s is taken at theta while the adiabatic check starts from theta_i: XLPE at 90 °C with theta 70 °C gave Z_s 0.7901 ohm, 3.7 % below the 0.8190 ohm at 90 °C).
/// Expected values from python3: R_12 = 17.241e-3·k_R·20·(1/2.5 + 1/1.5)·(1 + 0.00393·(θ − 20)); with k_R 1 (the finding's pure resistivity) R(20 °C) = 0.367808 ohm, so
/// Z_s = 0.35 + 0.367808·1.1965 = 0.7901 ohm at 70 °C and 0.35 + 0.367808·1.2751 = 0.8190 ohm at 90 °C.
/// </summary>
public sealed class FaultProtectionTimeAndTemperatureTests
{
    private static readonly FaultProtectionCalculator Fault = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Fault, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Fault.Template), (set, v) => set.With(v)));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void A_Long_Disconnection_Time_On_A_230_V_Circuit_Is_Warned()
    {
        var r = Run(Q("t_req", 5, "s"));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "t_req");
        Assert.Contains("t_req", warning.Message, StringComparison.Ordinal);
        Assert.Contains("0.4", warning.Message, StringComparison.Ordinal);
        Assert.Contains("distribution", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Higher_Voltage_To_Earth_With_The_230_V_Time_Is_Told_To_Check_The_Table()
    {
        var r = Run(Q("U_0", 400, "V"));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "t_req");
        Assert.Contains("U_0", warning.Message, StringComparison.Ordinal);
        Assert.Contains("Table 41.1", warning.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(230, 0.4)]
    [InlineData(230, 0.2)]
    [InlineData(120, 0.4)]
    [InlineData(400, 0.1)]
    public void The_Usual_Times_Raise_No_Time_Notice(double u0, double tReq)
    {
        Assert.DoesNotContain(Run(Q("U_0", u0, "V"), Q("t_req", tReq, "s")).Warnings, w => w.RuleId == "t_req");
    }

    [Fact]
    public void A_Loop_Temperature_Below_The_Conductor_Temperature_Of_The_Adiabatic_Check_Is_Warned()
    {
        var r = Run(CalcInputValue.Of("k_R", 1), Q("theta_i", 90, "degC"), Q("theta_f", 250, "degC"), Q("T_op", 70, "degC"));

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "theta");
        Assert.Contains("90", warning.Message, StringComparison.Ordinal);
        Assert.Contains("understated", warning.Message, StringComparison.Ordinal);
        Assert.Equal(0.7901, r.OutputSi("Z_s"), 4);

        var matched = Run(CalcInputValue.Of("k_R", 1), Q("theta_i", 90, "degC"), Q("theta_f", 250, "degC"), Q("T_op", 90, "degC"));
        Assert.DoesNotContain(matched.Warnings, w => w.RuleId == "theta");
        Assert.Equal(0.8190, matched.OutputSi("Z_s"), 4);
    }

    [Fact]
    public void The_Defaults_Raise_Neither_Notice()
    {
        var r = Run();

        Assert.DoesNotContain(r.Warnings, w => w.RuleId is "t_req" or "theta");
    }
}
