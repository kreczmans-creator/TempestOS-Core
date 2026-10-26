using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// M3a 1.2.1: mech.bolted-joint-01 (the thread torsion modulus uses the minor diameter, which the spec did not state) and -R2 (F_M is the maximum preload, but nothing said that the nut factor and the
/// friction coefficients must be the smallest values the joint can have). Expected values from python3 with the defaults of the m12-88 case: d_3 = 12 - 1.2269 x 1.75 = 9.8529 mm, d_S = (d_2 + d_3)/2 = 10.358 mm
/// with d_2 = 10.8634 mm, so W_p(d_3) = 187.81 mm3 against 218.21 mm3 at d_S: tau_M 16.2 % higher and nu_M 0.9687 against 0.9180 (5.5 %), conservative.
/// </summary>
public sealed class BoltedJointLabelTests
{
    private static readonly BoltedJointCalculator Joint = new();

    [Theory]
    [InlineData("K")]
    [InlineData("mu_G")]
    [InlineData("mu_K")]
    public void The_Nut_Factor_And_The_Friction_Coefficients_Are_Labelled_As_The_Smallest_Values(string symbol)
    {
        var description = Joint.Template.FindInput(symbol)!.Description;

        Assert.Contains("smallest", description, StringComparison.Ordinal);
        Assert.Contains("maximum preload", description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Method_And_Assumptions_State_That_Thread_Torsion_Uses_The_Minor_Diameter()
    {
        Assert.Contains("W_p = π/16·d_3³", Joint.Template.Method, StringComparison.Ordinal);
        Assert.Contains(Joint.Template.Assumptions, a => a.Statement.Contains("minor diameter d_3", StringComparison.Ordinal) && a.Statement.Contains("conservative", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Assumptions_State_That_The_Assembly_Preload_Is_The_Maximum_One()
    {
        Assert.Contains(Joint.Template.Assumptions, a => a.Statement.Contains("F_M is the maximum preload", StringComparison.Ordinal));
    }
}
