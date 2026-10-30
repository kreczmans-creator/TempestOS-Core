using CsCheck;
using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Physics;

namespace Tempest.Calc.Tests.Engine;

/// <summary>
/// The gas-law kernel for a gas charged by one process and discharged by another (<see cref="GasLaw.ChargeVolumeSplit"/>): a held accumulator is charged
/// slowly (isothermal, V₂ = V₀·p₀/p₂) and discharged fast (V₁ = V₂·(p₂/p₁)^(1/κ)). The single-exponent <see cref="GasLaw.ChargeVolume"/> and its tests are untouched.
/// </summary>
public sealed class GasLawSplitProcessTests
{
    [Fact]
    public void Isothermal_Charge_And_Adiabatic_Discharge_Gives_The_Held_Accumulator_Volume()
    {
        // p0 90, p1 100, p2 210 bar abs, ΔV 14 L, κ = 1.4 (python3): V0 = ΔV (p2/p0) / [(p2/p1)^(1/κ) − 1] = 14 × 2.33333 / 0.698856 = 46.74303967158 L
        var v0 = GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, GasLaw.Isothermal, GasLaw.Adiabatic(1.4));
        Assert.Equal(46.743039671582224, v0, 9);

        // The gas held at p2 is V0 p0/p2 = 20.03273 L and expands to 34.03273 L at p1: the difference is ΔV.
        var held = v0 * 90e5 / 210e5;
        Assert.Equal(20.03273128782095, held, 9);
        Assert.Equal(34.03273128782095, GasLaw.VolumeAt(210e5, held, 100e5, 1.4), 9);
    }

    [Fact]
    public void The_Single_Exponent_Form_Sizes_The_Same_Duty_Smaller()
    {
        // The single-exponent form of the same duty is 36.69279 L; a slowly charged, fast discharged accumulator needs 46.74304 L (python3: 46.74304 / 36.69279 = 1.2739, 27.4 % more).
        var single = GasLaw.ChargeVolume(14.0, 90e5, 100e5, 210e5, 1.4);
        var split = GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, GasLaw.Isothermal, 1.4);
        Assert.Equal(36.69278925358671, single, 9);
        Assert.True(split > single);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.2)]
    [InlineData(1.4)]
    public void With_The_Same_Exponent_Both_Ways_It_Is_The_Single_Exponent_Form(double n)
    {
        Assert.Equal(GasLaw.ChargeVolume(14.0, 90e5, 100e5, 210e5, n), GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, n, n), 9);
    }

    [Fact]
    public void An_Isothermal_Discharge_Makes_The_Charge_Process_Immaterial_Only_When_Both_Are_Isothermal()
    {
        // n_c = n_d = 1: V0 = 14 / (0.9 − 90/210) = 29.69697 L either way.
        Assert.Equal(14.0 / (0.9 - 90.0 / 210.0), GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, 1.0, 1.0), 9);

        // A faster charge than discharge (n_c = 1.4, n_d = 1) holds less gas at p2 and needs less volume than the isothermal charge.
        Assert.True(GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, 1.4, 1.0) < GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, 1.0, 1.0));
    }

    [Fact]
    public void Bad_Pressures_And_Exponents_Are_Refused_As_The_Single_Exponent_Form_Refuses_Them()
    {
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(14.0, 90e5, 210e5, 100e5, 1.0, 1.4));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(14.0, 110e5, 100e5, 210e5, 1.0, 1.4));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(14.0, 0, 100e5, 210e5, 1.0, 1.4));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(double.NaN, 90e5, 100e5, 210e5, 1.0, 1.4));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, 0.0, 1.4));
        Assert.Throws<CalcEvaluationException>(() => GasLaw.ChargeVolumeSplit(14.0, 90e5, 100e5, 210e5, 1.0, 2.5));
    }

    [Fact]
    public void Property_The_Split_Volume_Displaces_Exactly_The_Requested_Liquid()
    {
        Gen.Select(Gen.Double[1e-6, 10], Gen.Double[1e3, 1e8], Gen.Double[0.3, 0.99], Gen.Double[1.05, 20], Gen.Double[0.5, 1.67], Gen.Double[0.5, 1.67]).Sample((dv, p1, chargeFraction, factor, nc, nd) =>
        {
            var p0 = p1 * chargeFraction;
            var p2 = p1 * factor;
            var v0 = GasLaw.ChargeVolumeSplit(dv, p0, p1, p2, nc, nd);
            var held = GasLaw.VolumeAt(p0, v0, p2, nc);
            var delivered = GasLaw.VolumeAt(p2, held, p1, nd);
            return Math.Abs((delivered - held) - dv) <= 1e-8 * dv;
        });
    }
}
