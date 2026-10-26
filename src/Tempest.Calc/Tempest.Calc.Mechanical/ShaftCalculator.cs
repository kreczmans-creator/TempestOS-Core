using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;
using Tempest.Calc.Structural.Frame;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// M4 — stepped shaft on two bearings: bending in two planes by the S2 frame solver, fatigue (DE-Goodman, Marin factors
/// by Shigley's published equations) and yield at user-marked sections, slopes at the bearings and deflection at the
/// loads, first lateral critical speed (Rayleigh, Dunkerley lower bound), and parallel keys.
/// </summary>
public sealed class ShaftCalculator : ICalcCalculator
{
    public const string Id = "mech.shaft";
    public const string Ground = "ground";
    public const string Machined = "machined or cold-drawn";
    public const string HotRolled = "hot-rolled";
    public const string Forged = "as-forged";
    public const int Divisions = 4;
    public const double Gravity = 9.80665;

    private static readonly IReadOnlyList<CalcColumnSpec> SegmentColumns =
    [
        new("x_end", "Segment ends at", CalcInputKind.Quantity, "mm", Min: 1, Max: 3000, HardMin: 1, HardMax: 3000),
        new("d", "Diameter", CalcInputKind.Quantity, "mm", Min: 5, Max: 300, HardMin: 5, HardMax: 300),
        new("d_i", "Bore (hollow)", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 295, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> LoadColumns =
    [
        new("x", "Position", CalcInputKind.Quantity, "mm", Min: 0, Max: 3000, HardMin: 0, HardMax: 3000),
        new("F_y", "Force, vertical plane (up +)", CalcInputKind.Quantity, "kN", HardMin: -1e4, HardMax: 1e4, IsOptional: true),
        new("F_z", "Force, horizontal plane", CalcInputKind.Quantity, "kN", HardMin: -1e4, HardMax: 1e4, IsOptional: true),
        new("m", "Mass carried (gear, pulley)", CalcInputKind.Quantity, "kg", HardMin: 0, HardMax: 1e5, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> SectionColumns =
    [
        new("x", "Position", CalcInputKind.Quantity, "mm", Min: 0, Max: 3000, HardMin: 0, HardMax: 3000),
        new("K_t", "K_t, bending", CalcInputKind.Number, HardMin: 1, HardMax: 10),
        new("K_ts", "K_ts, torsion", CalcInputKind.Number, HardMin: 1, HardMax: 10),
        new("q", "Notch sensitivity, bending", CalcInputKind.Number, HardMin: 0, HardMax: 1),
        new("q_s", "Notch sensitivity, torsion", CalcInputKind.Number, HardMin: 0, HardMax: 1),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.3.1", CalcDiscipline.Mechanical, "Shaft: static, fatigue, keys, critical speed",
        "A stepped (optionally hollow) shaft on two bearings carrying radial loads in two planes and a torque with a steady (mean) part and an optional alternating part: bending moment, slope and deflection along it, the fatigue and yield safety factors at the sections the user marks, slopes at the bearings, deflection at the loads, the first lateral critical speed and parallel keys.",
        "Shaft bending moment: the shaft as an S2 beam (pin at A, roller at B) in each plane; M = √(M_y² + M_z²)\n" +
        "Stress at a section: σ_a = K_f·32M_a·d/(π(d⁴ − d_i⁴)), τ_m = K_fs·16T_m·d/(π(d⁴ − d_i⁴)), τ_a = K_fs·16T_a·d/(π(d⁴ − d_i⁴)); K_f = 1 + q(K_t − 1)\n" +
        "Endurance limit: S_e = k_a·k_b·k_c·k_d·k_e·k_f·S_e1, S_e1 (the rotating-beam limit S_e′) = 0.5·S_ut (S_ut ≤ 1400 MPa, else 700 MPa); k_a = a·S_ut^b, k_b = 1.24d^−0.107 or 1.51d^−0.157, k_e = 1 − 0.08·z_R\n" +
        "Fatigue (DE-Goodman, bending fully reversed, torque T_m ± T_a): 1/n_f = √(σ_a² + 3τ_a²)/S_e + √3·τ_m/S_ut; yield n_y = S_y/√((σ_a)² + 3τ_pk²), τ_pk from the larger of T_m + T_a and the peak torque T_pk when given\n" +
        "Critical speed: Rayleigh ω = √(g·Σm·y/Σm·y²), y signed (an overhung mass lifts the span, which lowers the quotient); Dunkerley 1/ω² = Σ m_i·δ_ii (lower bound, used for the ratio to the operating speed)\n" +
        "Weight loads (with w_masses): F_y,i = −m_i·g at each node, m_i the carried mass plus the shaft's own mass split to the node; without it the loads table is the whole load\n" +
        "Parallel key, on the peak torque T_k = the larger of T_m + T_a and T_pk: τ = 2T_k/(d·w·L), σ = 4T_k/(d·h·L); required length L = max(2T_k·n_target/(d·w·0.577·S_y,key), 4T_k·n_target/(d·h·S_y,min)), S_y,min the lowest of the key, the shaft and (when entered) the hub; a shoulder is checked on both sides and the lower n_f governs",
        [
            new CalcInputSpec("segments", "Shaft segments (left to right)", CalcInputKind.Table, Columns: SegmentColumns, MaxRows: 20, Group: "Geometry"),
            new CalcInputSpec("x_A", "Bearing A position", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 3000, HardMin: 0, HardMax: 3000, Group: "Geometry"),
            new CalcInputSpec("x_B", "Bearing B position", CalcInputKind.Quantity, "mm", Default: 400, Min: 1, Max: 3000, HardMin: 1, HardMax: 3000, Group: "Geometry"),
            new CalcInputSpec("loads", "Radial loads and masses (a carried mass adds no weight unless w_masses is on: enter its weight as a force in F_y)", CalcInputKind.Table, Columns: LoadColumns, MaxRows: 20, Group: "Loads"),
            new CalcInputSpec("w_masses", "Add the weight of the carried masses and of the shaft itself as downward vertical loads (off: the loads table is the whole load; the masses then enter the critical speed only)", CalcInputKind.Boolean, Default: 0, Group: "Loads"),
            new CalcInputSpec("T", "Torque transmitted, steady or mean part T_m", CalcInputKind.Quantity, "kN*m", Default: 0, Min: 0, Max: 50, HardMin: 0, HardMax: 50, Group: "Loads"),
            new CalcInputSpec("T_a", "Alternating torque amplitude T_a about the mean (blank: 0, a steady torque; a reversing or cyclic drive needs it)", CalcInputKind.Quantity, "kN*m", Min: 0, Max: 50, HardMin: 0, HardMax: 50, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("T_pk", "Peak torque for the yield check and the key (blank: T + T_a)", CalcInputKind.Quantity, "kN*m", Min: 0, Max: 50, HardMin: 0, HardMax: 50, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("x_T1", "Torque enters at", CalcInputKind.Quantity, "mm", Default: 0, Min: 0, Max: 3000, HardMin: 0, HardMax: 3000, Group: "Loads"),
            new CalcInputSpec("x_T2", "Torque leaves at (blank: the shaft length, so every section beyond x_T1 carries the torque)", CalcInputKind.Quantity, "mm", Min: 0, Max: 3000, HardMin: 0, HardMax: 3000, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("n", "Operating speed", CalcInputKind.Quantity, "rpm", Default: 1500, Min: 0, Max: 30000, HardMin: 0, HardMax: 30000, Group: "Loads"),
            new CalcInputSpec("sections", "Sections to check", CalcInputKind.Table, Columns: SectionColumns, MaxRows: 20, Group: "Sections"),
            new CalcInputSpec("S_ut", "Ultimate tensile strength", CalcInputKind.Quantity, "MPa", Default: 600, Min: 300, Max: 2000, HardMin: 300, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("S_y", "Yield strength", CalcInputKind.Quantity, "MPa", Default: 450, Min: 150, Max: 1900, HardMin: 100, HardMax: 2000, Group: "Material"),
            new CalcInputSpec("E", "Young's modulus", CalcInputKind.Quantity, "GPa", Default: 207, Min: 50, Max: 250, HardMin: 10, HardMax: 500, Group: "Material"),
            new CalcInputSpec("rho", "Density (shaft self-mass in the critical speed)", CalcInputKind.Quantity, "kg/m^3", Default: 7850, Min: 0, Max: 20000, HardMin: 0, HardMax: 20000, Group: "Material"),
            new CalcInputSpec("finish", "Surface finish", CalcInputKind.Choice, Choices: [Ground, Machined, HotRolled, Forged], DefaultChoice: Machined, Group: "Fatigue"),
            new CalcInputSpec("R", "Reliability", CalcInputKind.Quantity, "%", Default: 99, Min: 50, Max: 99.9, HardMin: 50, HardMax: 99.9, Group: "Fatigue"),
            new CalcInputSpec("k_d", "Temperature factor k_d", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 1.1, HardMin: 0.1, HardMax: 1.5, Group: "Fatigue"),
            new CalcInputSpec("k_a", "Surface factor override (blank: k_a = a·S_ut^b, Shigley 10th ed. Table 6-2)", CalcInputKind.Number, Min: 0.1, Max: 1.5, HardMin: 0.05, HardMax: 2, IsOptional: true, Group: "Fatigue"),
            new CalcInputSpec("k_f", "Miscellaneous factor k_f", CalcInputKind.Number, Default: 1, Min: 0.3, Max: 1.5, HardMin: 0.1, HardMax: 2, Group: "Fatigue"),
            new CalcInputSpec("n_target", "Target safety factor", CalcInputKind.Number, Default: 1.5, Min: 1.2, Max: 4, HardMin: 1.2, HardMax: 4, Group: "Requirements"),
            new CalcInputSpec("theta_max", "Slope limit at the bearings", CalcInputKind.Quantity, "rad", Default: 0.001, Min: 1e-5, Max: 0.1, HardMin: 1e-6, HardMax: 0.5, Group: "Requirements"),
            new CalcInputSpec("y_max", "Deflection limit at the loads", CalcInputKind.Quantity, "mm", Default: 0.13, Min: 0.001, Max: 10, HardMin: 1e-4, HardMax: 100, Group: "Requirements"),
            new CalcInputSpec("x_key", "Key position", CalcInputKind.Quantity, "mm", Min: 0, Max: 3000, HardMin: 0, HardMax: 3000, IsOptional: true, Group: "Key"),
            new CalcInputSpec("w", "Key width", CalcInputKind.Quantity, "mm", Min: 1, Max: 100, HardMin: 0.5, HardMax: 200, IsOptional: true, Group: "Key"),
            new CalcInputSpec("h", "Key height", CalcInputKind.Quantity, "mm", Min: 1, Max: 100, HardMin: 0.5, HardMax: 200, IsOptional: true, Group: "Key"),
            new CalcInputSpec("L_key", "Key length", CalcInputKind.Quantity, "mm", Min: 1, Max: 500, HardMin: 0.5, HardMax: 2000, IsOptional: true, Group: "Key"),
            new CalcInputSpec("S_y_key", "Key yield strength", CalcInputKind.Quantity, "MPa", Min: 150, Max: 1500, HardMin: 50, HardMax: 2000, IsOptional: true, Group: "Key"),
            new CalcInputSpec("S_y_hub", "Hub yield strength (the key bearing is checked against the weakest of key, shaft and hub; blank: the hub is not checked)", CalcInputKind.Quantity, "MPa", Min: 50, Max: 2000, HardMin: 20, HardMax: 2500, IsOptional: true, Group: "Key"),
        ],
        [
            new CalcAssumption("Linear elastic, two simple bearing supports (pin at A, roller at B); bending fully reversed by rotation; torque a steady part T_m plus an alternating part T_a about it (fully alternating when T_m = 0), both acting between the torque-in and torque-out positions.", "Shigley 10th ed. §7-4."),
            new CalcAssumption("No torsional vibration; only the first lateral mode, by Rayleigh with Dunkerley as a lower bound.", "Shigley 10th ed. §7-6."),
            new CalcAssumption("K_t and K_ts come from user records or published fit equations; no stress-concentration chart is reproduced.", "No stress-concentration chart is shipped; the factors are entered or derived."),
            new CalcAssumption("At a shoulder both sides are checked, each on its own diameter and bore, and the lower fatigue safety factor governs; k_c = 1 because the stresses are combined by von Mises.", "Shigley 10th ed. §6-9, §7-4."),
            new CalcAssumption("The loads table is the whole load: a carried mass adds no weight to the reactions, the moment or the deflection, and neither does the shaft's own weight, unless w_masses is on (which adds m·g at the nodes in the vertical plane).", "Stated limitation; w_masses adds the weights."),
            new CalcAssumption("Loads, sections and steps closer together than 1/200 of the shaft length share one node.", "Keeps the beam model well conditioned."),
        ],
        [
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "10th ed., Ch. 6 (§6-9, Table 6-2 Marin surface factors), Ch. 7 (§7-4 to §7-7)", "Fatigue, shaft stresses, deflection, critical speeds, keys"),
            new CalcClauseRef("Mott, Machine Elements in Mechanical Design", "6th ed., Ch. 12", "Shaft design"),
        ],
        [
            new CalcApplicabilityRule("geometry", "Segments must run left to right, bearings and loads must lie on the shaft, and the bearings must be at least 1/200 of the shaft length apart.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("critical-speed", "The first critical speed (the Dunkerley lower bound) is less than twice the operating speed.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("critical-speed-skipped", "No critical speed is found when no mass deflects (ρ = 0 and every carried mass sits on a bearing).", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("mass-weight", "A carried mass is entered and w_masses is off: its weight is not in the reactions, the moment or the deflection.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("torque-span", "A torque is transmitted and a section or the key lies outside x_T1 … x_T2: it carries no torque.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("key-hub", "A key check without a hub strength: the key bearing is checked against the key and the shaft only.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("key", "A key check needs its position, width, height and length.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("size-factor", "Diameter above 254 mm: k_b is held at its 254 mm value.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("strength", "Yield above ultimate strength.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-simply-supported", "Uniform shaft, central load and mass: slope, deflection, critical speed",
                new CalcReferenceSource("Analytic (beam formulas; single-mass Rayleigh)", null, "d = 30 mm, L = 400 mm, E = 207 GPa, P = 2 kN at mid-span: M = PL/4 = 200 N·m; θ_A = PL²/(16EI) = 2.42999e-3 rad; y = PL³/(48EI) = 0.323999 mm; 20 kg at mid-span, massless shaft: n_c = (60/2π)·√(48EI/(L³m)) = 5305.17 rpm"),
                [Segments((400, 30, 0)), Q("x_A", 0, "mm"), Q("x_B", 400, "mm"), Loads((200, -2, 0, 20)), Q("rho", 0, "kg/m^3"), Sections((200, 1, 1, 0, 0))],
                [new CalcExpectedValue("R_A", 1, "kN", 1e-6), new CalcExpectedValue("M_max", 0.2, "kN*m", 1e-6), new CalcExpectedValue("theta_A", 2.429991e-3, "rad", 1e-5), new CalcExpectedValue("y_load", 0.3239989, "mm", 1e-5), new CalcExpectedValue("n_c", 5305.174, "rpm", 1e-5), new CalcExpectedValue("n_c_D", 5305.174, "rpm", 1e-5)]),
            new CalcTestCase("ka-key", "Parallel key, 124.3 N·m on a 28 mm shaft",
                new CalcReferenceSource("Analytic (key shear and bearing)", null, "w = 8, h = 7, L = 30 mm: τ = 2T/(d w L) = 36.994 MPa; σ = 4T/(d h L) = 84.558 MPa"),
                [Segments((400, 28, 0)), Loads((200, -1.424, 0, 0)), Q("T", 0.1243, "kN*m"), Sections((200, 1, 1, 0, 0)), Q("x_key", 100, "mm"), Q("w", 8, "mm"), Q("h", 7, "mm"), Q("L_key", 30, "mm"), Q("S_y_key", 400, "MPa")],
                [new CalcExpectedValue("tau_key", 36.99405, "MPa", 1e-6), new CalcExpectedValue("sigma_key", 84.55782, "MPa", 1e-6)]),
            new CalcTestCase("ka-shigley-7-1", "Shoulder fatigue: 1260 lbf·in reversed bending, 1100 lbf·in steady torque, d = 1.100 in (Shigley Ch. 7 example)",
                new CalcReferenceSource("Budynas & Nisbett, Shigley's Mechanical Engineering Design", "10th ed., Ch. 7, Example 7-1", "Data of the example converted to SI exactly: M 1260 lbf·in = 142.361 N·m, T 1100 lbf·in = 124.283 N·m, d 1.100 in = 27.94 mm, S_ut 105 kpsi = 723.949 MPa, S_y 82 kpsi = 565.370 MPa; machined, R 0.99, K_t 1.68, K_ts 1.42, q 0.85, q_s 0.92; computed here (python3, independent): k_a 0.78769, k_b 0.86831, k_e 0.81389, S_e 201.50 MPa (29.23 kpsi), σ_a 104.91 MPa, τ_m 40.23 MPa, n_f (DE-Goodman) 1.621, n_y 4.489. The book's printed answers are to be compared by the owner against their copy; K_t, K_ts, q and q_s are those of the earlier version of this case and are equally to be checked"),
                [Segments((400, 27.94, 0)), Loads((200, -1.4236088, 0, 0)), Q("T", 0.1242833119, "kN*m"), Q("x_T1", 0, "mm"), Q("x_T2", 400, "mm"), Sections((200, 1.68, 1.42, 0.85, 0.92)), Q("S_ut", 723.949485, "MPa"), Q("S_y", 565.370074, "MPa"), CalcInputValue.Choice("finish", Machined), Q("R", 99, "%")],
                [new CalcExpectedValue("S_e", 201.4997, "MPa", 1e-5), new CalcExpectedValue("n_f_min", 1.620988, "", 1e-5), new CalcExpectedValue("n_y_min", 4.488973, "", 1e-5)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Drives",
        OutputPorts:
        [
            new CalcOutputPort("R_A", "kN", "Radial reaction at bearing A (→ M5 F_r)"),
            new CalcOutputPort("R_B", "kN", "Radial reaction at bearing B (→ M5 F_r)"),
            new CalcOutputPort("d_A", "mm", "Seat diameter at A (→ M2)"),
            new CalcOutputPort("d_B", "mm", "Seat diameter at B (→ M2)"),
            new CalcOutputPort("n_f_min", "", "Lowest fatigue safety factor"),
            new CalcOutputPort("n_c", "rpm", "First critical speed"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("shaft.material", "Shaft material", ["S_ut", "S_y", "E"], "From the material standard or certificate."),
            new CalcRecordKind("shaft.marin", "Marin surface factor", ["k_a"], "From the edition of the fatigue reference in use (Shigley 11th ed. Table 6-2 revised the coefficients), with its source."),
            new CalcRecordKind("shaft.limits", "Slope and deflection limits", ["theta_max", "y_max"], "From the bearing and gear manufacturers' data."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>Segments from (x_end mm, d mm, bore mm) rows.</summary>
    public static CalcInputValue Segments(params (double XEnd, double D, double Di)[] rows) =>
        CalcInputValue.Table("segments", rows.Select(r => CalcRow.Of(("x_end", new CalcCell(r.XEnd, "mm")), ("d", new CalcCell(r.D, "mm")), ("d_i", new CalcCell(r.Di, "mm")))).ToList());

    /// <summary>Loads from (x mm, F_y kN, F_z kN, m kg) rows.</summary>
    public static CalcInputValue Loads(params (double X, double Fy, double Fz, double M)[] rows) =>
        CalcInputValue.Table("loads", rows.Select(r => CalcRow.Of(("x", new CalcCell(r.X, "mm")), ("F_y", new CalcCell(r.Fy, "kN")), ("F_z", new CalcCell(r.Fz, "kN")), ("m", new CalcCell(r.M, "kg")))).ToList());

    /// <summary>Sections from (x mm, K_t, K_ts, q, q_s) rows.</summary>
    public static CalcInputValue Sections(params (double X, double Kt, double Kts, double Q, double Qs)[] rows) =>
        CalcInputValue.Table("sections", rows.Select(r => CalcRow.Of(("x", new CalcCell(r.X, "mm")), ("K_t", new CalcCell(r.Kt)), ("K_ts", new CalcCell(r.Kts)), ("q", new CalcCell(r.Q)), ("q_s", new CalcCell(r.Qs)))).ToList());

    private sealed record Segment(double X0, double X1, double D, double Di)
    {
        public double I => Math.PI / 64 * (Math.Pow(D, 4) - Math.Pow(Di, 4));

        public double Area => Math.PI / 4 * (D * D - Di * Di);
    }

    private sealed record Load(double X, double Fy, double Fz, double Mass);

    public void Run(CalcRunContext c)
    {
        try
        {
            RunCore(c);
        }
        catch (FrameModelException ex)
        {
            c.Block("geometry", "segments: the shaft model could not be solved: " + ex.Message);
        }
    }

    private void RunCore(CalcRunContext c)
    {
        foreach (var s in new[] { "x_A", "x_B", "T", "x_T1", "n", "S_ut", "S_y", "E", "rho", "R", "k_d", "k_f", "n_target", "theta_max", "y_max" })
            c.Input(s);

        if (c.Si("S_y") > c.Si("S_ut"))
        {
            c.Block("strength", "S_y: the yield strength is above the ultimate strength.");
            return;
        }

        var mm = UnitCatalog.Parse("mm");
        var segments = new List<Segment>();
        var x0 = 0.0;

        foreach (var row in c.Rows("segments"))
        {
            var x1 = Cell(row, "x_end", mm);
            var d = Cell(row, "d", mm);
            var di = Cell(row, "d_i", mm);

            if (!(x1 > x0) || !(di < d))
            {
                c.Block("geometry", "segments: each segment must end beyond the previous one, and a bore must be smaller than its diameter.");
                return;
            }

            segments.Add(new Segment(x0, x1, d, di));
            x0 = x1;
        }

        var length = x0;

        // mech.shaft-03: a blank torque exit is the shaft length, not a 400 mm default that suited one example.
        var xT2 = length;

        if (c.Has("x_T2"))
            xT2 = c.InputSi("x_T2");
        else
            c.Assume($"The torque leaves at the shaft end ({F(length * 1e3)} mm): x_T2 is blank.", "Every section beyond the torque entry carries the torque; enter x_T2 for a torque taken off part-way.");

        var xa = c.Si("x_A");
        var xb = c.Si("x_B");
        var loads = c.Rows("loads").Select(r => new Load(Cell(r, "x", mm), Cell(r, "F_y", UnitCatalog.Parse("kN")), Cell(r, "F_z", UnitCatalog.Parse("kN")), Cell(r, "m", UnitCatalog.Parse("kg")))).ToList();
        var sections = c.Rows("sections").Select(r => (X: Cell(r, "x", mm), Kt: r.Get("K_t")?.Number ?? 1, Kts: r.Get("K_ts")?.Number ?? 1, Q: r.Get("q")?.Number ?? 1, Qs: r.Get("q_s")?.Number ?? 1)).ToList();

        if (segments.Count == 0 || xa > length || xb > length || Math.Abs(xa - xb) < 1e-6 || loads.Any(l => l.X > length) || sections.Any(s => s.X > length))
        {
            c.Block("geometry", "segments: enter the shaft segments, and keep the bearings (which must differ), the loads and the sections on the shaft.");
            return;
        }

        // Points closer than L/200 share a node, so two bearings that near would collapse into one support (Black B-A-07).
        if (Math.Abs(xa - xb) < length / 200)
        {
            c.Block("geometry", $"x_B: the bearings ({F(xa * 1e3)} and {F(xb * 1e3)} mm) are closer than 1/200 of the shaft length ({F(length * 1e3 / 200)} mm), so the model would treat them as one support; move them apart.");
            return;
        }

        // Nodes at every feature, each interval split into equal parts.
        var features = new SortedSet<double> { 0, length, xa, xb, c.Si("x_T1"), xT2 };

        foreach (var x in segments.Select(s => s.X1).Concat(loads.Select(l => l.X)).Concat(sections.Select(s => s.X)).Where(x => x <= length))
            features.Add(x);

        // Features closer than L/200 share a node (the ends and bearings first), so no element is vanishingly short.
        var xs = new List<double>();
        var featureList = new List<double>();

        foreach (var x in new[] { 0, length, xa, xb }.Concat(features).Where(x => x >= 0 && x <= length))
        {
            if (featureList.All(f => Math.Abs(f - x) >= length / 200))
                featureList.Add(x);
        }

        featureList.Sort();

        for (var i = 0; i < featureList.Count - 1; i++)
        {
            // Short intervals are not subdivided, so no element is needlessly tiny beside a long one.
            var parts = featureList[i + 1] - featureList[i] < length / 100 ? 1 : Divisions;

            for (var k = 0; k < parts; k++)
                xs.Add(featureList[i] + (featureList[i + 1] - featureList[i]) * k / parts);
        }

        xs.Add(length);
        xs = Dedupe(xs);
        var e = c.Si("E");
        var elements = new List<FrameElement>();

        for (var i = 0; i < xs.Count - 1; i++)
        {
            // Axial stiffness plays no part (no axial load, pinned at A): the area is scaled so EA/ℓ matches 12EI/ℓ³,
            // which keeps the stiffness matrix well conditioned when a short element sits beside long ones.
            var seg = SegmentAt(segments, (xs[i] + xs[i + 1]) / 2);
            var span = xs[i + 1] - xs[i];
            elements.Add(new FrameElement(i + 1, i + 1, i + 2, e, 12 * seg.I / (span * span), seg.I, false, false));
        }

        int NodeAt(double x) => xs.Select((v, i) => (D: Math.Abs(v - x), I: i)).MinBy(p => p.D).I + 1;
        var nodeA = NodeAt(xa);
        var nodeB = NodeAt(xb);
        var nodes = xs.Select((x, i) => new FrameNode(i + 1, x, 0, i + 1 == nodeA, i + 1 == nodeA || i + 1 == nodeB, false)).ToList();
        var combo = new FrameCombination("design", [(1, 1.0)]);

        // Masses at the nodes: the shaft's own mass split to its end nodes plus the masses carried. They give the critical speed, and with w_masses their weights load the shaft (mech.shaft-04).
        var masses = new double[nodes.Count];

        for (var i = 0; i < elements.Count; i++)
        {
            var half = c.Si("rho") * SegmentAt(segments, (xs[i] + xs[i + 1]) / 2).Area * (xs[i + 1] - xs[i]) / 2;
            masses[i] += half;
            masses[i + 1] += half;
        }

        foreach (var l in loads)
            masses[NodeAt(l.X) - 1] += l.Mass;

        var addWeights = c.InputFlag("w_masses");
        var carried = loads.Sum(l => l.Mass);

        if (!addWeights && carried > 0)
            c.Warn("mass-weight", $"m: the carried mass ({F(carried)} kg) adds no weight to the reactions, the moment or the deflection, and neither does the shaft's own weight; switch w_masses on to add them, or enter the weights as forces in F_y.");

        // A plane with no load carries nothing (the frame model refuses an empty load case).
        FrameResult? SolvePlane(Func<Load, double> force, bool weights)
        {
            var cases = loads.Where(l => force(l) != 0).Select(l => new FrameLoad(1, FrameLoadKind.Nodal, NodeAt(l.X), 0, 0, 0, 0, 0, force(l), 0, false)).ToList();

            if (weights)
                cases.AddRange(masses.Select((m, i) => (m, i)).Where(q => q.m > 0).Select(q => new FrameLoad(1, FrameLoadKind.Nodal, q.i + 1, 0, 0, 0, 0, 0, -q.m * Gravity, 0, false)));

            return cases.Count > 0 ? FrameSolver.Solve(new FrameModel(nodes, elements, cases, [combo]), c.CancellationToken)[0] : null;
        }

        var vertical = SolvePlane(l => l.Fy, addWeights);
        var horizontal = SolvePlane(l => l.Fz, false);
        var kN = UnitCatalog.Parse("kN");
        double Reaction(FrameResult? r, int node) => r is not null && r.Reactions.TryGetValue(node, out var v) ? v.Ry : 0;
        var ra = Math.Sqrt(Math.Pow(Reaction(vertical, nodeA), 2) + Math.Pow(Reaction(horizontal, nodeA), 2));
        var rb = Math.Sqrt(Math.Pow(Reaction(vertical, nodeB), 2) + Math.Pow(Reaction(horizontal, nodeB), 2));
        c.Record("R_A", "√(R_A,y² + R_A,z²)", $"√({F(Reaction(vertical, nodeA) / 1e3)}² + {F(Reaction(horizontal, nodeA) / 1e3)}²)", ScalarValue.Of(ra / 1e3, kN), "Radial reaction at bearing A");
        c.Record("R_B", "√(R_B,y² + R_B,z²)", $"√({F(Reaction(vertical, nodeB) / 1e3)}² + {F(Reaction(horizontal, nodeB) / 1e3)}²)", ScalarValue.Of(rb / 1e3, kN), "Radial reaction at bearing B");
        c.Record("d_A", "seat diameter at A", $"x = {F(xa * 1e3)} mm", ScalarValue.Of(SegmentAt(segments, xa).D * 1e3, mm), "Shaft diameter at bearing A");
        c.Record("d_B", "seat diameter at B", $"x = {F(xb * 1e3)} mm", ScalarValue.Of(SegmentAt(segments, xb).D * 1e3, mm), "Shaft diameter at bearing B");

        (double M, double Slope, double Y) At(double x)
        {
            var i = Math.Clamp(xs.FindLastIndex(v => v <= x + 1e-12), 0, elements.Count - 1);
            var local = x - xs[i];
            var mv = vertical?.Members[i].At(local).M ?? 0;
            var mh = horizontal?.Members[i].At(local).M ?? 0;
            var sv = vertical?.Members[i].Slope(local) ?? 0;
            var sh = horizontal?.Members[i].Slope(local) ?? 0;
            var yv = vertical?.Members[i].At(local).Vy ?? 0;
            var yh = horizontal?.Members[i].At(local).Vy ?? 0;
            return (Math.Sqrt(mv * mv + mh * mh), Math.Sqrt(sv * sv + sh * sh), Math.Sqrt(yv * yv + yh * yh));
        }

        var mMax = xs.Max(x => At(x).M);
        c.Record("M_max", "max √(M_y² + M_z²) along the shaft", $"{xs.Count} stations", ScalarValue.Of(mMax / 1e3, "kN*m"), "Largest bending moment");
        var thetaA = At(xa).Slope;
        var thetaB = At(xb).Slope;
        c.Record("theta_A", "slope at A (two planes)", $"x = {F(xa * 1e3)} mm", ScalarValue.Of(thetaA, "rad"), "Slope at bearing A");
        c.Record("theta_B", "slope at B (two planes)", $"x = {F(xb * 1e3)} mm", ScalarValue.Of(thetaB, "rad"), "Slope at bearing B");
        var yLoad = loads.Count == 0 ? 0 : loads.Max(l => At(l.X).Y);
        c.Record("y_load", "largest deflection at a load", string.Join(", ", loads.Select(l => $"{F(At(l.X).Y * 1e3)} mm at {F(l.X * 1e3)}")), ScalarValue.Of(yLoad * 1e3, mm), "Deflection at the loads");

        // Fatigue and yield at each marked section.
        var sut = c.Si("S_ut") / 1e6;
        var sy = c.Si("S_y") / 1e6;
        var finish = c.InputText("finish");
        var (a, b) = finish switch { Ground => (1.58, -0.085), HotRolled => (57.7, -0.718), Forged => (272.0, -0.995), _ => (4.51, -0.265) };
        var ka = c.Has("k_a") ? c.InputSi("k_a") : a * Math.Pow(sut, b);
        var z = NormalQuantile(c.Si("R"));
        var ke = 1 - 0.08 * z;
        var sePrime = sut <= 1400 ? 0.5 * sut : 700;
        c.Record("k_a", c.Has("k_a") ? "k_a (entered, from a record)" : "a·S_ut^b", c.Has("k_a") ? F(ka) : $"{F(a)} × {F(sut)}^{F(b)} ({finish})", new ScalarValue(ka), "Surface factor", "Shigley 10th ed. §6-9, Table 6-2");
        c.Record("k_e", "1 − 0.08·z_R", $"1 − 0.08 × {F(z)}", new ScalarValue(ke), "Reliability factor", "Shigley 10th ed. §6-9");
        c.Record("S_e1", sut <= 1400 ? "0.5·S_ut (S_e′)" : "700 MPa (S_ut > 1400 MPa, S_e′)", $"{F(sut)} MPa", ScalarValue.Of(sePrime, "MPa"), "Rotating-beam endurance limit");
        var torque = c.Si("T");
        var alternatingTorque = c.Has("T_a") ? c.InputSi("T_a") : 0;
        var peakTorque = Math.Max(torque + alternatingTorque, c.Has("T_pk") ? c.InputSi("T_pk") : 0);
        var tLo = Math.Min(c.Si("x_T1"), xT2);
        var tHi = Math.Max(c.Si("x_T1"), xT2);
        var worstF = double.PositiveInfinity;
        var worstY = double.PositiveInfinity;
        var seGoverning = 0.0;
        var index = 0;

        foreach (var s in sections)
        {
            index++;
            var inTorqueZone = s.X >= tLo - 1e-12 && s.X <= tHi + 1e-12;
            var ma = At(s.X).M;
            var tm = inTorqueZone ? torque : 0;
            var ta = inTorqueZone ? alternatingTorque : 0;
            var tPeak = inTorqueZone ? peakTorque : 0;
            var kf = 1 + s.Q * (s.Kt - 1);
            var kfs = 1 + s.Qs * (s.Kts - 1);

            // A shoulder is checked on both sides (mech.shaft-02): the smaller outer diameter is not always the weaker side when the bores differ.
            var candidates = Meeting(segments, s.X).Select(seg =>
            {
                var dmm = seg.D * 1e3;
                var kb = dmm <= 51 ? 1.24 * Math.Pow(dmm, -0.107) : 1.51 * Math.Pow(Math.Min(dmm, 254), -0.157);

                if (dmm > 254)
                    c.Warn("size-factor", $"segments: d = {F(dmm)} mm is above 254 mm; k_b is held at its 254 mm value.");

                var se = ka * kb * c.InputSi("k_d") * ke * c.InputSi("k_f") * sePrime;
                var modulus = Math.PI * (Math.Pow(seg.D, 4) - Math.Pow(seg.Di, 4)) / seg.D;
                var sa = kf * 32 * ma / modulus / 1e6;
                var taum = kfs * 16 * tm / modulus / 1e6;
                var taua = kfs * 16 * ta / modulus / 1e6;
                var taupk = kfs * 16 * tPeak / modulus / 1e6;
                var inverse = Math.Sqrt(sa * sa + 3 * taua * taua) / se + Math.Sqrt(3) * taum / sut;
                var nf = inverse > 0 ? 1 / inverse : double.PositiveInfinity;
                var vm = Math.Sqrt(sa * sa + 3 * taupk * taupk);
                var ny = vm > 0 ? sy / vm : double.PositiveInfinity;
                return (Seg: seg, dmm, kb, se, sa, taum, taua, taupk, nf, ny);
            }).ToList();

            var g = candidates.MinBy(q => q.nf);
            var (_, dmmG, kbG, seG, saG, taumG, tauaG, taupkG, nfG, nyG) = g;
            var nyMin = candidates.Min(q => q.ny);
            var side = candidates.Count > 1 ? $" (shoulder: the side with d = {F(dmmG)} mm{(g.Seg.Di > 0 ? $", bore {F(g.Seg.Di * 1e3)} mm" : string.Empty)} governs, of {candidates.Count})" : string.Empty;
            c.Record($"X{index}", "σ_a = K_f·32M/(πd³′), τ_m = K_fs·16T_m/(πd³′), τ_a = K_fs·16T_a/(πd³′); 1/n_f = √(σ_a² + 3τ_a²)/S_e + √3τ_m/S_ut",
                $"x = {F(s.X * 1e3)} mm, d = {F(dmmG)} mm, k_b = {F(kbG)}, S_e = {F(seG)} MPa, K_f = {F(kf)}, K_fs = {F(kfs)}, M = {F(ma)} N·m, T = {F(tm)} N·m, σ_a = {F(saG)} MPa, τ_m = {F(taumG)} MPa{(ta > 0 ? $", T_a = {F(ta)} N·m, τ_a = {F(tauaG)} MPa" : string.Empty)}{(tPeak > tm ? $", T_pk = {F(tPeak)} N·m, τ_pk = {F(taupkG)} MPa" : string.Empty)}, n_y = {(double.IsInfinity(nyG) ? "—" : F(nyG))}{(nyMin < nyG * (1 - 1e-12) ? $" (n_y of the other side of the shoulder, which sets n_y,min: {F(nyMin)})" : string.Empty)}{side}",
                new ScalarValue(double.IsInfinity(nfG) ? 1e9 : nfG), $"Section at {F(s.X * 1e3)} mm: fatigue safety factor (DE-Goodman)", "Shigley 10th ed. §7-4");

            if (nfG < worstF)
            {
                worstF = nfG;
                seGoverning = seG;
            }

            worstY = Math.Min(worstY, nyMin);
        }

        if (peakTorque > 0)
        {
            var outside = sections.Where(q => q.X < tLo - 1e-12 || q.X > tHi + 1e-12).Select(q => F(q.X * 1e3)).ToList();

            if (outside.Count > 0)
                c.Warn("torque-span", $"x_T2: the torque is transmitted only between x_T1 = {F(tLo * 1e3)} mm and x_T2 = {F(tHi * 1e3)} mm, so the section(s) at {string.Join(", ", outside)} mm carry no torque; set x_T2 (blank: the shaft length) if the torque is carried there.");
        }

        var outputs = new List<(string, string)> { ("R_A", "Reaction at A"), ("R_B", "Reaction at B"), ("d_A", "Seat diameter at A"), ("d_B", "Seat diameter at B"), ("M_max", "Largest bending moment"), ("theta_A", "Slope at A"), ("theta_B", "Slope at B"), ("y_load", "Deflection at the loads") };

        if (sections.Count > 0)
        {
            c.Record("S_e", "k_a·k_b·k_c·k_d·k_e·k_f·S_e′ (governing section)", $"k_c = 1", ScalarValue.Of(seGoverning, "MPa"), "Endurance limit at the governing section");
            c.Record("n_f_min", "min over sections", $"{sections.Count} section(s)", new ScalarValue(double.IsInfinity(worstF) ? 1e9 : worstF), "Lowest fatigue safety factor");
            c.Record("n_y_min", "min over sections S_y/σ′_max", $"{sections.Count} section(s)", new ScalarValue(double.IsInfinity(worstY) ? 1e9 : worstY), "Lowest yield safety factor");
            outputs.AddRange([("S_e", "Endurance limit"), ("n_f_min", "Lowest fatigue safety factor"), ("n_y_min", "Lowest yield safety factor")]);
        }

        // Critical speed: the masses at the nodes.
        var opSpeed = c.Si("n");

        var top = 0.0;
        var bottom = 0.0;

        if (masses.Sum() > 0)
        {
            var gravity = FrameSolver.Solve(new FrameModel(nodes, elements, [.. masses.Select((m, i) => (m, i)).Where(p => p.m > 0).Select(p => new FrameLoad(1, FrameLoadKind.Nodal, p.i + 1, 0, 0, 0, 0, 0, -p.m * Gravity, 0, false))], [combo]), c.CancellationToken)[0];
            var y = Enumerable.Range(0, nodes.Count).Select(i => gravity.Displacements[i + 1].Uy).ToArray();
            top = masses.Select((m, i) => -m * y[i]).Sum();
            bottom = masses.Select((m, i) => m * y[i] * y[i]).Sum();
        }

        if (!(top > 0 && bottom > 0))
        {
            // Nothing deflects: no density and no carried mass, or every carried mass sits on a bearing (Black B-A-07).
            c.Warn("critical-speed-skipped", masses.Sum() > 0
                ? "rho: every carried mass sits on a bearing and ρ is 0, so no mass deflects and no critical speed is found; enter ρ or move a mass off the bearings."
                : "rho: no mass is carried and ρ is 0, so no critical speed is found; enter ρ (the shaft's own mass) or a carried mass.");
        }
        else
        {
            var rpm = UnitCatalog.Parse("rpm");
            var omega = Math.Sqrt(Gravity * top / bottom);
            c.Record("n_c", "√(g·Σm·y/Σm·y²) (Rayleigh, y signed)", $"Σm = {F(masses.Sum())} kg over {nodes.Count} nodes", ScalarValue.Of(rpm.FromSi(omega), rpm), "First lateral critical speed (Rayleigh)", "Shigley 10th ed. §7-6");

            // Dunkerley: δ_ii by a unit load at each loaded node, fifty cases per solve.
            var loaded = masses.Select((m, i) => (m, i)).Where(p => p.m > 0).ToList();
            var sum = 0.0;

            foreach (var batch in loaded.Chunk(FrameLimits.MaxCases))
            {
                var cases = batch.Select((p, k) => new FrameLoad(k + 1, FrameLoadKind.Nodal, p.i + 1, 0, 0, 0, 0, 0, -1, 0, false)).ToList();
                var results = FrameSolver.Solve(new FrameModel(nodes, elements, cases, [.. batch.Select((p, k) => new FrameCombination($"u{k + 1}", [(k + 1, 1.0)]))]), c.CancellationToken);

                for (var k = 0; k < batch.Length; k++)
                    sum += batch[k].m * Math.Abs(results[k].Displacements[batch[k].i + 1].Uy);
            }

            var omegaD = sum > 0 ? 1 / Math.Sqrt(sum) : double.PositiveInfinity;
            c.Record("n_c_D", "1/ω² = Σ m_i·δ_ii (Dunkerley, lower bound)", $"{loaded.Count} masses", ScalarValue.Of(rpm.FromSi(omegaD), rpm), "First critical speed, Dunkerley lower bound", "Shigley 10th ed. §7-6");
            outputs.AddRange([("n_c", "First critical speed (Rayleigh)"), ("n_c_D", "Dunkerley lower bound")]);

            if (opSpeed > 0)
            {
                // The Dunkerley lower bound, not the Rayleigh upper bound, is compared with the operating speed (Green G-A-07).
                var ratio = omegaD / opSpeed;
                c.Record("r_c", "n_c,D/n", $"{F(rpm.FromSi(omegaD))} / {F(rpm.FromSi(opSpeed))}", new ScalarValue(ratio), "Critical-speed ratio (Dunkerley lower bound over operating speed)");
                outputs.Add(("r_c", "Critical-speed ratio"));

                if (ratio < 2)
                    c.Warn("critical-speed", $"n: the first critical speed (Dunkerley lower bound) is {CalcValue.FormatNumber(ratio, 3)} times the operating speed; keep it at least twice.");
            }
        }

        // Key.
        if ((c.Has("x_key") || c.Has("w") || c.Has("h") || c.Has("L_key")) && !(c.Has("x_key") && c.Has("w") && c.Has("h") && c.Has("L_key")))
            c.Warn("key", "x_key: the key check needs its position, width, height and length; it is not made until all four are entered.");

        if (c.Has("x_key") && c.Has("w") && c.Has("h") && c.Has("L_key"))
        {
            foreach (var s in new[] { "x_key", "w", "h", "L_key" })
                c.Input(s);

            if (peakTorque > 0 && (c.Si("x_key") < tLo - 1e-12 || c.Si("x_key") > tHi + 1e-12))
                c.Warn("torque-span", $"x_key: the key at {F(c.Si("x_key") * 1e3)} mm lies outside the torque span {F(tLo * 1e3)} … {F(tHi * 1e3)} mm; the key check still uses the peak torque.");

            var dk = Smallest(segments, c.Si("x_key")).D;
            var tauKey = 2 * peakTorque / (dk * c.Si("w") * c.Si("L_key")) / 1e6;
            var sigmaKey = 4 * peakTorque / (dk * c.Si("h") * c.Si("L_key")) / 1e6;
            c.Record("tau_key", "2T/(d·w·L)", $"2 × {F(peakTorque)} N·m / ({F(dk * 1e3)} × {F(c.Si("w") * 1e3)} × {F(c.Si("L_key") * 1e3)}) mm³", ScalarValue.Of(tauKey, "MPa"), "Key shear stress", "Shigley 10th ed. §7-7");
            c.Record("sigma_key", "4T/(d·h·L)", $"4 × {F(peakTorque)} N·m / ({F(dk * 1e3)} × {F(c.Si("h") * 1e3)} × {F(c.Si("L_key") * 1e3)}) mm³", ScalarValue.Of(sigmaKey, "MPa"), "Key bearing stress", "Shigley 10th ed. §7-7");
            outputs.AddRange([("tau_key", "Key shear"), ("sigma_key", "Key bearing")]);

            if (c.Has("S_y_key"))
            {
                c.Input("S_y_key");
                var syk = c.Si("S_y_key") / 1e6;
                var target = c.InputSi("n_target");

                // The key bears on the key, the shaft's keyway and the hub: the weakest of the three yield strengths governs the bearing (mech.shaft-05); shear is the key's.
                var syBearing = Math.Min(syk, sy);
                var hubNote = string.Empty;

                if (c.Has("S_y_hub"))
                {
                    c.Input("S_y_hub");
                    syBearing = Math.Min(syBearing, c.Si("S_y_hub") / 1e6);
                    hubNote = $", hub {F(c.Si("S_y_hub") / 1e6)}";
                }
                else
                {
                    c.Warn("key-hub", "S_y_hub: no hub strength is entered, so the key bearing is checked against the key and the shaft only; the hub is often the weakest part, enter its yield strength.");
                }

                var lReq = Math.Max(2 * peakTorque * target / (dk * c.Si("w") * 0.577 * syk * 1e6), 4 * peakTorque * target / (dk * c.Si("h") * syBearing * 1e6));
                c.Record("L_key_req", "max(2T·n_target/(d·w·0.577S_y,key), 4T·n_target/(d·h·S_y,min))", $"n_target = {F(target)}, S_y,min = {F(syBearing)} MPa (key {F(syk)}, shaft {F(sy)}{hubNote})", ScalarValue.Of(lReq * 1e3, mm), "Key length required");
                c.Record("u_key", "L_req/L", $"{F(lReq * 1e3)} / {F(c.Si("L_key") * 1e3)}", new ScalarValue(lReq / c.Si("L_key")), "Key utilisation");
                outputs.AddRange([("L_key_req", "Key length required"), ("u_key", "Key utilisation")]);
            }
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        var nTarget = c.InputSi("n_target");

        if (sections.Count > 0)
        {
            c.Record("u_f", "n_target/n_f,min", $"{F(nTarget)} / {Show(worstF)}", new ScalarValue(nTarget / worstF), "Fatigue utilisation");
            c.Record("u_y", "n_target/n_y,min", $"{F(nTarget)} / {Show(worstY)}", new ScalarValue(nTarget / worstY), "Yield utilisation");
            c.CheckUtilisation("Fatigue safety factor against target (DE-Goodman)", "u_f", "Shigley 10th ed. §7-4");
            c.CheckUtilisation("Yield safety factor against target", "u_y");
        }

        c.Record("u_slope", "max(θ_A, θ_B)/θ_max", $"{F(Math.Max(thetaA, thetaB))} / {F(c.Si("theta_max"))}", new ScalarValue(Math.Max(thetaA, thetaB) / c.Si("theta_max")), "Slope utilisation");
        c.Record("u_defl", "y_load/y_max", $"{F(yLoad * 1e3)} / {F(c.Si("y_max") * 1e3)} mm", new ScalarValue(yLoad / c.Si("y_max")), "Deflection utilisation");
        c.CheckUtilisation("Slope at the bearings against limit", "u_slope");
        c.CheckUtilisation("Deflection at the loads against limit", "u_defl");

        if (c.Has("S_y_key") && c.Has("L_key") && c.Has("w") && c.Has("h") && c.Has("x_key"))
            c.CheckUtilisation("Key length against required", "u_key");

        c.Series(new CalcSeries("Bending moment", "x", "mm", "M", "N·m", xs.Select(x => (x * 1e3, At(x).M)).ToList()));
        c.Series(new CalcSeries("Deflection", "x", "mm", "y", "mm", xs.Select(x => (x * 1e3, At(x).Y * 1e3)).ToList()));
    }

    /// <summary>The standard normal quantile (Acklam's rational approximation, relative error below 1.2e-9).</summary>
    public static double NormalQuantile(double p)
    {
        double[] a = [-3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00];
        double[] b = [-5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01];
        double[] c = [-7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00];
        double[] d = [7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00];
        const double low = 0.02425;

        if (p < low)
        {
            var q = Math.Sqrt(-2 * Math.Log(p));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        if (p > 1 - low)
        {
            var q = Math.Sqrt(-2 * Math.Log(1 - p));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) / ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1);
        }

        var r = p - 0.5;
        var s = r * r;
        return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r / (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1);
    }

    private static List<double> Dedupe(List<double> xs)
    {
        var result = new List<double>();

        foreach (var x in xs.OrderBy(v => v))
        {
            if (result.Count == 0 || x - result[^1] > 1e-9)
                result.Add(x);
        }

        return result;
    }

    private static Segment SegmentAt(IReadOnlyList<Segment> segments, double x) => segments.FirstOrDefault(s => x >= s.X0 - 1e-12 && x <= s.X1 + 1e-12) ?? segments[^1];

    /// <summary>The smaller of the segments meeting at x (a shoulder governs by its small side).</summary>
    private static Segment Smallest(IReadOnlyList<Segment> segments, double x) =>
        segments.Where(s => x >= s.X0 - 1e-9 && x <= s.X1 + 1e-9).OrderBy(s => s.D).FirstOrDefault() ?? segments[^1];

    /// <summary>Every segment meeting at x (two at a shoulder, one elsewhere), for a check on each side.</summary>
    private static List<Segment> Meeting(IReadOnlyList<Segment> segments, double x)
    {
        var meeting = segments.Where(s => x >= s.X0 - 1e-9 && x <= s.X1 + 1e-9).ToList();
        return meeting.Count > 0 ? meeting : [segments[^1]];
    }

    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);

    private static string Show(double v) => double.IsFinite(v) ? F(v) : "—";
}
