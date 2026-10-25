using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>
/// Addendum A, §A7: the example inputs of the built-in chains (see <see cref="CalcPackStep.Example"/>). They are invented for illustration
/// — a conveyor head drive, a servo axis, a two-room heating circuit, a drive panel's cooling, an equipment support and a hydraulic power unit — and every one is
/// recorded as an assumption to replace. Choice values are the calculators' own texts (the Library does not reference the calculator
/// assemblies); instantiating each template in the tests proves they are accepted. No tabulated regulation data appears, and catalogue data
/// is limited to the single steel section the steel-support example uses as a user input (UC 203×203×46). The motor circuit's
/// cable-capacity and device-rating tables are not here, so that chain stays refused until the user supplies them (the hydraulic power unit's cable step is
/// in the same position: it opens blocked, naming the tables, until they are supplied).
/// </summary>
internal static class CalcPackExamples
{
    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue C(string symbol, string value) => CalcInputValue.Choice(symbol, value);

    private static CalcInputValue Table(string symbol, params CalcRow[] rows) => CalcInputValue.Table(symbol, rows);

    private static (string, CalcCell) Mm(string key, double value) => (key, new CalcCell(value, "mm"));

    private static (string, CalcCell) Num(string key, double value) => (key, new CalcCell(value));

    private static (string, CalcCell) Txt(string key, string value) => (key, new CalcCell(Text: value));

    // ---- Drive train: a 4-pole motor through a 12.5:1 gearbox turns a conveyor drum at 120 rpm against 1.8 kN·m -------------

    public static IReadOnlyList<CalcInputValue> DriveE1 { get; } =
    [
        C("mechanism", "gearbox rotary"), C("duty_mode", "continuous"), N("i", 12.5), N("eta", 0.95),
        Q("J_mech", 0.002, "kg*m^2"), Q("J_L", 4, "kg*m^2"), Q("T_L", 1800, "N*m"), Q("n_L", 120, "rpm"), Q("t_start", 3, "s"),
        Q("J_motor", 0.12, "kg*m^2"), Q("T_rated", 200, "N*m"), Q("T_peak", 400, "N*m"), Q("n_max", 3000, "rpm"),
    ];

    /// <summary>
    /// The gearbox input shaft: coupling at one end, a pinion of 60 mm pitch RADIUS (120 mm pitch diameter) at mid-span. T and n arrive by link, but the
    /// mesh forces are entered, derived for T = 151.58 N·m: F_t = T/r_p = 2.53 kN (horizontal plane) and F_r = F_t·tan 20° = 0.92 kN (vertical plane).
    /// They do not follow a change of E1 by themselves: re-derive them when T_rms changes.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> DriveM4 { get; } =
    [
        Table("segments",
            CalcRow.Of(Mm("x_end", 50), Mm("d", 35), Mm("d_i", 0)), CalcRow.Of(Mm("x_end", 250), Mm("d", 45), Mm("d_i", 0)), CalcRow.Of(Mm("x_end", 300), Mm("d", 35), Mm("d_i", 0))),
        Q("x_A", 25, "mm"), Q("x_B", 275, "mm"),
        Table("loads", CalcRow.Of(Mm("x", 150), ("F_y", new CalcCell(-0.92, "kN")), ("F_z", new CalcCell(2.53, "kN")), ("m", new CalcCell(3, "kg")))),
        Q("x_T1", 0, "mm"), Q("x_T2", 150, "mm"),
        Table("sections",
            CalcRow.Of(Mm("x", 50), Num("K_t", 1.7), Num("K_ts", 1.5), Num("q", 0.85), Num("q_s", 0.9)), CalcRow.Of(Mm("x", 150), Num("K_t", 1.7), Num("K_ts", 1.5), Num("q", 0.85), Num("q_s", 0.9))),
        Q("S_ut", 850, "MPa"), Q("S_y", 650, "MPa"), Q("E", 207, "GPa"), Q("rho", 7850, "kg/m^3"), C("finish", "ground"),
    ];

    public static IReadOnlyList<CalcInputValue> DriveM5 { get; } =
    [
        C("type", "ball (p = 3)"), Q("C", 25.5, "kN"), Q("C_0", 15.3, "kN"), Q("R", 90, "%"), Q("L_req", 20000, "h"),
    ];

    /// <summary>
    /// The bearing seat: a k6 shaft in the bearing's bore. A rolling-bearing bore is not an ISO 286 H7 hole: it has its own tolerance (ISO 492), entered here from
    /// the bearing record; 0 / −9 µm is an invented illustration, to be replaced by the bearing maker's figure.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> DriveM2 { get; } =
    [
        C("hole_kind", "rolling-bearing bore (deviations from the bearing record)"), Q("ES_override", 0, "µm"), Q("EI_override", -9, "µm"), C("shaft_letter", "k"), N("shaft_grade", 6),
    ];

    /// <summary>Four M8 8.8 bolts (stress area 36.6 mm², shear resistance 14.0 kN, tension 21.0 kN) on an 80 mm circle carry the torque by shear.</summary>
    public static IReadOnlyList<CalcInputValue> DriveM3 { get; } =
    [
        Table("bolts", Bolt(40, 0, 36.6), Bolt(0, 40, 36.6), Bolt(-40, 0, 36.6), Bolt(0, -40, 36.6)),
        Q("F_x", 0, "kN"), Q("F_y", 0, "kN"), Q("e_x", 0, "mm"), Q("e_y", 0, "mm"), Q("N", 0, "kN"), Q("M_x", 0, "kN*m"), Q("M_y", 0, "kN*m"),
        Q("V_R", 14.0, "kN"), Q("T_R", 21.0, "kN"),
    ];

    private static CalcRow Bolt(double x, double y, double area) => CalcRow.Of(Mm("x", x), Mm("y", y), ("A", new CalcCell(area, "mm^2")));

    // ---- Motor circuit: a ball-screw servo axis, its panel and its cable -------------------------------------------------------

    public static IReadOnlyList<CalcInputValue> MotorE1 { get; } =
    [
        C("mechanism", "lead / ball screw"), Q("p_screw", 10, "mm"), Q("m", 50, "kg"), N("mu", 0.1), N("s", 0.5), Q("t_move", 0.5, "s"),
        Q("n_max", 10000, "rpm"),
    ];

    /// <summary>
    /// The panel's own loads: 24 V electronics and a fan inside the enclosure release all their input power as heat there (heat share 1); the
    /// 24 V supply is fed from the single-phase bus. The axis motor arrives by link and is outside the enclosure (its heat share is 0).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> MotorE6 { get; } =
    [
        Table("loads",
            CalcRow.Of(Txt("name", "24 V controls and I/O"), Txt("supply", "DC"), ("P", new CalcCell(0.12, "kW")), Num("cosphi", 1), Num("eta", 0.9), Num("heat", 1), Num("d", 1), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false))),
            CalcRow.Of(Txt("name", "Panel fan"), Txt("supply", "AC single-phase"), ("P", new CalcCell(0.02, "kW")), Num("cosphi", 0.9), Num("eta", 0.7), Num("heat", 1), Num("d", 1), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false)))),
        N("cosphi_add", 0.85), N("eta_add", 0.97), N("heat_add", 0), C("psu_bus", "AC single-phase"), N("eta_psu", 0.9),
    ];

    /// <summary>The cable (25 m, PVC, type C device, method C). The capacity and rating tables are NOT supplied: they are the user's own.</summary>
    public static IReadOnlyList<CalcInputValue> MotorE4 { get; } =
    [
        Q("L", 25, "m"), Q("U", 400, "V"), C("insulation", "PVC 70 °C"), C("method", "C"), C("device", "MCB type C"),
    ];

    public static IReadOnlyList<CalcInputValue> MotorE3 { get; } =
    [
        C("system", "AC three-phase"), Q("V_n", 400, "V"), N("cos_phi", 0.85), Q("T_amb", 30, "degC"),
    ];

    /// <summary>
    /// The device is the type C MCB the cable step sizes (the choice must agree with it or the step is blocked); the protective conductor is
    /// the same size as the line conductor; the let-through energy is an invented figure for a small MCB, to be replaced from its datasheet.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> MotorE5 { get; } =
    [
        Q("U_0", 230, "V"), Q("Z_e", 0.35, "ohm"), Q("S_cpc", 1.5, "mm^2"), C("device", "MCB type C (I_a = 10 I_n)"), Q("t_req", 0.4, "s"), Q("I2t_let", 1.5e4, "A^2*s"),
    ];

    /// <summary>A 1000 × 1200 × 300 enclosure against a wall.</summary>
    public static IReadOnlyList<CalcInputValue> MotorE2 { get; } =
    [
        Table("loads",
            CalcRow.Of(Txt("name", "Servo drive"), ("P", new CalcCell(60, "W")), Num("qty", 1), Num("duty", 1)),
            CalcRow.Of(Txt("name", "Contactors, terminals and busbars"), ("P", new CalcCell(20, "W")), Num("qty", 1), Num("duty", 1))),
        Q("W", 1.0, "m"), Q("H", 1.2, "m"), Q("D", 0.3, "m"), Q("T_amb", 35, "degC"), Q("T_max", 55, "degC"),
    ];

    // ---- Wet heating: two rooms to a circulator motor ------------------------------------------------------------------------

    private static readonly (string Element, string Layer, double D, double Lambda)[] CavityLayers =
    [
        ("Cavity wall", "Outer brick leaf", 102.5, 0.84), ("Cavity wall", "Insulation", 100, 0.03), ("Cavity wall", "Inner block leaf", 100, 0.2), ("Cavity wall", "Plaster", 13, 0.4),
    ];

    private static CalcInputValue Layers(params (string Element, string Layer, double D, double Lambda)[] rows) =>
        Table("layers", [.. rows.Select(r => CalcRow.Of(Txt("element", r.Element), Txt("name", r.Layer), Mm("d", r.D), ("lambda", new CalcCell(r.Lambda, "W/(m*K)"))))]);

    /// <summary>A room with an insulated cavity wall and a window, naturally ventilated (Φ about 379 W). The layer conductivities are invented, illustrative values.</summary>
    public static IReadOnlyList<CalcInputValue> WetRoomA { get; } =
    [
        Q("theta_int", 21, "degC"), Q("theta_e", -3, "degC"), Layers(CavityLayers),
        Table("elements",
            CalcRow.Of(Txt("name", "Cavity wall"), ("area", new CalcCell(20, "m^2"))),
            CalcRow.Of(Txt("name", "Window"), ("area", new CalcCell(3, "m^2")), ("U_user", new CalcCell(1.4, "W/(m^2*K)")))),
        Q("U_target", 0.3, "W/(m^2*K)"), Q("n", 0.5, "1/h"), Q("V", 40, "m^3"),
    ];

    /// <summary>A room with MVHR, a partition to an 18 °C space and a ground floor with a user U (Φ about 264 W). The partition is not held to the outside target U.</summary>
    public static IReadOnlyList<CalcInputValue> WetRoomB { get; } =
    [
        Q("theta_int", 21, "degC"), Q("theta_e", -3, "degC"),
        Layers([.. CavityLayers, ("Partition", "Plasterboard", 12.5, 0.22), ("Partition", "Plasterboard", 12.5, 0.22)]),
        Table("elements",
            CalcRow.Of(Txt("name", "Cavity wall"), ("area", new CalcCell(15, "m^2"))),
            CalcRow.Of(Txt("name", "Partition"), ("area", new CalcCell(12, "m^2")), ("theta_adj", new CalcCell(18, "degC")), ("Rg", new CalcCell(0.18, "m^2*K/W"))),
            CalcRow.Of(Txt("name", "Ground floor"), ("area", new CalcCell(10, "m^2")), Num("f", 0.6), ("U_user", new CalcCell(0.25, "W/(m^2*K)")))),
        Q("y_tb", 0.05, "W/(m^2*K)"), Q("n", 0.6, "1/h"), Q("V", 50, "m^3"), Q("theta_supply", 16, "degC"),
    ];

    /// <summary>
    /// The sealed system is given its two rooms (instantiating never creates rows); each room's Φ is a placeholder the cell links replace. The pipework
    /// water content is B1's V_pipe (π/4 × 0.010² × 40 m = 3.14 L), entered by hand: B1 takes its heat load from B6, so a link back from B1 would be a cycle.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> WetB6 { get; } =
    [
        Q("V_pipe", 3.14, "L"),
        Table("rooms",
            CalcRow.Of(Txt("name", "Room A"), ("Phi", new CalcCell(1, "W")), ("V_em", new CalcCell(4, "L"))),
            CalcRow.Of(Txt("name", "Room B"), ("Phi", new CalcCell(1, "W")), ("V_em", new CalcCell(4, "L")))),
    ];

    /// <summary>
    /// A closed flow-and-return loop in 10 mm microbore: no static lift. The load is small (about 0.64 kW), so the flow is about 57 L/h and the pump duty a
    /// fraction of a watt hydraulic: a deliberately tiny illustration.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> WetB1 { get; } =
    [
        C("fluid", "water"), C("circuit", "closed (flow and return: the rises cancel, no static lift)"),
        Table("segments", CalcRow.Of(Txt("name", "primary flow and return"), ("L", new CalcCell(40, "m")), Mm("D", 10), Mm("eps", 0.045), Num("K", 12), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), Txt("side", "discharge"))),
        Q("n_pump", 2800, "rpm"),
    ];

    public static IReadOnlyList<CalcInputValue> WetE1 { get; } =
    [
        C("mechanism", "direct rotary"), C("duty_mode", "continuous"), Q("J_L", 0.002, "kg*m^2"), Q("J_mech", 0, "kg*m^2"), Q("t_start", 5, "s"),
    ];

    // ---- Cooling: a drive panel's heat to a water-cooled exchanger -----------------------------------------------------------

    /// <summary>Two main drives, servo drives, 24 V supplies: what the panel dissipates is Σ (1 − η) P.</summary>
    public static IReadOnlyList<CalcInputValue> CoolE6 { get; } =
    [
        Table("loads",
            Drive("Extruder drive", 55, 0.95, 0.97, 1), Drive("Pump drive", 30, 0.95, 0.975, 1), Drive("Servo drives (4)", 30, 0.9, 0.96, 0.8), Drive("24 V supplies", 1.5, 0.9, 0.9, 1)),
        N("cosphi_add", 0.85),
    ];

    private static CalcRow Drive(string name, double kw, double cosphi, double eta, double duty) =>
        CalcRow.Of(Txt("name", name), Txt("supply", "AC three-phase"), ("P", new CalcCell(kw, "kW")), Num("cosphi", cosphi), Num("eta", eta), Num("d", duty), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false)));

    /// <summary>A floor-standing enclosure 1200 × 2000 × 600 with its back to a wall and a neighbour on the right.</summary>
    public static IReadOnlyList<CalcInputValue> CoolE2 { get; } =
    [
        Table("loads", CalcRow.Of(Txt("name", "Busbars, contactors and terminals"), ("P", new CalcCell(150, "W")), Num("qty", 1), Num("duty", 1))),
        Q("W", 1.2, "m"), Q("H", 2.0, "m"), Q("D", 0.6, "m"),
        N("f_top", 1), N("f_front", 1), N("f_rear", 0), N("f_left", 1), N("f_right", 0), N("f_bottom", 0),
        Q("T_amb", 35, "degC"), Q("T_max", 45, "degC"),
    ];

    /// <summary>
    /// A water-cooled air coil: the enclosure air (c_p 1006 J/kg·K, the constant E2 uses; 0.4 kg/s through the coil) enters at E2's temperature
    /// limit and gives its heat to plant cooling water (22 °C, 0.25 kg/s), counterflow. U = 60 W/m²K is an air-side coil figure.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> CoolB5 { get; } =
    [
        C("arrangement", "counterflow"), C("mode", "sizing (given duty)"),
        Q("m_h", 0.4, "kg/s"), Q("cp_h", 1006, "J/(kg*K)"),
        Q("m_c", 0.25, "kg/s"), Q("cp_c", 4180, "J/(kg*K)"), Q("T_c_in", 22, "degC"),
        Q("U", 60, "W/(m^2*K)"), Q("R_f", 0.0001, "m^2*K/W"),
    ];

    /// <summary>A closed supply-and-return loop: no static lift.</summary>
    public static IReadOnlyList<CalcInputValue> CoolB1 { get; } =
    [
        C("fluid", "water"), C("circuit", "closed (flow and return: the rises cancel, no static lift)"),
        Table("segments", CalcRow.Of(Txt("name", "cooling water supply and return"), ("L", new CalcCell(30, "m")), Mm("D", 25), Mm("eps", 0.045), Num("K", 12), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), Txt("side", "discharge"))),
        Q("n_pump", 2800, "rpm"),
    ];

    // ---- Steel support: an equipment bracket on a cantilever column ------------------------------------------------------------

    /// <summary>The equipment's dead load and the maintenance load, both downward (negative effects), EN 1990 6.10.</summary>
    public static IReadOnlyList<CalcInputValue> SteelS5 { get; } =
    [
        Table("actions",
            CalcRow.Of(Txt("name", "Equipment dead load"), Txt("category", "G permanent"), Num("F_k", -12)),
            CalcRow.Of(Txt("name", "Maintenance load"), Txt("category", "E storage"), Num("F_k", -8))),
        C("unit", "kN"), C("method", "6.10"),
    ];

    /// <summary>
    /// A 3 m cantilever column (node 1 fixed, node 2 its top) with a 1 m bracket arm (node 3 its tip): member 1 is the column (1 → 2),
    /// member 2 the arm (2 → 3). The first load row (case 1) is the equipment load at node 3, a placeholder S5's ULS effect replaces by cell link;
    /// the second (case 2) is the 5 kN horizontal ULS design action at node 2. The combination "ULS" adds the two at factor 1: both are already design values.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> SteelS2 { get; } =
    [
        Table("nodes",
            CalcRow.Of(Num("id", 1), ("x", new CalcCell(0, "m")), ("y", new CalcCell(0, "m")), Txt("support", "fixed")),
            CalcRow.Of(Num("id", 2), ("x", new CalcCell(0, "m")), ("y", new CalcCell(3, "m")), Txt("support", "free")),
            CalcRow.Of(Num("id", 3), ("x", new CalcCell(1, "m")), ("y", new CalcCell(3, "m")), Txt("support", "free"))),
        Table("elements",
            CalcRow.Of(Num("id", 1), Num("i", 1), Num("j", 2), ("A", new CalcCell(58.7, "cm^2")), ("I", new CalcCell(4568, "cm^4")), ("rel_i", new CalcCell(Flag: false)), ("rel_j", new CalcCell(Flag: false))),
            CalcRow.Of(Num("id", 2), Num("i", 2), Num("j", 3), ("A", new CalcCell(58.7, "cm^2")), ("I", new CalcCell(4568, "cm^4")), ("rel_i", new CalcCell(Flag: false)), ("rel_j", new CalcCell(Flag: false)))),
        Table("loads", FrameLoad(1, 3, 0, -1), FrameLoad(2, 2, 5, 0)),
        Table("combinations",
            CalcRow.Of(Txt("name", "ULS"), Num("case", 1), Num("factor", 1)), CalcRow.Of(Txt("name", "ULS"), Num("case", 2), Num("factor", 1))),
        Q("E", 210, "GPa"), new CalcInputValue("report", Text: "ULS"),
    ];

    private static CalcRow FrameLoad(int @case, int node, double fx, double fy) =>
        CalcRow.Of(Num("case", @case), Txt("kind", "nodal"), Num("target", node), ("a", new CalcCell(0, "m")), ("b", new CalcCell(0, "m")), ("w1", new CalcCell(0, "kN/m")), ("w2", new CalcCell(0, "kN/m")),
            ("F_x", new CalcCell(fx, "kN")), ("F_y", new CalcCell(fy, "kN")), ("M", new CalcCell(0, "kN*m")));

    /// <summary>The column: 203 × 203 UC in S355 (properties as in S3's defaults), fixed base and free top, buckling length 2 × 3 m.</summary>
    public static IReadOnlyList<CalcInputValue> SteelS3 { get; } =
    [
        Q("M_z_Ed", 0, "kN*m"), Q("f_y", 355, "MPa"), Q("L_cr_y", 6, "m"), Q("L_cr_z", 6, "m"),
    ];

    /// <summary>
    /// The arm's bolted end-plate joint: six M20 8.8 bolts in two columns 120 mm apart and three rows 150 mm apart (bolt-group step). The
    /// moment arrives as an out-of-plane moment about the bolt-group centroid and the shear as the in-plane force; the bolts' resistances are
    /// those of M20 8.8 (94.0 kN shear, 141.0 kN tension, the same values the connection step computes).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> SteelM3 { get; } =
    [
        Table("bolts", Bolt(-60, 150, 245), Bolt(60, 150, 245), Bolt(-60, 0, 245), Bolt(60, 0, 245), Bolt(-60, -150, 245), Bolt(60, -150, 245)),
        Q("F_x", 0, "kN"), Q("e_x", 0, "mm"), Q("e_y", 0, "mm"), Q("T_z", 0, "kN*m"), Q("N", 0, "kN"), Q("M_y", 0, "kN*m"),
        Q("V_R", 94.0, "kN"), Q("T_R", 141.0, "kN"),
    ];

    /// <summary>The arm joint's bolt checks: M20 8.8 through a 20 mm S275 end plate; the bolt forces arrive by link from the bolt group.</summary>
    public static IReadOnlyList<CalcInputValue> SteelS4A { get; } =
    [
        C("grade", "8.8"), C("size", "M20"), C("steel", "S275"), Q("t", 20, "mm"),
        Q("e_1", 40, "mm"), Q("e_2", 40, "mm"), Q("p_1", 150, "mm"), Q("p_2", 120, "mm"),
        Q("A_nt", 1200, "mm^2"), Q("A_nv", 2400, "mm^2"),
    ];

    /// <summary>
    /// The holding-down bolts in the 25 mm base plate: M20 8.8 with the plate's edge distance (the plate edge is 40 mm from the bolt row)
    /// and a 150 mm gauge across it, so S6 gets bearing for the plate it designs. The step supplies bolt resistances to S6; the anchor demands
    /// are checked in S6, so none is entered here.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> SteelS4B { get; } =
    [
        C("grade", "8.8"), C("size", "M20"), C("steel", "S275"), Q("t", 25, "mm"),
        Q("e_1", 40, "mm"), Q("e_2", 75, "mm"), Q("p_2", 150, "mm"),
        Q("F_v_Ed", 0, "kN"), Q("F_t_Ed", 0, "kN"),
    ];

    /// <summary>
    /// The base plate: 350 × 300 × 25 on C25/30 concrete, two holding-down bolts each side 135 mm from the column axis, 40 mm to the plate edge. The foundation
    /// concentration factor is 1 (no pad geometry is claimed). The tension T-stub geometry is entered, not defaulted: m = 28 mm (bolt to weld toe) and
    /// l_eff = 150 mm for the two bolts of a row (half the plate width governs, 0.5·b_p, from the cl. 6.2.6.5 patterns).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> SteelS6 { get; } =
    [
        C("column", "I/H"), Q("h", 203.2, "mm"), Q("b", 203.6, "mm"), Q("t_f", 11, "mm"), Q("t_w", 7.2, "mm"),
        Q("B", 300, "mm"), Q("L", 350, "mm"), Q("t_p", 25, "mm"), N("alpha", 1), Q("x_b", 135, "mm"), Q("m", 28, "mm"), Q("l_eff", 150, "mm"), Q("e_min", 40, "mm"),
    ];

    // ---- Hydraulic power unit: a 160 bar press circuit, its accumulator, pressure line, pump motor and supply cable ----------------

    /// <summary>
    /// A double-acting 100/70 × 500 mm cylinder at 160 bar fed by a 28 cm³/rev pump at 1450 rpm (38.6 L/min delivered against the circuit's 36 L/min), mineral
    /// oil of 870 kg/m³. The 80 kN push and 40 kN pull are the press loads; the rest of the sheet (mounting factor, line velocity limits, rod material) is left at the
    /// sheet's own defaults, which the chain tests record as assumptions. The pressure line is 20 mm and goes to the pressure-line step by link, with the oil density (the
    /// line to the cap port carries the 75.6 L/min retract discharge at 4.0 m/s, inside the 5 m/s placeholder limit).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuF1 { get; } =
    [
        C("mode", "Standard"), Q("p", 160, "bar"), Q("Q", 36, "L/min"), Q("rho", 870, "kg/m^3"), Q("F_push", 80, "kN"), Q("F_pull", 40, "kN"),
        Q("D", 100, "mm"), Q("d_r", 70, "mm"), Q("stroke", 500, "mm"), Q("V_g", 28, "cm^3/rev"), Q("n_p", 1450, "rpm"), Q("d_prs", 20, "mm"),
    ];

    /// <summary>
    /// An emergency stroke: the accumulator carries the whole stroke volume (linked) from the system pressure (linked) down to 120 bar gauge, above the 107 bar the 80 kN
    /// push needs on the 100 mm bore (80 kN / (78.5 cm² × 0.95)). Fast discharge, so n = 1.4; 105 bar gauge pre-charge (at 50 °C) from the manufacturer's rule of
    /// p0 below 0.9 p1 on absolute pressures; a cold shed at 10 °C. These are invented illustrations.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuF2 { get; } =
    [
        C("mode", "Emergency stroke"), Q("p_0", 105, "bar"), Q("p_1", 120, "bar"), N("n", 1.4), Q("T_max", 50, "degC"), Q("T_min", 10, "degC"),
    ];

    /// <summary>
    /// The pressure line from the unit to the cylinder cap port: 12 m of tube with 6 velocity heads of fittings. The flow (the cap-port line flow of the cylinder step,
    /// 75.6 L/min, the larger of its two strokes), the oil density and the tube bore (the first table row's <c>D</c>) are all linked from the cylinder step, so the line B1
    /// judges is the line F1 judges; the 20 mm bore here is only the row's placeholder. The oil is a custom fluid (30 mPa·s at the working temperature, 50 °C; its density
    /// is linked). At 75.6 L/min in 20 mm the flow is transitional (Re about 2330, just above the 2300 where the sheet switches from 64/Re to Colebrook–White), so
    /// the sheet warns that the friction factor is uncertain; the Colebrook value is the larger of the two and so the conservative one. The 6 velocity heads of fittings
    /// are a turbulent-flow figure; the real K rises at low Reynolds number, so at Re 2300 the fittings term is understated and the sheet warns of it (`fittings`).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuB1 { get; } =
    [
        C("fluid", "custom (ρ and μ given)"), Q("T", 50, "degC"), Q("mu_custom", 30, "mPa*s"), Q("v_max", 5, "m/s"),
        Table("segments", CalcRow.Of(Txt("name", "pressure line to cylinder"), ("L", new CalcCell(12, "m")), Mm("D", 20), Mm("eps", 0.045), Num("K", 6), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), Txt("side", "discharge"))),
    ];

    /// <summary>
    /// The pump drive: continuous running of a direct-coupled pump (coupling efficiency 0.99) with a 2 s run-up, against a candidate 18.5 kW 4-pole motor (120 N·m
    /// rated, 275 N·m peak, 3000 rpm). The pump torque and speed arrive by link.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuE1 { get; } =
    [
        C("mechanism", "direct rotary"), C("duty_mode", "continuous"), N("eta", 0.99), Q("J_L", 0.004, "kg*m^2"), Q("J_mech", 0.001, "kg*m^2"), Q("t_start", 2, "s"),
        Q("J_motor", 0.1, "kg*m^2"), Q("T_rated", 120, "N*m"), Q("T_peak", 275, "N*m"), Q("n_max", 3000, "rpm"),
    ];

    /// <summary>The motor's supply cable (30 m, PVC, type C device, method C; 400 V, motor efficiency 0.92 and power factor 0.86). The capacity and rating tables are NOT supplied: they are the user's own.</summary>
    public static IReadOnlyList<CalcInputValue> HpuE4 { get; } =
    [
        Q("L", 30, "m"), Q("U", 400, "V"), N("cos_phi", 0.86), N("eta", 0.92), C("insulation", "PVC 70 °C"), C("method", "C"), C("device", "MCB type C"),
    ];
}
