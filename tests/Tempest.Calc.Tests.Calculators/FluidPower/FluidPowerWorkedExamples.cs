using Tempest.Calc.FluidPower;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.FluidPower;

/// <summary>The Fluid Power worked examples (ADR-0170); written under <c>docs/worked-examples/fluid-power/</c>.</summary>
public static class FluidPowerWorkedExamples
{
    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(CylinderCalculator.Id, "press-ram-125-90-pump-and-lines", "Press ram: 125/90 cylinder at 180 bar on a 45 cm³/rev pump, lines checked, rod screened for buckling",
            new CalcReferenceSource("Analytic (closed-form areas, force, speed, pump flow and power, line continuity and the Johnson parabola; all numbers are an invented design example)", null, "A_b = π·125²/4 = 122.718 cm²; A_a = 122.718 − 63.617 = 59.101 cm²; F_ext = 180e5·122.718e-4·0.95 = 209.849 kN; Q_p = 45 cm³/rev·1450 rpm·0.95 = 61.9875 L/min; P = 180e5·61.9875e-3/60/0.88 = 21.132 kW; T = 139.170 N·m; return flow 61.9875·122.718/59.101 = 128.712 L/min, 2.667 m/s in 32 mm; λ = 950/22.5 = 42.22 < 91.05: Johnson F_cr = 2838.87 kN"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("mode", CylinderCalculator.Standard, CalcProvenance.Assumed("Standard connection: the ram needs its full bore force for the press stroke.")),
                CalcInputValue.Quantity("p", 180, "bar", CalcProvenance.Specification("Hydraulic specification HS-31: relief valve set 180 bar at the ram")),
                CalcInputValue.Quantity("p_back", 0, "bar", CalcProvenance.Assumed("Return line direct to tank; the return filter drop is neglected for this example.")),
                CalcInputValue.Quantity("Q", 60, "L/min", CalcProvenance.Specification("Hydraulic specification HS-31: ram flow 60 L/min")),
                CalcInputValue.Quantity("rho", 870, "kg/m^3", CalcProvenance.FromReference("fluid data sheet: mineral oil, density at 40 °C, to be taken from the oil supplier's data")),
                CalcInputValue.Quantity("F_push", 150, "kN", CalcProvenance.Specification("Hydraulic specification HS-31: pressing force 150 kN")),
                CalcInputValue.Quantity("F_pull", 60, "kN", CalcProvenance.Specification("Hydraulic specification HS-31: return stroke load 60 kN")),
                CalcInputValue.Quantity("D", 125, "mm", CalcProvenance.Drawing("DWG-4310", "B")),
                CalcInputValue.Quantity("d_r", 90, "mm", CalcProvenance.Drawing("DWG-4310", "B")),
                CalcInputValue.Quantity("stroke", 800, "mm", CalcProvenance.Drawing("DWG-4310", "B")),
                CalcInputValue.Of("eta_m", 0.95, CalcProvenance.Assumed("Typical seal and guide friction for a new cylinder; to be confirmed against the cylinder maker's figure.")),
                CalcInputValue.Of("eta_vc", 0.98, CalcProvenance.Assumed("Allowance for leakage past the piston; to be confirmed against the cylinder maker's figure.")),
                CalcInputValue.Quantity("t_dwell", 0, "s", CalcProvenance.Assumed("Cycle time of the strokes only; dwell added by the machine controller.")),
                CalcInputValue.Of("K_m", 1, CalcProvenance.FromReference("record: cylinder mounting (clevis at both ends, pinned), to be taken from the cylinder maker's mounting guidance")),
                CalcInputValue.Quantity("L_add", 150, "mm", CalcProvenance.Drawing("DWG-4310", "B")),
                CalcInputValue.Quantity("E", 210, "GPa", CalcProvenance.FromReference("record: rod material (chromed carbon steel), to be taken from the material library")),
                CalcInputValue.Quantity("f_y", 500, "MPa", CalcProvenance.FromReference("record: rod material (chromed carbon steel), to be taken from the rod maker's certificate")),
                CalcInputValue.Of("S_f", 3.5, CalcProvenance.Assumed("Safety factor chosen for this example; the design reference sets the real one.")),
                CalcInputValue.Quantity("V_g", 45, "cm^3/rev", CalcProvenance.Specification("Hydraulic specification HS-31: fixed-displacement pump 45 cm³/rev")),
                CalcInputValue.Quantity("n_p", 1450, "rpm", CalcProvenance.Specification("Hydraulic specification HS-31: 4-pole motor, 1450 rpm")),
                CalcInputValue.Of("eta_vp", 0.95, CalcProvenance.Assumed("Pump volumetric efficiency at 180 bar; to be taken from the pump data sheet.")),
                CalcInputValue.Of("eta_tp", 0.88, CalcProvenance.Assumed("Pump total efficiency at 180 bar; to be taken from the pump data sheet.")),
                CalcInputValue.Quantity("d_suc", 50, "mm", CalcProvenance.Drawing("DWG-4311", "A")),
                CalcInputValue.Quantity("d_prs", 25, "mm", CalcProvenance.Drawing("DWG-4311", "A")),
                CalcInputValue.Quantity("d_ret", 32, "mm", CalcProvenance.Drawing("DWG-4311", "A")),
                CalcInputValue.Quantity("v_suc_max", 1.2, "m/s", CalcProvenance.FromReference("record: line velocity limits, a placeholder to be replaced from the design reference")),
                CalcInputValue.Quantity("v_prs_max", 5, "m/s", CalcProvenance.FromReference("record: line velocity limits, a placeholder to be replaced from the design reference")),
                CalcInputValue.Quantity("v_ret_max", 3, "m/s", CalcProvenance.FromReference("record: line velocity limits, a placeholder to be replaced from the design reference")),
                CalcInputValue.Table("hoses", new[] { 16.0, 19, 25, 32, 38, 51, 63 }.Select(v => CalcRow.Of(("ID", new CalcCell(v, "mm")))).ToList()) with { Provenance = CalcProvenance.FromReference("record: illustrative hose size list invented for this example, to be replaced by the supplier's range") },
            ]),
            [new CalcExpectedValue("F_ext", 209.8485718, "kN", 1e-9), new CalcExpectedValue("F_ret", 101.0630722, "kN", 1e-9), new CalcExpectedValue("v_ext", 79.85758425, "mm/s", 1e-9), new CalcExpectedValue("t_cycle", 14.84242243, "s", 1e-9), new CalcExpectedValue("dV", 9.817477042, "L", 1e-9), new CalcExpectedValue("Q_p", 61.9875, "L/min", 1e-9), new CalcExpectedValue("u_pump", 0.9679370841, "", 1e-9), new CalcExpectedValue("P_pump", 21.13210227, "kW", 1e-9), new CalcExpectedValue("T_shaft", 139.1701463, "N*m", 1e-9), new CalcExpectedValue("v_rtn", 2.667328803, "m/s", 1e-9), new CalcExpectedValue("u_rtn", 0.8891096009, "", 1e-9), new CalcExpectedValue("d_rec_suc", 38, "mm", 1e-9), new CalcExpectedValue("F_cr", 2838.868904, "kN", 1e-9), new CalcExpectedValue("u_buck", 0.1849328087, "", 1e-9)],
            "The ram makes 209.8 kN on the press stroke and 101.1 kN on the return, against loads of 150 and 60 kN, and needs a swept volume of 9.82 L per stroke. The pump delivers 61.99 L/min, 3 % above the 60 L/min the ram takes, for 21.1 kW of shaft power and 139 N·m at 1450 rpm. The return line carries the cap-side discharge, 128.7 L/min on the retract stroke, at 2.67 m/s in the 32 mm line: close to its 3 m/s limit (utilisation 0.89), and it governs the line checks. The smallest suction bore the 1.2 m/s limit needs is 33.1 mm, so the next listed size is 38 mm; the 50 mm line drawn is larger than that. The 90 mm rod over 950 mm (clevis at both ends, K = 1) has a slenderness of 42, below the 91 transition, so the Johnson parabola gives a critical load of 2839 kN and a buckling utilisation of 0.18 at a safety factor of 3.5. This is a screening only; the ISO/TS 13725 method is not implemented. The velocity limits and the hose list are placeholders for your own records, and the figures are an invented design example.",
            CalcReferenceStatus.Confirmed),

        new(AccumulatorCalculator.Id, "clamp-circuit-bladder-accumulator", "Bladder accumulator for a clamp circuit: 14 L delivered between 100 and 210 bar",
            new CalcReferenceSource("Analytic (isothermal gas law p·V = const, V0 = ΔV/[(p0/p1) − (p0/p2)]; pressures after the Parker HY10-4004/UK numbers quoted in Addendum B, read as absolute bar; size list invented for this example)", null, "V0_ideal = 14/(0.9 − 0.428571) = 29.69697 L; oil range 30 to 50 °C: f_T = 323.15/303.15 = 1.065974, V0 = 31.65620 L; next listed size 32 L; p0 at 20 °C from 50 °C = 90·293.15/323.15 = 81.64475 bar abs; p0 at 30 °C = 90·303.15/323.15 = 84.42983 bar abs, p2/p0 = 2.48727; PS 230 bar gauge on the 32 L: bar·L = 230·32 = 7360"),
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
                CalcInputValue.Quantity("T_min", 30, "degC", CalcProvenance.Assumed("Oil temperature range assumed 30 to 50 °C in service (invented for this example); the gas is coldest at 30 °C.")),
                CalcInputValue.Quantity("T_c", 20, "degC", CalcProvenance.Assumed("Charged in the workshop at 20 °C.")),
                CalcInputValue.Of("r_0", 0.9, CalcProvenance.FromReference("record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide")),
                CalcInputValue.Of("r_p", 4, CalcProvenance.FromReference("record: accumulator pre-charge rules (bladder type), to be taken from the manufacturer's guide")),
                CalcInputValue.Table("sizes", new[] { 10.0, 20, 32, 50 }.Select(v => CalcRow.Of(("V", new CalcCell(v, "L")))).ToList()) with { Provenance = CalcProvenance.FromReference("record: illustrative size list invented for this example, to be replaced by the manufacturer's range") },
                CalcInputValue.Quantity("PS", 230, "bar", CalcProvenance.Specification("Hydraulic specification HS-12: accumulator relief setting 230 bar gauge (vessel PS; illustrative, to be replaced by the nameplate)")),
            ]),
            [new CalcExpectedValue("V0", 31.65619580, "L", 1e-9), new CalcExpectedValue("V_sel", 32, "L", 1e-9), new CalcExpectedValue("u_V", 0.9892561188, "", 1e-9), new CalcExpectedValue("p0", 81.64474702, "bar", 1e-9), new CalcExpectedValue("u_r0", 1.0, "", 1e-9), new CalcExpectedValue("u_rp", 0.6218181318, "", 1e-9), new CalcExpectedValue("bar_litres", 7360, "bar*L", 1e-9)],
            "Delivering 14 L between 100 and 210 bar from a gas pre-charge of 90 bar (all absolute, isothermal) needs 29.70 L of gas at the 50 °C maximum; because the oil can fall to 30 °C the gas delivers 6.6 % less when cold, so V0 is scaled by 323.15/303.15 to 31.66 L and the next listed size is 32 L (99 % utilised). The pre-charge sits exactly on the 0.9·p1 limit and the pressure ratio at the cold pre-charge of 84.4 bar (210/84.4 = 2.49) is well inside 4. Charged at 20 °C for a 50 °C maximum the pre-charge is 81.6 bar absolute. With the vessel's relief setting PS = 230 bar gauge the 32 L is 7360 bar·L, far above the 250 bar·L indication, and the sheet warns that 210 bar is above the 200 bar where real-gas behaviour should be considered. The numbers are those quoted for the Parker HY10-4004/UK example (not confirmed against the catalogue); the oil temperature range, the size list and the relief setting are illustrative.",
            CalcReferenceStatus.Confirmed),
    ];
}
