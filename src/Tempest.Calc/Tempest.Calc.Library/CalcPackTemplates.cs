namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the linked chains every build ships as pack templates. Steps name calculators by template id
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
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing"),
            new CalcPackStep("m4", "mech.shaft", "Shaft"),
            new CalcPackStep("m5", "mech.bearing-life", "Bearing A life"),
            new CalcPackStep("m2", "mech.iso286-fit", "Bearing A seat fit"),
            new CalcPackStep("m3", "mech.bolt-group", "Coupling flange bolts"),
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
    /// enclosure heat (E2). E1's peak power enters E6 as a linked load; E6's bus current sizes the cable; the cable's
    /// size, length and device feed E3 and E5; E6's dissipated heat enters E2.
    /// </summary>
    public static CalcPackTemplate MotorCircuit { get; } = new(
        "pack.motor-circuit", "Motor circuit",
        "Sizes a motor's supply from the motor duty to the enclosure: demand, cable and device, voltage drop, earth-fault disconnection and the heat the panel must lose.",
        [
            new CalcPackStep("e1", "elec.motor-sizing", "Motor sizing"),
            new CalcPackStep("e6", "elec.load-budget", "Load budget"),
            new CalcPackStep("e4", "elec.conductor-sizing", "Cable and device"),
            new CalcPackStep("e3", "elec.voltage-drop", "Voltage drop"),
            new CalcPackStep("e5", "elec.fault-protection", "Fault protection"),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure heat"),
        ],
        [
            new CalcPackBinding("e1", "P_pk", "e6", "P_add"),
            new CalcPackBinding("e6", "I_3ph", "e4", "I_b_in"),
            new CalcPackBinding("e4", "I_b", "e3", "I"),
            new CalcPackBinding("e4", "L", "e3", "L"),
            new CalcPackBinding("e4", "CSA", "e3", "A_c"),
            new CalcPackBinding("e4", "CSA", "e5", "S_line"),
            new CalcPackBinding("e4", "L", "e5", "L"),
            new CalcPackBinding("e4", "I_n", "e5", "I_n"),
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
            new CalcPackStep("b4a", "bs.room-heat-loss", "Room A heat loss"),
            new CalcPackStep("b4b", "bs.room-heat-loss", "Room B heat loss"),
            new CalcPackStep("b6", "bs.sealed-system", "Sealed system"),
            new CalcPackStep("b1", "bs.pipe-pump", "Primary pipework and pump"),
            new CalcPackStep("e1", "elec.motor-sizing", "Circulator motor"),
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
    /// Cooling: panel loads (E6) → enclosure heat (E2) → heat exchanger (B5) → cooling-water pipework and pump (B1). E6's dissipated heat
    /// is a linked load of the enclosure; the heat the enclosure cannot lose naturally (E2's cooling-unit duty) is the exchanger's
    /// required duty (sizing mode); the exchanger's duty, cold-side temperature rise and mean cold-side temperature give B1 the cooling
    /// water's design flow and temperature (the heat-load form of B1, as in the wet-heating chain). The cold-side flow is B5's input; B1
    /// works the same flow out from the duty, so give B1 the cold side's specific heat (<c>cp_load</c>) for the two to agree.
    /// </summary>
    public static CalcPackTemplate Cooling { get; } = new(
        "pack.cooling", "Enclosure cooling",
        "Takes the heat a control panel dissipates, finds what the enclosure cannot lose naturally, sizes the water-cooled exchanger that removes it and the pipework and pump that carry the cooling water.",
        [
            new CalcPackStep("e6", "elec.load-budget", "Panel load budget"),
            new CalcPackStep("e2", "elec.enclosure-heat", "Enclosure heat"),
            new CalcPackStep("b5", "bs.heat-exchanger", "Cooling-water exchanger"),
            new CalcPackStep("b1", "bs.pipe-pump", "Cooling-water pipework and pump"),
        ],
        [
            new CalcPackBinding("e6", "Phi", "e2", "P_add"),
            new CalcPackBinding("e2", "P_cool", "b5", "Q_req"),
            new CalcPackBinding("b5", "Q", "b1", "Phi_load"),
            new CalcPackBinding("b5", "dT_c", "b1", "dT_load"),
            new CalcPackBinding("b5", "T_c_m", "b1", "T"),
        ]);

    /// <summary>
    /// Steel support: load combinations (S5) → frame (S2) → member check (S3), connection (S4) → base plate (S6). S5's governing downward
    /// ULS effect is a cell of S2's load table (a table-cell link); S2's column force, moment and shear are S3's design actions; S2's
    /// arm shear is the connection's group force; S2's base reactions are the base plate's actions; S4's bolt values are the
    /// holding-down bolt data of S6. S2 must be given its nodes, elements and loads as inputs (instantiating never creates rows), and S6's
    /// anchorage is designed externally: the PROFIS report is attached to S6 as Evidence.
    /// </summary>
    public static CalcPackTemplate SteelSupport { get; } = new(
        "pack.steel-support", "Steel support",
        "Combines the actions on an equipment support, analyses the steel frame, checks the column, designs the bolted connection and the column base plate, and files the anchorage design done elsewhere as Evidence.",
        [
            new CalcPackStep("s5", "struct.load-combinations", "Load combinations"),
            new CalcPackStep("s2", "struct.frame-2d", "Support frame"),
            new CalcPackStep("s3", "struct.en1993-member", "Column check"),
            new CalcPackStep("s4", "struct.connection-ec3", "Arm connection and bolts"),
            new CalcPackStep("s6", "struct.base-plate", "Column base plate"),
        ],
        [
            new CalcPackBinding("s5", "E_d_min", "s2", "loads", Row: 0, Column: "F_y"),
            new CalcPackBinding("s2", "N_e1", "s3", "N_Ed"),
            new CalcPackBinding("s2", "M_abs_e1", "s3", "M_y_Ed"),
            new CalcPackBinding("s2", "V_e1", "s3", "V_Ed"),
            new CalcPackBinding("s2", "V_e2", "s4", "V_Ed"),
            new CalcPackBinding("s2", "R1_y", "s6", "N_Ed"),
            new CalcPackBinding("s2", "R1_m_abs", "s6", "M_Ed"),
            new CalcPackBinding("s2", "R1_x_abs", "s6", "V_Ed"),
            new CalcPackBinding("s4", "F_t_Rd", "s6", "F_t_Rd"),
            new CalcPackBinding("s4", "A_s", "s6", "A_s"),
            new CalcPackBinding("s4", "F_b_Rd", "s6", "F_1_vb_Rd"),
        ]);

    /// <summary>Every built-in chain.</summary>
    public static IReadOnlyList<CalcPackTemplate> All { get; } = [DriveTrain, MotorCircuit, WetHeating, Cooling, SteelSupport];
}
