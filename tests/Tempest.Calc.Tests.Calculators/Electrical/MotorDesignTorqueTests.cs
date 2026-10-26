using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.7.0, chain.drive-train-D1: the design torque a coupling downstream carries is the owner's choice of basis. ka-ball-screw (T_pk 2.578945 N·m,
/// T_peak 6, T_rated 2); expected values from python3: SF_c 1.5 x 2.0 = 3.0 N·m; T_lim 4.2 N·m.
/// </summary>
public sealed class MotorDesignTorqueTests
{
    private static readonly MotorSizingCalculator Motor = new();

    private static CalcRunResult Run(int testCase, params CalcInputValue[] overrides) =>
        CalcRunner.Run(Motor, overrides.Aggregate(new CalcInputSet([.. Motor.Template.TestCases[testCase].Inputs]), (set, v) => set.With(v)));

    [Fact]
    public void The_Default_Basis_Is_The_Load_Peak_As_Before()
    {
        Assert.Equal(MotorSizingCalculator.BasisLoadPeak, Motor.Template.FindInput("T_basis")!.DefaultChoice);
        var r = Run(0);
        Assert.Equal(r.OutputSi("T_pk"), r.OutputSi("T_design"), 12);
        Assert.Equal(2.578945, r.OutputSi("T_design"), 5);
        Assert.Contains("T_design", Motor.Template.OutputPorts!.Select(p => p.Symbol));
    }

    [Fact]
    public void The_Motor_Peak_Basis_Is_The_Typed_T_peak()
    {
        var r = Run(0, CalcInputValue.Choice("T_basis", MotorSizingCalculator.BasisMotorPeak));
        Assert.Equal(6.0, r.OutputSi("T_design"), 9);
    }

    [Fact]
    public void The_Service_Factor_Basis_Is_SF_c_Times_The_Rated_Torque()
    {
        var r = Run(0, CalcInputValue.Choice("T_basis", MotorSizingCalculator.BasisServiceFactor), CalcInputValue.Of("SF_c", 1.5));
        Assert.Equal(3.0, r.OutputSi("T_design"), 9);
        Assert.Contains(r.Steps, s => s.Symbol == "T_design" && s.Description!.Contains("service factor", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Protective_Limit_Basis_Is_The_Typed_Limit()
    {
        var r = Run(0, CalcInputValue.Choice("T_basis", MotorSizingCalculator.BasisProtectiveLimit), CalcInputValue.Quantity("T_lim", 4.2, "N*m"));
        Assert.Equal(4.2, r.OutputSi("T_design"), 9);
    }

    [Theory]
    [InlineData(MotorSizingCalculator.BasisServiceFactor, "SF_c:")]
    [InlineData(MotorSizingCalculator.BasisProtectiveLimit, "T_lim:")]
    public void A_Basis_Chosen_Without_Its_Input_Blocks_And_Is_Not_Replaced_By_Another(string basis, string prefix)
    {
        foreach (var testCase in new[] { 0, 2 })
        {
            var r = Run(testCase, CalcInputValue.Choice("T_basis", basis));
            var block = Assert.Single(r.Blocks);
            Assert.Equal("design-basis", block.RuleId);
            Assert.StartsWith(prefix, block.Message, StringComparison.Ordinal);
            Assert.Null(r.FindOutput("T_design"));
        }
    }

    [Fact]
    public void Continuous_Duty_Gives_A_Design_Torque_On_Every_Basis()
    {
        // ka-continuous-pump: T_pk 1.008448 N·m, T_peak 6, T_rated 2.
        Assert.Equal(1.008448, Run(2).OutputSi("T_design"), 6);
        Assert.Equal(6.0, Run(2, CalcInputValue.Choice("T_basis", MotorSizingCalculator.BasisMotorPeak)).OutputSi("T_design"), 9);
        Assert.Equal(5.0, Run(2, CalcInputValue.Choice("T_basis", MotorSizingCalculator.BasisServiceFactor), CalcInputValue.Of("SF_c", 2.5)).OutputSi("T_design"), 9);
    }
}
