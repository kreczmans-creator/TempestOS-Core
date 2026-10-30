using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E1 1.7.0, chain.drive-train-D1 (1.8.0, elec.motor-sizing N2: the load-peak basis is max(|T_pk|, |T_dec|)): the design torque a coupling downstream carries is the owner's choice of basis. ka-ball-screw (T_pk 2.578945 N·m,
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
    public void The_Load_Peak_Basis_Takes_The_Braking_Torque_When_It_Is_Larger_On_An_Overhauling_Drive()
    {
        // 200 kg at -20 degrees (python3): T_pk 4.019220970, T_dec -5.739896819 N.m, so the coupling carries 5.7399 (T_pk alone was 4.0192, 30 % short).
        var r = Run(0, CalcInputValue.Quantity("m", 200, "kg"), CalcInputValue.Quantity("theta", -20, "deg"), CalcInputValue.Quantity("T_rated", 5.5, "N*m"),
            CalcInputValue.Quantity("T_peak", 6, "N*m"), CalcInputValue.Switch("brake", true));
        Assert.Equal(4.019220970, r.OutputSi("T_pk"), 6);
        Assert.Equal(-5.739896819, r.OutputSi("T_dec"), 6);
        Assert.Equal(5.739896819, r.OutputSi("T_design"), 6);
        Assert.Contains(r.Steps, s => s.Symbol == "T_design" && s.Description!.Contains("braking", StringComparison.Ordinal));
        Assert.Contains("max(|T_pk|, |T_dec|)", Motor.Template.OutputPorts!.Single(p => p.Symbol == "T_design").Description, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Load_Peak_Basis_Stays_T_pk_When_The_Accelerating_Torque_Is_The_Larger()
    {
        // ka-ball-screw, 90 degrees up: T_dec = -T_acc + T_load is smaller in magnitude than T_pk = T_acc + T_load while T_load > 0.
        var r = Run(0);
        Assert.True(Math.Abs(r.OutputSi("T_dec")) < r.OutputSi("T_pk"));
        Assert.Equal(r.OutputSi("T_pk"), r.OutputSi("T_design"), 12);
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
