namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the linked chains every build ships as pack templates. Each carries example step inputs (<see cref="CalcPackStep.Example"/>)
/// so it can be created as it stands, except the motor circuit, which stays refused until its cable tables are supplied (the hydraulic power unit's cable step opens blocked until then). Steps name calculators by template id
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
    /// Hydraulic power unit: cylinder and pump circuit (F1) → accumulator (F2), pressure line (B1), pump motor (E1) → motor supply cable (E4).
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
    /// pump's demand: a larger motor fitted means a new E1 candidate. E4 does not check the protective device against the motor's starting current: the motor-start calculation (E9)
    /// takes that in the full §B7 chain. B1 judges the cap-port line at the retract discharge, where its drop is the back pressure of the retract stroke; neither that drop nor the
    /// supply-side drop is fed back into F1. The steps carry example inputs except E4's two record tables, which no build
    /// ships: the cable step opens blocked (naming them) until the user supplies his own, and the pack cannot be checked or issued while it is. Nothing takes
    /// from E4 yet (the voltage-drop and fault-protection steps follow in a later phase), so the pack is created with E4 blocked, not refused.
    /// </para>
    /// </summary>
    public static CalcPackTemplate HydraulicPowerUnit { get; } = new(
        "pack.hydraulic-power-unit", "Hydraulic power unit",
        "Takes a cylinder circuit and its pump through to the electric supply: the cylinder, pump and line velocities; the accumulator that carries the emergency stroke between the supply pressure and the lowest pressure the stroke can finish at; the pressure drop in the line to the cylinder; the motor that drives the pump continuously; and the cable that supplies the motor. The motor's cable tables are entered by the user: no build ships them. The pressure line takes its flow, oil density and tube bore from the cylinder step, and the cable is sized for the rated power of the candidate motor.",
        [
            new CalcPackStep("f1", "fp.cylinder", "Cylinder and pump circuit", CalcPackExamples.HpuF1),
            new CalcPackStep("f2", "fp.accumulator", "Emergency stroke accumulator", CalcPackExamples.HpuF2),
            new CalcPackStep("b1", "bs.pipe-pump", "Pressure line", CalcPackExamples.HpuB1),
            new CalcPackStep("e1", "elec.motor-sizing", "Pump drive motor", CalcPackExamples.HpuE1),
            new CalcPackStep("e4", "elec.conductor-sizing", "Motor supply cable", CalcPackExamples.HpuE4,
                Hint: "The cable step needs your own cable-capacity (ampacity) and preferred device-rating (ratings) tables: none ship with the program. Enter them from your copy of the wiring regulations and the device manufacturer's data, then create the pack again."),
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
        ]);

    /// <summary>
    /// Envelope moisture: room heat loss (B4) and the room air state (B2) → Glaser condensation (B7).
    /// <para>
    /// B4 → B7: the heat-loss step offers no per-layer output (its layers table is an input, and a link feeds one number or one table cell from a declared output), so
    /// the wall's layers are typed in the Glaser step as well, with the vapour resistance factors it needs, and the one value the two share is linked: B4's U-value of the
    /// first element (<c>U_1</c>, a declared port since B4 1.2.0) is the Glaser step's <c>U_ref</c>. The Glaser step compares it with <c>1/R_T</c> of its own layers and warns when
    /// they differ by more than 2 %, so a revision of the wall in one step makes the other stale and, once updated, shows whether the layers still agree. The Glaser step's vapour control
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
            new CalcPackBinding("b2", "t_1_out", "b7", "theta_int"),
            new CalcPackBinding("b2", "phi_1", "b7", "RH_int"),
        ]);

    /// <summary>
    /// Cold pipe: chilled-water pipe (B1) and the room air state (B2) → insulation thickness (B9).
    /// <para>
    /// B1 → B9: the fluid temperature the pipe step worked at (<c>T_fluid</c>, a declared port since B1 1.4.0) is the process temperature <c>theta_i</c>, and the first segment's outside
    /// diameter (<c>OD_1</c>, from the optional <c>OD</c> column added to the segments table in B1 1.4.0) is the insulation's inside diameter <c>D_i</c>: the inner diameter B1 judges the flow in is not the
    /// diameter the insulation covers. B2 → B9: the room air's dry-bulb (<c>t_1_out</c>) is the ambient temperature <c>theta_a</c> and its dew point (<c>t_dp_1</c>) the dew point <c>theta_dew</c> the surface must
    /// stay above by the margin; the relative humidity <c>RH_a</c> is then not used (an entered dew point wins), so it is not linked. The heat-gain limit <c>q_max_m</c> is omitted from the example so that the
    /// dew-point margin is the one criterion; the conductivity, the surface coefficient, the margin and the chosen thickness are the user's. Revising the pipe's temperature or outside diameter, or the room air,
    /// makes the insulation stale. The pipe step's own pump and pressure checks are unaffected by the insulation: nothing is fed back.
    /// </para>
    /// </summary>
    public static CalcPackTemplate ColdPipe { get; } = new(
        "pack.cold-pipe", "Cold pipe",
        "Takes the temperature and outside diameter of a chilled-water pipe from the pipe step, and the ambient temperature and dew point of the surrounding air from the psychrometrics step, to the insulation thickness that keeps the pipe's outer surface above the dew point by the chosen margin, and the heat gain and surface temperature at the chosen thickness. The pipe step also checks the flow velocity in the pipe's inner diameter; the insulation's conductivity and surface coefficient are the user's.",
        [
            new CalcPackStep("b1", "bs.pipe-pump", "Chilled water pipe", CalcPackExamples.ColdB1),
            new CalcPackStep("b2", "bs.psychrometrics", "Plant room air", CalcPackExamples.ColdB2),
            new CalcPackStep("b9", "bs.insulation", "Pipe insulation", CalcPackExamples.ColdB9, Omit: ["q_max_m"]),
        ],
        [
            new CalcPackBinding("b1", "OD_1", "b9", "D_i"),
            new CalcPackBinding("b1", "T_fluid", "b9", "theta_i"),
            new CalcPackBinding("b2", "t_1_out", "b9", "theta_a"),
            new CalcPackBinding("b2", "t_dp_1", "b9", "theta_dew"),
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
        "Takes the force a heated pipe puts on its anchor from the pipe-expansion step to the steel post that holds the anchor and the post's base plate, and files the anchorage design done elsewhere as Evidence. The links assume the post frame's numbering: node 1 the column base (fixed), node 2 the anchor, member 1 the post, the first load row the thermal force along the pipe; the weight in the second row and the load factors are the user's. The pipe step is a screening: a line that fails it needs a formal stress analysis, and its moment is not carried to the post. The holding-down bolt data are typed (no bolt-check step).",
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

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit, WetHeating, Cooling, SteelSupport, HydraulicPowerUnit, EnvelopeMoisture, ColdPipe, PipeworkSupports];
}
