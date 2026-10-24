namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the linked chains every build ships as pack templates. Each carries example step inputs (<see cref="CalcPackStep.Example"/>)
/// so it can be created as it stands, except the motor circuit, which stays refused until its cable tables are supplied. Steps name calculators by template id
/// (the Library does not reference the calculator assemblies); <see cref="CalcInstanceService.ValidatePackTemplate"/>
/// refuses a template whose calculators are not composed in.
/// </summary>
public static class CalcPackTemplates
{
    /// <summary>
    /// Drive train: motor (E1) → shaft (M4) → bearing (M5) → bearing seat fit (M2) → coupling flange bolts (M3). E1's peak torque is
    /// the torque the flange bolt group (M3b, <c>mech.bolt-group</c>) carries about its centroid (<c>T_z</c>), so the coupling joining
    /// motor and shaft is checked for the worst torque the motor produces. M3 must be given its bolt pattern as an input.
    /// </summary>
    public static CalcPackTemplate DriveTrain { get; } = new(
        "pack.drive-train", "Drive train",
        "Motor sizing gives the torque and speed the shaft carries; the shaft gives the bearing load and the seat diameter; the bearing life uses the shaft reaction and the motor speed; the seat fit uses the shaft diameter; the coupling flange bolts carry the motor's peak torque.",
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
    /// given its two rooms as inputs: instantiating never creates table rows.
    /// </summary>
    public static CalcPackTemplate WetHeating { get; } = new(
        "pack.wet-heating", "Wet heating",
        "Sizes a wet heating circuit from the rooms to the circulator: room heat losses, emitters and expansion vessel, the pipework and pump at the flow that heat needs, and the motor that drives the pump.",
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
    /// The links depend on S2's numbering, which the example inputs fix: nodes 1 (column base, fixed), 2 (column top) and 3 (arm tip);
    /// member 1 the column (1 → 2) and member 2 the arm (2 → 3); the first load row (case 1) the vertical equipment load at node 3, which S5
    /// fills, and the second (case 2) the horizontal ULS design action at node 2. Give S2 another numbering and the links to
    /// <c>R1_*</c>, <c>*_e1</c> and <c>*_e2</c> must follow it. S2 must be given its nodes, elements and loads (instantiating never creates
    /// rows); the PROFIS anchorage report is attached to S6 as Evidence.
    /// </para>
    /// </summary>
    public static CalcPackTemplate SteelSupport { get; } = new(
        "pack.steel-support", "Steel support",
        "Combines the actions on an equipment support, analyses the steel frame, checks the column, designs the bracket's bolted joint and the column base plate, and files the anchorage design done elsewhere as Evidence. The links assume the frame's numbering: nodes 1 (column base, fixed), 2 (column top), 3 (arm tip); member 1 the column, member 2 the arm; the first load row the vertical load at node 3 (filled from the load combinations), the second the horizontal design action at node 2.",
        [
            new CalcPackStep("s5", "struct.load-combinations", "Load combinations", CalcPackExamples.SteelS5),
            new CalcPackStep("s2", "struct.frame-2d", "Support frame", CalcPackExamples.SteelS2),
            new CalcPackStep("s3", "struct.en1993-member", "Column check", CalcPackExamples.SteelS3),
            new CalcPackStep("m3", "mech.bolt-group", "Arm joint bolt group", CalcPackExamples.SteelM3),
            new CalcPackStep("s4a", "struct.connection-ec3", "Arm connection bolts", CalcPackExamples.SteelS4A),
            new CalcPackStep("s4b", "struct.connection-ec3", "Holding-down bolts", CalcPackExamples.SteelS4B),
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

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit, WetHeating, Cooling, SteelSupport];
}
