using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.FluidPower;

/// <summary>
/// F1 — hydraulic cylinder and actuator circuit: the bore, rod and annulus areas; force and speed on both strokes (with the
/// mechanical and volumetric efficiencies, a back pressure on the discharge side and an optional regenerative extend); the
/// swept volume and cycle time; the pump delivery, shaft power, torque and speed; the hydraulic motor torque; the velocity in
/// the suction, pressure and return lines against the user's limits, with the recommended bore from the user's hose or pipe
/// size list; an orifice flow screening; and a rod buckling screening (Euler above the transition slenderness, Johnson parabola
/// below it, an effective-length factor from the mounting record, a safety factor, judged at the larger of the push load entered and the
/// stall thrust p·A_b). The buckling check is a screening only: the
/// ISO/TS 13725 method is not implemented and none of its text or tables is shipped (Addendum B §B5). The limits, the mounting
/// factor, the hose sizes and the material properties are the user's records; no catalogue table is shipped.
/// </summary>
public sealed class CylinderCalculator : ICalcCalculator
{
    public const string Id = "fp.cylinder";

    /// <summary>The flow the sheet opens on, L/min; a flow still at it beside a required extend speed is not warned about.</summary>
    public const double DefaultFlowLpm = 36;

    public const string Standard = "Standard";
    public const string Regenerative = "Regenerative extend";

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because the Fluid Power assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    /// <summary>The label every buckling result on the sheet carries.</summary>
    public const string ScreeningLabel = "screening only; ISO/TS 13725 method not implemented";

    private static readonly IReadOnlyList<CalcColumnSpec> HoseColumns =
    [
        new("ID", "Inside diameter", CalcInputKind.Quantity, "mm", Min: 3, Max: 200, HardMin: 3, HardMax: 200),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.3.1", CalcDiscipline.FluidPower, "Cylinder and actuator circuit",
        "A double-acting hydraulic cylinder in its circuit: the bore, rod and annulus areas; the force and speed on the extend and the retract stroke with mechanical and volumetric efficiency and an optional back pressure, or with the regenerative extend connection; the swept volume and the cycle time; the pump delivery, shaft power, torque and speed; a hydraulic motor's torque; the velocity in the suction, pressure and return lines against your own limits and the smallest listed hose or pipe bore that meets them; a sharp orifice's flow for flow-control screening; and a screening check of the rod against buckling (Euler above the transition slenderness, Johnson parabola below it). " + ScreeningLabel + ". Not a cylinder design to a standard: the limits, the hose sizes, the mounting factor and the material properties are your records.",
        "Piston and rod areas: A_b = π·D²/4, A_r = π·d_r²/4, A_a = A_b − A_r, area ratio φ = A_b/A_a\n" +
        "Cylinder force: F_ext = (p·A_b − p_back·A_a)·η_m, F_ret = (p·A_a − p_back·A_b)·η_m; regenerative extend F_ext = p·A_r·η_m\n" +
        "Speed and time: v_ext = Q·η_vc/A_b (regenerative: A_r), v_ret = Q·η_vc/A_a; t = stroke/v; t_cycle = t_ext + t_ret + t_dwell\n" +
        "Required speed (optional): when v_req is given it gives the flow, Q = v_req·A_b/η_vc (regenerative: A_r), and replaces the entered Q; the flow must lie within 0.1 to 2000 L/min\n" +
        "Swept volume: ΔV_ext = A_b·stroke (regenerative: A_r·stroke), ΔV_ret = A_a·stroke; ΔV = the larger\n" +
        "Pump flow and power: Q_p = V_g·n·η_vp; P = p·Q_p/η_tp; T = P/ω; displacement needed V_g,req = Q/(n·η_vp)\n" +
        "Hydraulic motor: T = Δp·V_g/(2π)·η_hm (V_g per revolution)\n" +
        "Line velocities: v = Q_line/A_line; suction Q_suc = max(Q, V_g·n) (the geometric delivery), pressure/cap-port line Q_prs = Q_line·A_b/A_a on the retract stroke (regenerative extend: Q_line·A_b/A_r if larger), return Q_rtn = Q_line·A_b/A_a, with Q_line = max(Q, Q_p); smallest bore d = √(4Q/(π·v_max)), recommended = the smallest listed size not below it\n" +
        "Orifice flow: Q = C_d·A_o·√(2Δp/ρ); bore for the circuit flow d_o = √(4Q/(π·C_d·√(2Δp/ρ)))\n" +
        "Rod buckling (screening): L_eff = K·(stroke + L_add), r = d_r/4, λ = L_eff/r, λ_c = π·√(2E/f_y); Euler F_cr = π²·E·I/L_eff² for λ > λ_c, Johnson F_cr = A_r·f_y·[1 − f_y·λ²/(4π²E)] for λ ≤ λ_c; load F_b = max(F_push, F_stall) with F_stall = p·A_b (the stalled cylinder at the supply pressure: no flow, so no back pressure and no friction loss; in the regenerative extend too, since the standard connection can always be selected); utilisation u = F_b·S_f/F_cr",
        [
            new CalcInputSpec("mode", "Extend connection", CalcInputKind.Choice, Choices: [Standard, Regenerative], DefaultChoice: Standard, Group: "Duty"),
            new CalcInputSpec("p", "Supply pressure at the cylinder and the pump (gauge)", CalcInputKind.Quantity, "bar", Default: 160, Min: 1, Max: 700, HardMin: 1, HardMax: 700, Group: "Duty", PressureBasis: CalcInputSpec.BasisGauge),
            new CalcInputSpec("p_back", "Back pressure on the discharge side (standard connection, gauge)", CalcInputKind.Quantity, "bar", Default: 0, Min: 0, Max: 700, HardMin: 0, HardMax: 700, Group: "Duty", PressureBasis: CalcInputSpec.BasisGauge),
            new CalcInputSpec("Q", "Flow into the cylinder (blank when a required extend speed gives it)", CalcInputKind.Quantity, "L/min", Default: DefaultFlowLpm, Min: 0.1, Max: 2000, HardMin: 0.1, HardMax: 2000, IsOptional: true, Group: "Duty"),
            new CalcInputSpec("v_req", "Required extend speed (gives the flow Q = v·A/η_vc and replaces Q; links M9 output_motion as a kinematic proxy: a cylinder-actuated linkage runs at about constant cylinder speed)", CalcInputKind.Quantity, "mm/s", Min: 0.1, Max: 10000, HardMin: 0.1, HardMax: 10000, IsOptional: true, Group: "Duty"),
            new CalcInputSpec("rho", "Fluid density", CalcInputKind.Quantity, "kg/m^3", Default: 870, Min: 700, Max: 1300, HardMin: 700, HardMax: 1300, Group: "Duty"),
            new CalcInputSpec("F_push", "Load to push on the extend stroke (compresses the rod)", CalcInputKind.Quantity, "kN", Default: 80, Min: 0, Max: 15000, HardMin: 0, HardMax: 15000, Group: "Load"),
            new CalcInputSpec("F_pull", "Load to pull on the retract stroke", CalcInputKind.Quantity, "kN", Default: 40, Min: 0, Max: 15000, HardMin: 0, HardMax: 15000, Group: "Load"),
            new CalcInputSpec("D", "Bore", CalcInputKind.Quantity, "mm", Default: 100, Min: 10, Max: 500, HardMin: 10, HardMax: 500, Group: "Cylinder"),
            new CalcInputSpec("d_r", "Rod diameter", CalcInputKind.Quantity, "mm", Default: 70, Min: 4, Max: 450, HardMin: 4, HardMax: 450, Group: "Cylinder"),
            new CalcInputSpec("stroke", "Stroke", CalcInputKind.Quantity, "mm", Default: 500, Min: 1, Max: 5000, HardMin: 1, HardMax: 5000, Group: "Cylinder"),
            new CalcInputSpec("eta_m", "Mechanical efficiency of the cylinder (seal and guide friction)", CalcInputKind.Number, Default: 0.95, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Cylinder"),
            new CalcInputSpec("eta_vc", "Volumetric efficiency of the cylinder (leakage past the piston)", CalcInputKind.Number, Default: 0.98, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Cylinder"),
            new CalcInputSpec("t_dwell", "Dwell time added to each cycle", CalcInputKind.Quantity, "s", Default: 0, Min: 0, Max: 3600, HardMin: 0, HardMax: 3600, Group: "Cylinder"),
            new CalcInputSpec("K_m", "Effective-length factor K of the mounting (L_eff = K·L)", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 2.5, HardMin: 0.5, HardMax: 2.5, Group: "Rod buckling"),
            new CalcInputSpec("L_add", "Length added to the stroke for the buckling length (mounting and rod end beyond the stroke)", CalcInputKind.Quantity, "mm", Default: 100, Min: 0, Max: 5000, HardMin: 0, HardMax: 5000, Group: "Rod buckling"),
            new CalcInputSpec("E", "Young's modulus of the rod", CalcInputKind.Quantity, "GPa", Default: 210, Min: 50, Max: 250, HardMin: 50, HardMax: 250, Group: "Rod buckling", MaterialProperty: "E"),
            new CalcInputSpec("f_y", "Yield strength of the rod", CalcInputKind.Quantity, "MPa", Default: 500, Min: 100, Max: 1500, HardMin: 100, HardMax: 1500, Group: "Rod buckling", MaterialProperty: "f_y"),
            new CalcInputSpec("S_f", "Safety factor on the critical load", CalcInputKind.Number, Default: 3.5, Min: 1, Max: 20, HardMin: 1, HardMax: 20, Group: "Rod buckling"),
            new CalcInputSpec("V_g", "Pump displacement per revolution (leave blank to skip the pump delivery check)", CalcInputKind.Quantity, "cm^3/rev", Min: 0.1, Max: 1000, HardMin: 0.1, HardMax: 1000, IsOptional: true, Group: "Pump"),
            new CalcInputSpec("n_p", "Pump speed", CalcInputKind.Quantity, "rpm", Min: 50, Max: 6000, HardMin: 50, HardMax: 6000, IsOptional: true, Group: "Pump"),
            new CalcInputSpec("eta_vp", "Volumetric efficiency of the pump", CalcInputKind.Number, Default: 0.95, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Pump"),
            new CalcInputSpec("eta_tp", "Total efficiency of the pump (volumetric × hydromechanical)", CalcInputKind.Number, Default: 0.88, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Pump"),
            new CalcInputSpec("V_gm", "Hydraulic motor displacement per revolution (leave blank for no motor)", CalcInputKind.Quantity, "cm^3/rev", Min: 0.1, Max: 2000, HardMin: 0.1, HardMax: 2000, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("dp_m", "Pressure drop across the motor", CalcInputKind.Quantity, "bar", Min: 1, Max: 700, HardMin: 1, HardMax: 700, IsOptional: true, Group: "Motor"),
            new CalcInputSpec("eta_hm", "Hydromechanical efficiency of the motor", CalcInputKind.Number, Default: 0.9, Min: 0.5, Max: 1, HardMin: 0.5, HardMax: 1, Group: "Motor"),
            new CalcInputSpec("d_suc", "Suction line inside diameter", CalcInputKind.Quantity, "mm", Default: 40, Min: 3, Max: 200, HardMin: 3, HardMax: 200, Group: "Lines"),
            new CalcInputSpec("d_prs", "Pressure line inside diameter (to the cylinder cap port; judged at the larger flow of the two strokes)", CalcInputKind.Quantity, "mm", Default: 16, Min: 3, Max: 200, HardMin: 3, HardMax: 200, Group: "Lines"),
            new CalcInputSpec("d_ret", "Return line inside diameter", CalcInputKind.Quantity, "mm", Default: 25, Min: 3, Max: 200, HardMin: 3, HardMax: 200, Group: "Lines"),
            new CalcInputSpec("v_suc_max", "Suction line velocity limit (your record; the default is a placeholder, not a code value)", CalcInputKind.Quantity, "m/s", Default: 1.2, Min: 0.1, Max: 20, HardMin: 0.1, HardMax: 20, Group: "Line limits"),
            new CalcInputSpec("v_prs_max", "Pressure line velocity limit (your record; the default is a placeholder, not a code value)", CalcInputKind.Quantity, "m/s", Default: 5, Min: 0.1, Max: 20, HardMin: 0.1, HardMax: 20, Group: "Line limits"),
            new CalcInputSpec("v_ret_max", "Return line velocity limit (your record; the default is a placeholder, not a code value)", CalcInputKind.Quantity, "m/s", Default: 3, Min: 0.1, Max: 20, HardMin: 0.1, HardMax: 20, Group: "Line limits"),
            new CalcInputSpec("hoses", "Available hose or pipe inside diameters (from your supplier's range)", CalcInputKind.Table, Columns: HoseColumns, MaxRows: 60, IsOptional: true, Group: "Line limits"),
            new CalcInputSpec("C_d", "Orifice discharge coefficient", CalcInputKind.Number, Default: 0.62, Min: 0.2, Max: 1, HardMin: 0.2, HardMax: 1, Group: "Orifice"),
            new CalcInputSpec("dp_o", "Pressure drop across the orifice (leave blank to skip the orifice screening)", CalcInputKind.Quantity, "bar", Min: 0.1, Max: 700, HardMin: 0.1, HardMax: 700, IsOptional: true, Group: "Orifice"),
            new CalcInputSpec("d_o", "Orifice diameter (optional: gives the flow it passes)", CalcInputKind.Quantity, "mm", Min: 0.1, Max: 100, HardMin: 0.1, HardMax: 100, IsOptional: true, Group: "Orifice"),
        ],
        [
            new CalcAssumption("The supply pressure p is the gauge pressure at the cylinder port and, for the pump, at the pump outlet; line losses are not subtracted (the line losses of a pipe run come from the pipe calculation). The force is the pressure on the area less the back pressure on the other side, times the mechanical efficiency.", "F = p·A·η_m, closed form."),
            new CalcAssumption("The speed is the flow into the cylinder times the volumetric efficiency over the area, in steady motion: no acceleration, cushioning or compressibility. The cycle time is the two strokes plus the dwell.", "v = Q·η_vc/A."),
            new CalcAssumption("The pump power is the supply pressure times the pump's delivery over the total efficiency: when the pump delivers more than the circuit takes, the excess is taken to go over the relief valve at the supply pressure, which is the conservative case for the drive. The shaft torque is the power over the angular speed.", "P = p·Q_p/η_tp."),
            new CalcAssumption("The line flows use the pump delivery when it is larger than the circuit flow: the suction line the geometric delivery V_g·n, the pressure line and the return line Q_line = max(Q, Q_p), which carries no volumetric efficiency while the speeds use Q·η_vc (the flows are the conservative side of the speeds). The line to the cylinder cap port is judged at the larger flow of the two strokes: on the retract stroke it carries the cap-side discharge Q_line·A_b/A_a (the same flow as the return line), and in the regenerative extend Q_line·A_b/A_r if that is larger. The velocity limits are your records; the sheet's defaults are placeholders, not values from a standard.", "Continuity."),
            new CalcAssumption("The orifice relation is the sharp-edged turbulent one, Q = C_d·A·√(2Δp/ρ), with your discharge coefficient; it ignores viscosity and cavitation and is a screening of a flow-control orifice, not a valve selection.", "Orifice equation, closed form."),
            new CalcAssumption("The rod is treated as a solid round column of its own diameter with the effective-length factor of the mounting; the tube, the piston and the rod-end stiffness are not modelled. The rod is judged at the larger of the push load you enter and the stall thrust p·A_b (a stalled, obstructed or relief-limited cylinder loads the rod with the full pressure force on the bore: no flow, so no back pressure, and the seal friction does not relieve it); no pressure-limiting device is credited, and the regenerative extend is judged at the same stall thrust because the standard connection can always be selected. The mounting factor K_m defaults to 1 (pinned at both ends): a fixed-free mounting is K_m = 2, and the factor and the safety factor S_f are your records. " + ScreeningLabel + ".", "Euler and Johnson column relations, closed form."),
        ],
        [
            new CalcClauseRef("ISO/TS 13725", "—", "Cylinder buckling load: method not implemented, screening only"),
            new CalcClauseRef("Esposito, Fluid Power with Applications", "Cylinder and pump chapters", "Force, speed, flow and power relations, closed form"),
        ],
        [
            new CalcApplicabilityRule("rod", "The rod must be smaller than the bore.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("flow", "A flow into the cylinder or a required extend speed is needed, and the flow the speed needs must lie within 0.1 to 2000 L/min.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("v-req-q", "A flow other than the sheet's opening 36 L/min is entered as well as a required extend speed: the flow from the speed is used and Q ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("force", "The back pressure must leave a net force on both strokes.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("pump", "The pump displacement and speed are entered together, and the total efficiency cannot exceed the volumetric efficiency.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("motor", "The motor displacement and pressure drop are entered together.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("orifice", "The orifice diameter needs the pressure drop; a drop above the supply pressure is not possible.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("size", "No listed hose or pipe size is as large as the smallest bore the velocity limit needs.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-circuit-standard", "Standard 100/70 cylinder at 160 bar with pump, motor, lines, orifice and rod check (analytic)",
                new CalcReferenceSource("Analytic (closed-form areas, force, speed, pump, line, orifice and Johnson column relations)", null, "A_b = π·100²/4 = 7853.98 mm² = 78.5398 cm²; A_r = 38.4845 cm²; A_a = 40.0553 cm²; F_ext = 160e5·78.5398e-4·0.95 = 119.3805 kN; F_ret = 160e5·40.0553e-4·0.95 = 60.8841 kN; v_ext = 36e-3/60·0.98/78.5398e-4 = 74.8665 mm/s; Q_p = 28 cm³/rev·1450 rpm·0.95 = 38.57 L/min; P = 160e5·38.57e-3/60/0.88 = 11.6879 kW; T = P/ω = 76.973 N·m; motor T = 160e5·16e-6/(2π)·0.9 = 36.669 N·m; suction at the geometric delivery 28 cm³/rev·1450 rpm = 40.6 L/min: v_suc = 40.6e-3/60/(π·0.04²/4) = 0.53847 m/s; cap-port line on the retract stroke 38.57·A_b/A_a = 75.627 L/min: v_prs = 75.627e-3/60/(π·0.016²/4) = 6.2690 m/s (utilisation 1.254 against 5 m/s, a FAIL), d_min = 17.916 mm so the next listed size is 20 mm; d_o = √(4Q/(π·0.62·√(2·10e5/870))) = 5.0694 mm; λ = 600/17.5 = 34.2857 < λ_c = π√(2·210e3/500) = 91.052: Johnson F_cr = 38.4845e-4 m²·500e6·(1 − 500·34.2857²/(4π²·210e3)) = 1787.81 kN; the rod load is the larger of the 80 kN push load and the stall thrust p·A_b = 160e5·78.5398e-4 = 125.664 kN (no η_m), so u_buck = 125.66·3.5/1787.81 = 0.24601"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 160, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("Q", 36, "L/min"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 80, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 70, "mm"), CalcInputValue.Quantity("stroke", 500, "mm"), CalcInputValue.Of("eta_m", 0.95), CalcInputValue.Of("eta_vc", 0.98), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Quantity("V_g", 28, "cm^3/rev"), CalcInputValue.Quantity("n_p", 1450, "rpm"), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Quantity("V_gm", 16, "cm^3/rev"), CalcInputValue.Quantity("dp_m", 160, "bar"), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62), CalcInputValue.Quantity("dp_o", 10, "bar"), CalcInputValue.Quantity("d_o", 3, "mm"), Hoses(8, 10, 12.5, 16, 20, 25, 32, 40, 50)],
                [new CalcExpectedValue("A_b", 78.53981633974483, "cm^2", 1e-9), new CalcExpectedValue("A_r", 38.48451000647497, "cm^2", 1e-9), new CalcExpectedValue("A_a", 40.05530633326986, "cm^2", 1e-9), new CalcExpectedValue("phi", 1.9607843137254901, "", 1e-9), new CalcExpectedValue("F_ext", 119.38052083641215, "kN", 1e-9), new CalcExpectedValue("F_ret", 60.88406562657019, "kN", 1e-9), new CalcExpectedValue("v_ext", 74.86648523042757, "mm/s", 1e-9), new CalcExpectedValue("v_ret", 146.79702986358348, "mm/s", 1e-9), new CalcExpectedValue("t_ext", 6.6785558112027905, "s", 1e-9), new CalcExpectedValue("t_ret", 3.4060634637134233, "s", 1e-9), new CalcExpectedValue("t_cycle", 10.084619274916214, "s", 1e-9), new CalcExpectedValue("dV_ext", 3.926990816987242, "L", 1e-9), new CalcExpectedValue("dV_ret", 2.0027653166634933, "L", 1e-9), new CalcExpectedValue("dV", 3.926990816987242, "L", 1e-9), new CalcExpectedValue("Q_p", 38.56999999999999, "L/min", 1e-9), new CalcExpectedValue("Vg_req", 26.134301270417424, "cm^3/rev", 1e-9), new CalcExpectedValue("u_pump", 0.9333679025149082, "", 1e-9), new CalcExpectedValue("P_pump", 11.687878787878786, "kW", 1e-9), new CalcExpectedValue("T_shaft", 76.97311793171664, "N*m", 1e-9), new CalcExpectedValue("n_shaft", 1450, "rpm", 1e-9), new CalcExpectedValue("T_motor", 36.66929888837269, "N*m", 1e-9), new CalcExpectedValue("Q_line", 38.56999999999999, "L/min", 1e-9), new CalcExpectedValue("Q_suc", 40.6, "L/min", 1e-9), new CalcExpectedValue("Q_prs", 75.62745098039215, "L/min", 1e-9), new CalcExpectedValue("Q_rtn", 75.62745098039215, "L/min", 1e-9), new CalcExpectedValue("v_suc", 0.5384742241275793, "m/s", 1e-9), new CalcExpectedValue("v_prs", 6.269001383838239, "m/s", 1e-9), new CalcExpectedValue("v_rtn", 2.5677829668201424, "m/s", 1e-9), new CalcExpectedValue("u_suc", 0.4487285201063161, "", 1e-9), new CalcExpectedValue("u_prs", 1.2538002767676477, "", 1e-9), new CalcExpectedValue("u_rtn", 0.8559276556067141, "", 1e-9), new CalcExpectedValue("d_min_suc", 26.794880708264138, "mm", 1e-9), new CalcExpectedValue("d_min_prs", 17.91571575049453, "mm", 1e-9), new CalcExpectedValue("d_min_rtn", 23.129089579017077, "mm", 1e-9), new CalcExpectedValue("d_rec_suc", 32, "mm", 1e-9), new CalcExpectedValue("d_rec_prs", 20, "mm", 1e-9), new CalcExpectedValue("d_rec_rtn", 25, "mm", 1e-9), new CalcExpectedValue("d_o_req", 5.0694069278021825, "mm", 1e-9), new CalcExpectedValue("Q_o", 12.607550087575712, "L/min", 1e-9), new CalcExpectedValue("L_eff", 600, "mm", 1e-9), new CalcExpectedValue("lam", 34.285714285714285, "", 1e-9), new CalcExpectedValue("lam_c", 91.0520054524614, "", 1e-9), new CalcExpectedValue("F_cr", 1787.8069776735522, "kN", 1e-9), new CalcExpectedValue("F_allow", 510.8019936210149, "kN", 1e-9), new CalcExpectedValue("F_stall", 125.66370614359174, "kN", 1e-9), new CalcExpectedValue("F_buck", 125.66370614359174, "kN", 1e-9), new CalcExpectedValue("u_buck", 0.2460125600778818, "", 1e-9), new CalcExpectedValue("u_push", 0.6701260761764014, "", 1e-9), new CalcExpectedValue("u_pull", 0.6569863491925504, "", 1e-9)]),
            new CalcTestCase("ka-regenerative", "Regenerative extend: force on the rod area, speed over the rod area, cap-side line flow Q·A_b/A_r (analytic)",
                new CalcReferenceSource("Analytic (regenerative connection: F = p·A_r·η_m, v = Q·η_vc/A_r, Q_cap = Q·A_b/A_r)", null, "F_ext = 160e5·38.4845e-4·0.95 = 58.4965 kN (standard 119.3805 kN); v_ext = 36e-3/60·0.98/38.4845e-4 = 152.7887 mm/s; ΔV_ext = A_r·500 mm = 1.92423 L; pump 38.57 L/min so Q_prs = 38.57·A_b/A_r = 78.7143 L/min (larger than the retract stroke's 38.57·A_b/A_a = 75.627 L/min); v_prs = 78.7143e-3/60/(π·0.025²/4) = 2.6726 m/s; the rod is judged at the stall thrust of the standard connection, p·A_b = 125.664 kN (the regenerative thrust is lower): u_buck = 125.664·3.5/1787.81 = 0.24601"),
                [CalcInputValue.Choice("mode", Regenerative), CalcInputValue.Quantity("p", 160, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("Q", 36, "L/min"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 40, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 70, "mm"), CalcInputValue.Quantity("stroke", 500, "mm"), CalcInputValue.Of("eta_m", 0.95), CalcInputValue.Of("eta_vc", 0.98), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Quantity("V_g", 28, "cm^3/rev"), CalcInputValue.Quantity("n_p", 1450, "rpm"), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 25, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62)],
                [new CalcExpectedValue("F_std", 119.38052083641215, "kN", 1e-9), new CalcExpectedValue("F_ext", 58.496455209841955, "kN", 1e-9), new CalcExpectedValue("v_ext", 152.78874536821954, "mm/s", 1e-9), new CalcExpectedValue("t_ext", 3.2724923474893677, "s", 1e-9), new CalcExpectedValue("t_cycle", 6.678555811202791, "s", 1e-9), new CalcExpectedValue("dV_ext", 1.9242255003237485, "L", 1e-9), new CalcExpectedValue("dV_ret", 2.0027653166634933, "L", 1e-9), new CalcExpectedValue("dV", 2.0027653166634933, "L", 1e-9), new CalcExpectedValue("Q_prs", 78.7142857142857, "L/min", 1e-9), new CalcExpectedValue("v_prs", 2.672590434853617, "m/s", 1e-9), new CalcExpectedValue("u_prs", 0.5345180869707234, "", 1e-9), new CalcExpectedValue("d_min_prs", 18.27768596832493, "mm", 1e-9), new CalcExpectedValue("u_push", 0.6838021185473483, "", 1e-9), new CalcExpectedValue("F_buck", 125.66370614359174, "kN", 1e-9), new CalcExpectedValue("u_buck", 0.2460125600778818, "", 1e-9)]),
            new CalcTestCase("ka-euler-slender", "Slender rod: 40 mm rod over 2100 mm is Euler-buckled and the cylinder's thrust is above its critical load (analytic)",
                new CalcReferenceSource("Analytic (Euler F_cr = π²EI/(KL)² above λ_c = π√(2E/f_y))", null, "I = π·40⁴/64 = 125 663.7 mm⁴; L_eff = 1·(2000 + 100) = 2100 mm; λ = 2100/10 = 210 > λ_c = 91.052; F_cr = π²·210e3·125 663.7/2100² = 59.0596 kN; allowable 59.0596/3.5 = 16.8742 kN; the 15 kN push load alone would read u = 0.88893, but a stalled cylinder at 160 bar carries p·A_b = 160e5·50.2655e-4 = 80.4248 kN (76.4035 kN with the seal friction η_m, which does not relieve a stalled rod), above the Euler load 59.0596 kN even with no safety factor, so u = 80.4248·3.5/59.0596 = 4.76615 (a FAIL)"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 160, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("Q", 36, "L/min"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 15, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 80, "mm"), CalcInputValue.Quantity("d_r", 40, "mm"), CalcInputValue.Quantity("stroke", 2000, "mm"), CalcInputValue.Of("eta_m", 0.95), CalcInputValue.Of("eta_vc", 0.98), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62)],
                [new CalcExpectedValue("L_eff", 2100, "mm", 1e-9), new CalcExpectedValue("lam", 210, "", 1e-9), new CalcExpectedValue("lam_c", 91.0520054524614, "", 1e-9), new CalcExpectedValue("F_cr", 59.05957462914251, "kN", 1e-9), new CalcExpectedValue("F_allow", 16.874164179755002, "kN", 1e-9), new CalcExpectedValue("F_stall", 80.4247719318987, "kN", 1e-9), new CalcExpectedValue("F_buck", 80.4247719318987, "kN", 1e-9), new CalcExpectedValue("u_buck", 4.766148478535569, "", 1e-9), new CalcExpectedValue("F_ret", 57.30265000147782, "kN", 1e-9), new CalcExpectedValue("F_ext", 76.40353333530378, "kN", 1e-9)]),
            new CalcTestCase("ka-back-pressure-fixed-free", "Back pressure 10 bar and a fixed-free mounting K = 2: Johnson parabola (analytic)",
                new CalcReferenceSource("Analytic (F = (p·A − p_back·A_other)·η_m; Johnson F_cr = A·f_y·[1 − f_y·λ²/(4π²E)] for λ ≤ λ_c)", null, "F_ext = (160e5·78.5398e-4 − 10e5·40.0553e-4)·0.95 = 115.5753 kN; F_ret = (160e5·40.0553e-4 − 10e5·78.5398e-4)·0.95 = 53.4228 kN; L_eff = 2·(300 + 100) = 800 mm; λ = 800/17.5 = 45.7143 < 91.052; F_cr = 38.4845e-4·500e6·(1 − 500·45.7143²/(4π²·210e3)) = 1681.70 kN; the rod load is the stall thrust p·A_b = 125.664 kN (no back pressure at stall, no η_m): u_buck = 125.664·3.5/1681.70 = 0.26153"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 160, "bar"), CalcInputValue.Quantity("p_back", 10, "bar"), CalcInputValue.Quantity("Q", 36, "L/min"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 80, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 70, "mm"), CalcInputValue.Quantity("stroke", 300, "mm"), CalcInputValue.Of("eta_m", 0.95), CalcInputValue.Of("eta_vc", 0.98), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 2), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62)],
                [new CalcExpectedValue("F_ext", 115.57526673475151, "kN", 1e-9), new CalcExpectedValue("F_ret", 53.42278307429443, "kN", 1e-9), new CalcExpectedValue("L_eff", 800, "mm", 1e-9), new CalcExpectedValue("lam", 45.714285714285715, "", 1e-9), new CalcExpectedValue("F_cr", 1681.7036822789553, "kN", 1e-9), new CalcExpectedValue("F_allow", 480.4867663654158, "kN", 1e-9), new CalcExpectedValue("F_buck", 125.66370614359174, "kN", 1e-9), new CalcExpectedValue("u_buck", 0.26153416689112935, "", 1e-9), new CalcExpectedValue("t_ext", 4.007133486721674, "s", 1e-9), new CalcExpectedValue("t_ret", 2.043638078228054, "s", 1e-9), new CalcExpectedValue("dV", 2.356194490192345, "L", 1e-9)]),
            new CalcTestCase("ka-orifice", "Sharp orifice: Q = C_d·A·√(2Δp/ρ) with ρ = 800 kg/m³, Δp = 10 bar (analytic)",
                new CalcReferenceSource("Analytic (orifice equation, √(2·10e5/800) = 50 m/s)", null, "v = 0.6·50 = 30 m/s; a 4 mm orifice has A = 12.5664 mm² and passes 30·12.5664e-6 = 3.76991e-4 m³/s = 22.6195 L/min; the bore for 22.6195 L/min is therefore 4 mm"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 160, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("Q", 22.619467105846514, "L/min"), CalcInputValue.Quantity("rho", 800, "kg/m^3"), CalcInputValue.Quantity("F_push", 80, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 70, "mm"), CalcInputValue.Quantity("stroke", 500, "mm"), CalcInputValue.Of("eta_m", 0.95), CalcInputValue.Of("eta_vc", 0.98), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.6), CalcInputValue.Quantity("dp_o", 10, "bar"), CalcInputValue.Quantity("d_o", 4, "mm")],
                [new CalcExpectedValue("d_o_req", 4, "mm", 1e-9), new CalcExpectedValue("Q_o", 22.61946710584651, "L/min", 1e-9)]),
            new CalcTestCase("ka-lossless", "Lossless cylinder: F = p·A and v = Q/A exactly (analytic)",
                new CalcReferenceSource("Analytic (η = 1: F_ext = p·A_b, F_ret = p·A_a, v = Q/A)", null, "p = 100 bar, D = 100 mm, d_r = 56 mm: F_ext = 100e5·78.5398e-4 = 78.5398 kN; A_r = 24.6301 cm²; A_a = 53.9097 cm²; F_ret = 53.9097 kN; Q = 47.1239 L/min gives v_ext = 100 mm/s (A_b·0.1 m/s = 7.85398e-4 m³/s = 47.1239 L/min); stroke 400 mm: t_ext = 4 s"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 100, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("Q", 47.1238898038469, "L/min"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 80, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 56, "mm"), CalcInputValue.Quantity("stroke", 400, "mm"), CalcInputValue.Of("eta_m", 1), CalcInputValue.Of("eta_vc", 1), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62)],
                [new CalcExpectedValue("A_a", 53.90972993560086, "cm^2", 1e-9), new CalcExpectedValue("F_ext", 78.53981633974483, "kN", 1e-9), new CalcExpectedValue("F_ret", 53.90972993560086, "kN", 1e-9), new CalcExpectedValue("v_ext", 99.99999999999999, "mm/s", 1e-9), new CalcExpectedValue("t_ext", 4.000000000000001, "s", 1e-9), new CalcExpectedValue("v_ret", 145.68764568764567, "mm/s", 1e-9), new CalcExpectedValue("t_ret", 2.7456000000000005, "s", 1e-9), new CalcExpectedValue("dV", 3.1415926535897936, "L", 1e-9)]),

            new CalcTestCase("ka-required-speed", "Lossless cylinder driven at a required extend speed: Q = v·A_b exactly (analytic)",
                new CalcReferenceSource("Analytic (η = 1: Q = v_req·A_b; the flow field is blank)", null, "D = 100 mm: A_b = 78.5398 cm²; v_req = 100 mm/s needs Q = 0.1 m/s · 7.85398e-3 m² = 7.85398e-4 m³/s = 47.1239 L/min, which is the flow of ka-lossless, so the strokes, times and swept volume are the same (v_ext 100 mm/s, t_ext 4 s, dV 3.14159 L)"),
                [CalcInputValue.Choice("mode", Standard), CalcInputValue.Quantity("p", 100, "bar"), CalcInputValue.Quantity("p_back", 0, "bar"), CalcInputValue.Quantity("v_req", 100, "mm/s"), CalcInputValue.Quantity("rho", 870, "kg/m^3"), CalcInputValue.Quantity("F_push", 80, "kN"), CalcInputValue.Quantity("F_pull", 40, "kN"), CalcInputValue.Quantity("D", 100, "mm"), CalcInputValue.Quantity("d_r", 56, "mm"), CalcInputValue.Quantity("stroke", 400, "mm"), CalcInputValue.Of("eta_m", 1), CalcInputValue.Of("eta_vc", 1), CalcInputValue.Quantity("t_dwell", 0, "s"), CalcInputValue.Of("K_m", 1), CalcInputValue.Quantity("L_add", 100, "mm"), CalcInputValue.Quantity("E", 210, "GPa"), CalcInputValue.Quantity("f_y", 500, "MPa"), CalcInputValue.Of("S_f", 3.5), CalcInputValue.Of("eta_vp", 0.95), CalcInputValue.Of("eta_tp", 0.88), CalcInputValue.Of("eta_hm", 0.9), CalcInputValue.Quantity("d_suc", 40, "mm"), CalcInputValue.Quantity("d_prs", 16, "mm"), CalcInputValue.Quantity("d_ret", 25, "mm"), CalcInputValue.Quantity("v_suc_max", 1.2, "m/s"), CalcInputValue.Quantity("v_prs_max", 5, "m/s"), CalcInputValue.Quantity("v_ret_max", 3, "m/s"), CalcInputValue.Of("C_d", 0.62)],
                [new CalcExpectedValue("Q", 47.1238898038469, "L/min", 1e-9), new CalcExpectedValue("v_ext", 99.99999999999999, "mm/s", 1e-9), new CalcExpectedValue("t_ext", 4.000000000000001, "s", 1e-9), new CalcExpectedValue("v_ret", 145.68764568764567, "mm/s", 1e-9), new CalcExpectedValue("dV", 3.1415926535897936, "L", 1e-9)]),
        ],
        Category: "Hydraulics",
        OutputPorts:
        [
            new CalcOutputPort("Q", "L/min", "Flow into the cylinder circuit (hyd.flow)"),
            new CalcOutputPort("p", "bar", "Supply pressure (hyd.pressure)"),
            new CalcOutputPort("P_pump", "kW", "Pump shaft power (hyd.pump_power)"),
            new CalcOutputPort("u_buck", "", "Rod buckling utilisation, screening (hyd.buckling_util)"),
            new CalcOutputPort("F_ext", "kN", "Cylinder force on the extend stroke (hyd.cylinder_force)"),
            new CalcOutputPort("dV", "L", "Swept volume of the larger stroke (to the accumulator, F2)"),
            new CalcOutputPort("T_shaft", "N*m", "Pump shaft torque (to the motor sizing, E1)"),
            new CalcOutputPort("n_shaft", "rpm", "Pump shaft speed (to the motor sizing, E1)"),
            new CalcOutputPort("Q_prs", "L/min", "Flow in the line to the cylinder cap port, the larger of the two strokes (to the pressure line, B1)"),
            new CalcOutputPort("rho", "kg/m^3", "Fluid density (to the pressure line, B1)"),
            new CalcOutputPort("d_prs", "mm", "Pressure line inside diameter (to the pressure line tube bore, B1)"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Rod material", ["E", "f_y"], "A material from the library: its Young's modulus and yield strength fill these inputs, each citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("cylinder.mounting", "Mounting effective-length factor", ["K_m"], "K for the way the cylinder and rod end are held (pinned both ends 1, fixed both ends 0.5, one end free 2, and so on), from your reference or the cylinder maker. No mounting table is shipped. The sheet's default of 1 is the pinned-pinned case and is not a safe default for a fixed-free mounting (K = 2): enter the real mounting."),
            new CalcRecordKind("cylinder.buckling-safety", "Rod buckling safety factor", ["S_f"], "The safety factor on the critical load, from your design reference or the cylinder maker, with its source and edition. The sheet's default is a placeholder, not a value from a standard."),
            new CalcRecordKind("cylinder.line-limits", "Line velocity limits", ["v_suc_max", "v_prs_max", "v_ret_max"], "The largest velocity you allow in the suction, pressure and return lines, from your design guide, the fluid or the component maker, with its source and edition."),
            new CalcRecordKind("cylinder.hose-sizes", "Hose and pipe size list", ["hoses"], "The inside diameters of the hose or pipe range you use, read from the supplier's catalogue."),
        ]);

    private static CalcInputValue Hoses(params double[] millimetres) =>
        CalcInputValue.Table("hoses", millimetres.Select(v => CalcRow.Of(("ID", new CalcCell(v, "mm")))).ToList());

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "p", "p_back", "rho", "F_push", "F_pull", "D", "d_r", "stroke", "eta_m", "eta_vc", "t_dwell", "K_m", "L_add", "E", "f_y", "S_f", "eta_tp", "d_suc", "d_prs", "d_ret", "v_suc_max", "v_prs_max", "v_ret_max" })
            c.Input(s);

        var regen = c.InputText("mode") == Regenerative;
        var pump = c.Has("V_g") || c.Has("n_p");
        var motor = c.Has("V_gm") || c.Has("dp_m");
        var orifice = c.Has("dp_o") || c.Has("d_o");

        if (!(c.Si("d_r") < c.Si("D")))
        {
            c.Block("rod", $"d_r: the rod diameter ({F(c.Si("d_r") * 1e3)} mm) must be smaller than the bore D ({F(c.Si("D") * 1e3)} mm); enter a smaller rod.");
            return;
        }

        if (pump)
        {
            c.Input("eta_vp");

            if (!c.Has("V_g"))
            {
                c.Block("pump", "V_g: enter the pump displacement as well as its speed, or clear both.");
                return;
            }

            if (!c.Has("n_p"))
            {
                c.Block("pump", "n_p: enter the pump speed as well as its displacement, or clear both.");
                return;
            }

            if (c.Si("eta_tp") > c.Si("eta_vp"))
            {
                c.Block("pump", $"eta_tp: the total efficiency ({F(c.Si("eta_tp"))}) cannot exceed the volumetric efficiency eta_vp ({F(c.Si("eta_vp"))}); enter a total efficiency at or below it.");
                return;
            }

            c.Input("V_g");
            c.Input("n_p");
        }

        if (motor)
        {
            c.Input("eta_hm");

            if (!c.Has("V_gm"))
            {
                c.Block("motor", "V_gm: enter the motor displacement as well as the pressure drop, or clear both.");
                return;
            }

            if (!c.Has("dp_m"))
            {
                c.Block("motor", "dp_m: enter the pressure drop across the motor as well as its displacement, or clear both.");
                return;
            }

            c.Input("V_gm");
            c.Input("dp_m");
        }

        if (orifice)
        {
            c.Input("C_d");

            if (!c.Has("dp_o"))
            {
                c.Block("orifice", "dp_o: enter the pressure drop across the orifice as well as its diameter, or clear both.");
                return;
            }

            c.Input("dp_o");

            if (c.Has("d_o"))
                c.Input("d_o");
        }

        // Areas.
        c.Assign("A_b", "pi * D^2 / 4", "cm^2", "Bore (cap-side) area");
        c.Assign("A_r", "pi * d_r^2 / 4", "cm^2", "Rod area");
        c.Assign("A_a", "A_b - A_r", "cm^2", "Annulus (rod-side) area");
        c.Assign("phi", "A_b / A_a", null, "Area ratio A_b/A_a");

        // Flow into the cylinder: entered, or the flow a required extend speed needs (v·A/η_vc over the area the flow acts on in the extend stroke).
        if (c.Has("v_req"))
        {
            c.Input("v_req");

            // Q is optional but carries a sheet default (DefaultFlowLpm, 36 L/min) so every other use of the sheet is unchanged; c.Has("Q") is therefore true on every new sheet,
            // and a sheet given only v_req would warn about a Q nobody entered. The run cannot tell that default from a typed 36 L/min, so Q at the default value is treated as not entered
            // and any other Q warns. Compare with the shared constant (the same one the Q spec defaults from), never a literal, so the two cannot drift apart.
            if (c.Has("Q") && Math.Abs(c.InputSi("Q") - DefaultFlowLpm / 60000.0) > 1e-12)
                c.Warn("v-req-q", "Q: ignored, the flow comes from the required extend speed; clear Q to remove this notice.");

            c.Assign("Q", regen ? "v_req * A_r / eta_vc" : "v_req * A_b / eta_vc", "L/min", regen ? "Flow the required extend speed needs, regenerative: v·A_r/η_vc" : "Flow the required extend speed needs: v·A_b/η_vc");

            if (!(c.Si("Q") >= 0.1 / 60000 * (1 - 1e-9) && c.Si("Q") <= 2000.0 / 60000 * (1 + 1e-9)))
            {
                c.Block("flow", $"v_req: the required extend speed needs a flow of {F(c.Si("Q") * 60000)} L/min, outside the 0.1 to 2000 L/min this sheet covers; change the speed or the cylinder.");
                return;
            }
        }
        else if (!c.Has("Q"))
        {
            c.Block("flow", "Q: enter the flow into the cylinder, or a required extend speed.");
            return;
        }
        else
        {
            c.Input("Q");
        }

        // Force and speed.
        if (regen)
        {
            c.Assign("F_std", "(p * A_b - p_back * A_a) * eta_m", "kN", "Standard-connection extend force, for comparison");
            c.Assign("v_std", "Q * eta_vc / A_b", "mm/s", "Standard-connection extend speed, for comparison");
            c.Assign("F_ext", "p * A_r * eta_m", "kN", "Extend force, regenerative: pressure on the rod area");
            c.Assign("v_ext", "Q * eta_vc / A_r", "mm/s", "Extend speed, regenerative: flow over the rod area");
            c.Assign("dV_ext", "A_r * stroke", "L", "Liquid taken from the supply for the regenerative extend stroke");
        }
        else
        {
            c.Assign("F_ext", "(p * A_b - p_back * A_a) * eta_m", "kN", "Extend force");
            c.Assign("v_ext", "Q * eta_vc / A_b", "mm/s", "Extend speed");
            c.Assign("dV_ext", "A_b * stroke", "L", "Liquid taken from the supply for the extend stroke");
        }

        c.Assign("F_ret", "(p * A_a - p_back * A_b) * eta_m", "kN", "Retract force");

        if (!(c.Si("F_ext") > 0))
        {
            c.Block("force", $"p_back: the back pressure ({F(c.Si("p_back") / 1e5)} bar) takes all the force on the extend stroke at p = {F(c.Si("p") / 1e5)} bar; lower it or raise p.");
            return;
        }

        if (!(c.Si("F_ret") > 0))
        {
            c.Block("force", $"p_back: the back pressure ({F(c.Si("p_back") / 1e5)} bar) takes all the force on the retract stroke at p = {F(c.Si("p") / 1e5)} bar; lower it or raise p.");
            return;
        }

        c.Assign("v_ret", "Q * eta_vc / A_a", "mm/s", "Retract speed");
        c.Assign("t_ext", "stroke / v_ext", "s", "Extend stroke time");
        c.Assign("t_ret", "stroke / v_ret", "s", "Retract stroke time");
        c.Assign("t_cycle", "t_ext + t_ret + t_dwell", "s", "Cycle time");
        c.Assign("dV_ret", "A_a * stroke", "L", "Liquid taken from the supply for the retract stroke");
        c.Assign("dV", "max(dV_ext, dV_ret)", "L", "Swept volume of the larger stroke");

        // Pump.
        if (pump)
        {
            c.Assign("Q_p", "V_g * n_p * eta_vp", "L/min", "Pump delivery");
            c.Assign("Vg_req", "Q / (n_p * eta_vp)", "cm^3/rev", "Pump displacement the circuit flow needs at this speed");
            c.Assign("u_pump", "Q / Q_p", null, "Circuit flow over the pump delivery");
            c.Assign("Q_pump", "Q_p", "L/min", "Flow the pump delivers at the supply pressure");
        }
        else
        {
            c.Assign("Q_pump", "Q", "L/min", "Flow the pump delivers at the supply pressure (the circuit flow; no pump entered)");
        }

        c.Assign("P_hyd", "p * Q_pump", "kW", "Hydraulic power at the pump outlet");
        c.Assign("P_pump", "P_hyd / eta_tp", "kW", "Pump shaft power");

        if (pump)
        {
            c.Assign("T_shaft", "P_pump / n_p", "N*m", "Pump shaft torque P / ω");
            c.Assign("n_shaft", "n_p", "rpm", "Pump shaft speed");
        }

        // Motor.
        if (motor)
            c.Assign("T_motor", "dp_m * V_gm * eta_hm", "N*m", "Motor torque Δp·V_g/(2π)·η_hm (the displacement per revolution carries the 1/2π)");

        // Lines.
        c.Assign("Q_line", pump ? "max(Q, Q_p)" : "Q", "L/min", pump ? "Flow in the pressure and return lines before the area ratio (the larger of the circuit flow and the pump delivery)" : "Flow in the pressure and return lines before the area ratio (the circuit flow)");
        c.Assign("Q_suc", pump ? "max(Q, V_g * n_p)" : "Q", "L/min", pump ? "Flow in the suction line (the larger of the circuit flow and the geometric pump delivery V_g·n)" : "Flow in the suction line (the circuit flow)");
        c.Assign("Q_prs", regen ? "Q_line * max(A_b / A_a, A_b / A_r)" : "Q_line * A_b / A_a", "L/min", regen ? "Flow in the line to the cap port, worst stroke (regenerative extend Q·A_b/A_r, retract Q·A_b/A_a: the larger)" : "Flow in the line to the cap port, worst stroke (retract: the cap-side discharge Q·A_b/A_a)");
        c.Assign("Q_rtn", "Q_line * A_b / A_a", "L/min", "Flow in the return line on the retract stroke (cap-side discharge)");

        var sf = c.Session.SignificantFigures;
        var sizes = c.Rows("hoses").Select(HoseSi).Where(v => v > 0).Order().ToList();
        var recommended = new List<string>();
        var lines = new[] { ("suc", "suction", "Q_suc", "d_suc", "v_suc_max"), ("prs", "pressure", "Q_prs", "d_prs", "v_prs_max"), ("rtn", "return", "Q_rtn", "d_ret", "v_ret_max") };

        foreach (var (key, name, flow, bore, limit) in lines)
        {
            c.Assign($"v_{key}", $"{flow} / (pi * {bore}^2 / 4)", "m/s", $"Velocity in the {name} line");
            c.Assign($"u_{key}", $"v_{key} / {limit}", null, $"Velocity in the {name} line over its limit");
            c.Assign($"d_min_{key}", $"sqrt(4 * {flow} / (pi * {limit}))", "mm", $"Smallest {name} line bore for the velocity limit");

            if (sizes.Count > 0)
            {
                var needed = c.Si($"d_min_{key}") * (1 - 1e-9);
                var pick = sizes.FirstOrDefault(v => v >= needed);

                if (pick > 0)
                {
                    recommended.Add(key);
                    c.Record($"d_rec_{key}", "smallest listed size not below the minimum bore", $"d_min = {c.Scalar($"d_min_{key}").Format(sf)}, {sizes.Count} sizes listed", ScalarValue.Of(pick * 1e3, "mm"), $"Recommended {name} line bore");
                }
                else
                    c.Warn("size", $"hoses: no listed size is as large as the {F(c.Si($"d_min_{key}") * 1e3)} mm the {name} line needs; the largest listed is {F(sizes[^1] * 1e3)} mm.");
            }
        }

        // Orifice.
        if (orifice)
        {
            if (c.Si("dp_o") > c.Si("p"))
            {
                c.Block("orifice", $"dp_o: the pressure drop across the orifice ({F(c.Si("dp_o") / 1e5)} bar) cannot be larger than the supply pressure p ({F(c.Si("p") / 1e5)} bar).");
                return;
            }

            c.Assign("v_jet", "C_d * sqrt(2 * dp_o / rho)", "m/s", "Effective jet velocity C_d·√(2Δp/ρ)");
            c.Assign("d_o_req", "sqrt(4 * Q / (pi * v_jet))", "mm", "Orifice diameter that passes the circuit flow at this pressure drop");

            if (c.Has("d_o"))
                c.Assign("Q_o", "v_jet * pi * d_o^2 / 4", "L/min", "Flow through the entered orifice");
        }

        // Rod buckling, screening.
        c.Assign("L_b", "stroke + L_add", "mm", "Buckling length: stroke plus the length beyond it");
        c.Assign("L_eff", "K_m * L_b", "mm", "Effective length K·L");
        c.Assign("I_r", "pi * d_r^4 / 64", "mm^4", "Second moment of area of the rod");
        c.Assign("r_g", "d_r / 4", "mm", "Radius of gyration of the solid rod");
        c.Assign("lam", "L_eff / r_g", null, "Slenderness ratio");
        c.Assign("lam_c", "pi * sqrt(2 * E / f_y)", null, "Transition slenderness between Johnson and Euler");

        if (c.Si("lam") > c.Si("lam_c"))
        {
            c.Assign("F_cr", "pi^2 * E * I_r / L_eff^2", "kN", "Critical load, Euler (slender rod)");
        }
        else
        {
            c.Assign("F_cr", "A_r * f_y * (1 - f_y * lam^2 / (4 * pi^2 * E))", "kN", "Critical load, Johnson parabola (stocky rod)");
        }

        c.Assign("F_allow", "F_cr / S_f", "kN", "Allowable push load: critical load over the safety factor");
        c.Assign("F_stall", "p * A_b", "kN", "Stall thrust: the supply pressure on the bore area (no flow, so no back pressure; the friction of the seals does not relieve the rod of a stalled cylinder)");
        c.Assign("F_buck", "max(F_push, F_stall)", "kN", "Rod load for the buckling check: the larger of the push load entered and the stall thrust p·A_b");
        c.Assign("u_buck", "F_buck / F_allow", null, "Buckling utilisation");
        c.Assign("u_push", "F_push / F_ext", null, "Push load over the extend force");
        c.Assign("u_pull", "F_pull / F_ret", null, "Pull load over the retract force");

        var outputs = new List<(string, string)>
        {
            ("A_b", "Bore area"), ("A_r", "Rod area"), ("A_a", "Annulus area"), ("phi", "Area ratio A_b/A_a"),
            ("F_ext", regen ? "Extend force (regenerative)" : "Extend force"), ("F_ret", "Retract force"),
            ("v_ext", regen ? "Extend speed (regenerative)" : "Extend speed"), ("v_ret", "Retract speed"),
            ("t_ext", "Extend time"), ("t_ret", "Retract time"), ("t_cycle", "Cycle time"),
            ("dV_ext", "Extend swept volume"), ("dV_ret", "Retract swept volume"), ("dV", "Swept volume per stroke (the larger)"),
            ("u_push", "Push load over the extend force"), ("u_pull", "Pull load over the retract force"),
            ("Q", "Flow into the cylinder"), ("p", "Supply pressure"), ("rho", "Fluid density"), ("d_prs", "Pressure line inside diameter"),
        };

        if (regen)
            outputs.InsertRange(5, [("F_std", "Extend force, standard connection (comparison)"), ("v_std", "Extend speed, standard connection (comparison)")]);

        if (pump)
            outputs.AddRange([("Q_p", "Pump delivery"), ("Vg_req", "Pump displacement the circuit needs"), ("u_pump", "Circuit flow over the pump delivery")]);

        outputs.Add(("P_pump", "Pump shaft power"));

        if (pump)
            outputs.AddRange([("T_shaft", "Pump shaft torque"), ("n_shaft", "Pump shaft speed")]);

        if (motor)
            outputs.Add(("T_motor", "Hydraulic motor torque"));

        outputs.AddRange(
        [
            ("Q_line", "Flow in the pressure and return lines before the area ratio"), ("Q_suc", "Flow in the suction line"), ("Q_prs", "Flow in the line to the cap port (worst stroke)"), ("Q_rtn", "Flow in the return line"),
            ("v_suc", "Suction line velocity"), ("v_prs", "Pressure line velocity (cap-port line, worst stroke)"), ("v_rtn", "Return line velocity"),
            ("u_suc", "Suction line velocity over its limit"), ("u_prs", "Pressure line velocity over its limit"), ("u_rtn", "Return line velocity over its limit"),
            ("d_min_suc", "Smallest suction line bore"), ("d_min_prs", "Smallest pressure line bore"), ("d_min_rtn", "Smallest return line bore"),
        ]);
        outputs.AddRange(recommended.Select(k => ($"d_rec_{k}", $"Recommended {LineName(k)} line bore")));

        if (orifice)
        {
            outputs.Add(("d_o_req", "Orifice diameter for the circuit flow"));

            if (c.Has("d_o"))
                outputs.Add(("Q_o", "Flow through the entered orifice"));
        }

        outputs.AddRange([("L_eff", "Effective length"), ("lam", "Slenderness"), ("lam_c", "Transition slenderness"), ("F_cr", "Critical load (screening)"), ("F_allow", "Allowable push load (screening)"), ("F_stall", "Stall thrust p·A_b"), ("F_buck", "Rod load judged for buckling: the larger of the push load and the stall thrust"), ("u_buck", "Rod buckling utilisation (screening)")]);

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        c.Output("buckling_note", "Rod buckling check", new TextValue(ScreeningLabel + (c.Si("lam") > c.Si("lam_c") ? "; slender rod, Euler load" : "; stocky rod, Johnson parabola")));

        c.CheckUtilisation("Extend force covers the push load", "u_push");
        c.CheckUtilisation("Retract force covers the pull load", "u_pull");
        c.CheckUtilisation("Rod buckling at the larger of the push load and the stall thrust, " + ScreeningLabel, "u_buck", "Euler / Johnson column relations");
        c.CheckUtilisation("Suction line velocity within its limit", "u_suc", "cylinder.line-limits record");
        c.CheckUtilisation("Pressure line velocity within its limit, cap-port line at the larger stroke flow", "u_prs", "cylinder.line-limits record");
        c.CheckUtilisation("Return line velocity within its limit", "u_rtn", "cylinder.line-limits record");

        if (pump)
            c.CheckUtilisation("Pump delivery covers the circuit flow", "u_pump");

        c.Assume(regen
            ? "Regenerative extend: the rod-side discharge joins the supply to the cap side, so the extend force is p·A_r·η_m and the speed Q·η_vc/A_r; the back pressure does not enter. The cap-side line carries Q·A_b/A_r."
            : "Standard connection: the extend force is the pressure on the bore area less the back pressure on the annulus area; the retract force the pressure on the annulus less the back pressure on the bore.", "Continuity and force balance.");
    }

    private static string LineName(string key) => key switch { "suc" => "suction", "prs" => "pressure", _ => "return" };

    private static double HoseSi(CalcRow row) =>
        row.Get("ID") is { Number: { } v } cell && double.IsFinite(v) ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : UnitCatalog.Parse("mm")).ToSi(v) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
