using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M3b — bolt group by the elastic method: in-plane forces and torsion, out-of-plane moments and axial load, per-bolt shear and tension, governing bolt, optional simple prying.</summary>
public sealed class BoltGroupCalculator : ICalcCalculator
{
    public const string Id = "mech.bolt-group";

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("x", "x", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("y", "y", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("A", "Shear/stress area", CalcInputKind.Quantity, "mm^2", HardMin: 1e-6, HardMax: 1e6),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Bolt group (elastic method)",
        "A pattern of bolts in a plane under in-plane forces (with eccentricity, so torsion), out-of-plane moments and axial load: the shear and tension in every bolt by the elastic (area-weighted) method, the governing bolt, and an optional lever-model prying addition.",
        "Centroid x_c = Σ A x / Σ A. Torsion about the centroid T = F_y (e_x − x_c) − F_x (e_y − y_c) + T_z. Polar J = Σ A r². Shear per bolt: v = A (F/ΣA) + T·A·r/J (vector sum). Tension per bolt: t = A N/ΣA + M_x A y'/Σ A y'² + M_y A x'/Σ A x'². Prying: t_p = t (1 + b/a) when a and b are given. Utilisation: (v/V_R)² + (t_p/T_R)² ≤ 1 (interaction) and each alone.",
        [
            new CalcInputSpec("bolts", "Bolt positions and areas", CalcInputKind.Table, Columns: Columns, MaxRows: 200),
            new CalcInputSpec("F_x", "In-plane force, x", CalcInputKind.Quantity, "kN", Default: 0, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("F_y", "In-plane force, y", CalcInputKind.Quantity, "kN", Default: -50, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("e_x", "Point of application, x", CalcInputKind.Quantity, "mm", Default: 150, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("e_y", "Point of application, y", CalcInputKind.Quantity, "mm", Default: 0, HardMin: -1e5, HardMax: 1e5, Group: "Loads"),
            new CalcInputSpec("T_z", "Additional in-plane torque about the centroid", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("N", "Axial (out-of-plane) load, tension positive", CalcInputKind.Quantity, "kN", Default: 0, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("M_x", "Out-of-plane moment about x (tension on +y side)", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("M_y", "Out-of-plane moment about y (tension on +x side)", CalcInputKind.Quantity, "kN*m", Default: 0, HardMin: -1e6, HardMax: 1e6, Group: "Loads"),
            new CalcInputSpec("V_R", "Shear resistance per bolt", CalcInputKind.Quantity, "kN", Default: 30, Min: 0.1, Max: 1e4, HardMin: 1e-3, HardMax: 1e6, Group: "Resistance"),
            new CalcInputSpec("T_R", "Tension resistance per bolt", CalcInputKind.Quantity, "kN", Default: 45, Min: 0.1, Max: 1e4, HardMin: 1e-3, HardMax: 1e6, Group: "Resistance"),
            new CalcInputSpec("a", "Prying lever: bolt to flange edge", CalcInputKind.Quantity, "mm", IsOptional: true, HardMin: 1e-3, HardMax: 1e4, Group: "Prying"),
            new CalcInputSpec("b", "Prying lever: bolt to the loaded line", CalcInputKind.Quantity, "mm", IsOptional: true, HardMin: 0, HardMax: 1e4, Group: "Prying"),
        ],
        [
            new CalcAssumption("The connected parts are rigid; bolt forces vary linearly with distance from the centroid (elastic method).", "Conservative compared with plastic / instantaneous-centre methods."),
            new CalcAssumption("Resistances are per bolt as entered, with their source (a design-code capacity or a manufacturer value).", "No code capacity is computed here."),
            new CalcAssumption("Prying uses a simple lever: the bolt tension rises by the factor (1 + b/a).", "An optional, conservative first estimate."),
        ],
        [
            new CalcClauseRef("EN 1993-1-8:2005", "§3.6, §3.8", "Design resistance of individual fasteners; elastic distribution in a bolt group"),
            new CalcClauseRef("AISC Steel Construction Manual", "Part 7", "Eccentrically loaded bolt groups — elastic method"),
        ],
        [
            new CalcApplicabilityRule("few-bolts", "Fewer than two bolts: torsion cannot be resisted.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("coincident", "Two bolts share a position.", CalcNoticeSeverity.Block),
        ],
        [
            new CalcTestCase("ka-four-bolt-eccentric", "Four-bolt square pattern, 50 kN at 150 mm eccentricity",
                new CalcReferenceSource("Analytic (elastic method; AISC Manual Part 7 procedure)", null, "100 mm square, F_y = −50 kN at 150 mm from the centroid: direct 12.5 kN/bolt; T = −7.5 kN·m; J = 4 × 84.3 × 5000 = 1.686e6 mm⁴; torsional components ±18.75 kN each way; governing resultant √(18.75² + 31.25²) = 36.44 kN"),
                [
                    Bolts(("0", "0"), ("100", "0"), ("100", "100"), ("0", "100")),
                    CalcInputValue.Quantity("F_x", 0, "kN"), CalcInputValue.Quantity("F_y", -50, "kN"), CalcInputValue.Quantity("e_x", 200, "mm"), CalcInputValue.Quantity("e_y", 50, "mm"),
                    CalcInputValue.Quantity("T_z", 0, "kN*m"), CalcInputValue.Quantity("N", 0, "kN"), CalcInputValue.Quantity("M_x", 0, "kN*m"), CalcInputValue.Quantity("M_y", 0, "kN*m"),
                    CalcInputValue.Quantity("V_R", 40, "kN"), CalcInputValue.Quantity("T_R", 45, "kN"),
                ],
                [new CalcExpectedValue("T", -7.5, "kN*m", 1e-6), new CalcExpectedValue("J", 1.686e6, "mm^4", 1e-6), new CalcExpectedValue("v_max", 36.443, "kN", 1e-3), new CalcExpectedValue("u_max", 0.91109, "", 1e-3)]),
            new CalcTestCase("ka-moment-tension", "Two rows of bolts under an out-of-plane moment",
                new CalcReferenceSource("Analytic (elastic method)", null, "Four bolts at y = ±100 mm, M_x = 20 kN·m: Σ A y² = 4 × 100² A; t = 20e6 × 100 / 40000 = 50 kN per bolt on the tension side"),
                [
                    Bolts(("0", "-100"), ("100", "-100"), ("0", "100"), ("100", "100")),
                    CalcInputValue.Quantity("F_x", 0, "kN"), CalcInputValue.Quantity("F_y", 0, "kN"), CalcInputValue.Quantity("e_x", 0, "mm"), CalcInputValue.Quantity("e_y", 0, "mm"),
                    CalcInputValue.Quantity("T_z", 0, "kN*m"), CalcInputValue.Quantity("N", 0, "kN"), CalcInputValue.Quantity("M_x", 20, "kN*m"), CalcInputValue.Quantity("M_y", 0, "kN*m"),
                    CalcInputValue.Quantity("V_R", 40, "kN"), CalcInputValue.Quantity("T_R", 60, "kN"),
                ],
                [new CalcExpectedValue("t_max", 50, "kN", 1e-6), new CalcExpectedValue("u_max", 0.83333, "", 1e-4)]),
        ],
        DiagramKey: "bolt-group-eccentric-shear",
        Category: "Fasteners");

    private static CalcInputValue Bolts(params (string X, string Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(double.Parse(p.X, System.Globalization.CultureInfo.InvariantCulture), "mm")), ("y", new CalcCell(double.Parse(p.Y, System.Globalization.CultureInfo.InvariantCulture), "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("bolts");

        foreach (var s in new[] { "F_x", "F_y", "e_x", "e_y", "T_z", "N", "M_x", "M_y", "V_R", "T_R" })
            c.Input(s);

        if (rows.Count < 2)
        {
            c.Block("few-bolts", "bolts: at least two bolts are needed.");
            return;
        }

        var mm = UnitCatalog.Parse("mm");
        var kN = UnitCatalog.Parse("kN");
        var bolts = rows.Select((r, i) => (
            X: Si(r, "x"), Y: Si(r, "y"), A: Si(r, "A"), Index: i + 1)).ToList();

        for (var i = 0; i < bolts.Count; i++)
        {
            for (var j = i + 1; j < bolts.Count; j++)
            {
                if (Math.Abs(bolts[i].X - bolts[j].X) < 1e-9 && Math.Abs(bolts[i].Y - bolts[j].Y) < 1e-9)
                {
                    c.Block("coincident", $"bolts: rows {i + 1} and {j + 1} share a position.");
                    return;
                }
            }
        }

        var sumA = bolts.Sum(b => b.A);
        var xc = bolts.Sum(b => b.A * b.X) / sumA;
        var yc = bolts.Sum(b => b.A * b.Y) / sumA;
        c.Record("n_b", "number of bolts", $"{bolts.Count}", new ScalarValue(bolts.Count), "Bolts in the group");
        c.Record("x_c", "Σ A x / Σ A", string.Join(" + ", bolts.Select(b => $"{F(b.A, 1e-6)}×{F(b.X, 1e-3)}")) + $" / {F(sumA, 1e-6)}", ScalarValue.Of(mm.FromSi(xc), mm), "Centroid x");
        c.Record("y_c", "Σ A y / Σ A", string.Join(" + ", bolts.Select(b => $"{F(b.A, 1e-6)}×{F(b.Y, 1e-3)}")) + $" / {F(sumA, 1e-6)}", ScalarValue.Of(mm.FromSi(yc), mm), "Centroid y");

        var fx = c.Si("F_x");
        var fy = c.Si("F_y");
        var ex = c.Si("e_x") - xc;
        var ey = c.Si("e_y") - yc;
        var torque = fy * ex - fx * ey + c.Si("T_z");
        c.Record("T", "F_y (e_x − x_c) − F_x (e_y − y_c) + T_z", $"{F(fy, 1e3)} × {F(ex, 1e-3)} − {F(fx, 1e3)} × {F(ey, 1e-3)} + {F(c.Si("T_z"), 1e3)}", ScalarValue.Of(torque / 1e3, "kN*m"), "In-plane torque about the centroid");

        // J with areas in mm² and distances in mm → mm⁴ (A r²); SI m⁴.
        var polar = bolts.Sum(b => b.A * ((b.X - xc) * (b.X - xc) + (b.Y - yc) * (b.Y - yc)));
        var sumAy2 = bolts.Sum(b => b.A * (b.Y - yc) * (b.Y - yc));
        var sumAx2 = bolts.Sum(b => b.A * (b.X - xc) * (b.X - xc));
        c.Record("J", "Σ A r²", "about the centroid", new ScalarValue(polar, Dimension.L.Pow(4), UnitCatalog.Parse("mm^4")), "Polar second moment of the bolt areas");

        var n = c.Si("N");
        var mx = c.Si("M_x");
        var my = c.Si("M_y");
        var a = c.Has("a") ? c.InputSi("a") : (double?)null;
        var b = c.Has("b") ? c.InputSi("b") : (double?)null;
        var prying = a is { } pa && b is { } pb && pa > 0 ? 1 + pb / pa : 1.0;

        if (prying != 1.0)
            c.Record("k_pry", "1 + b / a", $"1 + {F(b!.Value, 1e-3)} / {F(a!.Value, 1e-3)}", new ScalarValue(prying), "Prying factor on bolt tension");

        var vR = c.Si("V_R");
        var tR = c.Si("T_R");
        var worst = (Index: 0, V: 0.0, T: 0.0, U: -1.0);
        var shear = new List<(double X, double Y)>();

        foreach (var bolt in bolts)
        {
            var rx = bolt.X - xc;
            var ry = bolt.Y - yc;
            var vx = bolt.A / sumA * fx + (polar > 0 ? -torque * bolt.A * ry / polar : 0);
            var vy = bolt.A / sumA * fy + (polar > 0 ? torque * bolt.A * rx / polar : 0);
            var v = Math.Sqrt(vx * vx + vy * vy);
            var t = bolt.A / sumA * n + (sumAy2 > 0 ? mx * bolt.A * ry / sumAy2 : 0) + (sumAx2 > 0 ? my * bolt.A * rx / sumAx2 : 0);
            var tp = Math.Max(t, 0) * prying;
            var u = Math.Sqrt((v / vR) * (v / vR) + (tp / tR) * (tp / tR));
            shear.Add((rx, ry));
            c.Record($"b{bolt.Index}", $"bolt {bolt.Index} at ({F(bolt.X, 1e-3)}, {F(bolt.Y, 1e-3)}) mm", $"v = ({F(vx, 1e3)}, {F(vy, 1e3)}) kN, t = {F(tp, 1e3)} kN", new ScalarValue(u), $"Interaction utilisation √((v/V_R)² + (t/T_R)²) = {CalcValue.FormatNumber(u, 3)}");

            if (u > worst.U)
                worst = (bolt.Index, v, tp, u);
        }

        c.Record("v_max", "governing bolt shear", $"bolt {worst.Index}", ScalarValue.Of(worst.V / 1e3, kN), "Maximum bolt shear");
        c.Record("t_max", "governing bolt tension (with prying)", $"bolt {worst.Index}", ScalarValue.Of(worst.T / 1e3, kN), "Maximum bolt tension");
        c.Record("u_max", "√((v/V_R)² + (t/T_R)²)", $"√(({F(worst.V, 1e3)}/{F(vR, 1e3)})² + ({F(worst.T, 1e3)}/{F(tR, 1e3)})²)", new ScalarValue(worst.U), "Governing interaction utilisation");
        c.Record("governing", "governing bolt", $"bolt {worst.Index}", new TextValue($"bolt {worst.Index}"), "The bolt with the highest utilisation");
        c.Assign("u_v", "v_max / V_R", null, "Shear utilisation of the governing bolt");
        c.Assign("u_t", "t_max / T_R", null, "Tension utilisation of the governing bolt");

        c.Output("T", "Torque about the centroid");
        c.Output("J", "Polar second moment of bolt areas");
        c.Output("v_max", "Maximum bolt shear");
        c.Output("t_max", "Maximum bolt tension");
        c.Output("u_max", "Governing interaction utilisation");
        c.Output("governing", "Governing bolt");
        c.CheckUtilisation("Shear–tension interaction (governing bolt)", "u_max", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Shear alone", "u_v");
        c.CheckUtilisation("Tension alone", "u_t");
        c.Series(new CalcSeries("Bolt pattern", "x", "mm", "y", "mm", bolts.Select(bb => (mm.FromSi(bb.X), mm.FromSi(bb.Y))).ToList()));
    }

    private static double Si(CalcRow row, string key)
    {
        var cell = row.Get(key) ?? throw new CalcEvaluationException($"bolts: column '{key}' is missing.");
        return UnitCatalog.Parse(cell.Unit ?? (key == "A" ? "mm^2" : "mm")).ToSi(cell.Number ?? throw new CalcEvaluationException($"bolts: column '{key}' needs a number."));
    }

    private static string F(double si, double unitFactor) => CalcValue.FormatNumber(si / unitFactor, 5);
}
