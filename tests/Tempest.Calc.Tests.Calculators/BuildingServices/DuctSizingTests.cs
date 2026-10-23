using CsCheck;
using Tempest.Calc.BuildingServices;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.BuildingServices;

public sealed class DuctSizingTests
{
    private static readonly DuctSizingCalculator Calc = new();

    private static CalcRunResult Run(params CalcInputValue[] values) => CalcRunner.Run(Calc, new CalcInputSet(values));

    [Fact]
    public void Air_Properties_Hit_Reference_Points()
    {
        Assert.Equal(1.204, DuctSizingCalculator.AirDensity(20, 101.325), 0.002);
        Assert.Equal(1.81e-5, DuctSizingCalculator.AirViscosity(20), 0.03e-5);
    }

    [Fact]
    public void Equal_Friction_Sizes_To_The_Target_Rate_Before_Rounding()
    {
        Gen.Select(Gen.Double[0.05, 5], Gen.Double[0.5, 3]).Sample((q, rate) =>
        {
            var r = Run(CalcInputValue.Choice("method", DuctSizingCalculator.MethodEqualFriction), CalcInputValue.Quantity("dp_target", rate, "Pa/m"), CalcInputValue.Quantity("round_to", 1, "mm"),
                DuctSizingCalculator.Sections(("s", q, 10, DuctSizingCalculator.ShapeRound, 0, 0, 0, 0, true)));
            Assert.False(r.IsBlocked, string.Join("; ", r.Blocks.Select(b => b.Message)));
            // Rounded up to 1 mm, the friction rate sits at or just under the target.
            Assert.InRange(r.OutputSi("dpL_s1"), rate * 0.9, rate * 1.0001);
        }, iter: 60);
    }

    [Fact]
    public void Velocity_Method_Never_Exceeds_The_Target_After_Rounding_Up()
    {
        Gen.Select(Gen.Double[0.05, 5], Gen.Double[3, 12]).Sample((q, v) =>
        {
            var r = Run(CalcInputValue.Choice("method", DuctSizingCalculator.MethodVelocity), CalcInputValue.Quantity("v_target", v, "m/s"),
                DuctSizingCalculator.Sections(("s", q, 5, DuctSizingCalculator.ShapeRound, 0, 0, 0, 0, true)));
            Assert.True(r.OutputSi("v_s1") <= v + 1e-9);
            Assert.Equal(0, (r.OutputSi("D_s1") * 1e3) % 50, 6);
        }, iter: 60);
    }

    [Fact]
    public void Rectangular_And_Flat_Oval_Equivalents_Match_Round_For_A_Circle_Like_Shape()
    {
        // A flat oval with a = b is a circle: D_e equals the diameter.
        Assert.Equal(0.4, DuctSizingCalculator.EquivalentDiameterFlatOval(0.4, 0.4), 3);
        // Rectangular equivalent is below the larger side and above the smaller.
        var de = DuctSizingCalculator.EquivalentDiameterRectangular(0.6, 0.3);
        Assert.InRange(de, 0.3, 0.6);
    }

    [Fact]
    public void Index_Run_Sums_Only_Flagged_Sections()
    {
        var r = Run(DuctSizingCalculator.Sections(("a", 1, 10, DuctSizingCalculator.ShapeRound, 0, 400, 0, 0, true), ("b", 0.5, 10, DuctSizingCalculator.ShapeRound, 0, 300, 0, 0, false)));
        Assert.Equal(r.OutputSi("dp_s1"), r.OutputSi("dp_index"), 1e-9);
        Assert.Equal(r.OutputSi("dp_s1") + r.OutputSi("dp_s2"), r.OutputSi("dp_all"), 1e-9);
    }

    [Fact]
    public void Limits_Warn_And_Fail_Checks()
    {
        var r = Run(CalcInputValue.Quantity("v_limit", 5, "m/s"), CalcInputValue.Quantity("dp_limit", 10, "Pa"), DuctSizingCalculator.Sections(("a", 1, 10, DuctSizingCalculator.ShapeRound, 0, 400, 2, 0, true)));
        Assert.Contains(r.Notices, n => n.RuleId == "velocity");
        Assert.False(r.Passed);
        Assert.Equal(2, r.Checks.Count(c => !c.Passed));
    }

    [Fact]
    public void Bad_Sections_Block()
    {
        Assert.Contains(Run().Blocks, b => b.Message.StartsWith("sections", StringComparison.Ordinal));
        Assert.Contains(Run(DuctSizingCalculator.Sections(("a", 1, 10, DuctSizingCalculator.ShapeRound, 300, 400, 0, 0, true))).Blocks, b => b.RuleId == "geometry");
    }
}
