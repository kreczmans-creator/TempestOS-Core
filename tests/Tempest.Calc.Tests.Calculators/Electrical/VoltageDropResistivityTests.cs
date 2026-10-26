using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E3 1.1.0, elec.voltage-drop-01: the default resistivity 0.01724 gave conductor resistances 5 to 11 % below the cable standard's class 2 maxima.
/// Expected values from python3: class 2 maximum 0.524 Ω/km at 35 mm² (and 0.387 Ω/km at 50 mm²), × (1 + 0.00393 × 50) at 70 °C.
/// </summary>
public sealed class VoltageDropResistivityTests
{
    private static readonly VoltageDropCalculator Drop = new();

    /// <summary>The three-phase 35 mm², 80 m case with the resistivity left blank (so the template default applies), unity power factor and no reactance.</summary>
    private static CalcRunResult RunDefaultResistivity(double area)
    {
        var inputs = new CalcInputSet([.. Drop.Template.TestCases[1].Inputs.Where(v => v.Symbol is not "rho_20")]).With(CalcInputValue.Of("cos_phi", 1)).With(CalcInputValue.Quantity("X_km", 0, "ohm/km")).With(CalcInputValue.Quantity("A_c", area, "mm^2"));
        return CalcRunner.Run(Drop, inputs);
    }

    [Fact]
    public void The_Default_Resistivity_Gives_A_Resistance_Not_Below_The_Cable_Standard_Maximum_At_35_mm2()
    {
        // 80 m of 35 mm2 at 70 degC: 0.524 ohm/km x 0.08 km x 1.1965 = 0.050159 ohm is the table basis; the default gave 0.047150 ohm (6 % below).
        var r = RunDefaultResistivity(35);
        var basis = 0.524e-3 * 80 * 1.1965;
        Assert.True(r.OutputSi("R") >= basis, $"R = {r.OutputSi("R")} ohm is below the cable standard's maximum {basis} ohm");
        Assert.Equal(0.0194 * 1.1965 * 80 / 35, r.OutputSi("R"), 9);
        Assert.Equal(1.1487, Math.Sqrt(3) * r.OutputSi("R") / 80 * 1e3, 3);
    }

    [Fact]
    public void The_Default_Resistivity_Also_Covers_The_Largest_Class_2_Maximum_At_50_mm2()
    {
        // 0.387 ohm/km at 50 mm2 is an effective resistivity of 0.01935 ohm.mm2/m, the largest in 1.5 to 120 mm2 (python3); the pure-copper default was 10.9 % below it.
        var r = RunDefaultResistivity(50);
        Assert.True(r.OutputSi("R") >= 0.387e-3 * 80 * 1.1965);
    }

    [Fact]
    public void A_Stated_Resistivity_Is_Used_As_Entered()
    {
        var inputs = new CalcInputSet([.. Drop.Template.TestCases[1].Inputs]);
        Assert.Equal(0.01724 * 1.1965 * 80 / 35, CalcRunner.Run(Drop, inputs).OutputSi("R"), 9);
    }
}
