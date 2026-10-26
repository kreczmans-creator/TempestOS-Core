using System.Globalization;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E8 — cable containment: the fill of a conduit, trunking or tray by cable cross-section against a space factor the
/// user supplies, the bend radius against a multiple of the largest cable's diameter the user supplies, and the pull of
/// the cables through a route of straight lengths and bends in both directions (straight T₂ = T₁ + w·g·w_c·μ·L, bend
/// T₂ = T₁·e^(w_c·μ·θ), sidewall pressure T₂/R), with the direction that passes every check recommended (the lower maximum tension when both or neither do). No cable or conduit
/// factor table and no space-factor value ships: the factors, the friction coefficient and the pulling limits are
/// records the user enters from their own references and the cable maker's data.
/// </summary>
public sealed class ContainmentCalculator : ICalcCalculator
{
    public const string Id = "elec.containment";

    public const string Conduit = "Conduit (round)";
    public const string Trunking = "Trunking or tray (rectangular)";
    public const string Straight = "Straight";
    public const string Bend = "Bend";

    public const string BendWeighted = "Horizontal bend, cable on the containment floor (weight term)";
    public const string BendCapstan = "Capstan (no weight term)";

    /// <summary>Standard gravity, m/s² (derived constant), for the weight per metre of the bundle.</summary>
    public const double Gravity = 9.80665;

    /// <summary>A tension above this (N) is refused: it is past any cable and a sign of a mistyped route.</summary>
    public const double TensionCeiling = 1e12;

    private static readonly IReadOnlyList<CalcColumnSpec> CableColumns =
    [
        new("name", "Cable", CalcInputKind.Text, IsOptional: true),
        new("od", "Overall diameter", CalcInputKind.Quantity, "mm", HardMin: 0.1, HardMax: 300),
        new("count", "Number of cables", CalcInputKind.Number, HardMin: 1, HardMax: 1000),
        new("mass", "Mass per metre, each", CalcInputKind.Quantity, "kg/m", HardMin: 0, HardMax: 1000),
        new("T_max", "Maximum pulling tension, each", CalcInputKind.Quantity, "N", HardMin: 1, HardMax: 1e7, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> RouteColumns =
    [
        new("name", "Segment", CalcInputKind.Text, IsOptional: true),
        new("kind", "Straight length or bend", CalcInputKind.Choice, Choices: [Straight, Bend]),
        new("L", "Length (straight)", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 5000, IsOptional: true),
        new("theta", "Angle (bend)", CalcInputKind.Quantity, "deg", HardMin: 0, HardMax: 720, IsOptional: true),
        new("R", "Radius (bend)", CalcInputKind.Quantity, "mm", HardMin: 0, HardMax: 100000, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Electrical, "Containment fill, bend radius and cable pulling",
        "The fill of a conduit, trunking or tray by the cables in it against the space factor you enter, the bend radius of the route against the multiple of the largest cable diameter you enter, and the pull of the cables through a route of straight lengths and bends in both directions: the tension along the route, the sidewall pressure at each bend, and the pull direction that passes every check (the lower maximum tension when both do or neither does). Space factor, bend multiple, friction coefficient and pulling limits are your own records; no cable or conduit factor method is offered.",
        "Cable area: A_cab = Σ n_i·π·d_i²/4 (n_i cables of overall diameter d_i)\n" +
        "Containment area: A_int = π·D²/4 (conduit, internal diameter D) or W·H (trunking or tray, internal width × depth)\n" +
        "Containment fill: fill = A_cab/A_int; utilisation u_fill = fill/f_max, f_max the space factor from your record\n" +
        "Bend radius: R_min = n_bend·d_max, d_max the largest cable diameter, n_bend from your record; each bend u_b = R_min/R\n" +
        "Cable weight: w = Σ n_i·m_i (kg/m); force per metre w·g·w_c, g = 9.80665 m/s², w_c your weight-correction factor (1 = full weight)\n" +
        "Straight length: T₂ = T₁ + w·g·w_c·μ·L\n" +
        "Tension round a bend in the horizontal plane, the cable resting on the containment floor (the default): T₂ = T₁·cosh(μ_e·θ) + √(T₁² + (w·g·w_c·R)²)·sinh(μ_e·θ), μ_e = w_c·μ, θ in radians, R the bend radius: the cable's weight bears on the floor round the bend as well as the tension, so a heavy cable on a large radius gains tension even from T₁ = 0\n" +
        "Tension round a bend, capstan option (no weight term): T₂ = T₁·e^(w_c·μ·θ), the equation for a weightless pull; it is T₂ = T₁·e^(μ_e·θ) and equals the weighted form when w·g·w_c·R is small against T₁\n" +
        "Sidewall pressure at a bend: SWP = T₂/R (N/m)\n" +
        "Pulling direction: the route is run in the listed order and in reverse; the direction that passes every tension and sidewall pressure check is recommended, and when both or neither pass, the one of lower maximum tension (equal tension: lower SWP)\n" +
        "Cable limit: load shared equally, so each cable carries T/N (N cables); T_cab = k_n·N·min(T_max,i), k_n your derating for more than 3 cables (1 if none); u_T = max_i (T/N)/(k_n·T_max,i) = T/T_cab\n" +
        "Grip limit: u_grip = T/T_grip, T_grip the limit of the grip or pulling eye (your record); sidewall pressure u_P = SWP_max/P_lim",
        [
            new CalcInputSpec("cables", "Cables in the containment and pulled together", CalcInputKind.Table, Columns: CableColumns, MaxRows: 40, Group: "Cables"),
            new CalcInputSpec("cont_type", "Containment", CalcInputKind.Choice, Choices: [Conduit, Trunking], DefaultChoice: Conduit, Group: "Containment"),
            new CalcInputSpec("D_c", "Conduit internal diameter (conduit)", CalcInputKind.Quantity, "mm", Min: 10, Max: 300, HardMin: 1, HardMax: 1000, IsOptional: true, Group: "Containment"),
            new CalcInputSpec("W_c", "Trunking or tray internal width", CalcInputKind.Quantity, "mm", Min: 20, Max: 1000, HardMin: 1, HardMax: 5000, IsOptional: true, Group: "Containment"),
            new CalcInputSpec("H_c", "Trunking or tray internal depth", CalcInputKind.Quantity, "mm", Min: 20, Max: 300, HardMin: 1, HardMax: 2000, IsOptional: true, Group: "Containment"),
            new CalcInputSpec("f_max", "Space factor limit (fill limit, from your record)", CalcInputKind.Number, HardMin: 0.01, HardMax: 1, Group: "Containment"),
            new CalcInputSpec("n_bend", "Minimum bend radius as a multiple of the largest cable diameter (from your record)", CalcInputKind.Number, HardMin: 1, HardMax: 100, IsOptional: true, Group: "Bends"),
            new CalcInputSpec("route", "Pull route in pull order (straight lengths and bends)", CalcInputKind.Table, Columns: RouteColumns, MaxRows: 50, IsOptional: true, Group: "Pull"),
            new CalcInputSpec("T_1", "Tension at the start of the pull (pulling rope, drum or head)", CalcInputKind.Quantity, "N", Default: 0, Min: 0, Max: 10000, HardMin: 0, HardMax: 1e7, Group: "Pull"),
            new CalcInputSpec("bend_model", "Tension round a bend: horizontal bend with the cable's weight on the containment floor (default), or the capstan equation without the weight term", CalcInputKind.Choice, Choices: [BendWeighted, BendCapstan], DefaultChoice: BendWeighted, Group: "Pull"),
            new CalcInputSpec("mu", "Coefficient of friction, cable sheath on containment (from your record)", CalcInputKind.Number, HardMin: 0, HardMax: 2, IsOptional: true, Group: "Pull"),
            new CalcInputSpec("w_c", "Weight-correction factor on the cable weight and, by the same factor, the bend friction (blank = 1 = full weight on the containment; above 1 for cradled or triangular arrangements, from your record)", CalcInputKind.Number, Min: 1, Max: 3, HardMin: 1, HardMax: 10, IsOptional: true, Group: "Pull"),
            new CalcInputSpec("T_grip", "Grip or pulling-eye limit of the whole bundle (from the pulling equipment or your record)", CalcInputKind.Quantity, "N", HardMin: 1, HardMax: 1e8, IsOptional: true, Group: "Pull"),
            new CalcInputSpec("k_n", "Derating of the cable limits for more than 3 cables on one grip (from your record)", CalcInputKind.Number, HardMin: 0.05, HardMax: 1, IsOptional: true, Group: "Pull"),
            new CalcInputSpec("P_lim", "Sidewall pressure limit, force per metre of bend (from the cable maker)", CalcInputKind.Quantity, "N/m", HardMin: 1, HardMax: 1e7, IsOptional: true, Group: "Pull"),
        ],
        [
            new CalcAssumption("Cables are circular and counted by overall diameter; there is no packing or cable and conduit factor method (the jam-ratio warning is the only jamming check). The space factor is yours, from your own record.", "Fill by geometry; the factor is a user record."),
            new CalcAssumption("Every cable is pulled together as one bundle on one pulling head with the load shared equally, so each cable carries T/N and the weakest cable governs: the cable limit is N times the lowest maximum pulling tension, optionally derated by your factor for more than 3 cables. The grip or pulling-eye limit is a separate limit you enter.", "Equal sharing; unequal sharing is not modelled."),
            new CalcAssumption("Straight lengths are horizontal and the cable weight bears on the containment over the full length; bends are taken in the plane of the route. Inclines, vertical runs and pulley or roller reductions are not modelled; fold them into μ. A cradled or triangular weight correction is entered as w_c, which scales the weight on the straight lengths and the friction in the bends (the contact force of a cradled or triangular arrangement is w_c times that of a flat one), so T₂ = T₁ + w·g·w_c·μ·L and, at a bend, μ_e = w_c·μ in the cosh/sinh (or capstan) form. A bend in a vertical plane has the weight along the cable rather than on the floor: take the capstan option and fold the rise into the route.", "Equivalent friction w_c·μ at the bends; blank w_c means 1."),
            new CalcAssumption("The coefficient of friction is one constant for the whole route; static and running friction are not distinguished.", "A user record with its source."),
            new CalcAssumption("Sidewall pressure is the tension leaving the bend over the bend radius, for the bundle as a whole. Enter the cable maker's per-cable limit unchanged: the bundle T/R against it is conservative for a triangular arrangement and roughly equal for a cradled one. Do not multiply the limit by the number of cables.", "SWP = T₂/R."),
            new CalcAssumption("The bend radius uses the individual cable diameter; for a triplexed or bundled assembly, enter the assembly diameter from the cable maker as the overall diameter.", "Cable data sheet."),
            new CalcAssumption("The tension at the start of the pull is the same in both directions. With the horizontal-bend (weight) model a heavy cable gains tension at a bend even from T₁ = 0; with the capstan option a bend at the very start of a pull with no start tension adds nothing, so enter the pulling rope or drum tension when it matters.", "Weight term for a horizontal bend; capstan equation as the option."),
        ],
        [
            new CalcClauseRef("BS 7671:2018 (as amended)", "reg. 522.8", "Other mechanical stresses on wiring systems (damage during installation, bending radius)"),
            new CalcClauseRef("BS EN 61386-1", "general", "Conduit systems for cable management: general requirements"),
            new CalcClauseRef("Beer and Johnston, Vector Mechanics for Engineers: Statics", "Ch. 8", "Friction: belt friction (the capstan equation)"),
        ],
        [
            new CalcApplicabilityRule("cables", "Each cable row needs a whole number of cables of at least one.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("containment", "The conduit diameter, or the trunking or tray width and depth, must be entered for the containment chosen.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("route", "A straight row needs a length, a bend row an angle and a radius; the tension must stay below 1e12 N.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("mu", "A route needs the coefficient of friction.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("n_bend", "No bend-radius multiple entered: the bends are not checked against the cable.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("T_cab", "A cable has no maximum pulling tension: the cable limit is not checked.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("jam", "Three equal cables in a conduit with 1.05·D/d between 2.8 and 3.2 may jam (geometry, 5 % allowance for ovality).", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("k_n", "A derating is entered but the bundle has 3 cables or fewer: it is not applied.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("route-limits", "A route-only value (friction, grip limit, sidewall limit, derating) is entered but there is no route: it is not used.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("P_lim", "No sidewall pressure limit entered: the pressure is not checked.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-fill-3x10-in-25", "Three 10 mm cables in a 25 mm conduit: 48 % fill, and a pull through two bends in both directions (analytic)",
                new CalcReferenceSource("Analytic (geometry, capstan equation as the bend_model option; recomputed in python3)", null, "fill = 3·π·10²/4 / (π·25²/4) = 300/625 = 48 %; w = 3 × 0.2 = 0.6 kg/m, w·g·μ = 2.0593965 N/m (μ 0.35); route 12 m, 90° R 400 mm, 18 m, 90° R 300 mm, 8 m: as listed 24.712758, 42.823985, 79.893122, 138.444356, 154.919528 N; reversed 16.475172, 28.549323, 65.618460, 113.708230, 138.420988 N; SWP at the last bend 461.4812 N/m as listed, 284.2706 N/m reversed; R_min = 12 × 10 = 120 mm; T_cab = 3 × 400 = 1200 N. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Control", 10, 3, 0.2, 400)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 25, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 12), Route(RS(12), RB(90, 400), RS(18), RB(90, 300), RS(8)), CalcInputValue.Choice("bend_model", BendCapstan), CalcInputValue.Of("mu", 0.35), Q("P_lim", 400, "N/m")],
                [new CalcExpectedValue("A_cab", 235.6194490, "mm^2", 1e-9), new CalcExpectedValue("A_int", 490.8738521, "mm^2", 1e-9), new CalcExpectedValue("fill", 48, "%", 1e-9), new CalcExpectedValue("u_fill", 0.96, "", 1e-9), new CalcExpectedValue("R_min", 120, "mm", 1e-9), new CalcExpectedValue("u_bend", 0.4, "", 1e-9), new CalcExpectedValue("w", 0.6, "kg/m", 1e-9), new CalcExpectedValue("T_max_a", 154.9195277, "N", 1e-9), new CalcExpectedValue("T_max_b", 138.4209876, "N", 1e-9), new CalcExpectedValue("max_tension", 138.4209876, "N", 1e-9), new CalcExpectedValue("P_max_a", 461.4811856, "N/m", 1e-9), new CalcExpectedValue("P_max", 284.2705741, "N/m", 1e-9), new CalcExpectedValue("T_cab", 1200, "N", 1e-9), new CalcExpectedValue("u_T", 0.1153508230, "", 1e-9), new CalcExpectedValue("u_P", 0.7106764352, "", 1e-9)]),
            new CalcTestCase("ka-pull-tray-two-bends", "Mixed bundle pulled through two bends from a 100 N start tension, in a 100 × 50 mm tray (analytic)",
                new CalcReferenceSource("Analytic (capstan equation, the bend_model option; recomputed in python3)", null, "2 × 12 mm at 0.6 kg/m and 1 × 8 mm at 0.15 kg/m: w = 1.35 kg/m, w·g·μ = 5.295591 N/m (μ 0.4), T₁ = 100 N; route 20 m, 90° R 500 mm, 30 m, 90° R 600 mm, 10 m: as listed 1074.235304 N, SWP 1702.132 N/m; reversed 941.126090 N, SWP 1670.429 N/m; T_cab = 3 × min(700, 150) = 450 N (the 150 N control cable carries 941.126/3 = 313.709 N, u_T = 2.091391, FAIL in both directions); fill = (2·π·12²/4 + π·8²/4)/(100·50) = 5.529 %. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Power", 12, 2, 0.6, 700), ("Control", 8, 1, 0.15, 150)), CalcInputValue.Choice("cont_type", Trunking), Q("W_c", 100, "mm"), Q("H_c", 50, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 8), Route(RS(20), RB(90, 500), RS(30), RB(90, 600), RS(10)), Q("T_1", 100, "N"), CalcInputValue.Choice("bend_model", BendCapstan), CalcInputValue.Of("mu", 0.4), Q("P_lim", 2000, "N/m")],
                [new CalcExpectedValue("A_cab", 276.4601535, "mm^2", 1e-9), new CalcExpectedValue("fill", 5.529203071, "%", 1e-9), new CalcExpectedValue("w", 1.35, "kg/m", 1e-9), new CalcExpectedValue("T_max_a", 1074.235304, "N", 1e-9), new CalcExpectedValue("T_max_b", 941.1260901, "N", 1e-9), new CalcExpectedValue("max_tension", 941.1260901, "N", 1e-9), new CalcExpectedValue("P_max_a", 1702.132324, "N/m", 1e-9), new CalcExpectedValue("P_max_b", 1670.428540, "N/m", 1e-9), new CalcExpectedValue("T_cab", 450, "N", 1e-9), new CalcExpectedValue("u_T", 2.091391311, "", 1e-9), new CalcExpectedValue("R_min", 96, "mm", 1e-9)]),
            new CalcTestCase("ka-direction-passes-every-check", "A route whose lower-tension direction breaks the sidewall limit: the other direction is recommended (analytic)",
                new CalcReferenceSource("Analytic (capstan equation, the bend_model option; recomputed in python3)", null, "1 × 20 mm at 1.0 kg/m, T₁ = 50 N, μ 0.35, route 20 m, 90° R 1200 mm, 40 m, 180° R 600 mm, 40 m: as listed 1166.942072 N, SWP 1716.0816 N/m (fails P_lim 1500 N/m); reversed 1281.141772 N, SWP 1010.4127 N/m (passes); the reverse direction passes every check and is recommended although its tension is higher; T_cab = 5000 N. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Single", 20, 1, 1.0, 5000)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 100, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 6), Route(RS(20), RB(90, 1200), RS(40), RB(180, 600), RS(40)), Q("T_1", 50, "N"), CalcInputValue.Choice("bend_model", BendCapstan), CalcInputValue.Of("mu", 0.35), Q("P_lim", 1500, "N/m")],
                [new CalcExpectedValue("T_max_a", 1166.942072, "N", 1e-9), new CalcExpectedValue("T_max_b", 1281.141772, "N", 1e-9), new CalcExpectedValue("max_tension", 1281.141772, "N", 1e-9), new CalcExpectedValue("P_max_a", 1716.081620, "N/m", 1e-9), new CalcExpectedValue("P_max", 1010.412685, "N/m", 1e-9), new CalcExpectedValue("T_cab", 5000, "N", 1e-9), new CalcExpectedValue("u_T", 0.2562283544, "", 1e-9), new CalcExpectedValue("u_P", 0.6736084567, "", 1e-9)]),
            new CalcTestCase("ka-capstan-180", "A single 180° bend with a 200 N start tension: the capstan equation (analytic)",
                new CalcReferenceSource("Analytic (T₂ = T₁·e^(μθ), the bend_model capstan option; recomputed in python3)", null, "T₂ = 200·e^(0.35·π) = 600.567352 N; SWP = 600.567352/0.4 = 1501.418380 N/m; the same in both directions. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Single", 20, 1, 0.3, 1000)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 50, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 6), Route(RB(180, 400)), Q("T_1", 200, "N"), CalcInputValue.Choice("bend_model", BendCapstan), CalcInputValue.Of("mu", 0.35)],
                [new CalcExpectedValue("T_max_a", 600.5673521, "N", 1e-9), new CalcExpectedValue("T_max_b", 600.5673521, "N", 1e-9), new CalcExpectedValue("P_max", 1501.418380, "N/m", 1e-9), new CalcExpectedValue("u_bend", 0.3, "", 1e-9)]),
            new CalcTestCase("ka-heavy-cable-horizontal-bend", "A heavy cable round a large horizontal bend: the weight term adds 64 % to the capstan tension (analytic)",
                new CalcReferenceSource("Analytic (horizontal bend with the cable's weight on the containment floor, recomputed in python3)", null, "One 20 mm cable at 10 kg/m, μ 0.3, T₁ = 100 N, one 90° bend R 3 m: w·g = 98.0665 N/m, w·g·R = 294.1995 N, μθ = 0.471239; T₂ = 100·cosh(0.471239) + √(100² + 294.1995²)·sinh(0.471239) = 263.218479 N; SWP = 263.218479/3 = 87.739493 N/m; the capstan equation gives 100·e^(0.471239) = 160.197765 N. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Heavy", 20, 1, 10, 5000)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 100, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 6), Route(RB(90, 3000)), Q("T_1", 100, "N"), CalcInputValue.Of("mu", 0.3)],
                [new CalcExpectedValue("T_max_a", 263.2184789, "N", 1e-9), new CalcExpectedValue("T_max_b", 263.2184789, "N", 1e-9), new CalcExpectedValue("P_max", 87.73949298, "N/m", 1e-9), new CalcExpectedValue("u_bend", 0.04, "", 1e-9)]),
            new CalcTestCase("ka-heavy-cable-bend-from-rest", "The same bend with no start tension: the weight term still pulls tension in (analytic)",
                new CalcReferenceSource("Analytic (horizontal bend with the cable's weight on the containment floor, recomputed in python3)", null, "As ka-heavy-cable-horizontal-bend with T₁ = 0: T₂ = √(0 + 294.1995²)·sinh(0.471239) = 143.826665 N; the capstan equation gives 0. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Heavy", 20, 1, 10, 5000)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 100, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 6), Route(RB(90, 3000)), Q("T_1", 0, "N"), CalcInputValue.Of("mu", 0.3)],
                [new CalcExpectedValue("T_max_a", 143.8266655, "N", 1e-9)]),
            new CalcTestCase("ka-trunking-fill", "Twelve 8 mm cables in 100 × 50 mm trunking (analytic)",
                new CalcReferenceSource("Analytic (geometry, recomputed in python3)", null, "fill = 12·π·8²/4 / (100 × 50) = 603.1858 mm² / 5000 mm² = 12.0637 %; u_fill = 0.120637/0.42 = 0.287231. Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Data", 8, 12, 0.07, 100)), CalcInputValue.Choice("cont_type", Trunking), Q("W_c", 100, "mm"), Q("H_c", 50, "mm"), CalcInputValue.Of("f_max", 0.42)],
                [new CalcExpectedValue("A_cab", 603.1857895, "mm^2", 1e-9), new CalcExpectedValue("A_int", 5000, "mm^2", 1e-9), new CalcExpectedValue("fill", 12.06371579, "%", 1e-9), new CalcExpectedValue("u_fill", 0.2872313283, "", 1e-9)]),
            new CalcTestCase("ka-bend-too-tight", "A 100 mm bend for 10 mm cables with a multiple of 12: too tight (analytic)",
                new CalcReferenceSource("Analytic (R_min = n·d_max)", null, "R_min = 12 × 10 = 120 mm; u_b = 120/100 = 1.2 (FAIL). Record values (space factor, bend multiple, friction, limits) are illustrative, not from any standard."),
                [Cables(("Control", 10, 3, 0.2, 400)), CalcInputValue.Choice("cont_type", Conduit), Q("D_c", 32, "mm"), CalcInputValue.Of("f_max", 0.5), CalcInputValue.Of("n_bend", 12), Route(RS(5), RB(90, 100)), CalcInputValue.Of("mu", 0.3)],
                [new CalcExpectedValue("R_min", 120, "mm", 1e-9), new CalcExpectedValue("u_bend", 1.2, "", 1e-9)]),
        ],
        Category: "Power",
        OutputPorts:
        [
            new CalcOutputPort("fill", "%", "Cable fill of the containment"),
            new CalcOutputPort("max_tension", "N", "Maximum pulling tension in the recommended direction"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("containment.space-factor", "Containment space factor", ["f_max"], "The fill limit for this containment and cable arrangement, from your own copy of the installation standard or your company rule, with its source and edition. No factor table or value ships and there is no default."),
            new CalcRecordKind("cable.bend-radius", "Minimum bend radius multiple", ["n_bend"], "The minimum bend radius as a multiple of the cable's overall diameter, from the cable maker's data sheet (use the largest multiple among the cables)."),
            new CalcRecordKind("containment.friction", "Pulling friction coefficient", ["mu"], "The coefficient of friction of the cable sheath on the containment, with or without lubricant, from the cable or lubricant maker or your own pulling records."),
            new CalcRecordKind("cable.pulling-limit", "Pulling tension, grip and sidewall pressure limits", ["T_grip", "P_lim", "k_n"], "The grip or pulling-eye limit of the whole bundle, the maximum sidewall pressure per cable from the cable maker's installation data, and the derating of the cable limits when more than 3 cables are on one grip, from the cable maker or your pulling procedure. Each cable's own maximum pulling tension goes in the cable table. No value ships."),
            new CalcRecordKind("containment.weight-correction", "Cable weight-correction factor", ["w_c"], "The factor on the cable weight bearing on the containment for a cradled or triangular arrangement of three or more cables, from your reference; 1 for full weight on the containment."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    /// <summary>A cable table from (name, OD mm, count, mass kg/m, maximum pulling tension N) rows.</summary>
    public static CalcInputValue Cables(params (string Name, double Od, double Count, double Mass, double Tmax)[] rows) =>
        CalcInputValue.Table("cables", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("od", new CalcCell(r.Od, "mm")), ("count", new CalcCell(r.Count)), ("mass", new CalcCell(r.Mass, "kg/m")), ("T_max", new CalcCell(r.Tmax, "N")))).ToList());

    /// <summary>A straight route segment of a length in metres.</summary>
    public static CalcRow RS(double length) => CalcRow.Of(("kind", new CalcCell(Text: Straight)), ("L", new CalcCell(length, "m")));

    /// <summary>A bend of an angle in degrees and a radius in millimetres.</summary>
    public static CalcRow RB(double degrees, double radius) => CalcRow.Of(("kind", new CalcCell(Text: Bend)), ("theta", new CalcCell(degrees, "deg")), ("R", new CalcCell(radius, "mm")));

    /// <summary>A route table from segments in pull order.</summary>
    public static CalcInputValue Route(params CalcRow[] rows) => CalcInputValue.Table("route", rows);

    private sealed record Cable(string Name, double Od, double Count, double Mass, double? Tmax);

    private sealed record Segment(int Index, string Name, bool IsBend, double Length, double Angle, double Radius);

    private sealed record Leg(Segment Segment, double T, double? Swp);

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_P", "u_T", "u_bend", "u_fill", "u_grip");
        var mm = UnitCatalog.Parse("mm");
        var kgPerM = UnitCatalog.Parse("kg/m");
        var newton = UnitCatalog.Parse("N");
        var mm2 = UnitCatalog.Parse("mm^2");
        var perM = UnitCatalog.Parse("N/m");

        // Cables: area, largest diameter, weight, tension limit.
        var tableRows = c.Rows("cables");

        if (tableRows.Count == 0)
        {
            c.Block("cables", c.EnterRows("cables"));
            return;
        }

        var cables = new List<Cable>();

        for (var i = 0; i < tableRows.Count; i++)
        {
            var row = tableRows[i];
            var count = row.Get("count")?.Number ?? 0;

            if (!(count >= 1) || count != Math.Round(count))
            {
                c.Block("cables", $"cables[{i + 1}].count: enter a whole number of cables, 1 or more.");
                return;
            }

            var name = row.Get("name")?.Text is { Length: > 0 } text ? CalcSheetTextRenderer.Clean(text) : $"Cable {i + 1}";
            cables.Add(new Cable(name, Cell(row, "od", mm), count, Cell(row, "mass", kgPerM), row.Get("T_max") is { Number: { } } ? Cell(row, "T_max", newton) : null));
        }

        for (var i = 0; i < cables.Count; i++)
        {
            var cable = cables[i];
            var area = cable.Count * Math.PI * cable.Od * cable.Od / 4;
            c.Record($"A_c{i + 1}", "n·π·d²/4", $"{F(cable.Count)} × π × ({F(mm.FromSi(cable.Od))} mm)² / 4", ScalarValue.Of(mm2.FromSi(area), mm2), $"{cable.Name}: cross-section of {F(cable.Count)} cable(s)");
        }

        var total = cables.Sum(k => k.Count * Math.PI * k.Od * k.Od / 4);
        c.Record("A_cab", "Σ n_i·π·d_i²/4", Sum(cables.Select((_, i) => $"A_c{i + 1}")), ScalarValue.Of(mm2.FromSi(total), mm2), "Total cable cross-section");
        var dMax = cables.Max(k => k.Od);
        c.Record("OD_max", "max d_i", $"{F(mm.FromSi(dMax))} mm", ScalarValue.Of(mm.FromSi(dMax), mm), "Largest cable overall diameter");
        var weight = cables.Sum(k => k.Count * k.Mass);
        c.Record("w", "Σ n_i·m_i", Sum(cables.Select(k => $"{F(k.Count)} × {F(k.Mass)} kg/m")), ScalarValue.Of(weight, kgPerM), "Mass per metre of the bundle");

        // Containment area and fill.
        var rectangular = c.InputText("cont_type") == Trunking;

        if (rectangular)
        {
            foreach (var symbol in new[] { "W_c", "H_c" })
            {
                if (!c.Has(symbol))
                {
                    c.Block("containment", $"{symbol}: enter the trunking or tray internal {(symbol == "W_c" ? "width" : "depth")} (the containment is trunking or tray).");
                    return;
                }

                c.Input(symbol);
            }

            c.Assign("A_int", "W_c * H_c", "mm^2", "Internal cross-section of the trunking or tray");
        }
        else
        {
            if (!c.Has("D_c"))
            {
                c.Block("containment", "D_c: enter the conduit internal diameter (the containment is a conduit).");
                return;
            }

            c.Input("D_c");
            c.Assign("A_int", "pi * D_c^2 / 4", "mm^2", "Internal cross-section of the conduit");
        }

        c.Input("f_max");
        c.Assign("fill", "A_cab / A_int", "%", "Fill: cable cross-section over internal cross-section");
        c.Assign("u_fill", "A_cab / (A_int * f_max)", null, "Fill utilisation against the space factor");
        var totalCables = cables.Sum(k => k.Count);
        var smallSide = rectangular ? Math.Min(c.Si("W_c"), c.Si("H_c")) : c.Si("D_c");
        var fits = rectangular ? dMax <= smallSide : dMax < smallSide;

        if (!rectangular && Math.Abs(totalCables - 3) < 1e-9 && cables.Max(k => k.Od) - cables.Min(k => k.Od) <= 1e-9 * dMax && 1.05 * c.Si("D_c") / dMax is var jam && jam >= 2.8 && jam <= 3.2)
            c.Warn("jam", $"D_c: three equal cables in a conduit with 1.05·D/d = {CalcValue.FormatNumber(jam, 3)} (D/d with a 5 % allowance for ovality; between 2.8 and 3.2) may jam when pulled, whatever the fill; use a larger conduit or smaller cables.");

        var outputs = new List<(string, string)> { ("A_cab", "Total cable cross-section"), ("A_int", "Internal cross-section"), ("fill", "Fill"), ("u_fill", "Fill utilisation"), ("OD_max", "Largest cable diameter"), ("w", "Mass per metre of the bundle") };

        // Route.
        var routeRows = c.Rows("route");
        var segments = new List<Segment>();
        var deg = UnitCatalog.Parse("deg");
        var metre = UnitCatalog.Parse("m");

        for (var i = 0; i < routeRows.Count; i++)
        {
            var row = routeRows[i];
            var name = row.Get("name")?.Text is { Length: > 0 } text ? CalcSheetTextRenderer.Clean(text) : $"Segment {i + 1}";
            var bend = row.Get("kind")?.Text == Bend;

            if (bend)
            {
                var angle = Cell(row, "theta", deg);
                var radius = Cell(row, "R", mm);

                if (!(angle > 0) || !(radius > 0))
                {
                    c.Block("route", $"route[{i + 1}].{(angle > 0 ? "R" : "theta")}: enter the {(angle > 0 ? "radius" : "angle")} of the bend ({name}); both are above zero.");
                    return;
                }

                segments.Add(new Segment(i + 1, name, true, 0, angle, radius));
            }
            else
            {
                var length = Cell(row, "L", metre);

                if (!(length > 0))
                {
                    c.Block("route", $"route[{i + 1}].L: enter the length of the straight segment ({name}), above zero.");
                    return;
                }

                segments.Add(new Segment(i + 1, name, false, length, 0, 0));
            }
        }

        // Bend radius.
        var bends = segments.Where(s => s.IsBend).ToList();
        var bendRadiusChecked = false;

        if (c.Has("n_bend"))
        {
            c.Input("n_bend");
            c.Assign("R_min", "n_bend * OD_max", "mm", "Minimum bend radius for the largest cable");
            outputs.Add(("R_min", "Minimum bend radius"));
            var minimum = c.Si("R_min");

            if (bends.Count > 0)
            {
                foreach (var bend in bends)
                    c.Record($"u_b{bend.Index}", "R_min/R", $"{F(mm.FromSi(minimum))} mm / {F(mm.FromSi(bend.Radius))} mm", new ScalarValue(minimum / bend.Radius), $"{bend.Name}: bend radius utilisation");

                var worst = bends.Max(b => minimum / b.Radius);
                c.Record("u_bend", "max R_min/R", $"{bends.Count} bend(s)", new ScalarValue(worst), "Bend radius utilisation (tightest bend)");
                outputs.Add(("u_bend", "Bend radius utilisation"));
                bendRadiusChecked = true;
            }
        }
        else if (bends.Count > 0)
        {
            c.Warn("n_bend", "n_bend: no bend-radius multiple is entered, so the bends are not checked against the cable; enter it from the cable maker's data.");
        }

        // Pull.
        Leg[]? forward = null;
        Leg[]? reverse = null;
        var recommendedReverse = false;
        double? cableLimit = null;
        double? gripLimit = null;
        double? swpLimit = null;
        var direction = string.Empty;

        if (segments.Count > 0)
        {
            if (!c.Has("mu"))
            {
                c.Block("mu", "mu: enter the coefficient of friction for the pull (the route has segments); it has no default.");
                return;
            }

            c.Input("mu");
            c.Input("T_1");
            var weightFactor = 1.0;

            if (c.Has("w_c"))
            {
                c.Input("w_c");
                weightFactor = c.Si("w_c");
            }

            c.Assign("wg", $"w * {Gravity.ToString("R", CultureInfo.InvariantCulture)} m/s^2" + (c.Has("w_c") ? " * w_c" : string.Empty), "N/m", c.Has("w_c") ? "Weight per metre of the bundle on the containment (with the weight-correction factor)" : "Weight per metre of the bundle on the containment");
            var wg = c.Si("wg");
            var mu = c.Si("mu");
            var start = c.Si("T_1");
            var weighted = c.InputText("bend_model") == BendWeighted;

            forward = Pull(segments, start, wg, mu, weightFactor, weighted);
            reverse = Pull([.. segments.AsEnumerable().Reverse()], start, wg, mu, weightFactor, weighted);

            if (!AllFinite(forward) || !AllFinite(reverse))
            {
                c.Block("route", $"route: the pulling tension exceeds {F(TensionCeiling)} N; reduce μ, the bend angles or the lengths, or enter the route again.");
                return;
            }

            RecordPull(c, forward, "a", "as listed", start, wg, mu, weightFactor, weighted, newton, perM);
            RecordPull(c, reverse, "b", "reversed", start, wg, mu, weightFactor, weighted, newton, perM);

            var tensionA = forward[^1].T;
            var tensionB = reverse[^1].T;
            var swpA = forward.Max(l => l.Swp ?? 0);
            var swpB = reverse.Max(l => l.Swp ?? 0);
            c.Record("T_max_a", "T at the end of the route, as listed", $"{F(tensionA)} N", ScalarValue.Of(tensionA, newton), "Maximum tension, pulling in the listed order");
            c.Record("T_max_b", "T at the end of the route, reversed", $"{F(tensionB)} N", ScalarValue.Of(tensionB, newton), "Maximum tension, pulling in the reverse order");
            outputs.Add(("T_max_a", "Maximum tension, as listed"));
            outputs.Add(("T_max_b", "Maximum tension, reversed"));

            if (bends.Count > 0)
            {
                c.Record("P_max_a", "max SWP, as listed", $"{F(swpA)} N/m", ScalarValue.Of(swpA, perM), "Maximum sidewall pressure, pulling in the listed order");
                c.Record("P_max_b", "max SWP, reversed", $"{F(swpB)} N/m", ScalarValue.Of(swpB, perM), "Maximum sidewall pressure, pulling in the reverse order");
                outputs.Add(("P_max_a", "Maximum sidewall pressure, as listed"));
                outputs.Add(("P_max_b", "Maximum sidewall pressure, reversed"));
            }

            // Limits: the cable limit (equal sharing, the weakest cable governs), the grip limit, the sidewall pressure limit.
            if (c.Has("k_n"))
                c.Input("k_n");

            var derate = c.Has("k_n") && totalCables > 3 ? c.Si("k_n") : 1.0;

            if (c.Has("k_n") && totalCables <= 3)
                c.Warn("k_n", $"k_n: the derating is for more than 3 cables on one grip and the bundle has {F(totalCables)}, so it is not applied.");

            if (cables.All(k => k.Tmax is not null))
            {
                var lowest = cables.Min(k => k.Tmax!.Value);
                cableLimit = derate * totalCables * lowest;
                var factor = derate < 1 ? $"{F(derate)} × " : string.Empty;
                c.Record("T_cab", derate < 1 ? "k_n·N·min(T_max,i)" : "N·min(T_max,i)", $"{factor}{F(totalCables)} × {F(lowest)} N", ScalarValue.Of(cableLimit.Value, newton), "Pulling tension limit of the bundle from the cables (equal load share, weakest cable)");
            }
            else
            {
                c.Warn("T_cab", "T_max: a cable has no maximum pulling tension, so the cable limit is not checked; enter the maximum pulling tension of every cable.");
            }

            if (c.Has("T_grip"))
            {
                c.Input("T_grip");
                gripLimit = c.Si("T_grip");
            }

            if (bends.Count > 0)
            {
                if (c.Has("P_lim"))
                {
                    c.Input("P_lim");
                    swpLimit = c.Si("P_lim");
                }
                else
                {
                    c.Warn("P_lim", "P_lim: no sidewall pressure limit is entered, so the pressure at the bends is not checked; enter it from the cable maker's data.");
                }
            }

            // The direction: the one that passes every tension and sidewall check; the lower tension (then lower SWP) when both or neither do.
            bool Passes(double tension, double swp) =>
                (cableLimit is null || tension <= cableLimit.Value * (1 + 1e-9))
                && (gripLimit is null || tension <= gripLimit.Value * (1 + 1e-9))
                && (swpLimit is null || swp <= swpLimit.Value * (1 + 1e-9));

            var passA = Passes(tensionA, swpA);
            var passB = Passes(tensionB, swpB);
            var tied = Math.Abs(tensionA - tensionB) <= 1e-9 * Math.Max(tensionA, tensionB);
            var swpTied = Math.Abs(swpA - swpB) <= 1e-9 * Math.Max(swpA, swpB);
            var onlyOne = passA != passB;
            recommendedReverse = onlyOne ? passB : tensionB < tensionA && !tied || tied && swpB < swpA && !swpTied;
            var last = segments[^1].Index;
            var first = segments[0].Index;
            direction = !onlyOne && tied && swpTied
                ? "Either direction (the same maximum tension and sidewall pressure)"
                : (recommendedReverse ? $"Reverse: pull from segment {last} to segment {first}" : $"As listed: pull from segment {first} to segment {last}")
                  + (onlyOne ? " (the only direction that passes every check)" : string.Empty);

            var chosen = recommendedReverse ? reverse : forward;
            var maxTension = chosen[^1].T;
            c.Record("max_tension", onlyOne ? "T_max of the direction that passes every check" : "min(T_max,a ; T_max,b)", $"{F(tensionA)} N ; {F(tensionB)} N", ScalarValue.Of(maxTension, newton), "Maximum pulling tension, recommended direction");
            outputs.Add(("max_tension", "Maximum tension, recommended direction"));

            if (bends.Count > 0)
            {
                var maxSwp = recommendedReverse ? swpB : swpA;
                c.Record("P_max", "max SWP in the recommended direction", $"{F(maxSwp)} N/m", ScalarValue.Of(maxSwp, perM), "Maximum sidewall pressure, recommended direction");
                outputs.Add(("P_max", "Maximum sidewall pressure, recommended direction"));
            }

            if (cableLimit is not null)
            {
                var share = maxTension / totalCables;

                for (var i = 0; i < cables.Count; i++)
                    c.Record($"u_Tc{i + 1}", "(T/N)/(k_n·T_max,i)", $"{F(share)} N / ({F(derate)} × {F(cables[i].Tmax!.Value)} N)", new ScalarValue(share / (derate * cables[i].Tmax!.Value)), $"{cables[i].Name}: share of the pull against its own limit");

                c.Assign("u_T", "max_tension / T_cab", null, "Tension utilisation against the cable limit (the weakest cable)");
                outputs.Add(("T_cab", "Pulling tension limit from the cables"));
                outputs.Add(("u_T", "Cable tension utilisation"));
            }

            if (gripLimit is not null)
            {
                c.Assign("u_grip", "max_tension / T_grip", null, "Tension utilisation against the grip or pulling-eye limit");
                outputs.Add(("u_grip", "Grip tension utilisation"));
            }

            if (swpLimit is not null)
            {
                c.Assign("u_P", "P_max / P_lim", null, "Sidewall pressure utilisation against the limit");
                outputs.Add(("u_P", "Sidewall pressure utilisation"));
            }
        }
        else
        {
            foreach (var (symbol, what) in new[] { ("mu", "the coefficient of friction"), ("T_grip", "the grip limit"), ("P_lim", "the sidewall pressure limit"), ("k_n", "the derating"), ("w_c", "the weight-correction factor") })
            {
                if (c.Has(symbol))
                    c.Warn("route-limits", $"{symbol}: {what} is entered but there is no route, so it is not used; enter the route to work the pull.");
            }
        }

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        if (segments.Count > 0)
            c.Output("direction", "Recommended pull direction", new TextValue(direction));

        c.CheckUtilisation("Fill against the space factor", "u_fill");
        c.CheckCondition("Largest cable fits the containment", fits, $"largest cable {F(mm.FromSi(dMax))} mm", $"{(rectangular ? "smaller internal side" : "conduit internal diameter")} {F(mm.FromSi(smallSide))} mm", "OD_max");

        if (bendRadiusChecked)
            c.CheckUtilisation("Bend radius against the cable minimum", "u_bend", "cable maker's limit; cf. BS 7671 reg. 522.8");

        if (cableLimit is not null && segments.Count > 0)
            c.CheckUtilisation("Pulling tension against the weakest cable's limit", "u_T");

        if (gripLimit is not null)
            c.CheckUtilisation("Pulling tension against the grip or pulling-eye limit", "u_grip");

        if (swpLimit is not null)
            c.CheckUtilisation("Sidewall pressure against the limit", "u_P");
    }

    /// <summary>Tension (and sidewall pressure at bends) after each segment, pulling in the order given.</summary>
    private static Leg[] Pull(IReadOnlyList<Segment> segments, double start, double weightPerMetre, double mu, double weightFactor, bool weighted)
    {
        var legs = new Leg[segments.Count];
        var tension = start;

        for (var i = 0; i < segments.Count; i++)
        {
            var s = segments[i];

            if (s.IsBend)
            {
                tension = BendTension(tension, weightPerMetre, s.Radius, weightFactor * mu * s.Angle, weighted);
                legs[i] = new Leg(s, tension, tension / s.Radius);
            }
            else
            {
                tension += weightPerMetre * mu * s.Length;
                legs[i] = new Leg(s, tension, null);
            }

            if (!double.IsFinite(tension) || tension > TensionCeiling)
            {
                for (var j = i; j < legs.Length; j++)
                    legs[j] = new Leg(segments[j], double.PositiveInfinity, null);

                break;
            }
        }

        return legs;
    }

    /// <summary>
    /// The tension leaving a bend of friction angle <paramref name="muTheta"/> (μ_e·θ). Capstan: T₁·e^(μθ). Horizontal bend with the cable resting on the floor (Rifenburg):
    /// T₁·cosh(μθ) + √(T₁² + (w·g·R)²)·sinh(μθ), <paramref name="weightPerMetre"/> already carrying the weight-correction factor.
    /// </summary>
    private static double BendTension(double t1, double weightPerMetre, double radius, double muTheta, bool weighted)
    {
        if (!weighted)
            return t1 * Math.Exp(muTheta);

        var wr = weightPerMetre * radius;
        return t1 * Math.Cosh(muTheta) + Math.Sqrt(t1 * t1 + wr * wr) * Math.Sinh(muTheta);
    }

    private static bool AllFinite(IEnumerable<Leg> legs) => legs.All(l => double.IsFinite(l.T) && l.T <= TensionCeiling && (l.Swp is null || double.IsFinite(l.Swp.Value)));

    /// <summary>One record per segment: the tension after it (and the sidewall pressure at a bend), as <c>Ta_1</c>… for the listed order and <c>Tb_5</c>… for the reverse.</summary>
    private static void RecordPull(CalcRunContext c, Leg[] legs, string tag, string label, double start, double weightPerMetre, double mu, double weightFactor, bool weighted, Unit newton, Unit perM)
    {
        var previous = start;

        foreach (var leg in legs)
        {
            var s = leg.Segment;
            var symbol = $"T{tag}_{s.Index}";

            if (s.IsBend)
            {
                c.Record(symbol, weighted ? "T₁·cosh(μ_e·θ) + √(T₁² + (w·g·w_c·R)²)·sinh(μ_e·θ)" : "T₁·e^(w_c·μ·θ)", weighted ? $"{F(previous)} N × cosh({F(weightFactor * mu * s.Angle)}) + √({F(previous)}² + ({F(weightPerMetre)} N/m × {F(s.Radius)} m)²) × sinh({F(weightFactor * mu * s.Angle)})" : $"{F(previous)} N × e^({F(weightFactor)} × {F(mu)} × {F(s.Angle)} rad)", ScalarValue.Of(leg.T, newton), $"{s.Name} ({label}): tension after the bend");
                c.Record($"P{tag}_{s.Index}", "T₂/R", $"{F(leg.T)} N / {F(s.Radius)} m", ScalarValue.Of(leg.Swp ?? 0, perM), $"{s.Name} ({label}): sidewall pressure");
            }
            else
            {
                c.Record(symbol, "T₁ + w·g·w_c·μ·L", $"{F(previous)} N + {F(weightPerMetre)} N/m × {F(mu)} × {F(s.Length)} m", ScalarValue.Of(leg.T, newton), $"{s.Name} ({label}): tension after the straight length");
            }

            previous = leg.T;
        }
    }

    private static string Sum(IEnumerable<string> terms)
    {
        var list = terms.ToList();
        return list.Count <= 6 ? string.Join(" + ", list) : string.Join(" + ", list.Take(5)) + $" + … ({list.Count} terms)";
    }

    private static double Cell(CalcRow row, string key, Unit unit) => row.Get(key) is { Number: { } n } cell && double.IsFinite(n) ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : unit).ToSi(n) : 0;

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
