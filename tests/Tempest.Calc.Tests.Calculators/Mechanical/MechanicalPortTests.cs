using CsCheck;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Mechanical;

/// <summary>
/// Behaviour tests for the mechanical templates ported from the legacy modules
/// (ADR-0167; Yellow Y10-03): the physics each method promises, checked as
/// properties and limits rather than single figures. Defaults are each
/// template's first known-answer case.
/// </summary>
public sealed class MechanicalPortTests
{
    private static CalcInputSet Defaults(ICalcCalculator calc) => CalcTemplateHasher.DefaultInputs(calc.Template);

    private static CalcInputSet Without(CalcInputSet set, string symbol) => new([.. set.Values.Where(v => v.Symbol != symbol)]);

    private static bool Close(double a, double b, double rel = 1e-9) => Math.Abs(a - b) <= rel * Math.Max(Math.Abs(a), Math.Abs(b));

    // Lifting lug.
    private static readonly LiftingLugCalculator Lug = new();

    [Fact]
    public void Lug_Every_Utilisation_Is_Linear_In_The_Load()
    {
        Gen.Double[1, 500].Sample(p =>
        {
            var a = CalcRunner.Run(Lug, Defaults(Lug).With(CalcInputValue.Quantity("P", p, "kN")));
            var b = CalcRunner.Run(Lug, Defaults(Lug).With(CalcInputValue.Quantity("P", 2 * p, "kN")));
            return new[] { "u_t", "u_br", "u_to", "u_p", "u_b" }.All(u => Close(b.OutputSi(u), 2 * a.OutputSi(u)));
        }, iter: 40);
    }

    [Fact]
    public void Lug_Pin_Moment_Grows_By_Half_The_Load_Per_Unit_Of_Gap()
    {
        var a = CalcRunner.Run(Lug, Defaults(Lug));
        var b = CalcRunner.Run(Lug, Defaults(Lug).With(CalcInputValue.Quantity("g", 3, "mm")));
        Assert.Equal(50e3 / 2 * 1e-3, b.OutputSi("M_p") - a.OutputSi("M_p"), 9);
    }

    [Theory]
    [InlineData("W", 30, "geometry")]
    [InlineData("d_p", 33, "geometry")]
    [InlineData("d_p", 28, "loose-pin")]
    public void Lug_Geometry_Outside_The_Method_Blocks(string symbol, double mm, string rule) =>
        Assert.Contains(CalcRunner.Run(Lug, Defaults(Lug).With(CalcInputValue.Quantity(symbol, mm, "mm"))).Blocks, b => b.RuleId == rule);

    // Bearing life.
    private static readonly BearingLifeBasicCalculator Bearing = new();

    [Theory]
    [InlineData(BearingLifeBasicCalculator.Ball, 3.0)]
    [InlineData(BearingLifeBasicCalculator.Roller, 10.0 / 3.0)]
    public void Bearing_Doubling_The_Load_Divides_The_Life_By_Two_To_The_Exponent(string type, double p)
    {
        var inputs = Defaults(Bearing).With(CalcInputValue.Choice("type", type));
        var a = CalcRunner.Run(Bearing, inputs);
        var b = CalcRunner.Run(Bearing, inputs.With(CalcInputValue.Quantity("F_r", 8, "kN")));
        Assert.Equal(Math.Pow(2, p), a.OutputSi("L_10") / b.OutputSi("L_10"), 9);
    }

    [Fact]
    public void Bearing_Hours_Are_Inverse_To_Speed_And_Linear_In_The_Reliability_Factor()
    {
        var a = CalcRunner.Run(Bearing, Defaults(Bearing));
        var faster = CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Quantity("n", 3000, "rpm")));
        var reliable = CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Of("a_1", 0.62)));
        Assert.Equal(a.OutputSi("L_10h") / 2, faster.OutputSi("L_10h"), 6);
        Assert.Equal(0.62 * a.OutputSi("L_10h"), reliable.OutputSi("L_nmh"), 6);
    }

    [Fact]
    public void Bearing_Loads_Outside_The_Rating_Life_Equation_Block()
    {
        Assert.Contains(CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Quantity("F_r", 0, "kN")).With(CalcInputValue.Quantity("F_a", 0, "kN"))).Blocks, b => b.RuleId == "no-load");
        Assert.Contains(CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Quantity("F_r", 20, "kN"))).Blocks, b => b.RuleId == "heavy-load");
        Assert.Contains(CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Quantity("F_r", 1e-5, "kN"))).Blocks, b => b.RuleId == "light-load");
    }

    [Fact]
    public void Bearing_Without_A_Required_Life_Has_No_Check()
    {
        var r = CalcRunner.Run(Bearing, Defaults(Bearing).With(CalcInputValue.Quantity("L_req", 0, "h")));
        Assert.False(r.IsBlocked);
        Assert.Empty(r.Checks);
    }

    // Shaft stress.
    private static readonly ShaftStressCalculator Shaft = new();

    [Fact]
    public void Shaft_Tresca_Is_Never_Less_Conservative_Than_Von_Mises()
    {
        Gen.Select(Gen.Double[0, 2000], Gen.Double[0, 2000], Gen.Double[10, 100]).Sample((m, t, d) =>
        {
            if (m + t < 1)
                return true;

            var r = CalcRunner.Run(Shaft, Defaults(Shaft).With(CalcInputValue.Quantity("M", m, "N*m")).With(CalcInputValue.Quantity("T", t, "N*m")).With(CalcInputValue.Quantity("d", d, "mm")));
            return r.OutputSi("n_T") <= r.OutputSi("n_VM") * (1 + 1e-12) && Close(r.OutputSi("n_min"), r.OutputSi("n_T"));
        }, iter: 60);
    }

    [Fact]
    public void Shaft_Pure_Bending_Agrees_And_Pure_Torsion_Differs_By_Two_Over_Root_Three()
    {
        var bending = CalcRunner.Run(Shaft, Defaults(Shaft).With(CalcInputValue.Quantity("T", 0, "N*m")));
        Assert.Equal(bending.OutputSi("n_T"), bending.OutputSi("n_VM"), 9);
        var torsion = CalcRunner.Run(Shaft, Defaults(Shaft).With(CalcInputValue.Quantity("M", 0, "N*m")));
        Assert.Equal(2 / Math.Sqrt(3), torsion.OutputSi("n_VM") / torsion.OutputSi("n_T"), 9);
    }

    [Fact]
    public void Shaft_Stress_Falls_With_The_Cube_Of_The_Diameter()
    {
        var a = CalcRunner.Run(Shaft, Defaults(Shaft));
        var b = CalcRunner.Run(Shaft, Defaults(Shaft).With(CalcInputValue.Quantity("d", 80, "mm")));
        Assert.Equal(8, a.OutputSi("sigma_vm") / b.OutputSi("sigma_vm"), 9);
    }

    [Fact]
    public void Shaft_With_No_Load_Blocks() =>
        Assert.Contains(CalcRunner.Run(Shaft, Defaults(Shaft).With(CalcInputValue.Quantity("M", 0, "N*m")).With(CalcInputValue.Quantity("T", 0, "N*m"))).Blocks, b => b.RuleId == "no-load");

    // Thermal expansion.
    private static readonly ThermalExpansionCalculator Thermal = new();

    [Fact]
    public void Thermal_An_Open_Gap_Carries_No_Force_And_The_Bar_Moves_Freely()
    {
        var r = CalcRunner.Run(Thermal, Defaults(Thermal).With(CalcInputValue.Quantity("g", 2, "mm")));
        Assert.Equal(0, r.OutputSi("F"), 12);
        Assert.Equal(r.OutputSi("delta_free"), r.OutputSi("delta"), 12);
        Assert.True(r.Passed);
    }

    [Fact]
    public void Thermal_Cooling_Mirrors_Heating_With_Tension()
    {
        Gen.Double[1, 300].Sample(dT =>
        {
            var heat = CalcRunner.Run(Thermal, Defaults(Thermal).With(CalcInputValue.Quantity("dT", dT, "K")));
            var cool = CalcRunner.Run(Thermal, Defaults(Thermal).With(CalcInputValue.Quantity("dT", -dT, "K")));
            return heat.OutputSi("sigma") < 0 && Close(cool.OutputSi("sigma"), -heat.OutputSi("sigma"));
        }, iter: 40);
    }

    [Fact]
    public void Thermal_A_Very_Stiff_Restraint_Tends_To_The_Rigid_Force()
    {
        var rigid = CalcRunner.Run(Thermal, Defaults(Thermal));
        var stiff = CalcRunner.Run(Thermal, Defaults(Thermal).With(CalcInputValue.Quantity("k_s", 1e7, "kN/mm")));
        Assert.Equal(1, stiff.OutputSi("F") / rigid.OutputSi("F"), 5);
        Assert.True(stiff.OutputSi("F") < rigid.OutputSi("F"));
    }

    // Thin vessel.
    private static readonly ThinVesselCalculator Vessel = new();

    [Fact]
    public void Vessel_At_The_Required_Thickness_The_Utilisation_Is_One_And_The_Hoop_Stress_Is_The_Allowable()
    {
        var first = CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("c", 0, "mm")));
        var tc = first.OutputSi("t_c");
        var at = CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("c", 0, "mm")).With(CalcInputValue.Quantity("t_p", tc * 1000, "mm")));
        Assert.Equal(1, at.OutputSi("u"), 9);
        Assert.Equal(1, at.OutputSi("sigma_h") / 138e6, 9);
    }

    [Fact]
    public void Vessel_Corrosion_Allowance_Adds_Directly_To_The_Required_Thickness()
    {
        var a = CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("c", 0, "mm")));
        var b = CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("c", 2.5, "mm")));
        Assert.Equal(2.5e-3, b.OutputSi("t_req") - a.OutputSi("t_req"), 12);
    }

    [Fact]
    public void Vessel_Outside_The_Thin_Shell_Range_Blocks()
    {
        Assert.Contains(CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("P", 50, "MPa"))).Blocks, b => b.RuleId == "thin-wall");
        Assert.Contains(CalcRunner.Run(Vessel, Defaults(Vessel).With(CalcInputValue.Quantity("t_p", 3, "mm"))).Blocks, b => b.RuleId == "corroded-through");
    }

    // Material margin.
    private static readonly MaterialMarginCalculator Margin = new();

    [Fact]
    public void Margin_Without_An_Ultimate_Strength_Is_The_Yield_Margin()
    {
        var r = CalcRunner.Run(Margin, Without(Defaults(Margin), "f_u"));
        Assert.Null(r.FindOutput("n_u"));
        Assert.Equal(r.OutputSi("n_y"), r.OutputSi("n_gov"), 12);
    }

    [Fact]
    public void Margin_Ultimate_Governs_When_The_Scaled_Ultimate_Factor_Is_Lower()
    {
        var r = CalcRunner.Run(Margin, Defaults(Margin).With(CalcInputValue.Quantity("f_u", 360, "MPa")));
        Assert.Equal(360.0 / 120 / 1.5, r.OutputSi("n_gov"), 9);
        Assert.True(r.OutputSi("n_gov") < r.OutputSi("n_y"));
    }

    [Fact]
    public void Margin_Utilisation_Is_Linear_In_The_Stress()
    {
        Gen.Double[1, 1000].Sample(s =>
        {
            var a = CalcRunner.Run(Margin, Defaults(Margin).With(CalcInputValue.Quantity("sigma", s, "MPa")));
            var b = CalcRunner.Run(Margin, Defaults(Margin).With(CalcInputValue.Quantity("sigma", 2 * s, "MPa")));
            return Close(b.OutputSi("u"), 2 * a.OutputSi("u"));
        }, iter: 40);
    }

    // Fatigue (Palmgren–Miner).
    private static readonly FatigueMinerCalculator Miner = new();

    [Fact]
    public void Miner_Damage_Is_Additive_Over_Blocks_And_Independent_Of_Their_Order()
    {
        var both = CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((100, 1e5), (60, 5e5))));
        var reversed = CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((60, 5e5), (100, 1e5))));
        var first = CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((100, 1e5))));
        var second = CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((60, 5e5))));
        Assert.Equal(both.OutputSi("D_tot"), reversed.OutputSi("D_tot"), 12);
        Assert.Equal(first.OutputSi("D_tot") + second.OutputSi("D_tot"), both.OutputSi("D_tot"), 12);
    }

    [Fact]
    public void Miner_At_The_Reference_Range_The_Life_Is_The_Reference_Cycles()
    {
        var r = CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((71, 1e6))));
        Assert.Equal(2e6, r.OutputSi("N_1"), 3);
        Assert.Equal(0.5, r.OutputSi("D_tot"), 12);
    }

    [Fact]
    public void Miner_Low_Cycle_And_Empty_Spectra_Block()
    {
        Assert.Contains(CalcRunner.Run(Miner, Defaults(Miner).With(FatigueMinerCalculator.Blocks((2000, 10)))).Blocks, b => b.RuleId == "low-cycle");
        Assert.Contains(CalcRunner.Run(Miner, Defaults(Miner).With(CalcInputValue.Table("blocks", []))).Blocks, b => b.RuleId == "no-blocks");
    }
}
