using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E6 1.4.0: chain.motor-circuit-04 (the linked machine entered at its duty power, so a 20 N·m motor and a 2 N·m motor on the same duty gave the same cable) and -07 (the supply voltage and power factor did not
/// reach the voltage-drop and fault steps). Expected values from python3: linked duty 784.1424 W, η 0.97, cos φ 0.85, 400 V: I_3φ = 784.1424/0.97/0.85/(√3·400) = 1.37273 A; with a rated shaft power of
/// 6283.185 W (20 N·m at 3000 rpm) the supply carries the rating: 6283.185/0.97/0.85/(√3·400) = 10.99939 A.
/// </summary>
public sealed class LoadBudgetLinkedMachineTests
{
    private static readonly LoadBudgetCalculator Budget = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Budget, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Budget.Template).With(LoadBudgetCalculator.Loads())
        .With(CalcInputValue.Quantity("P_add", 784.1424404, "W")).With(CalcInputValue.Of("cosphi_add", 0.85)).With(CalcInputValue.Of("eta_add", 0.97)).With(CalcInputValue.Quantity("U_3ph", 400, "V")), (set, v) => set.With(v)));

    [Fact]
    public void A_Linked_Machine_With_No_Rating_Is_Sized_On_Its_Duty_And_The_Sheet_Says_So()
    {
        var r = Run();

        Assert.Equal(1.3727254, r.OutputSi("I_3ph"), 6);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "rated-add");
        Assert.Contains("rated", warning.Message, StringComparison.Ordinal);
        Assert.Contains("P_rated_add", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Machines_Rated_Shaft_Power_Sizes_The_Supply_When_It_Is_Above_The_Duty()
    {
        var r = Run(CalcInputValue.Quantity("P_rated_add", 6283.185307, "W"));

        Assert.Equal(10.999390, r.OutputSi("I_3ph"), 5);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "rated-add");
    }

    [Fact]
    public void A_Rating_Below_The_Duty_Does_Not_Lower_The_Supply()
    {
        var r = Run(CalcInputValue.Quantity("P_rated_add", 300, "W"));

        Assert.Equal(1.3727254, r.OutputSi("I_3ph"), 6);
    }

    [Fact]
    public void The_Supply_Voltage_And_The_Linked_Power_Factor_Are_Ports()
    {
        var r = Run();

        Assert.Equal(400, r.OutputSi("U_L"), 9);
        Assert.Equal(0.85, r.OutputSi("pf_link"), 9);
        Assert.Contains(Budget.Template.OutputPorts!, p => p is { Symbol: "U_L", Unit: "V" });
        Assert.Contains(Budget.Template.OutputPorts!, p => p.Symbol == "pf_link");
    }
}
