using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class MemberChecksTests
{
    private static readonly MemberChecksCalculator Calc = new();

    private static CalcInputSet Uc(params CalcInputValue[] overrides)
    {
        var baseline = Calc.Template.TestCases[0].Inputs.ToDictionary(v => v.Symbol);
        foreach (var o in overrides) baseline[o.Symbol] = o;
        return new CalcInputSet([.. baseline.Values]);
    }

    [Fact]
    public void Reduction_Factor_Is_One_For_Stocky_And_Falls_With_Slenderness()
    {
        Gen.Select(Gen.Double[0.5, 12], Gen.Double[0.5, 12]).Sample((l1, l2) =>
        {
            var r1 = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_z", Math.Min(l1, l2), "m")));
            var r2 = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_z", Math.Max(l1, l2), "m")));
            Assert.False(r1.IsBlocked);
            Assert.True(r1.OutputSi("chi_z") <= 1 + 1e-12);
            Assert.True(r1.OutputSi("chi_z") >= r2.OutputSi("chi_z") - 1e-12);
            Assert.True(r1.OutputSi("N_b_z_Rd") <= r1.OutputSi("N_pl_Rd") + 1e-6);
        }, iter: 100);
    }

    [Fact]
    public void Short_Member_Buckling_Resistance_Equals_Plastic_Resistance()
    {
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("L_cr_y", 0.1, "m"), CalcInputValue.Quantity("L_cr_z", 0.1, "m")));
        Assert.Equal(1, r.OutputSi("chi_z"), 9);
        Assert.Equal(r.OutputSi("N_pl_Rd"), r.OutputSi("N_b_Rd"), 1e-6);
    }

    [Fact]
    public void Tension_Skips_Buckling_Checks()
    {
        var r = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", 500, "kN")));
        Assert.False(r.IsBlocked);
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Flexural", StringComparison.Ordinal));
        Assert.Equal(0, r.OutputSi("u_buckling"));
        Assert.Equal(500.0 / 2083.85, r.OutputSi("u_N"), 4);
    }

    [Fact]
    public void High_Shear_Reduces_Bending_Resistance()
    {
        var low = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("V_Ed", 100, "kN")));
        var high = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("V_Ed", 300, "kN")));
        Assert.Equal(0, low.OutputSi("rho"));
        Assert.True(high.OutputSi("rho") > 0);
        Assert.True(high.OutputSi("M_y_c_Rd") < low.OutputSi("M_y_c_Rd"));
        Assert.Contains(high.Notices, n => n.RuleId == "high-shear");
    }

    [Fact]
    public void Class_4_Blocks_And_Class_3_Uses_Elastic_Modulus()
    {
        Assert.True(CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "4"))).IsBlocked);
        var c3 = CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "3")));
        var c1 = CalcRunner.Run(Calc, Uc(CalcInputValue.Choice("section_class", "1")));
        Assert.Equal(450.0 / 497.0, c3.OutputSi("M_y_c_Rd") / c1.OutputSi("M_y_c_Rd"), 9);
    }

    [Fact]
    public void Imperfection_Override_And_Partial_Factors_Apply()
    {
        var a = CalcRunner.Run(Calc, Uc(CalcInputValue.Of("alpha_z", 0.13)));
        var b = CalcRunner.Run(Calc, Uc());
        Assert.True(a.OutputSi("chi_z") > b.OutputSi("chi_z"));
        var g = CalcRunner.Run(Calc, Uc(CalcInputValue.Of("gamma_M1", 1.1)));
        Assert.Equal(b.OutputSi("N_b_z_Rd") / 1.1, g.OutputSi("N_b_z_Rd"), 1e-6);
    }

    [Fact]
    public void Governing_Check_Is_The_Highest_Utilisation()
    {
        var r = CalcRunner.Run(Calc, Uc());
        Assert.NotNull(r.Governing);
        Assert.Equal(r.Checks.Max(c => c.Utilisation), r.Governing!.Utilisation);
        Assert.True(r.Passed);
        var fail = CalcRunner.Run(Calc, Uc(CalcInputValue.Quantity("N_Ed", -1500, "kN")));
        Assert.False(fail.Passed);
        Assert.StartsWith("Flexural", fail.Governing!.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_Shear_Area_Blocks_With_The_Symbol()
    {
        var inputs = Calc.Template.TestCases[0].Inputs.Where(v => v.Symbol is not ("h" or "b" or "t_w" or "t_f" or "r")).ToList();
        var r = CalcRunner.Run(Calc, new CalcInputSet(inputs));
        Assert.True(r.IsBlocked);
        Assert.Contains(r.Blocks, b => b.Message.StartsWith("A_v:", StringComparison.Ordinal));
    }
}
