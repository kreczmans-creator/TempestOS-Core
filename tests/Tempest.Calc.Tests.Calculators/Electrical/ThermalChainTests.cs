using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

public sealed class ThermalChainTests
{
    private static readonly ThermalChainCalculator Chain = new();

    private static CalcInputSet Inputs(double power, params (string, double)[] stages) =>
        CalcTemplateHasher.DefaultInputs(Chain.Template).With(CalcInputValue.Quantity("P", power, "W")).With(ThermalChainCalculator.Stages(stages));

    [Fact]
    public void The_Last_Node_Less_Its_Rise_Is_The_Ambient()
    {
        Gen.Select(Gen.Double[0, 200], Gen.Double[0, 3], Gen.Double[0.01, 3]).Sample((p, r1, r2) =>
        {
            var r = CalcRunner.Run(Chain, Inputs(p, ("jc", r1), ("sa", r2)));
            return Math.Abs(r.OutputSi("T_2") - r.OutputSi("dT_2") - 313.15) < 1e-9 * r.OutputSi("T_2");
        }, iter: 50);
    }

    [Fact]
    public void At_The_Budget_The_Utilisation_Is_One()
    {
        var r = CalcRunner.Run(Chain, Inputs(25, ("jc", 0.5), ("sa", 2)));
        var budget = r.OutputSi("R_max");
        var atBudget = CalcRunner.Run(Chain, Inputs(25, ("jc", 0.5), ("sa", budget - 0.5)));
        Assert.Equal(1.0, atBudget.OutputSi("u"), 9);
        Assert.Equal(0.0, atBudget.OutputSi("dT_m"), 9);
    }

    [Fact]
    public void Zero_Power_Leaves_The_Budget_Unbounded()
    {
        var r = CalcRunner.Run(Chain, Inputs(0, ("jc", 1)));
        Assert.False(r.IsBlocked);
        Assert.Equal(313.15, r.OutputSi("T_1"), 9);
        Assert.Equal("unbounded", Assert.IsType<TextValue>(r.FindOutput("R_max")!.Value).Value);
    }

    [Fact]
    public void Inconsistent_Chains_Block()
    {
        Assert.Contains(CalcRunner.Run(Chain, Inputs(10)).Blocks, b => b.RuleId == "no-stages");
        Assert.Contains(CalcRunner.Run(Chain, Inputs(10, ("pad", 0))).Blocks, b => b.RuleId == "zero-chain");
        Assert.Contains(CalcRunner.Run(Chain, Inputs(10, ("jc", 1)).With(CalcInputValue.Quantity("T_max", 30, "degC")).With(CalcInputValue.Quantity("T_a", 40, "degC"))).Blocks, b => b.RuleId == "limit");
    }
}
