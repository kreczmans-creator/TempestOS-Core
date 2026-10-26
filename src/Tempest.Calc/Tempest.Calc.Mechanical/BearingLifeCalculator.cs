using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M5 — rolling bearing rating life (ISO 281:2007) and static safety factor (ISO 76:2006). Ratings, X/Y/e factors and a_ISO come from reference records (Addendum A, §A3).</summary>
public sealed class BearingLifeCalculator : ICalcCalculator
{
    public const string Id = "mech.bearing-life";
    public const string Ball = "ball (p = 3)";
    public const string Roller = "roller (p = 10/3)";

    private static readonly IReadOnlyList<CalcColumnSpec> DutyColumns =
    [
        new("F_r", "Radial load", CalcInputKind.Quantity, "kN", HardMin: 0, HardMax: 2000),
        new("F_a", "Axial load", CalcInputKind.Quantity, "kN", HardMin: 0, HardMax: 2000),
        new("U", "Share of revolutions", CalcInputKind.Number, HardMin: 0, HardMax: 1),
    ];

    public CalcTemplate Template { get; } = new(
        Id, "1.2.0", CalcDiscipline.Mechanical, "Rolling bearing life (ISO 281)",
        "The basic and modified rating life of a rolling bearing under a steady load or a duty cycle, at a chosen reliability, against a required life; and the static safety factor against a required value.",
        "Equivalent load: P = X_1·F_r + Y_1·F_a when F_a/F_r ≤ e_lim (X_1 = 1, Y_1 = 0 for a single-row radial ball bearing), else P = X·F_r + Y·F_a (Y = 0 is refused when an axial load acts above e_lim)\n" +
        "Duty cycle: P_eq = (Σ P_i^p·U_i / Σ U_i)^(1/p)\n" +
        "Basic rating life: L_10 = (C/P)^p million revolutions, p = 3 ball, 10/3 roller\n" +
        "Life in hours: L_10h = 10⁶·L_10 / (60·n)\n" +
        "Reliability factor (ISO 281:2007, derived): a_1 = 0.95·[ln(100/R)/ln(100/90)]^(2/3) + 0.05\n" +
        "Modified life: L_nm = a_1·a_ISO·L_10\n" +
        "Static safety: P_0 = max(X_0·F_r + Y_0·F_a, F_r), s_0 = C_0/P_0",
        [
            new CalcInputSpec("type", "Bearing type", CalcInputKind.Choice, Choices: [Ball, Roller], DefaultChoice: Ball),
            new CalcInputSpec("F_r", "Radial load", CalcInputKind.Quantity, "kN", Default: 2, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Loads"),
            new CalcInputSpec("F_a", "Axial load", CalcInputKind.Quantity, "kN", Default: 0, Min: 0, Max: 2000, HardMin: 0, HardMax: 2000, Group: "Loads"),
            new CalcInputSpec("duty", "Duty cycle (replaces F_r, F_a when given)", CalcInputKind.Table, Columns: DutyColumns, MaxRows: 50, IsOptional: true, Group: "Loads"),
            new CalcInputSpec("n", "Speed", CalcInputKind.Quantity, "rpm", Default: 1000, Min: 1, Max: 50000, HardMin: 1, HardMax: 50000, Group: "Loads"),
            new CalcInputSpec("C", "Basic dynamic load rating", CalcInputKind.Quantity, "kN", Default: 20, Min: 0.1, Max: 10000, HardMin: 1e-3, HardMax: 1e5, Group: "Bearing"),
            new CalcInputSpec("C_0", "Basic static load rating", CalcInputKind.Quantity, "kN", Default: 11, Min: 0.1, Max: 10000, HardMin: 1e-3, HardMax: 1e5, Group: "Bearing"),
            new CalcInputSpec("e_lim", "Load ratio limit e", CalcInputKind.Number, Default: 0.3, Min: 0, Max: 5, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("X_1", "Radial factor X₁ for F_a/F_r ≤ e (1 for a single-row radial ball bearing)", CalcInputKind.Number, Default: 1, Min: 0, Max: 5, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("Y_1", "Axial factor Y₁ for F_a/F_r ≤ e (0 for a single-row radial ball bearing; above 0 for double-row and paired bearings)", CalcInputKind.Number, Default: 0, Min: 0, Max: 10, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("X", "Radial factor X for F_a/F_r > e", CalcInputKind.Number, Default: 1, Min: 0, Max: 5, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("Y", "Axial factor Y for F_a/F_r > e (from the manufacturer; Y = 0 makes the axial load count for nothing and is refused when one acts)", CalcInputKind.Number, Default: 0, Min: 0, Max: 10, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("X_0", "Static radial factor X₀", CalcInputKind.Number, Default: 0.6, Min: 0, Max: 5, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("Y_0", "Static axial factor Y₀", CalcInputKind.Number, Default: 0.5, Min: 0, Max: 10, HardMin: 0, HardMax: 100, Group: "Factors"),
            new CalcInputSpec("R", "Reliability", CalcInputKind.Quantity, "%", Default: 90, Min: 90, Max: 99.95, HardMin: 90, HardMax: 99.95, Group: "Life"),
            new CalcInputSpec("a_iso", "Life modification factor a_ISO (from the manufacturer's tool)", CalcInputKind.Number, Default: 1, Min: 0.1, Max: 50, HardMin: 0.1, HardMax: 50, Group: "Life"),
            new CalcInputSpec("kappa", "Viscosity ratio κ used for a_ISO (0.1 to 100; above 4 it is read at 4)", CalcInputKind.Number, Min: 0.1, Max: 4, HardMin: 0.1, HardMax: 100, IsOptional: true, Group: "Life"),
            new CalcInputSpec("L_req", "Required life", CalcInputKind.Quantity, "h", Default: 20000, Min: 1, Max: 1e6, HardMin: 1e-3, HardMax: 1e7, Group: "Requirements"),
            new CalcInputSpec("s_0_req", "Required static safety factor", CalcInputKind.Number, Default: 1, Min: 0.5, Max: 4, HardMin: 0.5, HardMax: 4, Group: "Requirements"),
        ],
        [
            new CalcAssumption("Ratings C and C₀, factors X, Y, e, X₀, Y₀ and a_ISO are the manufacturer's values for the bearing, entered as reference records.", "No catalogue table ships with TempestOS."),
            new CalcAssumption("a₁ uses the ISO 281:2007 Weibull form (0.64 at 95 %, 0.25 at 99 %); ISO 281:1990 gave 0.62 and 0.21.", "Edition stated on the sheet."),
            new CalcAssumption("Loads are steady within each duty step; speed is constant.", "Duty steps weighted by share of revolutions."),
        ],
        [
            new CalcClauseRef("ISO 281:2007", "cl. 6–7", "Dynamic equivalent load and basic rating life"),
            new CalcClauseRef("ISO 281:2007", "cl. 9", "Modified rating life"),
            new CalcClauseRef("ISO 76:2006", "cl. 6–7", "Static equivalent load and static safety factor"),
            new CalcClauseRef("Shigley's Mechanical Engineering Design", "11th ed., Ch. 11", "Rolling-contact bearings"),
        ],
        [
            new CalcApplicabilityRule("no-load", "No load: nothing to compute.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("kappa-low", "κ below 0.1 is outside the range a_ISO is defined for.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("kappa-high", "κ above 4 is treated as 4 when a_ISO is read.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("light-load", "Load so small against C that L_10 exceeds 10¹² million revolutions: the life is capped at that value.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("axial-factor", "An axial load acts above e_lim but Y is 0, so it would count for nothing: enter the bearing's Y.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("heavy-load", "P above 0.5·C: the rating-life equations lose accuracy.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("duty", "A duty cycle needs a positive total share of revolutions.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-ball-95", "Ball bearing, radial load, 95 % reliability",
                new CalcReferenceSource("Analytic (ISO 281:2007 relations)", "ISO 281:2007", "C = 20 kN, P = 2 kN, 1000 rpm: L_10 = 1000 Mrev, L_10h = 16 666.7 h; a_1(95 %) = 0.6379; L_5h = 10 631.9 h"),
                [Choice(Ball), Q("F_r", 2, "kN"), Q("F_a", 0, "kN"), Q("n", 1000, "rpm"), Q("C", 20, "kN"), Q("C_0", 11, "kN"), Q("R", 95, "%"), N("a_iso", 1), Q("L_req", 10000, "h")],
                [new CalcExpectedValue("L_10", 1000, "", 1e-9), new CalcExpectedValue("L_10h", 16666.67, "h", 1e-6), new CalcExpectedValue("a_1", 0.637912, "", 1e-5), new CalcExpectedValue("L_nmh", 10631.86, "h", 1e-5), new CalcExpectedValue("u_life", 0.940569, "", 1e-5)]),
            new CalcTestCase("ka-roller-99", "Roller bearing, 99 % reliability, a_ISO = 2",
                new CalcReferenceSource("Analytic (ISO 281:2007 relations)", "ISO 281:2007", "C = 50 kN, P = 5 kN, 500 rpm: L_10 = 10^(10/3) = 2154.43 Mrev, L_10h = 71 814.5 h; a_1(99 %) = 0.2483; L_1m = 1070.03 Mrev"),
                [Choice(Roller), Q("F_r", 5, "kN"), Q("F_a", 0, "kN"), Q("n", 500, "rpm"), Q("C", 50, "kN"), Q("C_0", 40, "kN"), Q("R", 99, "%"), N("a_iso", 2), Q("L_req", 30000, "h")],
                [new CalcExpectedValue("L_10", 2154.435, "", 1e-6), new CalcExpectedValue("L_10h", 71814.49, "h", 1e-6), new CalcExpectedValue("a_1", 0.248332, "", 1e-5), new CalcExpectedValue("L_nm", 1070.029, "", 1e-5), new CalcExpectedValue("L_nmh", 35667.62, "h", 1e-5)]),
            new CalcTestCase("ka-combined", "Combined load above e, static check",
                new CalcReferenceSource("Analytic (ISO 281:2007 / ISO 76:2006 relations, illustrative factors)", null, "F_r = 3, F_a = 1.5 kN; F_a/F_r = 0.5 > e_lim = 0.22: P = 0.56·3 + 2·1.5 = 4.68 kN; L_10 = (30/4.68)³ = 263.41 Mrev; P_0 = max(2.55, 3) = 3 kN; s_0 = 16/3"),
                [Choice(Ball), Q("F_r", 3, "kN"), Q("F_a", 1.5, "kN"), Q("n", 1200, "rpm"), Q("C", 30, "kN"), Q("C_0", 16, "kN"), N("e_lim", 0.22), N("X", 0.56), N("Y", 2), N("X_0", 0.6), N("Y_0", 0.5), Q("R", 90, "%"), N("a_iso", 1), Q("L_req", 3000, "h"), N("s_0_req", 2)],
                [new CalcExpectedValue("P", 4.68, "kN", 1e-9), new CalcExpectedValue("L_10", 263.4063, "", 1e-6), new CalcExpectedValue("L_10h", 3658.421, "h", 1e-6), new CalcExpectedValue("P_0", 3, "kN", 1e-9), new CalcExpectedValue("s_0", 5.333333, "", 1e-6)]),
            new CalcTestCase("ka-duty-cycle", "Two-step duty cycle",
                new CalcReferenceSource("Analytic (cubic mean load)", null, "2 kN for half the revolutions, 4 kN for half: P_eq = ((2³ + 4³)/2)^(1/3) = 36^(1/3) = 3.3019 kN"),
                [Choice(Ball), Q("F_r", 0, "kN"), Q("F_a", 0, "kN"), Duty((2, 0, 0.5), (4, 0, 0.5)), Q("n", 800, "rpm"), Q("C", 25, "kN"), Q("C_0", 15, "kN"), Q("R", 90, "%"), N("a_iso", 1), Q("L_req", 5000, "h")],
                [new CalcExpectedValue("P", 3.301927, "kN", 1e-6), new CalcExpectedValue("L_10h", 9042.245, "h", 1e-6), new CalcExpectedValue("P_0", 4, "kN", 1e-9)]),
            new CalcTestCase("ka-skf-6206", "SKF 6206 deep groove ball bearing, 2.5 kN at 1500 rpm",
                new CalcReferenceSource("SKF Rolling bearings catalogue (PUB BU/P1 17000)", "2018", "6206: C = 20.3 kN, C_0 = 11.2 kN (as listed by RS Components; not yet checked against SKF's own catalogue). L_10 = (20.3/2.5)³ = 535.39 Mrev; L_10h = 5948.7 h; s_0 = 4.48"),
                [Choice(Ball), Q("F_r", 2.5, "kN"), Q("F_a", 0, "kN"), Q("n", 1500, "rpm"), Q("C", 20.3, "kN"), Q("C_0", 11.2, "kN"), Q("R", 90, "%"), N("a_iso", 1), Q("L_req", 5000, "h")],
                [new CalcExpectedValue("L_10", 535.3873, "", 1e-6), new CalcExpectedValue("L_10h", 5948.748, "h", 1e-6), new CalcExpectedValue("s_0", 4.48, "", 1e-6)],
                CalcReferenceStatus.Unconfirmed),
        ],
        Category: "Bearings",
        OutputPorts:
        [
            new CalcOutputPort("L_nmh", "h", "Modified rating life in hours", "bearing.life_modified"),
            new CalcOutputPort("L_10h", "h", "Basic rating life in hours", "bearing.life_basic"),
            new CalcOutputPort("P", "kN", "Equivalent dynamic load", "bearing.load_dynamic"),
            new CalcOutputPort("P_0", "kN", "Equivalent static load", "bearing.load_static"),
            new CalcOutputPort("s_0", "", "Static safety factor", "bearing.safety_static"),
        ],
        RecordKinds:
        [
            new CalcRecordKind("bearing.ratings", "Bearing load ratings", ["C", "C_0"], "From the manufacturer's catalogue or product page for the exact designation."),
            new CalcRecordKind("bearing.factors", "Bearing X, Y, e factors", ["e_lim", "X_1", "Y_1", "X", "Y", "X_0", "Y_0"], "From the manufacturer's catalogue for the bearing type and F_a/C_0."),
            new CalcRecordKind("bearing.aiso", "Bearing a_ISO", ["a_iso", "kappa"], "From the manufacturer's life tool for the lubricant, contamination and κ."),
        ]);

    private static CalcInputValue Q(string symbol, double value, string unit) => CalcInputValue.Quantity(symbol, value, unit);

    private static CalcInputValue N(string symbol, double value) => CalcInputValue.Of(symbol, value);

    private static CalcInputValue Choice(string type) => CalcInputValue.Choice("type", type);

    /// <summary>A duty-cycle table from (F_r kN, F_a kN, share of revolutions) steps.</summary>
    public static CalcInputValue Duty(params (double Fr, double Fa, double U)[] steps) =>
        CalcInputValue.Table("duty", steps.Select(s => CalcRow.Of(("F_r", new CalcCell(s.Fr, "kN")), ("F_a", new CalcCell(s.Fa, "kN")), ("U", new CalcCell(s.U)))).ToList());

    public void Run(CalcRunContext c)
    {
        c.MarkUtilisation("u_life", "u_s0");
        c.MarkAtLeast("s_0");
        var roller = c.InputText("type") == Roller;

        foreach (var s in new[] { "F_r", "F_a", "n", "C", "C_0", "e_lim", "X_1", "Y_1", "X", "Y", "X_0", "Y_0", "R", "a_iso", "L_req", "s_0_req" })
            c.Input(s);

        if (c.Has("kappa"))
        {
            var kappa = c.InputSi("kappa");

            if (kappa < 0.1)
            {
                c.Block("kappa-low", "kappa: κ below 0.1 is outside the range a_ISO is defined for; improve the lubrication or use a different bearing.");
                return;
            }

            if (kappa > 4)
                c.Warn("kappa-high", "kappa: κ above 4 is treated as 4; read a_ISO at κ = 4.");
        }

        var e = c.InputSi("e_lim");
        var x1 = c.InputSi("X_1");
        var y1 = c.InputSi("Y_1");
        var x = c.InputSi("X");
        var y = c.InputSi("Y");
        var x0 = c.InputSi("X_0");
        var y0 = c.InputSi("Y_0");
        var p = roller ? 10.0 / 3.0 : 3.0;
        c.Assign("p", roller ? "10 / 3" : "3", null, roller ? "Life exponent, roller bearing" : "Life exponent, ball bearing", "ISO 281:2007 cl. 6–7");
        var kN = UnitCatalog.Parse("kN");
        var rows = c.Rows("duty");

        if (rows.Count > 0)
        {
            var steps = rows.Select(r => (Fr: Cell(r, "F_r", "kN"), Fa: Cell(r, "F_a", "kN"), U: r.Get("U")?.Number ?? 0)).ToList();
            var sumU = steps.Sum(s => s.U);

            if (!(sumU > 0) || steps.All(s => s.Fr == 0 && s.Fa == 0))
            {
                c.Block("duty", "duty: the steps need a load and a positive total share of revolutions.");
                return;
            }

            var lost = steps.FindIndex(s => s.Fa > 0 && !Below(s.Fr, s.Fa));

            if (y == 0 && lost >= 0 && AxialLost(c, steps[lost].Fr, steps[lost].Fa, e))
                return;

            var loads = steps.Select(s => Dynamic(s.Fr, s.Fa)).ToList();
            var peq = Math.Pow(steps.Select((s, i) => Math.Pow(loads[i], p) * s.U).Sum() / sumU, 1 / p);
            c.Record("P", "(Σ P_i^p·U_i / Σ U_i)^(1/p)", "(" + string.Join(" + ", steps.Select((s, i) => $"{F(loads[i] / 1e3)}^p×{F(s.U)}")) + $") / {F(sumU)}", ScalarValue.Of(peq / 1e3, kN), "Equivalent dynamic load over the duty cycle", "ISO 281:2007 cl. 6–7");
            var p0 = steps.Max(s => Math.Max(x0 * s.Fr + y0 * s.Fa, s.Fr));
            c.Record("P_0", "max over steps of max(X_0·F_r + Y_0·F_a, F_r)", $"{F(p0 / 1e3)} kN", ScalarValue.Of(p0 / 1e3, kN), "Equivalent static load (heaviest step)", "ISO 76:2006 cl. 6–7");
        }
        else
        {
            var fr = c.InputSi("F_r");
            var fa = c.InputSi("F_a");

            if (fr == 0 && fa == 0)
            {
                c.Block("no-load", "F_r/F_a: both loads are zero; there is nothing to compute.");
                return;
            }

            var radialOnly = Below(fr, fa);

            if (!radialOnly && y == 0 && AxialLost(c, fr, fa, e))
                return;

            c.Assign("P", radialOnly ? "X_1 * F_r + Y_1 * F_a" : "X * F_r + Y * F_a", "kN", radialOnly ? "Equivalent dynamic load (F_a/F_r ≤ e_lim)" : "Equivalent dynamic load (F_a/F_r > e_lim)", "ISO 281:2007 cl. 6–7");
            c.Assign("P_0", "max(X_0 * F_r + Y_0 * F_a, F_r)", "kN", "Equivalent static load", "ISO 76:2006 cl. 6–7");
        }

        if (!(c.Si("P") > 0) || !(c.Si("P_0") > 0))
        {
            c.Block("no-load", "F_r/F_a: the equivalent load is zero; there is nothing to compute.");
            return;
        }

        var capped = !(Math.Pow(c.Si("C") / c.Si("P"), p) is var ratio && double.IsFinite(ratio) && ratio <= 1e12);

        if (capped)
            c.Warn("light-load", "F_r/F_a: the load is so small against C that L_10 would exceed 10¹² million revolutions; the life is held at that value.");

        if (c.Si("P") > 0.5 * c.Si("C"))
            c.Warn("heavy-load", "C: P is above 0.5·C; the rating-life equations lose accuracy under such heavy load.");

        c.Assign("L_10", capped ? "1000000000000" : "(C / P)^p", null, capped ? "Basic rating life, million revolutions (held at 10¹²)" : "Basic rating life, million revolutions", "ISO 281:2007 cl. 6–7");
        c.Assign("L_10h", "L_10 * 1000000 * 2 * pi / n", "h", "Basic rating life in hours");
        c.Assign("a_1", "0.95 * (ln(1 / R) / ln(1 / 0.9))^(2 / 3) + 0.05", null, "Reliability factor (derived, ISO 281:2007 Weibull form)", "ISO 281:2007 cl. 9");
        c.Assign("L_nm", "a_1 * a_iso * L_10", null, "Modified rating life, million revolutions", "ISO 281:2007 cl. 9");
        c.Assign("L_nmh", "a_1 * a_iso * L_10h", "h", "Modified rating life in hours");
        c.Assign("s_0", "C_0 / P_0", null, "Static safety factor", "ISO 76:2006");
        c.Assign("u_life", "L_req / L_nmh", null, "Life utilisation (required / modified life)");
        c.Assign("u_s0", "s_0_req / s_0", null, "Static utilisation (required / actual safety factor)");

        foreach (var (symbol, text) in new[] { ("P", "Equivalent dynamic load"), ("L_10", "Basic rating life (Mrev)"), ("L_10h", "Basic rating life"), ("a_1", "Reliability factor"), ("L_nm", "Modified rating life (Mrev)"), ("L_nmh", "Modified rating life"), ("P_0", "Equivalent static load"), ("s_0", "Static safety factor"), ("u_life", "Life utilisation"), ("u_s0", "Static utilisation") })
            c.Output(symbol, text);

        c.CheckUtilisation("Modified rating life against required life", "u_life", "ISO 281:2007 cl. 9");
        c.CheckUtilisation("Static safety factor against required", "u_s0", "ISO 76:2006");

        bool Below(double fr, double fa) => fa == 0 || (fr > 0 && fa / fr <= e);
        double Dynamic(double fr, double fa) => Below(fr, fa) ? x1 * fr + y1 * fa : x * fr + y * fa;
    }

    /// <summary>Blocks (and returns true) when an axial load acts above e_lim while Y = 0: P would ignore it (Black B-A-01).</summary>
    private static bool AxialLost(CalcRunContext c, double fr, double fa, double e)
    {
        var ratio = fr > 0 ? $"F_a/F_r = {F(fa / fr)} exceeds e_lim = {F(e)}" : "F_r is 0 and the load is pure thrust";
        c.Block("axial-factor", $"Y: {ratio}, but Y = 0, so the axial load {F(fa / 1e3)} kN would count for nothing in P. Enter the bearing's X and Y for F_a/F_r > e (from the manufacturer), or raise e_lim if the bearing takes no more axial load than it allows.");
        return true;
    }

    private static double Cell(CalcRow row, string key, string unit)
    {
        var cell = row.Get(key);
        return cell?.Number is { } n ? UnitCatalog.Parse(cell.Unit ?? unit).ToSi(n) : 0;
    }

    private static string F(double v) => CalcValue.FormatNumber(v, 5);
}
