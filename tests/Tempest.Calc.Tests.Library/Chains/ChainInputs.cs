using Tempest.Calc.BuildingServices;
using Tempest.Calc.Electrical;
using Tempest.Calc.Library;
using Tempest.Calc.Mechanical;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural;

namespace Tempest.Calc.Tests.Library.Chains;

/// <summary>
/// The inputs of every step of the five built-in chains, shared by the chain integration tests and the pack worked examples.
/// Project, drawing and specification references are illustrative (invented for the examples); every input carries a
/// provenance: the figures this file chooses cite the step's specification, and a template default left in place is
/// recorded as an assumption.
/// </summary>
internal static class ChainInputs
{
    /// <summary>The provenance of a template default the example does not override.</summary>
    public static CalcProvenance DefaultAssumption { get; } = CalcProvenance.Assumed("Sheet default value; no project figure given.");

    /// <summary>
    /// A step's inputs: the sheet's opening inputs less those <paramref name="drop"/> names (inputs a link supplies), the
    /// <paramref name="given"/> values over them, each given value citing <paramref name="basis"/> unless it carries its own
    /// provenance, and every remaining default recorded as an assumption.
    /// </summary>
    public static List<CalcInputValue> Step(CalcTemplate template, CalcProvenance basis, IEnumerable<string>? drop = null, params CalcInputValue[] given)
    {
        var dropped = (drop ?? []).ToHashSet(StringComparer.Ordinal);
        var set = new CalcInputSet(CalcTemplateHasher.NewSheetInputs(template).Values.Where(v => !dropped.Contains(v.Symbol)));
        var named = given.Select(v => v.Symbol).ToHashSet(StringComparer.Ordinal);

        foreach (var value in given)
            set.Set(value);

        return [.. set.Values.Select(v => v.Provenance is not null ? v : v with { Provenance = named.Contains(v.Symbol) ? basis : DefaultAssumption })];
    }

    private static CalcProvenance Spec(string reference) => CalcProvenance.Specification(reference);

    private static CalcProvenance Drg(string number) => CalcProvenance.Drawing(number, "B");

    // ---------------------------------------------------------------------------------------------------------------------
    // Drive train: a 30 kW geared motor driving a conveyor head shaft through a flange coupling.
    // ---------------------------------------------------------------------------------------------------------------------

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> DriveTrain()
    {
        var e1 = Spec("SPEC-CNV-07 §3 (conveyor duty)");
        var m4 = Drg("DRG-CNV-410");
        var m5 = Spec("SPEC-CNV-07 §6 (bearing schedule)");
        var m2 = Drg("DRG-CNV-411");
        var m3 = Drg("DRG-CNV-412");

        return new(StringComparer.Ordinal)
        {
            // A 4-pole motor through a 12.5:1 gearbox turns the drum at 120 rpm against a steady 1.8 kN·m; the run-up takes 3 s.
            ["e1"] = Step(new MotorSizingCalculator().Template, e1, null,
                CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechGearedRotary),
                CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyContinuous),
                CalcInputValue.Of("i", 12.5), CalcInputValue.Of("eta", 0.95),
                CalcInputValue.Quantity("J_mech", 0.002, "kg*m^2"), CalcInputValue.Quantity("J_L", 4, "kg*m^2"),
                CalcInputValue.Quantity("T_L", 1800, "N*m"), CalcInputValue.Quantity("n_L", 120, "rpm"), CalcInputValue.Quantity("t_start", 3, "s"),
                CalcInputValue.Quantity("J_motor", 0.12, "kg*m^2"), CalcInputValue.Quantity("T_rated", 200, "N*m"), CalcInputValue.Quantity("T_peak", 400, "N*m"),
                CalcInputValue.Quantity("n_max", 3000, "rpm")),

            // The gearbox input shaft: coupling at one end, a 60 mm pitch pinion at mid-span; T and n come from E1.
            ["m4"] = Step(new ShaftCalculator().Template, m4, ["T", "n"],
                ShaftCalculator.Segments((50, 35, 0), (250, 45, 0), (300, 35, 0)),
                CalcInputValue.Quantity("x_A", 25, "mm"), CalcInputValue.Quantity("x_B", 275, "mm"),
                ShaftCalculator.Loads((150, -0.92, 2.53, 3)),
                CalcInputValue.Quantity("x_T1", 0, "mm"), CalcInputValue.Quantity("x_T2", 150, "mm"),
                ShaftCalculator.Sections((50, 1.7, 1.5, 0.85, 0.9), (150, 1.7, 1.5, 0.85, 0.9)),
                CalcInputValue.Quantity("S_ut", 850, "MPa"), CalcInputValue.Quantity("S_y", 650, "MPa"), CalcInputValue.Quantity("E", 207, "GPa"),
                CalcInputValue.Choice("finish", ShaftCalculator.Ground)),

            // Bearing A: a deep-groove ball bearing; the radial load is the shaft's reaction and the speed the motor's.
            ["m5"] = Step(new BearingLifeCalculator().Template, m5, ["F_r", "n"],
                CalcInputValue.Choice("type", BearingLifeCalculator.Ball),
                CalcInputValue.Quantity("C", 25.5, "kN"), CalcInputValue.Quantity("C_0", 15.3, "kN"), CalcInputValue.Quantity("L_req", 20000, "h")),

            // The seat: a transition fit on the shaft's seat diameter.
            ["m2"] = Step(new Iso286FitCalculator().Template, m2, ["D"],
                CalcInputValue.Choice("hole_letter", "H"), CalcInputValue.Of("hole_grade", 7),
                CalcInputValue.Choice("shaft_letter", "k"), CalcInputValue.Of("shaft_grade", 6)),

            // The flange coupling: four M8 8.8 bolts on an 80 mm circle carry the motor's peak torque by shear.
            ["m3"] = Step(new BoltGroupCalculator().Template, m3, ["T_z"],
                Bolts((40, 0), (0, 40), (-40, 0), (0, -40)),
                CalcInputValue.Quantity("F_x", 0, "kN"), CalcInputValue.Quantity("F_y", 0, "kN"), CalcInputValue.Quantity("e_x", 0, "mm"), CalcInputValue.Quantity("e_y", 0, "mm"),
                CalcInputValue.Quantity("N", 0, "kN"), CalcInputValue.Quantity("M_x", 0, "kN*m"), CalcInputValue.Quantity("M_y", 0, "kN*m"),
                CalcInputValue.Quantity("V_R", 14.0, "kN"), CalcInputValue.Quantity("T_R", 21.0, "kN")),
        };
    }

    /// <summary>A bolt pattern of M8 bolts (stress area 36.6 mm²) at the given (x, y) positions in mm.</summary>
    private static CalcInputValue Bolts(params (double X, double Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(p.X, "mm")), ("y", new CalcCell(p.Y, "mm")), ("A", new CalcCell(36.6, "mm^2")))).ToList());

    // ---------------------------------------------------------------------------------------------------------------------
    // Motor circuit: a servo axis, its panel and its cable (the E4 tables are the synthetic ones).
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The circuit's inputs; E4 is given the two tables as pins to the records it is passed (null: the tables as plain synthetic inputs).</summary>
    public static Dictionary<string, IReadOnlyList<CalcInputValue>> MotorCircuit(CalcProvenance? ampacityPin = null, CalcProvenance? ratingsPin = null)
    {
        var duty = Spec("SPEC-LINE-12 §2 (axis duty cycle)");
        var panel = Spec("SPEC-LINE-12 §4 (panel schedule)");
        var cable = Spec("SPEC-LINE-12 §5 (cabling)");
        var earth = Spec("SPEC-LINE-12 §6 (earthing and protection)");
        var board = Drg("DRG-LINE-120");

        return new(StringComparer.Ordinal)
        {
            // A ball-screw axis: 50 kg slide, 10 mm lead, 0.5 m in 0.5 s.
            ["e1"] = Step(new MotorSizingCalculator().Template, duty, null,
                CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechScrew), CalcInputValue.Quantity("p_screw", 10, "mm"),
                CalcInputValue.Quantity("m", 50, "kg"), CalcInputValue.Of("mu", 0.1), CalcInputValue.Of("s", 0.5), CalcInputValue.Quantity("t_move", 0.5, "s")),

            // The panel carries the axis (linked) and its controls.
            ["e6"] = Step(new LoadBudgetCalculator().Template, panel, ["P_add"],
                LoadBudgetCalculator.Loads(("24 V controls and I/O", LoadBudgetCalculator.Dc, 0.18, 1, 0.9, 1, 1, 1, false), ("Panel fan", LoadBudgetCalculator.Ac1, 0.04, 0.9, 0.7, 1, 1, 1, false)),
                CalcInputValue.Of("cosphi_add", 0.85), CalcInputValue.Of("eta_add", 0.97)),

            // The cable and its device; the design current comes from E6.
            ["e4"] = [.. Step(new ConductorSizingCalculator().Template, cable, ["I_b_in"],
                CalcInputValue.Quantity("L", 25, "m"), CalcInputValue.Quantity("U", 400, "V"), CalcInputValue.Choice("insulation", ConductorSizingCalculator.Pvc),
                CalcInputValue.Choice("method", "C", cable), CalcInputValue.Choice("device", ConductorSizingCalculator.McbC)),
                ConductorSizingCalculator.SyntheticAmpacity() with { Provenance = ampacityPin ?? Spec("SYNTHETIC test capacities (not a regulation table)") },
                ConductorSizingCalculator.TypicalRatings() with { Provenance = ratingsPin ?? Spec("SYNTHETIC test device ratings (not a manufacturer table)") }],

            ["e3"] = Step(new VoltageDropCalculator().Template, cable, ["I", "L", "A_c"],
                CalcInputValue.Quantity("V_n", 400, "V"), CalcInputValue.Of("cos_phi", 0.85), CalcInputValue.Quantity("T_amb", 30, "degC")),

            ["e5"] = Step(new FaultProtectionCalculator().Template, earth, ["S_line", "L", "I_n"],
                CalcInputValue.Quantity("U_0", 230, "V"), CalcInputValue.Quantity("Z_e", 0.35, "ohm"), CalcInputValue.Quantity("S_cpc", 2.5, "mm^2"), CalcInputValue.Quantity("t_req", 0.4, "s")),

            // The enclosure: 1000 × 1000 × 300 against a wall.
            ["e2"] = Step(new EnclosureHeatCalculator().Template, board, ["P_add"],
                EnclosureHeatCalculator.Loads(("Servo drive", 60, 1, 1), ("Contactors, terminals and busbars", 20, 1, 1)),
                CalcInputValue.Quantity("W", 1.0, "m"), CalcInputValue.Quantity("H", 1.0, "m"), CalcInputValue.Quantity("D", 0.3, "m"),
                CalcInputValue.Quantity("T_amb", 35, "degC"), CalcInputValue.Quantity("T_max", 55, "degC")),
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Wet heating: two rooms to a circulator motor.
    // ---------------------------------------------------------------------------------------------------------------------

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> WetHeating()
    {
        var spec = Spec("SPEC-HTG-01 §3");
        var b4 = new RoomHeatLossCalculator().Template;
        var b6 = new SealedSystemCalculator().Template;
        var b1 = new PipeFlowCalculator().Template;
        var e1 = new MotorSizingCalculator().Template;

        return new(StringComparer.Ordinal)
        {
            ["b4a"] = [.. b4.TestCases[0].Inputs.Select(v => v with { Provenance = spec })],
            ["b4b"] = [.. b4.TestCases[1].Inputs.Select(v => v with { Provenance = spec })],

            // B6 is given its two rooms: instantiating never creates rows. Φ is a placeholder the links replace.
            ["b6"] = Step(b6, spec, ["rooms"], SealedSystemCalculator.Rooms(("Room A", 1, 0, 4), ("Room B", 1, 0, 4))),

            // B1: heat load, ΔT and the mean water temperature come from B6, so no design flow or temperature is given and the
            // specific heat of water is the sheet's assumed constant; the pump runs at 2800 rpm.
            ["b1"] = Step(b1, spec, ["Q", "T", "Phi_load", "dT_load", "cp_load"],
                CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater),
                PipeFlowCalculator.Segments(("primary flow and return", 40, 28, 0.045, 12, 0, 3, "discharge")),
                CalcInputValue.Quantity("n_pump", 2800, "rpm")),

            // E1: a circulator on a direct drive in continuous duty; B1's shaft torque is its load torque and B1's shaft speed its
            // load speed, so only the run-up time and the inertia are given.
            ["e1"] = Step(e1, spec, null,
                CalcInputValue.Choice("mechanism", MotorSizingCalculator.MechDirectRotary),
                CalcInputValue.Choice("duty_mode", MotorSizingCalculator.DutyContinuous),
                CalcInputValue.Quantity("J_L", 0.002, "kg*m^2"), CalcInputValue.Quantity("J_mech", 0, "kg*m^2"), CalcInputValue.Quantity("t_start", 5, "s")),
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Cooling: a drive panel's heat to a water-cooled exchanger and its cooling-water circuit.
    // ---------------------------------------------------------------------------------------------------------------------

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> Cooling()
    {
        var panel = Spec("SPEC-MCC-03 §4 (panel schedule)");
        var board = Drg("DRG-MCC-210");
        var plant = Spec("SPEC-MCC-03 §7 (cooling water)");
        var pipe = Drg("DRG-MCC-215");

        return new(StringComparer.Ordinal)
        {
            // Two main drives, four servo drives, the 24 V supplies and the controls: what the panel dissipates is Σ (1 − η) P.
            ["e6"] = Step(new LoadBudgetCalculator().Template, panel, ["P_add"],
                LoadBudgetCalculator.Loads(
                    ("Extruder drive", LoadBudgetCalculator.Ac3, 55, 0.95, 0.97, 1, 1, 1, false),
                    ("Pump drive", LoadBudgetCalculator.Ac3, 30, 0.95, 0.975, 1, 1, 1, false),
                    ("Servo drives (4)", LoadBudgetCalculator.Ac3, 30, 0.9, 0.96, 0.8, 1, 1, false),
                    ("24 V supplies", LoadBudgetCalculator.Ac3, 1.5, 0.9, 0.9, 1, 1, 1, false)),
                CalcInputValue.Of("cosphi_add", 0.85)),

            // A floor-standing enclosure 1200 × 2000 × 600 with its back to a wall and a neighbour on the right.
            ["e2"] = Step(new EnclosureHeatCalculator().Template, board, null,
                EnclosureHeatCalculator.Loads(("Busbars, contactors and terminals", 150, 1, 1)),
                CalcInputValue.Quantity("W", 1.2, "m"), CalcInputValue.Quantity("H", 2.0, "m"), CalcInputValue.Quantity("D", 0.6, "m"),
                CalcInputValue.Of("f_top", 1), CalcInputValue.Of("f_front", 1), CalcInputValue.Of("f_rear", 0), CalcInputValue.Of("f_left", 1), CalcInputValue.Of("f_right", 0), CalcInputValue.Of("f_bottom", 0),
                CalcInputValue.Quantity("T_amb", 35, "degC"), CalcInputValue.Quantity("T_max", 45, "degC")),

            // The exchanger is sized for the duty E2 hands over; the enclosure's closed water loop (38 °C in) gives its heat to the
            // plant cooling water (22 °C in, 0.25 kg/s), counterflow.
            ["b5"] = Step(new HeatExchangerCalculator().Template, plant, ["Q_req", "A"],
                CalcInputValue.Choice("arrangement", HeatExchangerCalculator.Counterflow), CalcInputValue.Choice("mode", HeatExchangerCalculator.Sizing),
                CalcInputValue.Quantity("m_h", 0.3, "kg/s"), CalcInputValue.Quantity("T_h_in", 38, "degC"),
                CalcInputValue.Quantity("m_c", 0.25, "kg/s"), CalcInputValue.Quantity("T_c_in", 22, "degC"),
                CalcInputValue.Quantity("U", 1000, "W/(m^2*K)"), CalcInputValue.Quantity("R_f", 0.0001, "m^2*K/W")),

            // B1: the cooling-water circuit. The duty, its temperature rise and the mean temperature come from B5; cp is the cold
            // side's, so B1's flow equals B5's cold-side mass flow over the water density. The pump runs at 2800 rpm.
            ["b1"] = Step(new PipeFlowCalculator().Template, pipe, ["Q", "T", "Phi_load", "dT_load"],
                CalcInputValue.Choice("fluid", PipeFlowCalculator.FluidWater),
                CalcInputValue.Quantity("cp_load", 4180, "J/(kg*K)"),
                PipeFlowCalculator.Segments(("cooling water supply and return", 30, 25, 0.045, 12, 0, 3, "discharge")),
                CalcInputValue.Quantity("n_pump", 2800, "rpm")),
        };
    }

    // ---------------------------------------------------------------------------------------------------------------------
    // Steel support: an equipment bracket on a cantilever column.
    // ---------------------------------------------------------------------------------------------------------------------

    /// <summary>The characteristic vertical actions on the bracket, kN (downward negative, as S5 reads a signed effect).</summary>
    public const double DeadLoad = 12;

    public const double ImposedLoad = 8;

    /// <summary>The ULS horizontal action at the column top, kN.</summary>
    public const double HorizontalLoad = 5;

    public static Dictionary<string, IReadOnlyList<CalcInputValue>> SteelSupport()
    {
        var actions = Spec("SPEC-STR-09 §2 (actions)");
        var frame = Drg("DRG-STR-310");
        var section = Spec("SPEC-STR-09 §3 (members)");
        var joint = Drg("DRG-STR-312");
        var baseplate = Drg("DRG-STR-315");

        return new(StringComparer.Ordinal)
        {
            // The equipment's dead load and the maintenance load, both downward (negative effects).
            ["s5"] = Step(new LoadCombinationsCalculator().Template, actions, null,
                LoadCombinationsCalculator.Actions(("Equipment dead load", LoadCombinationsCalculator.Permanent, -DeadLoad), ("Maintenance load", "E storage", -ImposedLoad)),
                CalcInputValue.Choice("unit", "kN"), CalcInputValue.Choice("method", LoadCombinationsCalculator.Method610)),

            // A 3 m cantilever column (fixed at node 1) with a 1 m bracket arm. Row 0 of the load table is the equipment load on the
            // bracket tip, a placeholder S5's ULS effect replaces; row 1 is the horizontal ULS action at the column top.
            ["s2"] = Step(new FrameCalculator().Template, frame, ["loads"],
                FrameCalculator.Nodes((1, 0, 0, FrameCalculator.SupportFixed), (2, 0, 3, FrameCalculator.SupportFree), (3, 1, 3, FrameCalculator.SupportFree)),
                FrameCalculator.Elements((1, 1, 2, 58.7, 4568), (2, 2, 3, 58.7, 4568)),
                FrameCalculator.Loads((1, "nodal", 3, 0, 0, 0, 0, 0, -1, 0), (2, "nodal", 2, 0, 0, 0, 0, HorizontalLoad, 0, 0)),
                FrameCalculator.Combinations(("ULS", 1, 1.0), ("ULS", 2, 1.0)),
                CalcInputValue.Quantity("E", 210, "GPa"), new CalcInputValue("report", Text: "ULS")),

            // The column: 203 × 203 × 46 UC in S355, fixed base and free top (buckling length 2 × 3 m).
            ["s3"] = Step(new MemberChecksCalculator().Template, section, ["N_Ed", "M_y_Ed", "V_Ed"],
                CalcInputValue.Quantity("M_z_Ed", 0, "kN*m"),
                CalcInputValue.Quantity("f_y", 355, "MPa"), CalcInputValue.Quantity("L_cr_y", 6, "m"), CalcInputValue.Quantity("L_cr_z", 6, "m")),

            // The bracket's bolted connection: M20 8.8 in a 20 mm S275 plate, the same bolts as the holding-down bolts.
            ["s4"] = Step(new ConnectionCalculator().Template, joint, ["V_Ed"],
                CalcInputValue.Choice("grade", "8.8"), CalcInputValue.Choice("size", "M20"), CalcInputValue.Choice("steel", "S275"),
                CalcInputValue.Quantity("t", 20, "mm"), CalcInputValue.Quantity("p_1", 70, "mm"), CalcInputValue.Quantity("p_2", 70, "mm"),
                CalcInputValue.Quantity("F_v_Ed", 8, "kN"), CalcInputValue.Quantity("F_t_Ed", 0, "kN"),
                CalcInputValue.Quantity("A_nt", 1200, "mm^2"), CalcInputValue.Quantity("A_nv", 2400, "mm^2")),

            // The base plate: 350 × 300 × 25 on C25/30 concrete, two M20 8.8 holding-down bolts each side.
            ["s6"] = Step(new BasePlateCalculator().Template, baseplate, ["N_Ed", "M_Ed", "V_Ed", "F_t_Rd", "A_s", "F_1_vb_Rd"],
                CalcInputValue.Choice("column", BasePlateCalculator.ColumnIH),
                CalcInputValue.Quantity("h", 203.2, "mm"), CalcInputValue.Quantity("b", 203.6, "mm"), CalcInputValue.Quantity("t_f", 11, "mm"), CalcInputValue.Quantity("t_w", 7.2, "mm"),
                CalcInputValue.Quantity("B", 300, "mm"), CalcInputValue.Quantity("L", 350, "mm"), CalcInputValue.Quantity("t_p", 25, "mm"),
                CalcInputValue.Quantity("x_b", 135, "mm")),
        };
    }
}
