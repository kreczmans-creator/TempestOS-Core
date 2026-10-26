namespace Tempest.Calc.Mechanical;

/// <summary>
/// ISO 286-1 limits and fits from the published formulae, in this
/// assembly's own notation. Nothing tabulated is reproduced: the standard
/// tolerance unit, the IT grade multipliers and the fundamental-deviation
/// formulae are evaluated at the geometric mean of the nominal size's
/// main diameter step, which is how the tables themselves are derived.
/// Results are the unrounded formula values. For the clearance and
/// transition letters (a–n) the tabulated values differ by the standard's
/// own rounding (typically ≤ 1 µm or ~2 %); for the interference letters
/// p–zc the standard's tables also add its own increments and sub-steps,
/// so the formula value can be several µm below the tabulated one
/// (see <see cref="InterferenceLetterNote"/>).
/// Values are in micrometres for a nominal size in millimetres.
/// </summary>
public static class Iso286
{
    /// <summary>The upper limits of the main diameter steps (ISO 286-1 §4), in mm: 3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150.</summary>
    private static readonly double[] StepUpper = [3, 6, 10, 18, 30, 50, 80, 120, 180, 250, 315, 400, 500, 630, 800, 1000, 1250, 1600, 2000, 2500, 3150];

    /// <summary>Relative slack on a step's upper limit: a size typed as exactly the limit (18 mm) may arrive a few ulp above it after a unit conversion (18 mm × 0.001 × 1000 = 18.000000000000004).</summary>
    private const double StepSlack = 1e-9;

    public const double MinimumSizeMm = 0.0;
    public const double MaximumSizeMm = 3150.0;

    /// <summary>The main diameter step (lower, upper] containing <paramref name="sizeMm"/>; sizes up to 3 mm use the first step.</summary>
    public static (double Lower, double Upper) Step(double sizeMm)
    {
        if (!(sizeMm > MinimumSizeMm) || sizeMm > MaximumSizeMm * (1.0 + StepSlack))
            throw new ArgumentOutOfRangeException(nameof(sizeMm), sizeMm, "ISO 286 covers nominal sizes above 0 up to 3150 mm.");

        var lower = 0.0;

        foreach (var upper in StepUpper)
        {
            if (sizeMm <= upper * (1.0 + StepSlack))
                return (lower, upper);

            lower = upper;
        }

        return (lower, MaximumSizeMm);
    }

    /// <summary>Geometric mean of the diameter step, the size the formulae are evaluated at (for the first step, √(1 × 3)).</summary>
    public static double StepMean(double sizeMm)
    {
        var (lower, upper) = Step(sizeMm);
        return Math.Sqrt(Math.Max(lower, 1.0) * upper);
    }

    /// <summary>Standard tolerance unit: i = 0.45·D^(1/3) + 0.001·D for D ≤ 500 mm; I = 0.004·D + 2.1 for D > 500 mm (µm, D in mm).</summary>
    public static double ToleranceUnit(double sizeMm)
    {
        var d = StepMean(sizeMm);
        return d <= 500 ? 0.45 * Math.Cbrt(d) + 0.001 * d : 0.004 * d + 2.1;
    }

    /// <summary>The standard tolerance IT<paramref name="grade"/> in µm (grades 01 → -1, 0 → 0, 1…18).</summary>
    public static double Tolerance(double sizeMm, int grade)
    {
        if (grade is < -1 or > 18)
            throw new ArgumentOutOfRangeException(nameof(grade), grade, "IT grades run from IT01 (−1) to IT18.");

        var d = StepMean(sizeMm);
        var i = ToleranceUnit(sizeMm);

        if (d <= 500)
        {
            switch (grade)
            {
                case -1: return 0.3 + 0.008 * d;
                case 0: return 0.5 + 0.012 * d;
                case 1: return 0.8 + 0.020 * d;
                case 2:
                case 3:
                case 4:
                {
                    // IT2–IT4: geometric progression between IT1 and IT5.
                    var it1 = 0.8 + 0.020 * d;
                    var it5 = 7 * i;
                    return it1 * Math.Pow(it5 / it1, (grade - 1) / 4.0);
                }
            }
        }

        var multiplier = grade switch
        {
            <= 1 => 2.0 * Math.Pow(10, (grade - 1) / 5.0),
            2 => 2.7,
            3 => 3.7,
            4 => 5.0,
            5 => 7.0,
            6 => 10.0,
            7 => 16.0,
            8 => 25.0,
            9 => 40.0,
            10 => 64.0,
            11 => 100.0,
            12 => 160.0,
            13 => 250.0,
            14 => 400.0,
            15 => 640.0,
            16 => 1000.0,
            17 => 1600.0,
            _ => 2500.0,
        };

        return multiplier * i;
    }

    /// <summary>The letters whose tabulated values are not given by the formulae alone: p to zc add the standard's own 0 to 5 µm increments and sub-steps over the size ranges.</summary>
    public static bool IsInterferenceLetter(string letter) => string.CompareOrdinal(letter.ToLowerInvariant(), "p") >= 0 && letter.ToLowerInvariant() != "js";

    /// <summary>The note printed with a fit that uses an interference letter from the formulae.</summary>
    public const string InterferenceLetterNote = "the formula value of a letter from p to zc can lie several µm (up to about 9 µm in the 100 to 565 mm checks, p6 at 450 mm 62.2 against 68 µm tabulated) below the tabulated deviation, because the standard's tables add their own increments and sub-steps; the maximum interference is then understated";

    /// <summary>The size above which the standard has a t shaft (24 mm); below it there is none, and the 24 to 30 mm sub-step has its own mean diameter √(24·30).</summary>
    public const double TShaftFromMm = 24.0;

    public static readonly IReadOnlyList<string> ShaftLetters = ["a", "b", "c", "cd", "d", "e", "ef", "f", "fg", "g", "h", "js", "j", "k", "m", "n", "p", "r", "s", "t", "u", "v", "x", "y", "z", "za", "zb", "zc"];

    public static readonly IReadOnlyList<string> HoleLetters = ["A", "B", "C", "CD", "D", "E", "EF", "F", "FG", "G", "H", "JS", "J", "K", "M", "N", "P", "R", "S", "T", "U", "V", "X", "Y", "Z", "ZA", "ZB", "ZC"];

    /// <summary>
    /// The shaft fundamental deviation (the limit nearer the zero line) in
    /// µm, from the ISO 286-1 formulae; the sign is the deviation's own.
    /// For a–h the upper deviation es; for j–zc the lower deviation ei.
    /// Returns <see langword="null"/> for a letter the formulae do not give
    /// (a, b, c, j), which must come from a reference record.
    /// </summary>
    public static double? ShaftFundamentalDeviation(string letter, double sizeMm, int grade)
    {
        var d = StepMean(sizeMm);

        return letter switch
        {
            "cd" => -(Math.Sqrt(ShaftFundamentalDeviation("c", sizeMm, grade) is { } c ? -c : 0) * Math.Sqrt(16 * Math.Pow(d, 0.44))),
            "d" => -16 * Math.Pow(d, 0.44),
            "e" => -11 * Math.Pow(d, 0.41),
            "ef" => -Math.Sqrt(11 * Math.Pow(d, 0.41) * 5.5 * Math.Pow(d, 0.41)),
            "f" => -5.5 * Math.Pow(d, 0.41),
            "fg" => -Math.Sqrt(5.5 * Math.Pow(d, 0.41) * 2.5 * Math.Pow(d, 0.34)),
            "g" => -2.5 * Math.Pow(d, 0.34),
            "h" => 0.0,
            "js" => -Tolerance(sizeMm, grade) / 2,
            "k" => grade is >= 4 and <= 7 ? 0.6 * Math.Cbrt(d) : 0.0,
            "m" => Tolerance(sizeMm, 7) - Tolerance(sizeMm, 6),
            "n" => 5 * Math.Pow(d, 0.34),
            "p" => d > 500 ? 0.072 * d + 37.8 : Tolerance(sizeMm, 7) + 0.0,
            "r" => Math.Sqrt((Tolerance(sizeMm, 7) + 0.0) * S(d, sizeMm)),
            "s" => S(d, sizeMm),
            "t" => sizeMm > TShaftFromMm ? Tolerance(sizeMm, 7) + 0.63 * (sizeMm <= 30 ? Math.Sqrt(TShaftFromMm * 30) : d) : null,
            "u" => Tolerance(sizeMm, 7) + d,
            "v" => d > 14 ? Tolerance(sizeMm, 7) + 1.25 * d : Tolerance(sizeMm, 7) + d,
            "x" => Tolerance(sizeMm, 7) + 1.6 * d,
            "y" => d > 18 ? Tolerance(sizeMm, 7) + 2 * d : Tolerance(sizeMm, 7) + 1.6 * d,
            "z" => Tolerance(sizeMm, 7) + 2.5 * d,
            "za" => Tolerance(sizeMm, 8) + 3.15 * d,
            "zb" => Tolerance(sizeMm, 9) + 4 * d,
            "zc" => Tolerance(sizeMm, 10) + 5 * d,
            _ => null,
        };

        static double S(double d, double sizeMm) => d <= 50 ? Tolerance(sizeMm, 8) + 1.0 : Tolerance(sizeMm, 7) + 0.4 * d;
    }

    /// <summary>
    /// The hole fundamental deviation in µm by the general rule: for A–H,
    /// EI = −es of the same letter; for JS, ES = +IT/2; for K–ZC, ES = −ei
    /// plus Δ = IT(n) − IT(n−1) for K, M, N up to IT8 and P–ZC up to IT7.
    /// Returns <see langword="null"/> where the shaft letter has no formula.
    /// </summary>
    public static double? HoleFundamentalDeviation(string letter, double sizeMm, int grade)
    {
        var lower = letter.ToLowerInvariant();

        if (lower == "js")
            return Tolerance(sizeMm, grade) / 2;

        // K, M and N up to IT8 take the deviation of the shaft letter at the grade the table lists it for: the k formula is 0 at IT8, but a K8 hole keeps the k deviation of IT4 to IT7 (mech.iso286-fit-03).
        var shaft = ShaftFundamentalDeviation(lower, sizeMm, lower == "k" && grade == 8 ? 7 : grade);

        if (shaft is null)
            return null;

        if (string.CompareOrdinal(lower, "h") <= 0 || lower is "cd" or "ef" or "fg")
            return -shaft.Value;

        // ISO 286-1: for N above IT8 (sizes over 3 mm) the upper deviation is zero, not −5·D^0.34.
        if (lower == "n" && grade > 8 && StepMean(sizeMm) > 3.0)
            return 0.0;

        var delta = (lower is "k" or "m" or "n" && grade <= 8) || (string.CompareOrdinal(lower, "p") >= 0 && grade <= 7)
            ? Tolerance(sizeMm, grade) - Tolerance(sizeMm, grade - 1)
            : 0.0;

        return -shaft.Value + delta;
    }

    /// <summary>Whether a shaft letter's fundamental deviation is an upper deviation (a–h) rather than a lower one (j–zc).</summary>
    public static bool ShaftFundamentalIsUpper(string letter) => string.CompareOrdinal(letter.ToLowerInvariant(), "h") <= 0 && letter.ToLowerInvariant() is not "js";
}
