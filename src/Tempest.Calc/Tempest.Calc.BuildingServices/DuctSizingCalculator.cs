using Tempest.Calc.BuildingServices.Fluids;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.BuildingServices;

/// <summary>
/// B3 — duct sizing by the equal-friction or the velocity method for
/// round, rectangular and flat-oval ducts (equivalent diameters), with
/// friction loss per length (Darcy–Weisbach, Colebrook), fittings by
/// user-supplied loss coefficients, velocity and pressure-drop limits as
/// inputs with warnings, and system totals along the index run.
/// </summary>
public sealed class DuctSizingCalculator : ICalcCalculator
{
    public const string Id = "bs.duct-sizing";

    public const string MethodEqualFriction = "equal friction";
    public const string MethodVelocity = "velocity";
    public const string ShapeRound = "round";
    public const string ShapeRectangular = "rectangular";
    public const string ShapeFlatOval = "flat oval";

    private const int MaxDetailed = 50;

    private static readonly IReadOnlyList<CalcColumnSpec> SectionColumns =
    [
        new("name", "Section", CalcInputKind.Text, IsOptional: true),
        new("Q", "Airflow", CalcInputKind.Quantity, "m^3/h", UnitChangedAt: "1.2.0", UnitWas: "m^3/s", HardMin: 0.0036, HardMax: 3.6e6),
        new("L", "Length", CalcInputKind.Quantity, "m", HardMin: 0, HardMax: 1e4),
        new("shape", "Shape", CalcInputKind.Choice, Choices: [ShapeRound, ShapeRectangular, ShapeFlatOval], IsOptional: true),
        new("a", "Fixed side a (rectangular/oval; blank = size)", CalcInputKind.Quantity, "mm", HardMin: 10, HardMax: 1e4, IsOptional: true),
        new("D", "Given diameter or side b (blank = size)", CalcInputKind.Quantity, "mm", HardMin: 10, HardMax: 1e4, IsOptional: true),
        new("C", "Σ fitting loss coefficients", CalcInputKind.Number, HardMin: 0, HardMax: 1e3, IsOptional: true),
        new("dp_eq", "Equipment / terminal loss", CalcInputKind.Quantity, "Pa", HardMin: 0, HardMax: 1e5, IsOptional: true),
        new("index", "On the index run", CalcInputKind.Boolean, IsOptional: true),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.1", CalcDiscipline.BuildingServices, "Duct sizing",
        "Calculates duct sizes by the equal-friction or the velocity method, rounded up to standard sizes. It reports the velocity, friction rate and losses of each section and the pressure drop along the index run.",
        "Round duct diameter from the design target: D by Colebrook with the air properties at T (equal friction), or D = √(4Q/πv) (velocity), rounded up to the next standard size.\nRectangular duct of equal friction and capacity: D_e = 1.30 (ab)^0.625/(a + b)^0.25.\nFlat-oval duct of equal friction and capacity: D_e = 1.55 A^0.625/P^0.25, with A = πb²/4 + b(a − b) and P = πb + 2(a − b).\nVelocity method, rectangular or flat-oval duct with side a fixed: b from the free area A = Q/v (rectangle b = A/a; flat oval the smaller root of (1 − π/4)b² − ab + A = 0), rounded up; D_e is kept for the friction only.\nFriction loss per metre, at the velocity v_e of the equivalent round duct: Δp/L = f ρ v_e²/(2D_e).\nFitting losses, at the actual velocity: Δp = ΣC ρ v²/2.\nIndex-run totals, summed over every section of the run: Δp_run = Σ(Δp/L · L + ΣC ρ v²/2).",
        [
            new CalcInputSpec("method", "Sizing method", CalcInputKind.Choice, Choices: [MethodEqualFriction, MethodVelocity], DefaultChoice: MethodEqualFriction, Group: "Setup"),
            new CalcInputSpec("sections", "Duct sections", CalcInputKind.Table, Columns: SectionColumns, MaxRows: 500, Group: "Setup"),
            new CalcInputSpec("round_to", "Round sizes up to a multiple of", CalcInputKind.Quantity, "mm", Default: 50, Min: 1, Max: 100, HardMin: 1, HardMax: 1000, Group: "Geometry"),
            new CalcInputSpec("eps_rough", "Duct roughness", CalcInputKind.Quantity, "mm", Default: 0.09, Min: 0.03, Max: 3, HardMin: 0, HardMax: 50, Group: "Material", Aliases: ["eps"]),
            new CalcInputSpec("T_air", "Air temperature", CalcInputKind.Quantity, "degC", Default: 20, Min: -20, Max: 60, HardMin: -50, HardMax: 200, Group: "Conditions", Aliases: ["T"]),
            new CalcInputSpec("p_atm", "Barometric pressure", CalcInputKind.Quantity, "kPa", Default: 101.325, Min: 80, Max: 105, HardMin: 10, HardMax: 1000, Group: "Conditions"),
            new CalcInputSpec("dp_target", "Target friction rate (equal friction)", CalcInputKind.Quantity, "Pa/m", Default: 1.0, Min: 0.3, Max: 5, HardMin: 0.01, HardMax: 100, Group: "Requirements"),
            new CalcInputSpec("v_target", "Target velocity (velocity method)", CalcInputKind.Quantity, "m/s", Default: 6, Min: 2, Max: 20, HardMin: 0.1, HardMax: 100, Group: "Requirements"),
            new CalcInputSpec("v_limit", "Velocity limit (warning and check)", CalcInputKind.Quantity, "m/s", Default: 8, Min: 2, Max: 30, HardMin: 0.1, HardMax: 100, Group: "Requirements"),
            new CalcInputSpec("dp_limit", "Total pressure-drop limit (check)", CalcInputKind.Quantity, "Pa", Default: 300, Min: 50, Max: 2000, HardMin: 1, HardMax: 1e5, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Air is dry at the stated temperature and pressure.", "Ideal gas, ρ = p/(287.055 T)."),
            new CalcAssumption("The viscosity of air follows Sutherland's law.", "Standard air properties."),
            new CalcAssumption("Rectangular and flat-oval ducts are sized through the equivalent diameter for equal friction and capacity.", "Huebscher / Heyt–Diaz relations as given in ASHRAE Fundamentals Ch. 21."),
            new CalcAssumption("Fitting loss coefficients C are user-supplied (manufacturer or ASHRAE/CIBSE data, not embedded) and refer to the section's velocity pressure.", "Non-negotiable: no standard tables embedded."),
        ],
        [
            new CalcClauseRef("ASHRAE Handbook — Fundamentals", "Ch. 21", "Duct design (friction chart basis, equivalent diameters, loss coefficients)"),
            new CalcClauseRef("CIBSE Guide C", "§4", "Flow of fluids in pipes and ducts"),
            new CalcClauseRef("BS EN 1505:1998", "—", "Ventilation for buildings: sheet metal air ducts and fittings with rectangular cross-section, dimensions"),
            new CalcClauseRef("BS EN 1506:2007", "—", "Ventilation for buildings: sheet metal air ducts and fittings with circular cross-section, dimensions"),
        ],
        [
            new CalcApplicabilityRule("sections", "At least one section is required.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("geometry", "A given size must be positive, and a fixed side needs the rectangular or flat-oval shape.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("velocity", "A section exceeds the velocity limit.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("laminar", "A section is laminar or transitional (Re < 4000), which is unusual for ductwork.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("summary-only", "More than 50 sections: per-section lines are omitted.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-round-given", "Round duct of given size, friction rate (ASHRAE friction-chart point)",
                new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 21 friction chart: 1 m³/s in a 400 mm round duct at standard air ≈ 8 m/s, ≈ 1.6 Pa/m (ε = 0.09 mm). Values here from Colebrook with Sutherland viscosity: v = 7.958 m/s, Re = 2.12e5, f = 0.01714, Δp/L = 1.633 Pa/m"),
                [CalcInputValue.Choice("method", MethodEqualFriction), CalcInputValue.Quantity("T_air", 20, "degC"), CalcInputValue.Quantity("eps_rough", 0.09, "mm"), Sections(("main", 1.0, 10, ShapeRound, 0, 400, 0, 0, true))],
                [new CalcExpectedValue("v_s1", 7.958, "m/s", 1e-3), new CalcExpectedValue("f_s1", 0.01714, "", 5e-3), new CalcExpectedValue("dpL_s1", 1.633, "Pa/m", 5e-3), new CalcExpectedValue("dp_index", 16.33, "Pa", 5e-3)]),
            new CalcTestCase("ka-rectangular-equivalent", "Rectangular 600 × 300 equivalent diameter (ASHRAE Ch. 21 relation)",
                new CalcReferenceSource("ASHRAE Handbook — Fundamentals", "2017", "Ch. 21 equivalent round duct for a rectangular duct: D_e = 1.30 (ab)^0.625/(a + b)^0.25; 600 × 300 → 457 mm; friction rate through D_e at v_e = 4Q/πD_e² = 4.87 m/s ≈ 0.556 Pa/m (independent recomputation)"),
                [CalcInputValue.Choice("method", MethodEqualFriction), CalcInputValue.Quantity("T_air", 20, "degC"), Sections(("branch", 0.8, 12, ShapeRectangular, 600, 300, 0.5, 0, true))],
                [new CalcExpectedValue("De_s1", 457.3, "mm", 2e-3), new CalcExpectedValue("v_s1", 4.444, "m/s", 1e-3), new CalcExpectedValue("dpL_s1", 0.556, "Pa/m", 1e-2)]),
            new CalcTestCase("ka-velocity-method", "Velocity method sizes to the next standard round size (analytic)",
                new CalcReferenceSource("Analytic", null, "Q = 0.5 m³/s at 6 m/s: D = √(4Q/πv) = 325.7 mm → 350 mm standard; actual v = 5.197 m/s"),
                [CalcInputValue.Choice("method", MethodVelocity), CalcInputValue.Quantity("v_target", 6, "m/s"), CalcInputValue.Quantity("round_to", 50, "mm"), Sections(("riser", 0.5, 8, ShapeRound, 0, 0, 1.2, 50, true))],
                [new CalcExpectedValue("D_s1", 350, "mm", 1e-9), new CalcExpectedValue("v_s1", 5.197, "m/s", 1e-3), new CalcExpectedValue("dp_fit_s1", 19.47, "Pa", 5e-3)]),
        ],
        DiagramKey: "duct-sizing",
        Category: "Air",
        OutputPorts:
        [
            new CalcOutputPort("dp_index", "Pa", "Pressure drop along the index run", "duct.pressure_drop_index"),
            new CalcOutputPort("v_peak", "m/s", "Highest velocity of any section", "duct.velocity_peak"),
        ]);

    /// <summary>Sections: (name, Q m³/s, L m, shape, a mm, D-or-b mm, ΣC, equipment loss Pa, on index run).</summary>
    public static CalcInputValue Sections(params (string Name, double Q, double L, string Shape, double A, double D, double C, double DpEq, bool Index)[] rows) =>
        CalcInputValue.Table("sections", rows.Select(r => CalcRow.Of(("name", new CalcCell(Text: r.Name)), ("Q", new CalcCell(r.Q, "m^3/s")), ("L", new CalcCell(r.L, "m")), ("shape", new CalcCell(Text: r.Shape)), ("a", r.A > 0 ? new CalcCell(r.A, "mm") : new CalcCell()), ("D", r.D > 0 ? new CalcCell(r.D, "mm") : new CalcCell()), ("C", new CalcCell(r.C)), ("dp_eq", new CalcCell(r.DpEq, "Pa")), ("index", new CalcCell(Flag: r.Index)))).ToList());

    /// <summary>Dry-air density (kg/m³) from the ideal-gas law.</summary>
    public static double AirDensity(double celsius, double pKpa) => pKpa * 1e3 / (287.055 * (celsius + 273.15));

    /// <summary>Dry-air dynamic viscosity (Pa·s) by Sutherland's law (μ₀ = 1.716e-5 at 273.15 K, S = 110.4 K).</summary>
    public static double AirViscosity(double celsius)
    {
        var t = celsius + 273.15;
        return 1.716e-5 * Math.Pow(t / 273.15, 1.5) * (273.15 + 110.4) / (t + 110.4);
    }

    public static double EquivalentDiameterRectangular(double a, double b) => 1.30 * Math.Pow(a * b, 0.625) / Math.Pow(a + b, 0.25);

    public static double EquivalentDiameterFlatOval(double a, double b)
    {
        var area = Math.PI * b * b / 4 + b * (a - b);
        var perimeter = Math.PI * b + 2 * (a - b);
        return 1.55 * Math.Pow(area, 0.625) / Math.Pow(perimeter, 0.25);
    }

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_dp", "u_v");
        var method = c.InputText("method");
        var rows = c.Rows("sections");
        c.Input("round_to");
        c.Input("eps_rough");
        c.Input("T_air");
        c.Input("p_atm");
        c.Input("dp_target");
        c.Input("v_target");
        c.Input("v_limit");
        c.Input("dp_limit");
        var t = c.Si("T_air") - 273.15;
        var rho = AirDensity(t, c.Si("p_atm") / 1e3);
        var mu = AirViscosity(t);
        var eps = c.Si("eps_rough");
        var dpTarget = c.Si("dp_target");
        var vTarget = c.Si("v_target");
        var vLimit = c.Si("v_limit");
        var roundTo = c.Si("round_to");
        c.Record("rho", "p / (287.055 T)", $"{F(c.Si("p_atm"))} Pa / (287.055 × {F(t + 273.15)} K)", ScalarValue.Of(rho, "kg/m^3"), "Air density");
        c.Record("mu", "Sutherland's law", $"T = {F(t + 273.15)} K", ScalarValue.Of(mu * 1e6, "uPa*s"), "Air dynamic viscosity");

        if (rows.Count == 0)
        {
            c.Block("sections", c.EnterRows("sections"));
            return;
        }

        var detailed = rows.Count <= MaxDetailed;

        if (!detailed)
            c.Warn("summary-only", "sections: there are more than 50 sections. Per-section lines are omitted.");

        double dpIndex = 0, dpAll = 0, vPeak = 0;
        var vPeakName = string.Empty;
        var mm = UnitCatalog.Parse("mm");
        var mps = UnitCatalog.Parse("m/s");
        var pa = UnitCatalog.Parse("Pa");
        var paPerM = UnitCatalog.Parse("Pa/m");

        for (var i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            var k = $"s{i + 1}";
            var name = r.Get("name")?.Text is { Length: > 0 } n ? n : $"section {i + 1}";
            var q = Cell(r, "Q", "m^3/h");
            var l = Cell(r, "L", "m");
            var shape = r.Get("shape")?.Text ?? ShapeRound;
            var aGiven = r.Get("a")?.Number is { } ? Cell(r, "a", "mm") : double.NaN;
            var dGiven = r.Get("D")?.Number is { } ? Cell(r, "D", "mm") : double.NaN;
            var cSum = r.Get("C")?.Number ?? 0;
            var dpEq = Cell(r, "dp_eq", "Pa");
            var onIndex = r.Get("index")?.Flag ?? true;

            if (q <= 0 || l < 0 || (!double.IsNaN(dGiven) && dGiven <= 0) || (!double.IsNaN(aGiven) && aGiven <= 0))
            {
                c.Block("geometry", $"sections: {name} needs positive airflow and sizes.");
                return;
            }

            if (shape == ShapeRound && !double.IsNaN(aGiven))
            {
                c.Block("geometry", $"sections: {name} is round. A fixed side a needs a rectangular or flat-oval shape.");
                return;
            }

            // Size: a target equivalent diameter from the method, then the actual geometry.
            double deTarget;

            if (method == MethodVelocity)
            {
                deTarget = Math.Sqrt(4 * q / (Math.PI * vTarget));
            }
            else
            {
                // Equal friction: solve f ρ v²/(2D) = Δp/L with v = 4Q/(πD²) by bisection on D.
                deTarget = Engine.Numerics.RootFinding.Bisect(d => FrictionRate(q, d, rho, mu, eps) - dpTarget, 0.02, 5.0, 1e-10, 200);
            }

            double de, area, sizeText;
            double dimA = double.NaN, dimB = double.NaN;

            if (shape == ShapeRound)
            {
                de = double.IsNaN(dGiven) ? RoundUp(deTarget, roundTo) : dGiven;
                area = Math.PI * de * de / 4;
                sizeText = de;
            }
            else
            {
                // Fixed side a (default 1.5 × the target equivalent diameter, rounded); find b for the equivalent diameter, round up.
                dimA = double.IsNaN(aGiven) ? RoundUp(deTarget * 1.5, roundTo) : aGiven;

                if (!double.IsNaN(dGiven))
                {
                    dimB = dGiven;
                }
                else if (method == MethodVelocity)
                {
                    // The velocity method sizes the free area, Q/v (D_e is kept for the friction calculation only): a rectangle of the round duct's D_e has more area than the round duct.
                    var areaTarget = q / vTarget;
                    double b;

                    if (shape == ShapeRectangular)
                    {
                        b = areaTarget / dimA;
                    }
                    else
                    {
                        // Flat oval: A = π b²/4 + b (a − b) = a b − (1 − π/4) b², so (1 − π/4) b² − a b + A = 0 (the smaller root); b = a (a circle) is the most area a given a can have.
                        var kOval = 1 - Math.PI / 4;
                        var disc = dimA * dimA - 4 * kOval * areaTarget;
                        b = disc <= 0 ? dimA : (dimA - Math.Sqrt(disc)) / (2 * kOval);
                    }

                    dimB = RoundUp(b, roundTo);
                }
                else
                {
                    var b = Engine.Numerics.RootFinding.Bisect(bb => Equivalent(shape, dimA, bb) - deTarget, 0.01, Math.Max(dimA, deTarget) * 4, 1e-10, 200);
                    dimB = RoundUp(b, roundTo);
                }

                if (shape == ShapeFlatOval && dimB > dimA)
                    (dimA, dimB) = (dimB, dimA);

                de = Equivalent(shape, dimA, dimB);
                area = shape == ShapeRectangular ? dimA * dimB : Math.PI * dimB * dimB / 4 + dimB * (dimA - dimB);
                sizeText = de;
            }

            var v = q / area;
            // Friction through the equivalent round duct: D_e carries the same airflow at the same friction rate, so the friction velocity is 4Q/(πD_e²), not Q/A (G-01).
            var vFriction = 4 * q / (Math.PI * de * de);
            var re = rho * vFriction * de / mu;
            var (f, _) = re < Friction.LaminarLimit ? (Friction.Laminar(Math.Max(re, 1)), 0) : Friction.Colebrook(re, eps / de);
            var dpL = f * rho * vFriction * vFriction / (2 * de);
            var dpFriction = dpL * l;
            var pv = rho * v * v / 2;
            var dpFit = cSum * pv;
            var dpSection = dpFriction + dpFit + dpEq;
            dpAll += dpSection;
            if (onIndex) dpIndex += dpSection;
            if (v > vPeak || vPeakName.Length == 0)
                vPeakName = name;

            vPeak = Math.Max(vPeak, v);

            if (re < Friction.TurbulentLimit)
                c.Warn("laminar", $"sections: {name} has Re = {F(re)}, below 4000. Check the airflow.");

            if (v > vLimit)
                c.Warn("velocity", $"sections: {name} runs at {F(v)} m/s, above the {F(vLimit)} m/s limit.");

            if (!detailed)
                continue;

            if (shape == ShapeRound)
            {
                c.Record($"D_{k}", $"{name}: diameter", double.IsNaN(dGiven) ? $"target {F(deTarget * 1e3)} mm rounded up to {F(roundTo * 1e3)} mm" : "as given", ScalarValue.Of(de * 1e3, mm), $"Diameter, {name}");
                c.Output($"D_{k}", $"D, {name}");
            }
            else
            {
                c.Record($"a_{k}", $"{name}: side a", double.IsNaN(aGiven) ? "chosen" : "as given", ScalarValue.Of(dimA * 1e3, mm), $"Side a, {name}");
                c.Record($"b_{k}", $"{name}: side b", double.IsNaN(dGiven) ? (method == MethodVelocity ? $"from the free area Q/v = {F(q / vTarget * 1e6)} mm² at the target velocity" : $"from the target equivalent diameter {F(deTarget * 1e3)} mm") : "as given", ScalarValue.Of(dimB * 1e3, mm), $"Side b, {name}");
                c.Record($"De_{k}", $"{name}: equivalent diameter", shape == ShapeRectangular ? "1.30 (ab)^0.625/(a + b)^0.25" : "1.55 A^0.625/P^0.25", ScalarValue.Of(de * 1e3, mm), $"Equivalent diameter, {name}", "ASHRAE Ch. 21");
                c.Output($"a_{k}", $"a, {name}");
                c.Output($"b_{k}", $"b, {name}");
                c.Output($"De_{k}", $"D_e, {name}");
            }

            c.Record($"v_{k}", $"{name}: Q / A", $"{F(q)} / {F(area)}", ScalarValue.Of(v, mps), $"Velocity, {name}");
            c.Record($"Re_{k}", $"{name}: ρ v_e D_e / μ (v_e = 4Q/πD_e²)", $"{F(rho)} × {F(vFriction)} × {F(de)} / {F(mu)}", new ScalarValue(re), $"Reynolds number, {name}");
            c.Record($"f_{k}", $"{name}: friction factor", re < Friction.LaminarLimit ? "64/Re" : "Colebrook–White", new ScalarValue(f), $"Friction factor, {name}");
            c.Record($"dpL_{k}", $"{name}: f ρ v_e² / (2 D_e)", $"{F(f)} × {F(rho)} × {F(vFriction)}² / (2 × {F(de)})", ScalarValue.Of(dpL, paPerM), $"Friction rate, {name}");
            c.Record($"dp_f_{k}", $"{name}: (Δp/L) L", $"{F(dpL)} × {F(l)}", ScalarValue.Of(dpFriction, pa), $"Friction loss, {name}");
            c.Record($"dp_fit_{k}", $"{name}: ΣC ρ v²/2", $"{F(cSum)} × {F(pv)}", ScalarValue.Of(dpFit, pa), $"Fitting loss, {name}");
            c.Record($"dp_{k}", $"{name}: friction + fittings + equipment", $"{F(dpFriction)} + {F(dpFit)} + {F(dpEq)}", ScalarValue.Of(dpSection, pa), $"Section loss, {name}{(onIndex ? " (index run)" : "")}");

            foreach (var o in new[] { ($"v_{k}", $"v, {name}"), ($"Re_{k}", $"Re, {name}"), ($"f_{k}", $"f, {name}"), ($"dpL_{k}", $"Δp/L, {name}"), ($"dp_f_{k}", $"Friction loss, {name}"), ($"dp_fit_{k}", $"Fitting loss, {name}"), ($"dp_{k}", $"Section loss, {name}") })
                c.Output(o.Item1, o.Item2);
        }

        c.Record("dp_index", "Σ section losses on the index run", $"{rows.Count(r => r.Get("index")?.Flag ?? true)} section(s)", ScalarValue.Of(dpIndex, pa), "Index-run pressure drop");
        c.Record("dp_all", "Σ section losses, all sections", $"{rows.Count} section(s)", ScalarValue.Of(dpAll, pa), "Sum over every section (not a path)");
        // Shot-fix C7: the step printed an empty substitution line ("="); it names the fastest section and the number compared, as dp_index names its count.
        c.Record("v_peak", "highest section velocity", $"v of {vPeakName}, the highest of {rows.Count} section(s)", ScalarValue.Of(vPeak, mps), "Highest velocity");
        c.Record("u_dp", "dp_index / dp_limit", $"{F(dpIndex)} / {F(c.Si("dp_limit"))}", new ScalarValue(dpIndex / c.Si("dp_limit")), "Pressure-drop utilisation");
        c.Record("u_v", "v_peak / v_limit", $"{F(vPeak)} / {F(vLimit)}", new ScalarValue(vPeak / vLimit), "Velocity utilisation");
        c.Output("dp_index", "Index-run Δp");
        c.Output("dp_all", "Σ all sections");
        c.Output("v_peak", "Highest velocity");
        c.Check("Index-run pressure drop against limit", "dp_index", "dp_limit");
        c.Check("Highest velocity against limit", "v_peak", "v_limit");
    }

    private static double Equivalent(string shape, double a, double b) => shape == ShapeRectangular ? EquivalentDiameterRectangular(a, b) : EquivalentDiameterFlatOval(Math.Max(a, b), Math.Min(a, b));

    private static double FrictionRate(double q, double d, double rho, double mu, double eps)
    {
        var v = 4 * q / (Math.PI * d * d);
        var re = rho * v * d / mu;
        var f = re < Friction.LaminarLimit ? Friction.Laminar(Math.Max(re, 1)) : Friction.Colebrook(re, eps / d).F;
        return f * rho * v * v / (2 * d);
    }

    private static double RoundUp(double metres, double step) => Math.Ceiling(metres / step - 1e-9) * step;

    private static double Cell(CalcRow row, string key, string unit)
    {
        var cell = row.Get(key);
        return cell?.Number is { } n ? UnitCatalog.Parse(string.IsNullOrEmpty(cell.Unit) ? unit : cell.Unit).ToSi(n) : 0;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
