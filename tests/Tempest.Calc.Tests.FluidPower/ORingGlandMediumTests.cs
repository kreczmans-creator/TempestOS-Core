using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.FluidPower;

/// <summary>
/// F3 1.4.0: fp.oring-gland-02 (no input for gland eccentricity or run-out), -03 (the permitted gap did not say diametral) and -R1 (the installed state at 20 °C is not among the squeeze extremes).
/// Expected values from python3 with every tolerance set to zero so the corner is the mean state (piston seal: bore 56.9, groove root 51.5, W 3.5, d 50, b 4.8, r 0.2, α 1.5e-4, α_m 1.2e-5, −20 to 80 °C,
/// swell 0 to 5 %, incompressible reduction): cold factors f = 0.994, g = 0.99952; hot f = (1 + 1.5e-4·60)·1.05^(1/3), g = 1.00072.
/// With no eccentricity: squeeze 21.0555 % to 24.5339 %, fill 70.9813 % to 80.0058 %. With e = 0.05 mm (depth + e at the cold end, − e at the hot end): squeeze 19.5928 % to 25.9304 %, fill 69.6885 % to 81.5163 %.
/// </summary>
public sealed class ORingGlandMediumTests
{
    private static readonly ORingGlandCalculator Gland = new();

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>The defaults with every tolerance and deviation zero (the mean state is the corner) and a permitted gap so the extrusion row is filled.</summary>
    private static CalcInputSet Mean(params CalcInputValue[] overrides) => overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Gland.Template)
        .With(Q("d_r_tol", 0, "mm")).With(Q("W_tol", 0, "mm")).With(Q("D_s_u", 0, "mm")).With(Q("D_s_l", 0, "mm")).With(Q("D_g_u", 0, "mm")).With(Q("D_g_l", 0, "mm")).With(Q("D_c_u", 0, "mm")).With(Q("D_c_l", 0, "mm"))
        .With(Q("b_u", 0, "mm")).With(Q("b_l", 0, "mm")).With(Q("gap_allow", 0.2, "mm")), (set, v) => set.With(v));

    [Fact]
    public void An_Eccentricity_Makes_The_Gland_Deeper_At_The_Loose_End_And_Shallower_At_The_Tight_End()
    {
        var none = CalcRunner.Run(Gland, Mean());
        Assert.Equal(21.05548, none.OutputSi("squeeze_min") * 100, 3);
        Assert.Equal(24.53392, none.OutputSi("squeeze_max") * 100, 3);

        var offset = CalcRunner.Run(Gland, Mean(Q("ecc", 0.05, "mm")));
        Assert.Equal(19.59284, offset.OutputSi("squeeze_min") * 100, 3);
        Assert.Equal(25.93043, offset.OutputSi("squeeze_max") * 100, 3);
        Assert.Equal(69.68847, offset.OutputSi("fill_min") * 100, 3);
        Assert.Equal(81.51632, offset.OutputSi("fill_max") * 100, 3);
    }

    [Fact]
    public void The_Eccentricity_Reaches_The_Exact_Corner_Check_Too()
    {
        var offset = CalcRunner.Run(Gland, Mean(Q("ecc", 0.05, "mm")));

        double Step(string symbol) => ((Tempest.Calc.Engine.Values.ScalarValue)offset.Steps.Single(st => st.Symbol == symbol).Result).Si;

        Assert.Equal(offset.OutputSi("squeeze_min"), Step("sq_min_exact"), 6);
        Assert.Equal(offset.OutputSi("squeeze_max"), Step("sq_max_exact"), 6);
        Assert.DoesNotContain(offset.Warnings, w => w.RuleId == "worst-case");
    }

    [Fact]
    public void The_Permitted_Gap_Is_Labelled_Diametral_Where_It_Is_Entered()
    {
        Assert.Contains("diametral", Gland.Template.FindInput("gap_allow")!.Description, StringComparison.Ordinal);

        var column = Gland.Template.FindInput("ext")!.Columns!.Single(col => col.Key == "gap");
        Assert.Contains("diametral", column.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void The_Permitted_Gap_Output_Says_Diametral_And_Is_Compared_With_The_Diametral_Clearance()
    {
        var r = CalcRunner.Run(Gland, Mean());

        Assert.Contains("diametral", r.FindOutput("gap_allow")!.Description, StringComparison.Ordinal);
        Assert.Contains("diametral", r.FindOutput("gap_max")!.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-20, 80, false)]
    [InlineData(20, 80, false)]
    [InlineData(60, 100, true)]
    [InlineData(-60, 10, true)]
    public void A_Temperature_Range_That_Leaves_Out_The_Installation_Temperature_Is_Warned(double tMin, double tMax, bool warned)
    {
        // Defaults with T_min 60, T_max 100 and no swell: squeeze_min 18.320 % at the cold end against 17.643 % installed at 20 °C (the finding's numbers); the installed state is not a bounding case.
        var r = CalcRunner.Run(Gland, Mean(Q("T_min", tMin, "degC"), Q("T_max", tMax, "degC"), Q("s_max", 0, "%")));

        Assert.Equal(warned, r.Warnings.Any(w => w.RuleId == "installed-state"));

        if (warned)
            Assert.Contains("20 °C", r.Warnings.First(w => w.RuleId == "installed-state").Message, StringComparison.Ordinal);
    }
}
