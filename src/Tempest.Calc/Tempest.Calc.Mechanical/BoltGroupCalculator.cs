using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M3b — bolt group by the elastic method: in-plane forces and torsion, out-of-plane moments and axial load, per-bolt shear and tension, governing bolt, optional simple prying.</summary>
public sealed class BoltGroupCalculator : ICalcCalculator
{
    public const string Id = "mech.bolt-group";

    private static CalcReferenceSource Legacy(int n) =>
        new("TempestOS calc.bolt-group-eccentric-shear (v0.21.0) hand calculation", null, $"docs/engineering/calculations/calc.bolt-group-eccentric-shear.md, Example {n}");

    /// <summary>A legacy in-plane shear case: unit bolt areas (forces do not depend on them, and J in mm⁴ then equals the legacy unit polar moment in mm²), no tension.</summary>
    private static CalcInputValue[] Shear((double X, double Y)[] bolts, double fx, double fy, double ex, double ey, double vR) =>
    [
        CalcInputValue.Table("bolts", [.. bolts.Select(b => CalcRow.Of(("x", new CalcCell(b.X, "mm")), ("y", new CalcCell(b.Y, "mm")), ("A", new CalcCell(1, "mm^2"))))]),
        CalcInputValue.Quantity("F_x", fx, "kN"), CalcInputValue.Quantity("F_y", fy, "kN"), CalcInputValue.Quantity("e_x", ex, "mm"), CalcInputValue.Quantity("e_y", ey, "mm"),
        CalcInputValue.Quantity("T_z", 0, "kN*m"), CalcInputValue.Quantity("N", 0, "kN"), CalcInputValue.Quantity("M_x", 0, "kN*m"), CalcInputValue.Quantity("M_y", 0, "kN*m"),
        CalcInputValue.Quantity("V_R", vR, "kN"), CalcInputValue.Quantity("T_R", 45, "kN"),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> Columns =
    [
        new("x", "x", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("y", "y", CalcInputKind.Quantity, "mm", HardMin: -1e5, HardMax: 1e5),
        new("A", "Shear/stress area", CalcInputKind.Quantity, "mm^2", HardMin: 1e-6, HardMax: 1e6),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.4.0", CalcDiscipline.Mechanical, "Bolt group (elastic method)",
        "A pattern of bolts in a plane under in-plane forces (with eccentricity, so torsion), out-of-plane moments and axial load: the shear and tension in every bolt by the elastic (area-weighted) method, the governing bolt, and an optional lever-model prying addition.",
        "Centroid of the bolt group, each bolt weighted by its area: x_c = Σ A x / Σ A (y_c likewise).\nTorsion about the centroid from the eccentric load: T = F_y (e_x − x_c) − F_x (e_y − y_c) + T_z.\nPolar moment of the bolt areas about the centroid: J = Σ A r².\nShear on each bolt, its direct share plus the torsion (vector sum): v = A (F/ΣA) + T·A·r/J.\nTension on each bolt, its direct share plus the moments, with the product of inertia so that an unsymmetric pattern is right: t = A N/ΣA + A·[(M_x I_y − M_y I_xy) y' + (M_y I_x − M_x I_xy) x']/(I_x I_y − I_xy²), where I_x = Σ A y'², I_y = Σ A x'², I_xy = Σ A x'y' (this reduces to M_x A y'/Σ A y'² + M_y A x'/Σ A x'² only for a pattern symmetric about a centroidal axis).\nTension with prying, when the distances a and b are given: t_p = t (1 + b/a).\nCombined shear and tension, circular interaction (elastic method, Shigley-style): (v/V_R)² + (t_p/T_R)² ≤ 1.\nCombined shear and tension, EN 1993-1-8 linear form (each is also checked alone; the highest governs): v/V_R + t_p/(1.4 T_R) ≤ 1.",
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
            new CalcApplicabilityRule("few-bolts", "No bolts, or a single bolt under an in-plane torque it cannot resist.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("coincident", "Two bolts share a position.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("collinear", "All bolts on one line: an out-of-plane moment about that line cannot be resisted.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
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
            new CalcTestCase("ka-unsymmetric-bending", "Four bolts in an unsymmetric pattern under an out-of-plane moment: the product moment matters",
                new CalcReferenceSource("Analytic (unsymmetric bending; recomputed in python3 by solving the moment equilibrium of t = A(a·x′ + b·y′))", null, "bolts at (0,0), (120,0), (60,80), (0,160) mm, A = 84.3 mm², M_x = 12 kN·m: centroid (45, 60), I_x = 1 483 680, I_y = 834 570, I_xy = −505 800 mm⁴; bolt tensions −75, −12.5, 25 and 62.5 kN (the symmetric-axes form gives 68.18 kN at the top bolt)"),
                [
                    Bolts(("0", "0"), ("120", "0"), ("60", "80"), ("0", "160")),
                    CalcInputValue.Quantity("F_x", 0, "kN"), CalcInputValue.Quantity("F_y", 0, "kN"), CalcInputValue.Quantity("e_x", 0, "mm"), CalcInputValue.Quantity("e_y", 0, "mm"),
                    CalcInputValue.Quantity("T_z", 0, "kN*m"), CalcInputValue.Quantity("N", 0, "kN"), CalcInputValue.Quantity("M_x", 12, "kN*m"), CalcInputValue.Quantity("M_y", 0, "kN*m"),
                    CalcInputValue.Quantity("V_R", 1000, "kN"), CalcInputValue.Quantity("T_R", 100, "kN"),
                ],
                [new CalcExpectedValue("t_max", 62.5, "kN", 1e-9), new CalcExpectedValue("u_max", 0.625, "", 1e-9)]),
            new CalcTestCase("ka-legacy-four-bolts", "Legacy example 1: four bolts, vertical load beside the group", Legacy(1), Shear([(75, 50), (75, -50), (-75, 50), (-75, -50)], 0, -20, 250, 0, 25),
                [new CalcExpectedValue("T", -5.0000, "kN*m", 5e-5), new CalcExpectedValue("J", 32_500, "mm^4", 5e-5), new CalcExpectedValue("v_max", 18.240, "kN", 5e-5), new CalcExpectedValue("u_max", 0.72959, "", 5e-5)]),
            new CalcTestCase("ka-legacy-line", "Legacy example 2: three bolts in a line, load beside the line", Legacy(2), Shear([(0, 0), (0, 80), (0, 160)], 0, -30, 120, 80, 20),
                [new CalcExpectedValue("T", -3.6000, "kN*m", 5e-5), new CalcExpectedValue("J", 12_800, "mm^4", 5e-5), new CalcExpectedValue("v_max", 24.622, "kN", 5e-5), new CalcExpectedValue("u_max", 1.2311, "", 5e-5)]),
            new CalcTestCase("ka-legacy-inclined", "Legacy example 3: the line of bolts under an inclined load", Legacy(3), Shear([(0, 0), (0, 80), (0, 160)], 15, -30, 120, 80, 40),
                [new CalcExpectedValue("T", -3.6000, "kN*m", 5e-5), new CalcExpectedValue("J", 12_800, "mm^4", 5e-5), new CalcExpectedValue("v_max", 29.262, "kN", 5e-5), new CalcExpectedValue("u_max", 0.73154, "", 5e-5)]),
            new CalcTestCase("ka-legacy-centroid", "Legacy example 4: load through the centroid, shared equally", Legacy(4), Shear([(75, 50), (75, -50), (-75, 50), (-75, -50)], 0, -20, 0, 0, 25),
                [new CalcExpectedValue("T", 0, "kN*m", 1e-9), new CalcExpectedValue("v_max", 5.0000, "kN", 5e-5), new CalcExpectedValue("u_max", 0.20000, "", 5e-5)]),
            new CalcTestCase("ka-legacy-single-bolt", "Legacy calc.bolt-shear-capacity case: one bolt in direct shear", new CalcReferenceSource("TempestOS calc.bolt-shear-capacity (v0.16.0) retired as covered (ADR-0167)", null, "v = 20 kN against V_R = 25 kN: u = 0.8"), Shear([(0, 0)], 0, -20, 0, 0, 25),
                [new CalcExpectedValue("v_max", 20.000, "kN", 5e-5), new CalcExpectedValue("u_max", 0.80000, "", 5e-5)]),
        ],
        DiagramKey: "bolt-group-eccentric-shear",
        Category: "Connections");

    private static CalcInputValue Bolts(params (string X, string Y)[] points) =>
        CalcInputValue.Table("bolts", points.Select(p => CalcRow.Of(("x", new CalcCell(double.Parse(p.X, System.Globalization.CultureInfo.InvariantCulture), "mm")), ("y", new CalcCell(double.Parse(p.Y, System.Globalization.CultureInfo.InvariantCulture), "mm")), ("A", new CalcCell(84.3, "mm^2")))).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_lin", "u_max", "u_t", "u_v");
        var rows = c.Rows("bolts");

        foreach (var s in new[] { "F_x", "F_y", "e_x", "e_y", "T_z", "N", "M_x", "M_y", "V_R", "T_R" })
            c.Input(s);

        if (rows.Count == 0)
        {
            c.Block("few-bolts", "bolts: at least one bolt is needed.");
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

        var group = GroupGeometry.OfPoints([.. bolts.Select(b => (b.X, b.Y, b.A))]);
        var sumA = group.Size;
        var xc = group.Xc;
        var yc = group.Yc;
        c.Record("n_b", "number of bolts", $"{bolts.Count}", new ScalarValue(bolts.Count), "Bolts in the group");
        c.Record("x_c", "Σ A x / Σ A", string.Join(" + ", bolts.Select(b => $"{F(b.A, 1e-6)}×{F(b.X, 1e-3)}")) + $" / {F(sumA, 1e-6)}", ScalarValue.Of(mm.FromSi(xc), mm), "Centroid x");
        c.Record("y_c", "Σ A y / Σ A", string.Join(" + ", bolts.Select(b => $"{F(b.A, 1e-6)}×{F(b.Y, 1e-3)}")) + $" / {F(sumA, 1e-6)}", ScalarValue.Of(mm.FromSi(yc), mm), "Centroid y");

        var fx = c.Si("F_x");
        var fy = c.Si("F_y");
        var ex = c.Si("e_x") - xc;
        var ey = c.Si("e_y") - yc;
        var torque = fy * ex - fx * ey + c.Si("T_z");
        c.Record("T", "F_y (e_x − x_c) − F_x (e_y − y_c) + T_z", $"{F(fy, 1e3)} × {F(ex, 1e-3)} − {F(fx, 1e3)} × {F(ey, 1e-3)} + {F(c.Si("T_z"), 1e3)}", ScalarValue.Of(torque / 1e3, "kN*m"), "In-plane torque about the centroid");

        // A single bolt takes direct shear only (the legacy calc.bolt-shear-capacity case): any torque about it is unresisted.
        if (bolts.Count == 1 && Math.Abs(torque) > 1e-9 * Math.Max(1, Math.Sqrt(fx * fx + fy * fy)))
        {
            c.Block("few-bolts", "bolts: a single bolt cannot resist the in-plane torque; put the load through the bolt or add bolts.");
            return;
        }

        // J with areas in mm² and distances in mm → mm⁴ (A r²); SI m⁴.
        var polar = group.J;
        c.Record("J", "Σ A r²", "about the centroid", new ScalarValue(polar, Dimension.L.Pow(4), UnitCatalog.Parse("mm^4")), "Polar second moment of the bolt areas");

        var n = c.Si("N");
        var mx = c.Si("M_x");
        var my = c.Si("M_y");

        var a = c.Has("a") ? c.InputSi("a") : (double?)null;
        var b = c.Has("b") ? c.InputSi("b") : (double?)null;
        var prying = a is { } pa && b is { } pb && pa > 0 ? 1 + pb / pa : 1.0;

        if (prying != 1.0)
            c.Record("k_pry", "1 + b / a", $"1 + {F(b!.Value, 1e-3)} / {F(a!.Value, 1e-3)}", new ScalarValue(prying), "Prying factor on bolt tension");

        if ((mx != 0 || my != 0) && group.Bending(mx, my, 1, 1) is null)
        {
            c.Block("collinear", "M_x: all bolts lie on one line and the out-of-plane moment has a component about that line, which no bolt resists; add a bolt off the line or remove that component.");
            return;
        }

        var vR = c.Si("V_R");
        var tR = c.Si("T_R");
        var worst = (Index: 0, V: 0.0, T: 0.0, U: -1.0);
        var worstLin = (Index: 0, V: 0.0, T: 0.0, U: -1.0);
        var maxShear = (Index: 0, V: -1.0);
        var maxTension = (Index: 0, T: -1.0);
        var shear = new List<(double X, double Y)>();

        foreach (var bolt in bolts)
        {
            var rx = bolt.X - xc;
            var ry = bolt.Y - yc;
            var vx = bolt.A / sumA * fx + (polar > 0 ? -torque * bolt.A * ry / polar : 0);
            var vy = bolt.A / sumA * fy + (polar > 0 ? torque * bolt.A * rx / polar : 0);
            var v = Math.Sqrt(vx * vx + vy * vy);
            var t = bolt.A / sumA * n + bolt.A * (group.Bending(mx, my, rx, ry) ?? 0);
            var tp = Math.Max(t, 0) * prying;
            var u = Math.Sqrt((v / vR) * (v / vR) + (tp / tR) * (tp / tR));
            shear.Add((rx, ry));
            c.Record($"b{bolt.Index}", $"bolt {bolt.Index} at ({F(bolt.X, 1e-3)}, {F(bolt.Y, 1e-3)}) mm", $"v = ({F(vx, 1e3)}, {F(vy, 1e3)}) kN, t = {F(tp, 1e3)} kN", new ScalarValue(u), $"Interaction utilisation √((v/V_R)² + (t/T_R)²) = {CalcValue.FormatNumber(u, 3)}");

            if (u > worst.U)
                worst = (bolt.Index, v, tp, u);

            // The group maxima of shear and of tension, each at its own bolt (mech.bolt-group-04): they are not the values at the governing bolt.
            if (v > maxShear.V)
                maxShear = (bolt.Index, v);

            if (tp > maxTension.T)
                maxTension = (bolt.Index, tp);

            // The linear form of EN 1993-1-8 Table 3.4 is a different surface from the circular one: another bolt can govern it, so it is evaluated at every bolt.
            var uLin = v / vR + tp / (1.4 * tR);

            if (uLin > worstLin.U)
                worstLin = (bolt.Index, v, tp, uLin);
        }

        c.Record("v_max", "max over the bolts of v", $"bolt {maxShear.Index}", ScalarValue.Of(maxShear.V / 1e3, kN), "Maximum shear on any bolt");
        c.Record("t_max", "max over the bolts of t (with prying)", $"bolt {maxTension.Index}", ScalarValue.Of(maxTension.T / 1e3, kN), "Maximum tension on any bolt");
        c.Record("bolt_v", "bolt with the largest shear", $"bolt {maxShear.Index}", new TextValue($"bolt {maxShear.Index}"), "The bolt carrying the largest shear");
        c.Record("bolt_t", "bolt with the largest tension", $"bolt {maxTension.Index}", new TextValue($"bolt {maxTension.Index}"), "The bolt carrying the largest tension");
        c.Record("u_max", "√((v/V_R)² + (t/T_R)²)", $"√(({F(worst.V, 1e3)}/{F(vR, 1e3)})² + ({F(worst.T, 1e3)}/{F(tR, 1e3)})²)", new ScalarValue(worst.U), "Governing interaction utilisation");
        c.Record("governing", "governing bolt", $"bolt {worst.Index}", new TextValue($"bolt {worst.Index}"), "The bolt with the highest utilisation");
        c.Assign("u_v", "v_max / V_R", null, "Shear utilisation of the most sheared bolt");
        c.Assign("u_t", "t_max / T_R", null, "Tension utilisation of the most tensioned bolt");

        c.Output("T", "Torque about the centroid");
        c.Output("J", "Polar second moment of bolt areas");
        c.Output("v_max", "Maximum shear on any bolt");
        c.Output("t_max", "Maximum tension on any bolt");
        c.Output("bolt_v", "Bolt with the largest shear");
        c.Output("bolt_t", "Bolt with the largest tension");
        c.Output("u_max", "Governing interaction utilisation");
        c.Output("governing", "Governing bolt");
        c.Record("governing_lin", "bolt with the highest linear utilisation", $"bolt {worstLin.Index}", new TextValue($"bolt {worstLin.Index}"), "The bolt that governs the linear shear–tension check");
        c.Record("u_lin", "max over the bolts of v/V_R + t/(1.4 T_R)", $"bolt {worstLin.Index}: {F(worstLin.V, 1e3)}/{F(vR, 1e3)} + {F(worstLin.T, 1e3)}/(1.4 × {F(tR, 1e3)})", new ScalarValue(worstLin.U), "Linear shear–tension interaction, the highest over all bolts");
        c.Output("u_lin", "Linear interaction utilisation (highest over all bolts)");
        c.Output("governing_lin", "Bolt governing the linear interaction");
        c.CheckUtilisation("Circular shear–tension interaction (governing bolt)", "u_max");
        c.CheckUtilisation("Linear shear–tension interaction v/V_R + t/(1.4 T_R), every bolt", "u_lin", "EN 1993-1-8 Table 3.4");
        c.CheckUtilisation("Shear alone (largest bolt shear)", "u_v");
        c.CheckUtilisation("Tension alone (largest bolt tension)", "u_t");
        c.Series(new CalcSeries("Bolt pattern", "x", "mm", "y", "mm", bolts.Select(bb => (mm.FromSi(bb.X), mm.FromSi(bb.Y))).ToList()));
    }

    private static double Si(CalcRow row, string key)
    {
        var cell = row.Get(key) ?? throw new CalcEvaluationException($"bolts: column '{key}' is missing.");
        return UnitCatalog.Parse(cell.Unit ?? (key == "A" ? "mm^2" : "mm")).ToSi(cell.Number ?? throw new CalcEvaluationException($"bolts: column '{key}' needs a number."));
    }

    private static string F(double si, double unitFactor) => CalcValue.FormatNumber(si / unitFactor, 5);
}
