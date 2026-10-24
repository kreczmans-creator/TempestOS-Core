using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class ConnectionTests
{
    private static readonly ConnectionCalculator Ec3 = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Ec3.Template);

    [Theory]
    [InlineData(12, 1.75, 84.3)]
    [InlineData(16, 2.0, 157)]
    [InlineData(20, 2.5, 245)]
    [InlineData(24, 3.0, 353)]
    [InlineData(30, 3.5, 561)]
    [InlineData(36, 4.0, 817)]
    public void Stress_Area_Formula_Gives_The_Familiar_Values(double d, double pitch, double expected) =>
        Assert.Equal(expected, ConnectionCalculator.StressArea(d, pitch), expected * 0.003);

    [Theory]
    [InlineData("4.6", true, 0.6)]
    [InlineData("4.8", true, 0.5)]
    [InlineData("5.8", true, 0.5)]
    [InlineData("6.8", true, 0.5)]
    [InlineData("8.8", true, 0.6)]
    [InlineData("10.9", true, 0.5)]
    [InlineData("10.9", false, 0.6)]
    public void Alpha_V_Follows_Table_3_4(string grade, bool threads, double alphaV) => Assert.Equal(alphaV, ConnectionCalculator.AlphaV(grade, threads));

    [Fact]
    public void Shank_In_The_Shear_Plane_Uses_The_Gross_Area()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Switch("threads", false)));
        Assert.Equal(0.6 * 800 * Math.PI / 4 * 400 / 1.25, r.OutputSi("F_v_Rd"), 3);
    }

    [Fact]
    public void Edge_Distance_Below_The_Minimum_Fails_Spacing()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_1", 22, "mm")));
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Spacing", StringComparison.Ordinal) && !c.Passed);
    }

    [Fact]
    public void Plate_Thinner_Than_3_Mm_Is_A_Hard_Fail()
    {
        Assert.Contains(CalcInputValidator.Validate(Ec3.Template, Defaults().With(CalcInputValue.Quantity("t", 2, "mm"))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void Bearing_Never_Exceeds_The_Plate_Limit()
    {
        foreach (var e1 in new[] { 30.0, 60, 120, 400 })
        {
            var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("e_1", e1, "mm")));
            Assert.True(r.OutputSi("F_b_Rd") <= 2.5 * 410e6 * 0.020 * 0.010 / 1.25 + 1e-6);
        }
    }

    [Fact]
    public void Weld_Methods_Agree_For_Pure_Longitudinal_Shear()
    {
        // τ∥ only: directional limit f_u/(√3 β_w γ_M2) equals the simplified strength.
        var fvwd = 410 / Math.Sqrt(3) / (0.85 * 1.25);
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("tau_par", fvwd, "MPa")));
        Assert.Equal(1.0, r.OutputSi("u_dir"), 6);
    }

    [Fact]
    public void Block_Tearing_Needs_All_Three_Inputs()
    {
        var partial = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("V_Ed", 100, "kN")));
        Assert.Contains(partial.Warnings, w => w.RuleId == "block");
        var full = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Quantity("V_Ed", 100, "kN")).With(CalcInputValue.Quantity("A_nt", 300, "mm^2")).With(CalcInputValue.Quantity("A_nv", 1200, "mm^2")));
        Assert.Equal(410 * 300 / 1.25 + 275 * 1200 / Math.Sqrt(3), full.OutputSi("V_eff_Rd"), 3);
    }

    [Fact]
    public void Bolt_Values_That_Feed_The_Base_Plate_Are_Ports()
    {
        var r = CalcRunner.Run(Ec3, Defaults());
        var ports = Ec3.Template.OutputPorts!;

        foreach (var (symbol, unit) in new[] { ("F_t_Rd", "kN"), ("A_s", "mm^2"), ("F_b_Rd", "kN") })
        {
            Assert.Contains(ports, p => p.Symbol == symbol && p.Unit == unit);
            Assert.NotNull(r.FindOutput(symbol));
        }

        // M20: A_s = 244.79 mm², F_t,Rd = 0.9 f_ub A_s / γ_M2.
        Assert.Equal(244.794, r.OutputSi("A_s") * 1e6, 3);
        Assert.Equal(0.9 * 800 * ConnectionCalculator.StressArea(20, 2.5) / 1.25, r.OutputSi("F_t_Rd"), 1e-3);
    }

    [Fact]
    public void Bolt_Strengths_Are_Ports_For_The_Anchor_Bolt_Data_Of_The_Base_Plate()
    {
        var r = CalcRunner.Run(Ec3, Defaults().With(CalcInputValue.Choice("grade", "10.9")));
        Assert.Equal(1000e6, r.OutputSi("f_ub"), 1);
        Assert.Equal(900e6, r.OutputSi("f_yb"), 1);
        Assert.Contains(Ec3.Template.OutputPorts!, p => p is { Symbol: "f_ub", Unit: "MPa" });
        Assert.Contains(Ec3.Template.OutputPorts!, p => p is { Symbol: "f_yb", Unit: "MPa" });
    }
}
