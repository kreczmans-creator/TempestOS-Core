using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.FluidPower;

/// <summary>The Fluid Power worked examples (ADR-0170); written under <c>docs/worked-examples/fluid-power/</c>.</summary>
public static class FluidPowerWorkedExamples
{
    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(AccumulatorCalculator.Id, "clamp-circuit-bladder-accumulator", "Bladder accumulator for a clamp circuit: 14 L delivered between 100 and 210 bar",
            new CalcReferenceSource("Analytic (isothermal gas law p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)]; pressures after the Parker HY10-4004/UK numbers quoted in Addendum B, read as absolute bar; size list invented for this example)", null, "V0 = 14/(0.9 − 0.428571) = 29.69697 L; next listed size 32 L; p0 at 20 °C from 50 °C = 90·293.15/323.15 = 81.64475 bar abs; PS = 210 − 1.01325 = 208.98675 bar; bar·L = 208.98675·32 = 6687.576"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("mode", AccumulatorCalculator.ModeEnergy, CalcProvenance.Assumed("Slow clamp-and-hold cycle: stored-energy duty.")),
                CalcInputValue.Quantity("dV", 14, "L", CalcProvenance.Specification("Hydraulic specification HS-12: clamp stroke plus leakage make-up, 14 L per cycle")),
                CalcInputValue.Choice("p_basis", AccumulatorCalculator.Absolute, CalcProvenance.Assumed("Pressures entered as absolute bar, as the source numbers are quoted.")),
                CalcInputValue.Quantity("p_0", 90, "bar", CalcProvenance.Specification("Hydraulic specification HS-12: pre-charge 90 bar at the maximum oil temperature")),
                CalcInputValue.Quantity("p_1", 100, "bar", CalcProvenance.Specification("Hydraulic specification HS-12: lowest clamp pressure 100 bar")),
                CalcInputValue.Quantity("p_2", 210, "bar", CalcProvenance.Specification("Hydraulic specification HS-12: pump cut-out 210 bar")),
                CalcInputValue.Quantity("p_atm", 1.01325, "bar", CalcProvenance.Assumed("Standard atmosphere, derived constant.")),
                CalcInputValue.Of("n", 1, CalcProvenance.Assumed("Isothermal: the clamp holds for minutes between cycles.")),
                CalcInputValue.Of("f_rg", 1, CalcProvenance.Assumed("Ideal gas; the highest pressure is just above 200 bar, so the sheet warns.")),
                CalcInputValue.Quantity("T_max", 50, "degC", CalcProvenance.Specification("Hydraulic specification HS-12: oil temperature limit 50 °C")),
                CalcInputValue.Quantity("T_c", 20, "degC", CalcProvenance.Assumed("Charged in the workshop at 20 °C.")),
                CalcInputValue.Of("r_0", 0.9, CalcProvenance.FromReference("record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide")),
                CalcInputValue.Of("r_p", 4, CalcProvenance.FromReference("record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide")),
                CalcInputValue.Table("sizes", new[] { 10.0, 20, 32, 50 }.Select(v => CalcRow.Of(("V", new CalcCell(v, "L")))).ToList()) with { Provenance = CalcProvenance.FromReference("record: illustrative size list invented for this example, to be replaced by the manufacturer's range") },
            ]),
            [new CalcExpectedValue("V0", 29.69696970, "L", 1e-9), new CalcExpectedValue("V_sel", 32, "L", 1e-9), new CalcExpectedValue("u_V", 0.9280303030, "", 1e-9), new CalcExpectedValue("p0", 81.64474702, "bar", 1e-9), new CalcExpectedValue("u_r0", 1.0, "", 1e-9), new CalcExpectedValue("u_rp", 0.5833333333, "", 1e-9), new CalcExpectedValue("bar_litres", 6687.576, "bar*L", 1e-9)],
            "Delivering 14 L between 100 and 210 bar from a gas pre-charge of 90 bar (all absolute, isothermal) needs 29.70 L of gas volume, so the next listed size is 32 L. The pre-charge sits exactly on the 0.9·p1 limit and the pressure ratio 210/90 is well inside 4. Charged at 20 °C for a 50 °C maximum the pre-charge is 81.6 bar absolute. The 32 L at 208.99 bar gauge is 6688 bar·L, far above the 250 bar·L indication, and the sheet warns that 210 bar is above the 200 bar where real-gas behaviour should be considered. The numbers are those quoted for the Parker HY10-4004/UK example (not confirmed against the catalogue); the size list is illustrative.",
            CalcReferenceStatus.Confirmed),
    ];
}
