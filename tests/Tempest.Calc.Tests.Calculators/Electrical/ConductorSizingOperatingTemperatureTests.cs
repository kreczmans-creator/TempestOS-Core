using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

/// <summary>
/// E4 1.3.0, chain.motor-circuit-R1: the insulation chosen in the cable step did not reach the fault-loop and voltage-drop steps, which kept their own 70 °C. The step now reports the conductor operating temperature
/// of its insulation as a port: 70 °C for PVC, 90 °C for XLPE, the entered θ_p for 'user'. The effect downstream (python3): with R at 20 °C unchanged, the resistance grows by
/// (1 + 0.00393·70)/(1 + 0.00393·50) = 1.27510/1.19650 = 1.0657 from 70 °C to 90 °C, so Z_s of the finding's 2.5/2.5 mm², 100 m circuit is 2.000 Ω at 70 °C and 2.109 Ω at 90 °C.
/// </summary>
public sealed class ConductorSizingOperatingTemperatureTests
{
    private static readonly ConductorSizingCalculator Sizing = new();

    private static CalcRunResult Run(params CalcInputValue[] overrides) => CalcRunner.Run(Sizing, overrides.Aggregate(CalcTemplateHasher.DefaultInputs(Sizing.Template), (set, v) => set.With(v)));

    [Theory]
    [InlineData(ConductorSizingCalculator.Pvc, 70.0)]
    [InlineData(ConductorSizingCalculator.Xlpe, 90.0)]
    public void The_Insulation_Gives_The_Conductor_Operating_Temperature_As_A_Port(string insulation, double expected)
    {
        var r = Run(CalcInputValue.Choice("insulation", insulation));

        Assert.Equal(expected + 273.15, r.OutputSi("theta_op"), 9);
        Assert.Contains(Sizing.Template.OutputPorts!, p => p is { Symbol: "theta_op", Unit: "degC" });
    }

    [Fact]
    public void A_User_Insulation_Gives_The_Entered_Temperature_And_None_Without_It()
    {
        var entered = Run(CalcInputValue.Choice("insulation", ConductorSizingCalculator.UserInsulation), CalcInputValue.Quantity("theta_p", 105, "degC"));
        Assert.Equal(105 + 273.15, entered.OutputSi("theta_op"), 9);

        var blank = Run(CalcInputValue.Choice("insulation", ConductorSizingCalculator.UserInsulation));
        Assert.Null(blank.FindOutput("theta_op"));
    }
}
