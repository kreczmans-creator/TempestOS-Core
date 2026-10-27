using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E2 1.6.0, elec.enclosure-heat-R2 and chain.cooling-04: with T_max at or below the ambient the run blocked ("T_max must exceed the ambient"), which is the usual case for a cooling unit. The unit then removes the
/// heat inside plus the heat the walls gain from the hotter ambient. python3, the ka-sheet enclosure (300 W, A_eff = 0.18 + 0.48 + 0.48 = 1.14 m², h 5.5 W/(m²·K)): h·A_eff = 6.27 W/K;
/// T_amb 45, T_max 40: P_cool = 300 + 6.27 × 5 = 331.35 W; T_amb = T_max: P_cool = 300 W; T_amb 35, T_max 50 is unchanged at 300 − 6.27 × 15 = 205.95 W.
/// </summary>
public sealed class EnclosureHotAmbientTests
{
    private static readonly EnclosureHeatCalculator Enclosure = new();

    private static CalcRunResult Run(double ambient, double limit)
    {
        var d = Enclosure.Template.TestCases[0].Inputs.ToDictionary(v => v.Symbol);
        d["T_amb"] = CalcInputValue.Quantity("T_amb", ambient, "degC");
        d["T_max"] = CalcInputValue.Quantity("T_max", limit, "degC");
        return CalcRunner.Run(Enclosure, new CalcInputSet([.. d.Values]));
    }

    [Fact]
    public void An_Ambient_Above_The_Limit_Gives_The_Cooling_Duty_With_The_Heat_Gained_Through_The_Walls()
    {
        var r = Run(45, 40);

        Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
        Assert.Equal(300, r.OutputSi("P_total"), 9);
        Assert.Equal(-6.27 * 5, r.OutputSi("P_nat_max"), 9);
        Assert.Equal(331.35, r.OutputSi("P_cool"), 9);
        Assert.Equal(40, r.OutputSi("T_max") - 273.15, 9);

        var warning = Assert.Single(r.Warnings, w => w.RuleId == "limit");
        Assert.Contains("331.4", warning.Message, StringComparison.Ordinal);

        // No ventilation figure is given for a limit the ambient already exceeds, and natural dissipation is reported as not holding it.
        Assert.Null(r.FindOutput("V_req"));
        Assert.Null(r.FindOutput("V_req_net"));
        Assert.Null(r.FindOutput("rho_air"));
        Assert.Contains(r.Checks, c => c.Name == "Maximum internal temperature against the ambient" && !c.Passed);
        Assert.False(r.Passed);
    }

    [Fact]
    public void An_Ambient_Equal_To_The_Limit_Needs_The_Whole_Load_Removed()
    {
        var r = Run(40, 40);

        Assert.False(r.IsBlocked);
        Assert.Equal(0, r.OutputSi("P_nat_max"), 12);
        Assert.Equal(300, r.OutputSi("P_cool"), 9);
    }

    [Fact]
    public void A_Limit_Above_The_Ambient_Is_Unchanged()
    {
        var r = Run(35, 50);

        Assert.Equal(205.95, r.OutputSi("P_cool"), 9);
        Assert.Equal(62.48, r.OutputSi("V_req") * 3600, 2);
        Assert.Contains(r.Checks, c => c.Name == "Natural temperature rise against the allowed rise" && c.Utilisation > 1);
        Assert.DoesNotContain(r.Warnings, w => w.RuleId == "limit");
    }
}
