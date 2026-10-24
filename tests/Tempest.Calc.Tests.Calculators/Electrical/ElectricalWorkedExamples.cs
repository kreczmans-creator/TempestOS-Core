using Tempest.Calc.Electrical;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Tests.Calculators.Electrical;

public static class ElectricalWorkedExamples
{
    private static CalcProvenance Spec(string s) => CalcProvenance.Specification(s);

    private static CalcInputSet FromCase(ICalcCalculator calc, int index, Func<string, CalcProvenance> provenance, params CalcInputValue[] overrides)
    {
        var d = calc.Template.TestCases[index].Inputs.Select(v => v with { Provenance = provenance(v.Symbol) }).ToDictionary(v => v.Symbol);
        foreach (var o in overrides) d[o.Symbol] = o;
        return new CalcInputSet([.. d.Values]);
    }

    public static IReadOnlyList<WorkedExample> All { get; } =
    [
        new(MotorSizingCalculator.Id, "ball-screw-pick-and-place", "Ball-screw axis of a pick-and-place machine",
            new CalcReferenceSource("Analytic servo-sizing relations (Hughes & Drury; manufacturer sizing guides)", null, "Reflected inertia, trapezoidal 1/3 profile, RMS torque with dwell"),
            FromCase(new MotorSizingCalculator(), 0, s => s switch
            {
                "m" => Spec("Mechanical layout ML-12: carriage + gripper 50 kg"),
                "p_screw" => Spec("Screw datasheet BS-1610: 10 mm lead"),
                "J_mech" => Spec("Screw datasheet BS-1610 + coupling: 1.0e-4 kg·m²"),
                "eta" => Spec("Screw datasheet BS-1610: 90 %"),
                "mu" => CalcProvenance.Assumed("Linear guides with seals: μ = 0.1."),
                "J_motor" or "T_rated" or "T_peak" or "n_max" => Spec("Motor datasheet SM-040: 2.0e-4 kg·m², 2.0 / 6.0 N·m, 10 000 rpm"),
                "s" or "t_move" or "t_dwell" or "f_acc" => Spec("Cycle specification CS-3: 0.5 m in 0.5 s, 0.5 s dwell, 1/3 ramps"),
                _ => CalcProvenance.Assumed("Sizing convention."),
            }),
            [new CalcExpectedValue("T_pk", 2.579, "N*m", 1e-3), new CalcExpectedValue("T_rms", 1.4402, "N*m", 1e-3), new CalcExpectedValue("n_pk", 9000, "rpm", 1e-6)],
            "A 50 kg carriage on a 10 mm-lead ball screw moving 0.5 m in 0.5 s. The candidate motor's peak (6 N·m) and continuous (2 N·m) torques are checked with a 1.25 margin; the 9000 rpm peak speed is the sizing driver, suggesting a coarser lead or a gearbox."),

        new(MotorSizingCalculator.Id, "geared-index-table", "Rotary index table through a 10:1 gearbox",
            new CalcReferenceSource("Analytic servo-sizing relations", null, "Reflected inertia J_L/(i² η); inertia ratio above 10 warns"),
            FromCase(new MotorSizingCalculator(), 1, s => s switch
            {
                "J_L" => Spec("S1-style inertia calc of the 600 mm table with fixtures: 0.5 kg·m²"),
                "i" or "eta" => Spec("Gearbox datasheet PG-10: 10:1, 85 %"),
                "J_motor" or "T_rated" or "T_peak" or "n_max" => Spec("Motor datasheet SM-060: 5.0e-4 kg·m², 3.0 / 9.0 N·m, 3000 rpm"),
                "s" or "t_move" or "t_dwell" or "f_acc" => Spec("Cycle specification CS-7: 90° index in 0.3 s, 0.7 s dwell"),
                _ => CalcProvenance.Assumed("Sizing convention."),
            }),
            [new CalcExpectedValue("T_pk", 5.0126, "N*m", 1e-3), new CalcExpectedValue("J_ratio", 11.76, "", 2e-3)],
            "A 90° index in 0.3 s through a 10:1 gearbox: the motor sees 5.9e-3 kg·m² of reflected inertia, nearly twelve times its own rotor inertia, so the sheet warns on the inertia ratio even though the torque and speed checks pass."),

        new(EnclosureHeatCalculator.Id, "wall-mounted-plc-panel", "Wall-mounted PLC panel, natural dissipation and fan",
            new CalcReferenceSource("Effective-surface method (IEC TR 60890 approach; manufacturer guides)", null, "A_eff from face exposure; ΔT = P/(h A_eff); V = P/(ρ c_p ΔT)"),
            FromCase(new EnclosureHeatCalculator(), 1, s => s switch
            {
                "loads" => Spec("Panel schedule PNL-04: PLC 80 W, 4 drives at 55 W"),
                "W" or "H" or "D" => Spec("Enclosure datasheet AE-1380: 600 × 800 × 300"),
                "h" => CalcProvenance.FromReference("Reference record: painted steel enclosure, still air, 5.5 W/m²K (manufacturer guide)"),
                "T_amb" or "T_max" => Spec("Site conditions SC-2: 35 °C plant room; 50 °C inside limit for the drives"),
                "V_pts" or "p_pts" => Spec("Fan datasheet FF-120: curve points"),
                "p_sys" or "V_ref" => Spec("Filter datasheet: 20 Pa at 100 m³/h"),
                _ => CalcProvenance.Assumed("Wall mounted on a floor plinth: rear and bottom covered; indoors."),
            }, EnclosureHeatCalculator.Loads(("PLC and I/O", 80, 1, 1), ("drives", 55, 4, 1)) with { Provenance = Spec("Panel schedule PNL-04") }),
            [new CalcExpectedValue("dT_nat", 47.847, "K", 1e-4), new CalcExpectedValue("V_duty", 113.75, "m^3/h", 1e-3)],
            "300 W in a wall-mounted 600 × 800 × 300 enclosure gives a 48 K natural rise against a 15 K allowance, so natural dissipation fails; a filter fan delivering 114 m³/h at its duty point covers the 62 m³/h needed with margin."),

        new(EnclosureHeatCalculator.Id, "outdoor-kiosk-cooling-unit", "Outdoor kiosk in the sun: cooling-unit capacity",
            new CalcReferenceSource("Effective-surface method with solar gain", null, "P_cool = P_total − h A_eff (T_max − T_amb)"),
            FromCase(new EnclosureHeatCalculator(), 0, s => s switch
            {
                "loads" => Spec("Panel schedule PNL-09"),
                "W" or "H" or "D" => Spec("Enclosure datasheet AE-1380"),
                "h" => CalcProvenance.FromReference("Reference record: painted steel enclosure, still air, 5.5 W/m²K"),
                "T_amb" or "T_max" => Spec("Site conditions SC-5: 40 °C design ambient; 45 °C inside limit"),
                _ => CalcProvenance.Assumed("Free-standing kiosk, bottom on a plinth."),
            }, CalcInputValue.Quantity("T_amb", 40, "degC", Spec("Site conditions SC-5")), CalcInputValue.Quantity("T_max", 45, "degC", Spec("Site conditions SC-5")), CalcInputValue.Quantity("I_sun", 800, "W/m^2", Spec("Site conditions SC-5: 800 W/m² on the sunlit side")), CalcInputValue.Quantity("A_sun", 0.48, "m^2", CalcProvenance.Assumed("One 600 × 800 face in the sun.")), CalcInputValue.Of("alpha_sun", 0.5, CalcProvenance.Assumed("Light-grey paint.")), CalcInputValue.Of("f_rear", 1, CalcProvenance.Assumed("Free-standing."))),
            [new CalcExpectedValue("P_total", 492, "W", 1e-9)],
            "With 192 W of solar gain on top of 300 W internal load and only 5 K allowed above a 40 °C ambient, natural dissipation holds a fraction of the load; ventilation at 5 K rise would need a large airflow, so the sheet reports the cooling-unit capacity needed."),

        new(VoltageDropCalculator.Id, "three-phase-motor-feeder", "Three-phase motor feeder, 100 A over 80 m",
            new CalcReferenceSource("Analytic (√3 I (R cos φ + X sin φ)); BS 7671 Appendix 4 method for the temperature estimate", null, "R from resistivity at 70 °C; X′ = 0.08 Ω/km"),
            FromCase(new VoltageDropCalculator(), 1, s => s switch
            {
                "I" => Spec("Motor schedule MS-01: 100 A full-load current"),
                "L" => CalcProvenance.Drawing("DRG-E-120", "B"),
                "A_c" => Spec("Cable schedule CS-01: 35 mm² Cu XLPE/SWA"),
                "rho_20" or "alpha" => CalcProvenance.FromReference("Reference record: annealed copper 0.01724 Ω·mm²/m, 0.00393/K (Hughes)"),
                "X_km" => Spec("Cable datasheet: 0.08 Ω/km"),
                "I_z" => Spec("Cable schedule CS-01: I_z = 119 A for the installation method"),
                "dv_limit" => Spec("Project spec ES-3 §4: 5 % (BS 7671 Appendix 4 §6.4 default for other uses)"),
                _ => CalcProvenance.Assumed("Design convention."),
            }),
            [new CalcExpectedValue("dV_pct", 1.8815, "", 1e-3), new CalcExpectedValue("T_c_est", 58.25, "degC", 1e-3)],
            "A 35 mm² copper feeder carrying 100 A over 80 m drops 7.5 V (1.9 %) against the 5 % project limit; the conductor runs at an estimated 58 °C, below the 70 °C used for the resistance, so the drop figure is on the safe side."),

        new(VoltageDropCalculator.Id, "dc-battery-feed", "48 V DC battery feed, 20 A over 30 m",
            new CalcReferenceSource("Analytic (2 I R with temperature-corrected resistivity)", null, "6 mm² copper at 70 °C"),
            FromCase(new VoltageDropCalculator(), 0, s => s switch
            {
                "I" => Spec("Load schedule DC-02: 20 A"),
                "L" => CalcProvenance.Drawing("DRG-E-301", "A"),
                "A_c" => Spec("Cable schedule CS-02: 6 mm² Cu"),
                "rho_20" or "alpha" => CalcProvenance.FromReference("Reference record: annealed copper (Hughes)"),
                "dv_limit" => Spec("Battery system spec BS-1: 5 % at the load"),
                _ => CalcProvenance.Assumed("Design convention."),
            }),
            [new CalcExpectedValue("dV", 4.1256, "V", 1e-4), new CalcExpectedValue("dV_pct", 8.595, "", 1e-3)],
            "A 48 V DC feed on 6 mm² copper drops 4.1 V (8.6 %), failing the 5 % limit: the next size up (10 mm²) or a shorter route is needed. The I²R loss of 83 W is reported for the battery budget."),
        new(LoadBudgetCalculator.Id, "pump-skid-panel", "Pump skid control panel: supply, PSU, transformer and UPS battery",
            new CalcReferenceSource("Analytic (load budget arithmetic)", null, "Two pumps, a fan, panel lighting, PLC and comms; 2 h autonomy on the 24 V controls"),
            new CalcInputSet(
            [
                LoadBudgetCalculator.Loads(
                    ("Duty pump", LoadBudgetCalculator.Ac3, 11, 0.86, 0.91, 1, 1, 6.5, false),
                    ("Standby pump", LoadBudgetCalculator.Ac3, 11, 0.86, 0.91, 1, 0, 6.5, false),
                    ("Panel fan", LoadBudgetCalculator.Ac1, 0.06, 0.9, 0.7, 1, 1, 1, false),
                    ("Panel light", LoadBudgetCalculator.Ac1, 0.02, 0.95, 1, 0.1, 1, 1, false),
                    ("PLC and I/O", LoadBudgetCalculator.Dc, 0.096, 1, 0.9, 1, 1, 1, true),
                    ("Router", LoadBudgetCalculator.Dc, 0.024, 1, 0.9, 1, 1, 1, true)) with { Provenance = CalcProvenance.Drawing("E-SCH-010", "A") },
                CalcInputValue.Quantity("U_3ph", 400, "V", CalcProvenance.Specification("Site supply")),
                CalcInputValue.Quantity("U_1ph", 230, "V", CalcProvenance.Specification("Site supply")),
                CalcInputValue.Quantity("U_DC", 24, "V", CalcProvenance.Drawing("E-SCH-010", "A")),
                CalcInputValue.Of("margin", 1.25, CalcProvenance.Assumed("25 % growth margin.")),
                CalcInputValue.Quantity("theta", 45, "degC", CalcProvenance.Specification("Plant room design maximum")),
                CalcInputValue.Of("f_derate", 1, CalcProvenance.Assumed("PSU rated to 60 °C without derating (datasheet).")),
                CalcInputValue.Quantity("P_PSU", 240, "W", CalcProvenance.Drawing("E-SCH-010", "A")),
                CalcInputValue.Quantity("S_T", 25, "kVA", CalcProvenance.Specification("Existing site transformer allocation")),
                CalcInputValue.Of("k_T", 3, CalcProvenance.Assumed("Short-time overload allowed by the supply authority.")),
                CalcInputValue.Quantity("t_auto", 2, "h", CalcProvenance.Specification("Telemetry to stay up for 2 h on mains loss")),
                CalcInputValue.Quantity("U_b", 24, "V", CalcProvenance.Drawing("E-SCH-010", "A")),
                CalcInputValue.Of("eta_conv", 1, CalcProvenance.Assumed("DC UPS module, loads on the battery bus.")),
                CalcInputValue.Of("DoD", 0.8, CalcProvenance.Assumed("VRLA, 80 % depth of discharge.")),
                CalcInputValue.Of("eta_b", 0.95, CalcProvenance.Assumed("Discharge efficiency.")),
                CalcInputValue.Of("k_temp", 1, CalcProvenance.Assumed("Battery kept at 20 °C.")),
                CalcInputValue.Of("k_age", 0.8, CalcProvenance.Assumed("End-of-life capacity 80 %.")),
                CalcInputValue.Quantity("C_batt", 17, "A*h", CalcProvenance.Drawing("E-SCH-010", "A")),
            ]),
            [new CalcExpectedValue("P_dem", 11.182, "kW", 1e-9), new CalcExpectedValue("S_dem", 12.97947, "kVA", 1e-6), new CalcExpectedValue("S_peak", 83.32831, "kVA", 1e-6), new CalcExpectedValue("I_b", 5, "A", 1e-9), new CalcExpectedValue("C_req", 16.44737, "A*h", 1e-6)],
            "Duty and standby pumps (standby at diversity 0), a panel fan and light, and a 24 V PLC with its router on a 2 h battery. Demand is 11.18 kW and 12.98 kVA, well inside the 25 kVA allocation, but the duty pump start lifts the peak to 83.3 kVA, above three times the allocation (75 kVA): the starting check fails and calls for a soft starter or a larger allocation. The 240 W PSU and the 17 A·h battery are checked against the minimums.",
            CalcReferenceStatus.Confirmed),
        new(FaultProtectionCalculator.Id, "workshop-radial-c16", "Workshop radial: 16 A type C MCB, 40 m of 4/2.5 mm² copper",
            new CalcReferenceSource("Analytic (C_min relation; derived k)", null, "TN-S supply with the declared Z_e of 0.8 Ω; let-through I²t from the MCB datasheet"),
            new CalcInputSet(
            [
                CalcInputValue.Quantity("U_0", 230, "V", CalcProvenance.Specification("Supply characteristics")),
                CalcInputValue.Quantity("Z_e", 0.8, "ohm", CalcProvenance.Specification("DNO declared maximum for TN-S")),
                CalcInputValue.Of("C_min", 0.95, CalcProvenance.Assumed("Minimum voltage factor.")),
                CalcInputValue.Of("c_max", 1.1, CalcProvenance.Assumed("Maximum voltage factor.")),
                CalcInputValue.Quantity("L", 40, "m", CalcProvenance.Drawing("E-LAY-002", "B")),
                CalcInputValue.Quantity("S_line", 4, "mm^2", CalcProvenance.Drawing("E-SCH-002", "B")),
                CalcInputValue.Quantity("S_cpc", 2.5, "mm^2", CalcProvenance.Drawing("E-SCH-002", "B")),
                CalcInputValue.Quantity("theta", 70, "degC", CalcProvenance.Assumed("Thermoplastic insulation at full load.")),
                CalcInputValue.Quantity("rho_20", 17.241e-6, "ohm*mm", CalcProvenance.Assumed("Annealed copper resistivity.")),
                CalcInputValue.Quantity("alpha", 0.00393, "1/K", CalcProvenance.Assumed("Copper temperature coefficient.")),
                CalcInputValue.Quantity("Q_c", 3.45e-3, "J/(K*mm^3)", CalcProvenance.Assumed("Copper volumetric heat capacity.")),
                CalcInputValue.Quantity("beta", 234.5, "K", CalcProvenance.Assumed("Copper reciprocal temperature coefficient.")),
                CalcInputValue.Quantity("theta_i", 70, "degC", CalcProvenance.Assumed("Thermoplastic operating temperature.")),
                CalcInputValue.Quantity("theta_f", 160, "degC", CalcProvenance.Assumed("Thermoplastic limiting temperature.")),
                CalcInputValue.Choice("device", FaultProtectionCalculator.McbC, CalcProvenance.Drawing("E-SCH-002", "B")),
                CalcInputValue.Quantity("I_n", 16, "A", CalcProvenance.Drawing("E-SCH-002", "B")),
                CalcInputValue.Quantity("t_req", 0.4, "s", CalcProvenance.Specification("Final circuit up to 32 A, TN system")),
                CalcInputValue.Quantity("I2t_let", 1500, "A^2*s", CalcProvenance.Assumed("Read from the MCB let-through chart at the fault current.")),
            ]),
            [new CalcExpectedValue("Z_s", 1.33635, "ohm", 1e-6), new CalcExpectedValue("Z_s_max", 1.365625, "ohm", 1e-6), new CalcExpectedValue("u_Zs", 0.9785631, "", 1e-6), new CalcExpectedValue("S_min", 0.3372618, "mm^2", 1e-6)],
            "A 40 m radial on a 16 A type C MCB. With Z_e = 0.8 Ω the loop reaches 1.336 Ω against a maximum of 1.366 Ω (utilisation 0.98): it passes, narrowly, and a measured value above the 1.09 Ω field-test figure would need investigating. A type B device would leave a wide margin. The protective conductor easily passes the adiabatic check (0.34 mm² needed).",
            CalcReferenceStatus.Confirmed),
        new(ConductorSizingCalculator.Id, "conveyor-motor-circuit", "4 kW conveyor motor circuit in a machine panel, type C MCB",
            new CalcReferenceSource("Analytic (current, device and conductor selection; synthetic test values invented for this example, not taken from any table)", null, "I_b = 4000/(√3·400·0.82·0.85) = 8.28336 A; I_n = 10 A; I_t,req = 10/(0.91·0.77) = 14.27144 A; 2.5 mm² (synthetic 17.7 A); I_z = 17.7·0.91·0.77 = 12.40239 A"),
            new CalcInputSet(
            [
                CalcInputValue.Choice("context", ConductorSizingCalculator.ContextMachine, CalcProvenance.Specification("Machine design basis MDB-2: BS EN 60204-1 wiring")),
                CalcInputValue.Choice("supply", ConductorSizingCalculator.ThreePhase, CalcProvenance.Drawing("E-SCH-021", "A")),
                CalcInputValue.Quantity("P", 4, "kW", CalcProvenance.Specification("Motor schedule MS-04: conveyor drive 4 kW rated output")),
                CalcInputValue.Quantity("U", 400, "V", CalcProvenance.Specification("Site supply: 400 V three-phase")),
                CalcInputValue.Of("cos_phi", 0.82, CalcProvenance.Specification("Motor datasheet MD-04: 0.82 at full load")),
                CalcInputValue.Of("eta", 0.85, CalcProvenance.Specification("Motor datasheet MD-04: 85 % at full load")),
                CalcInputValue.Quantity("L", 35, "m", CalcProvenance.Drawing("E-LAY-021", "B")),
                CalcInputValue.Quantity("theta_a", 40, "degC", CalcProvenance.Specification("Machine design basis MDB-2: 40 °C inside the panel and trunking")),
                CalcInputValue.Choice("insulation", ConductorSizingCalculator.Pvc, CalcProvenance.Specification("Cable schedule CS-21: PVC 70 °C single cores")),
                new CalcInputValue("method", Text: "B2", Provenance: CalcProvenance.Drawing("E-LAY-021", "B")),
                CalcInputValue.Of("N_g", 3, CalcProvenance.Drawing("E-LAY-021", "B")),
                CalcInputValue.Choice("system", ConductorSizingCalculator.Metric, CalcProvenance.Specification("Cable schedule CS-21: metric cross-sections")),
                CalcInputValue.Choice("device", ConductorSizingCalculator.McbC, CalcProvenance.Drawing("E-SCH-021", "A")),
                CalcInputValue.Of("C_a", 0.91, CalcProvenance.FromReference("record: owner's licensed table (not shipped); 0.91 is a synthetic value invented for this example")),
                CalcInputValue.Of("C_g", 0.77, CalcProvenance.FromReference("record: owner's licensed table (not shipped); 0.77 is a synthetic value invented for this example")),
                CalcInputValue.Of("C_i", 1, CalcProvenance.FromReference("record: owner's licensed table (not shipped); 1 is a neutral value for this example")),
                CalcInputValue.Switch("derive_Ca", false, CalcProvenance.Assumed("The entered C_a is used; the derived approximation is not applied.")),
                CalcInputValue.Quantity("theta_ref", 30, "degC", CalcProvenance.Assumed("Reference ambient of the licensed table; only used when C_a is derived.")),
                ConductorSizingCalculator.Ratings(6, 10, 16, 20, 25, 32, 40, 50, 63) with { Provenance = CalcProvenance.FromReference("record: owner's licensed table (not shipped); ratings are the typical preferred series") },
                ConductorSizingCalculator.SyntheticAmpacity() with { Provenance = CalcProvenance.FromReference("record: owner's licensed table (not shipped); capacities are synthetic values invented for this example") },
            ]),
            [new CalcExpectedValue("I_b", 8.283361107, "A", 1e-6), new CalcExpectedValue("I_n", 10, "A", 1e-9), new CalcExpectedValue("I_t_req", 14.27144284, "A", 1e-6), new CalcExpectedValue("CSA", 2.5, "mm^2", 1e-9), new CalcExpectedValue("I_z", 12.40239, "A", 1e-6), new CalcExpectedValue("u_In", 0.8062962058, "", 1e-6)],
            "A 4 kW (rated output) conveyor motor, design current 8.28 A, behind a 10 A type C MCB, three circuits sharing trunking at 40 °C. With the synthetic factors 0.91 (ambient) and 0.77 (grouping) the required tabulated capacity is 14.3 A, met by 2.5 mm² at a synthetic 17.7 A, leaving I_z = 12.40 A against I_n = 10 A (81 %): all three coordination checks pass. The capacities and factors here are synthetic values invented for this example and entered as if from the owner's licensed table; the ratings are the typical preferred series. Replace them with the owner's own records before relying on the sheet.",
            CalcReferenceStatus.Confirmed),
    ];
}
