using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Mechanical;

/// <summary>M2a — ISO 286 limits and fits: IT grades and fundamental deviations from the published formulae, hole and shaft limits, fit classification, extreme clearances and interferences.</summary>
public sealed class Iso286FitCalculator : ICalcCalculator
{
    public const string Id = "mech.iso286-fit";
    public const string IsoHole = "ISO 286 hole class";
    public const string BearingBore = "rolling-bearing bore (deviations from the bearing record)";

    public CalcTemplate Template { get; } = new(
        Id, "1.6.0", CalcDiscipline.Mechanical, "Limits and fits (ISO 286)",
        "Calculates the limits of size of a hole and a shaft for a nominal size and ISO 286 tolerance classes, and the resulting fit with its extreme clearances and interferences. It uses the ISO 286-1 formulae, not the tables.",
        "Standard tolerance factor at the geometric mean D of the size step: i = 0.45·D^(1/3) + 0.001·D (µm).\nWidth of the tolerance band for the grade: ITn = multiplier × i.\nFundamental deviations by letter from the published formulae, and hole deviations by the general rule: EI_hole = −es_shaft (A–H); ES_hole = −ei_shaft + Δ (K–ZC).\nLimits of size of the hole and the shaft: limit = nominal + deviation.\nFit extremes, the largest and smallest clearance (negative is interference): c_max = ES_hole − ei_shaft; c_min = EI_hole − es_shaft.\nRolling-bearing bore mode: a bearing bore is not an ISO 286 hole; it has its own tolerance (ISO 492) lying below nominal, so ES_hole and EI_hole are entered from the bearing record instead of being computed.",
        [
            new CalcInputSpec("hole_kind", "Hole: an ISO 286 class, or a rolling-bearing bore (ES_hole and EI_hole from the bearing record, ISO 492)", CalcInputKind.Choice, Choices: [IsoHole, BearingBore], DefaultChoice: IsoHole, Group: "Setup"),
            new CalcInputSpec("hole_letter", "Hole fundamental deviation letter", CalcInputKind.Choice, Choices: ["H", "G", "F", "E", "D", "JS", "K", "M", "N", "P", "R", "S", "T", "U"], DefaultChoice: "H", Group: "Setup"),
            new CalcInputSpec("hole_grade", "Hole IT grade", CalcInputKind.Number, Default: 7, Min: 5, Max: 11, HardMin: 1, HardMax: 18, Group: "Setup"),
            new CalcInputSpec("shaft_letter", "Shaft fundamental deviation letter", CalcInputKind.Choice, Choices: ["h", "g", "f", "e", "d", "js", "k", "m", "n", "p", "r", "s", "t", "u"], DefaultChoice: "g", Group: "Setup"),
            new CalcInputSpec("shaft_grade", "Shaft IT grade", CalcInputKind.Number, Default: 6, Min: 4, Max: 11, HardMin: 1, HardMax: 18, Group: "Setup"),
            new CalcInputSpec("D", "Nominal size", CalcInputKind.Quantity, "mm", Default: 25, Min: 1, Max: 500, HardMin: 0.001, HardMax: Iso286.MaximumSizeMm, Group: "Geometry"),
            new CalcInputSpec("ES_hole_override", "Hole upper deviation override (optional; blank = the formula value, or a reference record)", CalcInputKind.Quantity, "µm", Min: -1000, Max: 1000, IsOptional: true, HardMin: -5000, HardMax: 5000, Group: "Overrides", Aliases: ["ES_override"]),
            new CalcInputSpec("EI_hole_override", "Hole lower deviation override (optional; blank = the formula value)", CalcInputKind.Quantity, "µm", Min: -1000, Max: 1000, IsOptional: true, HardMin: -5000, HardMax: 5000, Group: "Overrides", Aliases: ["EI_override"]),
            new CalcInputSpec("es_shaft_override", "Shaft upper deviation override (optional; blank = the formula value)", CalcInputKind.Quantity, "µm", Min: -1000, Max: 1000, IsOptional: true, HardMin: -5000, HardMax: 5000, Group: "Overrides", Aliases: ["es_override"]),
            new CalcInputSpec("ei_shaft_override", "Shaft lower deviation override (optional; blank = the formula value)", CalcInputKind.Quantity, "µm", Min: -1000, Max: 1000, IsOptional: true, HardMin: -5000, HardMax: 5000, Group: "Overrides", Aliases: ["ei_override"]),
        ],
        [
            new CalcAssumption("IT values and deviations are the unrounded results of the ISO 286-1 formulae at the size step's geometric mean.", "The tables apply the standard's own rounding, so tabulated values differ by about 1 µm for the letters a to n."),
            new CalcAssumption("For the letters p to zc the formula value can lie several µm below the tabulated deviation.", "The tables add their own increments and sub-steps, so p6 at 450 mm is 62.2 µm from the formula against 68 µm tabulated."),
            new CalcAssumption("Hole deviations follow the general rule from the shaft deviations, with Δ where the standard applies it.", "Δ is ITn − IT(n−1), and the standard's table-specific departures are not reproduced."),
        ],
        [
            new CalcClauseRef("ISO 286-1:2010", "§4, §5", "Bases of tolerances, deviations and fits — tolerance grades and fundamental deviations"),
            new CalcClauseRef("ISO 286-2:2010", "—", "Tables of standard tolerance classes and limit deviations"),
        ],
        [
            new CalcApplicabilityRule("bearing-bore", "A rolling-bearing bore needs ES_hole_override and EI_hole_override from the bearing record; it is not an H7 hole.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("shaft-override", "Only one of the two shaft deviation overrides is entered; both are needed.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("hole-override", "Only one of the two hole deviation overrides is entered; both are needed.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("shaft-letter", "The shaft letter has no formula at this size (the t shaft starts above 24 mm); choose another letter or enter the overrides.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("hole-letter", "The hole letter has no formula at this size (the T hole starts above 24 mm); choose another letter or enter the overrides.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("large-size", "Nominal size above 500 mm uses the I = 0.004 D + 2.1 tolerance unit.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("interference-letter", "A fundamental deviation from p to zc is evaluated from the formula: the tabulated value can be several µm larger, so the maximum interference is understated; use the overrides where the table governs.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("fine-grade", "IT grades below IT5 are evaluated by geometric interpolation; check against the standard.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-h7g6-25", "25 mm H7/g6", new CalcReferenceSource("ISO 286-2:2010 (public excerpt), confirmed in Machinery's Handbook", "30th ed.", "25 mm H7: +21/0 µm; g6: −7/−20 µm; clearance 7…41 µm"),
                [CalcInputValue.Quantity("D", 25, "mm"), CalcInputValue.Choice("hole_letter", "H"), CalcInputValue.Of("hole_grade", 7), CalcInputValue.Choice("shaft_letter", "g"), CalcInputValue.Of("shaft_grade", 6)],
                [new CalcExpectedValue("IT_hole", 21, "µm", 0.03), new CalcExpectedValue("IT_shaft", 13, "µm", 0.03), new CalcExpectedValue("es_shaft", -7, "µm", 0.06), new CalcExpectedValue("c_min", 7, "µm", 0.06), new CalcExpectedValue("c_max", 41, "µm", 0.03)]),
            new CalcTestCase("ka-h7p6-50", "50 mm H7/p6 interference", new CalcReferenceSource("ISO 286-2:2010 (public excerpt)", null, "50 mm H7: +25/0 µm; p6: +42/+26 µm; interference 1…42 µm (formula gives p = IT7 exactly, 1 µm under the table's +26)"),
                [CalcInputValue.Quantity("D", 50, "mm"), CalcInputValue.Choice("hole_letter", "H"), CalcInputValue.Of("hole_grade", 7), CalcInputValue.Choice("shaft_letter", "p"), CalcInputValue.Of("shaft_grade", 6)],
                [new CalcExpectedValue("IT_hole", 25, "µm", 0.03), new CalcExpectedValue("ei_shaft", 26, "µm", 0.06), new CalcExpectedValue("i_max", 42, "µm", 0.05), new CalcExpectedValue("i_min", 0, "µm", 1.5)]),
            new CalcTestCase("ka-bearing-bore-k6", "35 mm bearing bore (0 / −9 µm, illustrative) on a k6 seat: interference at every size combination",
                new CalcReferenceSource("Analytic (ISO 286-1 formulae for the shaft; the bore deviations are invented for the test)", null, "35 mm lies in the 30–50 step, D = √(30·50) = 38.730 mm, i = 0.45·D^(1/3) + 0.001·D = 1.5612 µm, IT6 = 15.612 µm; k: ei = 0.6·D^(1/3) = +2.030 µm, es = 17.642 µm; bore ES = 0, EI = −9 µm: c_max = ES − ei = −2.030 µm, c_min = EI − es = −26.642 µm, so every combination is an interference"),
                [CalcInputValue.Quantity("D", 35, "mm"), CalcInputValue.Choice("hole_kind", BearingBore), CalcInputValue.Choice("shaft_letter", "k"), CalcInputValue.Of("shaft_grade", 6), CalcInputValue.Quantity("ES_hole_override", 0, "µm"), CalcInputValue.Quantity("EI_hole_override", -9, "µm")],
                [new CalcExpectedValue("ei_shaft", 2.030017, "µm", 1e-5), new CalcExpectedValue("es_shaft", 17.642448, "µm", 1e-5), new CalcExpectedValue("c_max", -2.030017, "µm", 1e-5), new CalcExpectedValue("c_min", -26.642448, "µm", 1e-5), new CalcExpectedValue("i_min", 2.030017, "µm", 1e-5)]),
        ],
        Category: "Fits",
        OutputPorts:
        [
            new CalcOutputPort("c_max", "µm", "Largest clearance (negative: smallest interference)", "fit.clearance_max"),
            new CalcOutputPort("c_min", "µm", "Smallest clearance (negative: largest interference)", "fit.clearance_min"),
            new CalcOutputPort("i_max", "µm", "Largest interference", "fit.interference_max"),
            new CalcOutputPort("i_min", "µm", "Smallest interference (negative: largest clearance)", "fit.interference_min"),
            new CalcOutputPort("ES_hole", "µm", "Hole upper deviation", "fit.deviation_hole_upper", ["ES"]),
            new CalcOutputPort("EI_hole", "µm", "Hole lower deviation", "fit.deviation_hole_lower", ["EI"]),
            new CalcOutputPort("es_shaft", "µm", "Shaft upper deviation", "fit.deviation_shaft_upper", ["es"]),
            new CalcOutputPort("ei_shaft", "µm", "Shaft lower deviation", "fit.deviation_shaft_lower", ["ei"]),
            new CalcOutputPort("IT_hole", "µm", "Hole tolerance", "fit.tolerance_hole"),
            new CalcOutputPort("IT_shaft", "µm", "Shaft tolerance", "fit.tolerance_shaft"),
        ]);

    public void Run(CalcRunContext c)
    {
        var dMm = c.InputSi("D") * 1000;
        var bore = c.InputText("hole_kind") == BearingBore;
        var holeLetter = c.InputText("hole_letter");
        var shaftLetter = c.InputText("shaft_letter");
        var holeGrade = (int)Math.Round(c.InputSi("hole_grade"));
        var shaftGrade = (int)Math.Round(c.InputSi("shaft_grade"));
        var um = UnitCatalog.Parse("µm");

        if (dMm > 500)
            c.Warn("large-size", "D: above 500 mm the tolerance unit is I = 0.004 D + 2.1 µm.");

        if (holeGrade < 5 || shaftGrade < 5)
            c.Warn("fine-grade", $"{(holeGrade < 5 ? "hole_grade" : "shaft_grade")}: IT grades below IT5 are interpolated geometrically between IT1 and IT5.");

        var holeFormula = !bore && !(c.Has("ES_hole_override") || c.Has("EI_hole_override")) && Iso286.IsInterferenceLetter(holeLetter);
        var shaftFormula = !(c.Has("es_shaft_override") || c.Has("ei_shaft_override")) && Iso286.IsInterferenceLetter(shaftLetter);

        if (holeFormula || shaftFormula)
        {
            var which = holeFormula && shaftFormula ? $"{holeLetter} and {shaftLetter}" : holeFormula ? holeLetter : shaftLetter;
            c.Warn("interference-letter", $"{(holeFormula ? "hole_letter" : "shaft_letter")}: the deviation of '{which}' is a formula value that can lie several µm below the table. At 450 mm, p6 gives 62.2 µm against 68 µm tabulated, so enter an override where the table governs.");
        }

        var (lo, hi) = Iso286.Step(dMm);
        var dMean = Iso286.StepMean(dMm);
        c.Record("D_step", "size step (lower, upper]", $"({CalcValue.FormatNumber(lo)}, {CalcValue.FormatNumber(hi)}] mm", ScalarValue.Of(dMean, "mm"), "Geometric mean of the size step", "ISO 286-1 §4");
        c.Record("i", dMean <= 500 ? "0.45·D^(1/3) + 0.001·D" : "0.004·D + 2.1", $"D = {CalcValue.FormatNumber(dMean)} mm", ScalarValue.Of(Iso286.ToleranceUnit(dMm), um), "Standard tolerance unit", "ISO 286-1 §5");

        var itHole = Iso286.Tolerance(dMm, holeGrade);
        var itShaft = Iso286.Tolerance(dMm, shaftGrade);
        c.Record("IT_hole", $"IT{holeGrade}", $"{Multiplier(holeGrade)} × i", ScalarValue.Of(itHole, um), "Hole tolerance", "ISO 286-1 §5");
        c.Record("IT_shaft", $"IT{shaftGrade}", $"{Multiplier(shaftGrade)} × i", ScalarValue.Of(itShaft, um), "Shaft tolerance", "ISO 286-1 §5");

        // Shaft deviations.
        double es, ei;

        if (c.Has("es_shaft_override") || c.Has("ei_shaft_override"))
        {
            if (!(c.Has("es_shaft_override") && c.Has("ei_shaft_override")))
            {
                c.Block("shaft-override", $"{(c.Has("es_shaft_override") ? "ei_shaft_override" : "es_shaft_override")}: enter the missing shaft deviation. Enter both shaft deviations or neither.");
                return;
            }

            es = c.InputSi("es_shaft_override") * 1e6;
            ei = c.InputSi("ei_shaft_override") * 1e6;
            c.Record("es_shaft", "es_shaft (reference record)", "override", ScalarValue.Of(es, um), "Shaft upper deviation");
            c.Record("ei_shaft", "ei_shaft (reference record)", "override", ScalarValue.Of(ei, um), "Shaft lower deviation");
        }
        else
        {
            var fundamental = Iso286.ShaftFundamentalDeviation(shaftLetter, dMm, shaftGrade);

            if (fundamental is null)
            {
                c.Block("shaft-letter", shaftLetter == "t"
                    ? $"shaft_letter: the standard has no t shaft at {CalcValue.FormatNumber(dMm)} mm, because t starts above {CalcValue.FormatNumber(Iso286.TShaftFromMm)} mm. Choose another letter, or enter es_shaft_override and ei_shaft_override from a reference record."
                    : $"shaft_letter: no formula gives '{shaftLetter}'. Enter es_shaft_override and ei_shaft_override from a reference record.");
                return;
            }

            if (shaftLetter == "js")
            {
                es = itShaft / 2;
                ei = -itShaft / 2;
            }
            else if (Iso286.ShaftFundamentalIsUpper(shaftLetter))
            {
                es = fundamental.Value;
                ei = es - itShaft;
            }
            else
            {
                ei = fundamental.Value;
                es = ei + itShaft;
            }

            c.Record("es_shaft", Iso286.ShaftFundamentalIsUpper(shaftLetter) ? $"fundamental deviation '{shaftLetter}'" : "ei_shaft + IT_shaft", shaftLetter == "js" ? "+IT/2" : Iso286.ShaftFundamentalIsUpper(shaftLetter) ? Formula(shaftLetter) : $"{CalcValue.FormatNumber(ei)} + {CalcValue.FormatNumber(itShaft)}", ScalarValue.Of(es, um), "Shaft upper deviation", "ISO 286-1 §5");
            c.Record("ei_shaft", Iso286.ShaftFundamentalIsUpper(shaftLetter) ? "es_shaft − IT_shaft" : $"fundamental deviation '{shaftLetter}'", shaftLetter == "js" ? "−IT/2" : Iso286.ShaftFundamentalIsUpper(shaftLetter) ? $"{CalcValue.FormatNumber(es)} − {CalcValue.FormatNumber(itShaft)}" : Formula(shaftLetter), ScalarValue.Of(ei, um), "Shaft lower deviation", "ISO 286-1 §5");
        }

        // Hole deviations.
        double ES, EI;

        if (bore && !(c.Has("ES_hole_override") && c.Has("EI_hole_override")))
        {
            c.Block("bearing-bore", "ES_hole_override: a rolling-bearing bore has its own tolerance (ISO 492), not an ISO 286 class. Enter its upper and lower deviation (ES_hole_override, EI_hole_override) from the bearing record.");
            return;
        }

        if (c.Has("ES_hole_override") || c.Has("EI_hole_override"))
        {
            if (!(c.Has("ES_hole_override") && c.Has("EI_hole_override")))
            {
                c.Block("hole-override", $"{(c.Has("ES_hole_override") ? "EI_hole_override" : "ES_hole_override")}: enter the missing hole deviation. Enter both hole deviations or neither.");
                return;
            }

            ES = c.InputSi("ES_hole_override") * 1e6;
            EI = c.InputSi("EI_hole_override") * 1e6;
            c.Record("ES_hole", bore ? "ES_hole (bearing bore record, ISO 492)" : "ES_hole (reference record)", "override", ScalarValue.Of(ES, um), "Hole upper deviation");
            c.Record("EI_hole", bore ? "EI_hole (bearing bore record, ISO 492)" : "EI_hole (reference record)", "override", ScalarValue.Of(EI, um), "Hole lower deviation");
        }
        else
        {
            var fundamental = Iso286.HoleFundamentalDeviation(holeLetter, dMm, holeGrade);

            if (fundamental is null)
            {
                c.Block("hole-letter", holeLetter.Equals("T", StringComparison.OrdinalIgnoreCase)
                    ? $"hole_letter: the standard has no T hole at {CalcValue.FormatNumber(dMm)} mm, because T starts above {CalcValue.FormatNumber(Iso286.TShaftFromMm)} mm. Choose another letter, or enter ES_hole_override and EI_hole_override from a reference record."
                    : $"hole_letter: no formula gives '{holeLetter}'. Enter ES_hole_override and EI_hole_override from a reference record.");
                return;
            }

            var lower = holeLetter.ToLowerInvariant();

            if (lower == "js")
            {
                ES = itHole / 2;
                EI = -itHole / 2;
            }
            else if (string.CompareOrdinal(lower, "h") <= 0)
            {
                EI = fundamental.Value;
                ES = EI + itHole;
            }
            else
            {
                ES = fundamental.Value;
                EI = ES - itHole;
            }

            c.Record("ES_hole", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? "EI_hole + IT_hole" : $"fundamental deviation '{holeLetter}'", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? $"{CalcValue.FormatNumber(EI)} + {CalcValue.FormatNumber(itHole)}" : "general rule from the shaft deviation", ScalarValue.Of(ES, um), "Hole upper deviation", "ISO 286-1 §5");
            c.Record("EI_hole", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? $"fundamental deviation '{holeLetter}'" : "ES_hole − IT_hole", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? "−es_shaft of the same letter" : $"{CalcValue.FormatNumber(ES)} − {CalcValue.FormatNumber(itHole)}", ScalarValue.Of(EI, um), "Hole lower deviation", "ISO 286-1 §5");
        }

        c.Assign("D_hole_max", "D + ES_hole", "mm", "Hole maximum");
        c.Assign("D_hole_min", "D + EI_hole", "mm", "Hole minimum");
        c.Assign("d_shaft_max", "D + es_shaft", "mm", "Shaft maximum");
        c.Assign("d_shaft_min", "D + ei_shaft", "mm", "Shaft minimum");
        c.Assign("c_max", "ES_hole - ei_shaft", "µm", "Maximum clearance (negative: minimum interference)");
        c.Assign("c_min", "EI_hole - es_shaft", "µm", "Minimum clearance (negative: maximum interference)");
        c.Assign("i_max", "es_shaft - EI_hole", "µm", "Maximum interference");
        c.Assign("i_min", "ei_shaft - ES_hole", "µm", "Minimum interference (negative: maximum clearance)");

        var cMax = c.Si("c_max");
        var cMin = c.Si("c_min");
        var kind = cMin >= 0 ? "clearance" : cMax <= 0 ? "interference" : "transition";
        var holeName = bore ? "bearing bore" : $"{holeLetter}{holeGrade}";
        c.Record("fit", "fit class", $"{holeName}/{shaftLetter}{shaftGrade}", new TextValue(kind), "Fit kind: clearance, transition or interference");
        c.Assign("c_mean", "(c_max + c_min) / 2", "µm", "Mean clearance");

        c.Output("IT_hole", "Hole tolerance");
        c.Output("IT_shaft", "Shaft tolerance");
        c.Output("ES_hole", "Hole upper deviation");
        c.Output("EI_hole", "Hole lower deviation");
        c.Output("es_shaft", "Shaft upper deviation");
        c.Output("ei_shaft", "Shaft lower deviation");
        c.Output("D_hole_max", "Hole maximum");
        c.Output("D_hole_min", "Hole minimum");
        c.Output("d_shaft_max", "Shaft maximum");
        c.Output("d_shaft_min", "Shaft minimum");
        c.Output("fit", "Fit kind");
        c.Output("c_max", "Maximum clearance");
        c.Output("c_min", "Minimum clearance");
        c.Output("i_max", "Maximum interference");
        c.Output("i_min", "Minimum interference");
        c.Output("c_mean", "Mean clearance");
        c.CheckCondition("Fit classes against the fit kind", true, $"{holeName}/{shaftLetter}{shaftGrade}", kind, "fit");
    }

    private static string Multiplier(int grade) => grade switch
    {
        5 => "7", 6 => "10", 7 => "16", 8 => "25", 9 => "40", 10 => "64", 11 => "100", 12 => "160", 13 => "250", 14 => "400", 15 => "640", 16 => "1000", 17 => "1600", 18 => "2500",
        _ => "(interpolated)",
    };

    private static string Formula(string letter) => letter switch
    {
        "d" => "−16·D^0.44", "e" => "−11·D^0.41", "f" => "−5.5·D^0.41", "g" => "−2.5·D^0.34", "h" => "0",
        "k" => "+0.6·D^(1/3)", "m" => "+(IT7 − IT6)", "n" => "+5·D^0.34", "p" => "+IT7", "r" => "+√(p·s)", "s" => "+IT8 + 1 (D ≤ 50) / +IT7 + 0.4 D",
        "t" => "+IT7 + 0.63 D", "u" => "+IT7 + D", _ => "formula",
    };
}
