using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B9 — insulation thickness on a pipe or a flat surface by the ISO 12241 formula route: the thickness that meets a heat-loss limit,
/// a hot-surface temperature limit and an anti-condensation requirement, each solved by a root find on the thickness, and for a chosen
/// thickness the heat flow, the surface temperature and PASS/FAIL per criterion. No thickness table of any standard is used.
/// </summary>
public sealed class InsulationCalculator : ICalcCalculator
{
    public const string Id = "bs.insulation";
    public const string Pipe = "pipe (cylinder)";
    public const string Flat = "flat surface";
    public const string HGiven = "h_se as given";
    public const string HCorrelation = "convection + radiation (correlation)";

    /// <summary>The record kind of a W3 material (<c>MaterialLibrary.Kind</c>; written out here because this assembly does not reference the library, and a test keeps the two equal).</summary>
    public const string MaterialKind = "material.properties";

    public CalcTemplate Template { get; } = new(
        Id, "1.0.0", CalcDiscipline.BuildingServices, "Insulation thickness (ISO 12241 formula route)",
        "The insulation thickness on a pipe (cylinder) or a flat surface that meets a heat-flow limit, a limit on the surface temperature of a hot surface and an anti-condensation requirement for a cold one, each solved separately and the governing one reported; and for a chosen thickness the heat flow, the surface temperature and PASS/FAIL per criterion. Steady state, one layer, no thickness table of any standard.",
        "Pipe resistance per metre: R = ln(D_e/D_i)/(2π·λ) + 1/(h_se·π·D_e), D_e = D_i + 2t; with the inner film and pipe wall R_oth = 1/(h_i·π·D_p) + ln(D_i/D_p)/(2π·λ_w) added in series\n" +
        "Flat resistance per square metre: R = t/λ + 1/h_se (+ 1/h_i + t_w/λ_w)\n" +
        "Heat flow: q = (θ_i − θ_a)/R, positive from the process to the ambient\n" +
        "Surface temperature: θ_se = θ_a + q/(h_se·π·D_e) (pipe), θ_a + q/h_se (flat)\n" +
        "Surface coefficient: h_se as given, or h_se = h_c + h_r with h_r = ε·σ·(T_s² + T_a²)(T_s + T_a); h_c from free convection to air (laminar or turbulent, the larger), forced convection (cross-flow cylinder or flat plate) and their cube-law blend; solved with θ_se, which it depends on\n" +
        "Heat-loss limit: the thickness beyond which |q| ≤ q_max; surface-temperature limit (hot): θ_se ≤ θ_se,max; anti-condensation (cold): θ_se ≥ θ_dew + margin, θ_dew from the Magnus relation at θ_a and RH\n" +
        "Solve: each criterion's thickness by scanning t from 0 to 1 m for the last broken point and bisecting the bracket after it; governing t = max over the selected criteria\n" +
        "Critical diameter: D_crit = 2λ/h_se; below it, insulation raises the heat flow until D_e passes D_crit",
        [
            new CalcInputSpec("geometry", "Surface", CalcInputKind.Choice, Choices: [Pipe, Flat], DefaultChoice: Pipe, Group: "Geometry"),
            new CalcInputSpec("D_i", "Pipe outer diameter, the inside of the insulation (pipe)", CalcInputKind.Quantity, "mm", Default: 100, Min: 10, Max: 1000, HardMin: 5, HardMax: 3000, Group: "Geometry"),
            new CalcInputSpec("theta_i", "Process (fluid) temperature", CalcInputKind.Quantity, "degC", Default: 80, Min: -50, Max: 400, HardMin: -200, HardMax: 650, Group: "Conditions"),
            new CalcInputSpec("theta_a", "Ambient air temperature (also the radiation sink)", CalcInputKind.Quantity, "degC", Default: 20, Min: -20, Max: 50, HardMin: -60, HardMax: 70, Group: "Conditions"),
            new CalcInputSpec("lambda", "Thermal conductivity of the insulation", CalcInputKind.Quantity, "W/(m*K)", Default: 0.04, Min: 0.015, Max: 0.2, HardMin: 0.005, HardMax: 2, Group: "Insulation", MaterialProperty: "lambda"),
            new CalcInputSpec("theta_lambda", "Temperature the conductivity applies at (a note: compared with the mean insulation temperature)", CalcInputKind.Quantity, "degC", Min: -50, Max: 400, HardMin: -200, HardMax: 650, IsOptional: true, Group: "Insulation"),
            new CalcInputSpec("h_mode", "Where the outer surface coefficient comes from", CalcInputKind.Choice, Choices: [HGiven, HCorrelation], DefaultChoice: HGiven, Group: "Outer surface"),
            new CalcInputSpec("h_se", "Outer surface coefficient h_se (as given)", CalcInputKind.Quantity, "W/(m^2*K)", Default: 10, Min: 3, Max: 40, HardMin: 0.5, HardMax: 200, Group: "Outer surface"),
            new CalcInputSpec("eps", "Emissivity of the outer surface (correlation)", CalcInputKind.Number, Default: 0.9, Min: 0.05, Max: 1, HardMin: 0.01, HardMax: 1, Group: "Outer surface"),
            new CalcInputSpec("v_wind", "Air speed over the surface (correlation; 0 = still air)", CalcInputKind.Quantity, "m/s", Default: 0, Min: 0, Max: 20, HardMin: 0, HardMax: 60, Group: "Outer surface"),
            new CalcInputSpec("L_surf", "Height or flow length of the flat surface (correlation, flat)", CalcInputKind.Quantity, "m", Default: 1, Min: 0.1, Max: 10, HardMin: 0.01, HardMax: 100, Group: "Outer surface"),
            new CalcInputSpec("h_i", "Inner film coefficient (blank = the pipe wall is at the process temperature)", CalcInputKind.Quantity, "W/(m^2*K)", Min: 50, Max: 20000, HardMin: 0.5, HardMax: 1e5, IsOptional: true, Group: "Pipe wall and film"),
            new CalcInputSpec("t_w", "Pipe wall thickness (with its conductivity; blank = not included)", CalcInputKind.Quantity, "mm", Min: 0.5, Max: 30, HardMin: 0.1, HardMax: 100, IsOptional: true, Group: "Pipe wall and film"),
            new CalcInputSpec("lambda_w", "Pipe wall conductivity", CalcInputKind.Quantity, "W/(m*K)", Min: 0.2, Max: 400, HardMin: 0.1, HardMax: 500, IsOptional: true, Group: "Pipe wall and film"),
            new CalcInputSpec("q_max_m", "Heat-loss limit per metre of pipe (blank = not a criterion)", CalcInputKind.Quantity, "W/m", Default: 25, Min: 1, Max: 1000, HardMin: 1e-6, HardMax: 1e5, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("q_max_A", "Heat-loss limit per square metre of flat surface (blank = not a criterion)", CalcInputKind.Quantity, "W/m^2", Min: 1, Max: 500, HardMin: 1e-6, HardMax: 1e5, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("theta_se_max", "Highest surface temperature, for a hot surface (blank = not a criterion)", CalcInputKind.Quantity, "degC", Default: 45, Min: 30, Max: 70, HardMin: -50, HardMax: 300, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("dew_margin", "Margin of the surface above the dew point, for a cold surface (blank = not a criterion)", CalcInputKind.Quantity, "K", Min: 0, Max: 5, HardMin: 0, HardMax: 20, IsOptional: true, Group: "Criteria"),
            new CalcInputSpec("RH_a", "Relative humidity of the ambient air (anti-condensation)", CalcInputKind.Quantity, "%", Default: 60, Min: 20, Max: 95, HardMin: 1, HardMax: 100, Group: "Criteria"),
            new CalcInputSpec("t_chosen", "Chosen insulation thickness (blank = report at the required thickness, no checks)", CalcInputKind.Quantity, "mm", Default: 40, Min: 0, Max: 300, HardMin: 0, HardMax: 1000, IsOptional: true, Group: "Chosen thickness"),
        ],
        [
            new CalcAssumption("Steady one-dimensional heat flow through a single homogeneous layer of constant conductivity; no thermal bridges at supports, no joints, no moisture in the insulation. λ is taken at the value entered: use the value at the mean insulation temperature shown on the sheet (optionally noted in 'Temperature the conductivity applies at').", "Formula route of the standard."),
            new CalcAssumption("With the correlation, h_se = h_c + h_r. Radiation: h_r = ε·σ·(T_s² + T_a²)(T_s + T_a) to surroundings at the ambient air temperature. Free convection to air: Holman's simplified relations at atmospheric pressure, the larger of the laminar and turbulent forms (horizontal cylinder 1.32(ΔT/D)^0.25 and 1.24ΔT^(1/3); vertical surface 1.42(ΔT/L)^0.25 and 1.31ΔT^(1/3)). Forced convection: Churchill–Bernstein for a cylinder in cross-flow, the laminar/mixed flat-plate relations along a plate; air properties from fits at the film temperature, Pr = 0.71; blended with free convection as (h_nat³ + h_forced³)^(1/3). Indicative, not the standard's own correlation.", "Incropera 7th ed. §7.2, §7.4, §9.6; Holman, Heat Transfer; the standard's own coefficients are not reproduced."),
            new CalcAssumption("The dew point is the Magnus relation over water (ice below 0 °C), the same closed form B7 uses. A margin above it is the user's.", "Derived; B7 shares the function."),
            new CalcAssumption("The required thickness is the least thickness beyond which the criterion holds for every thicker layer up to 1 m, which is what matters when thin insulation on a small pipe raises the heat flow (critical diameter).", "Stated."),
            new CalcAssumption("No thickness table is used or shipped: not BS 5422, not the tables of any standard. The conductivity is the user's input, from a W3 material or the maker's data.", "Addendum B §B5."),
        ],
        [
            new CalcClauseRef("ISO 12241", "formula route (clause numbers to be checked)", "Thermal insulation for building equipment and industrial installations: calculation rules"),
            new CalcClauseRef("Incropera, Fundamentals of Heat and Mass Transfer", "7th ed., §7.2, §7.4, §9.6", "External forced convection, free convection and mixed convection correlations"),
            new CalcClauseRef("ISO 13788", "cl. 6", "Magnus saturation vapour pressure (shared with B7)"),
        ],
        [
            new CalcApplicabilityRule("temperatures", "The process temperature must differ from the ambient temperature.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("criteria", "At least one criterion that applies to the sign of θ_i − θ_a must be selected: a heat-flow limit, a hot-surface limit (hot) or a dew-point margin (cold).", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("unattainable", "A criterion that no thickness up to 1 m meets is refused.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("surface-limit", "The hot-surface limit must be above the ambient temperature.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("dew", "The dew point plus the margin must be below the ambient temperature.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("wall", "The pipe wall needs both thickness and conductivity, and must be thinner than the pipe radius.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("criterion-na", "A hot-surface limit on a cold pipe, or a dew-point margin on a hot one, is ignored.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("critical-radius", "The pipe is below the critical diameter 2λ/h_se, or the chosen insulation carries more heat than the bare pipe.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("lambda-temperature", "The mean insulation temperature is more than 20 K from the temperature the conductivity applies at.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-pipe-chosen", "Hot pipe, 30 mm chosen: heat flow and surface temperature",
                new CalcReferenceSource("Analytic (R = ln(D_e/D_i)/(2πλ) + 1/(h_se·π·D_e); q = Δθ/R; θ_se = θ_a + q/(h_se·π·D_e))", null, "D_i 100 mm, t 30 mm, D_e 160 mm, λ 0.04, h_se 10, 80 → 20 °C: R = 0.2999 + 0.0199 + ... = 2.069029 K·m/W; q = 28.99911 W/m; θ_se = 25.76919 °C; recomputed in python3"),
                [Choice("geometry", Pipe), Q("t_chosen", 30, "mm"), Q("q_max_m", 30, "W/m"), Q("theta_se_max", 45, "degC")],
                [new CalcExpectedValue("R", 2.0690287005046017, "m*K/W", 1e-9), new CalcExpectedValue("q_per_m", 28.99911440830521, "W/m", 1e-9), new CalcExpectedValue("theta_se", 25.769190504211473, "degC", 1e-9), new CalcExpectedValue("u_q", 0.9666371469435070, "", 1e-9)]),
            new CalcTestCase("ka-pipe-heat-loss", "Hot pipe: the thickness for a heat-loss limit of 25 W/m",
                new CalcReferenceSource("Analytic (root of q(t) = 25 W/m)", null, "D_i 100 mm, λ 0.04, h_se 10, 80 → 20 °C: R = 60/25 = 2.4 K·m/W is reached at t = 37.30360 mm; the surface is then at 24.55751 °C; recomputed in python3 by bisection to 1e-15"),
                [Choice("geometry", Pipe), Q("q_max_m", 25, "W/m")],
                [new CalcExpectedValue("thickness", 37.30359728106675, "mm", 1e-9), new CalcExpectedValue("t_q", 37.30359728106675, "mm", 1e-9), new CalcExpectedValue("theta_se", 24.557513895432887, "degC", 1e-9)]),
            new CalcTestCase("ka-pipe-surface", "Hot pipe: the thickness for a 45 °C surface limit, and it is not the governing one",
                new CalcReferenceSource("Analytic (root of θ_se(t) = 45 °C)", null, "As above: θ_se = 45 °C at t = 5.325914 mm where q = 86.90574 W/m; the 25 W/m limit needs 37.30360 mm and governs; recomputed in python3"),
                [Choice("geometry", Pipe), Q("q_max_m", 25, "W/m"), Q("theta_se_max", 45, "degC")],
                [new CalcExpectedValue("t_se", 5.325913504887262, "mm", 1e-9), new CalcExpectedValue("t_q", 37.30359728106675, "mm", 1e-9), new CalcExpectedValue("thickness", 37.30359728106675, "mm", 1e-9)]),
            new CalcTestCase("ka-flat-closed-form", "Flat wall: t = λ(Δθ/q − 1/h_se) in closed form",
                new CalcReferenceSource("Analytic (plane layer)", null, "λ 0.04, h_se 10, Δθ 60 K, limit 20 W/m²: R = 3 m²K/W, t = 0.04·(3 − 0.1) = 116 mm; the surface is at 20 + 20/10 = 22 °C"),
                [Choice("geometry", Flat), Q("q_max_A", 20, "W/m^2")],
                [new CalcExpectedValue("thickness", 116, "mm", 1e-9), new CalcExpectedValue("theta_se", 22, "degC", 1e-9), new CalcExpectedValue("q_area", 20, "W/m^2", 1e-9)]),
            new CalcTestCase("ka-flat-condensation", "Cold flat surface: the thickness to keep the surface 1 K above the dew point",
                new CalcReferenceSource("Analytic (plane layer, Magnus dew point)", null, "5 °C surface in 25 °C air at 70 % RH: θ_dew = 19.14535 °C, target 20.14535 °C; t = λ(Δθ/(h·X) − 1/h) with X = 25 − 20.14535 = 4.85465 K gives 12.47905 mm; recomputed in python3"),
                [Choice("geometry", Flat), Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 70, "%"), Q("dew_margin", 1, "K")],
                [new CalcExpectedValue("theta_dew", 19.14535190118326, "degC", 1e-9), new CalcExpectedValue("thickness", 12.479052316788733, "mm", 1e-9)]),
            new CalcTestCase("ka-pipe-condensation", "Cold pipe: the thickness for the same dew-point margin",
                new CalcReferenceSource("Analytic (root of θ_se(t) = θ_dew + 1 K on a cylinder)", null, "D_i 100 mm, λ 0.04, h_se 10, 5 °C in 25 °C air at 70 % RH: t = 11.29066 mm, q = −18.69528 W/m (heat gain); recomputed in python3"),
                [Choice("geometry", Pipe), Q("theta_i", 5, "degC"), Q("theta_a", 25, "degC"), Q("RH_a", 70, "%"), Q("dew_margin", 1, "K")],
                [new CalcExpectedValue("thickness", 11.290660460665531, "mm", 1e-9), new CalcExpectedValue("q_per_m", 18.695277853154153, "W/m", 1e-9), new CalcExpectedValue("theta_se", 20.14535190118326, "degC", 1e-9)]),
            new CalcTestCase("ka-correlation-still", "Hot pipe, h_se from convection and radiation in still air, low-emissivity cladding",
                new CalcReferenceSource("Analytic (the stated correlation, solved with θ_se; recomputed in python3 with an independent implementation)", null, "D_i 114 mm, λ 0.04, 120 → 15 °C, ε 0.1, 50 mm chosen: θ_se = 27.85497 °C, h_c = 3.674839 (horizontal cylinder, laminar form), h_r = 0.580066, h_se = 4.254904 W/(m²K), q = 36.77262 W/m; 60 W/m needs 23.37433 mm and a 60 °C surface 7.842292 mm"),
                [Choice("geometry", Pipe), Q("D_i", 114, "mm"), Q("theta_i", 120, "degC"), Q("theta_a", 15, "degC"), Choice("h_mode", HCorrelation), CalcInputValue.Of("eps", 0.1), Q("v_wind", 0, "m/s"), Q("q_max_m", 60, "W/m"), Q("theta_se_max", 60, "degC"), Q("t_chosen", 50, "mm")],
                [new CalcExpectedValue("theta_se", 27.854972274669827, "degC", 1e-8), new CalcExpectedValue("h_c", 3.6748386376329623, "W/(m^2*K)", 1e-8), new CalcExpectedValue("h_r", 0.5800655776199201, "W/(m^2*K)", 1e-8), new CalcExpectedValue("h_se", 4.254904215252882, "W/(m^2*K)", 1e-8), new CalcExpectedValue("q_per_m", 36.77262036715434, "W/m", 1e-8), new CalcExpectedValue("t_q", 23.374326102060238, "mm", 1e-8), new CalcExpectedValue("t_se", 7.8422918751036015, "mm", 1e-8)]),
            new CalcTestCase("ka-correlation-wind", "The same pipe, black surface in a 3 m/s wind: forced convection blended with free",
                new CalcReferenceSource("Analytic (Churchill–Bernstein cross-flow blended with free convection; recomputed in python3)", null, "ε 0.9, 3 m/s, 50 mm: h_c = 14.95590, h_r = 4.961738, h_se = 19.91764 W/(m²K); θ_se = 18.03869 °C; q = 40.69004 W/m; 60 W/m needs 29.58591 mm"),
                [Choice("geometry", Pipe), Q("D_i", 114, "mm"), Q("theta_i", 120, "degC"), Q("theta_a", 15, "degC"), Choice("h_mode", HCorrelation), CalcInputValue.Of("eps", 0.9), Q("v_wind", 3, "m/s"), Q("q_max_m", 60, "W/m"), Q("t_chosen", 50, "mm")],
                [new CalcExpectedValue("theta_se", 18.03869096438963, "degC", 1e-8), new CalcExpectedValue("h_c", 14.955903095093321, "W/(m^2*K)", 1e-8), new CalcExpectedValue("h_se", 19.917640766341446, "W/(m^2*K)", 1e-8), new CalcExpectedValue("q_per_m", 40.69003615128239, "W/m", 1e-8), new CalcExpectedValue("thickness", 29.585912146391017, "mm", 1e-8)]),
            new CalcTestCase("ka-flat-correlation", "Flat wall 2 m high, correlation in still air",
                new CalcReferenceSource("Analytic (vertical-surface free convection and radiation; recomputed in python3)", null, "λ 0.04, 50 mm, 60 → 20 °C, ε 0.9, L 2 m: θ_se = 23.94246 °C, h_c = 2.069476, h_r = 5.247289, q = 28.84603 W/m²"),
                [Choice("geometry", Flat), Q("theta_i", 60, "degC"), Q("theta_a", 20, "degC"), Choice("h_mode", HCorrelation), CalcInputValue.Of("eps", 0.9), Q("v_wind", 0, "m/s"), Q("L_surf", 2, "m"), Q("q_max_A", 30, "W/m^2"), Q("t_chosen", 50, "mm")],
                [new CalcExpectedValue("theta_se", 23.942457683302294, "degC", 1e-8), new CalcExpectedValue("h_c", 2.0694755947709167, "W/(m^2*K)", 1e-8), new CalcExpectedValue("h_r", 5.247288761516622, "W/(m^2*K)", 1e-8), new CalcExpectedValue("q_area", 28.846033853358165, "W/m^2", 1e-8)]),
            new CalcTestCase("ka-wall-and-film", "Pipe wall and inner film in series with the insulation",
                new CalcReferenceSource("Analytic (series resistances; recomputed in python3)", null, "D_i 100 mm, 30 mm chosen, λ 0.04, h_se 10, 80 → 20 °C, steel-like wall 3 mm λ 50, inner film 500 W/(m²K): R_oth = 0.0069695, R = 2.075998 K·m/W, q = 28.90176 W/m, θ_se = 25.74982 °C"),
                [Choice("geometry", Pipe), Q("h_i", 500, "W/(m^2*K)"), Q("t_w", 3, "mm"), Q("lambda_w", 50, "W/(m*K)"), Q("q_max_m", 30, "W/m"), Q("t_chosen", 30, "mm")],
                [new CalcExpectedValue("R_oth", 0.006969506297018693, "m*K/W", 1e-9), new CalcExpectedValue("R", 2.0759982068016205, "m*K/W", 1e-9), new CalcExpectedValue("q_per_m", 28.90175906868378, "W/m", 1e-9), new CalcExpectedValue("theta_se", 25.74982227479005, "degC", 1e-9)]),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("thickness", "mm", "Governing required insulation thickness (the largest of the selected criteria)"),
            new CalcOutputPort("q_per_m", "W/m", "Heat flow per metre of pipe at the evaluated thickness (magnitude; pipe only)"),
            new CalcOutputPort("theta_se", "degC", "Outer surface temperature at the evaluated thickness"),
        ],
        RecordKinds:
        [
            new CalcRecordKind(MaterialKind, "Material properties", ["lambda"], "A material from the library: its thermal conductivity fills this input, citing the material, its revision and the page or table of the value."),
            new CalcRecordKind("surface.coefficient", "Outer surface coefficient", ["h_se"], "h_se from your own copy of the standard or the maker's guidance for the surface (still or moving air, jacketing), with its source and edition. No value is shipped."),
            new CalcRecordKind("surface.emissivity", "Surface emissivity", ["eps"], "The total emissivity of the jacketing or finish, from its maker or your reference."),
            new CalcRecordKind("criteria.insulation", "Insulation criteria", ["q_max_m", "q_max_A", "theta_se_max", "dew_margin"], "The heat-loss limit, the hot-surface limit and the dew-point margin of your project specification or your copy of the standard, with their source."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    public void Run(CalcRunContext c)
    {
        var pipe = c.InputText("geometry") == Pipe;
        c.Input("theta_i");
        c.Input("theta_a");
        c.Input("lambda");
        var ti = c.InputSi("theta_i") - 273.15;
        var ta = c.InputSi("theta_a") - 273.15;
        var lambda = c.InputSi("lambda");

        if (!(Math.Abs(ti - ta) >= 0.01))
        {
            c.Block("temperatures", $"theta_i: the process temperature ({F(ti)} °C) is within 0.01 K of the ambient temperature ({F(ta)} °C); there is no heat flow to insulate against. Enter a different process or ambient temperature.");
            return;
        }

        var hot = ti > ta;
        var di = 0.0;

        if (pipe)
        {
            c.Input("D_i");
            di = c.InputSi("D_i");
        }

        // The pipe wall and inner film in series, off unless entered.
        var rOther = 0.0;
        var hasFilm = c.Has("h_i");
        var hasWall = c.Has("t_w") || c.Has("lambda_w");

        if (hasWall && !(c.Has("t_w") && c.Has("lambda_w")))
        {
            c.Block("wall", c.Has("t_w") ? "lambda_w: the pipe wall thickness is entered; enter its conductivity too, or clear the thickness." : "t_w: the pipe wall conductivity is entered; enter its thickness too, or clear the conductivity.");
            return;
        }

        var tw = 0.0;

        if (hasWall)
        {
            c.Input("t_w");
            c.Input("lambda_w");
            tw = c.InputSi("t_w");

            if (pipe && !(2 * tw < di))
            {
                c.Block("wall", $"t_w: the pipe wall ({F(tw * 1000)} mm) is not thinner than the pipe radius ({F(di * 500)} mm); enter a thinner wall.");
                return;
            }

            rOther += pipe ? Math.Log(di / (di - 2 * tw)) / (2 * Math.PI * c.InputSi("lambda_w")) : tw / c.InputSi("lambda_w");
        }

        if (hasFilm)
        {
            c.Input("h_i");
            rOther += pipe ? 1 / (c.InputSi("h_i") * Math.PI * (di - 2 * tw)) : 1 / c.InputSi("h_i");
        }

        // The outer surface.
        var correlation = c.InputText("h_mode") == HCorrelation;
        InsulationModel.Surface surface;

        if (correlation)
        {
            c.Input("eps");
            c.Input("v_wind");
            surface = new InsulationModel.Surface(null, c.InputSi("eps"), c.InputSi("v_wind"), 1);

            if (!pipe)
            {
                c.Input("L_surf");
                surface = surface with { Length = c.InputSi("L_surf") };
            }
        }
        else
        {
            c.Input("h_se");
            surface = new InsulationModel.Surface(c.InputSi("h_se"), 0, 0, 1);
        }

        // The criteria that apply.
        var criteria = new List<Criterion>();
        var qLimitSymbol = pipe ? "q_max_m" : "q_max_A";

        if (c.Has(qLimitSymbol))
        {
            c.Input(qLimitSymbol);
            var qMax = c.InputSi(qLimitSymbol);
            criteria.Add(new Criterion("q", "t_q", "heat-loss limit", s => Math.Abs(s.Q) - qMax, s => Math.Abs(s.Q) / qMax, "u_q", $"|q| ≤ {F(qMax)} {(pipe ? "W/m" : "W/m²")}"));
        }

        if (c.Has("theta_se_max"))
        {
            if (!hot)
            {
                c.Warn("criterion-na", "theta_se_max: a hot-surface limit does not apply to a pipe colder than the air; it is ignored.");
            }
            else
            {
                c.Input("theta_se_max");
                var seMax = c.InputSi("theta_se_max") - 273.15;

                if (!(seMax > ta))
                {
                    c.Block("surface-limit", $"theta_se_max: the surface limit ({F(seMax)} °C) must be above the ambient temperature ({F(ta)} °C); no insulation can bring a surface below the air it stands in.");
                    return;
                }

                criteria.Add(new Criterion("se", "t_se", "surface-temperature limit", s => s.Ts - seMax, s => (s.Ts - ta) / (seMax - ta), "u_se", $"θ_se ≤ {F(seMax)} °C"));
            }
        }

        var dewTarget = double.NaN;

        if (c.Has("dew_margin"))
        {
            if (hot)
            {
                c.Warn("criterion-na", "dew_margin: an anti-condensation margin does not apply to a pipe hotter than the air; it is ignored.");
            }
            else
            {
                c.Input("dew_margin");
                c.Input("RH_a");
                var dew = GlaserModel.DewPoint(c.InputSi("RH_a") * GlaserModel.SaturationPressure(ta));
                dewTarget = dew + c.InputSi("dew_margin");
                c.Record("theta_dew", "inverse Magnus of RH·p_sat(θ_a)", $"θ_a = {F(ta)} °C, RH = {F(c.InputSi("RH_a") * 100)} %", ScalarValue.Of(dew, "degC"), "Dew point of the ambient air", "ISO 13788 cl. 6");

                if (!(dewTarget < ta))
                {
                    c.Block("dew", $"dew_margin: the dew point ({F(dew)} °C) plus the margin ({F(c.InputSi("dew_margin"))} K) is not below the ambient temperature ({F(ta)} °C); no insulation keeps the surface above it. Lower the margin or the humidity.");
                    return;
                }

                var target = dewTarget;
                criteria.Add(new Criterion("dew", "t_dew", "anti-condensation margin", s => target - s.Ts, s => (ta - s.Ts) / (ta - target), "u_dew", $"θ_se ≥ {F(target)} °C"));
            }
        }

        if (criteria.Count == 0)
        {
            c.Block("criteria", $"{(pipe ? "q_max_m" : "q_max_A")}: no criterion applies. Enter a heat-loss limit{(hot ? " or a hot-surface limit (theta_se_max)" : " or a dew-point margin (dew_margin)")} for this {(hot ? "hot" : "cold")} {(pipe ? "pipe" : "surface")}.");
            return;
        }

        var setup = new InsulationModel.Setup(pipe, di, lambda, ti, ta, rOther, surface);
        var grid = InsulationModel.Scan(setup);
        var mm = UnitCatalog.Parse("mm");
        var required = new List<(Criterion Criterion, double Thickness)>();

        foreach (var criterion in criteria)
        {
            var (outcome, t) = InsulationModel.RequiredThickness(setup, grid, criterion.Violation);

            if (outcome == InsulationModel.SearchOutcome.Unattainable)
            {
                c.Block("unattainable", $"{(criterion.Key == "q" ? qLimitSymbol : criterion.Key == "se" ? "theta_se_max" : "dew_margin")}: the {criterion.Name} ({criterion.Limit}) is not met by any thickness up to {F(InsulationModel.MaxThickness * 1000)} mm with these conductivity and surface values. Relax the limit or use a lower conductivity.");
                return;
            }

            required.Add((criterion, t));
            c.Record(criterion.Symbol, $"t such that {criterion.Limit} for every thicker layer", $"scan 0 … {F(InsulationModel.MaxThickness * 1000)} mm, then bisection", ScalarValue.Of(t * 1000, mm), $"Required thickness, {criterion.Name}", "ISO 12241");
        }

        var governing = required.OrderByDescending(r => r.Thickness).First();
        var tGov = governing.Thickness;
        c.Record("thickness", required.Count > 1 ? "max over the selected criteria" : "the one selected criterion", string.Join(", ", required.Select(r => $"{r.Criterion.Symbol} = {F(r.Thickness * 1000)} mm")), ScalarValue.Of(tGov * 1000, mm), tGov > 0 ? $"Governing required thickness ({governing.Criterion.Name})" : "Governing required thickness (the bare surface already complies)", "ISO 12241");

        // The thickness the surface is evaluated at: the one chosen, else the required one.
        var chosen = c.Has("t_chosen");
        double tEval;

        if (chosen)
        {
            c.Input("t_chosen");
            tEval = c.InputSi("t_chosen");
        }
        else
        {
            tEval = tGov;
        }

        var state = InsulationModel.Evaluate(setup, tEval);
        var bare = grid[0];

        if (pipe)
            c.Record("D_e", "D_i + 2t", $"{F(di * 1000)} mm + 2 × {F(tEval * 1000)} mm", ScalarValue.Of(state.De * 1000, mm), chosen ? "Outer diameter of the insulation, chosen thickness" : "Outer diameter of the insulation, required thickness");
        else
            c.Record("t_eval", "t", $"{F(tEval * 1000)} mm", ScalarValue.Of(tEval * 1000, mm), chosen ? "Thickness evaluated (chosen)" : "Thickness evaluated (required)");

        var rUnit = pipe ? "m*K/W" : "m^2*K/W";
        c.Record("R_ins", pipe ? "ln(D_e/D_i)/(2π·λ)" : "t/λ", pipe ? $"ln({F(state.De * 1000)}/{F(di * 1000)})/(2π × {F(lambda)})" : $"{F(tEval * 1000)} mm / {F(lambda)}", ScalarValue.Of(state.RIns, rUnit), "Resistance of the insulation", "ISO 12241");

        if (rOther > 0)
            c.Record("R_oth", pipe ? "1/(h_i·π·D_p) + ln(D_i/D_p)/(2π·λ_w)" : "1/h_i + t_w/λ_w", hasFilm && hasWall ? "inner film and pipe wall" : hasFilm ? "inner film" : "pipe wall", ScalarValue.Of(rOther, rUnit), "Resistance of the inner film and the pipe wall, in series", "ISO 12241");

        if (correlation)
        {
            c.Record("h_c", "free convection to air, blended with forced convection", pipe ? "horizontal cylinder, Holman; Churchill–Bernstein if the wind blows" : "vertical surface, Holman; flat plate if the wind blows", ScalarValue.Of(state.Convection, "W/(m^2*K)"), "Convective surface coefficient at the surface temperature", "Incropera 7th ed. §7, §9");
            c.Record("h_r", "ε·σ·(T_s² + T_a²)(T_s + T_a)", $"ε = {F(surface.Emissivity)}, θ_se = {F(state.Ts)} °C", ScalarValue.Of(state.Radiation, "W/(m^2*K)"), "Radiative surface coefficient", "Incropera 7th ed. §1.2");
            c.Record("h_se", "h_c + h_r", $"{F(state.Convection)} + {F(state.Radiation)}", ScalarValue.Of(state.H, "W/(m^2*K)"), "Outer surface coefficient used", "ISO 12241");
        }

        c.Record("R_se", pipe ? "1/(h_se·π·D_e)" : "1/h_se", pipe ? $"1/({F(state.H)} × π × {F(state.De)} m)" : $"1/{F(state.H)}", ScalarValue.Of(state.RSe, rUnit), "Resistance of the outer surface", "ISO 12241");
        c.Record("R", pipe ? "R_oth + R_ins + R_se" : "R_oth + R_ins + R_se", $"{F(rOther)} + {F(state.RIns)} + {F(state.RSe)}", ScalarValue.Of(rOther + state.RIns + state.RSe, rUnit), pipe ? "Total resistance per metre" : "Total resistance per square metre", "ISO 12241");
        var gain = state.Q < 0;
        var direction = gain ? "heat gain" : "heat loss";
        var qUnit = pipe ? "W/m" : "W/m^2";
        c.Record(pipe ? "q_per_m" : "q_area", "|θ_i − θ_a| / R", $"{F(Math.Abs(ti - ta))} K / {F(rOther + state.RIns + state.RSe)}", ScalarValue.Of(Math.Abs(state.Q), qUnit), pipe ? $"Heat flow per metre of pipe ({direction}, magnitude)" : $"Heat flow per square metre ({direction}, magnitude)", "ISO 12241");
        c.Record("theta_se", pipe ? "θ_a + q/(h_se·π·D_e)" : "θ_a + q/h_se", $"{F(ta)} °C {(state.Q < 0 ? "−" : "+")} {F(Math.Abs(state.Q))} / {F(state.H * state.Area)}", ScalarValue.Of(state.Ts, "degC"), "Outer surface temperature", "ISO 12241");
        var mean = (ti + state.Ts) / 2;
        c.Record("theta_m", "(θ_i + θ_se)/2", $"({F(ti)} + {F(state.Ts)})/2", ScalarValue.Of(mean, "degC"), "Mean temperature of the insulation: take λ at this temperature");

        if (c.Has("theta_lambda"))
        {
            c.Input("theta_lambda");
            var applies = c.InputSi("theta_lambda") - 273.15;

            if (Math.Abs(mean - applies) > 20)
                c.Warn("lambda-temperature", $"theta_lambda: λ applies at {F(applies)} °C but the insulation's mean temperature is {F(mean)} °C; take λ at the mean temperature.");
        }

        // Critical diameter: ln(D_e/D_i)/(2πλ) + 1/(h·π·D_e) is least at D_e = 2λ/h.
        var criticalLines = new List<string>();

        if (pipe)
        {
            var dCrit = 2 * lambda / bare.H;
            c.Record("D_crit", "2λ/h_se", $"2 × {F(lambda)} / {F(bare.H)}", ScalarValue.Of(dCrit * 1000, mm), "Critical diameter: below it, insulation raises the heat flow", "ISO 12241");

            if (di < dCrit)
            {
                var peak = InsulationModel.Evaluate(setup, (dCrit - di) / 2);
                criticalLines.Add($"D_i: the pipe ({F(di * 1000)} mm) is smaller than the critical diameter 2λ/h_se = {F(dCrit * 1000)} mm; insulation up to D_e = {F(dCrit * 1000)} mm raises the heat flow from {F(Math.Abs(bare.Q))} W/m (bare) to {F(Math.Abs(peak.Q))} W/m.");
            }
        }

        if (tEval > 0 && Math.Abs(state.Q) > Math.Abs(bare.Q) * (1 + 1e-9))
            criticalLines.Add($"t_chosen: at {F(tEval * 1000)} mm the insulation carries {F(Math.Abs(state.Q))} {(pipe ? "W/m" : "W/m²")}, more than the bare surface ({F(Math.Abs(bare.Q))}); add more or none.");

        foreach (var line in criticalLines)
            c.Warn("critical-radius", line);

        // Checks, for a chosen thickness only: the required thickness meets each criterion by construction.
        var outputs = new List<(string, string)>();

        foreach (var (criterion, t) in required)
        {
            outputs.Add((criterion.Symbol, $"Required thickness, {criterion.Name}"));

            if (chosen)
            {
                var u = criterion.Utilisation(state);
                c.Record(criterion.UtilisationSymbol, criterion.Key switch { "q" => "|q| / q_max", "se" => "(θ_se − θ_a)/(θ_se,max − θ_a)", _ => "(θ_a − θ_se)/(θ_a − (θ_dew + margin))" }, $"{criterion.Limit}", new ScalarValue(u), $"Utilisation, {criterion.Name}");
                outputs.Add((criterion.UtilisationSymbol, $"Utilisation, {criterion.Name}"));
            }
        }

        if (!double.IsNaN(dewTarget))
            outputs.Insert(0, ("theta_dew", "Dew point of the ambient air"));

        outputs.Add(("thickness", "Governing required thickness"));
        outputs.Add((pipe ? "q_per_m" : "q_area", pipe ? $"Heat flow per metre ({direction})" : $"Heat flow per square metre ({direction})"));
        outputs.Add(("theta_se", "Outer surface temperature"));
        outputs.Add(("theta_m", "Mean insulation temperature"));
        outputs.Add(("R", "Total resistance"));

        if (rOther > 0)
            outputs.Add(("R_oth", "Resistance of the inner film and pipe wall"));

        if (pipe)
            outputs.Add(("D_crit", "Critical diameter"));

        if (correlation)
            outputs.AddRange([("h_c", "Convective surface coefficient"), ("h_r", "Radiative surface coefficient"), ("h_se", "Outer surface coefficient")]);

        foreach (var (symbol, text) in outputs)
            c.Output(symbol, text);

        c.Output("governing", "Governing criterion", new TextValue(tGov > 0 ? governing.Criterion.Name : "none: the bare surface complies"));

        if (chosen)
        {
            foreach (var (criterion, _) in required)
                c.CheckUtilisation($"Chosen thickness against the {criterion.Name}", criterion.UtilisationSymbol, "ISO 12241");
        }

        // The heat flow and surface temperature against thickness, up to a few times the governing thickness.
        var shown = Math.Min(InsulationModel.MaxThickness, Math.Max(0.05, 4 * Math.Max(tGov, tEval)));
        var inRange = grid.Where(g => g.Thickness <= shown).ToList();
        c.Series(new CalcSeries("Heat flow against thickness", "t", "mm", pipe ? "q (magnitude)" : "q (magnitude)", pipe ? "W/m" : "W/m^2", [.. inRange.Select(g => (g.Thickness * 1000, Math.Abs(g.Q)))]));
        c.Series(new CalcSeries("Surface temperature against thickness", "t", "mm", "θ_se", "degC", [.. inRange.Select(g => (g.Thickness * 1000, g.Ts))]));
    }

    /// <summary>One selected criterion: how it is broken, how loaded it is, and what it is called on the sheet.</summary>
    private sealed record Criterion(string Key, string Symbol, string Name, Func<InsulationModel.State, double> Violation, Func<InsulationModel.State, double> Utilisation, string UtilisationSymbol, string Limit);

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
