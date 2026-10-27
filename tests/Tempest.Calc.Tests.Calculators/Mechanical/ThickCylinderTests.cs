using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class ThickCylinderTests
{
    private static readonly ThickCylinderCalculator Cylinder = new();

    private static CalcInputSet Thin(double pOuter, double pInner = 0) => CalcTemplateHasher.DefaultInputs(Cylinder.Template)
        .With(CalcInputValue.Quantity("a", 95, "mm")).With(CalcInputValue.Quantity("b", 100, "mm")).With(CalcInputValue.Quantity("p_i", pInner, "MPa")).With(CalcInputValue.Quantity("p_o", pOuter, "MPa"))
        .With(CalcInputValue.Switch("closed", true)).With(CalcInputValue.Quantity("sigma_allow", 200, "MPa"));

    [Fact]
    public void An_External_Pressure_On_A_Moderately_Thin_Wall_Warns_That_Elastic_Collapse_Is_Not_Checked_And_Reports_The_Collapse_Pressure()
    {
        // mech.thick-cylinder-01: a 95, b 100 mm, p_o 7.5 MPa, closed ends, allowable 200 MPa: u 0.666, a pass, with no word that the long-tube elastic collapse pressure E/(4(1 - nu^2)) (t/r_m)^3 is 7.67 MPa.
        // python3: t = 5, r_m = 97.5 mm, E 207 GPa, nu 0.3: 207e3/(4 x 0.91) x (5/97.5)^3 = 56 868 x 1.3487e-4 = 7.6695 MPa, 7.5/7.6695 = 0.9779.
        var r = CalcRunner.Run(Cylinder, Thin(7.5));
        Assert.False(r.IsBlocked);
        Assert.Equal(0.666, r.OutputSi("u"), 3);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "stability");
        Assert.Contains("collapse", warning.Message, StringComparison.Ordinal);
        Assert.Contains("7.6695", warning.Message, StringComparison.Ordinal);
        Assert.Equal(7.669466, r.OutputSi("p_cr") / 1e6, 5);
        Assert.Equal(7.5 / 7.669466, r.OutputSi("u_cr"), 6);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Net external pressure against the elastic collapse", StringComparison.Ordinal) && c.Passed);

        // A little more pressure than the elastic collapse fails the sheet whatever the stress says.
        var over = CalcRunner.Run(Cylinder, Thin(8));
        Assert.Contains(over.Checks, c => c.Name.StartsWith("Net external pressure against the elastic collapse", StringComparison.Ordinal) && !c.Passed);
        Assert.False(over.Passed);

        // Internal pressure, a thick wall or no external pressure: no stability notice, as before.
        Assert.DoesNotContain(CalcRunner.Run(Cylinder, CalcTemplateHasher.DefaultInputs(Cylinder.Template)).Warnings, w => w.RuleId == "stability");
        Assert.DoesNotContain(CalcRunner.Run(Cylinder, Thin(0, 7.5)).Warnings, w => w.RuleId == "stability");
    }
}
