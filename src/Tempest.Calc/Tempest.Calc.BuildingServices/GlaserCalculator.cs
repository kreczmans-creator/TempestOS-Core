using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B7 — Glaser interstitial condensation of a layered wall (the method ISO 13788 describes): layer temperatures by series resistance, the
/// vapour-pressure profile by the tangent construction against the Magnus saturation line, the monthly accumulation carried forward over
/// twelve months from the first month of condensation, the annual balance, the peak against the user's limit, and the surface
/// temperature factor against its minimum at the critical surface humidity. No climate or material data of any standard ships:
/// the monthly climate, the humidity class, the layers and the limits are the user's records.
/// </summary>
public sealed class GlaserCalculator : ICalcCalculator
{
    public const string Id = "bs.glaser";
    public const string ConstantRh = "constant internal relative humidity";
    public const string MoistureExcess = "moisture excess from a humidity class (linear in the outside temperature)";
    public const int Months = 12;

    /// <summary>The relative difference between this wall's U-value and the heat-loss calculation's above which the sheet warns (the layers are typed twice, so rounding is expected, not a different build-up).</summary>
    public const double UrefTolerance = 0.02;

    /// <summary>The internal surface resistance (m²K/W) conventional for the surface-condensation check of an opaque element.</summary>
    public const double ConventionalRsiF = 0.25;

    private static readonly IReadOnlyList<CalcColumnSpec> LayerColumns =
    [
        new("element", "Element (blank = every row belongs to the wall; matches the elements of the room heat loss layers table)", CalcInputKind.Text, IsOptional: true),
        new("name", "Layer (inside first)", CalcInputKind.Text, IsOptional: true),
        new("d", "Thickness", CalcInputKind.Quantity, "mm", Min: 1, Max: 500, HardMin: 0.01, HardMax: 2000),
        new("lambda", "Thermal conductivity λ", CalcInputKind.Quantity, "W/(m*K)", Min: 0.01, Max: 5, HardMin: 0.005, HardMax: 10),
        new("mu", "Vapour resistance factor μ", CalcInputKind.Number, Min: 1, Max: 1e5, HardMin: 1, HardMax: 1e8),
        new("timber", "Timber or breathable layer (hygroscopic, capillary transport: raises the EN 15026 warning)", CalcInputKind.Boolean, IsOptional: true),
    ];

    private static readonly IReadOnlyList<CalcColumnSpec> ClimateColumns =
    [
        new("month", "Month", CalcInputKind.Text, IsOptional: true),
        new("theta_e", "Mean outside temperature θe", CalcInputKind.Quantity, "degC", Min: -30, Max: 40, HardMin: -60, HardMax: 60),
        new("RH_e", "Mean outside relative humidity RHe", CalcInputKind.Quantity, "%", Min: 20, Max: 100, HardMin: 0, HardMax: 100),
        new("days", "Days in the month (blank = calendar days, rows in January to December order)", CalcInputKind.Quantity, "d", Min: 28, Max: 31, HardMin: 1, HardMax: 366, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.4.0", CalcDiscipline.BuildingServices, "Glaser condensation (interstitial and surface)",
        "Calculates interstitial condensation in a layered wall by the Glaser method over twelve monthly climate steps. It gives the condensate on each interface, the annual balance, the peak against the limit and the surface temperature factor.",
        "Layer resistance and diffusion thickness: R_j = d_j/λ_j; s_d,j = μ_j·d_j\n" +
        "Total resistance, U-value and interface temperature: R_T = R_si + Σ R_j + R_se; U = 1/R_T; T_k = T_int − (T_int − T_e)·(R_si + Σ_(j≤k) R_j)/R_T\n" +
        "Saturation pressure (Magnus): p_sat = 610.5·exp(17.269·T/(237.3 + T)) over water (T ≥ 0 °C); p_sat = 610.5·exp(21.875·T/(265.5 + T)) over ice (T < 0 °C)\n" +
        "Vapour pressures: p_e = RH_e·p_sat(T_e); p_i = RH_i·p_sat(T_int), or p_e + f_Δp·Δv(T_e)·R_v·(T_i + T_e)/2 in the moisture-excess mode with R_v = 461.5 J/(kg·K)\n" +
        "Vapour flux and condensate: g = δ0·Δp/Δs_d with δ0 = 2×10⁻¹⁰ kg/(m·s·Pa); g_c = g_in − g_out on the tangent-construction profile; Mc accumulates g_c·t over 12 months, a wet interface drying at g_c < 0\n" +
        "Refinement check: the same year with every layer split into 20 equal sub-layers of the same λ and μ; the annual balance passes only when it passes both ways\n" +
        "Annual balance and peak: PASS when no condensate remains after 12 months; Mc_max = the most held on any interface, checked against Mc_limit\n" +
        "Surface factor: f_Rsi = 1 − R_si,f/R_T,f; f_Rsi,min = the largest monthly (T_si,min − T_e)/(T_int − T_e) with p_sat(T_si,min) = p_i/φ_crit; PASS when f_Rsi ≥ f_Rsi,min",
        [
            new CalcInputSpec("layers", "Wall layers from the inside outwards (thickness d, conductivity λ and vapour resistance factor μ)", CalcInputKind.Table, Columns: LayerColumns, MaxRows: GlaserModel.MaxLayers, Group: "Setup"),
            new CalcInputSpec("climate", "Twelve monthly outside conditions (the user's climate record, in the order the year is run)", CalcInputKind.Table, Columns: ClimateColumns, MaxRows: Months, Group: "Setup"),
            new CalcInputSpec("humidity_mode", "Internal humidity from", CalcInputKind.Choice, Choices: [ConstantRh, MoistureExcess], DefaultChoice: ConstantRh, Group: "Setup"),
            new CalcInputSpec("breathable", "The build-up is timber or breathable (hygroscopic layers, capillary transport)", CalcInputKind.Boolean, Group: "Setup"),
            new CalcInputSpec("element", "Element whose rows of the layers table to use (optional; blank = all rows)", CalcInputKind.Text, IsOptional: true, Group: "Setup"),
            new CalcInputSpec("T_int", "Internal air temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: 10, Max: 30, HardMin: 0, HardMax: 40, Group: "Conditions", Aliases: ["theta_int"]),
            new CalcInputSpec("RH_int", "Internal relative humidity (constant-humidity mode)", CalcInputKind.Quantity, "%", Default: 50, Min: 20, Max: 90, HardMin: 1, HardMax: 100, Group: "Conditions"),
            new CalcInputSpec("dv_a", "Moisture excess Δv at the cold point (humidity-class record)", CalcInputKind.Quantity, "g/m^3", Min: 0, Max: 20, HardMin: 0, HardMax: 50, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("theta_a", "Outside temperature of the cold point (Δv constant below it)", CalcInputKind.Quantity, "degC", Min: -20, Max: 30, HardMin: -60, HardMax: 60, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("dv_b", "Moisture excess Δv at the warm point (humidity-class record)", CalcInputKind.Quantity, "g/m^3", Min: 0, Max: 20, HardMin: 0, HardMax: 50, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("theta_b", "Outside temperature of the warm point (Δv constant above it)", CalcInputKind.Quantity, "degC", Min: -20, Max: 40, HardMin: -60, HardMax: 60, IsOptional: true, Group: "Conditions"),
            new CalcInputSpec("Rsi", "Internal surface resistance R_si", CalcInputKind.Quantity, "m^2*K/W", Default: 0.13, Min: 0.04, Max: 0.25, HardMin: 0.01, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("Rse", "External surface resistance R_se", CalcInputKind.Quantity, "m^2*K/W", Default: 0.04, Min: 0.02, Max: 0.25, HardMin: 0.01, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("Rsi_f", "Internal surface resistance R_si,f for the f_Rsi check (0.25 m²K/W is the conventional value for an opaque element and 0.13 for glazing; a record to check against the owner's copy of ISO 13788)", CalcInputKind.Quantity, "m^2*K/W", Default: 0.25, Min: 0.04, Max: 0.5, HardMin: 0.01, HardMax: 1, Group: "Factors"),
            new CalcInputSpec("dp_f", "Safety factor on the vapour-pressure difference Δp = Δv·R_v·(T_i + T_e)/2 in the moisture-excess mode (1.10 by convention; a record, wording to be checked against the owner's copy of ISO 13788)", CalcInputKind.Number, Default: 1.10, Min: 1, Max: 1.5, HardMin: 1, HardMax: 2, Group: "Factors"),
            new CalcInputSpec("RH_crit", "Critical surface relative humidity", CalcInputKind.Quantity, "%", Default: 80, Min: 50, Max: 100, HardMin: 30, HardMax: 100, Group: "Requirements"),
            new CalcInputSpec("Mc_limit", "Limit on the condensate on any interface (optional; blank = report only)", CalcInputKind.Quantity, "g/m^2", Min: 1, Max: 5000, HardMin: 0.001, HardMax: 1e6, IsOptional: true, Group: "Requirements"),
            new CalcInputSpec("U_ref", "U-value of this wall from the room heat loss calculation, for the consistency check against 1/R_T (optional; blank = not checked)", CalcInputKind.Quantity, "W/(m^2*K)", Min: 0.05, Max: 8, HardMin: 0.01, HardMax: 10, IsOptional: true, Group: "Requirements"),
            new CalcInputSpec("diagram_month", "Month to draw the Glaser diagram for (optional; blank = the month of the peak, or the coldest if nothing condenses)", CalcInputKind.Number, Min: 1, Max: 12, HardMin: 0.5, HardMax: 12.5, IsOptional: true, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Vapour diffuses one-dimensionally through homogeneous layers in series, in steady state each month.", "The Glaser method."),
            new CalcAssumption("Each month uses its mean climate, with no air movement, liquid transport, moisture storage, or solar or rain gain.", "The Glaser method."),
            new CalcAssumption("Material properties λ and μ are constants of each layer.", "The Glaser method."),
            new CalcAssumption("Condensate does not change the properties of a layer.", "The Glaser method."),
            new CalcAssumption("Condensation is checked at the interfaces between layers.", "The Glaser method."),
            new CalcAssumption("The saturation line is straight between interface values, which can miss condensate inside a layer.", "Stated limit; whether ISO 13788 requires sub-division is to be checked against the owner's copy."),
            new CalcAssumption("The annual balance is also run with every layer split into 20 sub-layers and passes only when both runs pass.", "Refinement check."),
            new CalcAssumption("The limit on the condensate per interface is checked on the interfaces as entered.", "Stated limit."),
            new CalcAssumption("The Magnus saturation pressure is used over water at and above 0 °C and over ice below.", "Derived."),
            new CalcAssumption("The water-vapour permeability of air is δ0 = 2×10⁻¹⁰ kg/(m·s·Pa).", "Derived; closed-form physics, not data of a standard."),
            new CalcAssumption("A blank month length is the calendar days of the month, with the climate rows taken as January to December.", "Calendar months, February 28 days; enter the days where the record starts in another month."),
            new CalcAssumption("A wet interface that dries within a month is dry from the next month.", "Monthly steps."),
            new CalcAssumption("The rates of a month are those of the wet state at its start.", "Monthly steps."),
            new CalcAssumption("The climate, humidity-class points, surface resistances, critical humidity and condensate limit are the user's own records.", "No table from a standard is shipped."),
        ],
        [
            new CalcClauseRef("BS EN ISO 13788:2012", "cl. 5 (clause number to be checked against the owner's copy)", "Hygrothermal performance of building components and elements — internal surface temperature and surface condensation"),
            new CalcClauseRef("BS EN ISO 13788:2012", "cl. 6 (clause number to be checked against the owner's copy)", "Hygrothermal performance of building components and elements — interstitial condensation (Glaser method)"),
            new CalcClauseRef("BS EN ISO 13788:2012", "—", "Internal humidity from the humidity class, moisture excess and its allowance (clause to be checked)"),
            new CalcClauseRef("BS EN 15026:2007", "—", "Assessment of moisture transfer by numerical simulation (the whole standard, for timber and breathable builds)"),
        ],
        [
            new CalcApplicabilityRule("layers", "Enter the layers (inside first), each with thickness, conductivity and μ; at most 30.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("element", "The element named must have rows in the layers table.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("climate", "The climate record has exactly twelve monthly rows, each with a temperature and a humidity.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("humidity-class", "The moisture-excess mode needs both humidity-class points, the cold point below the warm one.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("element-filter", "Rows of the layers table that belong to another element are ignored.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("layer-subdivision", "The condensate left after 12 months, or the wall peak, changes (a verdict flips, or by more than 10 %) when every layer is split into 20 sub-layers: condensate forms inside a layer. The annual balance must pass both ways.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("single-layer", "A single layer has no interface: no interstitial condensation can be found.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("p-int-saturated", "The internal vapour pressure from the humidity class exceeds saturation at the internal temperature in some month and is limited to it.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("fRsi-not-applicable", "No month has the outside air at least 1 K colder than the inside: f_Rsi,min is taken as 0.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("rsi-f-low", "The internal surface resistance for the surface check is below 0.25 m²K/W, the conventional value for an opaque element (0.13 is for glazing).", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("u-ref-mismatch", "The U-value 1/R_T of the layers differs by more than 2 % from the U-value given by the heat-loss calculation: the two describe different constructions or surface resistances.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("en15026", "A timber or breathable build-up (the flag, or any layer flagged timber or breathable) is outside the Glaser method: use EN 15026 hygrothermal simulation.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
        ],
        [
            new CalcTestCase("ka-seasonal-drying", "The same wall in an invented maritime year: condensation from November, dry again in June",
                new CalcReferenceSource("Analytic (the single-interface closed form, month by month, calendar days)", null, "Invented monthly climate θe = 2, 3, 6, 9, 13, 16, 18, 18, 15, 11, 6, 3 °C and RHe = 90, 88, 84, 80, 76, 74, 74, 76, 82, 87, 90, 91 % (not ISO 13788 data); month lengths blank, so the calendar days (January to December, February 28). Accumulation starts in November (row 11): Nov 314.262, Dec 1115.130, Jan 2055.496, Feb 2773.363, Mar 3083.081 (peak), Apr 2828.660, May 1648.023, Jun 0 (dried; the drying is limited to what is there); the balance PASSES; the peak exceeds a 3000 g/m² limit"),
                [WallInsulatedBrick(), SeasonalClimate(), Q("T_int", 20, "degC"), Q("RH_int", 50, "%"), Q("Mc_limit", 3000, "g/m^2")],
                [new CalcExpectedValue("n_start", 11, "", 1e-9), new CalcExpectedValue("Mc_11", 314.26204088410316, "g/m^2", 1e-9), new CalcExpectedValue("Mc_12", 1115.1297163923496, "g/m^2", 1e-9), new CalcExpectedValue("Mc_1", 2055.4959133011744, "g/m^2", 1e-9), new CalcExpectedValue("Mc_2", 2773.36347024196, "g/m^2", 1e-9), new CalcExpectedValue("Mc_3", 3083.081081674129, "g/m^2", 1e-9), new CalcExpectedValue("Mc_4", 2828.6599162337693, "g/m^2", 1e-9), new CalcExpectedValue("Mc_5", 1648.0232742406943, "g/m^2", 1e-9), new CalcExpectedValue("Mc_6", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 3083.081081674129, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 0, "g/m^2", 1e-9), new CalcExpectedValue("u_Mc", 1.0276936938913763, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-psat-20", "Magnus saturation pressure at 20 °C, and a wall with one interface that never condenses",
                new CalcReferenceSource("Analytic (Magnus form, the brief's coefficients)", null, "p_sat(20 °C) = 610.5·exp(17.269·20/(237.3 + 20)) = 2336.951 Pa, the 2337 Pa of the brief to its rounding; a 15 mm plaster and 215 mm brick wall (invented μ 10 and 8, λ 0.5 and 0.8) in the invented seasonal climate at 40 % internal humidity: R_T = 0.46875 m²K/W, S_d = 1.87 m, no month condenses; the surface check with the conventional R_si,f = 0.25 gives f_Rsi = 0.575372 against f_Rsi,min = 0.403835 (January, p_i/0.8 = 1168.476 Pa) and passes"),
                [WallVapourOpen(), SeasonalClimate(), Q("T_int", 20, "degC"), Q("RH_int", 40, "%")],
                [new CalcExpectedValue("psat_i", 2336.951143802342, "Pa", 1e-9), new CalcExpectedValue("fRsi", 0.5753715498938429, "", 1e-9), new CalcExpectedValue("fRsi_min", 0.4038351769856001, "", 1e-9), new CalcExpectedValue("R_T", 0.46875, "m^2*K/W", 1e-9), new CalcExpectedValue("S_d", 1.87, "m", 1e-9), new CalcExpectedValue("Mc_max", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 0, "g/m^2", 1e-9), new CalcExpectedValue("n_start", 0, "", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
            new CalcTestCase("ka-single-interface-all-year", "One interface condensing every month of a constant 0 °C / 90 % climate",
                new CalcReferenceSource("Analytic (series resistance, Magnus, g = δ0·Δp/Δs_d)", null, "100 mm insulation (λ 0.04, μ 2) inside 200 mm brick (λ 0.8, μ 10), R_si 0.13, R_se 0.04: R_T = 2.92 m²K/W; θ at the interface 1.98630 °C, p_sat 704.598 Pa; p_i 1168.476, p_e 549.450 Pa; g_c = δ0·[(p_e − p_sat)/(2.0) − (p_sat − p_i)/(0.2)] = 4.48363×10⁻⁷ kg/(m²·s) = 38.7385 g/m² per day; January (31 days) gives 1200.895 g/m², with February (28 days) 2285.575; nothing dries, so the 365 days of twelve months give 14139.573 g/m² and the annual balance FAILS"),
                [WallInsulatedBrick(), ConstantClimate(0, 90), Q("T_int", 20, "degC"), Q("RH_int", 50, "%"), Q("Mc_limit", 20000, "g/m^2")],
                [new CalcExpectedValue("R_T", 2.92, "m^2*K/W", 1e-9), new CalcExpectedValue("S_d", 2.2, "m", 1e-9), new CalcExpectedValue("n_start", 1, "", 1e-9), new CalcExpectedValue("Mc_1", 1200.8952446441, "g/m^2", 1e-9), new CalcExpectedValue("Mc_2", 2285.5748204517, "g/m^2", 1e-9), new CalcExpectedValue("Mc_12", 14139.573041777, "g/m^2", 1e-9), new CalcExpectedValue("Mc_if_1", 14139.573041777, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 14139.573041777, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 14139.573041777, "g/m^2", 1e-9), new CalcExpectedValue("u_Mc", 0.70697865208885, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-two-planes-tangent", "Five layers with two condensation planes: the tangent construction touches the saturation line twice",
                new CalcReferenceSource("Analytic (tangent construction, checked against an independent obstacle-problem solve in python3)", null, "12.5 mm board (λ 0.25, μ 8), 140 mm insulation (0.04, 1), 100 mm brick (0.8, 10), 50 mm insulation (0.04, 1), 3 mm render (0.9, 150) at 20 °C / 50 % inside, 0 °C / 90 % outside every month: R_T = 5.098333 m²K/W, S_d = 1.74 m; the profile rests on the saturation line at interfaces 2 and 4 only (906.8 and 618.1 Pa): per day 14.0908 g/m² on interface 2 and 2.1151 g/m² on interface 4, nothing on interfaces 1 and 3; January (31 days) 502.383 g/m² in all, the 365 days of twelve months give 5143.147 and 772.008 g/m²"),
                [WallTwoPlanes(), ConstantClimate(0, 90), Q("T_int", 20, "degC"), Q("RH_int", 50, "%")],
                [new CalcExpectedValue("R_T", 5.098333333333334, "m^2*K/W", 1e-9), new CalcExpectedValue("S_d", 1.74, "m", 1e-9), new CalcExpectedValue("Mc_if_1", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_if_2", 5143.147099105272, "g/m^2", 1e-9), new CalcExpectedValue("Mc_if_3", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_if_4", 772.0081436491156, "g/m^2", 1e-9), new CalcExpectedValue("Mc_1", 502.3830480147563, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 5143.147099105272, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 5915.155242754388, "g/m^2", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-surface-factor", "f_Rsi against f_Rsi,min: R_si 0.25 for the surface check, critical humidity 80 %",
                new CalcReferenceSource("Analytic (f_Rsi = 1 − R_si/R_T; inverse Magnus for θ_si,min)", null, "The plaster and brick wall with R_si,f = 0.25: R_T,f = 0.25 + 0.03 + 0.26875 + 0.04 = 0.58875, f_Rsi = 1 − 0.25/0.58875 = 0.575372; p_i = 0.5·2336.951 = 1168.476 Pa, p_i/0.8 = 1460.594 Pa, θ_si,min = 12.9098 °C; in the coldest month (2 °C) f_Rsi,min = (12.9098 − 2)/(20 − 2) = 0.590256 > f_Rsi, so the surface check FAILS (utilisation 1.025869) though nothing condenses inside the wall"),
                [WallVapourOpen(), SeasonalClimate(), Q("T_int", 20, "degC"), Q("RH_int", 50, "%"), Q("Rsi_f", 0.25, "m^2*K/W")],
                [new CalcExpectedValue("R_Tf", 0.58875, "m^2*K/W", 1e-9), new CalcExpectedValue("fRsi", 0.5753715498938429, "", 1e-9), new CalcExpectedValue("fRsi_min", 0.5902560168111524, "", 1e-9), new CalcExpectedValue("n_fcrit", 1, "", 1e-9), new CalcExpectedValue("u_f", 1.0258693133507482, "", 1e-9), new CalcExpectedValue("Mc_max", 0, "g/m^2", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-u-ref-cross-check", "Wall U-value 1/R_T and the cross-check against the heat-loss calculation's U-value",
                new CalcReferenceSource("Analytic (U = 1/R_T; |U − U_ref|/U_ref)", null, "The plaster and brick wall: R_T = 0.13 + 0.03 + 0.26875 + 0.04 = 0.46875 m²K/W, U = 1/0.46875 = 2.133333 W/m²K; against U_ref = 2.2 W/m²K the difference is |2.133333 − 2.2|/2.2 = 3.0303 %, above the 2 % tolerance, so the sheet warns (the warning is not a check: nothing else changes)"),
                [WallVapourOpen(), SeasonalClimate(), Q("T_int", 20, "degC"), Q("RH_int", 40, "%"), Q("U_ref", 2.2, "W/(m^2*K)")],
                [new CalcExpectedValue("U_wall", 2.1333333333333333, "W/(m^2*K)", 1e-9), new CalcExpectedValue("dU_ref", 0.030303030303030304, "", 1e-9), new CalcExpectedValue("R_T", 0.46875, "m^2*K/W", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
            new CalcTestCase("ka-humidity-class", "Moisture excess from two user points: Δv = 5 g/m³ at or below 0 °C, falling linearly to 0 at 20 °C, with the 1.10 margin on Δp",
                new CalcReferenceSource("Analytic (p_i = p_e + f_Δp·Δv·R_v·(T_i + T_e)/2, f_Δp = 1.10, R_v = 461.5 J/(kg·K); single-interface closed form; clause wording to be checked against the owner's copy of ISO 13788)", null, "The insulated brick wall in the invented seasonal climate with p_i = RH_e·p_sat(θ_e) + 1.10·Δv(θ_e)·461.5·(293.15 + T_e)/2 (January: Δv 4.5 g/m³, Δp = 1.10·4.5e-3·461.5·284.15 = 649.1 Pa); condensation starts in October (row 10), the peak is 5111.052 g/m² in April, 91.744 g/m² is still on the wall after twelve months (September) so the balance FAILS; f_Rsi,min = 0.670474 in January (θ_si,min from p_i/0.8)"),
                [WallInsulatedBrick(), SeasonalClimate(), Q("T_int", 20, "degC"), Choice("humidity_mode", MoistureExcess), Q("dv_a", 5, "g/m^3"), Q("theta_a", 0, "degC"), Q("dv_b", 0, "g/m^3"), Q("theta_b", 20, "degC"), CalcInputValue.Of("dp_f", 1.10)],
                [new CalcExpectedValue("n_start", 10, "", 1e-9), new CalcExpectedValue("Mc_10", 144.81298290481683, "g/m^2", 1e-9), new CalcExpectedValue("Mc_4", 5111.051756092058, "g/m^2", 1e-9), new CalcExpectedValue("Mc_8", 645.9625409543693, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 5111.051756092058, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 91.74381353425309, "g/m^2", 1e-9), new CalcExpectedValue("fRsi_min", 0.6704737425044534, "", 1e-9), new CalcExpectedValue("n_fcrit", 1, "", 1e-9), new CalcExpectedValue("pass", 0, "", 1e-9)]),
            new CalcTestCase("ka-humidity-class-no-margin", "The same moisture excess with no margin on Δp (f_Δp = 1): the mean-temperature formula alone",
                new CalcReferenceSource("Analytic (p_i = p_e + Δv·R_v·(T_i + T_e)/2; single-interface closed form)", null, "As the previous case with f_Δp = 1.00 (January Δp = 4.5e-3·461.5·284.15 = 590.1 Pa): condensation starts in October (row 10), the peak is 4249.892 g/m² in April, the wall is dry again by August and the balance PASSES; f_Rsi,min = 0.630284 in January"),
                [WallInsulatedBrick(), SeasonalClimate(), Q("T_int", 20, "degC"), Choice("humidity_mode", MoistureExcess), Q("dv_a", 5, "g/m^3"), Q("theta_a", 0, "degC"), Q("dv_b", 0, "g/m^3"), Q("theta_b", 20, "degC"), CalcInputValue.Of("dp_f", 1.0)],
                [new CalcExpectedValue("n_start", 10, "", 1e-9), new CalcExpectedValue("Mc_10", 64.53411829081696, "g/m^2", 1e-9), new CalcExpectedValue("Mc_4", 4249.892047228058, "g/m^2", 1e-9), new CalcExpectedValue("Mc_8", 0, "g/m^2", 1e-9), new CalcExpectedValue("Mc_max", 4249.892047228058, "g/m^2", 1e-9), new CalcExpectedValue("Mc_end", 0, "g/m^2", 1e-9), new CalcExpectedValue("fRsi_min", 0.6302837752670817, "", 1e-9), new CalcExpectedValue("n_fcrit", 1, "", 1e-9), new CalcExpectedValue("pass", 1, "", 1e-9)]),
        ],
        Category: "Thermal",
        OutputPorts:
        [
            new CalcOutputPort("Mc_max", "g/m^2", "Most condensate held on any interface in the year", "condensation.mass_interface_max"),
            new CalcOutputPort("fRsi", "", "Temperature factor of the internal surface", "condensation.factor_temperature_surface"),
            new CalcOutputPort("pass", "", "1 when every check passes (annual balance, peak against the limit, surface factor), else 0", "condensation.checks_passed"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("material.conductivity", "Layer conductivity and vapour resistance", ["layers"], "The layers table: thickness, λ from the manufacturer's declared value or your own licensed reference, and μ from the manufacturer's data or your copy of the material standard."),
            new CalcRecordKind("surface.resistance", "Surface resistances", ["Rsi", "Rse", "Rsi_f"], "R_si and R_se from your copy of the surface-resistance standard, with the R_si,f of the surface-condensation check: 0.25 m²K/W is the conventional value for an opaque element (0.13 for glazing); the sheet defaults to 0.25 and warns below it."),
            new CalcRecordKind("climate.monthly", "Monthly outside climate", ["climate"], "Twelve monthly mean temperatures and relative humidities from your own licensed climate source for the site, with its edition; no climate data is shipped."),
            new CalcRecordKind("climate.humidity-class", "Internal humidity class", ["dv_a", "theta_a", "dv_b", "theta_b", "dp_f"], "The moisture excess at two outside temperatures from your copy of the humidity-class method, and the safety factor on the pressure difference (default 1.10; check the wording against your copy)."),
            new CalcRecordKind("criteria.condensation", "Condensation criteria", ["RH_crit", "Mc_limit"], "The critical surface humidity and the limit on the condensate per interface from your own source for the materials used."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue Choice(string symbol, string choice) => CalcInputValue.Choice(symbol, choice);

    /// <summary>A layers table, inside first, from (element, layer, d mm, λ W/(m·K), μ) rows.</summary>
    public static CalcInputValue Layers(params (string Element, string Name, double D, double Lambda, double Mu)[] rows) =>
        CalcInputValue.Table("layers", rows.Select(r =>
        {
            var cells = new List<(string, CalcCell)>();

            if (r.Element.Length > 0)
                cells.Add(("element", new CalcCell(Text: r.Element)));

            cells.Add(("name", new CalcCell(Text: r.Name)));
            cells.Add(("d", new CalcCell(r.D, "mm")));
            cells.Add(("lambda", new CalcCell(r.Lambda, "W/(m*K)")));
            cells.Add(("mu", new CalcCell(r.Mu)));
            return CalcRow.Of([.. cells]);
        }).ToList());

    /// <summary>A twelve-month climate table from (θe °C, RHe %) rows, January first.</summary>
    public static CalcInputValue Climate(params (double ThetaE, double Rh)[] months) =>
        CalcInputValue.Table("climate", months.Select((m, i) => CalcRow.Of(("month", new CalcCell(Text: $"M{i + 1}")), ("theta_e", new CalcCell(m.ThetaE, "degC")), ("RH_e", new CalcCell(m.Rh, "%")))).ToList());

    /// <summary>A twelve-month climate table from (θe °C, RHe %, days) rows, the first row first.</summary>
    public static CalcInputValue Climate(params (double ThetaE, double Rh, double Days)[] months) =>
        CalcInputValue.Table("climate", months.Select((m, i) => CalcRow.Of(("month", new CalcCell(Text: $"M{i + 1}")), ("theta_e", new CalcCell(m.ThetaE, "degC")), ("RH_e", new CalcCell(m.Rh, "%")), ("days", new CalcCell(m.Days, "d")))).ToList());

    private static CalcInputValue ConstantClimate(double thetaE, double rh) => Climate([.. Enumerable.Repeat((thetaE, rh), Months)]);

    /// <summary>An invented maritime year (not ISO 13788 climate data) used by the known-answer cases.</summary>
    private static CalcInputValue SeasonalClimate() => Climate((2, 90), (3, 88), (6, 84), (9, 80), (13, 76), (16, 74), (18, 74), (18, 76), (15, 82), (11, 87), (6, 90), (3, 91));

    private static CalcInputValue WallInsulatedBrick() => Layers(("", "Insulation", 100, 0.04, 2), ("", "Brick", 200, 0.8, 10));

    private static CalcInputValue WallVapourOpen() => Layers(("", "Plaster", 15, 0.5, 10), ("", "Brick", 215, 0.8, 8));

    private static CalcInputValue WallTwoPlanes() => Layers(("", "Board", 12.5, 0.25, 8), ("", "Insulation", 140, 0.04, 1), ("", "Brick", 100, 0.8, 10), ("", "Insulation", 50, 0.04, 1), ("", "Render", 3, 0.9, 150));

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_Mc", "u_f");
        c.MarkAtLeast("fRsi");
        var layerRows = c.Rows("layers");

        if (layerRows.Count == 0)
        {
            c.Block("layers", c.EnterRows("layers"));
            return;
        }

        var climateRows = c.Rows("climate");

        if (climateRows.Count == 0)
        {
            c.Block("climate", c.EnterRows("climate"));
            return;
        }

        if (climateRows.Count != Months)
        {
            c.Block("climate", $"climate: enter exactly {Months} monthly rows, in the order the year is to be run. {climateRows.Count} rows are given.");
            return;
        }

        var element = c.Has("element") ? Clean(c.InputText("element")) : string.Empty;
        var rows = layerRows.Where(r => element.Length == 0 || string.Equals(Clean(r.Get("element")?.Text), element, StringComparison.OrdinalIgnoreCase)).ToList();

        if (rows.Count == 0)
        {
            c.Block("element", $"element: no row of the layers table belongs to the element '{element}'. Enter the layers of that element or clear the element name.");
            return;
        }

        if (rows.Count < layerRows.Count)
            c.Warn("element-filter", $"layers: {layerRows.Count - rows.Count} row(s) of the layers table belong to another element than '{element}' and are ignored.");

        var layers = new List<GlaserLayer>();
        var names = new List<string>();
        var timberLayers = new List<string>();

        for (var i = 0; i < rows.Count; i++)
        {
            var name = Clean(rows[i].Get("name")?.Text) is { Length: > 0 } text ? text : $"layer {i + 1}";
            var d = Si(rows[i], "d", "mm");
            var lambda = Si(rows[i], "lambda", "W/(m*K)");
            var mu = rows[i].Get("mu")?.Number;

            if (d is null || lambda is null || mu is null)
            {
                c.Block("layers", $"layers: row {i + 1} ('{name}') has a missing value. Enter the thickness d, the conductivity lambda and the factor mu.");
                return;
            }

            if (!(d > 0) || !(lambda > 0) || !(mu >= 1) || !double.IsFinite(d.Value) || !double.IsFinite(lambda.Value) || !double.IsFinite(mu.Value))
            {
                c.Block("layers", $"layers: row {i + 1} ('{name}') needs a positive thickness and conductivity and a vapour resistance factor of at least 1.");
                return;
            }

            layers.Add(new GlaserLayer(d.Value, lambda.Value, mu.Value));
            names.Add(name);

            if (rows[i].Get("timber")?.Flag == true)
                timberLayers.Add(name);
        }

        if (layers.Count > GlaserModel.MaxLayers)
        {
            c.Block("layers", $"layers: enter at most {GlaserModel.MaxLayers} layers. {layers.Count} layers are given.");
            return;
        }

        foreach (var symbol in new[] { "T_int", "Rsi", "Rse", "Rsi_f", "RH_crit" })
            c.Input(symbol);

        var rsi = c.Si("Rsi");
        var rse = c.Si("Rse");
        var thetaInt = c.Si("T_int") - 273.15;
        var crit = c.Si("RH_crit");
        var excess = c.InputText("humidity_mode") == MoistureExcess;
        double dvA = 0, dvB = 0, thetaA = 0, thetaB = 0, rhInt = 0, dpFactor = 1;

        if (excess)
        {
            foreach (var (symbol, what) in new[] { ("dv_a", "the moisture excess at the cold point"), ("theta_a", "the outside temperature of the cold point"), ("dv_b", "the moisture excess at the warm point"), ("theta_b", "the outside temperature of the warm point") })
            {
                if (!c.Has(symbol))
                {
                    c.Block("humidity-class", $"{symbol}: enter {what} from the humidity-class record. The moisture-excess mode needs both points.");
                    return;
                }

                c.Input(symbol);
            }

            c.Input("dp_f");
            dpFactor = c.Si("dp_f");
            dvA = c.Si("dv_a");
            dvB = c.Si("dv_b");
            thetaA = c.Si("theta_a") - 273.15;
            thetaB = c.Si("theta_b") - 273.15;

            if (!(thetaB > thetaA))
            {
                c.Block("humidity-class", "theta_b: the warm point must be at a higher outside temperature than the cold point theta_a. Enter the two points in order.");
                return;
            }
        }
        else
        {
            c.Input("RH_int");
            rhInt = c.Si("RH_int");
        }

        var thetaE = new double[Months];
        var rhE = new double[Months];
        var seconds = new double[Months];
        var labels = new string[Months];
        var calendarRows = 0;

        for (var m = 0; m < Months; m++)
        {
            var row = climateRows[m];
            var t = Celsius(row, "theta_e");
            var rh = Fraction(row, "RH_e");

            if (t is null || rh is null || !double.IsFinite(t.Value) || !double.IsFinite(rh.Value) || rh < 0 || rh > 1)
            {
                c.Block("climate", $"climate: row {m + 1} has a missing value. Enter the outside temperature theta_e and a relative humidity RH_e from 0 to 100 %.");
                return;
            }

            thetaE[m] = t.Value;
            rhE[m] = rh.Value;

            if (Si(row, "days", "d") is { } len && len > 0 && double.IsFinite(len))
            {
                seconds[m] = len;
            }
            else
            {
                seconds[m] = GlaserModel.CalendarMonthSeconds(m);
                calendarRows++;
            }

            labels[m] = Clean(row.Get("month")?.Text) is { Length: > 0 } label ? label : $"month {m + 1}";
        }

        var pExt = new double[Months];
        var pInt = new double[Months];
        var satInt = GlaserModel.SaturationPressure(thetaInt);
        var capped = new List<int>();

        for (var m = 0; m < Months; m++)
        {
            pExt[m] = rhE[m] * GlaserModel.SaturationPressure(thetaE[m]);

            if (excess)
            {
                var dv = thetaE[m] <= thetaA ? dvA : thetaE[m] >= thetaB ? dvB : dvA + (dvB - dvA) * (thetaE[m] - thetaA) / (thetaB - thetaA);
                var p = pExt[m] + dpFactor * dv * GlaserModel.VapourGasConstant * ((thetaInt + 273.15) + (thetaE[m] + 273.15)) / 2.0;

                if (p > satInt)
                {
                    capped.Add(m + 1);
                    p = satInt;
                }

                pInt[m] = p;
            }
            else
            {
                pInt[m] = rhInt * satInt;
            }
        }

        if (excess)
            c.Assume($"Moisture-excess mode: the internal vapour pressure is p_i = p_e + f_Δp·Δv·R_v·(T_i + T_e)/2 with the safety factor f_Δp = {F(dpFactor)} on the pressure difference, in both the interstitial and the surface check. The wording of the humidity-class clause of ISO 13788 (the mean temperature and the margin) must be checked against the owner's copy; the margin is a record, not a built-in value.", "Humidity-class method; clause wording to be checked.");

        if (calendarRows > 0)
            c.Assume($"Month length: {calendarRows} row(s) of the climate gave no days, so the calendar days of the month are used with the rows taken as January to December (February 28 days). If the record starts in another month, enter the days of each row.", "Calendar months.");

        if (capped.Count > 0)
            c.Warn("p-int-saturated", $"humidity_mode: the humidity class gives an internal vapour pressure above saturation ({GlaserModel.SaturationPressure(thetaInt):0.#} Pa) in month(s) {string.Join(", ", capped)}. It is limited to saturation (100 % RH).");

        if (layers.Count == 1)
            c.Warn("single-layer", "layers: a single layer has no interface, so only the surface factor is checked. Split the wall into its layers.");

        var year = GlaserModel.Simulate(layers, rsi, rse, thetaInt, thetaE, pInt, pExt, seconds);
        var n = layers.Count;

        // Refinement check (bs.glaser-01): the same year with every layer in RefineSlices sub-layers. The interface-only model can miss
        // condensate that forms inside a layer, so its verdict depends on how the wall was cut.
        var fine = GlaserModel.Simulate(GlaserModel.Refine(layers), rsi, rse, thetaInt, thetaE, pInt, pExt, seconds);

        // Surface factor.
        var rsiF = c.Si("Rsi_f");

        if (rsiF < ConventionalRsiF * (1 - 1e-9))
            c.Warn("rsi-f-low", $"Rsi_f: {F(rsiF)} m²K/W is below the conventional 0.25 m²K/W of an opaque element, which makes the check less demanding. Check the value against your copy of ISO 13788.");

        var rTotal = GlaserModel.TotalResistance(layers, rsi, rse);
        var rTotalF = GlaserModel.TotalResistance(layers, rsiF, rse);
        var fRsi = 1 - rsiF / rTotalF;
        var fMin = 0.0;
        var fMonth = 0;
        var any = false;

        for (var m = 0; m < Months; m++)
        {
            var dT = thetaInt - thetaE[m];

            if (dT < 1)
                continue;

            var f = (GlaserModel.DewPoint(pInt[m] / crit) - thetaE[m]) / dT;

            if (!any || f > fMin)
            {
                fMin = f;
                fMonth = m + 1;
                any = true;
            }
        }

        if (!any)
            c.Warn("fRsi-not-applicable", "climate: no month has the outside air at least 1 K colder than the inside. The minimum factor f_Rsi,min is taken as 0.");

        fMin = Math.Max(0.0, fMin);

        // Which month to draw.
        var totals = year.Months.Select(x => x.McTotal).ToList();
        int diagram;

        if (c.Has("diagram_month"))
        {
            c.Input("diagram_month");
            diagram = (int)Math.Round(c.Si("diagram_month")) - 1;
        }
        else if (year.StartIndex >= 0)
        {
            diagram = totals.IndexOf(totals.Max());
        }
        else
        {
            diagram = Array.IndexOf(thetaE, thetaE.Min());
        }

        diagram = Math.Clamp(diagram, 0, Months - 1);

        var m2kw = UnitCatalog.Parse("m^2*K/W");
        var meter = UnitCatalog.Parse("m");
        var pascal = UnitCatalog.Parse("Pa");
        var gm2 = UnitCatalog.Parse("g/m^2");
        var degC = UnitCatalog.Parse("degC");
        ScalarValue Gm2(double kgPerM2) => new(kgPerM2, gm2.Dimension, gm2);

        for (var j = 0; j < n; j++)
        {
            c.Record($"R_{j + 1}", "d / λ", $"{names[j]}: {F(layers[j].D * 1000)} mm / {F(layers[j].Lambda)} W/(m·K)", ScalarValue.Of(layers[j].R, m2kw), $"{names[j]}: thermal resistance", "BS EN ISO 13788 cl. 6");
            c.Record($"sd_{j + 1}", "μ · d", $"{names[j]}: {F(layers[j].Mu)} × {F(layers[j].D * 1000)} mm", ScalarValue.Of(layers[j].Sd, meter), $"{names[j]}: diffusion-equivalent air layer thickness", "BS EN ISO 13788 cl. 6");
        }

        c.Record("R_T", "R_si + Σ R_j + R_se", $"{F(rsi)} + {F(layers.Sum(l => l.R))} + {F(rse)}", ScalarValue.Of(rTotal, m2kw), "Total thermal resistance", "BS EN ISO 13788 cl. 6");
        c.Record("U_wall", "1/R_T", $"1 / {F(rTotal)}", ScalarValue.Of(1.0 / rTotal, UnitCatalog.Parse("W/(m^2*K)")), "U-value of the wall from the layers and the surface resistances above", "BS EN ISO 13788 cl. 6");

        if (c.Has("U_ref"))
        {
            c.Input("U_ref");
            var uRef = c.Si("U_ref");
            var dU = Math.Abs(1.0 / rTotal - uRef) / uRef;
            c.Record("dU_ref", "|U − U_ref|/U_ref", $"|{F(1.0 / rTotal)} − {F(uRef)}| / {F(uRef)}", new ScalarValue(dU), "Relative difference between this wall's U-value and the one given by the heat-loss calculation");

            if (dU > UrefTolerance)
                c.Warn("u-ref-mismatch", $"U_ref: the U-value of these layers ({F(1.0 / rTotal)} W/m²K) differs from the heat-loss value ({F(uRef)} W/m²K) by {F(dU * 100)} %. That is more than {UrefTolerance * 100:0} %, so make the layers, R_si and R_se agree.");
        }

        c.Record("S_d", "Σ s_d,j", $"{F(layers.Sum(l => l.Sd))} m", ScalarValue.Of(layers.Sum(l => l.Sd), meter), "Total diffusion-equivalent air layer thickness", "BS EN ISO 13788 cl. 6");
        c.Record("psat_i", "610.5·exp(17.269·θ/(237.3 + θ))", $"θ = {F(thetaInt)} °C", ScalarValue.Of(satInt, pascal), "Saturation vapour pressure at the internal air temperature (Magnus)", "BS EN ISO 13788 cl. 6");
        c.Record("p_ext_m", "RH_e · p_sat(θ_e)", "months 1 … 12 of the climate", VectorValue.Of(pExt, pascal), "Outside vapour pressure by month", "BS EN ISO 13788 cl. 6");
        c.Record("p_int_m", excess ? "p_e + f_Δp·Δv(θ_e)·R_v·(T_i + T_e)/2" : "RH_i · p_sat(θ_i)", excess ? $"f_Δp = {F(dpFactor)}, R_v = 461.5 J/(kg·K), T_i = {F(thetaInt + 273.15)} K, T_e the month's mean" : $"{F(rhInt * 100)} % × {F(satInt)} Pa", VectorValue.Of(pInt, pascal), "Inside vapour pressure by month", "BS EN ISO 13788 cl. 6");

        c.Record("n_start", "first month of condensation", year.StartIndex < 0 ? "no month condenses" : $"{labels[year.StartIndex]} (row {year.StartIndex + 1})", new ScalarValue(year.StartIndex + 1), "Row of the climate in which the accumulation starts (0 when nothing condenses)", "BS EN ISO 13788 cl. 6");

        for (var m = 0; m < Months; m++)
        {
            var previous = year.StartIndex < 0 || m == year.StartIndex ? 0.0 : year.Months[(m + Months - 1) % Months].McTotal;
            c.Record($"Mc_{m + 1}", "Σ Mc over the interfaces", $"{labels[m]}: θ_e = {F(thetaE[m])} °C; {F((year.Months[m].McTotal - previous) * 1000)} g/m² gained (−: dried) this month", Gm2(year.Months[m].McTotal), $"{labels[m]}: condensate on the wall at the end of the month", "BS EN ISO 13788 cl. 6");
        }

        var peaks = year.PeakPerNode;

        for (var k = 1; k < n; k++)
            c.Record($"Mc_if_{k}", "max over the year of Mc", $"between '{names[k - 1]}' and '{names[k]}'", Gm2(peaks[k]), $"Most condensate held on the interface between '{names[k - 1]}' and '{names[k]}'", "BS EN ISO 13788 cl. 6");

        var mcMax = year.PeakInterfaceMc;
        var mcPeakTotal = totals.Count == 0 ? 0 : totals.Max();
        var mcEnd = year.ResidualMc;
        c.Record("Mc_max", "max over interfaces and months of Mc", $"{F(mcMax * 1000)} g/m²", Gm2(mcMax), "Most condensate held on any one interface in the year", "BS EN ISO 13788 cl. 6");
        c.Record("Mc_tot", "max over months of Σ Mc", $"{F(mcPeakTotal * 1000)} g/m²", Gm2(mcPeakTotal), "Most condensate on the whole wall in the year", "BS EN ISO 13788 cl. 6");
        c.Record("Mc_end", "Σ Mc after 12 months", $"{F(mcEnd * 1000)} g/m²", Gm2(mcEnd), "Condensate left on the wall at the end of the 12-month cycle", "BS EN ISO 13788 cl. 6");

        // Glaser diagram data for the chosen month.
        var month = year.Months[diagram];
        var sd = GlaserModel.CumulativeSd(layers);
        var x = new double[n + 1];

        for (var k = 0; k < n; k++)
            x[k + 1] = x[k] + layers[k].D;

        c.Record("n_diag", "month drawn", $"{labels[diagram]} (row {diagram + 1})", new ScalarValue(diagram + 1), "Row of the climate the diagram vectors are for");
        c.Record("sd_if", "Σ s_d to the node", $"{n + 1} nodes, inside surface first", VectorValue.Of(sd, meter), "Diffusion thickness at the surfaces and interfaces", "BS EN ISO 13788 cl. 6");
        c.Record("x_if", "Σ d to the node", $"{n + 1} nodes", VectorValue.Of(x, meter), "Depth from the inside surface at the surfaces and interfaces", "BS EN ISO 13788 cl. 6");
        c.Record("T_if", "θ_i − (θ_i − θ_e)·(R_si + Σ R)/R_T", $"θ_e = {F(month.ThetaE)} °C", VectorValue.Of(month.Theta, degC), "Temperature at the surfaces and interfaces", "BS EN ISO 13788 cl. 6");
        c.Record("psat_if", "610.5·exp(17.269·θ/(237.3 + θ)), ice below 0 °C", $"{n + 1} nodes", VectorValue.Of(month.Psat, pascal), "Saturation vapour pressure at the surfaces and interfaces (the Glaser saturation line)", "BS EN ISO 13788 cl. 6");
        c.Record("p_if", "tangent construction", $"{n + 1} nodes", VectorValue.Of(month.P, pascal), "Vapour pressure at the surfaces and interfaces after the tangent construction", "BS EN ISO 13788 cl. 6");

        c.Series(new CalcSeries("Saturation pressure", "s_d", "m", "p_sat", "Pa", Enumerable.Range(0, n + 1).Select(k => (sd[k], month.Psat[k])).ToList()));
        c.Series(new CalcSeries("Vapour pressure", "s_d", "m", "p", "Pa", Enumerable.Range(0, n + 1).Select(k => (sd[k], month.P[k])).ToList()));
        c.Series(new CalcSeries("Temperature", "x", "mm", "θ", "degC", Enumerable.Range(0, n + 1).Select(k => (x[k] * 1000, month.Theta[k])).ToList()));

        // Surface factor records.
        c.Record("R_Tf", "R_si,f + Σ R_j + R_se", $"{F(rsiF)} + {F(layers.Sum(l => l.R))} + {F(rse)}", ScalarValue.Of(rTotalF, m2kw), "Total thermal resistance with the internal surface resistance of the surface check", "BS EN ISO 13788 cl. 5");

        c.Record("fRsi", "1 − R_si/R_T", $"1 − {F(rsiF)} / {F(rTotalF)}", new ScalarValue(fRsi), "Temperature factor of the internal surface", "BS EN ISO 13788 cl. 5");
        c.Record("fRsi_min", "max over months of (θ_si,min − θ_e)/(θ_i − θ_e)", any ? $"θ_si,min from p_i/φ_crit; governing month: {labels[fMonth - 1]}" : "no month with θ_e at least 1 K below θ_i", new ScalarValue(fMin), "Minimum temperature factor at the critical surface humidity", "BS EN ISO 13788 cl. 5");
        c.Record("n_fcrit", "month that governs f_Rsi,min", any ? $"{labels[fMonth - 1]} (row {fMonth})" : "none", new ScalarValue(fMonth), "Row of the climate that governs the surface check (0 when none applies)", "BS EN ISO 13788 cl. 5");

        var mcEndFine = fine.ResidualMc;
        var mcTotFine = fine.Months.Count == 0 ? 0 : fine.Months.Max(x => x.McTotal);
        c.Record("Mc_end_fine", "Σ Mc after 12 months, each layer in 20 sub-layers", $"{F(mcEndFine * 1000)} g/m²", Gm2(mcEndFine), "Condensate left after 12 months when every layer is split into 20 sub-layers (refinement check)", "BS EN ISO 13788 cl. 6");
        c.Record("Mc_tot_fine", "max over months of Σ Mc, each layer in 20 sub-layers", $"{F(mcTotFine * 1000)} g/m²", Gm2(mcTotFine), "Most condensate on the whole wall when every layer is split into 20 sub-layers (refinement check)", "BS EN ISO 13788 cl. 6");
        var balanceAsEntered = mcEnd <= 1e-9;
        var balanceFine = mcEndFine <= 1e-9;
        var balancePass = balanceAsEntered && balanceFine;
        c.CheckCondition("Condensate left after 12 months against zero", balancePass, $"{F(mcEnd * 1000)} g/m² as entered, {F(mcEndFine * 1000)} g/m² in 20 sub-layers per layer", "0 g/m² (the wall dries out within the year)", "Mc_end", "BS EN ISO 13788 cl. 6");

        var peakDiffers = Math.Abs(mcTotFine - mcPeakTotal) > 0.1 * Math.Max(mcTotFine, mcPeakTotal) && Math.Max(mcTotFine, mcPeakTotal) > 1e-6;
        var endDiffers = Math.Abs(mcEndFine - mcEnd) > 0.1 * Math.Max(mcEndFine, mcEnd) && Math.Max(mcEndFine, mcEnd) > 1e-9;

        if (balanceAsEntered != balanceFine || peakDiffers || endDiffers)
            c.Warn("layer-subdivision", "layers: the result depends on how the wall is cut into layers. Split thick layers into thinner ones, then compare Mc_end with Mc_end_fine.");

        if (c.Has("Mc_limit"))
        {
            c.Input("Mc_limit");
            var limit = c.Si("Mc_limit");
            c.Record("u_Mc", "Mc_max / Mc_limit", $"{F(mcMax * 1000)} g/m² / {F(limit * 1000)} g/m²", new ScalarValue(mcMax / limit), "Utilisation of the limit on the condensate per interface");
            c.Check("Peak condensate on an interface against its limit", "Mc_max", "Mc_limit", "BS EN ISO 13788 cl. 6");
        }
        else
        {
            c.Assume("No limit on the condensate was entered, so Mc_max is reported without a check against a limit.", "Enter Mc_limit from your own source for the materials to check it.");
        }

        c.Record("u_f", "f_Rsi,min / f_Rsi", $"{F(fMin)} / {F(fRsi)}", new ScalarValue(fMin / Math.Max(fRsi, 1e-9)), "Utilisation of the surface temperature factor", "BS EN ISO 13788 cl. 5");
        c.Check($"Minimum surface temperature factor against the factor of the wall at {F(crit * 100)} % critical humidity", "fRsi_min", "fRsi", "BS EN ISO 13788 cl. 5");

        var passed = c.Checks.All(k => k.Passed);
        c.Record("pass", "every check passes", $"{c.Checks.Count} check(s)", new ScalarValue(passed ? 1 : 0), "1 when the annual balance, the limit and the surface factor all pass, else 0");

        if (c.InputFlag("breathable") || timberLayers.Count > 0)
        {
            var which = timberLayers.Count > 0 ? $" (layer(s) flagged timber or breathable: {string.Join(", ", timberLayers)})" : string.Empty;
            c.Warn("en15026", $"{(timberLayers.Count > 0 ? "layers" : "breathable")}: a timber or breathable build-up{which} is outside the Glaser method. Use an EN 15026 hygrothermal simulation for the moisture risk.");
        }

        c.Output("U_wall", "U-value of the wall");

        if (c.Has("U_ref"))
            c.Output("dU_ref", "Difference from the heat-loss calculation's U-value");

        foreach (var (symbol, text) in new[] { ("R_T", "Total thermal resistance"), ("S_d", "Total diffusion-equivalent air layer thickness"), ("psat_i", "Saturation vapour pressure at the internal temperature"), ("p_ext_m", "Outside vapour pressure by month"), ("p_int_m", "Inside vapour pressure by month"), ("n_start", "First month of condensation") })
            c.Output(symbol, text);

        for (var m = 0; m < Months; m++)
            c.Output($"Mc_{m + 1}", $"{labels[m]}: condensate on the wall");

        for (var k = 1; k < n; k++)
            c.Output($"Mc_if_{k}", $"Peak condensate on interface {k}");

        foreach (var (symbol, text) in new[] { ("Mc_max", "Most condensate on any interface"), ("Mc_tot", "Most condensate on the wall"), ("Mc_end", "Condensate left after 12 months"), ("Mc_end_fine", "Condensate left after 12 months, each layer in 20 sub-layers"), ("Mc_tot_fine", "Most condensate on the wall, each layer in 20 sub-layers"), ("n_diag", "Month of the diagram"), ("sd_if", "Diffusion thickness at the nodes"), ("x_if", "Depth at the nodes"), ("T_if", "Temperature at the nodes"), ("psat_if", "Saturation pressure at the nodes"), ("p_if", "Vapour pressure at the nodes"), ("fRsi", "Temperature factor of the internal surface"), ("fRsi_min", "Minimum temperature factor"), ("n_fcrit", "Month governing the surface check") })
            c.Output(symbol, text);

        c.Output("R_Tf", "Total resistance for the surface check");

        if (c.Has("Mc_limit"))
            c.Output("u_Mc", "Peak condensate against its limit");

        c.Output("u_f", "Surface factor against its minimum");
        c.Output("pass", "All checks pass");
    }

    private static string Clean(string? text) => CalcSheetTextRenderer.Clean(text ?? string.Empty).Trim();

    private static double? Si(CalcRow row, string key, string defaultUnit) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u ? UnitCatalog.Parse(u) : UnitCatalog.Parse(defaultUnit)).ToSi(n) : null;

    private static double? Celsius(CalcRow row, string key) => row.Get(key) is { Number: { } n } cell ? (cell.Unit is { Length: > 0 } u && u != "degC" ? UnitCatalog.Parse(u).ToSi(n) - 273.15 : n) : null;

    private static double? Fraction(CalcRow row, string key) => Si(row, key, "%");

    private static string F(double v) => double.IsFinite(v) ? CalcValue.FormatNumber(v, 5) : "—";
}
