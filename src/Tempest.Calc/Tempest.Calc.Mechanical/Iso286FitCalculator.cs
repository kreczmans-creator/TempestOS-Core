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
        Id, "1.4.1", CalcDiscipline.Mechanical, "Limits and fits (ISO 286)",
        "Hole and shaft limits for a nominal size and tolerance classes, the resulting fit (clearance, transition or interference) and its extreme values, from the ISO 286-1 formulae.",
        "Standard tolerance factor at the geometric mean D of the size step: i = 0.45·D^(1/3) + 0.001·D (µm).\nWidth of the tolerance band for the grade: ITn = multiplier × i.\nFundamental deviations by letter from the published formulae, and hole deviations by the general rule: EI = −es (A–H); ES = −ei + Δ (K–ZC).\nLimits of size of the hole and the shaft: limit = nominal + deviation.\nFit extremes, the largest and smallest clearance (negative is interference): c_max = ES − ei; c_min = EI − es.\nRolling-bearing bore mode: a bearing bore is not an ISO 286 hole; it has its own tolerance (ISO 492) lying below nominal, so ES and EI are entered from the bearing record instead of being computed.",
        [
            new CalcInputSpec("D", "Nominal size", CalcInputKind.Quantity, "mm", Default: 25, Min: 1, Max: 500, HardMin: 0.001, HardMax: Iso286.MaximumSizeMm),
            new CalcInputSpec("hole_kind", "Hole: an ISO 286 class, or a rolling-bearing bore (ES and EI from the bearing record, ISO 492)", CalcInputKind.Choice, Choices: [IsoHole, BearingBore], DefaultChoice: IsoHole),
            new CalcInputSpec("hole_letter", "Hole fundamental deviation letter", CalcInputKind.Choice, Choices: ["H", "G", "F", "E", "D", "JS", "K", "M", "N", "P", "R", "S", "T", "U"], DefaultChoice: "H"),
            new CalcInputSpec("hole_grade", "Hole IT grade", CalcInputKind.Number, Default: 7, Min: 5, Max: 11, HardMin: 1, HardMax: 18),
            new CalcInputSpec("shaft_letter", "Shaft fundamental deviation letter", CalcInputKind.Choice, Choices: ["h", "g", "f", "e", "d", "js", "k", "m", "n", "p", "r", "s", "t", "u"], DefaultChoice: "g"),
            new CalcInputSpec("shaft_grade", "Shaft IT grade", CalcInputKind.Number, Default: 6, Min: 4, Max: 11, HardMin: 1, HardMax: 18),
            new CalcInputSpec("ES_override", "Hole upper deviation override (from a reference record)", CalcInputKind.Quantity, "µm", IsOptional: true, HardMin: -5000, HardMax: 5000),
            new CalcInputSpec("EI_override", "Hole lower deviation override", CalcInputKind.Quantity, "µm", IsOptional: true, HardMin: -5000, HardMax: 5000),
            new CalcInputSpec("es_override", "Shaft upper deviation override", CalcInputKind.Quantity, "µm", IsOptional: true, HardMin: -5000, HardMax: 5000),
            new CalcInputSpec("ei_override", "Shaft lower deviation override", CalcInputKind.Quantity, "µm", IsOptional: true, HardMin: -5000, HardMax: 5000),
        ],
        [
            new CalcAssumption("IT values and fundamental deviations are the unrounded results of the ISO 286-1 formulae at the geometric mean of the size step.", "The tabulated values apply the standard's own rounding and may differ by about 1 µm for the clearance and transition letters (a to n). For the interference letters p to zc the tables also carry the standard's own increments and sub-steps, so the formula value can be several µm below the tabulated one (p6 at 450 mm: 62.2 against 68 µm), and the sheet warns; enter overrides from a released reference record where the tabulated value governs."),
            new CalcAssumption("Hole deviations follow the general rule from the shaft deviations, with Δ = ITn − IT(n−1) where the standard applies it.", "Table-specific departures in the standard are not reproduced; use the overrides where they govern."),
        ],
        [
            new CalcClauseRef("ISO 286-1:2010", "§4, §5", "Bases of tolerances, deviations and fits — tolerance grades and fundamental deviations"),
            new CalcClauseRef("ISO 286-2:2010", "—", "Tables of standard tolerance classes and limit deviations (not reproduced)"),
        ],
        [
            new CalcApplicabilityRule("bearing-bore", "A rolling-bearing bore needs ES_override and EI_override from the bearing record; it is not an H7 hole.", CalcNoticeSeverity.Block, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("large-size", "Nominal size above 500 mm uses the I = 0.004 D + 2.1 tolerance unit.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
            new CalcApplicabilityRule("interference-letter", "A fundamental deviation from p to zc is evaluated from the formula: the tabulated value can be several µm larger, so the maximum interference is understated; use the overrides where the table governs.", CalcNoticeSeverity.Warning, CalcRuleEffect.Unconservative),
            new CalcApplicabilityRule("fine-grade", "IT grades below IT5 are evaluated by geometric interpolation; check against the standard.", CalcNoticeSeverity.Warning, CalcRuleEffect.Conservative),
        ],
        [
            new CalcTestCase("ka-h7g6-25", "25 mm H7/g6", new CalcReferenceSource("ISO 286-2:2010 (public excerpt), confirmed in Machinery's Handbook", "30th ed.", "25 mm H7: +21/0 µm; g6: −7/−20 µm; clearance 7…41 µm"),
                [CalcInputValue.Quantity("D", 25, "mm"), CalcInputValue.Choice("hole_letter", "H"), CalcInputValue.Of("hole_grade", 7), CalcInputValue.Choice("shaft_letter", "g"), CalcInputValue.Of("shaft_grade", 6)],
                [new CalcExpectedValue("IT_hole", 21, "µm", 0.03), new CalcExpectedValue("IT_shaft", 13, "µm", 0.03), new CalcExpectedValue("es", -7, "µm", 0.06), new CalcExpectedValue("c_min", 7, "µm", 0.06), new CalcExpectedValue("c_max", 41, "µm", 0.03)]),
            new CalcTestCase("ka-h7p6-50", "50 mm H7/p6 interference", new CalcReferenceSource("ISO 286-2:2010 (public excerpt)", null, "50 mm H7: +25/0 µm; p6: +42/+26 µm; interference 1…42 µm (formula gives p = IT7 exactly, 1 µm under the table's +26)"),
                [CalcInputValue.Quantity("D", 50, "mm"), CalcInputValue.Choice("hole_letter", "H"), CalcInputValue.Of("hole_grade", 7), CalcInputValue.Choice("shaft_letter", "p"), CalcInputValue.Of("shaft_grade", 6)],
                [new CalcExpectedValue("IT_hole", 25, "µm", 0.03), new CalcExpectedValue("ei", 26, "µm", 0.06), new CalcExpectedValue("i_max", 42, "µm", 0.05), new CalcExpectedValue("i_min", 0, "µm", 1.5)]),
            new CalcTestCase("ka-bearing-bore-k6", "35 mm bearing bore (0 / −9 µm, illustrative) on a k6 seat: interference at every size combination",
                new CalcReferenceSource("Analytic (ISO 286-1 formulae for the shaft; the bore deviations are invented for the test)", null, "35 mm lies in the 30–50 step, D = √(30·50) = 38.730 mm, i = 0.45·D^(1/3) + 0.001·D = 1.5612 µm, IT6 = 15.612 µm; k: ei = 0.6·D^(1/3) = +2.030 µm, es = 17.642 µm; bore ES = 0, EI = −9 µm: c_max = ES − ei = −2.030 µm, c_min = EI − es = −26.642 µm, so every combination is an interference"),
                [CalcInputValue.Quantity("D", 35, "mm"), CalcInputValue.Choice("hole_kind", BearingBore), CalcInputValue.Choice("shaft_letter", "k"), CalcInputValue.Of("shaft_grade", 6), CalcInputValue.Quantity("ES_override", 0, "µm"), CalcInputValue.Quantity("EI_override", -9, "µm")],
                [new CalcExpectedValue("ei", 2.030017, "µm", 1e-5), new CalcExpectedValue("es", 17.642448, "µm", 1e-5), new CalcExpectedValue("c_max", -2.030017, "µm", 1e-5), new CalcExpectedValue("c_min", -26.642448, "µm", 1e-5), new CalcExpectedValue("i_min", 2.030017, "µm", 1e-5)]),
        ],
        Category: "Fits");

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
            c.Warn("fine-grade", "grade: IT grades below IT5 are interpolated geometrically between IT1 and IT5.");

        var holeFormula = !bore && !(c.Has("ES_override") || c.Has("EI_override")) && Iso286.IsInterferenceLetter(holeLetter);
        var shaftFormula = !(c.Has("es_override") || c.Has("ei_override")) && Iso286.IsInterferenceLetter(shaftLetter);

        if (holeFormula || shaftFormula)
        {
            var which = holeFormula && shaftFormula ? $"{holeLetter} and {shaftLetter}" : holeFormula ? holeLetter : shaftLetter;
            c.Warn("interference-letter", $"{(holeFormula ? "hole_letter" : "shaft_letter")}: the deviation of '{which}' is evaluated from the ISO 286-1 formula; " + Iso286.InterferenceLetterNote + ". Enter ES_override/EI_override or es_override/ei_override from a reference record where the table governs.");
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

        if (c.Has("es_override") || c.Has("ei_override"))
        {
            if (!(c.Has("es_override") && c.Has("ei_override")))
            {
                c.Block("input.es_override", "es_override/ei_override: give both shaft deviations when overriding.");
                return;
            }

            es = c.InputSi("es_override") * 1e6;
            ei = c.InputSi("ei_override") * 1e6;
            c.Record("es", "es (reference record)", "override", ScalarValue.Of(es, um), "Shaft upper deviation");
            c.Record("ei", "ei (reference record)", "override", ScalarValue.Of(ei, um), "Shaft lower deviation");
        }
        else
        {
            var fundamental = Iso286.ShaftFundamentalDeviation(shaftLetter, dMm, shaftGrade);

            if (fundamental is null)
            {
                c.Block("input.shaft_letter", shaftLetter == "t"
                    ? $"shaft_letter: the standard has no t shaft at {CalcValue.FormatNumber(dMm)} mm; t starts above {CalcValue.FormatNumber(Iso286.TShaftFromMm)} mm. Choose another letter, or enter es_override and ei_override from a reference record."
                    : $"shaft_letter: no formula gives '{shaftLetter}'; enter es_override and ei_override from a reference record.");
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

            c.Record("es", Iso286.ShaftFundamentalIsUpper(shaftLetter) ? $"fundamental deviation '{shaftLetter}'" : "ei + IT_shaft", shaftLetter == "js" ? "+IT/2" : Iso286.ShaftFundamentalIsUpper(shaftLetter) ? Formula(shaftLetter) : $"{CalcValue.FormatNumber(ei)} + {CalcValue.FormatNumber(itShaft)}", ScalarValue.Of(es, um), "Shaft upper deviation", "ISO 286-1 §5");
            c.Record("ei", Iso286.ShaftFundamentalIsUpper(shaftLetter) ? "es − IT_shaft" : $"fundamental deviation '{shaftLetter}'", shaftLetter == "js" ? "−IT/2" : Iso286.ShaftFundamentalIsUpper(shaftLetter) ? $"{CalcValue.FormatNumber(es)} − {CalcValue.FormatNumber(itShaft)}" : Formula(shaftLetter), ScalarValue.Of(ei, um), "Shaft lower deviation", "ISO 286-1 §5");
        }

        // Hole deviations.
        double ES, EI;

        if (bore && !(c.Has("ES_override") && c.Has("EI_override")))
        {
            c.Block("bearing-bore", "ES_override: a rolling-bearing bore has its own tolerance (ISO 492), not an ISO 286 class; enter its upper and lower deviation (ES_override, EI_override) from the bearing record.");
            return;
        }

        if (c.Has("ES_override") || c.Has("EI_override"))
        {
            if (!(c.Has("ES_override") && c.Has("EI_override")))
            {
                c.Block("input.ES_override", "ES_override/EI_override: give both hole deviations when overriding.");
                return;
            }

            ES = c.InputSi("ES_override") * 1e6;
            EI = c.InputSi("EI_override") * 1e6;
            c.Record("ES", bore ? "ES (bearing bore record, ISO 492)" : "ES (reference record)", "override", ScalarValue.Of(ES, um), "Hole upper deviation");
            c.Record("EI", bore ? "EI (bearing bore record, ISO 492)" : "EI (reference record)", "override", ScalarValue.Of(EI, um), "Hole lower deviation");
        }
        else
        {
            var fundamental = Iso286.HoleFundamentalDeviation(holeLetter, dMm, holeGrade);

            if (fundamental is null)
            {
                c.Block("input.hole_letter", holeLetter.Equals("T", StringComparison.OrdinalIgnoreCase)
                    ? $"hole_letter: the standard has no T hole at {CalcValue.FormatNumber(dMm)} mm; T starts above {CalcValue.FormatNumber(Iso286.TShaftFromMm)} mm. Choose another letter, or enter ES_override and EI_override from a reference record."
                    : $"hole_letter: no formula gives '{holeLetter}'; enter ES_override and EI_override from a reference record.");
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

            c.Record("ES", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? "EI + IT_hole" : $"fundamental deviation '{holeLetter}'", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? $"{CalcValue.FormatNumber(EI)} + {CalcValue.FormatNumber(itHole)}" : "general rule from the shaft deviation", ScalarValue.Of(ES, um), "Hole upper deviation", "ISO 286-1 §5");
            c.Record("EI", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? $"fundamental deviation '{holeLetter}'" : "ES − IT_hole", string.CompareOrdinal(lower, "h") <= 0 && lower != "js" ? "−es of the same letter" : $"{CalcValue.FormatNumber(ES)} − {CalcValue.FormatNumber(itHole)}", ScalarValue.Of(EI, um), "Hole lower deviation", "ISO 286-1 §5");
        }

        c.Assign("D_hole_max", "D + ES", "mm", "Hole maximum");
        c.Assign("D_hole_min", "D + EI", "mm", "Hole minimum");
        c.Assign("d_shaft_max", "D + es", "mm", "Shaft maximum");
        c.Assign("d_shaft_min", "D + ei", "mm", "Shaft minimum");
        c.Assign("c_max", "ES - ei", "µm", "Maximum clearance (negative: minimum interference)");
        c.Assign("c_min", "EI - es", "µm", "Minimum clearance (negative: maximum interference)");
        c.Assign("i_max", "es - EI", "µm", "Maximum interference");
        c.Assign("i_min", "ei - ES", "µm", "Minimum interference (negative: maximum clearance)");

        var cMax = c.Si("c_max");
        var cMin = c.Si("c_min");
        var kind = cMin >= 0 ? "clearance" : cMax <= 0 ? "interference" : "transition";
        var holeName = bore ? "bearing bore" : $"{holeLetter}{holeGrade}";
        c.Record("fit", "fit class", $"{holeName}/{shaftLetter}{shaftGrade}", new TextValue(kind), "Fit kind: clearance, transition or interference");
        c.Assign("c_mean", "(c_max + c_min) / 2", "µm", "Mean clearance");

        c.Output("IT_hole", "Hole tolerance");
        c.Output("IT_shaft", "Shaft tolerance");
        c.Output("ES", "Hole upper deviation");
        c.Output("EI", "Hole lower deviation");
        c.Output("es", "Shaft upper deviation");
        c.Output("ei", "Shaft lower deviation");
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
        c.CheckCondition("Fit classified", true, $"{holeName}/{shaftLetter}{shaftGrade}", kind, "fit");
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
