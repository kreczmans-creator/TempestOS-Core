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
        Id, "1.3.1", CalcDiscipline.Mechanical, "Mechanism kinematics",
        "Calculates the position, velocity, acceleration, input torque and forces of a one-degree-of-freedom planar four-bar, slider-crank or cam mechanism. The input turns at constant speed, and the links are rigid, with no clearance and no friction.",
        "Four-bar position: a·e^{jθ₂} + b·e^{jθ₃} = d + c·e^{jθ₄}, with ℓ = |A − O₄|, cos γ = (c² + ℓ² − b²)/(2cℓ) and θ₄ = φ ∓ γ (φ the direction O₄→A; open: −, crossed: +); θ₃ from B − A\n" +
        "Four-bar velocity and acceleration: differentiated loop, 2×2 system with det = b·c·sin(θ₃ − θ₄); ω₃ = ω₂·a·sin(θ₄ − θ₂)/(b·sin(θ₃ − θ₄)), ω₄ = ω₂·a·sin(θ₃ − θ₂)/(c·sin(θ₃ − θ₄)); α from the second derivative at constant ω₂\n" +
        "Grashof class and transmission angle: s + l < p + q (crank-rocker, double-crank or double-rocker by the shortest link), = at the change point, > non-Grashof; transmission angle cos μ = (b² + c² − ℓ²)/(2bc) folded to 0 … 90°; toggle where ℓ = b + c or |b − c|\n" +
        "Slider-crank position: x = r·cosθ + σ·√(l² − (r·sinθ − e)²) (σ = +1 open, −1 crossed); dx/dθ = −r·sinθ − r·cosθ·(r·sinθ − e)/(x − r·cosθ); dead centres where crank and rod are collinear; μ = 90° − |rod angle to the slide line|\n" +
        "Input torque and forces: T_in·ω_in = Σ F·v by virtual work; four-bar T_in = (T_L + J·α₄)·ω₄/ω₂, tangential pin force P = (T_L + J·α₄)/c and coupler force P/sin μ; slider T_in = (m·a + F_L)·dx/dθ, slide-direction force F_x = m·a + F_L, rod force F_x/cos θ₃ and guide reaction F_x·tan θ₃; RMS torque and mean power over the sweep by Simpson's rule\n" +
        "Assembly limits and loop closure: four-bar toggles at cos θ₂ = (d² + a² − ℓ²)/(2ad), ℓ = b + c or |b − c|; slider sin θ = (e ± l)/r; the sweep is cut at them and the unbounded peaks are not reported; closure |a·e^{jθ₂} + b·e^{jθ₃} − c·e^{jθ₄} − d| (slider: |r·e^{jθ} + l·e^{jθ₃} − (x + j·e)|) below 1e-9 mm at every step\n" +
        "Cam motion: per segment with x = (θ − θ₀)/β, rise s = s₀ + h·f(x), return s = s₀ − h·f(x); cycloidal f = x − sin(2πx)/(2π); 3-4-5 f = 10x³ − 15x⁴ + 6x⁵; 4-5-6-7 f = 35x⁴ − 84x⁵ + 70x⁶ − 20x⁷; modified trapezoid from its acceleration pieces (C_v = 2, C_a = 8π/(π + 2), C_j = 32π²/(π + 2)); SVAJ at constant ω: v = ω·s′, a = ω²·s″, j = ω³·s‴ (′ = d/dθ), so v_pk = C_v·h·ω/β\n" +
        "Cam contact: roller pressure angle φ = arctan[(s′ − e)/(√(R_p² − e²) + s)] with R_p = R_b + R_r, base circle R_p,req = √(s₀,req² + e²) with s₀,req = max(|s′ − e|/tan φ_max − s); pitch-curve radius ρ = |P′|³/(P′ × P″), undercut where ρ_min ≤ R_r; flat-faced profile radius ρ = R_b + s + s″ above zero; follower force F = m·a + k·s + F_pre + F_ext (the follower leaves the cam where F < 0), input torque T = F·s′, roller normal force F/cos φ",
        BuildInputs(),
        [
            new CalcAssumption("The mechanism is rigid and planar, has one degree of freedom, and has no joint clearance or friction.", "Scope limits of the model."),
            new CalcAssumption("The input turns at a constant speed.", "Scope limits of the model."),
            new CalcAssumption("The position is the closed-form solution of the vector loop, on the chosen circuit.", "Open and crossed are the two branches: θ₄ = φ − γ and φ + γ for the four-bar, σ = +1 and −1 for the slider."),
            new CalcAssumption("The sweep stays on the chosen circuit and follows it by continuity through a change point.", "Only a change point, with all four links collinear (θ₂ = 0 or π), flips the branch."),
            new CalcAssumption("A linkage with one change point per turn repeats every 720°, and a sweep of more than one turn warns.", "A kite, for example 40/40/100/100, is such a linkage."),
            new CalcAssumption("The table leaves out angles where the linkage cannot be assembled or sits at a toggle.", "It counts them, and at a toggle the velocity system is singular."),
            new CalcAssumption("The input torque comes from the output load and the inertia of the output element only.", "The crank and the coupler or connecting rod are massless: add their mass to the output element for a first allowance."),
            new CalcAssumption("The crank, the coupler and the connecting rod are massless two-force members.", "Rigid body, frictionless."),
            new CalcAssumption("The peaks are those of the continuous motion over the swept range, not of the printed table.", "A dense scan refined by golden section finds them, so they do not depend on the step count of the table (up to 3600)."),
            new CalcAssumption("At an assembly limit the sweep is cut, and the unbounded peaks, torque and forces are not reported.", "The limit angles are listed: ℓ = b + c or |b − c| (four-bar), |r·sinθ − e| = l (slider)."),
            new CalcAssumption("A change-point four-bar is continued through its singular position on the same branch.", "Its speeds and torques next to the singular position are not reported."),
            new CalcAssumption("The cam drives a translating follower at constant speed.", "Norton, Design of Machinery, cam design: the four classical laws, pressure angle and radius of curvature."),
            new CalcAssumption("The follower force is m·a + k·s + F_pre + F_ext, and contact is lost below zero.", "The spring rate and the preload are those at the base circle, and the segment table must add to 360° and close."),
            new CalcAssumption("Pressure angle and undercut are checked for the roller follower, and the profile radius for the flat-faced one.", "The roller has the prime radius R_p = R_b + R_r."),
            new CalcAssumption("The output speed is a kinematic proxy at constant input speed, not the speed of an actuated cylinder.", "For motor sizing, use the peak input_torque for the peak check and T_rms_in with n_in for the thermal sizing. The mean power is P_mean."),
        ],
        [
            new CalcClauseRef("Norton, Design of Machinery", "Ch. 4, 6, 7, 6th ed.", "Position, velocity and acceleration analysis; transmission angle; Grashof condition"),
            new CalcClauseRef("Uicker, Pennock and Shigley, Theory of Machines and Mechanisms", "Ch. 4–6, 5th ed.", "Loop-closure position analysis; velocity and acceleration of planar linkages"),
        ],
        BuildRules(),
        BuildCases(),
        Category: "Mechanisms",
        OutputPorts:
        [
            new CalcOutputPort("input_torque", "N*m", "Peak input torque (crank or cam shaft) by virtual work. For thermal sizing use T_rms_in and n_in, not this peak as a constant load", "mechanism.input_torque"),
            new CalcOutputPort("output_motion", "m/s", "Peak output velocity: the slider or the cam follower, or for a four-bar the speed of the output pin at the rocker length c (omega_out_pk is the angular velocity). Into a cylinder sheet this is a kinematic proxy only: a cylinder-actuated linkage runs at about constant cylinder speed, not at constant crank speed", "mechanism.output_motion"),
            new CalcOutputPort("peak_accel", "m/s^2", "Peak output acceleration: the slider or the follower, or for a four-bar the magnitude of the acceleration of the output pin", "mechanism.peak_accel"),
            new CalcOutputPort("follower_force", "N", "Peak drive force on the output element: the cam follower's axial force, the slider's force in the slide direction, or the tangential force at the rocker pin. Use it as a force estimate", "mechanism.follower_force"),
            new CalcOutputPort("F_pin_pk", "N", "Peak force at the crank or cam-shaft pin: the force in the coupler (four-bar), in the connecting rod (slider), or the contact normal force F/cos φ (cam roller). Use it as a bearing load", "mechanism.pin_force_peak"),
            new CalcOutputPort("T_rms_in", "N*m", "RMS input torque over the swept range or the cam revolution. Use it for thermal sizing", "mechanism.torque_rms_input"),
            new CalcOutputPort("n_in", "rpm", "Input speed (crank or cam shaft) passed through. Use it as the load speed", "mechanism.speed_input"),
            new CalcOutputPort("f_in", "Hz", "Input frequency n_in/60 in Hz. Rpm is an angular velocity, so n_in cannot be linked to a frequency", "mechanism.frequency_input"),
            new CalcOutputPort("P_mean", "kW", "Mean input power over the swept range or the cam revolution", "mechanism.power_mean"),
            new CalcOutputPort("lift", "mm", "Greatest follower displacement (cam). Use it as the spring travel", "mechanism.lift"),
            new CalcOutputPort("F_spring_max", "N", "Spring force at full lift, k·lift + F_pre (cam). The follower force includes inertia and is not the spring force", "mechanism.force_spring_max"),
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
            new("mode", "Kind of mechanism", CalcInputKind.Choice, Choices: ModeChoices, DefaultChoice: FourBar, Group: "Setup"),
            new("circuit", "Assembly circuit (four-bar, slider-crank): open is θ₄ = φ − γ (slider on the +x side of the crank pin), crossed the mirror branch", CalcInputKind.Choice, Choices: [Open, Crossed], DefaultChoice: Open, IsOptional: true, Group: "Setup"),
            new("L1", "Ground link d, O₂ to O₄ (four-bar)", CalcInputKind.Quantity, "mm", Default: 100, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new("L2", "Crank length a (four-bar) or r (slider-crank)", CalcInputKind.Quantity, "mm", Default: 30, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new("L3", "Coupler length b (four-bar) or connecting-rod length l (slider-crank)", CalcInputKind.Quantity, "mm", Default: 90, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new("L4", "Rocker length c (four-bar)", CalcInputKind.Quantity, "mm", Default: 70, Min: 1, Max: 10000, HardMin: 0.01, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new("e_off", "Offset e of the slide line above the crank centre, or of the cam follower axis at +x: positive e reduces the pressure angle where ds/dθ exceeds e (optional; blank = 0)", CalcInputKind.Quantity, "mm", Default: 0, Min: -1000, Max: 1000, HardMin: -1e5, HardMax: 1e5, IsOptional: true, Group: "Geometry"),
            new("steps", "Intervals N of the table (N + 1 rows, a whole number, at most 3600; the peaks do not depend on it)", CalcInputKind.Number, Default: 72, Min: 4, Max: 720, HardMin: 2, HardMax: 3600, Group: "Sweep"),
            new("n_in", "Input speed (constant)", CalcInputKind.Quantity, "rpm", Default: 120, Min: 0.1, Max: 30000, HardMin: 1e-3, HardMax: 1e6, Group: "Sweep"),
            new("theta_start", "Input angle at the first row of the table", CalcInputKind.Quantity, "deg", Default: 0, Min: -720, Max: 720, HardMin: -7200, HardMax: 7200, IsOptional: true, Group: "Sweep"),
            new("theta_range", "Swept range of the input angle (360° for a full turn)", CalcInputKind.Quantity, "deg", Default: 360, Min: 1, Max: 720, HardMin: 0.1, HardMax: 3600, IsOptional: true, Group: "Sweep"),
            new("load_dir", "External load: constant direction, or always opposing the output motion", CalcInputKind.Choice, Choices: [Constant, Opposes], DefaultChoice: Opposes, IsOptional: true, Group: "Loads"),
            new("T_load", "External load torque on the output link (four-bar). Positive acts in the −θ₄ direction, resisting positive ω₄; with 'Opposes motion' its sign is applied against the actual direction of ω₄ (a negative value then assists)", CalcInputKind.Quantity, "N*m", Default: 1.5, Min: -10000, Max: 10000, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("J_out", "Moment of inertia of the output link about its pivot (four-bar)", CalcInputKind.Quantity, "kg*m^2", Default: 0.002, Min: 0, Max: 1000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("m_slider", "Slider mass (slider-crank)", CalcInputKind.Quantity, "kg", Default: 0.5, Min: 0, Max: 10000, HardMin: 0, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("F_load", "External load on the slider (slider-crank). Positive acts in the −x direction, resisting +x motion; with 'Opposes motion' it is applied against the actual direction of motion", CalcInputKind.Quantity, "N", Default: 200, Min: -1e6, Max: 1e6, HardMin: -1e9, HardMax: 1e9, IsOptional: true, Group: "Loads"),
            new("mu_req", "Minimum transmission angle required (linkages)", CalcInputKind.Quantity, "deg", Default: 40, Min: 10, Max: 80, HardMin: 0, HardMax: 90, IsOptional: true, Group: "Requirements"),
        };
        AddCamInputs(list);
        return list;
    }

    private static IReadOnlyList<string> ModeChoices => [FourBar, SliderCrank, .. CamModes];

    private static IReadOnlyList<CalcApplicabilityRule> BuildRules()
    {
        var rules = new List<CalcApplicabilityRule>
        {
            new("missing", "An input the chosen mechanism needs is blank.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new("geometry", "The mechanism cannot be assembled at any angle of the swept range, or the links are not a valid mechanism.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new("reach", "Some rows of the sweep cannot be assembled (the crank cannot turn fully) or sit at a toggle with no defined velocity: they are left out of the table.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new("limit", "The requested sweep reaches an assembly limit (a toggle, or the rod perpendicular to the slide line) or passes a change point: the sweep is cut where the mechanism cannot be assembled, and when the limit is inside it the speed, acceleration, torque and force peaks, which are unbounded at constant input speed, and the ports are not reported.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new("closure", "The loop-closure residual reached 1e-9 mm.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new("grashof", "The linkage is not a Grashof crank (the input link cannot turn through 360°) but the swept range is 360° or more.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        };

        AddCamRules(rules);
        return rules;
    }

    // ------------------------------------------------------------------
    // Run
    // ------------------------------------------------------------------

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_mu", "u_phi", "u_rho");
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
        c.Output("n_in", "Input (crank) speed, passed through");
        Scalar(c, "f_in", "n_in / 60", "rpm to Hz", omega / (2 * Math.PI), "Hz", "Input (crank) frequency, for a spring surge check");
        var steps = (int)Math.Round(c.InputSi("steps"));
        var start = c.InputSi("theta_start");
        var range = c.InputSi("theta_range");
        var crossed = c.Has("circuit") && c.InputText("circuit") == Crossed;
        var muReq = c.Has("mu_req") ? c.InputSi("mu_req") : 40 * Math.PI / 180;

        if (c.Has("mu_req"))
            c.Input("mu_req");
        else
            c.Assign("mu_req", "40 deg", "deg", "Required transmission angle (the default, as none was entered)");

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

            c.Block("missing", $"{symbol}: enter '{c.Template.FindInput(symbol)?.Description ?? symbol}'. The {c.InputText("mode").ToLowerInvariant()} calculation needs it.");
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
        c.Output(symbol, description, noiseProne: true);
    }

    private static void Scalar(CalcRunContext c, string symbol, string formula, string substitution, double value, string unit, string description, string? clause = null)
    {
        c.Record(symbol, formula, substitution, unit.Length == 0 ? new ScalarValue(value) : ScalarValue.Of(value, unit), description, clause);
        c.Output(symbol, description, noiseProne: true);
    }

    /// <summary>
    /// The two self-consistency residuals: the loop-closure residual (mm) with its floor from the size of the linkage, and the velocity-loop residual (relative)
    /// with the relative floor. Below its floor a residual is round-off and is shown as "below" the floor, in its row, its working and its check line (C2-20).
    /// </summary>
    private static void Residuals(CalcRunContext c, int tableRows, int scanAngles, double resMaxMetres, double velRes, double sizeMetres, string clause)
    {
        Scalar(c, "res_max", "max |loop residual|", $"{tableRows} table rows and {scanAngles} scan angles", Mm(resMaxMetres), "mm", "Largest loop-closure residual", clause);
        c.MarkResidual("res_max", ClosureFloorMm(sizeMetres));
        Scalar(c, "res_vel_max", "max |velocity loop| / its terms", $"{tableRows} table rows", velRes, "", "Largest velocity-loop residual, relative to the size of its terms", clause);
        c.MarkResidual("res_vel_max", CalcDisplay.ResidualFloor);
    }

    /// <summary>
    /// The noise floor of the loop-closure residual in mm: <see cref="CalcDisplay.ResidualFloor"/> of the longest link, not below 1e-12 mm and at most a tenth of
    /// <see cref="ClosureLimitMm"/>, so a residual shown below it always reads as passing its check. A product and a clamp only, so every platform gets the same floor.
    /// </summary>
    private static double ClosureFloorMm(double sizeMetres) => Math.Clamp(CalcDisplay.ResidualFloor * Mm(sizeMetres), 1e-12, ClosureLimitMm / 10);

    private static void VelocityCheck(CalcRunContext c, double velRes) =>
        c.CheckCondition("Velocity-loop residual against its limit", velRes < 1e-8, CalcDisplay.ResidualText("max residual", velRes, CalcDisplay.ResidualFloor), "< 1e-8", "res_vel_max", "Self-consistency");

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

    private const string MuCheck = "Required transmission angle against the smallest transmission angle";

    private static void UtilisationMu(CalcRunContext c, double muReq, double muMin)
    {
        // At a toggle (μ_min = 0) a positive requirement has no finite ratio: the ratio is withheld and the check fails on its own terms,
        // as a condition, never as a stand-in number (Yellow Y-B-03). A zero requirement is met at any angle: u = 0.
        if (muMin <= 1e-9 && muReq > 0)
        {
            c.Output("u_mu", "Required against smallest transmission angle", new TextValue($"— (μ_min = {F(Deg(muMin))}°: the sweep reaches a toggle, so there is no ratio)"));
            c.CheckCondition(MuCheck, false, $"μ_min = {F(Deg(muMin))}° (toggle in the sweep)", $"μ_req = {F(Deg(muReq))}°", null, "Transmission angle");
            return;
        }

        var u = muMin > 1e-9 ? muReq / muMin : 0;
        c.Record("u_mu", "μ_req / μ_min", $"{F(Deg(muReq))}° / {F(Deg(muMin))}°", new ScalarValue(u), "Transmission angle: required minimum against the smallest over the sweep", "Transmission angle");

        if (muMin > 1e-9)
        {
            c.Output("u_mu", "Required against smallest transmission angle");
            c.Check(MuCheck, "mu_req", "mu_min", "Transmission angle", resultSymbol: "u_mu");
        }
        else
        {
            c.Output("u_mu", "Required against smallest transmission angle", kind: CalcOutputKind.Utilisation);
            c.CheckCondition(MuCheck, true, $"μ_req = {F(Deg(muReq))}°", "no minimum required", null, "Transmission angle");
        }
    }

    /// <summary>The mean-power, RMS-torque and speed outputs the motor sizing needs (over the swept range).</summary>
    private static void DriveOutputs(CalcRunContext c, IReadOnlyList<double> torque, double omega, string span, double peakTorque)
    {
        var mean = SimpsonMean(torque);
        var rms = Math.Sqrt(SimpsonMean([.. torque.Select(t => t * t)]));
        Scalar(c, "T_rms_in", "√(mean T_in²)", span, rms, "N*m", "RMS input torque over the swept range", "Virtual work");
        PeakPower(c, omega, peakTorque);
        Scalar(c, "P_mean", "ω·mean(T_in)", span, omega * mean / 1e3, "kW", "Mean input power over the swept range (negative: the mechanism returns energy)", "Virtual work");
    }

    /// <summary>
    /// The peak input power ω·max |T_in| (working only): the scale of the power in the run, against which a mean power that is zero by the cycle (a full turn
    /// with conservative loads) is recognised as round-off and shown as 0 (C2-20).
    /// </summary>
    private static void PeakPower(CalcRunContext c, double omega, double peakTorque) =>
        c.Record("P_in_pk", "ω·max |T_in|", $"{F(omega)} rad/s × {F(peakTorque)} N·m", ScalarValue.Of(omega * peakTorque / 1e3, "kW"), "Peak input power (the scale of the mean power)", "Virtual work");

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
            c.Warn("grashof", "L2: the linkage is not a Grashof crank, so the input link cannot turn through 360°. A sweep of 360° or more is cut where the linkage cannot be assembled.");

        var link = new FourBarLinkage(d, a, b, cc, crossed);
        var changePoint = FourBarLinkage.IsChangePoint(d, a, b, cc);
        var limits = link.ToggleCrankAngles();
        var reqEnd = req.Start + req.Range;
        var arc = MechanismSweep.Clip(req.Start, reqEnd, limits, t => link.At(t, w2) is not null);

        if (!arc.Found)
        {
            c.Block("geometry", $"L1: the four links cannot be assembled at any crank angle from {SweepText(req.Start, reqEnd)}. Check that |b − c| ≤ ℓ ≤ b + c holds, with ℓ = |A − O₄|.");
            return;
        }

        var sw = new Sweep(arc.Lo, arc.Hi - arc.Lo, req.Steps, req.Omega);
        var unbounded = arc.LimitsInside.Count > 0;

        // Only the true change points flip the branch: the toggle angles at which all four links are collinear (θ₂ = 0 or π). The other
        // toggles are assembly limits. The circuit names a branch at a fixed reference angle (the middle of the first interval between
        // change points in 0 … 2π, π/2 when there are none), so the label at a crank angle does not depend on the sweep or on the turn
        // it is in: the parity at the start of the sweep is that of the start angle wrapped into [0, 2π) against the reference, and the
        // branch is followed by continuity (a flip at every change point passed) only inside the sweep.
        var changePoints = changePoint ? limits.Where(x => Math.Abs(Math.Sin(x)) < 1e-9).ToList() : [];
        var reference = Math.PI / 2;

        if (changePoints.Count > 0)
        {
            var points = new List<double> { 0 };
            points.AddRange(changePoints.Where(x => x > 1e-9 && x < 2 * Math.PI - 1e-9));
            points.Add(2 * Math.PI);
            reference = 0.5 * (points[0] + points[1]);
        }

        var startWrapped = Wrap360(arc.Lo);
        var startParity = changePoints.Count == 0 ? 0 : changePoints.Count(x => x > Math.Min(reference, startWrapped) + 1e-9 && x < Math.Max(reference, startWrapped) - 1e-9);

        int Flips(double t)
        {
            if (changePoints.Count == 0)
                return 0;

            var lo = Math.Min(arc.Lo, t);
            var hi = Math.Max(arc.Lo, t);
            return startParity + MechanismSweep.Unwrap(changePoints, lo, hi).Count(x => x > lo + 1e-9 && x < hi - 1e-9);
        }

        if (changePoints.Count % 2 == 1 && req.Range > 2 * Math.PI + 1e-9)
            c.Warn("limit", "theta_range: this linkage has one change point per turn, so its motion repeats every 720°, not 360°. A sweep of more than one turn does not return to the same configuration after 360°.");

        FourBarState? St(double t) => link.At(t, w2, Flips(t));

        if (arc.Clipped)
            c.Warn("limit", $"theta_range: the sweep {SweepText(req.Start, reqEnd)} is cut to {SweepText(arc.Lo, arc.Hi)}, where the linkage can be assembled. The toggle angles are θ₂ = {AnglesText(limits)}.");

        if (unbounded)
        {
            c.Warn("limit", arc.LimitsInside.Any(x => changePoints.Any(y => Math.Abs(Math.IEEERemainder(x - y, 2 * Math.PI)) < 1e-9))
                ? $"theta_range: the sweep passes the change point at θ₂ = {AnglesText(arc.LimitsInside)}, where the velocity system is singular. The peaks, torque and forces are not reported, so set theta_start and theta_range between the change-point angles."
                : $"theta_range: the sweep reaches the toggle at θ₂ = {AnglesText(arc.LimitsInside)}, where coupler and rocker are collinear. The peaks, torque and forces are not reported, so set theta_start and theta_range between the toggle angles.");
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
            c.Block("geometry", $"L1: every row of the sweep {SweepText(arc.Lo, arc.Hi)} is a toggle or cannot be assembled. Change theta_start and theta_range away from the toggle angles θ₂ = {AnglesText(limits)}.");
            return;
        }

        if ((unreachable > 0 || singular > 0) && !unbounded)
            c.Warn("reach", $"L2: {unreachable} of {sw.Steps + 1} table rows cannot be assembled and {singular} sit at a toggle. The table leaves them out.");

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
            c.Warn("limit", "theta_range: a toggle lies inside the sweep, so the peaks, torque, forces and ports are not reported. Move theta_start and theta_range away from it.");
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
        c.Record("alpha_ref", "ω₂²", $"({F(w2)} rad/s)²", ScalarValue.Of(w2 * w2, "rad/s^2"), "Angular-acceleration scale of the input (an α below 1e-9 of it is round-off and is shown as 0)", clause);
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
        var velRes = rows.Where(r => Math.Abs(Math.Sin(r.Theta3 - r.Theta4)) > 1e-5).Select(r => r.VelocityResidual).DefaultIfEmpty(0).Max();
        Residuals(c, rows.Count, n + 1, resMax, velRes, Math.Max(Math.Max(d, a), Math.Max(b, cc)), clause);

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
                c.Block("geometry", "L1: every assembled row of the sweep is a toggle, so no row has a defined velocity. Change the swept range or the link lengths.");
                return;
            }

            Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", omega3Pk.Value.Value, "rad/s", "Peak coupler angular velocity", clause);
            Scalar(c, "omega_out_pk", "max |ω₄|", "dense scan, refined", omega4Pk.Value.Value, "rad/s", "Peak output (rocker) angular velocity", clause);
            Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", alpha3Pk.Value.Value, "rad/s^2", "Peak coupler angular acceleration", clause);
            Scalar(c, "alpha_out_pk", "max |α₄|", "dense scan, refined", alpha4Pk.Value.Value, "rad/s^2", "Peak output (rocker) angular acceleration", clause);

            // The ports.
            Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tInPk.Value.Value, "N*m", "Peak input (crank) torque by virtual work", "Virtual work");
            Scalar(c, "output_motion", "c · max |ω₄|", "output pin at the rocker length", cc * omega4Pk.Value.Value, "m/s", "Peak speed of the output pin at the rocker length c (a kinematic proxy for a cylinder only if the cylinder acts perpendicular to the rocker at that radius; the angular velocity is omega_out_pk)", clause);
            Scalar(c, "peak_accel", "max |a_pin|", "√(α₄² + ω₄⁴)·c", aPinPk.Value.Value, "m/s^2", "Peak acceleration of the output pin", clause);
            Scalar(c, "follower_force", "max |(T_L + J·α₄)/c|", "tangential force at the output pin", fPk.Value.Value, "N", "Peak force perpendicular to the rocker at the output pin, the drive force of the output", "Virtual work");
            Scalar(c, "F_pin_pk", "max |(T_L + J·α₄)/(c·sin μ)|", "force in the coupler = the crank-pin force", couplerPk.Value.Value, "N", "Peak force in the coupler, which is also the force at the crank pin and the pin loads on the bearings", "Virtual work");

            var dense = states.Select(s => TIn(s!.Value)).ToList();
            DriveOutputs(c, dense, w2, "Simpson over the scan", tInPk.Value.Value);
        }

        UtilisationMu(c, muReq, muMin);
        ClosureCheck(c, resMax, Math.Max(Math.Max(d, a), Math.Max(b, cc)));
        VelocityCheck(c, velRes);
    }

    private static double WrapPi(double angle)
    {
        var w = (angle + Math.PI) % (2 * Math.PI);
        return (w < 0 ? w + 2 * Math.PI : w) - Math.PI;
    }

    private static void ClosureCheck(CalcRunContext c, double residualMetres, double sizeMetres)
    {
        var mm = Mm(residualMetres);
        var ok = mm < ClosureLimitMm;
        c.CheckCondition("Loop-closure residual against its limit", ok, CalcDisplay.ResidualText("max residual", mm, ClosureFloorMm(sizeMetres), "mm"), $"< {CalcValue.FormatNumber(ClosureLimitMm)} mm", "res_max", "Self-consistency");

        if (!ok)
            c.Warn("closure", $"L3: the loop-closure residual {CalcValue.FormatNumber(mm)} mm reached 1e-9 mm. The geometry is near-degenerate, so the position is no longer exact.");
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
            c.Block("geometry", $"L3: the rod cannot reach the slide line at any crank angle from {SweepText(req.Start, reqEnd)}. Check that |r·sinθ − e| ≤ l holds.");
            return;
        }

        var sw = new Sweep(arc.Lo, arc.Hi - arc.Lo, req.Steps, req.Omega);
        var unbounded = arc.LimitsInside.Count > 0;

        if (arc.Clipped)
            c.Warn("limit", $"theta_range: the sweep {SweepText(req.Start, reqEnd)} is cut to {SweepText(arc.Lo, arc.Hi)}, where the rod can reach the slide line. The limit angles are θ = {AnglesText(limits)}.");

        if (unbounded)
            c.Warn("limit", $"theta_range: the sweep reaches a limit at θ = {AnglesText(arc.LimitsInside)}, so the peaks, torque, forces and ports are not reported. Set theta_start and theta_range between the limit angles.");

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
            c.Block("geometry", $"L3: no row of the sweep {SweepText(arc.Lo, arc.Hi)} can be assembled. Change theta_start and theta_range.");
            return;
        }

        if (unreachable > 0 && !unbounded)
            c.Warn("reach", $"L3: {unreachable} of {sw.Steps + 1} table rows cannot be assembled, because the rod cannot reach the slide line. The table leaves them out.");

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
            c.Warn("limit", "theta_range: part of the sweep cannot be assembled, so the peaks, torque, forces and ports are not reported. Set theta_start and theta_range inside the assemblable stretch.");
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
            c.Block("geometry", "L3: the sweep has no assembled row. Change the swept range or the link lengths.");
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
        var velRes = rows.Where(row => row.RodCos > 1e-5).Select(row => row.VelocityResidual).DefaultIfEmpty(0).Max();
        Residuals(c, rows.Count, n + 1, resMax, velRes, Math.Max(Math.Max(r, l), Math.Abs(e)), clause);

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
                c.Block("geometry", "L3: the sweep has no assembled row. Change the swept range or the link lengths.");
                return;
            }

            Scalar(c, "omega3_pk", "max |ω₃|", "dense scan, refined", w3Pk.Value.Value, "rad/s", "Peak connecting-rod angular velocity", clause);
            Scalar(c, "alpha3_pk", "max |α₃|", "dense scan, refined", al3Pk.Value.Value, "rad/s^2", "Peak connecting-rod angular acceleration", clause);
            Scalar(c, "input_torque", "max |T_in|", "dense scan, refined", tPk.Value.Value, "N*m", "Peak input (crank) torque by virtual work", "Virtual work");
            Scalar(c, "output_motion", "max |v|", "dense scan, refined", vPk.Value.Value, "m/s", "Peak slider velocity (for a cylinder-driven slider at constant crank speed, a kinematic proxy)", clause);
            Scalar(c, "peak_accel", "max |a|", "dense scan, refined", aPk.Value.Value, "m/s^2", "Peak slider acceleration", clause);
            Scalar(c, "follower_force", "max |m·a + F_L|", "slide-direction force on the slider", fPk.Value.Value, "N", "Peak force on the slider in the slide direction, the rod's component along the slide line", "Virtual work");
            Scalar(c, "F_pin_pk", "max |m·a + F_L|/cosθ₃", "force along the connecting rod", rodPk.Value.Value, "N", "Peak force in the connecting rod, also the crank-pin load", "Virtual work");
            Scalar(c, "F_guide_pk", "max |m·a + F_L|·tanθ₃", "reaction of the slide on the slider", guidePk.Value.Value, "N", "Peak side reaction of the guide on the slider (frictionless)", "Virtual work");

            var dense = states.Select(s => TIn(s!.Value)).ToList();
            DriveOutputs(c, dense, w, "Simpson over the scan", tPk.Value.Value);
        }

        UtilisationMu(c, muReq, muMin);
        ClosureCheck(c, resMax, Math.Max(Math.Max(r, l), Math.Abs(e)));
        VelocityCheck(c, velRes);
    }
}
