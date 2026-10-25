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
        Id, "1.1.0", CalcDiscipline.Mechanical, "Mechanism kinematics: four-bar, slider-crank, cam",
        "A single-degree-of-freedom planar mechanism driven at constant speed: a four-bar linkage or an offset slider-crank (both circuits, position, velocity and acceleration over a sweep of the input angle, Grashof class, transmission angle against a minimum, toggle and dead-centre positions, loop-closure residual, input torque by virtual work), or a cam with a translating follower. Rigid links, no clearance, no friction.",
        "Four-bar: a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄}; ℓ = |A − O₄|, cos γ = (c² + ℓ² − b²)/(2cℓ), θ₄ = φ ∓ γ (φ the direction O₄→A; open: −, crossed: +); θ₃ from B − A\n" +
        "Four-bar velocity and acceleration: differentiated loop, 2×2 system with det = b·c·sin(θ₃ − θ₄); ω₃ = ω₂·a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄)), ω₄ = ω₂·a·sin(θ₃ − θ₂)/(c·sin(θ₃ − θ₄)); α from the second derivative at constant ω₂\n" +
        "Grashof: s + l < p + q (crank-rocker, double-crank or double-rocker by the shortest link), = at the change point, > non-Grashof; transmission angle cos μ = (b² + c² − ℓ²)/(2bc) folded to 0 … 90°; toggle where ℓ = b + c or |b − c|\n" +
        "Slider-crank: x = r·cosθ + σ·√(l² − (r·sinθ − e)²) (σ = +1 open, −1 crossed); dx/dθ = −r·sinθ − r·cosθ·(r·sinθ − e)/(x − r·cosθ); dead centres where crank and rod are collinear; μ = 90° − |rod angle to the slide line|\n" +
        "Input torque by virtual work: T_in·ω_in = Σ F·v; four-bar T_in = (T_L + J·α₄)·ω₄/ω₂; slider T_in = (m·a + F_L)·dx/dθ; RMS torque and mean power over the sweep by Simpson's rule\n" +
        "Forces: four-bar tangential pin force P = (T_L + J·α₄)/c, coupler (and crank-pin) force P/sin μ; slider slide-direction force F_x = m·a + F_L, rod force F_x/cos θ₃, guide reaction F_x·tan θ₃; cam contact normal F/cos φ\n" +
        "Assembly limits: four-bar toggles at cos θ₂ = (d² + a² − ℓ²)/(2ad), ℓ = b + c or |b − c|; slider sin θ = (e ± l)/r. The sweep is cut at them and, driven at constant speed into one, the unbounded peaks are not reported\n" +
        "Loop closure: |a·e^{jθ₂} + b·e^{jθ₃} − c·e^{jθ₄} − d| (slider: |r·e^{jθ} + l·e^{jθ₃} − (x + j·e)|) asserted below 1e-9 mm at every step\n" +
        "Cam, per segment with x = (θ − θ₀)/β: rise s = s₀ + h·f(x), return s = s₀ − h·f(x); cycloidal f = x − sin(2πx)/(2π); 3-4-5 f = 10x³ − 15x⁴ + 6x⁵; 4-5-6-7 f = 35x⁴ − 84x⁵ + 70x⁶ − 20x⁷; modified trapezoid from its acceleration pieces (C_v = 2, C_a = 8π/(π + 2), C_j = 32π²/(π + 2))\n" +
        "Cam SVAJ at constant ω: v = ω·s′, a = ω²·s″, j = ω³·s‴ (′ = d/dθ); peak coefficients C_v = max f′, C_a = max |f″|, C_j = max |f‴|, so v_pk = C_v·h·ω/β\n" +
        "Pressure angle (roller, offset e, R_p = R_b + R_r): φ = arctan[(s′ − e)/(√(R_p² − e²) + s)], which for e = 0 is arctan[s′/(R_p + s)]; the base circle that holds φ ≤ φ_max has R_p,req = √(s₀,req² + e²) with s₀,req = max(|s′ − e|/tan φ_max − s)\n" +
        "Radius of curvature of the pitch curve P(θ) = Rot(−θ)·(e, √(R_p² − e²) + s): ρ = |P′|³/(P′ × P″), convex where positive; undercut where ρ_min ≤ R_r (FAIL), none where the curve is nowhere convex; flat-faced follower: signed profile radius ρ = R_b + s + s″, the minimum of which must stay above zero (FAIL at or below) and the face must reach |s′|; the cam turns counter-clockwise with the follower axis at +x offset e\n" +
        "Cam follower force F = m·a + k·s + F_pre + F_ext (the follower leaves the cam where F < 0); input torque by virtual work T = F·s′ (T·ω = F·v)",
        BuildInputs(),
        [
            new CalcAssumption("Rigid links, a planar mechanism with one degree of freedom, no joint clearance and no friction; the input turns at a constant speed.", "Addendum B §B6 M9 limits."),
            new CalcAssumption("The position is the closed-form solution of the vector loop; the open and crossed circuits are the two branches (θ₄ = φ − γ and φ + γ for the four-bar, σ = +1 and −1 for the slider) and the sweep stays on the chosen branch; for a change-point linkage (parallelogram, deltoid) the circuit is named at a reference angle that does not depend on the sweep (the middle of the first interval between change points in 0 … 2π, π/2 when there are none) and is followed by continuity through the change points. Angles at which the linkage cannot be assembled, or at a toggle where the velocity system is singular, are left out of the table and counted.", "Norton, Design of Machinery, position analysis of the four-bar and the offset slider-crank."),
            new CalcAssumption("Input torque is by virtual work from the output load and the inertia of the output element only (four-bar: the output link's moment of inertia about its pivot; slider: the slider mass); the crank and the coupler or connecting rod are massless.", "No link-inertia or gravity terms: add the link masses to the output element for a first allowance."),
            new CalcAssumption("The peaks (speeds, accelerations, torque, transmission angle, swing) are those of the continuous motion over the swept range, found by a dense scan refined by golden section; the printed table (steps N, up to 3600) only samples it.", "Peaks do not depend on the table's step count."),
            new CalcAssumption("Cam: a translating follower on a cam turning at constant speed; the follower mass, a linear return spring (rate and preload at the base circle, for example linked from M7) and a constant external load make the force the cam must apply, F = m·a + k·s + F_pre + F_ext; contact is lost where it falls below zero. The segment table must add to 360° and close; pressure angle and undercut are checked for the roller follower (R_p = R_b + R_r), the positive profile radius for the flat-faced one.", "Norton, Design of Machinery, cam design: the four classical laws, pressure angle and radius of curvature."),
            new CalcAssumption("Follower force (four-bar: the tangential force at the output pin, (T_L + J·α₄)/c; slider: the slide-direction force m·a + F_L; cam: the axial force) is the force the mechanism must apply to the output element. The force in the coupler (F_pin_pk) is the tangential force over sin μ; in the connecting rod it is the slide-direction force over cos θ₃, and the guide reaction is that force times tan θ₃; the roller contact normal force is F/cos φ. Crank, coupler and rod are massless two-force members.", "Rigid body, frictionless."),
            new CalcAssumption("When the sweep meets an assembly limit (a toggle of a four-bar, the rod perpendicular to the slide line of a slider-crank) the sweep is cut there and, because the speed, acceleration, torque and forces at constant input speed are unbounded at the limit, they are not reported; the limit angles are listed. A change-point four-bar (parallelogram, deltoid) is continued through its singular position on the same branch by continuity (γ changes sign there), but its speeds and torques next to the singular position are not reported.", "Closed-form limit angles: ℓ = b + c or |b − c| (four-bar), |r·sinθ − e| = l (slider)."),
            new CalcAssumption("Into F1 and E1: output_motion is a kinematic proxy at constant crank speed, not the cylinder speed of an actuated linkage. For E1 use the peak input_torque for the peak check and T_rms_in with n_in for the thermal sizing; the mean power is P_mean.", "Chains 4 and 5, Addendum B §B7."),
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
            new CalcOutputPort("input_torque", "N*m", "Peak input (crank or cam-shaft) torque by virtual work (mech.input_torque → E1 peak torque). For E1's thermal sizing use T_rms_in and n_in, not this peak as a constant load"),
            new CalcOutputPort("output_motion", "m/s", "Peak output velocity: the slider or the cam follower; for a four-bar the speed of the output pin at the rocker length c (the angular velocity is omega_out_pk) (mech.output_motion). Into F1 this is a kinematic proxy only: a cylinder-actuated linkage runs at about constant cylinder speed, not at constant crank speed, and a four-bar pin speed is a cylinder speed only if the cylinder acts perpendicular to the rocker at that radius"),
            new CalcOutputPort("peak_accel", "m/s^2", "Peak output acceleration: the slider or the follower; for a four-bar the magnitude of the acceleration of the output pin (mech.peak_accel)"),
            new CalcOutputPort("follower_force", "N", "Peak drive force on the output element: the cam follower's axial force, the slider's force in the slide direction, or the tangential force at the rocker pin (mech.follower_force → F1 as a force estimate)"),
            new CalcOutputPort("F_pin_pk", "N", "Peak force at the crank or cam-shaft pin: the force in the coupler (four-bar), in the connecting rod (slider), or the contact normal force F/cos φ (cam roller) (mech.F_pin_pk → M4 bearing load, M5)"),
            new CalcOutputPort("T_rms_in", "N*m", "RMS input torque over the swept range or the cam revolution (mech.T_rms_in → E1 thermal sizing)"),
            new CalcOutputPort("n_in", "rpm", "Input (crank or cam-shaft) speed passed through (mech.n_in → E1 load speed)"),
            new CalcOutputPort("P_mean", "W", "Mean input power over the swept range or the cam revolution (mech.P_mean)"),
            new CalcOutputPort("lift", "mm", "Greatest follower displacement (cam) (mech.lift → M7 spring travel)"),
            new CalcOutputPort("F_spring_max", "N", "Spring force at full lift, k·lift + F_pre (cam) (mech.F_spring_max → M7 working force; the follower force includes inertia and is not the spring force)"),
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
            new("e_off", "Offset e: slide line above the crank centre (slider-crank; blank: 0); cam follower axis offset, with the cam turning counter-clockwise and the follower axis at +x: positive e reduces the pressure angle where ds/dθ exceeds e (the rise), negative raises it", CalcInputKind.Quantity, "mm", Default: 0, Min: -1000, Max: 1000, HardMin: -1e5, HardMax: 1e5, IsOptional: true, Group: "Links"),
            new("theta_start", "Input angle at the first row of the table", CalcInputKind.Quantity, "deg", Default: 0, Min: -720, Max: 720, HardMin: -7200, HardMax: 7200, IsOptional: true, Group: "Sweep"),
            new("theta_range", "Swept range of the input angle (360° for a full turn)", CalcInputKind.Quantity, "deg", Default: 360, Min: 1, Max: 720, HardMin: 0.1, HardMax: 3600, IsOptional: true, Group: "Sweep"),
            new("steps", "Intervals N of the table (N + 1 rows, a whole number, at most 3600; the peaks do not depend on it)", CalcInputKind.Number, Default: 72, Min: 4, Max: 720, HardMin: 2, HardMax: 3600, Group: "Sweep"),
            new("n_in", "Input speed (constant)", CalcInputKind.Quantity, "rpm", Default: 120, Min: 0.1, Max: 30000, HardMin: 1e-3, HardMax: 1e6, Group: "Sweep"),
            new("mu_req", "Minimum transmission angle required (linkages)", CalcInputKind.Quantity, "deg", Default: 40, Min: 0, Max: 90, HardMin: 0, HardMax: 90, IsOptional: true, Group: "Limits"),
            new("load_dir", "External load: constant direction, or always opposing the output motion", CalcInputKind.Choice, Choices: [Constant, Opposes], DefaultChoice: Opposes, IsOptional: true, Group: "Loads"),
            new("T_load", "External load torque on the output link (four-bar). Positive acts in the −θ₄ direction, resisting positive ω₄; with 'Opposes motion' its sign is applied against the actual direction of ω₄ (a negative value then assists)", CalcInputKind.Quantity, "N*m", Default: 1.5, Min: -10000, Max: 10000, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("J_out", "Moment of inertia of the output link about its pivot (four-bar)", CalcInputKind.Quantity, "kg*m^2", Default: 0.002, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("m_slider", "Slider mass (slider-crank)", CalcInputKind.Quantity, "kg", Default: 0.5, Min: 0, Max: 10000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("F_load", "External load on the slider (slider-crank). Positive acts in the −x direction, resisting +x motion; with 'Opposes motion' it is applied against the actual direction of motion", CalcInputKind.Quantity, "N", Default: 200, Min: -1e6, Max: 1e6, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
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
            new("limit", "The requested sweep reaches an assembly limit (a toggle, or the rod perpendicular to the slide line) or passes a change point: the sweep is cut where the mechanism cannot be assembled, and when the limit is inside it the speed, acceleration, torque and force peaks, which are unbounded at constant input speed, and the ports are not reported.", CalcNoticeSeverity.Warning),
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

        if (!Require(c, mode == FourBar ? new[] { "L1", "L2", "L3", "L4", "theta_start", "theta_range" } : new[] { "L2", "L3", "theta_start", "theta_range" }))
            return;

        var omega = c.InputSi("n_in");
        c.Output("n_in", "Input (crank) speed, passed through for E1's load speed (mech.n_in)");
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
        public int Dense => Math.Clamp((int)Math.Ceiling(Range * 180 / Math.PI * 4), 720, 14400) / 2 * 2;
    }

    private static void Table(CalcRunContext c, string symbol, string formula, string substitution, IReadOnlyList<double> values, string unit, string description, string? clause = null)
    {
        c.Record(symbol, formula, substitution, VectorValue.Of(values, unit), description, clause);
        c.Output(symbol, description);
    }

    private static void Scalar(CalcRunContext c, string symbol, string formula, string substitution, double value, string unit, string description, string? clause = null)
    {
        c.Record(symbol, formula, substitution, unit.Length == 0 ? new ScalarValue(value) : ScalarValue.Of(value, unit), description, clause);
        c.Output(symbol, description);
    }

    private static double Sgn(double x) => x > 0 ? 1 : x < 0 ? -1 : 0;

    private static double Deg(double rad) => rad * 180 / Math.PI;

    private static double Mm(double m) => m * 1e3;

    // ------------------------------------------------------------------
    // Shared helpers for the linkages
    // ------------------------------------------------------------------

    /// <summary>The mean of a function sampled at N + 1 equal steps (N even), by Simpson's rule.</summary>
    private static double SimpsonMean(IReadOnlyList<double> y)
    {
        var n = y.Count - 1;
        var sum = y[0] + y[n];

        for (var i = 1; i < n; i++)
            sum += (i % 2 == 1 ? 4 : 2) * y[i];

        return sum / (3.0 * n);
    }

    private static double Wrap360(double radians)
    {
        var w = radians % (2 * Math.PI);
        return w < 0 ? w + 2 * Math.PI : w;
    }

    private static string AnglesText(IEnumerable<double> radians) => string.Join(", ", radians.Select(t => $"{F(Deg(Wrap360(t)))}°").Distinct());

    /// <summary>The limit angles as text, each marked when it lies within the requested sweep.</summary>
    private static string MarkedAngles(IEnumerable<double> radians, double start, double end) =>
        string.Join(", ", radians.Select(t => $"{F(Deg(Wrap360(t)))}°{(MechanismSweep.Unwrap([t], start, end).Count > 0 ? " (in the requested sweep)" : string.Empty)}"));

    private static string SweepText(double start, double end) => $"{F(Deg(start))}° … {F(Deg(end))}°";

    private static void UtilisationMu(CalcRunContext c, double muReq, double muMin)
    {
        var u = muMin > 1e-9 ? muReq / muMin : (muReq > 0 ? 1e9 : 0);
        c.Record("u_mu", "μ_req / μ_min", $"{F(Deg(muReq))}° / {F(Deg(muMin))}°", new ScalarValue(u), "Transmission angle: required minimum against the smallest over the sweep", "Transmission angle");
        c.Output("u_mu", "Required against smallest transmission angle");
        c.CheckUtilisation("Transmission angle μ_min at or above the required minimum", "u_mu", "Transmission angle");
    }

    /// <summary>The mean-power, RMS-torque and speed outputs E1 and the motor chain need (over the swept range).</summary>
    private static void DriveOutputs(CalcRunContext c, IReadOnlyList<double> torque, double omega, string span)
    {
        var mean = SimpsonMean(torque);
        var rms = Math.Sqrt(SimpsonMean([.. torque.Select(t => t * t)]));
        Scalar(c, "T_rms_in", "√(mean T_in²)", span, rms, "N*m", "RMS input torque over the swept range (mech.T_rms_in → E1 thermal sizing)", "Virtual work");
        Scalar(c, "P_mean", "ω·mean(T_in)", span, omega * mean, "W", "Mean input power over the swept range (negative: the mechanism returns energy) (mech.P_mean)", "Virtual work");
    }

    // ------------------------------------------------------------------
    // Four-bar
    // ------------------------------------------------------------------

    private static void RunFourBar(CalcRunContext c, Sweep req, bool crossed, double muReq, bool opposes)
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
        var w2 = req.Omega;

        var cls = FourBarLinkage.Classify(d, a, b, cc, out var sl, out var pq, out var full);
        c.Output("grashof", "Grashof classification", new TextValue(cls));
        Scalar(c, "S_plus_L", "s + l", "shortest + longest link", Mm(sl), "mm", "Shortest plus longest link", "Grashof criterion");
        Scalar(c, "P_plus_Q", "p + q", "the other two links", Mm(pq), "mm", "Sum of the other two links", "Grashof criterion");

        if (!full && req.Range >= 2 * Math.PI - 1e-9)
            c.Warn("grashof", $"L2: {cls}. A swept range of 360° or more cannot be completed; the sweep is cut where the linkage cannot be assembled.");

        var link = new FourBarLinkage(d, a, b, cc, crossed);
        var changePoint = FourBarLinkage.IsChangePoint(d, a, b, cc);
        var limits = link.ToggleCrankAngles();
        var reqEnd = req.Start + req.Range;
        var arc = MechanismSweep.Clip(req.Start, reqEnd, limits, t => link.At(t, w2) is not null);

        if (!arc.Found)
        {
            c.Block("geometry", $"L1: the four links cannot be assembled at any crank angle of the swept range ({SweepText(req.Start, reqEnd)}); check the link lengths (|b − c| ≤ ℓ ≤ b + c must hold with ℓ = |A − O₄|). {cls}.");
            return;
        }

        var sw = new Sweep(arc.Lo, arc.Hi - arc.Lo, req.Steps, req.Omega);
        var unbounded = arc.LimitsInside.Count > 0;

        // The circuit names a branch at a reference angle that does not depend on the sweep: the middle of the first interval between
        // change points in 0 … 2π (π/2 when there are none). Flips are counted from there, so the same circuit means the same motion
        // whatever theta_start is.
        var reference = Math.PI / 2;

        if (changePoint && limits.Count > 0)
        {
            var points = new List<double> { 0 };
            points.AddRange(limits.Where(x => x > 1e-9));
            points.Add(2 * Math.PI);
            reference = 0.5 * (points[0] + points[1]);
        }

        // A change-point linkage passes through its singular positions; the circuit is followed by continuity (γ changes sign there).
        int Flips(double t)
        {
            if (!changePoint)
                return 0;

            var lo = Math.Min(reference, t);
            var hi = Math.Max(reference, t);
            return MechanismSweep.Unwrap(limits, lo, hi).Count(x => x > lo + 1e-9 && x < hi - 1e-9);
        }

        FourBarState? St(double t) => link.At(t, w2, Flips(t));

        if (arc.Clipped)
            c.Warn("limit", $"theta_range: the requested sweep {SweepText(req.Start, reqEnd)} is cut to {SweepText(arc.Lo, arc.Hi)}: beyond it the linkage cannot be assembled. The toggle angles that bound the crank's range are θ₂ = {MarkedAngles(limits, req.Start, reqEnd)}.");

        if (unbounded)
        {
            c.Warn("limit", changePoint
                ? $"theta_range: the sweep passes the change point at θ₂ = {AnglesText(arc.LimitsInside)}. The table follows the same circuit through it by continuity, but the velocity system is singular there and the speeds, accelerations, torque and forces next to it are not reliable, so they are not reported. Sweep between the change-point angles (set theta_start and theta_range inside the stretch) to get them."
                : $"theta_range: the sweep reaches the toggle at θ₂ = {AnglesText(arc.LimitsInside)}, where coupler and rocker are collinear: driven at constant speed the rocker's speed, acceleration, the input torque and the forces are unbounded there, so they and the ports are not reported. Set theta_start and theta_range inside the assemblable stretch (between the toggle angles) or reverse the crank before the toggle.");
        }

        // The table.
        var rows = new List<FourBarState>();
        var unreachable = 0;
        var singular = 0;

        for (var i = 0; i <= sw.Steps; i++)
        {
            var st = St(sw.At(i));

            if (st is null)
                unreachable++;
            else if (st.Value.Singular)
                singular++;
            else
                rows.Add(st.Value);
        }

        if (rows.Count == 0)
        {
            c.Block("geometry", $"L1: every row of the sweep {SweepText(arc.Lo, arc.Hi)} is a toggle or cannot be assembled; change theta_start and theta_range (toggle angles θ₂ = {AnglesText(limits)}). {cls}.");
            return;
        }

        if ((unreachable > 0 || singular > 0) && !unbounded)
            c.Warn("reach", $"L2: {unreachable} of {sw.Steps + 1} table rows cannot be assembled and {singular} sit at a toggle (coupler and rocker collinear, no defined velocity); they are left out of the table.");

        // Dense scan (for peaks and integrals).
        var n = sw.Dense;
        var xs = new double[n + 1];
        var states = new FourBarState?[n + 1];
        var resMax = rows.Max(r => r.Residual);

        for (var j = 0; j <= n; j++)
        {
            xs[j] = sw.Start + sw.Range * j / n;
            states[j] = St(xs[j]);

            if (states[j] is { } s)
                resMax = Math.Max(resMax, s.Residual);
        }

        if (!unbounded && states.Any(s => s is null || s.Value.Singular))
        {
            unbounded = true;
            c.Warn("limit", "theta_range: a toggle lies inside the sweep, where the rocker's velocity at constant crank speed is unbounded; the peaks, torque, forces and ports are not reported. Set theta_start and theta_range away from it.");
        }

        (double Value, double At)? Peak(Func<FourBarState, double> select, bool max, bool allowSingular = false)
        {
            double Eval(FourBarState? s) => s is { } v && (allowSingular || !v.Singular) ? select(v) : double.NaN;
            var ys = states.Select(Eval).ToArray();
            return MechanismPeaks.Refine(xs, ys, (_, x) => Eval(St(x)), max);
        }

        double Tc(double omega4) => opposes ? torque * Sgn(omega4) : torque;
        double Tangential(FourBarState s) => (Tc(s.Omega4) + inertia * s.Alpha4) / cc;
        double TIn(FourBarState s) => (Tc(s.Omega4) + inertia * s.Alpha4) * s.Omega4 / w2;
        double Coupler(FourBarState s) => Math.Abs(Tangential(s)) / Math.Abs(Math.Sin(s.Theta3 - s.Theta4));

        var muFound = Peak(s => s.Mu, false, allowSingular: true);
        var muMin = unbounded ? 0 : muFound?.Value ?? 0;

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

        double Theta4Near(int j, double x) => St(x) is { } v ? ys4[j] + WrapPi(v.Theta4 - ys4[j]) : double.NaN;
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
        var probe = St(sw.Start);

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
            var other = new FourBarLinkage(d, a, b, cc, !crossed).At(sw.Start, w2, Flips(sw.Start));

            if (other is { } o)
                Scalar(c, "theta4_alt", "θ₄ on the other circuit", "mirror branch, " + at0, Deg(o.Theta4), "deg", "Rocker angle on the other circuit at the first input angle", clause);
        }

        Scalar(c, "theta4_min", "min θ₄", "unwrapped along the sweep", Deg(t4Min), "deg", "Smallest rocker angle", clause);
        Scalar(c, "theta4_max", "max θ₄", "unwrapped along the sweep", Deg(t4Max), "deg", "Largest rocker angle", clause);
        Scalar(c, "swing", "max θ₄ − min θ₄", "over the sweep", Deg(t4Max - t4Min), "deg", "Rocker swing over the sweep", clause);
        Scalar(c, "mu_min", "min μ", unbounded ? "0 at a toggle in the sweep" : "dense scan, refined", Deg(muMin), "deg", "Smallest transmission angle over the sweep", clause);
        Scalar(c, "res_max", "max |loop residual|", $"{rows.Count} table rows and {n + 1} scan angles", Mm(resMax), "mm", "Largest loop-closure residual", clause);

        var velRes = rows.Where(r => Math.Abs(Math.Sin(r.Theta3 - r.Theta4)) > 1e-5).Select(r => r.VelocityResidual).DefaultIfEmpty(0).Max();
        Scalar(c, "res_vel_max", "max |velocity loop| / its terms", $"{rows.Count} table rows", velRes, "", "Largest velocity-loop residual, relative to the size of its terms", clause);

        c.Output("toggle_angles", "Toggle crank angles (coupler and rocker collinear, μ = 0): the limits of the crank's assemblable range", new TextValue(limits.Count == 0
            ? "none: coupler and rocker are never collinear"
            : "θ₂ = " + MarkedAngles(limits, req.Start, reqEnd)));

        var collinear = link.CollinearPositions();
        c.Output("limit_positions", "Crank and coupler collinear (the rocker's dead-centre positions)", new TextValue(collinear.Count == 0
            ? "none on this circuit"
            : string.Join("; ", collinear.Select(p => $"θ₂ = {F(Deg(p.Theta2))}° with θ₄ = {F(Deg(WrapPi(p.Theta4)))}°"))));

        if (!unbounded)
        {
            var omega3Pk = Peak(s => Math.Abs(s.Omega3), true);
            var omega4Pk = Peak(s => Math.Abs(s.Omega4), true);
            var alpha3Pk = Peak(s => Math.Abs(s.Alpha3), true);
            var alpha4Pk = Peak(s => Math.Abs(s.Alpha4), true);
            var tInPk = Peak(s => Math.Abs(TIn(s)), true);
            var fPk = Peak(s => Math.Abs(Tangential(s)), true);
            var couplerPk = Peak(Coupler, true);
            var aPinPk = Peak(s => cc * Math.Sqrt(s.Alpha4 * s.Alpha4 + Math.Pow(s.Omega4, 4)), true);

            if (omega4Pk is null || tInPk is null || fPk is null || couplerPk is null || aPinPk is null || omega3Pk is null || alpha3Pk is null || alpha4Pk is null)
            {
                c.Block("geometry", "L1: the sweep has no row with a defined velocity (every assembled row is a toggle); change the swept range or the link lengths.");
                return;
            }

            Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", omega3Pk.Value.Value, "rad/s", "Peak coupler angular velocity", clause);
            Scalar(c, "omega_out_pk", "max |ω₄|", "dense scan, refined", omega4Pk.Value.Value, "rad/s", "Peak output (rocker) angular velocity", clause);
            Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", alpha3Pk.Value.Value, "rad/s^2", "Peak coupler angular acceleration", clause);
            Scalar(c, "alpha_out_pk", "max |α₄|", "dense scan, refined", alpha4Pk.Value.Value, "rad/s^2", "Peak output (rocker) angular acceleration", clause);

            // The ports.
            Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tInPk.Value.Value, "N*m", "Peak input (crank) torque by virtual work (mech.input_torque → E1 peak torque)", "Virtual work");
            Scalar(c, "output_motion", "c · max |ω₄|", "output pin at the rocker length", cc * omega4Pk.Value.Value, "m/s", "Peak speed of the output pin at the rocker length c (mech.output_motion; a kinematic proxy for F1 only if the cylinder acts perpendicular to the rocker at that radius; the angular velocity is omega_out_pk)", clause);
            Scalar(c, "peak_accel", "max |a_pin|", "√(α₄² + ω₄⁴)·c", aPinPk.Value.Value, "m/s^2", "Peak acceleration of the output pin (mech.peak_accel)", clause);
            Scalar(c, "follower_force", "max |(T_L + J·α₄)/c|", "tangential force at the output pin", fPk.Value.Value, "N", "Peak force perpendicular to the rocker at the output pin, the drive force of the output (mech.follower_force)", "Virtual work");
            Scalar(c, "F_pin_pk", "max |(T_L + J·α₄)/(c·sin μ)|", "force in the coupler = the crank-pin force", couplerPk.Value.Value, "N", "Peak force in the coupler, which is also the force at the crank pin and the pin loads on the bearings (mech.F_pin_pk → M4, M5)", "Virtual work");

            var dense = states.Select(s => TIn(s!.Value)).ToList();
            DriveOutputs(c, dense, w2, "Simpson over the scan");
        }

        UtilisationMu(c, muReq, muMin);
        ClosureCheck(c, resMax);
        c.CheckCondition("Velocity-loop residual below 1e-8 of the size of its terms", velRes < 1e-8, $"max residual = {CalcValue.FormatNumber(velRes)}", "< 1e-8", "res_vel_max", "Self-consistency");
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

    private static void RunSlider(CalcRunContext c, Sweep req, bool crossed, double muReq, bool opposes)
    {
        var r = c.Si("L2");
        var l = c.Si("L3");
        var e = c.Has("e_off") ? c.InputSi("e_off") : 0;

        if (c.Has("m_slider"))
            c.Input("m_slider");

        if (c.Has("F_load"))
            c.Input("F_load");

        var mass = c.Has("m_slider") ? c.Si("m_slider") : 0;
        var load = c.Has("F_load") ? c.Si("F_load") : 0;
        var w = req.Omega;
        var link = new SliderCrankLinkage(r, l, e, crossed);

        var cls = link.FullRotation
            ? "Crank-slider: the crank can turn through 360° (l ≥ r + |e|)"
            : "Rocker-slider: the crank cannot turn fully (l < r + |e|), the sweep is limited";
        c.Output("slider_class", "Class of the slider-crank", new TextValue(cls));

        var limits = link.LimitAngles();
        var reqEnd = req.Start + req.Range;
        var arc = MechanismSweep.Clip(req.Start, reqEnd, limits, t => link.At(t) is not null);

        if (!arc.Found)
        {
            c.Block("geometry", $"L3: the rod cannot reach the slide line at any crank angle of the swept range ({SweepText(req.Start, reqEnd)}) (|r·sinθ − e| ≤ l must hold); {cls}.");
            return;
        }

        var sw = new Sweep(arc.Lo, arc.Hi - arc.Lo, req.Steps, req.Omega);
        var unbounded = arc.LimitsInside.Count > 0;

        if (arc.Clipped)
            c.Warn("limit", $"theta_range: the requested sweep {SweepText(req.Start, reqEnd)} is cut to {SweepText(arc.Lo, arc.Hi)}: beyond it the rod cannot reach the slide line. The limit angles (rod perpendicular to the slide line) are θ = {MarkedAngles(limits, req.Start, reqEnd)}.");

        if (unbounded)
            c.Warn("limit", $"theta_range: the sweep reaches the limit at θ = {AnglesText(arc.LimitsInside)}, where the rod is perpendicular to the slide line: driven at constant speed the slider's velocity, acceleration, the torque and the forces are unbounded there, so they and the ports are not reported. Set theta_start and theta_range inside the assemblable stretch (between the limit angles) or reverse the crank before the limit.");

        var rows = new List<SliderState>();
        var unreachable = 0;

        for (var i = 0; i <= sw.Steps; i++)
        {
            if (link.At(sw.At(i)) is { RodCos: > 1e-9 } st)
                rows.Add(st);
            else
                unreachable++;
        }

        if (rows.Count == 0)
        {
            c.Block("geometry", $"L3: no row of the sweep {SweepText(arc.Lo, arc.Hi)} can be assembled; change theta_start and theta_range.");
            return;
        }

        if (unreachable > 0 && !unbounded)
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

        if (!unbounded && states.Any(s => s is null))
        {
            unbounded = true;
            c.Warn("limit", "theta_range: part of the sweep cannot be assembled; the peaks, torque, forces and ports are not reported. Set theta_start and theta_range inside the assemblable stretch.");
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
        var muFound = Peak(s => s.Mu, false);

        if (xMax is null || xMin is null || muFound is null)
        {
            c.Block("geometry", "L3: the sweep has no assembled row; change the swept range or the link lengths.");
            return;
        }

        var muMin = unbounded ? 0 : muFound.Value.Value;

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
        Scalar(c, "mu_min", "min μ", unbounded ? "0 at a limit in the sweep" : "dense scan, refined", Deg(muMin), "deg", "Smallest transmission angle over the sweep", clause);
        Scalar(c, "res_max", "max |loop residual|", $"{rows.Count} table rows and {n + 1} scan angles", Mm(resMax), "mm", "Largest loop-closure residual", clause);

        var velRes = rows.Where(row => row.RodCos > 1e-5).Select(row => row.VelocityResidual).DefaultIfEmpty(0).Max();
        Scalar(c, "res_vel_max", "max |velocity loop| / its terms", $"{rows.Count} table rows", velRes, "", "Largest velocity-loop residual, relative to the size of its terms", clause);

        var dead = link.DeadCentres();
        c.Output("dead_centres", "Dead centres (crank and rod collinear, slider velocity zero)", new TextValue(dead.Count == 0
            ? "none: the rod does not reach a dead centre"
            : string.Join("; ", dead.Select(dc => $"{dc.Name} at θ = {F(Deg(dc.Theta))}°"))));

        c.Output("limit_angles", "Limit angles (rod perpendicular to the slide line): the bounds of the crank's assemblable range", new TextValue(limits.Count == 0
            ? "none: the crank turns fully"
            : "θ = " + MarkedAngles(limits, req.Start, reqEnd)));

        if (link.Stroke() is { } closed)
            Scalar(c, "stroke_closed", "√((l + r)² − e²) − √((l − r)² − e²)", "between the dead centres", Mm(closed), "mm", "Full slider stroke between the dead centres (closed form)", clause);

        if (!unbounded)
        {
            var vPk = Peak(s => Math.Abs(V(s)), true);
            var aPk = Peak(s => Math.Abs(A(s)), true);
            var w3Pk = Peak(s => Math.Abs(w * s.Dtheta3), true);
            var al3Pk = Peak(s => Math.Abs(w * w * s.D2theta3), true);
            var tPk = Peak(s => Math.Abs(TIn(s)), true);
            var fPk = Peak(s => Math.Abs(Fr(s)), true);
            var rodPk = Peak(s => Math.Abs(Fr(s)) / s.RodCos, true);
            var guidePk = Peak(s => Math.Abs(Fr(s)) * s.RodTan, true);

            if (vPk is null || aPk is null || w3Pk is null || al3Pk is null || tPk is null || fPk is null || rodPk is null || guidePk is null)
            {
                c.Block("geometry", "L3: the sweep has no assembled row; change the swept range or the link lengths.");
                return;
            }

            Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", w3Pk.Value.Value, "rad/s", "Peak connecting-rod angular velocity", clause);
            Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", al3Pk.Value.Value, "rad/s^2", "Peak connecting-rod angular acceleration", clause);
            Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tPk.Value.Value, "N*m", "Peak input (crank) torque by virtual work (mech.input_torque → E1 peak torque)", "Virtual work");
            Scalar(c, "output_motion", "max |v|", "dense scan, refined", vPk.Value.Value, "m/s", "Peak slider velocity (mech.output_motion; the cylinder speed of F1 for a slider driven at constant crank speed, a kinematic proxy)", clause);
            Scalar(c, "peak_accel", "max |a|", "dense scan, refined", aPk.Value.Value, "m/s^2", "Peak slider acceleration (mech.peak_accel)", clause);
            Scalar(c, "follower_force", "max |m·a + F_L|", "slide-direction force on the slider", fPk.Value.Value, "N", "Peak force on the slider in the slide direction, the rod's component along the slide line (mech.follower_force)", "Virtual work");
            Scalar(c, "F_pin_pk", "max |m·a + F_L|/cosθ₃", "force along the connecting rod", rodPk.Value.Value, "N", "Peak force in the connecting rod, also the crank-pin load (mech.F_pin_pk → M4, M5)", "Virtual work");
            Scalar(c, "F_guide_pk", "max |m·a + F_L|·tanθ₃", "reaction of the slide on the slider", guidePk.Value.Value, "N", "Peak side reaction of the guide on the slider (frictionless)", "Virtual work");

            var dense = states.Select(s => TIn(s!.Value)).ToList();
            DriveOutputs(c, dense, w, "Simpson over the scan");
        }

        UtilisationMu(c, muReq, muMin);
        ClosureCheck(c, resMax);
        c.CheckCondition("Velocity-loop residual below 1e-8 of the size of its terms", velRes < 1e-8, $"max residual = {CalcValue.FormatNumber(velRes)}", "< 1e-8", "res_vel_max", "Self-consistency");
    }
}
