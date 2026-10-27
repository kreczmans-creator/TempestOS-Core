using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M7 1.2.0, mech.press-fit-R1: the torque and the press force used the nominal interference, but the surface roughness smooths on assembly and takes about 0.8 of the summed Rz off it (DIN 7190-1), so the
/// torque of a fine fit read high and nothing said so. Expected values from python3 (the default case, 50 mm steel shaft in a 100 mm steel hub, δ 40 µm, μ 0.15, L 60 mm): p = 62.1 MPa, T = 2194.8 N·m;
/// Rz 4 µm on each part: δ_eff = 40 − 0.8 × 8 = 33.6 µm, so p_eff = 62.1 × 33.6/40 = 52.164 MPa and T = 2194.8 × 0.84 = 1843.6 N·m (−16 %); the stress checks stay at the nominal interference.
/// </summary>
public sealed class PressFitRoughnessTests
{
    private static readonly PressFitCalculator Fit = new();

    private static CalcInputSet Case(params CalcInputValue[] overrides) => overrides.Aggregate(new CalcInputSet([.. Fit.Template.TestCases[0].Inputs]), (set, v) => set.With(v));

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    [Fact]
    public void The_Roughness_Smoothing_Takes_0_8_Of_The_Summed_Rz_Off_The_Interference_For_The_Torque()
    {
        var plain = CalcRunner.Run(Fit, Case());
        var smooth = CalcRunner.Run(Fit, Case(Q("Rz_h", 4, "µm"), Q("Rz_s", 4, "µm")));

        Assert.Equal(2194.8, plain.OutputSi("T"), 0);
        Assert.Equal(33.6, smooth.OutputSi("delta_eff") * 1e6, 6);
        Assert.Equal(2194.8 * 0.84, smooth.OutputSi("T"), 0);
        Assert.Equal(plain.OutputSi("F_a") * 0.84, smooth.OutputSi("F_a"), 0);
        Assert.Equal(plain.OutputSi("p_int") * 0.84, smooth.OutputSi("p_eff"), 0);
    }

    [Fact]
    public void The_Stresses_And_The_Yield_Checks_Stay_At_The_Nominal_Interference()
    {
        var plain = CalcRunner.Run(Fit, Case());
        var smooth = CalcRunner.Run(Fit, Case(Q("Rz_h", 4, "µm"), Q("Rz_s", 4, "µm")));

        Assert.Equal(plain.OutputSi("p_int"), smooth.OutputSi("p_int"), 3);
        Assert.Equal(plain.OutputSi("sigma_vm_hub"), smooth.OutputSi("sigma_vm_hub"), 3);
    }

    [Fact]
    public void Without_A_Roughness_The_Run_States_That_The_Smoothing_Is_Not_Applied()
    {
        var plain = CalcRunner.Run(Fit, Case());

        Assert.Equal(plain.OutputSi("p_int"), plain.OutputSi("p_eff"), 3);
        var assumption = Assert.Single(plain.RunAssumptions, a => a.Statement.Contains("smoothing", StringComparison.Ordinal));
        Assert.Contains("minimum interference", assumption.Statement, StringComparison.Ordinal);
        Assert.Contains("maximum", assumption.Statement, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Roughness_Larger_Than_The_Interference_Leaves_No_Grip_And_Does_Not_Go_Negative()
    {
        var r = CalcRunner.Run(Fit, Case(Q("Rz_h", 30, "µm"), Q("Rz_s", 30, "µm")));

        Assert.Equal(0, r.OutputSi("delta_eff"), 12);
        Assert.Equal(0, r.OutputSi("T"), 9);
    }
}
