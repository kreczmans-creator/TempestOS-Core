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
    /// Drive train: motor (E1) → shaft (M4) → bearing (M5) → bearing seat fit (M2) → coupling flange bolts (M3). E1's peak torque <c>T_pk</c> is the
    /// peak torque the load demands at the motor shaft, run-up included (the motor's own limit <c>T_peak</c> is a check on E1, not a design torque): it is the
    /// torque the flange bolt group (M3b, <c>mech.bolt-group</c>) carries about its centroid (<c>T_z</c>) and the torque the shaft's yield check sees
    /// (<c>T_pk</c>, M4). The shaft's mesh forces are entered (derived for the E1 torque; re-derive them when it changes). M3 must be given its bolt pattern as an input.
    /// </summary>
    public static CalcPackTemplate DriveTrain { get; } = new(
        "pack.drive-train", "Drive train",
        "Motor sizing gives the torque and speed the shaft carries; the shaft gives the bearing load and the seat diameter; the bearing life uses the shaft reaction and the motor speed; the seat fit (a rolling-bearing bore against the shaft seat) uses the shaft diameter; the coupling flange bolts and the shaft's yield check carry the peak torque the load demands at the motor shaft. The gear mesh forces on the shaft are entered for the E1 torque and must be re-derived if it changes.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing", CalcPackExamples.DriveE1),
            new CalcPackStep("m4", "mech.shaft", "Shaft", CalcPackExamples.DriveM4),
            new CalcPackStep("m5", "mech.bearing-life", "Bearing A life", CalcPackExamples.DriveM5),
            new CalcPackStep("m2", "mech.iso286-fit", "Bearing A seat fit", CalcPackExamples.DriveM2),
            new CalcPackStep("m3", "mech.bolt-group", "Coupling flange bolts", CalcPackExamples.DriveM3),
        ],
        [
            new CalcPackBinding("e1", "T_rms", "m4", "T"),
            new CalcPackBinding("e1", "n_pk", "m4", "n"),
            new CalcPackBinding("m4", "R_A", "m5", "F_r"),
            new CalcPackBinding("e1", "n_pk", "m5", "n"),
            new CalcPackBinding("m4", "d_A", "m2", "D"),
            new CalcPackBinding("e1", "T_pk", "m4", "T_pk"),
            new CalcPackBinding("e1", "T_pk", "m3", "T_z"),
        ]);

    /// <summary>
    /// Motor circuit: motor (E1) → load budget (E6) → cable (E4) → voltage drop (E3) and fault protection (E5) →
    /// enclosure heat (E2). E1's RMS power (the thermal-equivalent continuous power, not the instantaneous peak) enters E6 as a linked
    /// load; E6's bus current sizes the cable; the cable's size, length, rated current and device type code feed E3 and E5 (E5 is blocked
    /// if the device chosen on it is not the one the cable step sized); E6's heat released inside the enclosure enters E2 (the linked
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
            new CalcPackBinding("e6", "I_3ph", "e4", "I_b_in"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
            new CalcPackBinding("e4", "device_code", "e5", "device_code"),
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
        "Sizes a wet heating circuit from the rooms to the circulator: room heat losses, emitters and expansion vessel, the pipework and pump at the flow that heat needs, and the motor that drives the pump. The loop is closed (no static lift) and the example load is small, so the pump duty is a deliberately tiny illustration. The pipework water content in the sealed system is entered by hand from the pipework's V_pipe, because a link back from B1 would be a cycle.",
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
            new CalcPackBinding("b6", "Phi_tot", "b1", "Phi_load"),
            new CalcPackBinding("b6", "dT_fr", "b1", "dT_load"),
            new CalcPackBinding("b6", "t_m", "b1", "T"),
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
        "Takes the heat a control panel dissipates, finds what the enclosure cannot lose naturally, sizes the water-cooled air coil that removes it at the enclosure's temperature limit and the pipework and pump that carry the cooling water.",
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
            new CalcPackBinding("b5", "Q", "b1", "Phi_load"),
            new CalcPackBinding("b5", "dT_c", "b1", "dT_load"),
            new CalcPackBinding("b5", "T_c_m", "b1", "T"),
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
        "Combines the actions on an equipment support, analyses the steel frame, checks the column, designs the bracket's bolted joint and the column base plate, and files the anchorage design done elsewhere as Evidence. The links assume the frame's numbering: nodes 1 (column base, fixed), 2 (column top), 3 (arm tip); member 1 the column, member 2 the arm; the first load row the vertical load at node 3 (filled from the load combinations), the second the horizontal design action at node 2. Signs: the column check takes the axial force tension positive (S2's N_e1), the base plate takes it compression positive (S2's R1_y). One ULS case is analysed (every action unfavourable); the favourable-permanent case is not.",
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
            new CalcPackBinding("m3", "v_max", "s4a", "F_v_Ed"),
            new CalcPackBinding("m3", "t_max", "s4a", "F_t_Ed"),
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
    /// <c>p_1</c> and the pre-charge are the user's. The linked <c>p</c> is a gauge pressure, so the accumulator's <c>p_basis</c> must stay Gauge (its default): in Absolute mode the
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
    /// three-phase fault current from E5 (<c>I_k3</c> into <c>I_k</c>; E5 computes it with c = 1.1, the factor E9's fault-current basis defaults to), so the dip is judged on the cable the pack sized and the supply the pack
    /// protects. E9's rated current is therefore NOT fed back to E4 (<c>I_b_in</c>): E4 → E5 → E9 → E4 would be a cycle, and E4 sizes on E1's rated power already. E9 and E4 each carry the motor's efficiency and power factor (typed equal;
    /// a chain test guards them, and that E9's <c>I_rated</c> equals E4's design current). E9's <c>I_start</c> is for starting-current coordination only (it is not a design current): the pack's star-delta start
    /// draws a third of the direct-on-line current, which stays below the type C device's instantaneous band; a direct-on-line start of the same motor would not (the chain test records it). E9's 400 V is the motor's; E5 computes its
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
            new CalcPackBinding("f1", "p", "f2", "p_2"),
            new CalcPackBinding("f1", "Q_prs", "b1", "Q"),
            new CalcPackBinding("f1", "rho", "b1", "rho_custom"),
            new CalcPackBinding("f1", "d_prs", "b1", "segments", Row: 0, Column: "D"),
            new CalcPackBinding("f1", "T_shaft", "e1", "T_L"),
            new CalcPackBinding("f1", "n_shaft", "e1", "n_L"),
            new CalcPackBinding("e1", "P_rated", "e4", "P"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
            new CalcPackBinding("e4", "device_code", "e5", "device_code"),
            new CalcPackBinding("e1", "P_rated", "e9", "P"),
            new CalcPackBinding("e4", "CSA", "e9", "A_c"),
            new CalcPackBinding("e4", "L", "e9", "L_cab"),
            new CalcPackBinding("e5", "I_k3", "e9", "I_k"),
        ]);

    /// <summary>
    /// Mechanism review: cam (M9) → return spring (M7), cam shaft (M4) → bearing (M5), geared motor (E1) → supply cable (E4). The cam step is the source of everything: its spring force at full lift (<c>F_spring_max</c>, N) is the spring's working force at full
    /// lift; its roller contact force (<c>F_pin_pk</c>, N) is the radial load at the cam on the shaft (a table-cell link into the load row, N to kN), its RMS and peak input torque (<c>T_rms_in</c>, <c>input_torque</c>, N·m to kN·m) the torque and the peak
    /// torque of the shaft, and its speed (<c>n_in</c>) the shaft's, the bearing's and the motor's load speed; the shaft's reaction at bearing A (<c>R_A</c>, kN) is the bearing's radial load; the cam's RMS torque is the motor's continuous load torque; and the
    /// motor's nameplate rated power (<c>P_rated</c>, from <c>n_rated</c>) sizes the supply cable.
    /// <para>
    /// Not linked, deliberately. M7's rate cannot go back to M9 (<c>k_sp</c> and <c>F_pre</c> are typed in M9, M7's own rate would make a cycle); M9 and M7 carry the same 9.874 N/mm and 50 N (chain test guard), and the cam's lift is not a
    /// port into M7 (it has no travel input in the Loads mode): the spring's working travel s₂ − s₁ equals the lift only while the two rates agree (guarded). M7's operating frequency is the cam frequency in Hz, typed (<c>n_in</c> is an angular
    /// speed; guarded). The peak input torque is also checked against the motor's peak torque by a test (E1 is judged on the RMS torque, as the motor's thermal duty). The cable step opens blocked until the user supplies his own cable-capacity and
    /// device-rating tables, which no build ships: the pack cannot be checked or issued until then (nothing takes from the cable step, so the pack is created, not refused).
    /// </para>
    /// </summary>
    public static CalcPackTemplate MechanismReview { get; } = new(
        "pack.mechanism-review", "Mechanism review",
        "Takes a cam mechanism through to its supply: the cam's motion, pressure angle, undercut and follower force; the return spring that holds the follower on the cam; the cam shaft that carries the contact force and the torque; the bearing on that shaft; the geared motor that drives the cam; and the cable that supplies the motor. The cam's spring force, contact force, torques and speed reach each step by link; the cable tables are entered by the user, as no build ships them.",
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
            new CalcPackBinding("m9", "F_pin_pk", "m4", "loads", Row: 0, Column: "F_y"),
            new CalcPackBinding("m9", "T_rms_in", "m4", "T"),
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
    /// (twice the crank radius), typed and guarded, as the linkage has no stroke port. F1 → F2 as in the hydraulic power unit: the swept volume of the larger stroke and the supply pressure (a gauge pressure; the accumulator's basis stays Gauge) size an emergency-stroke accumulator.
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
            new CalcPackBinding("f1", "p", "f2", "p_2"),
        ]);

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit, WetHeating, Cooling, SteelSupport, HydraulicPowerUnit, MechanismReview, ActuatedLinkage];
}
