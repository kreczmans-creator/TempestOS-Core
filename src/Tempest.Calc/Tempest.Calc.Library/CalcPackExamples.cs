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

    /// <summary>The 4-pole motor's rated speed (1480 rpm) is an INVENTED nameplate figure for this example, to be replaced from the real motor's nameplate.</summary>
    public static IReadOnlyList<CalcInputValue> DriveE1 { get; } =
    [
        C("mechanism", "gearbox rotary"), C("duty_mode", "continuous"), N("i", 12.5), N("eta", 0.95),
        Q("J_mech", 0.002, "kg*m^2"), Q("J_L", 4, "kg*m^2"), Q("T_L", 1800, "N*m"), Q("n_L", 120, "rpm"), Q("t_start", 3, "s"),
        Q("J_motor", 0.12, "kg*m^2"), Q("T_rated", 200, "N*m"), Q("T_peak", 400, "N*m"), Q("n_rated", 1480, "rpm"), Q("n_max", 3000, "rpm"),
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

    /// <summary>The circulator motor's rated speed (2850 rpm) is an INVENTED nameplate figure for this example, to be replaced from the real motor's nameplate.</summary>
    public static IReadOnlyList<CalcInputValue> WetE1 { get; } =
    [
        C("mechanism", "direct rotary"), C("duty_mode", "continuous"), Q("J_L", 0.002, "kg*m^2"), Q("J_mech", 0, "kg*m^2"), Q("t_start", 5, "s"), Q("n_rated", 2850, "rpm"),
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
    /// are a turbulent-flow figure; the real K rises at low Reynolds number, so at Re 2300 the fittings term is understated and the sheet warns of it (`fittings`). The line is judged at the
    /// retract discharge, where its pressure drop (about 2.5 bar) is the back pressure the cap side pushes against on the retract stroke; neither that drop nor the supply-side drop is
    /// fed back into F1 (whose <c>p_back</c> and <c>p</c> are entered).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuB1 { get; } =
    [
        C("fluid", "custom (ρ and μ given)"), Q("T", 50, "degC"), Q("mu_custom", 30, "mPa*s"), Q("v_max", 5, "m/s"),
        Table("segments", CalcRow.Of(Txt("name", "pressure line to cylinder"), ("L", new CalcCell(12, "m")), Mm("D", 20), Mm("eps", 0.045), Num("K", 6), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), Txt("side", "discharge"))),
    ];

    /// <summary>
    /// The pump drive: continuous running of a direct-coupled pump (coupling efficiency 0.99) with a 2 s run-up, against a candidate 4-pole motor (120 N·m rated at
    /// 1460 rpm rated speed = 18.35 kW, 275 N·m peak, 3000 rpm). The rated speed is an INVENTED nameplate figure for this example, to be replaced from the real motor's
    /// nameplate; it gives the rated power the cable step is sized on. The pump torque and speed arrive by link.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> HpuE1 { get; } =
    [
        C("mechanism", "direct rotary"), C("duty_mode", "continuous"), N("eta", 0.99), Q("J_L", 0.004, "kg*m^2"), Q("J_mech", 0.001, "kg*m^2"), Q("t_start", 2, "s"),
        Q("J_motor", 0.1, "kg*m^2"), Q("T_rated", 120, "N*m"), Q("T_peak", 275, "N*m"), Q("n_rated", 1460, "rpm"), Q("n_max", 3000, "rpm"),
    ];

    /// <summary>The motor's supply cable (30 m, PVC, type C device, method C; 400 V, motor efficiency 0.92 and power factor 0.86). The capacity and rating tables are NOT supplied: they are the user's own.</summary>
    public static IReadOnlyList<CalcInputValue> HpuE4 { get; } =
    [
        Q("L", 30, "m"), Q("U", 400, "V"), N("cos_phi", 0.86), N("eta", 0.92), C("insulation", "PVC 70 °C"), C("method", "C"), C("device", "MCB type C"),
    ];

    // ---- Envelope moisture: a wall's U-value, the room air state and the Glaser condensation check -----------------------------

    /// <summary>
    /// A room with a solid brick external wall lined internally with 80 mm of mineral wool and plasterboard, and a window. The wall is the FIRST element of the elements table: its
    /// U-value (<c>U_1</c>) is what the Glaser step cross-checks. The layer conductivities, the target U and the window U are invented illustrations, not design data. The 0.2 mm vapour
    /// control layer of the Glaser step is not a row here: it is thermally negligible and below this sheet's 1 mm layer minimum.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> EnvB4 { get; } =
    [
        Q("theta_int", 21, "degC"), Q("theta_e", -3, "degC"),
        Table("layers",
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Plasterboard"), Mm("d", 12.5), ("lambda", new CalcCell(0.25, "W/(m*K)"))),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Mineral wool"), Mm("d", 80), ("lambda", new CalcCell(0.04, "W/(m*K)"))),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Solid brick"), Mm("d", 215), ("lambda", new CalcCell(0.8, "W/(m*K)"))),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Render"), Mm("d", 15), ("lambda", new CalcCell(0.9, "W/(m*K)")))),
        Table("elements",
            CalcRow.Of(Txt("name", "External wall"), ("area", new CalcCell(18, "m^2"))),
            CalcRow.Of(Txt("name", "Window"), ("area", new CalcCell(3, "m^2")), ("U_user", new CalcCell(1.4, "W/(m^2*K)")))),
        Q("U_target", 0.45, "W/(m^2*K)"), Q("n", 0.5, "1/h"), Q("V", 48, "m^3"), Q("A_floor", 16, "m^2"),
    ];

    /// <summary>The room air: 21 °C dry-bulb at 50 % relative humidity, sea level, state only. Its dry-bulb and relative humidity are the Glaser step's internal conditions.</summary>
    public static IReadOnlyList<CalcInputValue> EnvB2 { get; } =
    [
        Q("altitude", 0, "m"), Q("t_db", 21, "degC"), C("known", "relative humidity"), N("RH", 50), C("process", "none (state only)"),
    ];

    /// <summary>
    /// The same wall for the Glaser check (inside first), with a 0.2 mm vapour control layer behind the plasterboard (μ 20000, an invented figure for a taped but imperfect membrane:
    /// real membranes are often higher). The conductivities match the heat-loss step's; the layer thicknesses and conductivities are typed here too, because the
    /// heat-loss step offers no per-layer output to link (a test guards that they agree). The U-value of the layers is cross-checked against the heat-loss step's by link.
    /// The twelve monthly outside conditions are an INVENTED maritime year, not a climate record of any standard; the 500 g/m² limit is an invented illustration of the owner's criterion.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> EnvB7 { get; } =
    [
        C("humidity_mode", "constant internal relative humidity"), C("element", "External wall"),
        Table("layers",
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Plasterboard"), Mm("d", 12.5), ("lambda", new CalcCell(0.25, "W/(m*K)")), Num("mu", 8)),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Vapour control layer"), Mm("d", 0.2), ("lambda", new CalcCell(0.35, "W/(m*K)")), Num("mu", 20000)),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Mineral wool"), Mm("d", 80), ("lambda", new CalcCell(0.04, "W/(m*K)")), Num("mu", 1)),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Solid brick"), Mm("d", 215), ("lambda", new CalcCell(0.8, "W/(m*K)")), Num("mu", 10)),
            CalcRow.Of(Txt("element", "External wall"), Txt("name", "Render"), Mm("d", 15), ("lambda", new CalcCell(0.9, "W/(m*K)")), Num("mu", 25))),
        Q("Rsi", 0.13, "m^2*K/W"), Q("Rse", 0.04, "m^2*K/W"), Q("Rsi_f", 0.25, "m^2*K/W"), Q("RH_crit", 80, "%"), Q("Mc_limit", 500, "g/m^2"),
        Table("climate",
            Month("Jan", 2, 90), Month("Feb", 3, 88), Month("Mar", 6, 84), Month("Apr", 9, 80), Month("May", 13, 76), Month("Jun", 16, 74),
            Month("Jul", 18, 74), Month("Aug", 18, 76), Month("Sep", 15, 82), Month("Oct", 11, 87), Month("Nov", 6, 90), Month("Dec", 3, 91)),
    ];

    private static CalcRow Month(string name, double thetaE, double rhE) =>
        CalcRow.Of(Txt("month", name), ("theta_e", new CalcCell(thetaE, "degC")), ("RH_e", new CalcCell(rhE, "%")));

    // ---- Cold pipe: a chilled water line, the room air around it and the insulation that keeps its surface above the dew point ----

    /// <summary>
    /// A chilled-water flow-and-return line, 6 °C flow and 12 °C return, so the MEAN fluid temperature <c>T</c> the properties are taken at is 9 °C, while the temperature the insulation is sized on is the coldest in
    /// the line, the 6 °C flow (<c>T_line</c>: a link from the mean would understate the thickness). 60 m of 54 × 1.2 mm copper tube (inner diameter 51.6 mm; the outside diameter is the insulation's inside diameter
    /// and goes to the insulation step by link), 12 m³/h, ten velocity heads of fittings. The tube roughness (0.0015 mm) and the fittings are invented illustrations.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ColdB1 { get; } =
    [
        C("fluid", "water"), Q("T", 9, "degC"), Q("T_line", 6, "degC"), Q("Q", 12, "m^3/h"), C("circuit", "closed (flow and return: the rises cancel, no static lift)"),
        Table("segments", CalcRow.Of(Txt("name", "chilled water flow and return"), ("L", new CalcCell(60, "m")), Mm("D", 51.6), Mm("OD", 54), Mm("eps", 0.0015), Num("K", 10), ("L_eq", new CalcCell(0, "m")), ("dz", new CalcCell(0, "m")), Txt("side", "discharge"))),
    ];

    /// <summary>The plant room air around the pipe: 28 °C dry-bulb at 60 % relative humidity, sea level. Its dry-bulb is the insulation step's ambient temperature and its dew point the temperature the surface must stay above.</summary>
    public static IReadOnlyList<CalcInputValue> ColdB2 { get; } =
    [
        Q("altitude", 0, "m"), Q("t_db", 28, "degC"), C("known", "relative humidity"), N("RH", 60), C("process", "none (state only)"),
    ];

    /// <summary>
    /// The insulation on that pipe: a closed-cell elastomeric-type cover of conductivity 0.035 W/(m·K) (an invented illustration, not a maker's value), still-air surface coefficient 8 W/(m²·K) as given, 1 K above the dew point,
    /// and 25 mm chosen. The pipe's outside diameter, the process temperature, the ambient temperature and the dew point arrive by link; the heat-gain limit (default 25 W/m) is left out, so the dew-point margin is the only criterion.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ColdB9 { get; } =
    [
        C("geometry", "pipe (cylinder)"), Q("lambda", 0.035, "W/(m*K)"), C("h_mode", "h_se as given"), Q("h_se", 8, "W/(m^2*K)"), Q("dew_margin", 1, "K"), Q("t_chosen", 25, "mm"),
    ];

    // ---- Pipework supports: a pipe anchor's force on a cantilever post and its base plate ------------------------------------

    /// <summary>
    /// A 219.1 × 6.3 mm steel line anchored 60 m apart, 10 to 150 °C, absorbed by a 9.5 m offset leg: the anchor takes the leg's force. E and α are the usual steel values
    /// (the sheet's own defaults, stated here); the 160 MPa allowable range and the 400 mm²/m² screening limit are INVENTED illustrations, not code values: the owner enters his own.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> PipeB8 { get; } =
    [
        Q("E", 210, "GPa"), Q("alpha", 12, "um/(m*K)"), Q("OD", 219.1, "mm"), Q("t_wall", 6.3, "mm"), Q("L", 60, "m"),
        C("temp_mode", "install and operating temperatures"), Q("T_install", 10, "degC"), Q("T_operate", 150, "degC"),
        C("layout", "offset leg (L- or Z-bend: the leg takes the whole movement)"), Q("H", 9.5, "m"), Q("Sa", 160, "MPa"), Q("K_scr", 400, "mm^2/m^2"),
    ];

    /// <summary>
    /// The anchor post: a 2.5 m cantilever column (node 1 fixed, node 2 the anchor), the 203 × 203 UC of the steel-support example. The first load row (case 1) is the horizontal force
    /// along the pipe at node 2, a 1 kN placeholder that the pipe-expansion step's anchor force replaces by cell link; the second (case 2) is the 6 kN weight of pipe, contents and
    /// bracket, downward. The ULS combination takes the thermal force at 1.5 and the weight at 1.35 (invented factors for this illustration; the owner's combination rules apply).
    /// The links depend on this numbering: node 1 the column base, node 2 the anchor; the first load row the thermal force.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> PipeS2 { get; } =
    [
        Table("nodes",
            CalcRow.Of(Num("id", 1), ("x", new CalcCell(0, "m")), ("y", new CalcCell(0, "m")), Txt("support", "fixed")),
            CalcRow.Of(Num("id", 2), ("x", new CalcCell(0, "m")), ("y", new CalcCell(2.5, "m")), Txt("support", "free"))),
        Table("elements",
            CalcRow.Of(Num("id", 1), Num("i", 1), Num("j", 2), ("A", new CalcCell(58.7, "cm^2")), ("I", new CalcCell(4568, "cm^4")), ("rel_i", new CalcCell(Flag: false)), ("rel_j", new CalcCell(Flag: false)))),
        Table("loads", FrameLoad(1, 2, 1, 0), FrameLoad(2, 2, 0, -6)),
        Table("combinations",
            CalcRow.Of(Txt("name", "ULS"), Num("case", 1), Num("factor", 1.5)), CalcRow.Of(Txt("name", "ULS"), Num("case", 2), Num("factor", 1.35))),
        Q("E", 210, "GPa"), new CalcInputValue("report", Text: "ULS"),
    ];

    /// <summary>
    /// The post's base plate: the steel-support example's 350 × 300 × 25 plate on C25/30 concrete, with the holding-down bolt data typed (M20 8.8 in a 25 mm plate: tension resistance 141.0 kN,
    /// stress area 244.79 mm², bearing 248.48 kN, f_ub 800, f_yb 640 MPa), because this chain has no bolt-check step; a test guards them against the connection step's own values. The anchorage
    /// is designed outside the program (Hilti PROFIS): the report is attached to this step as Evidence, and until it is the sheet says so.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> PipeS6 { get; } =
    [
        C("column", "I/H"), Q("h", 203.2, "mm"), Q("b", 203.6, "mm"), Q("t_f", 11, "mm"), Q("t_w", 7.2, "mm"),
        Q("B", 300, "mm"), Q("L", 350, "mm"), Q("t_p", 25, "mm"), N("alpha", 1), Q("x_b", 135, "mm"), Q("m", 28, "mm"), Q("l_eff", 150, "mm"), Q("e_min", 40, "mm"),
        Q("F_t_Rd", 141.0, "kN"), Q("A_s", 244.79, "mm^2"), Q("F_1_vb_Rd", 248.48, "kN"), Q("f_ub", 800, "MPa"), Q("f_yb", 640, "MPa"),
    ];

    // ---- Electronics enclosure: a power trace's loss, the board's heat, the box's temperature, a latch and a lid ------------------

    /// <summary>
    /// An 8 A supply trace on 2 oz external copper, 2 mm wide and 150 mm long, allowed a 30 K rise. Its board ambient is TYPED at 60 °C, the enclosure's design ceiling: the real
    /// ambient is the enclosure's internal temperature, which is downstream of this step (linking it back would be a cycle), so a test guards that the enclosure step stays below it.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ElxE7 { get; } =
    [
        Q("I", 8, "A"), Q("dT", 30, "K"), Q("T_amb", 60, "degC"), C("layer", "External layer"), N("Cu_oz", 2), Q("w_act", 2, "mm"), Q("L", 150, "mm"),
    ];

    /// <summary>
    /// The board's loads (24 V DC, fed from the single-phase mains through a 90 % efficient supply that sits INSIDE the enclosure). Row 0 is the power trace itself: its input power is a 1 W placeholder that the trace
    /// step's I²R loss replaces by cell link, all of it released as heat inside the enclosure. The gate drivers and logic and the display release all their input as heat; the power stage releases its loss
    /// only (the share is left blank, so 1 − η). The last row is the supply's own loss, which the sheet counts only if it is a row (E6's convention): 26.5 W = the 238.5 W the DC loads draw × (1/0.9 − 1), typed
    /// (a test guards it against the loads; it does not follow them automatically). The loads, efficiencies and the supply are invented illustrations.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ElxE6 { get; } =
    [
        Table("loads",
            DcLoad("Power trace loss (PCB)", 0.001, 1, 1, 1),
            DcLoad("Gate drivers and logic", 0.025, 0.9, 1, 1),
            CalcRow.Of(Txt("name", "Power stage"), Txt("supply", "DC"), ("P", new CalcCell(0.2, "kW")), Num("cosphi", 1), Num("eta", 0.96), Num("d", 1), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false))),
            DcLoad("Display and sensors", 0.012, 1, 1, 1),
            CalcRow.Of(Txt("name", "24 V supply loss"), Txt("supply", "AC single-phase"), ("P", new CalcCell(0.0265, "kW")), Num("cosphi", 1), Num("eta", 1), Num("heat", 1), Num("d", 1), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false)))),
        C("psu_bus", "AC single-phase"),
    ];

    private static CalcRow DcLoad(string name, double kw, double eta, double heat, double duty) =>
        CalcRow.Of(Txt("name", name), Txt("supply", "DC"), ("P", new CalcCell(kw, "kW")), Num("cosphi", 1), Num("eta", eta), Num("heat", heat), Num("d", duty), Num("k", 1), Num("inrush", 1), ("backed", new CalcCell(Flag: false)));

    /// <summary>
    /// A 500 × 600 × 200 mm wall-mounted enclosure (back against the wall) in a 35 °C room, limit 60 °C, SEALED when it was closed at 25 °C but fitted with a pressure-compensation breather that cracks at 1 kPa
    /// (an INVENTED breather rating, not a maker's data): the air warming would put 10.83 kPa across the walls, the breather limits it to 1 kPa, and that acts on the 500 × 600 front cover (0.3 m², typed; a test guards it
    /// against the cover plate's dimensions). The board's heat arrives by link as the additional load; the 4 W of terminals and wiring is the enclosure's own. It dissipates by itself (no fan, no cooling unit).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ElxE2 { get; } =
    [
        Table("loads", CalcRow.Of(Txt("name", "Terminals and wiring"), ("P", new CalcCell(4, "W")), Num("qty", 1), Num("duty", 1))),
        Q("W", 0.5, "m"), Q("H", 0.6, "m"), Q("D", 0.2, "m"),
        N("f_top", 1), N("f_front", 1), N("f_rear", 0), N("f_left", 1), N("f_right", 1), N("f_bottom", 0),
        Q("T_amb", 35, "degC"), Q("T_max", 60, "degC"), Q("T_seal", 25, "degC"), Q("dp_relief", 1, "kPa"), Q("A_cover", 0.3, "m^2"),
    ];

    /// <summary>
    /// The cover latch: a cantilever snap hook 12 mm long, 1.6 mm thick and 5 mm wide, deflected 1 mm; SIX identical latches around the cover (a typed count, an invented illustration) share the load on it, which arrives by link
    /// (a 1 N placeholder here). The secant modulus (1400 MPa) and the permissible strain (3 %) of the plastic are TYPED values
    /// for 60 °C, the enclosure's limit (<c>T_max</c>), an INVENTED illustration and not a maker's data: the temperature of use arrives by link, but the modulus and the permissible strain do not follow
    /// it, so they stay valid only while the enclosure stays at or below 60 °C, which the enclosure step's own check (natural dissipation holds <c>T_max</c>) guards at run time. The friction coefficient is a record, typed.
    /// IDEALISATION: the load is shared equally (F_req/n each). The cover's edge reactions peak mid-edge, so the latches nearest mid-edge carry more than that; equal sharing is an idealisation, so place the latches at the mid-edges or apply a load-share factor to F_req.
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ElxM8 { get; } =
    [
        C("mode", "Cantilever, constant section"), Q("l", 12, "mm"), Q("h", 1.6, "mm"), Q("b", 5, "mm"), Q("y", 1, "mm"),
        Q("alpha", 30, "deg"), Q("alpha_r", 70, "deg"), N("mu", 0.3), C("Es_source", "Typed secant modulus"), Q("Es", 1400, "MPa"),
        C("loading", "One-time (assembled once or rarely)"), N("eps_perm_once", 0.03), N("n_latch", 6),
    ];

    /// <summary>
    /// The front cover, the enclosure's largest face and so the panel that governs: a 600 × 500 × 4 mm aluminium plate (600 high, 500 wide), simply supported all round. Its pressure is the sealed enclosure's pressure difference limited by the
    /// breather (1 kPa), which arrives by link (a 1 kPa placeholder here). 4 mm passes; 3 mm would deflect (4/3)³ times as much, 2.03 mm against the 2 mm limit. The modulus (69 GPa), Poisson's ratio, density and limit stress (120 MPa) are
    /// generic aluminium-alloy values, TYPED for 60 °C, the enclosure's limit, and indicative only (not a maker's data).
    /// IDEALISATION: the plate is simply supported all round, whereas the real cover is held at six latches and a gasket; its edge reactions peak at mid-edge, so the latches nearest mid-edge carry more than F_req/n (see the latch step).
    /// </summary>
    public static IReadOnlyList<CalcInputValue> ElxS9 { get; } =
    [
        C("shape", "rectangular"), C("support", "simply supported"), Q("a", 600, "mm"), Q("b", 500, "mm"), Q("t", 4, "mm"),
        C("load_type", "uniform"), C("load_basis", "pressure q"), Q("q", 1, "kPa"),
        Q("E", 69, "GPa"), N("nu", 0.33), Q("rho", 2700, "kg/m^3"), Q("f_y", 120, "MPa"), N("gamma_M0", 1),
    ];
}
