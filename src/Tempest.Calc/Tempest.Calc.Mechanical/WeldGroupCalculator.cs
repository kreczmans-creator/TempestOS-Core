using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M6 — fillet weld group by the elastic line method (welds as lines of unit throat), on the group-geometry kernel shared with M3.</summary>
public sealed class WeldGroupCalculator : ICalcCalculator
{
    public const string Id = "mech.weld-group";
    public const double ThroatRatio = 0.707;

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("x1", "Start x", CalcInputKind.Quantity, "mm", Min: -2000, Max: 2000, HardMin: -2000, HardMax: 2000),
        new("y1", "Start y", CalcInputKind.Quantity, "mm", Min: -2000, Max: 2000, HardMin: -2000, HardMax: 2000),
        new("x2", "End x", CalcInputKind.Quantity, "mm", Min: -2000, Max: 2000, HardMin: -2000, HardMax: 2000),
        new("y2", "End y", CalcInputKind.Quantity, "mm", Min: -2000, Max: 2000, HardMin: -2000, HardMax: 2000),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Weld group (elastic line method)",
        "A group of straight fillet welds in a plane under in-plane forces (with eccentricity, so torsion), an out-of-plane force and out-of-plane moments: the resultant force per unit length at every weld end, the critical point, the throat stress against the design strength and the leg size required.",
        "Welds as lines of unit throat: L_w = Σ l, centroid x_c = Σ l·x_m/L_w\n" +
        "Unit second moments: I_ux = Σ [l(y_m − y_c)² + l·Δy²/12], I_uy likewise, J_u = I_ux + I_uy\n" +
        "Torsion about the centroid: T = F_y(e_x − x_c) − F_x(e_y − y_c) + M_z\n" +
        "At each weld end: direct F/L_w, torsional T·r/J_u (perpendicular to r), out-of-plane F_z/L_w + M_x·y′/I_ux + M_y·x′/I_uy; vector sum f_r\n" +
        "Throat: a = 0.707·s, τ = f_r,max/a; required leg s_req = f_r,max/(0.707·f_w)",
        [
            new CalcInputSpec("welds", "Weld lines", CalcInputKind.Table, Columns: Columns, MaxRows: 50),
            new CalcInputSpec("s", "Fillet leg", CalcInputKind.Quantity, "mm", Default: 6, Min: 3, Max: 25, HardMin: 3, HardMax: 25, Group: "Weld"),
            new CalcInputSpec("f_w", "Design strength of the throat", CalcInputKind.Quantity, "MPa", Default: 220, Min: 50, Max: 600, HardMin: 1, HardMax: 2000, Group: "Weld"),
            new CalcInputSpec("F_x", "In-plane force, x", CalcInputKind.Quantity, "kN", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("F_y", "In-plane force, y", CalcInputKind.Quantity, "kN", Default: -20, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("F_z", "Out-of-plane force (tension positive)", CalcInputKind.Quantity, "kN", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("e_x", "Point of application, x", CalcInputKind.Quantity, "mm", Default: 200, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("e_y", "Point of application, y", CalcInputKind.Quantity, "mm", Default: 50, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("M_x", "Out-of-plane moment about x (tension on +y side)", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("M_y", "Out-of-plane moment about y (tension on +x side)", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("M_z", "Additional in-plane torque", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
        ],
        [
            new CalcAssumption("Elastic, static; the connected parts are rigid and the weld stress varies linearly with distance from the centroid.", "Conservative against the instantaneous-centre method."),
            new CalcAssumption("Fillet welds only, all of one leg size; the throat is 0.707·s.", "Equal-leg fillet."),
            new CalcAssumption("The design strength of the throat comes from S4 (EN 1993-1-8 simplified method) or a user allowable with its source.", "No resistance table ships."),
        ],
        [
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "11th ed., Ch. 9, Tables 9-1 and 9-2", "Welds treated as lines: torsional and bending properties"),
            new CalcClauseRef("Blodgett, Design of Welded Structures", "1966, §7.4", "Treating the weld as a line"),
            new CalcClauseRef("EN 1993-1-8:2005", "cl. 4.5.3.3", "Simplified method for design resistance of fillet welds (resistance input)"),
        ],
        [
            new CalcApplicabilityRule("few-welds", "No weld of non-zero length.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("collinear", "All welds on one line: the group has no stiffness about that line.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-two-vertical", "Two vertical welds under an eccentric vertical load (Shigley Table 9-1 case 2)",
                new CalcReferenceSource("Shigley's Mechanical Engineering Design", "11th ed., Table 9-1", "b = 50, d = 100 mm: J_u = d(3b² + d²)/6 = 291 666.7 mm³; F_y = −20 kN at 175 mm from the centroid: T = −3.5 kN·m; corner: torsional 12 N/mm·mm × (50, 25) mm = (600, 300) N/mm plus direct 100 N/mm → f_r = √(600² + 400²) = 721.11 N/mm; s = 6 mm: τ = 721.11/4.242 = 169.99 MPa"),
                [Welds((0, 0, 0, 100), (50, 0, 50, 100)), Q("s", 6, "mm"), Q("f_w", 220, "MPa"), Q("F_y", -20, "kN"), Q("e_x", 200, "mm"), Q("e_y", 50, "mm")],
                [new CalcExpectedValue("L_w", 200, "mm", 1e-9), new CalcExpectedValue("J_u", 291666.7, "mm^3", 1e-6), new CalcExpectedValue("T", -3.5, "kN*m", 1e-9), new CalcExpectedValue("f_r_max", 721.1103, "N/mm", 1e-6), new CalcExpectedValue("tau", 169.9930, "MPa", 1e-5)]),
            new CalcTestCase("ka-box-torsion", "Square box weld in pure torsion (Shigley Table 9-1 case 5)",
                new CalcReferenceSource("Shigley's Mechanical Engineering Design", "11th ed., Table 9-1", "b = d = 100 mm: J_u = (b + d)³/6 = 1.3333e6 mm³; T = 2 kN·m: corner f = T·r/J_u = 2e6 × 70.711/1.3333e6 = 106.07 N/mm"),
                [Welds((0, 0, 100, 0), (100, 0, 100, 100), (100, 100, 0, 100), (0, 100, 0, 0)), Q("F_y", 0, "kN"), Q("e_x", 50, "mm"), Q("e_y", 50, "mm"), Q("M_z", 2, "kN*m")],
                [new CalcExpectedValue("J_u", 1333333.3, "mm^3", 1e-6), new CalcExpectedValue("f_r_max", 106.0660, "N/mm", 1e-6)]),
            new CalcTestCase("ka-two-horizontal-bending", "Two horizontal welds in out-of-plane bending (Shigley Table 9-2)",
                new CalcReferenceSource("Shigley's Mechanical Engineering Design", "11th ed., Table 9-2", "b = 100 at y = ±50 (d = 100): I_u = b·d²/2 = 5e5 mm³; M_x = 5 kN·m: 500 N/mm; with F_y = 10 kN direct 50 N/mm: f_r = √(500² + 50²) = 502.49 N/mm; s_req = 502.49/(0.707 × 220) = 3.2306 mm"),
                [Welds((0, -50, 100, -50), (0, 50, 100, 50)), Q("F_y", 10, "kN"), Q("e_x", 50, "mm"), Q("e_y", 0, "mm"), Q("M_x", 5, "kN*m")],
                [new CalcExpectedValue("I_ux", 500000, "mm^3", 1e-9), new CalcExpectedValue("f_r_max", 502.4938, "N/mm", 1e-6), new CalcExpectedValue("s_req", 3.230640, "mm", 1e-5)]),
        ],
        Category: "Fasteners",
        OutputPorts:
        [
            new CalcOutputPort("f_r_max", "N/mm", "Maximum resultant force per unit length (→ S4)"),
            new CalcOutputPort("tau", "MPa", "Maximum throat stress"),
            new CalcOutputPort("s_req", "mm", "Leg size required"),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>A welds table from (x1, y1, x2, y2) mm lines.</summary>
    public static CalcInputValue Welds(params (double X1, double Y1, double X2, double Y2)[] lines) =>
        CalcInputValue.Table("welds", lines.Select(l => CalcRow.Of(("x1", new CalcCell(l.X1, "mm")), ("y1", new CalcCell(l.Y1, "mm")), ("x2", new CalcCell(l.X2, "mm")), ("y2", new CalcCell(l.Y2, "mm")))).ToList());

    public void Run(CalcRunContext c)
    {
        foreach (var s in new[] { "s", "f_w", "F_x", "F_y", "F_z", "e_x", "e_y", "M_x", "M_y", "M_z" })
            c.Input(s);

        var mm = UnitCatalog.Parse("mm");
        var lines = c.Rows("welds").Select(r => (X1: Cell(r, "x1", mm), Y1: Cell(r, "y1", mm), X2: Cell(r, "x2", mm), Y2: Cell(r, "y2", mm)))
            .Where(l => l.X1 != l.X2 || l.Y1 != l.Y2).ToList();

        if (lines.Count == 0)
        {
            c.Block("few-welds", "welds: enter at least one weld of non-zero length.");
            return;
        }

        // Kernel in mm (lengths) so the unit second moments come out in mm³.
        var g = GroupGeometry.OfLines([.. lines.Select(l => (l.X1 * 1e3, l.Y1 * 1e3, l.X2 * 1e3, l.Y2 * 1e3))]);

        if (g.Ix <= 1e-9 * g.Size * g.Size * g.Size || g.Iy <= 1e-9 * g.Size * g.Size * g.Size)
        {
            c.Block("collinear", "welds: all welds lie on one line; the group cannot resist bending or torsion about it.");
            return;
        }

        var mm3 = UnitCatalog.Parse("mm^3");
        c.Record("L_w", "Σ l", string.Join(" + ", lines.Select(l => F(Math.Sqrt((l.X2 - l.X1) * (l.X2 - l.X1) + (l.Y2 - l.Y1) * (l.Y2 - l.Y1)) * 1e3))), ScalarValue.Of(g.Size, mm), "Total weld length");
        c.Record("x_c", "Σ l·x_m / L_w", $"{F(g.Xc)} mm", ScalarValue.Of(g.Xc, mm), "Centroid x");
        c.Record("y_c", "Σ l·y_m / L_w", $"{F(g.Yc)} mm", ScalarValue.Of(g.Yc, mm), "Centroid y");
        c.Record("I_ux", "Σ [l(y_m − y_c)² + l·Δy²/12]", "about the centroid", ScalarValue.Of(g.Ix, mm3), "Unit second moment about x", "Shigley Table 9-2");
        c.Record("I_uy", "Σ [l(x_m − x_c)² + l·Δx²/12]", "about the centroid", ScalarValue.Of(g.Iy, mm3), "Unit second moment about y", "Shigley Table 9-2");
        c.Record("J_u", "I_ux + I_uy", $"{F(g.Ix)} + {F(g.Iy)}", ScalarValue.Of(g.J, mm3), "Unit polar moment", "Shigley Table 9-1");

        // Forces in N, moments in N·mm, lengths in mm → N/mm.
        var fx = c.Si("F_x");
        var fy = c.Si("F_y");
        var fz = c.Si("F_z");
        var mx = c.Si("M_x") * 1e3;
        var my = c.Si("M_y") * 1e3;
        var ex = c.Si("e_x") * 1e3 - g.Xc;
        var ey = c.Si("e_y") * 1e3 - g.Yc;
        var torque = fy * ex - fx * ey + c.Si("M_z") * 1e3;
        c.Record("T", "F_y(e_x − x_c) − F_x(e_y − y_c) + M_z", $"{F(fy / 1e3)} × {F(ex)} − {F(fx / 1e3)} × {F(ey)} + {F(c.Si("M_z") / 1e3)}e3", ScalarValue.Of(torque / 1e6, "kN*m"), "In-plane torque about the centroid");

        var worst = (F: -1.0, X: 0.0, Y: 0.0, Vx: 0.0, Vy: 0.0, Vz: 0.0);

        foreach (var (x, y) in lines.SelectMany(l => new[] { (l.X1 * 1e3, l.Y1 * 1e3), (l.X2 * 1e3, l.Y2 * 1e3) }).Distinct())
        {
            var rx = x - g.Xc;
            var ry = y - g.Yc;
            var vx = fx / g.Size - torque * ry / g.J;
            var vy = fy / g.Size + torque * rx / g.J;
            var vz = fz / g.Size + mx * ry / g.Ix + my * rx / g.Iy;
            var f = Math.Sqrt(vx * vx + vy * vy + vz * vz);

            if (f > worst.F + 1e-9)
                worst = (f, x, y, vx, vy, vz);
        }

        var nmm = UnitCatalog.Parse("N/mm");
        c.Record("f_r_max", "max over weld ends √(f_x² + f_y² + f_z²)", $"at ({F(worst.X)}, {F(worst.Y)}) mm: ({F(worst.Vx)}, {F(worst.Vy)}, {F(worst.Vz)}) N/mm", ScalarValue.Of(worst.F, nmm), "Maximum resultant force per unit length");
        c.Record("critical", "critical point", $"({F(worst.X)}, {F(worst.Y)}) mm", new TextValue($"({F(worst.X)}, {F(worst.Y)}) mm"), "Where f_r is greatest");
        c.Assign("a", "0.707 * s", "mm", "Throat");
        c.Assign("tau", "f_r_max / a", "MPa", "Maximum throat stress");
        c.Assign("s_req", "f_r_max / (0.707 * f_w)", "mm", "Leg size required");
        c.Assign("u", "tau / f_w", null, "Utilisation");

        foreach (var (symbol, text) in new[] { ("L_w", "Weld length"), ("x_c", "Centroid x"), ("y_c", "Centroid y"), ("I_ux", "Unit second moment about x"), ("I_uy", "Unit second moment about y"), ("J_u", "Unit polar moment"), ("T", "Torque about the centroid"), ("f_r_max", "Maximum force per unit length"), ("critical", "Critical point"), ("a", "Throat"), ("tau", "Throat stress"), ("s_req", "Leg required"), ("u", "Utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Throat stress against design strength", "u", "EN 1993-1-8 cl. 4.5.3.3");
        c.Series(new CalcSeries("Weld lines", "x", "mm", "y", "mm", lines.SelectMany(l => new[] { (l.X1 * 1e3, l.Y1 * 1e3), (l.X2 * 1e3, l.Y2 * 1e3) }).ToList()));
    }

    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
