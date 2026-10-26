using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M7 1.4.0, mech.spring-R1 (verification): the extension Goodman row covered the body only and the hook, which usually governs, still passed with a warning. The hook bending stress and the side-bend torsion
/// stress now have their own Goodman rows between their values at the two working points, counted in the verdict. python3, ka-extension (d 2, D 16, F₁ 30 N, F₂ 60 N, r₁ 8 mm, r₂ 4 mm, R_m 1800 MPa,
/// S_e 400 MPa): K_A = 1.1026786, K_B = 1.25; σ_A = 346.50305 (F₁) and 693.00610 MPa (F₂): a = 173.25152, m = 519.75457; τ_B = 190.98593 and 381.97186 MPa: a = 95.49297, m = 286.47890;
/// side bend: 1/n_f = 95.49297/400 + 286.47890/(0.67 × 1800) = 0.476277 (n_f 2.0996, 1.2 × u = 0.5715, pass); hook bending with S_e_b 200 MPa: 173.25152/200 + 519.75457/1800 = 1.155010
/// (n_f 0.8658, 1.2 × u = 1.38601, FAIL) while the body (u_f 0.446715) passes.
/// </summary>
public sealed class SpringHookFatigueTests
{
    private static readonly SpringCalculator Spring = new();

    private static CalcRunResult Run(params CalcInputValue[] more) =>
        CalcRunner.Run(Spring, more.Aggregate(new CalcInputSet([.. Spring.Template.TestCases.Single(t => t.Id == "ka-extension").Inputs]), (set, v) => set.With(v)));

    [Fact]
    public void The_Hook_Bending_Fatigue_Fails_The_Spring_While_The_Body_Passes()
    {
        var r = Run(CalcInputValue.Quantity("S_e_b", 200, "MPa"));

        Assert.False(r.IsBlocked);
        Assert.Equal(0.446715079, r.OutputSi("u_f"), 7);
        Assert.Equal(1.155010158, r.OutputSi("u_f_A"), 7);
        Assert.Equal(0.865793251, r.OutputSi("n_f_A"), 7);
        var body = Assert.Single(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the body only", StringComparison.Ordinal));
        Assert.True(body.Passed);
        var hook = Assert.Single(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the hook bending", StringComparison.Ordinal));
        Assert.False(hook.Passed);
        Assert.Equal(1.386012190, hook.Utilisation, 7);
        Assert.False(r.Passed);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "hook-fatigue");
    }

    [Fact]
    public void The_Hook_Side_Bend_Has_Its_Own_Goodman_Row_On_The_Shear_Endurance()
    {
        var r = Run(CalcInputValue.Quantity("S_e_b", 700, "MPa"));

        Assert.Equal(0.476277106, r.OutputSi("u_f_B"), 7);
        Assert.Equal(2.099618033, r.OutputSi("n_f_B"), 7);
        var side = Assert.Single(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the hook side-bend torsion", StringComparison.Ordinal));
        Assert.True(side.Passed);
        Assert.Equal(0.571532527, side.Utilisation, 7);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Without_A_Hook_Bending_Endurance_The_Bending_Row_Is_Left_Out_And_The_Sheet_Says_So()
    {
        var r = Run();

        Assert.Null(r.FindOutput("u_f_A"));
        Assert.DoesNotContain(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the hook bending", StringComparison.Ordinal));
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "hook-fatigue");
        Assert.Contains("not assessed", warning.Message, StringComparison.Ordinal);
        Assert.Contains("S_e_b", warning.Message, StringComparison.Ordinal);

        // The side bend is still counted.
        Assert.Contains(r.Checks, c => c.Name.StartsWith("Goodman fatigue of the hook side-bend torsion", StringComparison.Ordinal));
    }

    [Fact]
    public void Compression_And_Torsion_Have_No_Hook_Rows()
    {
        var compression = CalcRunner.Run(Spring, new CalcInputSet([.. Spring.Template.TestCases.Single(t => t.Id == "ka-compression").Inputs]));

        Assert.DoesNotContain(compression.Checks, c => c.Name.Contains("hook", StringComparison.Ordinal));
        Assert.Null(compression.FindOutput("u_f_B"));
    }
}
