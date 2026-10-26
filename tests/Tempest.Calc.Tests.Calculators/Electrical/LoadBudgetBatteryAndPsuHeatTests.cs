using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E6 1.4.0: elec.load-budget-04 (the PSU rating leaves out the battery's charging power), -05 (the PSU's own loss was missing from the heat released inside the enclosure)
/// and -06 (Peukert gave a capacity credit beyond the rated hour rate). Expected values from python3.
/// </summary>
public sealed class LoadBudgetBatteryAndPsuHeatTests
{
    private static readonly LoadBudgetCalculator Budget = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Budget, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Budget.Template), (set, v) => set.With(v)));

    [Fact]
    public void The_Power_Supplys_Own_Loss_Is_Heat_Inside_The_Enclosure()
    {
        // 120 W DC electronics (heat h = 1 - eta = 0.1 of the input): 12 W; the 24 V supply on the three-phase bus at eta 0.9 loses 120 x (1/0.9 - 1) = 13.3333 W (python3).
        var r = Run(LoadBudgetCalculator.Loads(("Controls", LoadBudgetCalculator.Dc, 0.12, 1, 0.9, 1, 1, 1, false)),
            CalcInputValue.Choice("psu_bus", LoadBudgetCalculator.Ac3), CalcInputValue.Of("eta_psu", 0.9));

        Assert.Equal(12 + 13.333333333333334, r.OutputSi("Phi"), 6);
    }

    [Fact]
    public void Without_A_Bus_For_The_Supply_The_Heat_Is_As_Before()
    {
        var r = Run(LoadBudgetCalculator.Loads(("Controls", LoadBudgetCalculator.Dc, 0.12, 1, 0.9, 1, 1, 1, false)));

        Assert.Equal(12, r.OutputSi("Phi"), 6);
    }

    [Fact]
    public void A_Battery_Backed_Load_Warns_That_The_Power_Supply_Rating_Leaves_Out_The_Charge()
    {
        // 168 W backed load, 4 h, PSU 240 W: u_PSU = 1.25 x 168 / 240 = 0.875 passes, and the battery's charging power is not in it.
        var r = Run(LoadBudgetCalculator.Loads(("Backed", LoadBudgetCalculator.Dc, 0.168, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 4, "h"), CalcInputValue.Quantity("P_PSU", 240, "W"));

        Assert.Equal(0.875, r.OutputSi("u_PSU"), 9);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "battery-charge");
        Assert.Contains("charg", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_Charge_Notice_Without_A_Battery_Or_A_Backed_Load()
    {
        Assert.DoesNotContain(Run(LoadBudgetCalculator.Loads(("Plain", LoadBudgetCalculator.Dc, 0.168, 1, 1, 1, 1, 1, false)), CalcInputValue.Quantity("t_auto", 4, "h")).Warnings, w => w.RuleId == "battery-charge");
        Assert.DoesNotContain(Run(LoadBudgetCalculator.Loads(("Backed", LoadBudgetCalculator.Dc, 0.168, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 0, "h")).Warnings, w => w.RuleId == "battery-charge");
    }

    [Fact]
    public void Peukert_Beyond_The_Rated_Hour_Rate_Never_Gives_Less_Than_The_Plain_Product()
    {
        // 10 A (240 W at 24 V), 72 h on a 20 h rating, k_p 1.3: Peukert 10 x 20 x (72/20)^(1/1.3) = 535.74 A.h, below I x t = 720 A.h (python3).
        var r = Run(LoadBudgetCalculator.Loads(("Backed", LoadBudgetCalculator.Dc, 0.24, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 72, "h"), CalcInputValue.Of("k_p", 1.3), CalcInputValue.Quantity("H_rate", 20, "h"));

        Assert.Equal(720 * 3600, r.OutputSi("Ah"), 3);
        var warning = Assert.Single(r.Warnings, w => w.RuleId == "peukert-range");
        Assert.Contains("rated hour rate", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Peukert_Inside_The_Rated_Hour_Rate_Is_Unchanged()
    {
        // 10 A for 10 h on a 20 h rating, k_p 1.3: 10 x 20 x (10/20)^(1/1.3) = 117.2 A.h.
        var r = Run(LoadBudgetCalculator.Loads(("Backed", LoadBudgetCalculator.Dc, 0.24, 1, 1, 1, 1, 1, true)), CalcInputValue.Quantity("t_auto", 10, "h"), CalcInputValue.Of("k_p", 1.3), CalcInputValue.Quantity("H_rate", 20, "h"));

        Assert.Equal(10 * 20 * Math.Pow(10.0 / 20, 1 / 1.3) * 3600, r.OutputSi("Ah"), 3);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "peukert-range");
    }
}
