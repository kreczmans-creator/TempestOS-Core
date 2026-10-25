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
    public void Long_Welds_Warn_That_The_Long_Joint_Reduction_Is_Not_Applied()
    {
        Assert.DoesNotContain(CalcRunner.Run(Weld, Inputs()).Warnings, w => w.RuleId == "long-joint");
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("L", 800, "mm")));
        Assert.Contains(r.Warnings, w => w.RuleId == "long-joint");
    }

    [Fact]
    public void No_Load_Blocks()
    {
        var r = CalcRunner.Run(Weld, Inputs().With(CalcInputValue.Quantity("F_par", 0, "kN")));
        Assert.Contains(r.Blocks, b => b.RuleId == "no-load");
    }
}
