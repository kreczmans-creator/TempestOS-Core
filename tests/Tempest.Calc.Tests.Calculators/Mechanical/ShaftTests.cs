using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

public sealed class ShaftTests
{
    private static readonly ShaftCalculator Shaft = new();

    private static CalcInputSet Defaults() => CalcTemplateHasher.DefaultInputs(Shaft.Template);

    [Fact]
    public void Two_Planes_Combine_As_A_Vector()
    {
        var vertical = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -3, 0, 0))));
        var both = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Loads((200, -3, 4, 0))));
        Assert.Equal(5.0 / 3.0 * vertical.OutputSi("M_max"), both.OutputSi("M_max"), 6);
        Assert.Equal(5.0 / 3.0 * vertical.OutputSi("R_A"), both.OutputSi("R_A"), 6);
    }

    [Fact]
    public void Deflection_Falls_With_The_Fourth_Power_Of_Diameter()
    {
        var thin = CalcRunner.Run(Shaft, Defaults()).OutputSi("y_load");
        var thick = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 60, 0)))).OutputSi("y_load");
        Assert.Equal(16, thin / thick, 6);
    }

    [Fact]
    public void Dunkerley_Never_Exceeds_Rayleigh()
    {
        Gen.Select(Gen.Double[5, 80], Gen.Double[50, 350], Gen.Double[1, 100]).Sample((d, x, m) =>
        {
            var r = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, d, 0))).With(ShaftCalculator.Loads((x, -1, 0, m), (400 - x / 2, 0, 0, m / 2))));
            return r.OutputSi("n_c_D") <= r.OutputSi("n_c") * (1 + 1e-9);
        }, iter: 40);
    }

    [Fact]
    public void A_Bore_Raises_The_Stress()
    {
        var solid = CalcRunner.Run(Shaft, Defaults()).OutputSi("n_f_min");
        var hollow = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 30, 15)))).OutputSi("n_f_min");
        Assert.True(hollow < solid);
    }

    [Fact]
    public void Low_Critical_Speed_Warns()
    {
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("n", 4000, "rpm")));
        Assert.Contains(r.Warnings, w => w.RuleId == "critical-speed");
    }

    [Fact]
    public void Bearing_Off_The_Shaft_Blocks()
    {
        var r = CalcRunner.Run(Shaft, Defaults().With(CalcInputValue.Quantity("x_B", 500, "mm")));
        Assert.True(r.IsBlocked);
        Assert.Equal("geometry", r.Blocks[0].RuleId);
    }

    [Theory]
    [InlineData(0.5, 0)]
    [InlineData(0.9, 1.2815516)]
    [InlineData(0.99, 2.3263479)]
    [InlineData(0.999, 3.0902323)]
    public void Normal_Quantile_Matches_Tables(double p, double z) => Assert.Equal(z, ShaftCalculator.NormalQuantile(p), 6);

    [Fact]
    public void A_Shoulder_Is_Checked_On_Its_Smaller_Diameter()
    {
        var stepped = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((200, 30, 0), (400, 40, 0))));
        var uniform = CalcRunner.Run(Shaft, Defaults().With(ShaftCalculator.Segments((400, 30, 0))));
        Assert.True(stepped.OutputSi("n_f_min") > 0.9 * uniform.OutputSi("n_f_min"));
        Assert.Equal(40, stepped.OutputSi("d_B") * 1e3, 9);
    }
}
