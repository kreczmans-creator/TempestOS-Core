namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the linked chains every build ships as pack templates. Each carries example step inputs (<see cref="CalcPackStep.Example"/>)
/// so it can be created as it stands, except the motor circuit and the hydraulic power unit, which stay refused at the cable step until its cable tables are supplied. Steps name calculators by template id
/// (the Library does not reference the calculator assemblies); <see cref="CalcInstanceService.ValidatePackTemplate"/>
/// refuses a template whose calculators are not composed in.
/// </summary>
public static class CalcPackTemplates
{
    /// <summary>
    /// Drive train: motor (E1) → shaft (M4) → bearing (M5) → bearing seat fit (M2) → coupling flange bolts (M3). The shaft (M4) is the <b>motor-side</b> shaft, the gearbox input shaft behind the
    /// coupling: it carries the torque at the motor shaft, which E1 reports, so no ratio or efficiency is applied between them (E1 also reports the output-side torques, <c>T_out_m</c>, <c>T_out_a</c>,
    /// <c>T_out_pk</c> and <c>n_out</c>, T·i·η and n/i, for a shaft after the ratio: bind those instead). The torque on the shaft is split as the fatigue check needs it: E1's load torque <c>T_load</c>
    /// is the steady (mean) torque <c>T</c> and E1's <c>T_alt</c>, (T_pk − T_dec)/2 on a point-to-point cycle and 0 in continuous duty, is the alternating amplitude <c>T_a</c>; the RMS torque is a thermal
    /// quantity and is not a shaft torque. E1's peak torque <c>T_pk</c> is the peak torque the load demands at the motor shaft, run-up included (the motor's own limit <c>T_peak</c> is a check on E1); it is the torque the shaft's yield check and key
    /// see (<c>T_pk</c>, M4). The coupling flange bolt group (M3, <c>mech.bolt-group</c>) carries E1's design torque <c>T_design</c> about its centroid (<c>T_z</c>), on the basis the owner chooses in E1's <c>T_basis</c>:
    /// the load peak <c>T_pk</c> (the default, as before), the motor's typed peak torque <c>T_peak</c>, a service factor on the rated torque (<c>SF_c</c>·<c>T_rated</c>) or a protective torque limit (<c>T_lim</c>); the basis is a design decision, not a calculation. E1's peak axial
    /// thrust <c>F_ax_pk</c> (a ball screw's; 0 for a rotary mechanism) is the bearing's axial load <c>F_a</c>, peak rather than the cube-mean equivalent, so conservative for life. The bearing's speed is E1's mean motor speed over the cycle <c>n_mean</c> (the turns per cycle time, dwell included; the
    /// steady speed in continuous duty), the speed its life in hours is counted at: the peak speed <c>n_pk</c> (still the shaft's, for its critical speed) would understate the life of a cycle with a dwell by the ratio of the two.
    /// The shaft's mesh forces are entered (derived for the E1 torque; re-derive them when it changes). M3 must be given its bolt pattern as an input.
    /// </summary>
    public static CalcPackTemplate DriveTrain { get; } = new(
        "pack.drive-train", "Drive train",
        "Motor sizing gives the torque and speed the shaft carries; the shaft gives the bearing load and the seat diameter; the bearing life uses the shaft reaction and the motor speed; the seat fit (a rolling-bearing bore against the shaft seat) uses the shaft diameter; the coupling flange bolts, the shaft's yield check and its key carry the peak torque the load demands at the motor shaft. The shaft is the motor-side (gearbox input) shaft: it carries the motor-shaft torque, split into a steady part and an alternating part for its fatigue check. The gear mesh forces on the shaft are entered for the E1 torque and must be re-derived if it changes.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing", CalcPackExamples.DriveE1),
            new CalcPackStep("m4", "mech.shaft", "Shaft", CalcPackExamples.DriveM4),
            new CalcPackStep("m5", "mech.bearing-life", "Bearing A life", CalcPackExamples.DriveM5),
            new CalcPackStep("m2", "mech.iso286-fit", "Bearing A seat fit", CalcPackExamples.DriveM2),
            new CalcPackStep("m3", "mech.bolt-group", "Coupling flange bolts", CalcPackExamples.DriveM3),
        ],
        [
            new CalcPackBinding("e1", "T_load", "m4", "T"),
            new CalcPackBinding("e1", "T_alt", "m4", "T_a"),
            new CalcPackBinding("e1", "n_pk", "m4", "n"),
            new CalcPackBinding("m4", "R_A", "m5", "F_r"),
            new CalcPackBinding("e1", "n_mean", "m5", "n"),
            new CalcPackBinding("e1", "F_ax_pk", "m5", "F_a"),
            new CalcPackBinding("m4", "d_A", "m2", "D"),
            new CalcPackBinding("e1", "T_pk", "m4", "T_pk"),
            new CalcPackBinding("e1", "T_design", "m3", "M_z"),
        ]);

    /// <summary>
    /// Motor circuit: motor (E1) → load budget (E6) → cable (E4) → voltage drop (E3) and fault protection (E5) →
    /// enclosure heat (E2). E1's RMS power (the thermal-equivalent continuous power, not the instantaneous peak) enters E6 as a linked
    /// load, and E1's rated shaft power (<c>P_rated</c>, from the motor's rated speed) as its rating (<c>P_rated_add</c>): the supply carries the larger of the two, so a bigger motor on the same duty draws a bigger cable and device; E6's bus current sizes the cable; the cable's size, length, rated current and device type code feed E3 and E5 (the protective conductor is taken as the same size as the line conductor, <c>S_cpc</c> = the cable's CSA; above 16 mm² a smaller protective conductor is usual: unlink and enter it) (E5 is blocked
    /// if the device chosen on it is not the one the cable step sized); E6's line voltage and the linked machine's power factor are E3's voltage and power factor, and the cable's conductor operating temperature for its insulation (70 °C PVC, 90 °C XLPE) is E5's loop temperature and E3's conductor temperature. E5's phase-to-earth
    /// voltage <c>U_0</c> is not linked (the nominal 230 V of a 400 V system is not exactly 400/√3): on a supply other than 230/400 V set it with E6's line voltage; E6's heat released inside the enclosure enters E2 (the linked
    /// motor is outside it). The steps carry example inputs except E4's two record tables, which no build ships: the chain is refused at
    /// E4 until the user supplies his own.
    /// </summary>
    public static CalcPackTemplate MotorCircuit { get; } = new(
        "pack.motor-circuit", "Motor circuit",
        "Sizes a motor's supply from the motor duty to the enclosure: demand, cable and device, voltage drop, earth-fault disconnection and the heat the panel must lose.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing", CalcPackExamples.MotorE1),
            new CalcPackStep("e6", "elec.load-budget", "Load budget", CalcPackExamples.MotorE6),
            new CalcPackStep("e4", "elec.conductor-sizing", "Cable and device", CalcPackExamples.MotorE4,
                Hint: "The cable step needs your own cable-capacity (ampacity) and preferred device-rating (ratings) tables: none ship with the program. Enter them from your copy of the wiring regulations and the device manufacturer's data, then create the pack again."),
            new CalcPackStep("e3", "elec.voltage-drop", "Voltage drop", CalcPackExamples.MotorE3),
            new CalcPackStep("e5", "elec.fault-protection", "Fault protection", CalcPackExamples.MotorE5),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure heat", CalcPackExamples.MotorE2),
        ],
        [
            new CalcPackBinding("e1", "P_rms", "e6", "P_add"),
            new CalcPackBinding("e1", "P_rated", "e6", "P_rated_add"),
            new CalcPackBinding("e6", "I_3ph", "e4", "I_b_in"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e6", "U_L", "e3", "V_n"),
            new CalcPackBinding("e6", "pf_link", "e3", "cos_phi"),
            new CalcPackBinding("e4", "T_op", "e3", "T_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "CSA", "e5", "S_cpc"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
            new CalcPackBinding("e4", "device_code", "e5", "device_code"),
            new CalcPackBinding("e4", "T_op", "e5", "T_op"),
            new CalcPackBinding("e6", "Phi", "e2", "P_add"),
        ]);

    /// <summary>
    /// Wet heating: two rooms (B4) → sealed system (B6) → primary pipework and pump (B1) → circulator motor (E1). Each room's
    /// design heat loss enters a row of B6's rooms table (a table-cell link, ADR-0168 amendment 2); B6's total heat,
    /// flow-to-return difference and mean water temperature give B1 its design flow and fluid; B1's pump shaft torque and speed
    /// are E1's load torque and speed (the E1 step is given continuous duty). B6 must be
    /// given its two rooms as inputs: instantiating never creates table rows. B1 carries B6's heat, so B6's pipework water content cannot be linked from B1's
    /// <c>V_pipe</c> (a cycle): it is entered, equal to B1's value.
    /// </summary>
    public static CalcPackTemplate WetHeating { get; } = new(
        "pack.wet-heating", "Wet heating",
        "Sizes a wet heating circuit from the rooms to the circulator: room heat losses, emitters and expansion vessel, the pipework and pump at the flow that heat needs, and the motor that drives the pump. The loop is closed (no static lift) and the example load is small, so the pump duty is a deliberately tiny illustration. The pipework water content in the sealed system is entered by hand from the pipework's V_pipe, because a link back from B1 would be a cycle (the sealed-system step warns while it is left at its 20 L default). Each room's design temperature (B4's θ_int) is the room temperature of that room's emitter. The pump head covers pipe friction only unless the water-side loss of the boiler or heat exchanger, valves and emitters is entered as the outlet head h_out of the pipework step (the step warns while it is 0). The pipework step is on water: a glycol loop must be given the custom fluid with its density and viscosity (the step warns when the specific heat is not water's).",
        [
            new CalcPackStep("b4a", "bs.room-heat-loss", "Room A heat loss", CalcPackExamples.WetRoomA),
            new CalcPackStep("b4b", "bs.room-heat-loss", "Room B heat loss", CalcPackExamples.WetRoomB),
            new CalcPackStep("b6", "bs.sealed-system", "Sealed system", CalcPackExamples.WetB6),
            new CalcPackStep("b1", "bs.pipe-pump", "Primary pipework and pump", CalcPackExamples.WetB1, Omit: ["Q"]),
            new CalcPackStep("e1", "elec.motor-sizing", "Circulator motor", CalcPackExamples.WetE1),
        ],
        [
            new CalcPackBinding("b4a", "Phi_HL", "b6", "rooms", Row: 0, Column: "Phi"),
            new CalcPackBinding("b4b", "Phi_HL", "b6", "rooms", Row: 1, Column: "Phi"),
            new CalcPackBinding("b4a", "T_int", "b6", "rooms", Row: 0, Column: "t_i"),
            new CalcPackBinding("b4b", "T_int", "b6", "rooms", Row: 1, Column: "t_i"),
            new CalcPackBinding("b6", "Phi_tot", "b1", "Phi_load"),
            new CalcPackBinding("b6", "dT_fr", "b1", "dT_load"),
            new CalcPackBinding("b6", "T_m", "b1", "T_fluid"),
            new CalcPackBinding("b1", "T_shaft", "e1", "T_L"),
            new CalcPackBinding("b1", "n_shaft", "e1", "n_L"),
        ]);

    /// <summary>
    /// Cooling: panel loads (E6) → enclosure heat (E2) → air-to-water heat exchanger (B5) → cooling-water pipework and pump (B1). E6's
    /// dissipated heat is a linked load of the enclosure; the heat the enclosure cannot lose naturally (E2's cooling-unit duty) is the
    /// exchanger's required duty (sizing mode). The exchanger's hot side is the enclosure air: it enters the coil at E2's internal
    /// temperature limit (<c>T_max</c>), with c_p 1006 J/(kg·K), the conventional dry-air value E2 itself uses, and the air flow through the
    /// coil is the coil's own datum, so a water loop inside the enclosure is not modelled. The exchanger's duty, cold-side rise, mean
    /// cold-side temperature and specific heat give B1 the cooling water's design flow and fluid (the heat-load form of B1, as in the
    /// wet-heating chain), so a glycol mixture in B5's cold side reaches the pump flow.
    /// </summary>
    public static CalcPackTemplate Cooling { get; } = new(
        "pack.cooling", "Enclosure cooling",
        "Takes the heat a control panel dissipates, finds what the enclosure cannot lose naturally, sizes the water-cooled air coil that removes it at the enclosure's temperature limit and the pipework and pump that carry the cooling water. The coil duty is sensible only: when the water enters below the dew point of the enclosure air the coil condenses water (a latent load and a drain this chain does not size), and the coil step warns when its optional dew point is entered. The pump head covers pipe friction only unless the coil's water-side loss, valves and plant are entered as the outlet head h_out of the pipework step (the step warns while it is 0). The pipework step is on water, so a glycol mixture reaches it as a specific heat and mass flow only: for a glycol cold side give the pipework step the custom fluid with the mixture's density and viscosity (the step warns when the specific heat is not water's). A hot ambient at or above the enclosure's temperature limit does not stop the chain: the enclosure step then gives the duty the unit must remove, the heat inside plus the heat the walls gain from the ambient, P_cool = P + h·A_eff·(T_amb − T_max), and the coil is sized for it (no ventilation airflow or fan duty is given in that case).",
        [
            new CalcPackStep("e6", "elec.load-budget", "Panel load budget", CalcPackExamples.CoolE6),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure heat", CalcPackExamples.CoolE2),
            new CalcPackStep("b5", "bs.heat-exchanger", "Cooling-water air coil", CalcPackExamples.CoolB5, Omit: ["A"]),
            new CalcPackStep("b1", "bs.pipe-pump", "Cooling-water pipework and pump", CalcPackExamples.CoolB1, Omit: ["Q"]),
        ],
        [
            new CalcPackBinding("e6", "Phi", "e2", "P_add"),
            new CalcPackBinding("e2", "P_cool", "b5", "Q_req"),
            new CalcPackBinding("e2", "T_max", "b5", "T_h_in"),
            new CalcPackBinding("b5", "Phi", "b1", "Phi_load"),
            new CalcPackBinding("b5", "dT_c", "b1", "dT_load"),
            new CalcPackBinding("b5", "T_c_m", "b1", "T_fluid"),
            new CalcPackBinding("b5", "cp_c", "b1", "cp_load"),
        ]);

    /// <summary>
    /// Steel support: load combinations (S5) → frame (S2) → column check (S3) and arm joint (M3 bolt group → S4) → base plate (S6), with the
    /// holding-down bolts (a second S4) feeding S6. S5's governing downward ULS effect is a cell of S2's load table (a table-cell link);
    /// S2's column force, moment and shear are S3's design actions; S2's arm-end shear and moment load the arm joint's bolt group, whose
    /// per-bolt shear and tension are the arm connection's design forces (S4, <c>s4a</c>); S2's base reactions are the base plate's actions;
    /// the holding-down bolts' resistances, stress area, bearing value and strengths (S4, <c>s4b</c>, run for the base plate's thickness
    /// and edge distances) are S6's anchor bolt data.
    /// <para>
    /// Sign conventions: S3's <c>N_Ed</c> is tension positive (S2's member force <c>N_e1</c>, here −28.2 kN: compression), while S6's <c>N_Ed</c> is compression
    /// positive (S2's reaction <c>R1_y</c>, an upward reaction, +28.2 kN). The same symbol carries opposite signs, so each link takes the S2 output of the right sign.
    /// Limitation: one ULS scalar (S5's <c>E_d_min</c>, every action unfavourable) goes into S2; the favourable-permanent case (1.0·G with the horizontal action),
    /// which can govern holding-down tension for light columns, is not analysed here (for this support the unfavourable case governs, T_Ed 175.2 kN against 111.8 kN).
    /// </para>
    /// <para>
    /// The links depend on S2's numbering, which the example inputs fix: nodes 1 (column base, fixed), 2 (column top) and 3 (arm tip);
    /// member 1 the column (1 → 2) and member 2 the arm (2 → 3); the first load row (case 1) the vertical equipment load at node 3, which S5
    /// fills, and the second (case 2) the horizontal ULS design action at node 2. Give S2 another numbering and the links to
    /// <c>R1_*</c>, <c>*_e1</c> and <c>*_e2</c> must follow it. S2 must be given its nodes, elements and loads (instantiating never creates
    /// rows); the PROFIS anchorage report is attached to S6 as Evidence.
    /// </para>
    /// </summary>
    public static CalcPackTemplate SteelSupport { get; } = new(
        "pack.steel-support", "Steel support",
        "Combines the actions on an equipment support, analyses the steel frame, checks the column, checks the bolts of the bracket's joint, designs the column base plate and files the anchorage design done elsewhere as Evidence. What it does not do: the arm joint is checked at the bolts only (shear, bearing on the thinner ply, tension, combined and block tearing), not the end plate in bending with prying, the weld, the column flange in bending or the column web; the bolt group must be symmetric with the load concentric, because the per-bolt shear and tension come from the governing bolt; the column is checked in its plane only, with no lateral-torsional buckling check, and the minor-axis moment typed 0; the column step takes the compression flange as restrained (ltb = Restrained), an assumption of this example: the column is a cantilever with a free top, which nothing restrains laterally, so by its own description the flange is not restrained, and the member sheet blocks a member that is not (the example would have no column check); a real design checks the cantilever's lateral-torsional buckling by another method; the bolt group's bolt resistances are typed copies of the arm connection step's (change them together; a test guards them); the anchorage is not assessed unless the report's governing utilisation is entered in the base plate step, and the base plate's utilisation excludes it; and nothing checks the frame's numbering below. The links assume the frame's numbering: nodes 1 (column base, fixed), 2 (column top), 3 (arm tip); member 1 the column, member 2 the arm; the first load row the vertical load at node 3 (filled from the load combinations), the second the horizontal design action at node 2. Signs: the column check takes the axial force tension positive (S2's N_e1), the base plate takes it compression positive (S2's R1_y). One ULS case is analysed (every action unfavourable); the favourable-permanent case is not.",
        [
            new CalcPackStep("s5", "struct.load-combinations", "Load combinations", CalcPackExamples.SteelS5),
            new CalcPackStep("s2", "struct.frame-2d", "Support frame", CalcPackExamples.SteelS2),
            new CalcPackStep("s3", "struct.en1993-member", "Column check", CalcPackExamples.SteelS3),
            new CalcPackStep("m3", "mech.bolt-group", "Arm joint bolt group", CalcPackExamples.SteelM3),
            new CalcPackStep("s4a", "struct.connection-ec3", "Arm connection bolts", CalcPackExamples.SteelS4A),
            new CalcPackStep("s4b", "struct.connection-ec3", "Holding-down bolts", CalcPackExamples.SteelS4B, Omit: ["p_1"]),
            new CalcPackStep("s6", "struct.base-plate", "Column base plate", CalcPackExamples.SteelS6),
        ],
        [
            new CalcPackBinding("s5", "E_d_min", "s2", "loads", Row: 0, Column: "F_y"),
            new CalcPackBinding("s2", "N_e1", "s3", "N_Ed"),
            new CalcPackBinding("s2", "M_abs_e1", "s3", "M_y_Ed"),
            new CalcPackBinding("s2", "V_e1", "s3", "V_Ed"),
            new CalcPackBinding("s2", "V_e2", "m3", "F_y"),
            new CalcPackBinding("s2", "M_abs_e2", "m3", "M_x"),
            new CalcPackBinding("m3", "F_v_Ed_max", "s4a", "F_v_Ed"),
            new CalcPackBinding("m3", "F_t_Ed_max", "s4a", "F_t_Ed"),
            new CalcPackBinding("s2", "V_e2", "s4a", "V_Ed"),
            new CalcPackBinding("s2", "R1_y", "s6", "N_Ed"),
            new CalcPackBinding("s2", "R1_m_abs", "s6", "M_Ed"),
            new CalcPackBinding("s2", "R1_x_abs", "s6", "V_Ed"),
            new CalcPackBinding("s4b", "F_t_Rd", "s6", "F_t_Rd"),
            new CalcPackBinding("s4b", "A_s", "s6", "A_s"),
            new CalcPackBinding("s4b", "F_b_Rd", "s6", "F_1_vb_Rd"),
            new CalcPackBinding("s4b", "f_ub", "s6", "f_ub"),
            new CalcPackBinding("s4b", "f_yb", "s6", "f_yb"),
        ]);

    /// <summary>
    /// Hydraulic power unit: cylinder and pump circuit (F1) → accumulator (F2), pressure line (B1), pump motor (E1) → motor supply cable (E4) → voltage drop (E3) and fault protection (E5) → motor start (E9).
    /// <para>
    /// F1 → F2: the swept volume of the larger stroke (<c>dV</c>, L) is the liquid the accumulator must deliver for an emergency stroke (or store), and the supply
    /// pressure (<c>p</c>, bar gauge) is its highest working pressure <c>p_2</c>: the accumulator charges to the pressure the pump holds. The lowest working pressure
    /// <c>p_1</c> and the pre-charge are the user's; F1's <c>p_push</c> (the pressure the push load needs at the cylinder, gauge) is F2's <c>p_load</c>, so F2 checks that <c>p_1</c> still carries the push load, and the real-gas factor
    /// <c>f_rg</c> (the user's, from a gas-property source; 1.311 here) makes the ideal-gas volume the real nitrogen's. The linked <c>p</c> is a gauge pressure, so the accumulator's <c>p_basis</c> must stay Gauge (its default): in Absolute mode the
    /// value would be read one atmosphere low, and the accumulator sheet warns (<c>link-basis</c>). F1 → B1: B1 judges the line F1 judges, the line to the cylinder cap port, so it takes F1's cap-port line flow (<c>Q_prs</c>,
    /// L/min to m³/h: the larger of the two strokes, the retract discharge Q·A_b/A_a), the oil density (<c>rho</c>, to the custom fluid's <c>rho_custom</c>) and the line bore
    /// (<c>d_prs</c>, to the first segment's <c>D</c> by a table-cell link), so revising the cylinder's line bore makes the pressure line stale. B1 is not given a pump, so its
    /// own shaft torque (a water-pump relation) is not used. F1 → E1: the pump shaft torque (<c>T_shaft</c>, N·m) and speed (<c>n_shaft</c>, rpm) are the continuous load
    /// torque and speed (<c>T_L</c>, <c>n_L</c>) of the motor, which is given continuous duty. E1 → E4: E4 takes a shaft power directly (<c>P</c>, which its <c>η</c>
    /// and cos φ convert to the design current), so E1's nameplate rated shaft power (<c>P_rated</c> = T_rated·ω_rated from the motor's rated speed <c>n_rated</c>, W to kW) is linked to it without a load-budget step (E6); a design
    /// current from another calculation would enter <c>I_b_in</c> instead. The cable is sized for the candidate motor's rated power, not for the
    /// pump's demand: a larger motor fitted means a new E1 candidate.
    /// </para>
    /// <para>
    /// The electrical tail follows the motor circuit: E4's design current, length and conductor area are E3's (voltage drop), E4's conductor area, length, device rating and device type code are E5's (fault protection, with the
    /// supply transformer entered once, there). The motor-start step (E9) is placed last because it takes from both: the cable's length and conductor area from E4, the motor's rated power from E1 and the supply's
    /// three-phase fault current from E5 (<c>I_k3</c> into <c>I_k</c> and the factor it was computed with, <c>c_k3</c>, into <c>c_src</c>, so a change of E5's <c>c_max</c> reaches the source impedance), so the dip is judged on the cable the pack sized and the supply the pack
    /// protects. <b>That supply is the transformer-terminal fault level: the dip is a lower bound.</b> The supply cabling and the upstream network are not in it (E9's <c>S_kQ</c> adds an upstream network; a remote board needs a lower fault level), and E5's
    /// example loop impedance (0.05 Ω) is chosen to be coherent with a close-coupled 400 kVA transformer. E9's rated current is therefore NOT fed back to E4 (<c>I_b_in</c>): E4 → E5 → E9 → E4 would be a cycle, and E4 sizes on E1's rated power already. E9 and E4 each carry the motor's efficiency and power factor (typed equal;
    /// a chain test guards them, and that E9's <c>I_rated</c> equals E4's design current). E9's <c>I_start</c> is for starting-current coordination only (it is not a design current): the pack's star-delta start
    /// draws a third of the direct-on-line current, which stays below the type C device's instantaneous band; a direct-on-line start of the same motor would not (the chain test records it). The pump is started UNLOADED (through a vent or unloading valve
    /// to tank): a pump at relief pressure is at full load, and in star the motor gives about a third of its direct-on-line torque, so E1's run-up check, made at the direct-on-line peak torque, does not cover a loaded star start (a chain test guards that
    /// T_peak/3 covers the safety factor times the run-up and a typed, invented unloaded torque). The open-transition changeover from star to delta is not modelled: it can briefly approach the direct-on-line current and so reach the instantaneous band. E9's 400 V is the motor's; E5 computes its
    /// fault current at √3 × 230 V, 0.4 % lower, so E9's source impedance is 0.4 % high (conservative). B1 judges the cap-port line at the retract discharge, where its drop is the back pressure of the retract stroke; neither that drop nor the
    /// supply-side drop is fed back into F1. The steps carry example inputs except E4's two record tables, which no build ships: as in the motor circuit, the pack is refused at the cable step (naming the tables) until the user supplies his own,
    /// because E3, E5 and E9 take from it.
    /// </para>
    /// </summary>
    public static CalcPackTemplate HydraulicPowerUnit { get; } = new(
        "pack.hydraulic-power-unit", "Hydraulic power unit",
        "Takes a cylinder circuit and its pump through to the electric supply: the cylinder, pump and line velocities; the accumulator that carries the emergency stroke between the supply pressure and the lowest pressure the stroke can finish at; the pressure drop in the line to the cylinder; the motor that drives the pump continuously; the cable that supplies the motor, its voltage drop and its earth-fault disconnection; and the voltage dip when the motor starts on that cable and supply. The motor's cable tables are entered by the user: no build ships them. The pressure line takes its flow, oil density and tube bore from the cylinder step, the cable is sized for the rated power of the candidate motor, and the start is judged on the cable and the supply fault level the pack found.",
        [
            new CalcPackStep("f1", "fp.cylinder", "Cylinder and pump circuit", CalcPackExamples.HpuF1),
            new CalcPackStep("f2", "fp.accumulator", "Emergency stroke accumulator", CalcPackExamples.HpuF2),
            new CalcPackStep("b1", "bs.pipe-pump", "Pressure line", CalcPackExamples.HpuB1),
            new CalcPackStep("e1", "elec.motor-sizing", "Pump drive motor", CalcPackExamples.HpuE1),
            new CalcPackStep("e4", "elec.conductor-sizing", "Motor supply cable", CalcPackExamples.HpuE4,
                Hint: "The cable step needs your own cable-capacity (ampacity) and preferred device-rating (ratings) tables: none ship with the program. Enter them from your copy of the wiring regulations and the device manufacturer's data, then create the pack again."),
            new CalcPackStep("e3", "elec.voltage-drop", "Motor supply voltage drop", CalcPackExamples.HpuE3),
            new CalcPackStep("e5", "elec.fault-protection", "Motor supply fault protection", CalcPackExamples.HpuE5),
            new CalcPackStep("e9", "elec.motor-start", "Pump motor start", CalcPackExamples.HpuE9),
        ],
        [
            new CalcPackBinding("f1", "dV", "f2", "dV"),
            new CalcPackBinding("f1", "p_sys", "f2", "p_2"),
            new CalcPackBinding("f1", "p_push", "f2", "p_load"),
            new CalcPackBinding("f1", "Q_prs", "b1", "Q"),
            new CalcPackBinding("f1", "rho", "b1", "rho_custom"),
            new CalcPackBinding("f1", "d_prs", "b1", "segments", Row: 0, Column: "D"),
            new CalcPackBinding("f1", "T_shaft", "e1", "T_L"),
            new CalcPackBinding("f1", "n_shaft", "e1", "n_L"),
            new CalcPackBinding("e1", "P_rated", "e4", "P"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e4", "T_op", "e3", "T_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "CSA", "e5", "S_cpc"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
            new CalcPackBinding("e4", "device_code", "e5", "device_code"),
            new CalcPackBinding("e4", "T_op", "e5", "T_op"),
            new CalcPackBinding("e1", "P_rated", "e9", "P"),
            new CalcPackBinding("e4", "CSA", "e9", "A_c"),
            new CalcPackBinding("e4", "L", "e9", "L_cab"),
            new CalcPackBinding("e5", "I_k3", "e9", "I_k"),
            new CalcPackBinding("e5", "c_k3", "e9", "c_src"),
        ]);

    /// <summary>
    /// Mechanism review: cam (M9) → return spring (M7), cam shaft (M4) → bearing (M5), geared motor (E1) → supply cable (E4). The cam step is the source of everything: its spring force at full lift (<c>F_spring_max</c>, N) is the spring's working force at full
    /// lift; its roller contact force (<c>F_pin_pk</c>, N) is the radial load at the cam on the shaft (a table-cell link into the load row, N to kN), its peak input torque (<c>input_torque</c>, N·m to kN·m) the alternating torque amplitude and the peak
    /// torque of the shaft, and its speed (<c>n_in</c>) the shaft's, the bearing's and the motor's load speed; the shaft's reaction at bearing A (<c>R_A</c>, kN) is the bearing's radial load; the cam's RMS torque is the motor's continuous load torque (the motor runs at its 1440 rpm nameplate speed through the 9.6:1 gearbox); and the
    /// motor's nameplate rated power (<c>P_rated</c>, from <c>n_rated</c>) sizes the supply cable.
    /// <para>
    /// Run-time links that replace what would otherwise be test guards: the cam's lift (<c>lift</c>, mm) is the spring's expected working travel (<c>s_work</c>), and M7 warns (<c>travel-mismatch</c>) when its own s₂ − s₁ is more than 1 % away;
    /// the cam's frequency in Hz (<c>f_in</c>, since rpm is an angular velocity and <c>n_in</c> cannot be linked to a frequency) is the spring's operating frequency (<c>f_op</c>).
    /// </para>
    /// <para>
    /// Not linked, deliberately. M7's rate cannot go back to M9 (<c>k_sp</c> and <c>F_pre</c> are typed in M9, M7's own rate would make a cycle); M9 and M7 carry the same 9.874 N/mm and 50 N (chain test guard), and a revised cam whose
    /// rate no longer matches the spring's makes M7 warn <c>travel-mismatch</c> instead of silently still checking.
    /// </para>
    /// <para>
    /// The cam's torque (a frictionless spring system) is fully alternating about a mean of zero, ranging −9.61 to +10.11 N·m, so the shaft takes it as an alternating torque: the cam's peak input torque <c>input_torque</c>
    /// (the largest |T_in|, a conservative amplitude) is the shaft's alternating amplitude <c>T_a</c> and its peak torque <c>T_pk</c>, and the shaft's steady torque <c>T</c> is left at 0 (a test guards that the cam's mean torque is small). The RMS torque is a thermal quantity
    /// and goes to the motor only. The peak input torque is checked against the motor's peak torque by a test (E1 is judged on the RMS torque, as the motor's thermal duty). The motor runs at
    /// its 1440 rpm nameplate through a 9.6:1 gearbox. The cable step opens blocked until the user supplies his own cable-capacity and device-rating tables, which no build ships: the pack cannot be checked or issued until then (nothing takes from
    /// the cable step, so the pack is created, not refused).
    /// </para>
    /// </summary>
    public static CalcPackTemplate MechanismReview { get; } = new(
        "pack.mechanism-review", "Mechanism review",
        "Takes a cam mechanism through to its supply: the cam's motion, pressure angle, undercut and follower force; the return spring that holds the follower on the cam; the cam shaft that carries the contact force and the cam's alternating torque; the bearing on that shaft; the geared motor that drives the cam; and the cable that supplies the motor. The cam's spring force, contact force, torques and speed reach each step by link; the cable tables are entered by the user, as no build ships them.",
        [
            new CalcPackStep("m9", "mech.mechanism", "Tamper cam", CalcPackExamples.CamM9),
            new CalcPackStep("m7", "mech.spring", "Follower return spring", CalcPackExamples.CamM7),
            new CalcPackStep("m4", "mech.shaft", "Cam shaft", CalcPackExamples.CamM4),
            new CalcPackStep("m5", "mech.bearing-life", "Cam shaft bearing A", CalcPackExamples.CamM5),
            new CalcPackStep("e1", "elec.motor-sizing", "Cam drive motor", CalcPackExamples.CamE1),
            new CalcPackStep("e4", "elec.conductor-sizing", "Cam drive supply cable", CalcPackExamples.CamE4,
                Hint: "The cable step needs your own cable-capacity (ampacity) and preferred device-rating (ratings) tables: none ship with the program. Enter them from your copy of the wiring regulations and the device manufacturer's data, then create the pack again."),
        ],
        [
            new CalcPackBinding("m9", "F_spring_max", "m7", "F_2"),
            new CalcPackBinding("m9", "lift", "m7", "s_work"),
            new CalcPackBinding("m9", "f_in", "m7", "f_op"),
            new CalcPackBinding("m9", "F_pin_pk", "m4", "loads", Row: 0, Column: "F_y"),
            new CalcPackBinding("m9", "input_torque", "m4", "T_a"),
            new CalcPackBinding("m9", "input_torque", "m4", "T_pk"),
            new CalcPackBinding("m9", "n_in", "m4", "n"),
            new CalcPackBinding("m4", "R_A", "m5", "F_r"),
            new CalcPackBinding("m9", "n_in", "m5", "n"),
            new CalcPackBinding("m9", "T_rms_in", "e1", "T_L"),
            new CalcPackBinding("m9", "n_in", "e1", "n_L"),
            new CalcPackBinding("e1", "P_rated", "e4", "P"),
        ]);

    /// <summary>
    /// Actuated linkage: slider-crank (M9) → hydraulic cylinder (F1) → accumulator (F2). A slider-crank ejector is to be replaced by a cylinder. The linkage's peak slide speed (<c>output_motion</c>, m/s to mm/s) is the extend speed the cylinder must
    /// reach (<c>v_req</c>, which gives the flow and replaces the entered one) and its peak slide-direction force (<c>follower_force</c>, N to kN) is the load to push and, conservatively, to pull. <b>This is a kinematic proxy</b> (DB-04): a cylinder-actuated
    /// linkage runs at about constant cylinder speed, not at constant crank speed, so the crank's peak speed and force are what the cylinder must match, not what it would reproduce through the whole stroke. The cylinder's stroke is the slider's
    /// (twice the crank radius), typed and guarded, as the linkage has no stroke port. F1 → F2 as in the hydraulic power unit: the swept volume of the larger stroke and the supply pressure (a gauge pressure; the accumulator's basis stays Gauge) size an emergency-stroke accumulator, whose lowest working pressure is checked against the pressure the push load needs (F1's <c>p_push</c> to F2's <c>p_load</c>).
    /// </summary>
    public static CalcPackTemplate ActuatedLinkage { get; } = new(
        "pack.actuated-linkage", "Actuated linkage",
        "Replaces a slider-crank drive by a hydraulic cylinder: the linkage's peak slide speed and force are the speed the cylinder must reach and the load it must carry (a kinematic proxy, not the cylinder's own stroke profile), and the cylinder's swept volume and supply pressure size the accumulator for an emergency stroke.",
        [
            new CalcPackStep("m9", "mech.mechanism", "Ejector slider-crank", CalcPackExamples.LinkM9),
            new CalcPackStep("f1", "fp.cylinder", "Replacement cylinder", CalcPackExamples.LinkF1, Omit: ["Q"]),
            new CalcPackStep("f2", "fp.accumulator", "Emergency stroke accumulator", CalcPackExamples.LinkF2),
        ],
        [
            new CalcPackBinding("m9", "output_motion", "f1", "v_req"),
            new CalcPackBinding("m9", "follower_force", "f1", "F_push"),
            new CalcPackBinding("m9", "follower_force", "f1", "F_pull"),
            new CalcPackBinding("f1", "dV", "f2", "dV"),
            new CalcPackBinding("f1", "p_sys", "f2", "p_2"),
            new CalcPackBinding("f1", "p_push", "f2", "p_load"),
        ]);

    /// <summary>
    /// Envelope moisture: room heat loss (B4) and the room air state (B2) → Glaser condensation (B7).
    /// <para>
    /// B4 → B7: the heat-loss step offers no per-layer output (its layers table is an input, and a link feeds one number or one table cell from a declared output), so
    /// the wall's layers are typed in the Glaser step as well, with the vapour resistance factors it needs, and the one value the two share is linked: B4's U-value of the
    /// first element (<c>U_1</c>, a declared port since B4 1.2.0) is the Glaser step's <c>U_ref</c>. The Glaser step compares it with <c>1/R_T</c> of its own layers and warns when
    /// they differ by more than 2 %, so a revision of the wall in the heat-loss step makes the Glaser step stale and, once updated, the warning shows whether the layers still agree (a revision of the Glaser layers makes nothing stale: the warning is the only guard). The comparison is of the thermal resistance only: it cannot see a swapped layer order or a wrong vapour resistance factor in the twice-typed layers. The Glaser step's vapour control
    /// layer is not in the heat-loss step (0.2 mm is below its 1 mm layer minimum and thermally negligible: 0.0006 m²K/W). B2 → B7: the room air's dry-bulb (<c>t_1_out</c>) is the
    /// internal air temperature <c>theta_int</c> and its relative humidity (<c>phi_1</c>, added to B2 in 1.1.0 as a percentage quantity: the plain-number <c>RH_1</c> cannot be linked
    /// to a percentage input) the internal relative humidity <c>RH_int</c>. The heat-loss step's own internal design temperature is typed (21 °C, the same as the air state's;
    /// a test guards the match). The twelve monthly outside conditions, the surface resistances, the critical humidity and the limit on the condensate are the user's records:
    /// the example's are invented and no climate or material data of any standard is shipped.
    /// </para>
    /// </summary>
    public static CalcPackTemplate EnvelopeMoisture { get; } = new(
        "pack.envelope-moisture", "Envelope moisture",
        "Takes the U-value of a wall from the room heat-loss step and the temperature and humidity of the room air from the psychrometrics step to the Glaser condensation check of the wall: the condensate on each interface month by month, the annual balance, the peak against the owner's limit, and the surface temperature factor. The wall's layers are typed in both the heat-loss and the Glaser steps (no per-layer link exists) and the Glaser step warns if its U-value departs from the heat-loss step's by more than 2 %. The monthly climate is the user's own record: the example's is invented.",
        [
            new CalcPackStep("b4", "bs.room-heat-loss", "Room heat loss", CalcPackExamples.EnvB4),
            new CalcPackStep("b2", "bs.psychrometrics", "Room air state", CalcPackExamples.EnvB2),
            new CalcPackStep("b7", "bs.glaser", "Wall condensation", CalcPackExamples.EnvB7),
        ],
        [
            new CalcPackBinding("b4", "U_1", "b7", "U_ref"),
            new CalcPackBinding("b2", "T_1_out", "b7", "T_int"),
            new CalcPackBinding("b2", "phi_1", "b7", "RH_int"),
        ]);

    /// <summary>
    /// Cold pipe: chilled-water pipe (B1) and the room air state (B2) → insulation thickness (B9).
    /// <para>
    /// B1 → B9: the design temperature of the line (<c>T_line</c>, a declared port since B1 1.5.0) is the process temperature <c>theta_i</c>. It is NOT the mean fluid temperature <c>T</c> the pipe step takes its properties at: for a
    /// 6/12 °C chilled line the mean is 9 °C but anti-condensation must be sized on the 6 °C flow, so the pipe step is given the flow temperature as <c>T_line</c> (the port is offered only when <c>T_line</c> or <c>T</c> is entered). The first segment's
    /// outside diameter (<c>OD_1</c>, from the optional <c>OD</c> column added to the segments table in B1 1.4.0) is the insulation's inside diameter <c>D_i</c>: the inner diameter B1 judges the flow in is not the
    /// diameter the insulation covers. B2 → B9: the room air's dry-bulb (<c>t_1_out</c>) is the ambient temperature <c>theta_a</c>, its dew point (<c>t_dp_1</c>) the dew point <c>theta_dew</c> the surface must
    /// stay above by the margin, and its relative humidity (<c>phi_1</c>) <c>RH_a</c>: an entered dew point wins, so <c>RH_a</c> is not used in the result, but it is linked so that the sheet's printed humidity is the air state's, not a default. The heat-gain limit <c>q_max_m</c> is omitted from the example so that the
    /// dew-point margin is the one criterion; the conductivity, the surface coefficient, the margin and the chosen thickness are the user's. Revising the pipe's line temperature or outside diameter, or the room air,
    /// makes the insulation stale. The pipe step's own pump and pressure checks are unaffected by the insulation: nothing is fed back.
    /// </para>
    /// </summary>
    public static CalcPackTemplate ColdPipe { get; } = new(
        "pack.cold-pipe", "Cold pipe",
        "Takes the design temperature (the coldest fluid temperature in the line, not its mean) and the outside diameter of a chilled-water pipe from the pipe step, and the ambient temperature, dew point and humidity of the surrounding air from the psychrometrics step, to the insulation thickness that keeps the pipe's outer surface above the dew point by the chosen margin, and the heat gain and surface temperature at the chosen thickness. The pipe step also checks the flow velocity in the pipe's inner diameter; the insulation's conductivity and surface coefficient are the user's.",
        [
            new CalcPackStep("b1", "bs.pipe-pump", "Chilled water pipe", CalcPackExamples.ColdB1),
            new CalcPackStep("b2", "bs.psychrometrics", "Plant room air", CalcPackExamples.ColdB2),
            new CalcPackStep("b9", "bs.insulation", "Pipe insulation", CalcPackExamples.ColdB9, Omit: ["q_max_m"]),
        ],
        [
            new CalcPackBinding("b1", "OD_1", "b9", "D_i"),
            new CalcPackBinding("b1", "T_line", "b9", "T_proc"),
            new CalcPackBinding("b2", "T_1_out", "b9", "T_amb"),
            new CalcPackBinding("b2", "T_dp_1", "b9", "T_dew"),
            new CalcPackBinding("b2", "phi_1", "b9", "RH_a"),
        ]);

    /// <summary>
    /// Pipework supports: pipe expansion and anchor force (B8) → anchor post frame (S2) → post base plate (S6), with the anchorage design done elsewhere filed as Evidence.
    /// <para>
    /// B8 → S2: the anchor force (<c>anchor_force</c>, N) is the first load row's <c>F_x</c> (kN) of the post frame, a table-cell link: the horizontal force the pipe exerts on its anchor, along the pipe. The row's other
    /// load, the weight, is typed; the ULS combination (factors 1.5 on the thermal force and 1.35 on the weight) is the user's. B8's <c>anchor_moment</c> is NOT linked: it is the largest root moment of the guided-cantilever legs, not a
    /// moment applied to the anchor by a frame, and the sheet says a formal analysis is needed before an anchor is sized for moment. S2 → S6 as in the steel-support pack: the base reactions are the plate's actions (<c>R1_y</c> compression
    /// positive → <c>N_Ed</c>, <c>R1_m_abs</c> → <c>M_Ed</c>, <c>R1_x_abs</c> → <c>V_Ed</c>). There is no bolt-check step in this chain, so the holding-down bolt data of S6 (tension resistance, stress area, bearing, strengths) are typed for M20 8.8 in a 25 mm
    /// plate and guarded by a test against the connection step. The links depend on S2's numbering, which the example fixes: node 1 the column base (fixed), node 2 the anchor; member 1 the post; the first load row the thermal force at node 2.
    /// S2 must be given its nodes, elements and loads (instantiating never creates rows); the PROFIS anchorage report is attached to S6 as Evidence, as in the steel-support pack. The example's pipe-expansion limits (allowable range, screening limit) are invented.
    /// </para>
    /// </summary>
    public static CalcPackTemplate PipeworkSupports { get; } = new(
        "pack.pipework-supports", "Pipework supports",
        "Takes the force a heated pipe puts on its anchor from the pipe-expansion step to the steel post that holds the anchor and the post's base plate, and files the anchorage design done elsewhere as Evidence. The anchor force holds no support friction unless the pipe-expansion step is given the friction coefficient and the weight per metre (it warns when they are not); the force on the post is then the leg's alone. The links assume the post frame's numbering: node 1 the column base (fixed), node 2 the anchor, member 1 the post, the first load row the thermal force along the pipe; the weight in the second row and the load factors are the user's. The pipe step is a screening: a line that fails it needs a formal stress analysis, and its moment is not carried to the post. The holding-down bolt data are typed (no bolt-check step).",
        [
            new CalcPackStep("b8", "bs.pipe-expansion", "Pipe expansion and anchor force", CalcPackExamples.PipeB8),
            new CalcPackStep("s2", "struct.frame-2d", "Anchor post frame", CalcPackExamples.PipeS2),
            new CalcPackStep("s6", "struct.base-plate", "Anchor post base plate", CalcPackExamples.PipeS6),
        ],
        [
            new CalcPackBinding("b8", "anchor_force", "s2", "loads", Row: 0, Column: "F_x"),
            new CalcPackBinding("s2", "R1_y", "s6", "N_Ed"),
            new CalcPackBinding("s2", "R1_m_abs", "s6", "M_Ed"),
            new CalcPackBinding("s2", "R1_x_abs", "s6", "V_Ed"),
        ]);

    /// <summary>
    /// Electronics enclosure: PCB power trace (E7) → load budget (E6) → enclosure heat (E2) → cover latches (M8) and cover plate (S9).
    /// <para>
    /// E7 → E6: the trace's I²R loss (<c>trace_Ploss</c>, W) is the input power of a row of the board's load table (<c>loads</c> row 0, column <c>P</c>, kW), a table-cell link; all of it is released as heat inside the enclosure
    /// (the row's heat share is 1). E6 → E2: the heat the loads release inside (<c>Phi</c>, kW) is the enclosure's additional load (<c>P_add</c>, W), as in the cooling chain. The supply's own loss is E6's own: with the supply's bus and efficiency entered it adds the DC loads' draw × (1/η − 1)
    /// to the heat, so it follows the loads. E2 → M8: the enclosure's internal temperature (<c>T_int</c>) is the latches' temperature of use (<c>T_use</c>). The enclosure is sealed (<c>T_seal</c>) with a
    /// pressure-compensation breather (<c>dp_relief</c>), so the air warming from the sealing temperature to <c>T_int</c> at constant volume would put p0·(T_int/T_seal − 1) across the walls (an upper bound) and the breather
    /// limits it: <c>dp_seal</c> = min of the two. E2 → S9: <c>dp_seal</c> is the uniform pressure <c>q</c> on the cover plate. E2 → M8: the force <c>F_seal</c> = <c>dp_seal</c>·<c>A_cover</c> on the cover is the force <c>F_req</c> the
    /// <c>n_latch</c> latches (typed: six) must hold, each by its separation force W′; M8 checks <c>F_req</c> ≤ n·W′. The cover area is typed in E2 and in the plate's dimensions (a test guards the match).
    /// </para>
    /// <para>
    /// With the breather governing, the linked pressure and force do not change when the trace is revised, so the cover plate and the force on the latches stay current; they go stale when E2 itself, or the breather rating, is revised.
    /// The snap-fit records the temperature but its secant modulus and permissible strain, and the cover's modulus and limit stress, are TYPED for 60 °C, the enclosure's limit <c>T_max</c>: they do not follow the links, and are valid only while the enclosure stays
    /// at or below that limit, which the enclosure step's own check (natural dissipation holds <c>T_max</c>) guards at run time; if the limit or the temperature moves they must be re-entered from the material's data. The trace step's board ambient is typed (60 °C, also <c>T_max</c>):
    /// the real ambient is the enclosure's internal temperature, downstream of the trace, so linking it back would be a cycle; the same check guards it. The plate step has no temperature input.
    /// </para>
    /// </summary>
    public static CalcPackTemplate ElectronicsEnclosure { get; } = new(
        "pack.electronics-enclosure", "Electronics enclosure",
        "Follows the loss of a board's power trace into the heat of its load budget, the temperature a sealed enclosure with a pressure-compensation breather settles at, the pressure difference that temperature puts across it (limited by the breather) and the force of that pressure on the front cover, the cover latches that must hold it, and the cover plate under that pressure. The trace loss is a row of the load table (a table cell); the enclosure's internal temperature is the latches' temperature of use; the pressure difference is the cover's pressure and, times the cover area, the force on the latches. The latches' and cover's material values are typed for 60 °C, the enclosure's limit, and do not follow the links; the trace's board ambient, the supply's loss row and the latch count are typed.",
        [
            new CalcPackStep("e7", "elec.pcb-trace", "Power trace", CalcPackExamples.ElxE7),
            new CalcPackStep("e6", "elec.load-budget", "Board load budget", CalcPackExamples.ElxE6),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure temperature", CalcPackExamples.ElxE2),
            new CalcPackStep("m8", "mech.snap-fit", "Cover latches", CalcPackExamples.ElxM8),
            new CalcPackStep("s9", "struct.plate-bending", "Cover plate", CalcPackExamples.ElxS9),
        ],
        [
            new CalcPackBinding("e7", "trace_Ploss", "e6", "loads", Row: 0, Column: "P"),
            new CalcPackBinding("e6", "Phi", "e2", "P_add"),
            new CalcPackBinding("e2", "T_int", "m8", "T_use"),
            new CalcPackBinding("e2", "F_seal", "m8", "F_req"),
            new CalcPackBinding("e2", "dp_seal", "s9", "q"),
        ]);

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit, WetHeating, Cooling, SteelSupport, HydraulicPowerUnit, MechanismReview, ActuatedLinkage, EnvelopeMoisture, ColdPipe, PipeworkSupports, ElectronicsEnclosure];
}
