using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class PressFitTests
{
    private static readonly PressFitCalculator Fit = new();

    [Fact]
    public void A_Hollow_Shaft_Is_Checked_At_Its_Bore_Where_The_Hoop_Stress_Is_Largest()
    {
        // mech.press-fit-01: d 50, bore 25, hub 100 mm, delta 120 um, S_y 350 MPa. The shaft check read the von Mises stress at the outer surface (216.55 MPa, u 0.619, a pass) while the bore carries 2 p R^2/(R^2 - r_i^2) = 397.4 MPa (u 1.136).
        // python3: R 25, r_i 12.5, r_o 50 mm, E 207 GPa, nu 0.292: p = 0.06/(C_h + C_s) = 149.04 MPa; surface sigma_t = -p (R^2 + r_i^2)/(R^2 - r_i^2) = -248.4, sigma_r = -149.04: von Mises 216.55; bore sigma_t = -2 p R^2/(R^2 - r_i^2) = -397.44.
        var r = CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template).With(CalcInputValue.Quantity("d_i", 25, "mm")).With(CalcInputValue.Quantity("delta", 120, "µm")));
        Assert.False(r.IsBlocked);
        Assert.Equal(149.04, r.OutputSi("p_int") / 1e6, 2);
        Assert.Equal(216.55, r.OutputSi("sigma_vm_shaft") / 1e6, 2);
        Assert.Equal(397.44, r.OutputSi("sigma_vm_shaft_bore") / 1e6, 2);
        Assert.Equal(397.44, r.OutputSi("sigma_vm_shaft_max") / 1e6, 2);
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Largest shaft von Mises stress", StringComparison.Ordinal) && !c.Passed);
        Assert.False(r.Passed);

        // A solid shaft has no bore: its centre is in equal hoop and radial stress p, the same von Mises as the surface, so nothing changes for it.
        var solid = CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template));
        Assert.Equal(solid.OutputSi("sigma_vm_shaft"), solid.OutputSi("sigma_vm_shaft_max"), 6);
        Assert.Equal(62.1, solid.OutputSi("sigma_vm_shaft") / 1e6, 1);

        // A light interference keeps the hollow shaft inside its yield at the bore too (the 40 um case: p 49.68 MPa, bore 132.48 MPa against 350).
        var light = CalcRunner.Run(Fit, CalcTemplateHasher.DefaultInputs(Fit.Template).With(CalcInputValue.Quantity("d_i", 25, "mm")));
        Assert.Equal(132.48, light.OutputSi("sigma_vm_shaft_bore") / 1e6, 2);
        Assert.True(light.Passed);
    }
}
