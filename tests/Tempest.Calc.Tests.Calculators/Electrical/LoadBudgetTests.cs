using CsCheck;
using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

public sealed class LoadBudgetTests
{
    private static readonly LoadBudgetCalculator Budget = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Budget.Template);

    [Fact]
    public void Demand_Never_Exceeds_Connected_Load()
    {
        Gen.Select(Gen.Double[0, 200], Gen.Double[0, 1], Gen.Double[0, 1]).Sample((p, d, k) =>
        {
            var r = CalcRunner.Run(Budget, Defaults().With(LoadBudgetCalculator.Loads(("A", LoadBudgetCalculator.Ac3, p, 0.9, 0.9, d, k, 1, false))));
            return r.OutputSi("P_dem") <= r.OutputSi("P_conn") + 1e-9;
        }, iter: 60);
    }

    [Fact]
    public void Apparent_Power_Is_At_Least_The_Active_Power()
    {
        var r = CalcRunner.Run(Budget, Defaults());
        Assert.True(r.OutputSi("S_dem") >= r.OutputSi("P_dem"));
    }

    [Fact]
    public void Inrush_Of_The_Largest_Motor_Fails_A_Small_Transformer()
    {
        var r = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Quantity("S_T", 15, "kVA")));
        Assert.Contains(r.Checks, c => c.Name == "Transformer rating against required" && c.Passed);
        Assert.Contains(r.Checks, c => c.Name == "Starting peak against transformer overload" && !c.Passed);
    }

    [Fact]
    public void Peukert_With_Exponent_One_Equals_The_Linear_Capacity()
    {
        var linear = CalcRunner.Run(Budget, Defaults());
        var peukert = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Of("k_p", 1)).With(CalcInputValue.Quantity("H_rate", 20, "h")));
        Assert.Equal(linear.OutputSi("C_req"), peukert.OutputSi("C_req"), 6);
    }

    [Fact]
    public void Half_A_Peukert_Pair_Warns_And_Is_Not_Applied()
    {
        var r = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Of("k_p", 1.2)));
        Assert.Contains(r.Warnings, w => w.RuleId == "peukert");
        Assert.Equal(CalcRunner.Run(Budget, Defaults()).OutputSi("C_req"), r.OutputSi("C_req"), 9);
    }

    [Fact]
    public void Hot_Ambient_Without_Derating_Warns()
    {
        var r = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Quantity("theta", 55, "degC")));
        Assert.Contains(r.Warnings, w => w.RuleId == "derate");
    }

    [Fact]
    public void No_Autonomy_Means_No_Battery_Outputs()
    {
        var r = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Quantity("t_auto", 0, "h")));
        Assert.Null(r.FindOutput("C_req"));
    }

    [Fact]
    public void An_Empty_Load_Table_Blocks()
    {
        Assert.True(CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Table("loads", []))).IsBlocked);
    }
}
