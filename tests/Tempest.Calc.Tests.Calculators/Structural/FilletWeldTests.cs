using CsCheck;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Structural;

public sealed class FilletWeldTests
{
    private static readonly FilletWeldCalculator Weld = new();

    private static CalcInputSet Inputs() => CalcTemplateHasher.DefaultInputs(Weld.Template);

    [Fact]
    public void Utilisation_Scales_Linearly_With_The_Resultant_And_Ignores_Its_Direction()
    {
        Gen.Select(Gen.Double[1, 500], Gen.Double[0, Math.PI / 2]).Sample((f, angle) =>
        {
            var split = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", f * Math.Cos(angle), "kN")).With(CalcInputValue.Quantity("F_n", f * Math.Sin(angle), "kN")));
            var single = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", f, "kN")));
            return Math.Abs(split.OutputSi("u") - single.OutputSi("u")) < 1e-9 * single.OutputSi("u");
        }, iter: 50);
    }

    [Fact]
    public void Resistance_Equals_The_Load_At_The_Calculated_Throat()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", 400, "kN")));
        var aCalc = r.OutputSi("a_calc");
        Assert.True(aCalc > 3e-3);
        var atThroat = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", 400, "kN")).With(CalcInputValue.Quantity("a", aCalc * 1000, "mm")).With(CalcInputValue.Quantity("L", 200, "mm")));
        Assert.Equal(1.0, atThroat.OutputSi("u"), 9);
        Assert.Equal(aCalc, r.OutputSi("a_req"), 12);
    }

    [Fact]
    public void Required_Throat_Is_Never_Below_Three_Millimetres()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", 1, "kN")));
        Assert.Equal(3e-3, r.OutputSi("a_req"), 12);
    }

    [Theory]
    [InlineData("a", 2.5, "mm", "throat")]
    [InlineData("L", 25, "mm", "length")]
    public void Geometry_Outside_The_Method_Blocks(string symbol, double value, string unit, string rule)
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity(symbol, value, unit)));
        Assert.Contains(r.Blocks, b => b.RuleId == rule);
    }

    [Fact]
    public void Length_Below_Six_Throats_Blocks_Even_Above_Thirty_Millimetres()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("a", 10, "mm")).With(CalcInputValue.Quantity("L", 50, "mm")));
        Assert.Contains(r.Blocks, b => b.RuleId == "length");
    }

    [Fact]
    public void A_Lap_Joint_Longer_Than_150_Throats_Is_Reduced_By_The_Long_Joint_Factor()
    {
        // F2 (BS EN 1993-1-8 4.11(3)): the reduction was a warning that said it was not applied, and the sheet passed what the code reduces. python3: a = 5 mm (150a = 750 mm), f_u 490, beta_w 0.9,
        // gamma_M2 1.25: f_vw,d = 251.4676 MPa; L = 800 mm: beta_Lw = 1.2 - 0.2 x 800/750 = 0.986667, F_w,Rd = 0.986667 x 251.4676 x 5 x 800/1000 = 992.4608 kN (1005.8725 kN unreduced), a_calc 0.7557 mm;
        // L = 1500 mm: beta_Lw 0.8, F_w,Rd 1508.8087 kN; L = 3000 mm: beta_Lw 0.4, F_w,Rd 1508.8087 kN (the reduced resistance stops growing with the length).
        Assert.DoesNotContain(CalcRunner.Run(Weld, Inputs()).Notices, n => n.RuleId == "long-joint");
        Assert.Equal(1.0, CalcRunner.Run(Weld, Inputs()).OutputSi("beta_Lw"), 12);

        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 800, "mm")));
        Assert.Contains(r.Warnings, w => w.RuleId == "long-joint" && w.Message.Contains("reduced by", StringComparison.Ordinal));
        Assert.Equal(0.9866666666666667, r.OutputSi("beta_Lw"), 12);
        Assert.Equal(992.4608360683058, r.OutputSi("F_w_Rd") / 1000, 6);
        Assert.Equal(150 / 992.4608360683058, r.OutputSi("u"), 9);
        Assert.Equal(0.7556973260236352, r.OutputSi("a_calc") * 1000, 9);

        Assert.Equal(0.8, CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 1500, "mm"))).OutputSi("beta_Lw"), 12);
        Assert.Equal(1508.8087034822217, CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 1500, "mm"))).OutputSi("F_w_Rd") / 1000, 6);
        Assert.Equal(1508.8087034822215, CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 3000, "mm"))).OutputSi("F_w_Rd") / 1000, 6);

        // Exactly 150 throats is not reduced; another joint type (a flange-to-web weld) is not reduced either, and says so.
        Assert.Equal(1.0, CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 750, "mm"))).OutputSi("beta_Lw"), 12);
        var other = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 800, "mm")).With(CalcInputValue.Choice("joint", FilletWeldCalculator.OtherJoint)));
        Assert.Equal(1.0, other.OutputSi("beta_Lw"), 12);
        Assert.Equal(1005.872468988148, other.OutputSi("F_w_Rd") / 1000, 6);
        Assert.DoesNotContain(other.Notices, n => n.RuleId == "long-joint");

        // A saved instance without the new input reads as a lap joint.
        var saved = CalcRunner.Run(Weld, new CalcInputSet(Inputs().With(CalcInputValue.Quantity("L", 800, "mm")).Values.Where(v => v.Symbol != "joint")));
        Assert.Equal(0.9866666666666667, saved.OutputSi("beta_Lw"), 12);
    }

    [Fact]
    public void A_Lap_Longer_Than_900_Throats_Is_Outside_The_Long_Joint_Formula_And_Blocks()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 4500, "mm")));
        Assert.True(r.IsBlocked);
        var block = Assert.Single(r.Blocks);
        Assert.Equal("long-joint-range", block.RuleId);
        Assert.StartsWith("L:", block.Message, StringComparison.Ordinal);
        Assert.Empty(CalcMessageShape.Problems(block.Message));
        Assert.False(CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 4400, "mm"))).IsBlocked);
        Assert.False(CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 4500, "mm")).With(CalcInputValue.Choice("joint", FilletWeldCalculator.OtherJoint))).IsBlocked);
    }

    [Fact]
    public void No_Load_Blocks()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", 0, "kN")));
        Assert.Contains(r.Blocks, b => b.RuleId == "no-load");
    }
}
