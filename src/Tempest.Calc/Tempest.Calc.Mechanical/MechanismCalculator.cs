using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>
/// M9 — mechanism kinematics for a single-degree-of-freedom planar mechanism driven at constant speed: a four-bar linkage and an
/// offset slider-crank (closed-form vector-loop position for both circuits, velocity and acceleration over a sweep of the input
/// angle, Grashof class, transmission angle, toggle and dead-centre positions, input torque by virtual work), and a cam with a
/// translating follower. Rigid, planar, no clearance, no friction. Every peak is the peak of the continuous motion (a dense scan
/// refined by golden section), not of the printed table, so it does not depend on the table's step count.
/// </summary>
public sealed partial class MechanismCalculator : ICalcCalculator
{
    public const string Id = "mech.mechanism";

    public const string FourBar = "Four-bar";
    public const string SliderCrank = "Slider-crank";

    public const string Open = "Open";
    public const string Crossed = "Crossed";

    public const string Constant = "Constant direction";
    public const string Opposes = "Opposes motion";

    /// <summary>The loop-closure residual (mm) the sheet asserts at every step.</summary>
    public const double ClosureLimitMm = 1e-9;

    private const string Source = "Vector-loop closed form (Freudenstein / law of cosines)";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.Mechanical, "Mechanism kinematics: four-bar, slider-crank, cam",
        "A single-degree-of-freedom planar mechanism driven at constant speed: a four-bar linkage or an offset slider-crank (both circuits, position, velocity and acceleration over a sweep of the input angle, Grashof class, transmission angle against a minimum, toggle and dead-centre positions, loop-closure residual, input torque by virtual work), or a cam with a translating follower. Rigid links, no clearance, no friction.",
        "Four-bar: a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄}; ℓ = |A − O₄|, cos γ = (c² + ℓ² − b²)/(2cℓ), θ₄ = φ ∓ γ (φ the direction O₄→A; open: −, crossed: +); θ₃ from B − A\n" +
        "Four-bar velocity and acceleration: differentiated loop, 2×2 system with det = b·c·sin(θ₃ − θ₄); ω₃ = ω₂·a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄)), ω₄ = ω₂·a·sin(θ₃ − θ₂)/(c·sin(θ₃ − θ₄)); α from the second derivative at constant ω₂\n" +
        "Grashof: s + l < p + q (crank-rocker, double-crank or double-rocker by the shortest link), = at the change point, > non-Grashof; transmission angle cos μ = (b² + c² − ℓ²)/(2bc) folded to 0 … 90°; toggle where ℓ = b + c or |b − c|\n" +
        "Slider-crank: x = r·cosθ + σ·√(l² − (r·sinθ − e)²) (σ = +1 open, −1 crossed); dx/dθ = −r·sinθ − r·cosθ·(r·sinθ − e)/(x − r·cosθ); dead centres where crank and rod are collinear; μ = 90° − |rod angle to the slide line|\n" +
        "Input torque by virtual work: T_in·ω_in = Σ F·v; four-bar T_in = (T_L + J·α₄)·ω₄/ω₂; slider T_in = (m·a + F_L)·dx/dθ\n" +
        "Loop closure: |a·e^{jθ₂} + b·e^{jθ₃} − c·e^{jθ₄} − d| (slider: |r·e^{jθ} + l·e^{jθ₃} − (x + j·e)|) asserted below 1e-9 mm at every step\n" +
        "Cam, per segment with x = (θ − θ₀)/β: rise s = s₀ + h·f(x), return s = s₀ − h·f(x); cycloidal f = x − sin(2πx)/(2π); 3-4-5 f = 10x³ − 15x⁴ + 6x⁵; 4-5-6-7 f = 35x⁴ − 84x⁵ + 70x⁶ − 20x⁷; modified trapezoid from its acceleration pieces (C_v = 2, C_a = 8π/(π + 2), C_j = 32π²/(π + 2))\n" +
        "Cam SVAJ at constant ω: v = ω·s′, a = ω²·s″, j = ω³·s‴ (′ = d/dθ); peak coefficients C_v = max f′, C_a = max |f″|, C_j = max |f‴|, so v_pk = C_v·h·ω/β\n" +
        "Pressure angle (roller, offset e, R_p = R_b + R_r): φ = arctan[(s′ − e)/(√(R_p² − e²) + s)], which for e = 0 is arctan[s′/(R_p + s)]; the base circle that holds φ ≤ φ_max has R_p,req = √(s₀,req² + e²) with s₀,req = max(|s′ − e|/tan φ_max − s)\n" +
        "Radius of curvature of the pitch curve P(θ) = Rot(−θ)·(e, √(R_p² − e²) + s): ρ = |P′|³/(P′ × P″), convex where positive; undercut where ρ_min ≤ R_r (FAIL), none where the curve is nowhere convex; flat-faced follower ρ = R_b + s + s″ must stay positive and the face must reach |s′|\n" +
        "Cam follower force F = m·a + k·s + F_pre + F_ext (the follower leaves the cam where F < 0); input torque by virtual work T = F·s′ (T·ω = F·v)",
        BuildInputs(),
        [
            new CalcAssumption("Rigid links, a planar mechanism with one degree of freedom, no joint clearance and no friction; the input turns at a constant speed.", "Addendum B §B6 M9 limits."),
            new CalcAssumption("The position is the closed-form solution of the vector loop; the open and crossed circuits are the two branches (θ₄ = φ − γ and φ + γ for the four-bar, σ = +1 and −1 for the slider) and the sweep stays on the chosen branch. Angles at which the linkage cannot be assembled, or at a toggle where the velocity system is singular, are left out of the table and counted.", "Norton, Design of Machinery, position analysis of the four-bar and the offset slider-crank."),
            new CalcAssumption("Input torque is by virtual work from the output load and the inertia of the output element only (four-bar: the output link's moment of inertia about its pivot; slider: the slider mass); the crank and the coupler or connecting rod are massless.", "No link-inertia or gravity terms: add the link masses to the output element for a first allowance."),
            new CalcAssumption("The peaks (speeds, accelerations, torque, transmission angle, swing) are those of the continuous motion over the swept range, found by a dense scan refined by golden section; the printed table (steps N, up to 3600) only samples it.", "Peaks do not depend on the table's step count."),
            new CalcAssumption("Cam: a translating follower on a cam turning at constant speed; the follower mass, a linear return spring (rate and preload at the base circle, for example linked from M7) and a constant external load make the force the cam must apply, F = m·a + k·s + F_pre + F_ext; contact is lost where it falls below zero. The segment table must add to 360° and close; pressure angle and undercut are checked for the roller follower (R_p = R_b + R_r), the positive profile radius for the flat-faced one.", "Norton, Design of Machinery, cam design: the four classical laws, pressure angle and radius of curvature."),
            new CalcAssumption("Follower force (four-bar: the tangential force at the output pin, (T_L + J·α₄)/c; slider: the rod force on the slider, m·a + F_L) is the force the mechanism must apply to the output element.", "Rigid body, frictionless."),
        ],
        [
            new CalcClauseRef("Norton, Design of Machinery", "Ch. 4, 6, 7", "Position, velocity and acceleration analysis; transmission angle; Grashof condition"),
            new CalcClauseRef("Uicker, Pennock and Shigley, Theory of Machines and Mechanisms", "Ch. 4–6", "Loop-closure position analysis; velocity and acceleration of planar linkages"),
        ],
        BuildRules(),
        BuildCases(),
        Category: "Mechanisms",
        OutputPorts:
        [
            new CalcOutputPort("input_torque", "N*m", "Peak input torque by virtual work (mech.input_torque → E1, M4, M5)"),
            new CalcOutputPort("output_motion", "m/s", "Peak output velocity: the slider or the cam follower (m/s); for a four-bar the speed of the output link's pin at its length c (the angular velocity is omega_out_pk) (mech.output_motion → F1)"),
            new CalcOutputPort("peak_accel", "m/s^2", "Peak output acceleration: the slider or the follower; for a four-bar the magnitude of the acceleration of the output pin (mech.peak_accel)"),
            new CalcOutputPort("follower_force", "N", "Peak force on the output element: the follower contact force, the slider's driving force, or the tangential force at the output pin (mech.follower_force → M7, F1)"),
        ],
        RecordKinds: null);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static string F(double value) => CalcValue.FormatNumber(value, 5);

    // ------------------------------------------------------------------
    // Inputs, rules
    // ------------------------------------------------------------------

    private static IReadOnlyList<CalcInputSpec> BuildInputs()
    {
        var list = new List<CalcInputSpec>
        {
            new("mode", "Kind of mechanism", CalcInputKind.Choice, Choices: ModeChoices, DefaultChoice: FourBar, Group: "Mechanism"),
            new("circuit", "Assembly circuit (four-bar, slider-crank): open is θ₄ = φ − γ (slider on the +x side of the crank pin), crossed the mirror branch", CalcInputKind.Choice, Choices: [Open, Crossed], DefaultChoice: Open, IsOptional: true, Group: "Mechanism"),
            new("L1", "Ground link d, O₂ to O₄ (four-bar)", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("L2", "Crank length a (four-bar) or r (slider-crank)", CalcInputKind.Quantity, "mm", Default: 30, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("L3", "Coupler length b (four-bar) or connecting-rod length l (slider-crank)", CalcInputKind.Quantity, "mm", Default: 90, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("L4", "Rocker length c (four-bar)", CalcInputKind.Quantity, "mm", Default: 70, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("e_off", "Offset e: slide line above the crank centre (slider-crank); follower axis offset (cam)", CalcInputKind.Quantity, "mm", Default: 0, Min: -1000, Max: 1000, HardMin: -1e5, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("theta_start", "Input angle at the first row of the table", CalcInputKind.Quantity, "deg", Default: 0, Min: -720, Max: 720, HardMin: -7200, HardMax: 7200, IsOptional: true, Group: "Sweep"),
            new("theta_range", "Swept range of the input angle (360° for a full turn)", CalcInputKind.Quantity, "deg", Default: 360, Min: 1, Max: 720, HardMin: 0.1, HardMax: 3600, IsOptional: true, Group: "Sweep"),
            new("steps", "Intervals N of the table (N + 1 rows, a whole number, at most 3600; the peaks do not depend on it)", CalcInputKind.Number, Default: 72, Min: 4, Max: 720, HardMin: 2, HardMax: 3600, Group: "Sweep"),
            new("n_in", "Input speed (constant)", CalcInputKind.Quantity, "rpm", Default: 120, Min: 0.1, Max: 30000, HardMin: 1e-3, HardMax: 1e6, Group: "Sweep"),
            new("mu_req", "Minimum transmission angle required (linkages)", CalcInputKind.Quantity, "deg", Default: 40, Min: 0, Max: 90, HardMin: 0, HardMax: 90, IsOptional: true, Group: "Limits"),
            new("load_dir", "External load: constant direction, or always opposing the output motion", CalcInputKind.Choice, Choices: [Constant, Opposes], DefaultChoice: Opposes, IsOptional: true, Group: "Loads"),
            new("T_load", "External load torque on the output link (four-bar), positive against ω₄", CalcInputKind.Quantity, "N*m", Default: 1.5, Min: -10000, Max: 10000, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("J_out", "Moment of inertia of the output link about its pivot (four-bar)", CalcInputKind.Quantity, "kg*m^2", Default: 0.002, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("m_slider", "Slider mass (slider-crank)", CalcInputKind.Quantity, "kg", Default: 0.5, Min: 0, Max: 10000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("F_load", "External load on the slider, positive against +x (slider-crank)", CalcInputKind.Quantity, "N", Default: 200, Min: -1e6, Max: 1e6, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
        };

        AddCamInputs(list);
        return list;
    }

    private static IReadOnlyList<string> ModeChoices => [FourBar, SliderCrank, .. CamModes];

    private static IReadOnlyList<CalcApplicabilityRule> BuildRules()
    {
        var rules = new List<CalcApplicabilityRule>
        {
            new("missing", "An input the chosen mechanism needs is blank.", CalcNoticeSeverity.Block),
            new("geometry", "The mechanism cannot be assembled at any angle of the swept range, or the links are not a valid mechanism.", CalcNoticeSeverity.Block),
            new("reach", "Some rows of the sweep cannot be assembled (the crank cannot turn fully) or sit at a toggle with no defined velocity: they are left out of the table.", CalcNoticeSeverity.Warning),
            new("closure", "The loop-closure residual reached 1e-9 mm.", CalcNoticeSeverity.Warning),
            new("grashof", "The linkage is not a Grashof crank (the input link cannot turn through 360°) but the swept range is 360° or more.", CalcNoticeSeverity.Warning),
        };

        AddCamRules(rules);
        return rules;
    }

    // ------------------------------------------------------------------
    // Run
    // ------------------------------------------------------------------

    public void Run(CalcRunContext c)
    {
        var mode = c.InputText("mode");

        c.Input("steps");
        c.Input("n_in");

        if (mode == CamMode)
        {
            RunCam(c);
            return;
        }

        if (!Require(c, mode == FourBar ? new[] { "L1", "L2", "L3", "L4", "theta_start", "theta_range" } : new[] { "L2", "L3", "e_off", "theta_start", "theta_range" }))
            return;

        var omega = c.InputSi("n_in");
        var steps = (int)Math.Round(c.InputSi("steps"));
        var start = c.InputSi("theta_start");
        var range = c.InputSi("theta_range");
        var crossed = c.Has("circuit") && c.InputText("circuit") == Crossed;
        var muReq = c.Has("mu_req") ? c.InputSi("mu_req") : 40 * Math.PI / 180;
        var opposes = !c.Has("load_dir") || c.InputText("load_dir") == Opposes;

        if (mode == FourBar)
            RunFourBar(c, new Sweep(start, range, steps, omega), crossed, muReq, opposes);
        else
            RunSlider(c, new Sweep(start, range, steps, omega), crossed, muReq, opposes);
    }

    /// <summary>Blocks, in the sheet's own words, when an input the chosen mechanism needs has been cleared. False when blocked.</summary>
    private static bool Require(CalcRunContext c, params string[] symbols)
    {
        foreach (var symbol in symbols)
        {
            if (c.Has(symbol))
                continue;

            c.Block("missing", $"{symbol}: enter '{c.Template.FindInput(symbol)?.Description ?? symbol}'; the {c.InputText("mode").ToLowerInvariant()} calculation needs it.");
            return false;
        }

        foreach (var symbol in symbols)
            c.Input(symbol);

        return true;
    }

    private readonly record struct Sweep(double Start, double Range, int Steps, double Omega)
    {
        public double At(int i) => Start + Range * i / Steps;

        /// <summary>Intervals of the dense peak scan: about a quarter of a degree, at least 720.</summary>
        public int Dense => Math.Clamp((int)Math.Ceiling(Range * 180 / Math.PI * 4), 720, 14400);
    }

    private static void Table(CalcRunContext c, string symbol, string formula, string substitution, IReadOnlyList<double> values, string unit, string description, string? clause = null)
    {
        c.Record(symbol, formula, substitution, VectorValue.Of(values, unit), description, clause);
        c.Output(symbol, description);
    }

    private static void Scalar(CalcRunContext c, string symbol, string formula, string substitution, double value, string unit, string description, string? clause = null)
    {
        c.Record(symbol, formula, substitution, ScalarValue.Of(value, unit), description, clause);
        c.Output(symbol, description);
    }

    private static double Sgn(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

    private static double Deg(double rad) => rad * 180 / Math.PI;

    private static double Mm(double m) => m * 1e3;

    // ------------------------------------------------------------------
    // Four-bar
    // ------------------------------------------------------------------

    private static void RunFourBar(CalcRunContext c, Sweep sw, bool crossed, double muReq, bool opposes)
    {
        var d = c.Si("L1");
        var a = c.Si("L2");
        var b = c.Si("L3");
        var cc = c.Si("L4");

        if (c.Has("T_load"))
            c.Input("T_load");

        if (c.Has("J_out"))
            c.Input("J_out");

        var torque = c.Has("T_load") ? c.Si("T_load") : 0;
        var inertia = c.Has("J_out") ? c.Si("J_out") : 0;
        var w2 = sw.Omega;

        var cls = FourBarLinkage.Classify(d, a, b, cc, out var sl, out var pq, out var full);
        c.Output("grashof", "Grashof classification", new TextValue(cls));
        Scalar(c, "S_plus_L", "s + l", "shortest + longest link", Mm(sl), "mm", "Shortest plus longest link", "Grashof criterion");
        Scalar(c, "P_plus_Q", "p + q", "the other two links", Mm(pq), "mm", "Sum of the other two links", "Grashof criterion");

        if (!full && sw.Range >= 2 * Math.PI - 1e-9)
            c.Warn("grashof", $"L2: {cls}. A swept range of 360° or more cannot be completed; the unreachable angles are left out.");

        var link = new FourBarLinkage(d, a, b, cc, crossed);

        // The table.
        var rows = new List<FourBarState>();
        var unreachable = 0;
        var singular = 0;

        for (var i = 0; i <= sw.Steps; i++)
        {
            var st = link.At(sw.At(i), w2);

            if (st is null)
                unreachable++;
            else if (st.Value.Singular)
                singular++;
            else
                rows.Add(st.Value);
        }

        if (rows.Count == 0)
        {
            c.Block("geometry", $"L1: the four links cannot be assembled at any crank angle of the swept range ({F(Deg(sw.Start))}° to {F(Deg(sw.Start + sw.Range))}°), or every row is a toggle; check the link lengths (|b − c| ≤ ℓ ≤ b + c must hold with ℓ = |A − O₄|). {cls}.");
            return;
        }

        if (unreachable > 0 || singular > 0)
            c.Warn("reach", $"L2: {unreachable} of {sw.Steps + 1} table rows cannot be assembled and {singular} sit at a toggle (coupler and rocker collinear, no defined velocity); they are left out of the table.");

        // Dense scan (for peaks).
        var n = sw.Dense;
        var xs = new double[n + 1];
        var states = new FourBarState?[n + 1];
        var resMax = rows.Max(r => r.Residual);

        for (var j = 0; j <= n; j++)
        {
            xs[j] = sw.Start + sw.Range * j / n;
            states[j] = link.At(xs[j], w2);

            if (states[j] is { } s)
                resMax = Math.Max(resMax, s.Residual);
        }

        (double Value, double At)? Peak(Func<FourBarState, double> select, bool max, bool allowSingular = false)
        {
            double Eval(FourBarState? s) => s is { } v && (allowSingular || !v.Singular) ? select(v) : double.NaN;
            var ys = states.Select(Eval).ToArray();
            return MechanismPeaks.Refine(xs, ys, (_, x) => Eval(link.At(x, w2)), max);
        }

        double Tc(double omega4) => opposes ? torque * Sgn(omega4) : torque;
        double TIn(FourBarState s) => (Tc(s.Omega4) + inertia * s.Alpha4) * s.Omega4 / w2;
        double Pin(FourBarState s) => (Tc(s.Omega4) + inertia * s.Alpha4) / cc;

        var omega3Pk = Peak(s => Math.Abs(s.Omega3), true);
        var omega4Pk = Peak(s => Math.Abs(s.Omega4), true);
        var alpha3Pk = Peak(s => Math.Abs(s.Alpha3), true);
        var alpha4Pk = Peak(s => Math.Abs(s.Alpha4), true);
        var muMin = Peak(s => s.Mu, false, allowSingular: true);
        var tInPk = Peak(s => Math.Abs(TIn(s)), true);
        var fPk = Peak(s => Math.Abs(Pin(s)), true);
        var aPinPk = Peak(s => cc * Math.Sqrt(s.Alpha4 * s.Alpha4 + Math.Pow(s.Omega4, 4)), true);

        if (omega4Pk is null || muMin is null || tInPk is null || fPk is null || aPinPk is null || omega3Pk is null || alpha3Pk is null || alpha4Pk is null)
        {
            c.Block("geometry", "L1: the sweep has no row with a defined velocity (every assembled row is a toggle); change the swept range or the link lengths.");
            return;
        }

        // θ₄ swing, unwrapped along the scan.
        var ys4 = new double[n + 1];
        var prevRaw = double.NaN;
        var prevUnwrapped = 0.0;

        for (var j = 0; j <= n; j++)
        {
            if (states[j] is not { } s)
            {
                ys4[j] = double.NaN;
                continue;
            }

            var raw = s.Theta4;
            prevUnwrapped = double.IsNaN(prevRaw) ? raw : prevUnwrapped + WrapPi(raw - prevRaw);
            prevRaw = raw;
            ys4[j] = prevUnwrapped;
        }

        double Theta4Near(int j, double x) => link.At(x, w2) is { } v ? ys4[j] + WrapPi(v.Theta4 - ys4[j]) : double.NaN;
        var t4Max = MechanismPeaks.Refine(xs, ys4, Theta4Near, true)!.Value.Value;
        var t4Min = MechanismPeaks.Refine(xs, ys4, Theta4Near, false)!.Value.Value;

        // Records: the table.
        string cols = $"{rows.Count} rows, θ₂ from {F(Deg(rows[0].Theta2))}° to {F(Deg(rows[^1].Theta2))}°";
        const string clause = Source;
        Table(c, "theta2_sw", "θ₂", cols, rows.Select(r => Deg(r.Theta2)).ToList(), "deg", "Table: crank angle", clause);
        Table(c, "theta3_sw", "θ₃ = atan2(B − A)", cols, rows.Select(r => Deg(r.Theta3)).ToList(), "deg", "Table: coupler angle", clause);
        Table(c, "theta4_sw", "θ₄ = φ ∓ γ", cols, rows.Select(r => Deg(r.Theta4)).ToList(), "deg", "Table: rocker angle", clause);
        Table(c, "omega3_sw", "ω₃", cols, rows.Select(r => r.Omega3).ToList(), "rad/s", "Table: coupler angular velocity", clause);
        Table(c, "omega4_sw", "ω₄", cols, rows.Select(r => r.Omega4).ToList(), "rad/s", "Table: rocker angular velocity", clause);
        Table(c, "alpha3_sw", "α₃", cols, rows.Select(r => r.Alpha3).ToList(), "rad/s^2", "Table: coupler angular acceleration", clause);
        Table(c, "alpha4_sw", "α₄", cols, rows.Select(r => r.Alpha4).ToList(), "rad/s^2", "Table: rocker angular acceleration", clause);
        Table(c, "mu_sw", "μ", cols, rows.Select(r => Deg(r.Mu)).ToList(), "deg", "Table: transmission angle", clause);
        Table(c, "T_in_sw", "(T_L + J·α₄)·ω₄/ω₂", cols, rows.Select(TIn).ToList(), "N*m", "Table: input torque by virtual work", "Virtual work");

        c.Series(new CalcSeries("Rocker angle θ₄", "θ₂", "deg", "θ₄", "deg", rows.Select(r => (Deg(r.Theta2), Deg(r.Theta4))).ToList()));
        c.Series(new CalcSeries("Rocker angular velocity ω₄", "θ₂", "deg", "ω₄", "rad/s", rows.Select(r => (Deg(r.Theta2), r.Omega4)).ToList()));
        c.Series(new CalcSeries("Rocker angular acceleration α₄", "θ₂", "deg", "α₄", "rad/s^2", rows.Select(r => (Deg(r.Theta2), r.Alpha4)).ToList()));
        c.Series(new CalcSeries("Transmission angle μ", "θ₂", "deg", "μ", "deg", rows.Select(r => (Deg(r.Theta2), Deg(r.Mu))).ToList()));
        c.Series(new CalcSeries("Input torque", "θ₂", "deg", "T_in", "N*m", rows.Select(r => (Deg(r.Theta2), TIn(r))).ToList()));

        // Probe: the first row of the sweep, on the chosen circuit and on the other.
        var probe = link.At(sw.Start, w2);

        if (probe is { Singular: false } p0)
        {
            const string at0 = "at the first input angle";
            Scalar(c, "theta3_0", "θ₃", at0, Deg(p0.Theta3), "deg", "Coupler angle at the first input angle", clause);
            Scalar(c, "theta4_0", "θ₄", at0, Deg(p0.Theta4), "deg", "Rocker angle at the first input angle", clause);
            Scalar(c, "omega3_0", "ω₃", at0, p0.Omega3, "rad/s", "Coupler angular velocity at the first input angle", clause);
            Scalar(c, "omega4_0", "ω₄", at0, p0.Omega4, "rad/s", "Rocker angular velocity at the first input angle", clause);
            Scalar(c, "alpha3_0", "α₃", at0, p0.Alpha3, "rad/s^2", "Coupler angular acceleration at the first input angle", clause);
            Scalar(c, "alpha4_0", "α₄", at0, p0.Alpha4, "rad/s^2", "Rocker angular acceleration at the first input angle", clause);
            Scalar(c, "mu_0", "μ", at0, Deg(p0.Mu), "deg", "Transmission angle at the first input angle", clause);
            Scalar(c, "T_in_0", "(T_L + J·α₄)·ω₄/ω₂", at0, TIn(p0), "N*m", "Input torque at the first input angle", "Virtual work");
            var other = new FourBarLinkage(d, a, b, cc, !crossed).At(sw.Start, w2);

            if (other is { } o)
                Scalar(c, "theta4_alt", "θ₄ on the other circuit", "mirror branch, " + at0, Deg(o.Theta4), "deg", "Rocker angle on the other circuit at the first input angle", clause);
        }

        // Peaks and the toggle positions.
        Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", omega3Pk.Value.Value, "rad/s", "Peak coupler angular velocity", clause);
        Scalar(c, "omega_out_pk", "max |ω₄|", "dense scan, refined", omega4Pk.Value.Value, "rad/s", "Peak output (rocker) angular velocity", clause);
        Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", alpha3Pk.Value.Value, "rad/s^2", "Peak coupler angular acceleration", clause);
        Scalar(c, "alpha_out_pk", "max |α₄|", "dense scan, refined", alpha4Pk.Value.Value, "rad/s^2", "Peak output (rocker) angular acceleration", clause);
        Scalar(c, "theta4_min", "min θ₄", "unwrapped along the sweep", Deg(t4Min), "deg", "Smallest rocker angle", clause);
        Scalar(c, "theta4_max", "max θ₄", "unwrapped along the sweep", Deg(t4Max), "deg", "Largest rocker angle", clause);
        Scalar(c, "swing", "max θ₄ − min θ₄", "over the sweep", Deg(t4Max - t4Min), "deg", "Rocker swing over the sweep", clause);
        Scalar(c, "mu_min", "min μ", "dense scan, refined", Deg(muMin.Value.Value), "deg", "Smallest transmission angle over the sweep", clause);
        Scalar(c, "res_max", "max |loop residual|", $"{rows.Count} table rows and {n + 1} scan angles", Mm(resMax), "mm", "Largest loop-closure residual", clause);

        var toggles = link.ToggleCrankAngles();
        c.Output("toggle_angles", "Toggle crank angles (coupler and rocker collinear)", new TextValue(toggles.Count == 0
            ? "none: coupler and rocker are never collinear"
            : "θ₂ = " + string.Join(", ", toggles.Select(t => $"{F(Deg(t))}°") ) + " (μ = 0)"));

        // The ports.
        Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tInPk.Value.Value, "N*m", "Peak input torque by virtual work (mech.input_torque)", "Virtual work");
        Scalar(c, "output_motion", "c · max |ω₄|", "output pin at the rocker length", cc * omega4Pk.Value.Value, "m/s", "Peak speed of the output pin (mech.output_motion; the angular velocity is omega_out_pk)", clause);
        Scalar(c, "peak_accel", "max |a_pin|", "√(α₄² + ω₄⁴)·c", aPinPk.Value.Value, "m/s^2", "Peak acceleration of the output pin (mech.peak_accel)", clause);
        Scalar(c, "follower_force", "max |(T_L + J·α₄)/c|", "tangential force at the output pin", fPk.Value.Value, "N", "Peak tangential force at the output pin (mech.follower_force)", "Virtual work");

        // Checks.
        var muLimit = muReq;
        c.Record("u_mu", "μ_req / μ_min", $"{F(Deg(muLimit))}° / {F(Deg(muMin.Value.Value))}°", new ScalarValue(muMin.Value.Value > 1e-9 ? muLimit / muMin.Value.Value : 1e9), "Transmission angle: required minimum against the smallest over the sweep", "Transmission angle");
        c.Output("u_mu", "Required against smallest transmission angle");
        c.CheckUtilisation("Transmission angle μ_min at or above the required minimum", "u_mu", "Transmission angle");
        ClosureCheck(c, resMax);
    }

    private static double WrapPi(double angle)
    {
        var w = (angle + Math.PI) % (2 * Math.PI);
        return (w < 0 ? w + 2 * Math.PI : w) - Math.PI;
    }

    private static void ClosureCheck(CalcRunContext c, double residualMetres)
    {
        var mm = Mm(residualMetres);
        var ok = mm < ClosureLimitMm;
        c.CheckCondition("Loop-closure residual below 1e-9 mm at every step", ok, $"max residual = {CalcValue.FormatNumber(mm)} mm", $"< {CalcValue.FormatNumber(ClosureLimitMm)} mm", "res_max", "Self-consistency");

        if (!ok)
            c.Warn("closure", $"L3: the loop-closure residual {CalcValue.FormatNumber(mm)} mm reached 1e-9 mm; the geometry is near-degenerate and the position is no longer exact.");
    }

    // ------------------------------------------------------------------
    // Slider-crank
    // ------------------------------------------------------------------

    private static void RunSlider(CalcRunContext c, Sweep sw, bool crossed, double muReq, bool opposes)
    {
        var r = c.Si("L2");
        var l = c.Si("L3");
        var e = c.Si("e_off");

        if (c.Has("m_slider"))
            c.Input("m_slider");

        if (c.Has("F_load"))
            c.Input("F_load");

        var mass = c.Has("m_slider") ? c.Si("m_slider") : 0;
        var load = c.Has("F_load") ? c.Si("F_load") : 0;
        var w = sw.Omega;
        var link = new SliderCrankLinkage(r, l, e, crossed);

        var cls = link.FullRotation
            ? "Crank-slider: the crank can turn through 360° (l ≥ r + |e|)"
            : "Rocker-slider: the crank cannot turn fully (l < r + |e|), the sweep is limited";
        c.Output("slider_class", "Class of the slider-crank", new TextValue(cls));

        var rows = new List<SliderState>();
        var unreachable = 0;

        for (var i = 0; i <= sw.Steps; i++)
        {
            if (link.At(sw.At(i)) is { } st)
                rows.Add(st);
            else
                unreachable++;
        }

        if (rows.Count == 0)
        {
            c.Block("geometry", $"L3: the rod cannot reach the slide line at any crank angle of the swept range (|r·sinθ − e| ≤ l must hold); {cls}.");
            return;
        }

        if (unreachable > 0)
            c.Warn("reach", $"L3: {unreachable} of {sw.Steps + 1} table rows cannot be assembled (the rod cannot reach the slide line); they are left out of the table.");

        var n = sw.Dense;
        var xs = new double[n + 1];
        var states = new SliderState?[n + 1];
        var resMax = rows.Max(row => row.Residual);

        for (var j = 0; j <= n; j++)
        {
            xs[j] = sw.Start + sw.Range * j / n;
            states[j] = link.At(xs[j]);

            if (states[j] is { } s)
                resMax = Math.Max(resMax, s.Residual);
        }

        (double Value, double At)? Peak(Func<SliderState, double> select, bool max)
        {
            double Eval(SliderState? s) => s is { } v ? select(v) : double.NaN;
            var ys = states.Select(Eval).ToArray();
            return MechanismPeaks.Refine(xs, ys, (_, x) => Eval(link.At(x)), max);
        }

        double V(SliderState s) => w * s.DxDtheta;
        double A(SliderState s) => w * w * s.D2xDtheta2;
        double Fc(SliderState s) => opposes ? load * Sgn(V(s)) : load;
        double Fr(SliderState s) => mass * A(s) + Fc(s);
        double TIn(SliderState s) => Fr(s) * s.DxDtheta;

        var xMax = Peak(s => s.X, true);
        var xMin = Peak(s => s.X, false);
        var vPk = Peak(s => Math.Abs(V(s)), true);
        var aPk = Peak(s => Math.Abs(A(s)), true);
        var w3Pk = Peak(s => Math.Abs(w * s.Dtheta3), true);
        var al3Pk = Peak(s => Math.Abs(w * w * s.D2theta3), true);
        var muMin = Peak(s => s.Mu, false);
        var tPk = Peak(s => Math.Abs(TIn(s)), true);
        var fPk = Peak(s => Math.Abs(Fr(s)), true);

        if (xMax is null || xMin is null || vPk is null || aPk is null || w3Pk is null || al3Pk is null || muMin is null || tPk is null || fPk is null)
        {
            c.Block("geometry", "L3: the sweep has no assembled row; change the swept range or the link lengths.");
            return;
        }

        string cols = $"{rows.Count} rows, θ from {F(Deg(rows[0].Theta))}° to {F(Deg(rows[^1].Theta))}°";
        const string clause = Source;
        Table(c, "theta_sw", "θ", cols, rows.Select(row => Deg(row.Theta)).ToList(), "deg", "Table: crank angle", clause);
        Table(c, "x_sw", "x = r·cosθ + σ·√(l² − (r·sinθ − e)²)", cols, rows.Select(row => Mm(row.X)).ToList(), "mm", "Table: slider position", clause);
        Table(c, "v_sw", "v = ω·dx/dθ", cols, rows.Select(V).ToList(), "m/s", "Table: slider velocity", clause);
        Table(c, "a_sw", "a = ω²·d²x/dθ²", cols, rows.Select(A).ToList(), "m/s^2", "Table: slider acceleration", clause);
        Table(c, "theta3_sw", "θ₃ = atan2(e − r·sinθ, x − r·cosθ)", cols, rows.Select(row => Deg(row.Theta3)).ToList(), "deg", "Table: connecting-rod angle", clause);
        Table(c, "omega3_sw", "ω₃ = −ω·r·cosθ/(x − r·cosθ)", cols, rows.Select(row => w * row.Dtheta3).ToList(), "rad/s", "Table: connecting-rod angular velocity", clause);
        Table(c, "mu_sw", "μ = 90° − |rod angle to the slide line|", cols, rows.Select(row => Deg(row.Mu)).ToList(), "deg", "Table: transmission angle", clause);
        Table(c, "T_in_sw", "(m·a + F_L)·dx/dθ", cols, rows.Select(TIn).ToList(), "N*m", "Table: input torque by virtual work", "Virtual work");

        c.Series(new CalcSeries("Slider position", "θ", "deg", "x", "mm", rows.Select(row => (Deg(row.Theta), Mm(row.X))).ToList()));
        c.Series(new CalcSeries("Slider velocity", "θ", "deg", "v", "m/s", rows.Select(row => (Deg(row.Theta), V(row))).ToList()));
        c.Series(new CalcSeries("Slider acceleration", "θ", "deg", "a", "m/s^2", rows.Select(row => (Deg(row.Theta), A(row))).ToList()));
        c.Series(new CalcSeries("Transmission angle μ", "θ", "deg", "μ", "deg", rows.Select(row => (Deg(row.Theta), Deg(row.Mu))).ToList()));
        c.Series(new CalcSeries("Input torque", "θ", "deg", "T_in", "N*m", rows.Select(row => (Deg(row.Theta), TIn(row))).ToList()));

        if (link.At(sw.Start) is { } p0)
        {
            const string at0 = "at the first input angle";
            Scalar(c, "x_0", "x", at0, Mm(p0.X), "mm", "Slider position at the first input angle", clause);
            Scalar(c, "v_0", "ω·dx/dθ", at0, V(p0), "m/s", "Slider velocity at the first input angle", clause);
            Scalar(c, "a_0", "ω²·d²x/dθ²", at0, A(p0), "m/s^2", "Slider acceleration at the first input angle", clause);
            Scalar(c, "theta3_0", "θ₃", at0, Deg(p0.Theta3), "deg", "Connecting-rod angle at the first input angle", clause);
            Scalar(c, "omega3_0", "ω₃", at0, w * p0.Dtheta3, "rad/s", "Connecting-rod angular velocity at the first input angle", clause);
            Scalar(c, "alpha3_0", "α₃", at0, w * w * p0.D2theta3, "rad/s^2", "Connecting-rod angular acceleration at the first input angle", clause);
            Scalar(c, "mu_0", "μ", at0, Deg(p0.Mu), "deg", "Transmission angle at the first input angle", clause);
            Scalar(c, "T_in_0", "(m·a + F_L)·dx/dθ", at0, TIn(p0), "N*m", "Input torque at the first input angle", "Virtual work");
            var other = new SliderCrankLinkage(r, l, e, !crossed).At(sw.Start);

            if (other is { } o)
                Scalar(c, "x_alt", "x on the other circuit", "mirror branch, " + at0, Mm(o.X), "mm", "Slider position on the other circuit at the first input angle", clause);
        }

        Scalar(c, "x_max", "max x", "over the sweep", Mm(xMax.Value.Value), "mm", "Largest slider position over the sweep", clause);
        Scalar(c, "x_min", "min x", "over the sweep", Mm(xMin.Value.Value), "mm", "Smallest slider position over the sweep", clause);
        Scalar(c, "stroke", "max x − min x", "over the sweep", Mm(xMax.Value.Value - xMin.Value.Value), "mm", "Slider stroke over the sweep (the full stroke when the sweep covers 360°)", clause);
        Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", w3Pk.Value.Value, "rad/s", "Peak connecting-rod angular velocity", clause);
        Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", al3Pk.Value.Value, "rad/s^2", "Peak connecting-rod angular acceleration", clause);
        Scalar(c, "mu_min", "min μ", "dense scan, refined", Deg(muMin.Value.Value), "deg", "Smallest transmission angle over the sweep", clause);
        Scalar(c, "res_max", "max |loop residual|", $"{rows.Count} table rows and {n + 1} scan angles", Mm(resMax), "mm", "Largest loop-closure residual", clause);

        var dead = link.DeadCentres();
        c.Output("dead_centres", "Dead centres (crank and rod collinear, slider velocity zero)", new TextValue(dead.Count == 0
            ? "none: the rod does not reach a dead centre"
            : string.Join("; ", dead.Select(dc => $"{dc.Name} at θ = {F(Deg(dc.Theta))}°"))));

        if (link.Stroke() is { } closed)
            Scalar(c, "stroke_closed", "√((l + r)² − e²) − √((l − r)² − e²)", "between the dead centres", Mm(closed), "mm", "Full slider stroke between the dead centres (closed form)", clause);

        Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tPk.Value.Value, "N*m", "Peak input torque by virtual work (mech.input_torque)", "Virtual work");
        Scalar(c, "output_motion", "max |v|", "dense scan, refined", vPk.Value.Value, "m/s", "Peak slider velocity (mech.output_motion)", clause);
        Scalar(c, "peak_accel", "max |a|", "dense scan, refined", aPk.Value.Value, "m/s^2", "Peak slider acceleration (mech.peak_accel)", clause);
        Scalar(c, "follower_force", "max |m·a + F_L|", "rod force on the slider", fPk.Value.Value, "N", "Peak rod force on the slider (mech.follower_force)", "Virtual work");

        c.Record("u_mu", "μ_req / μ_min", $"{F(Deg(muReq))}° / {F(Deg(muMin.Value.Value))}°", new ScalarValue(muMin.Value.Value > 1e-9 ? muReq / muMin.Value.Value : 1e9), "Transmission angle: required minimum against the smallest over the sweep", "Transmission angle");
        c.Output("u_mu", "Required against smallest transmission angle");
        c.CheckUtilisation("Transmission angle μ_min at or above the required minimum", "u_mu", "Transmission angle");
        ClosureCheck(c, resMax);
    }
}
