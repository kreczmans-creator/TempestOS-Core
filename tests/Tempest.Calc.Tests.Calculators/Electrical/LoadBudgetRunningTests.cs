using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E6 1.3.0: elec.load-budget-R1 (the duty d scaled the bus currents and the PSU rating, which must carry the load while it is on) and
/// elec.load-budget-03 (blank optional cells silently became 1). Expected values from python3.
/// </summary>
public sealed class LoadBudgetRunningTests
{
    private static readonly LoadBudgetCalculator Budget = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Budget, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Budget.Template), (set, v) => set.With(v)));

    private static CalcRow Row(string supply, double kw, params (string Key, CalcCell Cell)[] cells) =>
        CalcRow.Of([("name", new CalcCell(Text: "Load")), ("supply", new CalcCell(Text: supply)), ("P", new CalcCell(kw, "kW")), .. cells]);

    [Fact]
    public void A_Pump_At_Half_Duty_Is_Carried_At_Its_Running_Current_Not_Half_Of_It()
    {
        // 7.5 kW, cos phi 0.85, 400 V: running current 7500/0.85/(sqrt(3)*400) = 12.7357 A (python3); the duty-weighted figure was 6.3678 A.
        var r = Run(LoadBudgetCalculator.Loads(("Pump", LoadBudgetCalculator.Ac3, 7.5, 0.85, 0.9, 0.5, 1, 1, false)));

        Assert.Equal(12.735667702712334, r.OutputSi("I_3ph"), 6);
        // The duty still weights the demand (the transformer) and the heat: P_dem = 3.75 kW, S_dem = 3.75/0.85, Phi = 0.1 x 3.75 = 0.375 kW.
        Assert.Equal(3750, r.OutputSi("P_dem"), 6);
        Assert.Equal(3750 / 0.85, r.OutputSi("S_dem"), 6);
        Assert.Equal(375, r.OutputSi("Phi"), 6);
    }

    [Fact]
    public void Diversity_Still_Scales_The_Running_Current()
    {
        var r = Run(LoadBudgetCalculator.Loads(("Pump", LoadBudgetCalculator.Ac3, 7.5, 0.85, 0.9, 0.5, 0.8, 1, false)));
        Assert.Equal(0.8 * 12.735667702712334, r.OutputSi("I_3ph"), 6);
    }

    [Fact]
    public void A_Brake_At_Low_Duty_Sizes_The_Power_Supply_For_Its_Running_Power()
    {
        // 200 W DC load, d 0.1, k 1: the PSU must give 200 W while the brake is on, so 1.25 x 200 = 250 W (the duty-weighted figure was 25 W), and 30 W fails.
        var r = Run(LoadBudgetCalculator.Loads(("Brake", LoadBudgetCalculator.Dc, 0.2, 1, 1, 0.1, 1, 1, false)), CalcInputValue.Quantity("P_PSU", 30, "W"));

        Assert.Equal(250, r.OutputSi("P_PSU_req"), 9);
        Assert.Equal(250.0 / 30, r.OutputSi("u_PSU"), 9);
        Assert.Contains(r.Checks, c => c.Name == "Required PSU rating against the selected rating" && !c.Passed);
        Assert.Equal(200.0 / 24, r.OutputSi("I_DC"), 9);
        Assert.Equal(0.1 * 0.2 * 1000, r.OutputSi("P_dem") , 9);
    }

    [Fact]
    public void A_Power_Supply_Bus_Carries_The_Running_Input_Of_The_Dc_Load()
    {
        var r = Run(LoadBudgetCalculator.Loads(("Brake", LoadBudgetCalculator.Dc, 0.2, 1, 1, 0.1, 1, 1, false)), CalcInputValue.Choice("psu_bus", LoadBudgetCalculator.Ac3), CalcInputValue.Of("eta_psu", 0.9));
        Assert.Equal(200 / 0.9 / (Math.Sqrt(3) * 400), r.OutputSi("I_3ph"), 9);
    }

    [Fact]
    public void A_Battery_Is_Still_Sized_On_The_Duty_Weighted_Energy()
    {
        // 4 h of a 200 W backed load at d 0.1, 24 V: I_b = 0.1 x 200/24 = 0.8333 A; C = 0.8333 x 4/(0.8 x 0.95 x 1 x 0.8) = 5.482 A.h (python3).
        var r = Run(LoadBudgetCalculator.Loads(("Brake", LoadBudgetCalculator.Dc, 0.2, 1, 1, 0.1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 4, "h"));
        Assert.Equal(0.1 * 200 / 24, r.OutputSi("I_b"), 9);
        Assert.Equal(0.1 * 200 / 24 * 4 * 3600 / (0.8 * 0.95 * 0.8), r.OutputSi("C_req"), 6);
    }

    [Fact]
    public void Blank_Optional_Cells_Are_Named_In_A_Warning_Row_By_Row()
    {
        // A three-phase row with only P and the power factor: eta, d, k and inrush are blank.
        var r = Run(CalcInputValue.Table("loads", [Row(LoadBudgetCalculator.Ac3, 5, ("cosphi", new CalcCell(0.85)))]));

        Assert.False(r.IsBlocked);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "load-defaults");
        Assert.Contains("loads[1]", warning.Message, StringComparison.Ordinal);
        foreach (var cell in new[] { "η", "d ", "k ", "inrush" })
            Assert.Contains(cell, warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_Fully_Entered_Row_Raises_No_Defaults_Warning_And_A_Heat_Share_Replaces_A_Blank_Eta()
    {
        var full = Run(LoadBudgetCalculator.Loads(("Pump", LoadBudgetCalculator.Ac3, 7.5, 0.85, 0.9, 1, 1, 6, false)));
        Assert.DoesNotContain(full.Warnings, w => w.RuleId == "load-defaults");

        var withHeat = Run(CalcInputValue.Table("loads", [Row(LoadBudgetCalculator.Dc, 0.1, ("heat", new CalcCell(1)), ("d", new CalcCell(1)), ("k", new CalcCell(1)))]));
        Assert.DoesNotContain(withHeat.Warnings, w => w.RuleId == "load-defaults");
    }

    [Fact]
    public void An_Ac_Row_Without_Its_Power_Factor_Is_Refused_Not_Read_As_One()
    {
        // 5 kW three-phase with a blank power factor gave 7.217 A; with 0.85 it is 8.491 A (18 % more).
        var r = Run(CalcInputValue.Table("loads", [Row(LoadBudgetCalculator.Ac3, 5, ("eta", new CalcCell(0.9)), ("d", new CalcCell(1)), ("k", new CalcCell(1)), ("inrush", new CalcCell(1)))]));

        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.Equal("cosphi", block.RuleId);
        Assert.StartsWith("loads[1].cosphi:", block.Message, StringComparison.Ordinal);

        // A DC row needs no power factor.
        var dc = Run(CalcInputValue.Table("loads", [Row(LoadBudgetCalculator.Dc, 0.1, ("eta", new CalcCell(0.9)), ("d", new CalcCell(1)), ("k", new CalcCell(1)))]));
        Assert.False(dc.IsBlocked);
    }
}
