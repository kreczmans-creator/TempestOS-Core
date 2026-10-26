using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.FluidPower;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Calculators.Consistency;

/// <summary>
/// C-2 phase A, F2: the inputs of the probes of <see cref="UnconservativeRuleTests"/> that are not in <see cref="UnconservativePassTests"/>. Each makes one unconservative warning fire on a
/// template that otherwise passes, and the probe then requires the check the warning is paired with to fail. The calculator tests hold the numbers; these are only the inputs.
/// </summary>
internal static class ProbeInputs
{
    /// <summary>The Annex B interaction with Table B.1 chosen for an I-section that buckles laterally (chi_LT below 1) and no recorded basis: Table B.2 is used and 6.62 fails (u_62 1.248).</summary>
    internal static CalcInputSet LtbTableB1() =>
        CalcTemplateHasher.DefaultInputs(new LtbInteractionCalculator().Template).With(CalcInputValue.Choice("torsion", LtbInteractionCalculator.NotSusceptible));

    /// <summary>A running shaft with its only mass on a bearing and no density: no critical speed is found, and the check that it clears the running speed fails.</summary>
    internal static CalcInputSet ShaftWithoutCriticalSpeed() =>
        CalcTemplateHasher.DefaultInputs(new ShaftCalculator().Template).With(ShaftCalculator.Loads((0, -2, 0, 20))).With(CalcInputValue.Quantity("y_max", 1, "mm")).With(CalcInputValue.Quantity("theta_max", 0.01, "rad"))
            .With(CalcInputValue.Quantity("n", 1500, "rpm"));

    /// <summary>A cam whose spring is too weak for 1500 rpm: the follower force falls below zero and the follower leaves the cam.</summary>
    internal static CalcInputSet WeakSpringCam() => new(
    [
        CalcInputValue.Choice("mode", MechanismCalculator.CamMode),
        CalcInputValue.Table("segs",
        [
            MechanismCalculator.Row(MechanismCalculator.Rise, MechanismCalculator.Cycloidal, 60, 20), MechanismCalculator.Row(MechanismCalculator.Dwell, null, 120, 0),
            MechanismCalculator.Row(MechanismCalculator.Return, MechanismCalculator.Cycloidal, 60, 20), MechanismCalculator.Row(MechanismCalculator.Dwell, null, 120, 0),
        ]),
        CalcInputValue.Quantity("n_in", 1500, "rpm"), CalcInputValue.Quantity("Rb", 40, "mm"), CalcInputValue.Quantity("R_r", 8, "mm"), CalcInputValue.Quantity("e_off", 0, "mm"), CalcInputValue.Quantity("m_f", 0.3, "kg"),
        CalcInputValue.Quantity("k_sp", 0.5, "N/mm"), CalcInputValue.Quantity("F_pre", 1, "N"), CalcInputValue.Quantity("F_ext", 0, "N"),
    ]);

    /// <summary>The DN80 100 m line with a pump curve that crosses the system curve once, where the pump curve rises (3.41 m³/h): the operating point is unstable.</summary>
    internal static CalcInputSet UnstablePump() => new(
    [
        CalcInputValue.Quantity("T_fluid", 20, "degC"), CalcInputValue.Quantity("Q", 15, "m^3/h"), PipeFlowCalculator.Segments(("riser", 100, 80, 0.045, 2, 0, 10, "discharge")),
        CalcInputValue.VectorOf("Q_pump", [0, 10, 20], "m^3/h"), CalcInputValue.VectorOf("H_pump", [8, 14, 18], "m"),
    ]);

    /// <summary>An emergency stroke sized with the isothermal exponent n = 1 (and a real-gas factor entered, so only the exponent can fail): n below 1.3 for a fast discharge.</summary>
    internal static CalcInputSet SlowEmergencyAccumulator() => AccumulatorBase().With(CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEmergency)).With(CalcInputValue.Of("n_gas", 1)).With(CalcInputValue.Of("f_rg", 1.15));

    /// <summary>The Parker numbers at 210 bar absolute with the ideal gas (f_rg left at 1): the real-gas check fails.</summary>
    internal static CalcInputSet IdealGasAccumulator() => AccumulatorBase();

    private static CalcInputSet AccumulatorBase() => new(
    [
        CalcInputValue.Quantity("dV", 14, "L"), CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute), CalcInputValue.Quantity("p_0", 90, "bar"), CalcInputValue.Quantity("p_1", 100, "bar"),
        CalcInputValue.Quantity("p_2", 210, "bar"), CalcInputValue.Of("n_gas", 1), CalcInputValue.Quantity("T_min", 50, "degC"),
    ]);

    /// <summary>A half-ounce trace, drawn wide enough: only the copper weight (outside 1 to 3 oz/ft²) fails.</summary>
    internal static CalcInputSet HalfOunceTrace() =>
        CalcTemplateHasher.DefaultInputs(new PcbTraceCalculator().Template).With(CalcInputValue.Of("Cu_oz", 0.5)).With(CalcInputValue.Quantity("w_act", 5, "mm"));

    /// <summary>The default base plate with prying switched on and no anchor design utilisation: the prying force Q is not in the bolt tension, and the prying check fails.</summary>
    internal static CalcInputSet PryingBasePlate() =>
        CalcTemplateHasher.DefaultInputs(new BasePlateCalculator().Template).With(CalcInputValue.Switch("prying", true));

    /// <summary>A long clamped plate: the Ritz solution stops at the stall rule without converging.</summary>
    internal static CalcInputSet LongClampedPlate() =>
        CalcTemplateHasher.DefaultInputs(new PlateBendingCalculator().Template)
            .With(CalcInputValue.Choice("shape", PlateBendingCalculator.Rectangular)).With(CalcInputValue.Choice("support", PlateBendingCalculator.Clamped)).With(CalcInputValue.Choice("load_type", PlateBendingCalculator.Uniform))
            .With(CalcInputValue.Quantity("a", 2500, "mm")).With(CalcInputValue.Quantity("b", 500, "mm")).With(CalcInputValue.Quantity("t", 8, "mm")).With(CalcInputValue.Quantity("q", 10, "kPa"))
            .With(CalcInputValue.Quantity("E", 210, "GPa")).With(CalcInputValue.Of("nu", 0.3));
}
