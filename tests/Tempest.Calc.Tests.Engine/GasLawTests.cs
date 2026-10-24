using CsCheck;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Physics;

namespace Tempest.Calc.Tests.Engine;

/// <summary>The shared gas-law kernel (B-0): absolute-pressure helpers and p·Vⁿ = const solved for volume or pressure, isothermal, adiabatic and polytropic.</summary>
public sealed class GasLawTests
{
    // ---- absolute pressure ----------------------------------------------------------------

    [Fact]
    public void Gauge_Plus_One_Bar_Is_Absolute_And_Back()
    {
        Assert.Equal(350_000.0, GasLaw.ToAbsolute(250_000.0, GasLaw.OneBar));
        Assert.Equal(250_000.0, GasLaw.ToGauge(350_000.0, GasLaw.OneBar));
        Assert.Equal(101_325.0 + 6e5, GasLaw.ToAbsolute(6e5));
        Assert.Equal(6e5, GasLaw.ToGauge(101_325.0 + 6e5), 6);
    }

    [Fact]
    public void A_Gauge_Vacuum_Above_Absolute_Zero_Is_Allowed_And_Below_It_Is_Refused()
    {
        Assert.Equal(1325.0, GasLaw.ToAbsolute(-100_000.0), 6);
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ToAbsolute(-101_325.0));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ToAbsolute(-2e5));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ToAbsolute(double.NaN));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ToAbsolute(1e5, -1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ToGauge(0));
    }

    // ---- known answers --------------------------------------------------------------------

    [Fact]
    public void Boyle_Halving_The_Volume_Doubles_The_Pressure()
    {
        Assert.Equal(5.0, GasLaw.VolumeAt(2e5, 10.0, 4e5, GasLaw.Isothermal), 12);
        Assert.Equal(4e5, GasLaw.PressureAt(2e5, 10.0, 5.0, GasLaw.Isothermal), 6);
    }

    [Fact]
    public void Adiabatic_Compression_To_Twice_The_Pressure_Leaves_60_Percent_Of_The_Volume()
    {
        // V2 = V1 (p1/p2)^(1/κ) = 10 × 0.5^(1/1.4) = 6.095068271
        Assert.Equal(6.095068271022377, GasLaw.VolumeAt(1e5, 10.0, 2e5, GasLaw.Adiabatic(1.4)), 9);
        // p2 = p1 (V1/V2)^κ = 1e5 × 2^1.4 = 263 901.58 Pa
        Assert.Equal(263_901.5821545788, GasLaw.PressureAt(1e5, 10.0, 5.0, GasLaw.Adiabatic(1.4)), 4);
    }

    [Fact]
    public void Sealed_System_Vessel_Form_Reproduces_The_Cibse_Relation()
    {
        // V_e = ΔV / [1 − (p_i + 1)/(p_f + 1)], p_i = 0.79019 bar, p_f = 2.5 bar gauge, ΔV = 1 L: 2.0470110714
        var low = GasLaw.ToAbsolute(0.79019e5, GasLaw.OneBar);
        var high = GasLaw.ToAbsolute(2.5e5, GasLaw.OneBar);
        Assert.Equal(2.0470110714055947, GasLaw.GasVolumeForDisplacement(1.0, low, high, GasLaw.Isothermal), 9);
    }

    [Fact]
    public void Accumulator_Form_Gives_The_Textbook_Precharge_Volume()
    {
        // ΔV = 1 L between 100 and 200 bar abs, pre-charge 90 bar: V0 = 1 / (0.9 − 0.45) = 2.2222 L isothermal; adiabatic 1.4: 2.76103 L
        Assert.Equal(1.0 / 0.45, GasLaw.ChargeVolume(1.0, 90, 100, 200, GasLaw.Isothermal), 12);
        Assert.Equal(2.761025873639015, GasLaw.ChargeVolume(1.0, 90, 100, 200, GasLaw.Adiabatic(1.4)), 9);
    }

    [Fact]
    public void Charge_Form_With_The_Charge_At_The_Low_Pressure_Is_The_Displacement_Form()
    {
        Assert.Equal(GasLaw.GasVolumeForDisplacement(3.5, 1.8e5, 3.5e5, 1.2), GasLaw.ChargeVolume(3.5, 1.8e5, 1.8e5, 3.5e5, 1.2), 12);
    }

    [Fact]
    public void The_Exponent_Between_Two_States_Recovers_The_Process()
    {
        var p2 = GasLaw.PressureAt(1e5, 10.0, 5.0, 1.25);
        Assert.Equal(1.25, GasLaw.ExponentBetween(1e5, 10.0, p2, 5.0)!.Value, 12);
        Assert.Null(GasLaw.ExponentBetween(1e5, 10.0, 2e5, 10.0));
    }

    [Fact]
    public void Constant_Is_Pressure_Times_Volume_To_The_N()
    {
        Assert.Equal(2e5 * Math.Pow(3.0, 1.4), GasLaw.Constant(2e5, 3.0, 1.4), 6);
    }

    // ---- guards ---------------------------------------------------------------------------

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(2.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void An_Exponent_Outside_The_Physical_Range_Is_Refused(double n)
    {
        Assert.Throws<CalcEvaluationException>(() => GasLaw.Polytropic(n));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.VolumeAt(1e5, 1, 2e5, n));
    }

    [Theory]
    [InlineData(0.9)]
    [InlineData(2.1)]
    [InlineData(double.NaN)]
    public void An_Adiabatic_Exponent_Below_One_Or_Above_Two_Is_Refused(double kappa)
    {
        Assert.Throws<CalcEvaluationException>(() => GasLaw.Adiabatic(kappa));
    }

    [Fact]
    public void Non_Positive_Or_Non_Finite_States_And_Inverted_Pressure_Ranges_Are_Refused()
    {
        Assert.Throws<CalcEvaluationException>(() => GasLaw.VolumeAt(0, 1, 2e5, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.VolumeAt(1e5, -1, 2e5, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.VolumeAt(1e5, 1, double.NaN, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.PressureAt(1e5, 1, 0, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.GasVolumeForDisplacement(1, 3e5, 2e5, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.GasVolumeForDisplacement(1, 2e5, 2e5, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolume(1, 2e5, 3e5, 2e5, 1));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolume(double.PositiveInfinity, 90, 100, 200, 1));
    }

    [Fact]
    public void A_Zero_Displacement_Needs_No_Vessel_And_A_Negative_One_Follows_Its_Sign()
    {
        Assert.Equal(0.0, GasLaw.GasVolumeForDisplacement(0.0, 1.8e5, 3.5e5, 1));
        Assert.True(GasLaw.GasVolumeForDisplacement(-2.0, 1.8e5, 3.5e5, 1) < 0);
    }

    // ---- properties -----------------------------------------------------------------------

    private static readonly Gen<double> Pressure = Gen.Double[1e3, 1e8];
    private static readonly Gen<double> Volume = Gen.Double[1e-6, 1e3];
    private static readonly Gen<double> Exponent = Gen.Double[0.5, 1.67];

    [Fact]
    public void Property_The_Polytropic_Constant_Is_Conserved_Along_Any_Solved_State()
    {
        Gen.Select(Pressure, Volume, Pressure, Exponent).Sample((p1, v1, p2, n) =>
        {
            var v2 = GasLaw.VolumeAt(p1, v1, p2, n);
            var k1 = GasLaw.Constant(p1, v1, n);
            var k2 = GasLaw.Constant(p2, v2, n);
            return Math.Abs(k1 - k2) <= 1e-9 * k1;
        });
    }

    [Fact]
    public void Property_Solving_For_Volume_Then_Pressure_Returns_The_Start()
    {
        Gen.Select(Pressure, Volume, Pressure, Exponent).Sample((p1, v1, p2, n) =>
        {
            var v2 = GasLaw.VolumeAt(p1, v1, p2, n);
            var back = GasLaw.PressureAt(p2, v2, v1, n);
            return Math.Abs(back - p1) <= 1e-9 * p1;
        });
    }

    [Fact]
    public void Property_Compression_Always_Shrinks_The_Gas_And_Expansion_Always_Grows_It()
    {
        Gen.Select(Pressure, Volume, Gen.Double[1.001, 50], Exponent).Sample((p, v, factor, n) =>
        {
            var compressed = GasLaw.VolumeAt(p, v, p * factor, n);
            var expanded = GasLaw.VolumeAt(p, v, p / factor, n);
            return compressed < v && expanded > v;
        });
    }

    [Fact]
    public void Property_A_Stiffer_Process_Compresses_Less()
    {
        // At the same pressure ratio > 1, the adiabatic volume is larger than the isothermal one.
        Gen.Select(Pressure, Volume, Gen.Double[1.01, 20]).Sample((p, v, factor) =>
            GasLaw.VolumeAt(p, v, p * factor, 1.4) > GasLaw.VolumeAt(p, v, p * factor, 1.0));
    }

    [Fact]
    public void Property_The_Vessel_Volume_Displaces_Exactly_The_Requested_Liquid()
    {
        Gen.Select(Gen.Double[1e-6, 10], Pressure, Gen.Double[1.01, 30], Exponent).Sample((dv, low, factor, n) =>
        {
            var high = low * factor;
            var v0 = GasLaw.GasVolumeForDisplacement(dv, low, high, n);
            var displaced = v0 - GasLaw.VolumeAt(low, v0, high, n);
            return Math.Abs(displaced - dv) <= 1e-9 * dv;
        });
    }

    [Fact]
    public void Property_The_Charge_Volume_Displaces_Exactly_The_Requested_Liquid_Between_The_Working_Pressures()
    {
        Gen.Select(Gen.Double[1e-6, 10], Pressure, Gen.Double[0.3, 0.99], Gen.Double[1.05, 20], Exponent).Sample((dv, p1, chargeFraction, factor, n) =>
        {
            var p0 = p1 * chargeFraction;
            var p2 = p1 * factor;
            var v0 = GasLaw.ChargeVolume(dv, p0, p1, p2, n);
            var atLow = GasLaw.VolumeAt(p0, v0, p1, n);
            var atHigh = GasLaw.VolumeAt(p0, v0, p2, n);
            return Math.Abs((atLow - atHigh) - dv) <= 1e-8 * dv;
        });
    }

    [Fact]
    public void Property_Gauge_And_Absolute_Round_Trip()
    {
        Gen.Select(Gen.Double[-0.9e5, 1e9], Gen.OneOfConst(GasLaw.OneBar, GasLaw.StandardAtmosphere)).Sample((gauge, atm) =>
        {
            var back = GasLaw.ToGauge(GasLaw.ToAbsolute(gauge, atm), atm);
            return Math.Abs(back - gauge) <= 1e-9 * Math.Max(1, Math.Abs(gauge));
        });
    }
}
