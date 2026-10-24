using Tempest.Calc.Engine.Numerics;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Electrical;

/// <summary>
/// E2 — enclosure heat dissipation and fan sizing: an internal heat-load
/// schedule, the effective dissipating surface from the mounting condition
/// (per-face exposure factors), natural dissipation through a user-editable
/// heat-transfer coefficient (IEC TR 60890-style approach), the temperature
/// rise, the airflow for forced ventilation at a target rise with altitude
/// density correction, fan duty against a user fan curve, optional solar
/// gain and the cooling-unit duty when ventilation cannot hold the limit.
/// </summary>
public sealed class EnclosureHeatCalculator : ICalcCalculator
{
    public const string Id = "elec.enclosure-heat";

    private static readonly IReadOnlyList<CalcColumnSpec> LoadColumns =
    [
        new("name", "Item", CalcInputKind.Text, IsOptional: true),
        new("P", "Heat loss per item", CalcInputKind.Quantity, "W", HardMin: 0, HardMax: 1e6),
        new("qty", "Quantity", CalcInputKind.Number, HardMin: 0, HardMax: 1e4, IsOptional: true),
        new("duty", "Duty factor (0–1)", CalcInputKind.Number, HardMin: 0, HardMax: 1, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.0.1", CalcDiscipline.Electrical, "Enclosure heat dissipation and fan sizing",
        "Total heat load inside a control enclosure, the effective surface that can dissipate it for the mounting condition, the internal temperature rise with natural convection and radiation through a stated overall heat-transfer coefficient, the forced-ventilation airflow for a target rise (density corrected for altitude), the fan duty point against a fan curve and the system resistance, optional solar gain, and the cooling-unit capacity needed when the limit cannot be met.",
        "Heat dissipated inside the enclosure, plus any solar gain: P = Σ P_i n_i d_i (+ α I A_sun).\nEffective cooling surface, each face weighted by how freely it sheds heat: A_eff = Σ f_face × A_face.\nTemperature rise with natural cooling, one coefficient over the exposed faces (the IEC TR 60890 tables are not used): ΔT = P/(h A_eff).\nAir flow a fan must move to hold the target rise: V = P/(ρ c_p ΔT_target), with ρ = p(z)/(287.055 T) and c_p = 1006 J/(kg·K).\nFan duty point, where the fan curve meets the system resistance (found by bisection): p_fan(V) = p_sys (V/V_ref)².\nCooling unit capacity, when the surfaces alone cannot reject the heat: P_cool = P − h A_eff (T_max − T_amb).",
        [
            new CalcInputSpec("loads", "Heat loads", CalcInputKind.Table, Columns: LoadColumns, MaxRows: 500, Group: "Heat load"),
            new CalcInputSpec("I_sun", "Solar irradiance on the enclosure (0 = indoors)", CalcInputKind.Quantity, "W/m^2", Default: 0, Min: 0, Max: 1200, HardMin: 0, HardMax: 2000, Group: "Heat load"),
            new CalcInputSpec("A_sun", "Sunlit area", CalcInputKind.Quantity, "m^2", Default: 0, Min: 0, Max: 100, HardMin: 0, HardMax: 1e4, Group: "Heat load"),
            new CalcInputSpec("alpha_sun", "Surface absorptivity", CalcInputKind.Number, Default: 0.5, Min: 0.1, Max: 1, HardMin: 0, HardMax: 1, Group: "Heat load"),
            new CalcInputSpec("W", "Enclosure width", CalcInputKind.Quantity, "m", Default: 0.6, Min: 0.1, Max: 5, HardMin: 0.01, HardMax: 50, Group: "Enclosure"),
            new CalcInputSpec("H", "Enclosure height", CalcInputKind.Quantity, "m", Default: 0.8, Min: 0.1, Max: 5, HardMin: 0.01, HardMax: 50, Group: "Enclosure"),
            new CalcInputSpec("D", "Enclosure depth", CalcInputKind.Quantity, "m", Default: 0.3, Min: 0.1, Max: 5, HardMin: 0.01, HardMax: 50, Group: "Enclosure"),
            new CalcInputSpec("f_top", "Top exposure (1 free, 0 covered)", CalcInputKind.Number, Default: 1, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("f_front", "Front exposure", CalcInputKind.Number, Default: 1, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("f_rear", "Rear exposure (0 = wall-mounted)", CalcInputKind.Number, Default: 0, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("f_left", "Left side exposure", CalcInputKind.Number, Default: 1, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("f_right", "Right side exposure", CalcInputKind.Number, Default: 1, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("f_bottom", "Bottom exposure (0 = on the floor)", CalcInputKind.Number, Default: 0, Min: 0, Max: 1, HardMin: 0, HardMax: 1, Group: "Enclosure"),
            new CalcInputSpec("h", "Overall heat-transfer coefficient of the enclosure surface (editable reference value)", CalcInputKind.Quantity, "W/(m^2*K)", Default: 5.5, Min: 3, Max: 8, HardMin: 0.1, HardMax: 100, Group: "Enclosure"),
            new CalcInputSpec("T_amb", "Ambient temperature", CalcInputKind.Quantity, "degC", Default: 35, Min: -20, Max: 55, HardMin: -60, HardMax: 100, Group: "Conditions"),
            new CalcInputSpec("T_max", "Maximum internal temperature", CalcInputKind.Quantity, "degC", Default: 50, Min: 20, Max: 80, HardMin: -60, HardMax: 150, Group: "Conditions"),
            new CalcInputSpec("altitude", "Site altitude (air density)", CalcInputKind.Quantity, "m", Default: 0, Min: 0, Max: 4000, HardMin: -500, HardMax: 10000, Group: "Conditions"),
            new CalcInputSpec("V_pts", "Fan curve: airflow points", CalcInputKind.Vector, "m^3/h", IsOptional: true, Group: "Fan"),
            new CalcInputSpec("p_pts", "Fan curve: static pressure points", CalcInputKind.Vector, "Pa", IsOptional: true, Group: "Fan"),
            new CalcInputSpec("p_sys", "System resistance (filters, grilles) at the reference airflow", CalcInputKind.Quantity, "Pa", Default: 20, Min: 0, Max: 500, HardMin: 0, HardMax: 1e4, Group: "Fan"),
            new CalcInputSpec("V_ref", "Reference airflow for the system resistance", CalcInputKind.Quantity, "m^3/h", Default: 100, Min: 1, Max: 1e5, HardMin: 1e-3, HardMax: 1e7, Group: "Fan"),
        ],
        [
            new CalcAssumption("Heat leaves the enclosure through its exposed faces at one overall coefficient h (natural convection plus radiation to surroundings at the ambient temperature); covered faces dissipate nothing.", "IEC TR 60890-style effective-surface approach; h is a reference value the user may edit."),
            new CalcAssumption("Forced ventilation removes the full heat load with air entering at ambient and leaving at the target rise; natural dissipation is neglected for the airflow (conservative).", "Manufacturer fan-sizing practice."),
            new CalcAssumption("The fan curve is linear between the entered points; the system resistance scales with airflow squared.", "Duty-point idealisation."),
        ],
        [
            new CalcClauseRef("IEC TR 60890", "—", "A method of temperature-rise verification of low-voltage switchgear and controlgear assemblies by calculation"),
            new CalcClauseRef("Manufacturer enclosure climate-control guides (Rittal, nVent Hoffman)", "—", "Effective surface, heat-transfer coefficient and fan-sizing practice (cited, not copied)"),
            new CalcClauseRef("BS EN 61439-1", "10.10", "Verification of temperature rise"),
        ],
        [
            new CalcApplicabilityRule("loads", "At least one heat load is needed.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("limit", "The maximum internal temperature must exceed the ambient.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("fan-curve", "The fan curve needs at least two points with matching counts, flows increasing.", CalcNoticeSeverity.Block),
            new CalcApplicabilityRule("no-fan-duty", "The fan curve and the system resistance do not cross: no duty point.", CalcNoticeSeverity.Warning),
            new CalcApplicabilityRule("cooling-unit", "Natural dissipation cannot hold the limit: forced ventilation or a cooling unit is needed.", CalcNoticeSeverity.Warning),
        ],
        [
            new CalcTestCase("ka-wall-mounted", "Wall-mounted 600 × 800 × 300 enclosure, 300 W (analytic)",
                new CalcReferenceSource("Analytic (effective-surface method as in IEC TR 60890 / manufacturer guides)", null, "A_eff = top 0.18 + front 0.48 + two sides 0.48 = 1.14 m²; ΔT = 300/(5.5 × 1.14) = 47.85 K; forced at 15 K rise, 35 °C sea level: ρ = 1.1455 kg/m³, V = 300/(1.1455 × 1006 × 15) = 0.017355 m³/s = 62.48 m³/h; cooling unit at 50 °C limit: 300 − 5.5 × 1.14 × 15 = 205.95 W"),
                [Loads(("PLC and I/O", 80, 1, 1), ("drives", 55, 4, 1)), CalcInputValue.Quantity("I_sun", 0, "W/m^2"), CalcInputValue.Quantity("A_sun", 0, "m^2"), CalcInputValue.Quantity("W", 0.6, "m"), CalcInputValue.Quantity("H", 0.8, "m"), CalcInputValue.Quantity("D", 0.3, "m"), CalcInputValue.Of("f_top", 1), CalcInputValue.Of("f_front", 1), CalcInputValue.Of("f_rear", 0), CalcInputValue.Of("f_left", 1), CalcInputValue.Of("f_right", 1), CalcInputValue.Of("f_bottom", 0), CalcInputValue.Quantity("h", 5.5, "W/(m^2*K)"), CalcInputValue.Quantity("T_amb", 35, "degC"), CalcInputValue.Quantity("T_max", 50, "degC"), CalcInputValue.Quantity("altitude", 0, "m")],
                [new CalcExpectedValue("P_total", 300, "W", 1e-9), new CalcExpectedValue("A_eff", 1.14, "m^2", 1e-9), new CalcExpectedValue("dT_nat", 47.847, "K", 1e-4), new CalcExpectedValue("V_req", 62.48, "m^3/h", 1e-3), new CalcExpectedValue("P_cool", 205.95, "W", 1e-4)]),
            new CalcTestCase("ka-fan-duty", "Fan duty point against a linear fan curve (analytic)",
                new CalcReferenceSource("Analytic", null, "Fan p = 60 − 0.3 V (V in m³/h), system p = 20 (V/100)²: 0.002 V² + 0.3 V − 60 = 0 ⇒ V = 113.75 m³/h, p = 25.88 Pa; required 62.48 m³/h ⇒ utilisation 0.5493"),
                [Loads(("load", 300, 1, 1)), CalcInputValue.Quantity("W", 0.6, "m"), CalcInputValue.Quantity("H", 0.8, "m"), CalcInputValue.Quantity("D", 0.3, "m"), CalcInputValue.Quantity("T_amb", 35, "degC"), CalcInputValue.Quantity("T_max", 50, "degC"), CalcInputValue.Quantity("altitude", 0, "m"), CalcInputValue.VectorOf("V_pts", [0, 100, 200], "m^3/h"), CalcInputValue.VectorOf("p_pts", [60, 30, 0], "Pa"), CalcInputValue.Quantity("p_sys", 20, "Pa"), CalcInputValue.Quantity("V_ref", 100, "m^3/h")],
                [new CalcExpectedValue("V_duty", 113.75, "m^3/h", 1e-3), new CalcExpectedValue("p_duty", 25.88, "Pa", 2e-3), new CalcExpectedValue("u_fan", 0.5493, "", 2e-3)]),
        ],
        DiagramKey: "enclosure-heat",
        Category: "Thermal");

    public static CalcInputValue Loads(params (string Name, double P, double Qty, double Duty)[] rows) =>
        CalcInputValue.Table("loads", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("P", new CalcCell(r.P, "W")), ("qty", new CalcCell(r.Qty)), ("duty", new CalcCell(r.Duty)))).ToList());

    public static double PressureAtAltitude(double z) => 101325 * Math.Pow(1 - 2.25577e-5 * z, 5.2559);

    public void Run(CalcRunContext c)
    {
        var rows = c.Rows("loads");

        if (rows.Count == 0)
        {
            c.Block("loads", "loads: at least one heat load is needed.");
            return;
        }

        foreach (var s in new[] { "I_sun", "A_sun", "alpha_sun", "W", "H", "D", "f_top", "f_front", "f_rear", "f_left", "f_right", "f_bottom", "h", "T_amb", "T_max", "altitude" })
            c.Input(s);

        var pLoads = rows.Sum(r => (r.Get("P")?.Number is { } p ? Engine.Units.UnitCatalog.Parse(string.IsNullOrEmpty(r.Get("P")!.Unit) ? "W" : r.Get("P")!.Unit!).ToSi(p) : 0) * (r.Get("qty")?.Number ?? 1) * (r.Get("duty")?.Number ?? 1));
        c.Record("P_loads", "Σ P_i × n_i × d_i", $"{rows.Count} item(s)", ScalarValue.Of(pLoads, "W"), "Internal heat load");
        c.Assign("P_solar", "alpha_sun * I_sun * A_sun", "W", "Solar gain");
        c.Assign("P_total", "P_loads + P_solar", "W", "Total heat to remove");
        c.Assign("A_eff", "f_top * W * D + f_bottom * W * D + f_front * W * H + f_rear * W * H + f_left * H * D + f_right * H * D", "m^2", "Effective dissipating surface", "IEC TR 60890 approach");
        c.Assign("dT_nat", "P_total / (h * A_eff)", "K", "Temperature rise with natural dissipation");
        c.Assign("T_int", "T_amb + dT_nat", "degC", "Internal temperature with natural dissipation");
        c.Assign("dT_allow", "T_max - T_amb", "K", "Allowed rise");

        if (c.Si("dT_allow") <= 0)
        {
            c.Block("limit", "T_max: the maximum internal temperature must exceed the ambient.");
            return;
        }

        c.Assign("u_nat", "dT_nat / dT_allow", null, "Natural dissipation utilisation");
        c.Assign("P_nat_max", "h * A_eff * dT_allow", "W", "Heat the enclosure dissipates naturally at the limit");
        c.Assign("P_cool", "max(0 W, P_total - P_nat_max)", "W", "Cooling-unit capacity needed if ventilation is not used");

        if (c.Si("P_cool") > 0)
            c.Warn("cooling-unit", $"T_max: natural dissipation holds {CalcValue.FormatNumber(c.Si("P_nat_max"), 4)} W of {CalcValue.FormatNumber(c.Si("P_total"), 4)} W; ventilate or fit a {CalcValue.FormatNumber(c.Si("P_cool"), 4)} W cooling unit.");

        // Forced ventilation.
        c.Record("p_site", "101325 (1 − 2.25577e-5 z)^5.2559", $"z = {CalcValue.FormatNumber(c.Si("altitude"), 4)} m", ScalarValue.Of(PressureAtAltitude(c.Si("altitude")) / 1e3, "kPa"), "Site pressure");
        c.Assign("rho_air", "p_site / (287.055 J/(kg*K) * T_amb)", "kg/m^3", "Air density at ambient and altitude");
        c.Assign("V_req", "P_total / (rho_air * 1006 J/(kg*K) * dT_allow)", "m^3/h", "Airflow for forced ventilation at the allowed rise (natural dissipation neglected)");
        c.Assign("V_req_net", "max(0 m^3/h, (P_total - P_nat_max) / (rho_air * 1006 J/(kg*K) * dT_allow))", "m^3/h", "Airflow crediting natural dissipation");

        foreach (var (symbol, text) in new[] { ("P_loads", "Internal heat load"), ("P_solar", "Solar gain"), ("P_total", "Total heat"), ("A_eff", "Effective surface"), ("dT_nat", "Natural temperature rise"), ("T_int", "Internal temperature (natural)"), ("P_nat_max", "Natural dissipation at the limit"), ("P_cool", "Cooling unit needed"), ("rho_air", "Air density"), ("V_req", "Required airflow"), ("V_req_net", "Required airflow (net)") })
            c.Output(symbol, text);

        c.CheckUtilisation("Natural dissipation holds T_max", "u_nat");

        // Fan duty.
        if (c.Has("V_pts") && c.Has("p_pts"))
        {
            var vv = ((VectorValue)c.Input("V_pts")).Si;
            var pp = ((VectorValue)c.Input("p_pts")).Si;

            if (vv.Count < 2 || vv.Count != pp.Count || vv.Zip(vv.Skip(1), (a, b) => b <= a).Any(x => x))
            {
                c.Block("fan-curve", "V_pts/p_pts: give at least two points with the same count and increasing airflows.");
                return;
            }

            c.Input("p_sys");
            c.Input("V_ref");
            var pSys = c.Si("p_sys");
            var vRef = c.Si("V_ref");
            double Gap(double v) => Interpolation.Linear(v, vv, pp) - pSys * (v / vRef) * (v / vRef);

            if (Gap(vv[0]) * Gap(vv[^1]) > 0)
            {
                c.Warn("no-fan-duty", "V_pts: the fan curve does not cross the system resistance within the curve's range.");
                c.CheckCondition("Fan duty point exists", false, "no crossing", "fan ≥ system somewhere");
            }
            else
            {
                var vd = RootFinding.Bisect(Gap, vv[0], vv[^1], 1e-12, 200);
                c.Record("V_duty", "p_fan(V) = p_sys (V/V_ref)², bisection", "", ScalarValue.Of(vd * 3600, "m^3/h"), "Fan duty airflow");
                c.Record("p_duty", "p_sys (V_duty/V_ref)²", $"{CalcValue.FormatNumber(pSys, 4)} × ({CalcValue.FormatNumber(vd * 3600, 4)}/{CalcValue.FormatNumber(vRef * 3600, 4)})²", ScalarValue.Of(pSys * (vd / vRef) * (vd / vRef), "Pa"), "Static pressure at duty");
                c.Assign("u_fan", "V_req / V_duty", null, "Airflow utilisation (required / delivered)");
                c.Output("V_duty", "Fan duty airflow");
                c.Output("p_duty", "Fan duty pressure");
                c.Output("u_fan", "Airflow utilisation");
                c.CheckUtilisation("Fan delivers the required airflow", "u_fan");
                c.Series(new CalcSeries("Fan curve", "V", "m^3/h", "p", "Pa", vv.Zip(pp, (a, b) => (a * 3600, b)).ToList()));
                c.Series(new CalcSeries("System resistance", "V", "m^3/h", "p", "Pa", Enumerable.Range(0, 21).Select(i => { var v = vv[^1] * i / 20; return (v * 3600, pSys * (v / vRef) * (v / vRef)); }).ToList()));
            }
        }
    }
}
