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

    private static CalcRow Row(string name, string supply, double kw, double eta, double? heat, double cosphi = 1)
    {
        var cells = new List<(string, CalcCell)>
        {
            ("name", new CalcCell(Text: name)), ("supply", new CalcCell(Text: supply)), ("P", new CalcCell(kw, "kW")), ("cosphi", new CalcCell(cosphi)), ("eta", new CalcCell(eta)),
            ("d", new CalcCell(1)), ("k", new CalcCell(1)), ("inrush", new CalcCell(1)),
        };

        if (heat is { } h)
            cells.Add(("heat", new CalcCell(h)));

        return CalcRow.Of([.. cells]);
    }

    [Fact]
    public void A_Linked_Machine_Is_Outside_The_Enclosure_And_Adds_No_Panel_Heat()
    {
        // Green G-B-04: the linked motor's own loss, (1 - 0.97) * 2 kW / 0.97 = 61.9 W, was counted as heat the panel must lose.
        var noRows = Defaults().With(CalcInputValue.Table("loads", [])).With(CalcInputValue.Quantity("P_add", 2, "kW")).With(CalcInputValue.Of("eta_add", 0.97));
        var outside = CalcRunner.Run(Budget, noRows);
        Assert.Equal(0, outside.OutputSi("Phi"), 12);
        Assert.Equal(2 / 0.97 * 1e3, outside.OutputSi("P_conn"), 6);

        var inside = CalcRunner.Run(Budget, noRows.With(CalcInputValue.Of("heat_add", 1)));
        Assert.Equal(2 / 0.97 * 1e3, inside.OutputSi("Phi"), 6);
    }

    [Fact]
    public void A_Load_Releases_Its_Stated_Share_Of_Its_Input_As_Panel_Heat()
    {
        // 180 W of electronics inside the panel release all 180 W (heat 1); left blank the share is the loss 1 - eta = 18 W.
        var electronics = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Table("loads", [Row("24 V controls", LoadBudgetCalculator.Dc, 0.18, 0.9, 1)])));
        var blank = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Table("loads", [Row("24 V controls", LoadBudgetCalculator.Dc, 0.18, 0.9, null)])));
        var outside = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Table("loads", [Row("Motor", LoadBudgetCalculator.Ac3, 5, 0.9, 0, 0.85)])));
        Assert.Equal(0.18, electronics.OutputSi("Phi") / 1e3, 12);
        Assert.Equal(0.018, blank.OutputSi("Phi") / 1e3, 12);
        Assert.Equal(0, outside.OutputSi("Phi"), 12);
        Assert.Contains(CalcInputValidator.Validate(Budget.Template, Defaults().With(CalcInputValue.Table("loads", [Row("x", LoadBudgetCalculator.Dc, 0.1, 0.9, 1.5)]))), n => n.Severity == CalcNoticeSeverity.Block);
    }

    [Fact]
    public void The_Power_Supply_Input_Is_Added_To_The_Bus_It_Is_Fed_From()
    {
        // Green G-B-12: 120 W of DC load needs 120 / 0.9 = 133.3 VA from the AC bus that feeds the PSU.
        var dc = CalcInputValue.Table("loads", [Row("Controls", LoadBudgetCalculator.Dc, 0.12, 1, 1)]);
        var none = CalcRunner.Run(Budget, Defaults().With(dc));
        Assert.Contains(none.Warnings, w => w.RuleId == "psu_bus");
        Assert.Equal(0, none.OutputSi("I_1ph"), 12);
        Assert.Equal(0, none.OutputSi("I_3ph"), 12);

        var single = CalcRunner.Run(Budget, Defaults().With(dc).With(CalcInputValue.Choice("psu_bus", "AC single-phase")).With(CalcInputValue.Of("eta_psu", 0.9)));
        Assert.DoesNotContain(single.Warnings, w => w.RuleId == "psu_bus");
        Assert.Equal(0.12e3 / 0.9 / 230, single.OutputSi("I_1ph"), 9);
        Assert.Equal(0, single.OutputSi("I_3ph"), 12);
        Assert.Equal(0.12 / 0.9, single.OutputSi("S_dem") / 1e3, 9);
        Assert.Equal(0.12e3 / 24, single.OutputSi("I_DC"), 9);

        var three = CalcRunner.Run(Budget, Defaults().With(dc).With(CalcInputValue.Choice("psu_bus", "AC three-phase")).With(CalcInputValue.Of("eta_psu", 0.9)));
        Assert.Equal(0.12e3 / 0.9 / (Math.Sqrt(3) * 400), three.OutputSi("I_3ph"), 9);
        Assert.Equal(0, three.OutputSi("I_1ph"), 12);
    }

    [Fact]
    public void The_Minimum_Transformer_Rating_Names_What_It_Covers_And_The_Start_Is_Sized_Too()
    {
        // Black B-A-09: S_T_req (13.42 kVA) was called the minimum rating but the sheet's own starting check needs S_peak / k_T = 18.29 kVA.
        var r = CalcRunner.Run(Budget, Defaults());
        Assert.Equal(1.25 * r.OutputSi("S_dem"), r.OutputSi("S_T_req"), 6);
        Assert.Equal(r.OutputSi("S_peak") / 3, r.OutputSi("S_T_req_start"), 6);
        Assert.Equal(Math.Max(r.OutputSi("S_T_req"), r.OutputSi("S_T_req_start")), r.OutputSi("S_T_min"), 6);
        Assert.True(r.OutputSi("S_T_min") > r.OutputSi("S_T_req"));
        Assert.Equal("Minimum transformer rating, running load", r.FindOutput("S_T_req")!.Description);

        // A transformer of exactly the minimum passes both of the sheet's own checks.
        var atMinimum = CalcRunner.Run(Budget, Defaults().With(CalcInputValue.Quantity("S_T", r.OutputSi("S_T_min") / 1e3 * (1 + 1e-9), "kVA")));
        Assert.All(atMinimum.Checks.Where(c => c.Name.Contains("ransformer", StringComparison.Ordinal) || c.Name.StartsWith("Starting", StringComparison.Ordinal)), c => Assert.True(c.Passed, c.Name));
    }

    [Fact]
    public void Starting_Peak_Uses_The_Undiversified_Motor()
    {
        // Opus review finding 6: a start draws m·P/cosφ whatever the duty; 7.5 kW, cosφ 0.85, d 0.5, m 6.
        var r = CalcRunner.Run(Budget, Defaults().With(LoadBudgetCalculator.Loads(("Pump", LoadBudgetCalculator.Ac3, 7.5, 0.85, 1, 0.5, 1, 6, false))));
        var s = 7.5 / 0.85;
        Assert.Equal((0.5 * s + (6 * s - 0.5 * s)) * 1e3, r.OutputSi("S_peak"), 3);
    }
}
