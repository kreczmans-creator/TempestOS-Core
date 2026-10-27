using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

public sealed partial class MechanismCalculator
{
    public const string CamMode = "Cam";

    public const string Roller = "Roller";
    public const string FlatFaced = "Flat-faced";

    public const string Rise = "Rise";
    public const string Dwell = "Dwell";
    public const string Return = "Return";

    public const string Cycloidal = "Cycloidal";
    public const string ModifiedTrapezoid = "Modified trapezoid";
    public const string Poly345 = "3-4-5 polynomial";
    public const string Poly4567 = "4-5-6-7 polynomial";

    private const string CamClause = "Cam follower kinematics";

    private static readonly IReadOnlyList<CalcColumnSpec> SegmentColumns =
    [
        new("kind", "Segment", CalcInputKind.Choice, Choices: [Rise, Dwell, Return]),
        new("law", "Motion law (rise, return)", CalcInputKind.Choice, Choices: [Cycloidal, ModifiedTrapezoid, Poly345, Poly4567], IsOptional: true),
        new("beta", "Cam angle β", CalcInputKind.Quantity, "deg", HardMin: 0.01, HardMax: 360),
        new("h", "Lift h (rise, return; ignored for a dwell)", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 1000, IsOptional: true),
    ];

    private static IReadOnlyList<string> CamModes => [CamMode];

    private static void AddCamInputs(List<CalcInputSpec> inputs)
    {
        inputs.AddRange(
        [
            new("follower", "Follower type: roller (translating, pressure angle and undercut checked) or flat-faced (in-line)", CalcInputKind.Choice, Choices: [Roller, FlatFaced], DefaultChoice: Roller, IsOptional: true, Group: "Cam"),
            new("Rb", "Base-circle radius of the cam profile", CalcInputKind.Quantity, "mm", Default: 40, Min: 1, Max: 1000, HardMin: 0.1, HardMax: 1e5, IsOptional: true, Group: "Cam"),
            new("R_r", "Roller radius (roller follower)", CalcInputKind.Quantity, "mm", Default: 8, Min: 1, Max: 200, HardMin: 0.01, HardMax: 1e4, IsOptional: true, Group: "Cam"),
            new("phi_max", "Maximum pressure angle allowed (roller follower)", CalcInputKind.Quantity, "deg", Default: 30, Min: 5, Max: 45, HardMin: 0.1, HardMax: 89.9, IsOptional: true, Group: "Cam"),
            new("segs", "Cam segments: rise, dwell or return, with motion law, angle and lift; the angles add to 360° and the rises equal the returns", CalcInputKind.Table, Columns: SegmentColumns, MaxRows: 24, IsOptional: true, Group: "Cam"),
            new("m_f", "Follower mass (all moving parts that follow the cam)", CalcInputKind.Quantity, "kg", Default: 0.2, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Follower loads"),
            new("k_sp", "Return spring rate", CalcInputKind.Quantity, "kN/m", Default: 5, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Follower loads"),
            new("F_pre", "Spring preload at the base circle", CalcInputKind.Quantity, "N", Default: 20, Min: 0, Max: 100000, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Follower loads"),
            new("F_ext", "External load on the follower, positive against the rise", CalcInputKind.Quantity, "N", Default: 0, Min: -100000, Max: 100000, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Follower loads"),
        ]);
    }

    private static void AddCamRules(List<CalcApplicabilityRule> rules)
    {
        rules.Add(new CalcApplicabilityRule("segments", "The cam segments do not form a cam: angles not adding to 360°, rises and returns not cancelling, a rise or return without a law or a lift, or a return below the base circle.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative));
        rules.Add(new CalcApplicabilityRule("cam-geometry", "The follower offset reaches the prime-circle radius.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative));
        rules.Add(new CalcApplicabilityRule("contact", "The follower force falls below zero, so the follower leaves the cam. A stronger spring or a smaller acceleration is needed.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative));
    }

    /// <summary>One row of the segment table, for callers that build inputs (examples, tests).</summary>
    public static CalcRow Row(string kind, string? law, double betaDeg, double liftMm) =>
        CalcRow.Of(("kind", new CalcCell(Text: kind)), ("law", law is null ? new CalcCell() : new CalcCell(Text: law)), ("beta", new CalcCell(betaDeg, "deg")), ("h", new CalcCell(liftMm, "mm")));

    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    /// <summary>The cam segments of the table, or <see langword="null"/> (with a block raised) when they are not a cam.</summary>
    private static CamProfile? ReadSegments(CalcRunContext c)
    {
        var rows = c.Rows("segs");

        if (rows.Count == 0)
        {
            c.Block("missing", c.EnterRows("segs"));
            return null;
        }

        c.Input("segs");
        var segments = new List<CamSegment>();
        var deg = UnitCatalog.Parse("deg");
        var mm = UnitCatalog.Parse("mm");

        for (var i = 0; i < rows.Count; i++)
        {
            var kind = rows[i].Get("kind")?.Text switch
            {
                Rise => CamSegmentKind.Rise,
                Return => CamSegmentKind.Return,
                _ => CamSegmentKind.Dwell,
            };

            var law = rows[i].Get("law")?.Text switch
            {
                ModifiedTrapezoid => CamLaw.ModifiedTrapezoid,
                Poly345 => CamLaw.Poly345,
                Poly4567 => CamLaw.Poly4567,
                _ => CamLaw.Cycloidal,
            };

            var beta = Cell(rows[i], "beta", deg);
            var lift = kind == CamSegmentKind.Dwell ? 0 : Cell(rows[i], "h", mm);

            if (kind != CamSegmentKind.Dwell && (rows[i].Get("law")?.Text is not { Length: > 0 } || !(lift > 0)))
            {
                c.Block("segments", $"segs[{i + 1}]: a {kind.ToString().ToLowerInvariant()} needs a motion law and a lift above zero. Enter them or make the segment a dwell.");
                return null;
            }

            segments.Add(new CamSegment(kind, law, beta, lift));
        }

        if (CamProfile.Validate(segments) is { } problem)
        {
            c.Block("segments", $"segs: {problem}. Enter segments whose angles add to 360° and whose rises and returns cancel.");
            return null;
        }

        return new CamProfile(segments);
    }

    private static string LawName(CamLaw law) => law switch
    {
        CamLaw.ModifiedTrapezoid => ModifiedTrapezoid,
        CamLaw.Poly345 => Poly345,
        CamLaw.Poly4567 => Poly4567,
        _ => Cycloidal,
    };

    private static void RunCam(CalcRunContext c)
    {
        if (!Require(c, "Rb"))
            return;

        var roller = !c.Has("follower") || c.InputText("follower") == Roller;

        if (roller && !Require(c, "R_r"))
            return;

        foreach (var symbol in new[] { "m_f", "k_sp", "F_pre", "F_ext", "phi_max", "e_off" })
        {
            if (c.Has(symbol) && (roller || symbol != "phi_max" && symbol != "e_off"))
                c.Input(symbol);
        }

        if (c.Has("follower"))
            c.Input("follower");

        var profile = ReadSegments(c);

        if (profile is null)
            return;

        var rb = c.Si("Rb");
        var rr = roller ? c.Si("R_r") : 0;
        var e = roller && c.Has("e_off") ? c.Si("e_off") : 0;
        var mass = c.Has("m_f") ? c.Si("m_f") : 0;
        var rate = c.Has("k_sp") ? c.Si("k_sp") : 0;
        var preload = c.Has("F_pre") ? c.Si("F_pre") : 0;
        var external = c.Has("F_ext") ? c.Si("F_ext") : 0;
        var phiReq = roller && c.Has("phi_max") ? c.Si("phi_max") : 30 * Math.PI / 180;
        var omega = c.Si("n_in");
        var steps = (int)Math.Round(c.Si("steps"));

        if (roller && !(Math.Abs(e) < rb + rr))
        {
            c.Block("cam-geometry", $"e_off: the follower offset {F(Mm(Math.Abs(e)))} mm must be smaller than the prime-circle radius R_b + R_r = {F(Mm(rb + rr))} mm.");
            return;
        }

        if (roller && !c.Has("phi_max"))
            c.Assign("phi_max", "30 deg", "deg", "Maximum pressure angle allowed (the default, as none was entered)");

        var cam = new CamAnalysis(profile, roller, rb, rr, e, omega, mass, rate, preload, external);
        var count = profile.Count;

        // Per-segment peaks of the continuous motion: a scan of the segment refined by golden section.
        (double Value, double At)? SegPeak(int k, Func<CamPoint, double> select, bool max = true) => MechanismPeaks.Find(x => select(cam.In(k, x)), 0, 1, 400, max);

        double Best(Func<int, (double Value, double At)?> peak, bool max)
        {
            var best = double.NaN;
            var any = false;

            for (var k = 0; k < count; k++)
            {
                if (peak(k) is not { } p)
                    continue;

                if (!any || (max ? p.Value > best : p.Value < best))
                    best = p.Value;

                any = true;
            }

            return best;
        }

        var segV = new double[count];
        var segA = new double[count];
        var segJ = new double[count];

        for (var k = 0; k < count; k++)
        {
            segV[k] = SegPeak(k, p => Math.Abs(p.V))!.Value.Value;
            segA[k] = SegPeak(k, p => Math.Abs(p.A))!.Value.Value;
            segJ[k] = SegPeak(k, p => Math.Abs(p.J))!.Value.Value;
        }

        var vPk = segV.Max();
        var aPk = segA.Max();
        var jPk = segJ.Max();
        var lift = profile.MaxDisplacement;
        var fMax = Best(k => SegPeak(k, p => p.Force), true);
        var fMin = Best(k => SegPeak(k, p => p.Force, false), false);
        var fPk = Math.Max(Math.Abs(fMax), Math.Abs(fMin));
        var tPk = Best(k => SegPeak(k, p => Math.Abs(p.Torque)), true);
        var kappaMax = roller ? Best(k => SegPeak(k, p => p.Curvature), true) : 0;
        var radiusMin = roller ? 0 : Best(k => SegPeak(k, p => p.ProfileRadius, false), false);
        var normalPk = Best(k => SegPeak(k, p => Math.Abs(p.NormalForce)), true);
        var phiPk = roller ? Best(k => SegPeak(k, p => Math.Abs(p.PressureAngle)), true) : 0;
        var faceMax = Best(k => SegPeak(k, p => p.FaceDemand), true);

        // The base circle that just keeps the pressure angle at its limit: s₀ ≥ |s′ − e|/tan φ_max − s everywhere.
        var rbMin = 0.0;

        if (roller)
        {
            var tan = Math.Tan(phiReq);
            var s0Req = Best(k => MechanismPeaks.Find(x => { var p = cam.In(k, x); return Math.Abs(p.SPrime - e) / tan - p.S; }, 0, 1, 400), true);
            rbMin = Math.Max(Math.Sqrt(Math.Max(s0Req, 0) * Math.Max(s0Req, 0) + e * e) - rr, 0);
        }

        // Tables at N + 1 cam angles.
        var table = Enumerable.Range(0, steps + 1).Select(i => cam.At(2 * Math.PI * i / steps)).ToList();
        string cols = $"{table.Count} rows, θ from 0° to 360°";
        Table(c, "theta_sw", "θ", cols, table.Select(p => Deg(p.Theta)).ToList(), "deg", "Table: cam angle", CamClause);
        Table(c, "s_sw", "s = s₀ + h·f(θ/β)", cols, table.Select(p => Mm(p.S)).ToList(), "mm", "Table: follower displacement", CamClause);
        Table(c, "v_sw", "v = ω·ds/dθ", cols, table.Select(p => p.V).ToList(), "m/s", "Table: follower velocity", CamClause);
        Table(c, "a_sw", "a = ω²·d²s/dθ²", cols, table.Select(p => p.A).ToList(), "m/s^2", "Table: follower acceleration", CamClause);
        Table(c, "j_sw", "j = ω³·d³s/dθ³", cols, table.Select(p => p.J).ToList(), "m/s^3", "Table: follower jerk", CamClause);

        if (roller)
            Table(c, "phi_sw", "φ = arctan[(s′ − e)/(√(R_p² − e²) + s)]", cols, table.Select(p => Deg(p.PressureAngle)).ToList(), "deg", "Table: pressure angle", CamClause);


        if (roller)
            Table(c, "kappa_sw", "κ = 1/ρ of the pitch curve (positive convex, negative concave)", cols, table.Select(p => p.Curvature / 1e3).ToList(), "1/mm", "Table: curvature of the pitch curve (the radius of curvature is 1/κ where κ is positive)", CamClause);
        else
            Table(c, "rho_sw", "ρ = R_b + s + s″", cols, table.Select(p => Mm(p.ProfileRadius)).ToList(), "mm", "Table: signed radius of curvature of the cam profile (zero or negative: cusp)", CamClause);

        Table(c, "F_sw", "F = m·a + k·s + F_pre + F_ext", cols, table.Select(p => p.Force).ToList(), "N", "Table: follower force on the cam", CamClause);
        Table(c, "T_in_sw", "T = F·ds/dθ", cols, table.Select(p => p.Torque).ToList(), "N*m", "Table: input torque by virtual work", "Virtual work");

        c.Series(new CalcSeries("Follower displacement s", "θ", "deg", "s", "mm", table.Select(p => (Deg(p.Theta), Mm(p.S))).ToList()));
        c.Series(new CalcSeries("Follower velocity v", "θ", "deg", "v", "m/s", table.Select(p => (Deg(p.Theta), p.V)).ToList()));
        c.Series(new CalcSeries("Follower acceleration a", "θ", "deg", "a", "m/s^2", table.Select(p => (Deg(p.Theta), p.A)).ToList()));
        c.Series(new CalcSeries("Follower jerk j", "θ", "deg", "j", "m/s^3", table.Select(p => (Deg(p.Theta), p.J)).ToList()));

        if (roller)
            c.Series(new CalcSeries("Pressure angle φ", "θ", "deg", "φ", "deg", table.Select(p => (Deg(p.Theta), Deg(p.PressureAngle))).ToList()));

        c.Series(new CalcSeries("Follower force F", "θ", "deg", "F", "N", table.Select(p => (Deg(p.Theta), p.Force)).ToList()));
        c.Series(new CalcSeries("Input torque", "θ", "deg", "T_in", "N*m", table.Select(p => (Deg(p.Theta), p.Torque)).ToList()));

        // The segment table: start angle, end displacement and the peak SVAJ of each segment.
        string segCols = $"{count} segments";
        Table(c, "seg_start", "Σ β of the segments before", segCols, Enumerable.Range(0, count).Select(k => Deg(profile.Start(k))).ToList(), "deg", "Segments: start angle", CamClause);
        Table(c, "seg_beta", "β", segCols, Enumerable.Range(0, count).Select(k => Deg(profile.Segment(k).Beta)).ToList(), "deg", "Segments: angle", CamClause);
        Table(c, "seg_s_end", "s at the end of the segment", segCols, Enumerable.Range(0, count).Select(k => Mm(profile.Base(k + 1))).ToList(), "mm", "Segments: displacement at the end", CamClause);
        Table(c, "seg_v_pk", "max |v| in the segment", segCols, segV, "m/s", "Segments: peak velocity", CamClause);
        Table(c, "seg_a_pk", "max |a| in the segment", segCols, segA, "m/s^2", "Segments: peak acceleration", CamClause);
        Table(c, "seg_j_pk", "max |j| in the segment", segCols, segJ, "m/s^3", "Segments: peak jerk", CamClause);
        c.Output("seg_law", "Segments: kind and motion law", new TextValue(string.Join("; ", Enumerable.Range(0, count).Select(k => { var s = profile.Segment(k); return s.Kind == CamSegmentKind.Dwell ? $"{k + 1}: dwell" : $"{k + 1}: {s.Kind.ToString().ToLowerInvariant()} ({LawName(s.Law)})"; }))));

        Scalar(c, "lift", "max s", "over the revolution", Mm(lift), "mm", "Greatest follower displacement", CamClause);
        Scalar(c, "v_pk", "max |v|", "scan of each segment, refined", vPk, "m/s", "Peak follower velocity", CamClause);
        Scalar(c, "a_pk", "max |a|", "scan of each segment, refined", aPk, "m/s^2", "Peak follower acceleration", CamClause);
        Scalar(c, "j_pk", "max |j|", "scan of each segment, refined", jPk, "m/s^3", "Peak follower jerk", CamClause);
        Scalar(c, "F_max", "max F", "scan of each segment, refined", fMax, "N", "Greatest follower force on the cam", CamClause);
        Scalar(c, "F_min", "min F", "scan of each segment, refined", fMin, "N", "Smallest follower force on the cam (negative: the follower leaves the cam)", CamClause);

        if (roller)
        {
            Scalar(c, "phi_max_pk", "max |φ|", "scan of each segment, refined", Deg(phiPk), "deg", "Largest pressure angle", CamClause);
            Scalar(c, "Rb_min_phi", "R_b that keeps φ at its limit", "s₀ = max(|s′ − e|/tan φ_max − s), R_p = √(s₀² + e²)", Mm(rbMin), "mm", "Smallest base-circle radius that keeps the pressure angle within its limit (roller follower)", CamClause);
        }
        else
        {
            Scalar(c, "w_face", "max |s′|", "either side of the follower axis", Mm(faceMax), "mm", "Face half-width the flat follower must reach either side of its axis", CamClause);
        }

        var limit = roller ? rr : 0.0;

        if (!roller)
        {
            Scalar(c, "rho_min", "min (R_b + s + s″)", "signed, scan of each segment, refined", Mm(radiusMin), "mm", "Smallest radius of curvature of the cam profile (zero or negative: the profile has a cusp and the flat follower cannot follow it)", CamClause);
            c.CheckCondition("Smallest profile radius of curvature against zero", radiusMin > 0, $"ρ_min = {F(Mm(radiusMin))} mm", "> 0 mm", "rho_min", CamClause);
        }
        else if (kappaMax > 1e-9)
        {
            var rhoMin = 1 / kappaMax;
            Scalar(c, "rho_min", "1/κ_max", "pitch curve", Mm(rhoMin), "mm", "Smallest radius of curvature of the pitch curve", CamClause);
            c.Record("u_rho", "R_r / ρ_min", $"{F(Mm(limit))} mm / {F(Mm(rhoMin))} mm", new ScalarValue(limit / rhoMin), "Roller radius against the smallest radius of curvature (undercut at 1 or more)", CamClause);
            c.Output("u_rho", "Roller radius against the smallest radius of curvature");
            c.CheckCondition("Smallest radius of curvature against the roller radius", rhoMin > limit, $"ρ_min = {F(Mm(rhoMin))} mm", $"> {F(Mm(limit))} mm", "rho_min", CamClause);
        }
        else
        {
            c.Output("rho_min", "Smallest radius of curvature", new TextValue("none: the pitch curve is nowhere convex, so it cannot be undercut"));
            c.CheckCondition("Smallest radius of curvature against the roller radius", true, "no convex region", $"> {F(Mm(limit))} mm", "rho_min", CamClause);
        }

        if (roller)
        {
            c.Record("u_phi", "φ_max / φ_allowed", $"{F(Deg(phiPk))}° / {F(Deg(phiReq))}°", new ScalarValue(phiPk / phiReq), "Largest pressure angle against the allowed one", CamClause);
            c.Output("u_phi", "Largest pressure angle against the allowed one");
            c.Check("Largest pressure angle against the allowed pressure angle", "phi_max_pk", "phi_max", CamClause, resultSymbol: "u_phi");
        }

        c.CheckCondition("Smallest follower force against zero", fMin >= 0, $"F_min = {F(fMin)} N", "≥ 0 N", "F_min", CamClause);

        if (fMin < 0)
            c.Warn("contact", $"k_sp: the follower force falls to {F(fMin)} N, so the follower leaves the cam. Use a stiffer spring, a larger preload or a lower speed.");

        // The ports.
        Scalar(c, "input_torque", "max |F·ds/dθ|", "virtual work, scan of each segment, refined", tPk, "N*m", "Peak input torque by virtual work", "Virtual work");
        Scalar(c, "output_motion", "max |v|", "follower", vPk, "m/s", "Peak follower velocity", CamClause);
        Scalar(c, "peak_accel", "max |a|", "follower", aPk, "m/s^2", "Peak follower acceleration", CamClause);
        Scalar(c, "follower_force", "max |F|", "m·a + k·s + F_pre + F_ext", fPk, "N", "Peak axial force of the follower on the cam", CamClause);
        Scalar(c, "F_pin_pk", roller ? "max |F|/cos φ" : "max |F|", roller ? "roller contact normal force" : "flat-faced contact force", normalPk, "N", "Peak contact normal force on the cam, the load on the cam shaft's bearings", CamClause);
        Scalar(c, "F_spring_max", "k·h + F_pre", "spring force at full lift, no inertia", rate * lift + preload, "N", "Spring force at full lift", CamClause);

        // RMS torque and mean power over the revolution, Simpson per segment.
        double sumT = 0, sumT2 = 0;

        for (var k = 0; k < count; k++)
        {
            const int m = 400;
            var beta = profile.Segment(k).Beta;
            double a1 = 0, a2 = 0;

            for (var i = 0; i <= m; i++)
            {
                var weight = i == 0 || i == m ? 1 : i % 2 == 1 ? 4 : 2;
                var t = cam.In(k, (double)i / m).Torque;
                a1 += weight * t;
                a2 += weight * t * t;
            }

            sumT += beta * a1 / (3.0 * m);
            sumT2 += beta * a2 / (3.0 * m);
        }

        Scalar(c, "T_rms_in", "√(mean T²)", "Simpson per segment", Math.Sqrt(sumT2 / (2 * Math.PI)), "N*m", "RMS input torque over the cam revolution", "Virtual work");
        PeakPower(c, omega, tPk);
        Scalar(c, "P_mean", "ω·mean(T)", "Simpson per segment", omega * sumT / (2 * Math.PI) / 1e3, "kW", "Mean input power over the cam revolution", "Virtual work");
        c.Output("n_in", "Cam-shaft speed, passed through");
        Scalar(c, "f_in", "n_in / 60", "rpm to Hz", omega / (2 * Math.PI), "Hz", "Cam-shaft frequency, for a spring surge check");
    }

    private static CalcInputValue[] CamInputs(IReadOnlyList<CalcRow> segments, double rpm, double rb, double rr, double offset, double mass, double rate, double preload, double external, string follower = Roller) =>
    [
        CalcInputValue.Choice("mode", CamMode), CalcInputValue.Choice("follower", follower),
        Q("Rb", rb, "mm"), Q("R_r", rr, "mm"), Q("e_off", offset, "mm"), Q("phi_max", 30, "deg"),
        CalcInputValue.Table("segs", segments), CalcInputValue.Of("steps", 72), Q("n_in", rpm, "rpm"),
        Q("m_f", mass, "kg"), Q("k_sp", rate, "kN/m"), Q("F_pre", preload, "N"), Q("F_ext", external, "N"),
    ];

    private const string CamOracle = "Independent python3 recomputation: the four laws from their closed forms, SVAJ per segment, pitch-curve curvature from a fourth-order finite-difference stencil of the parametric pitch curve, peaks by a scan of each segment polished by golden section";

    private static IReadOnlyList<CalcRow> FourSegments(string law, double betaRise, double betaDwell, double liftMm) =>
    [
        Row(Rise, law, betaRise, liftMm), Row(Dwell, null, betaDwell, 0), Row(Return, law, betaRise, liftMm), Row(Dwell, null, betaDwell, 0),
    ];

    private static IReadOnlyList<CalcRow> Mixed() =>
    [
        Row(Rise, Cycloidal, 120, 20), Row(Dwell, null, 60, 0), Row(Return, Poly345, 120, 20), Row(Dwell, null, 60, 0),
    ];

    private static void AddCamCases(List<CalcTestCase> cases)
    {
        cases.AddRange(
        [
            new("ka-cam-cycloidal", "Cycloidal rise and return, 20 mm over 90° at 600 rpm: closed-form peak velocity, acceleration and jerk",
                new CalcReferenceSource(AnalyticSource, null, "Rise 20 mm over β = 90° (cycloidal), dwell 90°, return 20 mm over 90°, dwell 90°, 600 rpm (ω = 20π rad/s), roller R_b 40 mm, R_r 8 mm, in-line; follower 0.3 kg, spring 8 N/mm with 30 N preload, external 10 N: v_pk = 2hω/β = 1.6 m/s exactly, a_pk = 2πhω²/β² = 64π = 201.06193 m/s², j_pk = 4π²hω³/β³ = 50532.375 m/s³; F_max = k·h + F_pre + F_ext = 200 N (at the top dwell), F_min = 40 N (at the base circle); the scan gives φ_max 23.9626°, ρ_min 37.8305 mm, peak T 3.07379 N·m and the smallest R_b for φ ≤ 30° of 27.0189 mm. The pressure angle and curvature are recomputed independently (pitch curve by a 4th-order finite-difference stencil)."),
                CamInputs(FourSegments(Cycloidal, 90, 90, 20), 600, 40, 8, 0, 0.3, 8, 30, 10),
                [X("lift", 20, "mm", 1e-9), X("output_motion", 1.6, "m/s", 1e-9), X("v_pk", 1.6, "m/s", 1e-9), X("peak_accel", 201.0619298297468, "m/s^2", 1e-9), X("j_pk", 50532.37453357752, "m/s^3", 1e-9), X("F_max", 200, "N", 1e-9), X("F_min", 40, "N", 1e-9), X("follower_force", 200, "N", 1e-9), X("input_torque", 3.0737873633237385, "N*m", 1e-7), X("phi_max_pk", 23.9625811239909, "deg", 1e-7), X("rho_min", 37.83054104211253, "mm", 1e-6), X("Rb_min_phi", 27.018890600603154, "mm", 1e-7), X("F_pin_pk", 200, "N", 1e-9), X("F_spring_max", 190, "N", 1e-9), X("lift", 20, "mm", 1e-9), X("T_rms_in", 1.3246676918337672, "N*m", 1e-6), X("P_mean", 0, "W", 1e-6), X("n_in", 600, "rpm", 1e-9)]),
            new("ka-cam-modified-trapezoid", "Modified-trapezoid rise and return, 25 mm over 120° at 300 rpm: C_v = 2, C_a = 8π/(π + 2), C_j = 32π²/(π + 2)",
                new CalcReferenceSource(AnalyticSource, null, "Rise 25 mm over β = 120° (modified trapezoid), dwell 60°, return, dwell 60°, 300 rpm (ω = 10π rad/s), R_b 45 mm, R_r 10 mm, in-line; follower 0.5 kg, spring 6 N/mm, preload 25 N: v_pk = 2hω/β = 0.75 m/s, a_pk = (8π/(π + 2))·hω²/β² = 27.495696 m/s², j_pk = (32π²/(π + 2))·hω³/β³ = 5182.8166 m/s³; F_max = 6·25 + 25 = 175 N, F_min = 25 N; the scan gives φ_max 19.6415°, ρ_min 53.0778 mm, peak T 2.45387 N·m, smallest R_b 19.8387 mm."),
                CamInputs(FourSegments(ModifiedTrapezoid, 120, 60, 25), 300, 45, 10, 0, 0.5, 6, 25, 0),
                [X("lift", 25, "mm", 1e-9), X("output_motion", 0.75, "m/s", 1e-9), X("peak_accel", 27.495696165824583, "m/s^2", 1e-9), X("j_pk", 5182.816624793493, "m/s^3", 1e-9), X("F_max", 175, "N", 1e-9), X("F_min", 25, "N", 1e-9), X("input_torque", 2.453866037128683, "N*m", 1e-7), X("phi_max_pk", 19.641507399412465, "deg", 1e-7), X("rho_min", 53.07777644194522, "mm", 1e-6), X("Rb_min_phi", 19.83873257343597, "mm", 1e-7)]),
            new("ka-cam-345", "3-4-5 polynomial rise and return, 15 mm over 90° at 900 rpm: C_v = 15/8, C_a = 10/√3, C_j = 60",
                new CalcReferenceSource(AnalyticSource, null, "Rise 15 mm over β = 90° (3-4-5 polynomial s = h(10x³ − 15x⁴ + 6x⁵)), dwell 90°, return, dwell 90°, 900 rpm (ω = 30π rad/s), R_b 35 mm, R_r 6 mm, in-line; follower 0.1 kg, spring 3 N/mm, preload 10 N, external 5 N: v_pk = (15/8)hω/β = 1.6875 m/s, a_pk = (10/√3)hω²/β² = 311.76915 m/s², j_pk = 60hω³/β³ = 194400 m/s³; F_max = 3·15 + 15 = 60 N, F_min = 15 N; the scan gives φ_max 20.4615°, ρ_min 33.6910 mm, peak T 0.740566 N·m."),
                CamInputs(FourSegments(Poly345, 90, 90, 15), 900, 35, 6, 0, 0.1, 3, 10, 5),
                [X("lift", 15, "mm", 1e-9), X("output_motion", 1.6875, "m/s", 1e-9), X("peak_accel", 311.7691453623978, "m/s^2", 1e-9), X("j_pk", 194400, "m/s^3", 1e-9), X("F_max", 60, "N", 1e-9), X("F_min", 15, "N", 1e-9), X("input_torque", 0.7405658738057201, "N*m", 1e-7), X("phi_max_pk", 20.461510418030052, "deg", 1e-7), X("rho_min", 33.69102870848566, "mm", 1e-6), X("Rb_min_phi", 18.30097344562704, "mm", 1e-7)]),
            new("ka-cam-4567", "4-5-6-7 polynomial rise and return, 15 mm over 90° at 900 rpm: C_v = 35/16, C_a = 7.5132, C_j = 52.5",
                new CalcReferenceSource(AnalyticSource, null, "Rise 15 mm over β = 90° (4-5-6-7 polynomial s = h(35x⁴ − 84x⁵ + 70x⁶ − 20x⁷)), dwell 90°, return, dwell 90°, 900 rpm, R_b 35 mm, R_r 6 mm, in-line, follower 0.1 kg, spring 3 N/mm, preload 10 N, external 5 N: v_pk = (35/16)hω/β = 1.96875 m/s, a_pk = C_a·hω²/β² with C_a = f″((5 − √5)/10) = 7.5131884044 → 405.712174 m/s², j_pk = 52.5hω³/β³ = 170100 m/s³ (the 4-5-6-7 law has zero jerk at both ends); the scan gives φ_max 23.5031°, ρ_min 30.1088 mm, F_max 60.7138 N, F_min 14.2862 N (inertia included), peak T 1.00117 N·m."),
                CamInputs(FourSegments(Poly4567, 90, 90, 15), 900, 35, 6, 0, 0.1, 3, 10, 5),
                [X("lift", 15, "mm", 1e-9), X("output_motion", 1.96875, "m/s", 1e-9), X("peak_accel", 405.71217383756147, "m/s^2", 1e-9), X("j_pk", 170100, "m/s^3", 1e-9), X("F_max", 60.71379043388232, "N", 1e-7), X("F_min", 14.286209566117677, "N", 1e-7), X("input_torque", 1.0011711441006657, "N*m", 1e-7), X("phi_max_pk", 23.50308359381908, "deg", 1e-7), X("rho_min", 30.10882801686118, "mm", 1e-6), X("Rb_min_phi", 23.297406083010312, "mm", 1e-7)]),
            new("ka-cam-offset-roller", "Offset roller follower (e = 10 mm), cycloidal rise and 3-4-5 return: pressure angle, undercut radius, minimum base circle",
                new CalcReferenceSource(CamOracle, null, "Rise 20 mm cycloidal over 120°, dwell 60°, return 20 mm 3-4-5 over 120°, dwell 60°, 120 rpm, R_b 40 mm, R_r 8 mm, offset e = +10 mm (φ = arctan[(s′ − e)/(√(R_p² − e²) + s)]), follower 0.2 kg, spring 5 N/mm, preload 20 N: v_pk = 0.24 m/s (cycloidal rise), j_pk = 259.2 m/s³ (3-4-5 return), φ_max 26.5958°, ρ_min 44.6811 mm, smallest R_b for φ ≤ 30° of 32.9632 mm, peak T 1.53449 N·m; the same cam in line (e = 0) has φ_max 18.4350° and ρ_min 45.9704 mm."),
                CamInputs(Mixed(), 120, 40, 8, 10, 0.2, 5, 20, 0),
                [X("output_motion", 0.24, "m/s", 1e-9), X("peak_accel", 4.523893421169301, "m/s^2", 1e-9), X("j_pk", 259.2, "m/s^3", 1e-9), X("F_max", 120, "N", 1e-9), X("input_torque", 1.534487469171382, "N*m", 1e-7), X("phi_max_pk", 26.595805687051826, "deg", 1e-7), X("rho_min", 44.68108272683772, "mm", 1e-6), X("Rb_min_phi", 32.96315610207776, "mm", 1e-7), X("F_pin_pk", 121.33134128619405, "N", 1e-7), X("F_spring_max", 120, "N", 1e-9), X("T_rms_in", 0.6975859169412787, "N*m", 1e-6), X("P_mean", 0, "W", 1e-6)]),
            new("ka-cam-inline-roller", "The same cam in line (e = 0): arctan[s′/(R_p + s)] and the pitch-curve radius",
                new CalcReferenceSource(CamOracle, null, "As the offset case with e = 0: φ_max = 18.4350°, ρ_min = 45.9704 mm, smallest R_b for φ ≤ 30° of 16.2901 mm; the peak velocity, acceleration, force and torque are those of the offset case (the offset does not change the motion or the virtual-work torque)."),
                CamInputs(Mixed(), 120, 40, 8, 0, 0.2, 5, 20, 0),
                [X("output_motion", 0.24, "m/s", 1e-9), X("input_torque", 1.534487469171382, "N*m", 1e-7), X("phi_max_pk", 18.435023310384924, "deg", 1e-7), X("rho_min", 45.970417430579836, "mm", 1e-6), X("Rb_min_phi", 16.290110908544293, "mm", 1e-7)]),
            new("ka-cam-dwell-only", "A cam that only dwells: a circle of radius R_b + R_r",
                new CalcReferenceSource(AnalyticSource, null, "One 360° dwell, R_b 40 mm, R_r 8 mm: the pitch curve is a circle of radius R_p = 48 mm, so ρ_min = 48 mm exactly, φ = 0, v = a = 0, the follower force is the preload plus the external load (20 + 7 = 27 N) and the input torque is zero."),
                CamInputs([Row(Dwell, null, 360, 0)], 120, 40, 8, 0, 0.2, 5, 20, 7),
                [X("lift", 0, "mm", 1e-9), X("rho_min", 48, "mm", 1e-9), X("phi_max_pk", 0, "deg", 1e-9), X("output_motion", 0, "m/s", 1e-9), X("follower_force", 27, "N", 1e-9), X("F_min", 27, "N", 1e-9), X("input_torque", 0, "N*m", 1e-9), X("Rb_min_phi", 0, "mm", 1e-9)]),
            new("ka-cam-flat-faced", "Flat-faced follower, cycloidal rise and 3-4-5 return: face half-width 2h/β and the profile radius R_b + s + s″",
                new CalcReferenceSource(AnalyticSource, null, "Rise 20 mm cycloidal over 120°, dwell 60°, return 3-4-5, dwell 60°, 120 rpm, R_b 40 mm, in-line flat-faced: the face must reach max |s′| = 2h/β = 19.098593 mm (cycloidal rise, closed form) either side of the axis; ρ = R_b + s + s″ has its minimum 29.336005 mm (recomputed in python3); v_pk 0.24 m/s, no pressure angle."),
                CamInputs(Mixed(), 120, 40, 8, 0, 0.2, 5, 20, 0, FlatFaced),
                [X("w_face", 19.098593171027442, "mm", 1e-9), X("rho_min", 29.336005162384772, "mm", 1e-7), X("output_motion", 0.24, "m/s", 1e-9), X("input_torque", 1.534487469171382, "N*m", 1e-7)]),
        ]);
    }
}
