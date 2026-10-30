using System.Globalization;
using System.Numerics;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library.ManualTesting;

/// <summary>How a shown value compares with an expected one.</summary>
public enum GuidedCompareOutcome
{
    /// <summary>The two agree to the rule.</summary>
    Match,

    /// <summary>The two differ by more than the rule allows.</summary>
    Mismatch,

    /// <summary>The two cannot be compared by rule (a unit or a form the rule does not cover): the tester decides.</summary>
    NotComparable,
}

/// <summary>The result of one comparison, with a one-sentence reason that names the quantity.</summary>
/// <param name="Outcome">Match, Mismatch or NotComparable.</param>
/// <param name="Detail">The reason, in one sentence.</param>
/// <param name="Allowance">For a number compared by the rule: the allowance actually applied (for example <c>±0.5</c>), which is the coarser of the expected value's last place and the form's displayed last place; <see langword="null"/> for a text or a comparison the rule did not decide.</param>
public sealed record GuidedComparison(GuidedCompareOutcome Outcome, string Detail, string? Allowance = null)
{
    /// <summary>Whether the values agree.</summary>
    public bool IsMatch => Outcome == GuidedCompareOutcome.Match;
}

/// <summary>
/// The one rule the Guided test compares by (Addendum C, C-5), stated on screen as <see cref="RuleText"/>. A number agrees with the expected
/// value when they differ by no more than half a unit in the last digit of the less precise of the two, the digits counted as written (the
/// expected value as the script prints it; the shown value as the form shows it, so its last digit is the last one printed,
/// <see cref="CalcDisplay.ShownResolution"/>: 144.9 MPa is 0.1, 0.003400 N·m is 0.000001, 150000 h is 1 and a utilisation of 0.73 is 0.01). That is exactly the allowance two roundings of one true value can differ by, so a correct result never fails and a wrong one
/// that differs in a digit both show does. A text (a governing PASS or FAIL, a choice) agrees only when it is identical. The arithmetic is
/// exact (integers scaled by powers of ten), never a floating-point comparison, so a value on the boundary is decided the same way on every machine.
/// </summary>
public static class GuidedTolerance
{
    /// <summary>The rule, in the words shown to the tester.</summary>
    public const string RuleText = "A number passes when it differs from the expected value by no more than half a unit in the last digit of the less precise of the two, counting the digits as written (the form's own last digit is the last one it prints: 4 or 5 significant figures, a whole number exactly, a utilisation to two decimals), and by no more than 1 % of the expected value unless the form's own last digit is coarser. A text must be identical.";

    /// <summary>
    /// The most an output the form shows may differ from the expected value, in per cent of it, whatever few digits the expected value is written with
    /// (the worked examples' own verification tolerance, <c>CalcExpectedValue.RelativeTolerance</c>, 1 % by default). Without it an expected value
    /// written as an exact '1' or '5' allowed ±0.5, so a utilisation of 1.4 against an expected 1, or 5.49 A against 5 A, was suggested as a Pass
    /// (security review SEC-C-02). The form's own last displayed digit is never refused: half of it is always allowed.
    /// </summary>
    public const int ShownRelativeCapPercent = 1;

    private const int MaxExponent = 300;

    /// <summary>Compares the form's shown output (a number and its unit, as <c>CalcOutput.ValueText</c> prints it) with an expected number and unit.</summary>
    /// <param name="symbol">The output's symbol, named in the reason.</param>
    /// <param name="expectedValue">The expected number text exactly as the script writes it.</param>
    /// <param name="expectedUnit">The expected unit text (empty for a ratio).</param>
    /// <param name="shown">What the form shows (for example <c>4290.3 h</c>).</param>
    /// <param name="shownRounding">How the form rounded it. To the nearest (the default): the last shown digit is off by at most half of itself. Down or up (a safety factor, a failing utilisation): by at most the whole digit, on the side the form rounds to.</param>
    public static GuidedComparison CompareShown(string symbol, string expectedValue, string expectedUnit, string? shown, CalcDisplay.Rounding shownRounding = CalcDisplay.Rounding.Nearest)
    {
        ArgumentNullException.ThrowIfNull(expectedValue);
        ArgumentNullException.ThrowIfNull(expectedUnit);

        if (string.IsNullOrWhiteSpace(shown))
            return new(GuidedCompareOutcome.NotComparable, $"{symbol}: the form shows no value for it.");

        // An expected text (a choice, PASS) is compared whole with what the form shows; a number is compared after its unit is split off.
        if (!TryParse(expectedValue, out var expected))
            return CompareText(symbol, expectedUnit.Length == 0 ? expectedValue : $"{expectedValue} {expectedUnit}", shown);

        var (shownNumber, shownUnit) = SplitUnit(shown.Trim());

        if (!SameUnit(expectedUnit, shownUnit))
            return new(GuidedCompareOutcome.NotComparable, $"{symbol}: the form shows '{shownUnit}' where the script expects '{expectedUnit}'; compare them by hand.");

        if (!TryParse(shownNumber, out var actual))
            return new(GuidedCompareOutcome.Mismatch, $"{symbol}: the form shows '{shownNumber}' where the script expects the number {expectedValue}.");

        // The allowance comes from the digits the form actually shows, never from a count of figures it is assumed to show (C2-22).
        return CompareNumbers(symbol, expected, actual, expectedValue, shownNumber, CalcDisplay.ShownResolutionExponent(shownNumber) ?? actual.Unit, ShownRelativeCapPercent, shownRounding != CalcDisplay.Rounding.Nearest, IsExactWholeNumber(shownNumber));
    }

    /// <summary>Compares two numbers given as text, the second exactly as the tester read it (its digits as written; no form rounding assumed).</summary>
    public static GuidedComparison CompareNumbers(string symbol, string expectedValue, string shownValue)
    {
        ArgumentNullException.ThrowIfNull(expectedValue);
        ArgumentNullException.ThrowIfNull(shownValue);

        if (!TryParse(expectedValue, out var expected))
            return CompareText(symbol, expectedValue, shownValue);

        if (!TryParse(shownValue, out var actual))
            return new(GuidedCompareOutcome.Mismatch, $"{symbol}: '{shownValue.Trim()}' is not a number; the script expects {expectedValue}.");

        return CompareNumbers(symbol, expected, actual, expectedValue, shownValue.Trim(), shownPlace: null, capPercent: null, directed: false, shownExact: false);
    }

    /// <summary>Two texts agree only when identical (ordinal, after trimming the ends).</summary>
    public static GuidedComparison CompareText(string symbol, string expected, string shown)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(shown);

        return string.Equals(expected.Trim(), shown.Trim(), StringComparison.Ordinal)
            ? new(GuidedCompareOutcome.Match, $"{symbol}: '{shown.Trim()}' is identical to the expected text.")
            : new(GuidedCompareOutcome.Mismatch, $"{symbol}: the form shows '{shown.Trim()}' where the script expects '{expected.Trim()}'.");
    }

    /// <summary>The allowance for an expected value as written, as text (for example <c>±0.05</c>): half a unit in its last digit, or <see cref="ShownRelativeCapPercent"/> of the value when that is less; <c>exact text</c> for a text.</summary>
    public static string AllowanceText(string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(expectedValue);
        if (!TryParse(expectedValue, out var n))
            return "exact text";

        if (n.Digits.IsZero && n.Unit >= 0)
            return "exactly zero";

        // The cap applies when 1 % of the value is less than half its last written place (at the scale of that place: |digits| · percent < 50).
        return BigInteger.Abs(n.Digits) * ShownRelativeCapPercent < 50
            ? $"±{ShownRelativeCapPercent.ToString(CultureInfo.InvariantCulture)} % of {expectedValue.Trim()}"
            : "±" + HalfUnitText(n.Unit);
    }

    private static GuidedComparison CompareNumbers(string symbol, Decimal10 expected, Decimal10 actual, string expectedText, string shownText, int? shownPlace, int? capPercent, bool directed, bool shownExact)
    {
        // The last place of the shown value: the power of ten of the last digit the form printed ('144.9' is 0.1, '150000' is 1, '0.003400' is 0.000001).
        var shownUnit = shownPlace ?? actual.Unit;

        // An expected zero written as a bare '0' is an exact zero, not "zero to the nearest whole number": it adds no coarseness of its own.
        var expectedUnit = expected.Digits.IsZero && expected.Unit >= 0 ? shownUnit : expected.Unit;

        // 'unit' is the exponent of the coarser last place; the allowance is half of 10^unit. A short whole number the form printed (a '4', a '150') is exact: the form
        // prints fewer than four figures only for a whole number, so there is no rounding in it and only the expected value's own last place counts.
        var unit = shownExact ? expectedUnit : Math.Max(expectedUnit, shownUnit);

        // Both numbers in whole multiples of 10^-s, s being the finer written scale (and the shown value's last place), so the arithmetic is exact.
        var s = -Math.Min(Math.Min(Math.Min(expected.Unit, actual.Unit), unit), shownUnit);
        var difference = BigInteger.Abs(expected.AtScale(s) - actual.AtScale(s));

        // Twice the allowance, times 100 so that a cap in whole per cent stays an integer: the rule's half unit, capped at capPercent of the expected
        // value, but never below half of the form's own last displayed place (a correct value rounded to the form's figures always agrees).
        var rule = 100 * BigInteger.Pow(10, s + unit);

        // A form that rounds in a direction (a safety factor down, a failing utilisation up) is off by up to the whole last digit it prints, not half of it.
        var display = shownExact ? BigInteger.Zero : (directed ? 200 : 100) * BigInteger.Pow(10, s + shownUnit);
        var displayText = directed ? WholeUnitText(shownUnit) : HalfUnitText(shownUnit);

        if (!shownExact && rule < display)
        {
            rule = display;
            unit = shownUnit;
        }

        var allowed = rule;
        var applied = "±" + (directed && !shownExact && unit == shownUnit ? displayText : HalfUnitText(unit));

        if (capPercent is { } percent)
        {
            var cap = 2 * percent * BigInteger.Abs(expected.AtScale(s));

            if (cap < rule)
            {
                allowed = BigInteger.Max(cap, display);
                applied = cap >= display ? $"±{percent.ToString(CultureInfo.InvariantCulture)} % of {expectedText}" : "±" + displayText;
            }
        }

        return 200 * difference <= allowed
            ? new(GuidedCompareOutcome.Match, $"{symbol}: {shownText} agrees with {expectedText} (allowance {applied}).", applied)
            : new(GuidedCompareOutcome.Mismatch, $"{symbol}: {shownText} differs from {expectedText} by more than {applied}.", applied);
    }

    /// <summary>A whole number of one to three figures ("4", "150", "-12"): the form prints so few figures only for a number that is exactly whole.</summary>
    private static bool IsExactWholeNumber(string number)
    {
        var t = number.Trim().TrimStart('-', '+', '−');
        return t.Length is >= 1 and <= 3 && t.All(char.IsAsciiDigit);
    }

    private static string WholeUnitText(int unitExponent) => unitExponent >= 0 ? "1" + new string('0', unitExponent) : "0." + new string('0', -unitExponent - 1) + "1";

    private static string HalfUnitText(int unitExponent)
    {
        // Half of 10^unit is 5 × 10^(unit - 1).
        var exponent = unitExponent - 1;
        return exponent >= 0 ? "5" + new string('0', exponent) : "0." + new string('0', -exponent - 1) + "5";
    }

    private static (string Number, string Unit) SplitUnit(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);

        // The form writes a degree sign straight after its number ("40.01°"), every other unit after a space.
        if (space < 0 && text.EndsWith('°'))
            return (text[..^1], "°");

        return space < 0 ? (text, string.Empty) : (text[..space], text[(space + 1)..].Trim());
    }

    /// <summary>
    /// The same unit written the README's way and the form's way. A pressure basis is part of the unit: 'bar a' (absolute) and 'bar g' (gauge) are
    /// not each other, and a bare 'bar' states no basis, so neither is the same as it; those are left to the tester rather than guessed (the two
    /// differ by the atmosphere, about 1.013 bar).
    /// </summary>
    private static bool SameUnit(string expected, string shown) =>
        string.Equals(NormaliseUnit(expected), NormaliseUnit(shown), StringComparison.Ordinal);

    /// <summary>Units written the README's way (ASCII) and the form's way compare equal: spaces dropped, '°C' as 'degC', 'Ω' as 'ohm', either micro sign alike, '·' and '*' alike.</summary>
    private static string NormaliseUnit(string unit)
    {
        var text = unit.Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("°C", "degC", StringComparison.Ordinal).Replace("°", "deg", StringComparison.Ordinal)
            .Replace("Ω", "ohm", StringComparison.Ordinal).Replace("μ", "µ", StringComparison.Ordinal)
            .Replace("·", "*", StringComparison.Ordinal).Replace("−", "-", StringComparison.Ordinal);

        // Superscript digits ('cm⁴', 'm⁻¹') as the README writes them ('cm^4', 'm^-1').
        return SuperscriptRun.Replace(text, match => "^" + new string([.. match.Value.Select(c => SuperscriptDigits[c])]));
    }

    private static readonly System.Text.RegularExpressions.Regex SuperscriptRun = new("[⁻⁰¹²³⁴⁵⁶⁷⁸⁹]+", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    private static readonly Dictionary<char, char> SuperscriptDigits = new()
    {
        ['⁻'] = '-', ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4', ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
    };

    /// <summary>A decimal number as exact integer digits, the power of ten of its last written digit (<c>Unit</c>) and of its leading digit (<c>Magnitude</c>).</summary>
    private readonly record struct Decimal10(BigInteger Digits, int Unit, int Magnitude)
    {
        /// <summary>The number as a whole multiple of 10^-<paramref name="scale"/> (the scale is at least as fine as the number's own).</summary>
        public BigInteger AtScale(int scale) => Digits * BigInteger.Pow(10, scale + Unit);
    }

    /// <summary>Parses '-12.50', '1.23457E-05', '.5' or '+3' (invariant); anything else, an exponent beyond ±300 or an absurd length is not a number to the rule.</summary>
    private static bool TryParse(string text, out Decimal10 number)
    {
        number = default;
        var t = text.Trim().Replace('−', '-');

        if (t.Length is 0 or > 60)
            return false;

        var exponent = 0;
        var e = t.IndexOfAny(['e', 'E']);

        if (e >= 0)
        {
            if (!int.TryParse(t[(e + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || Math.Abs(exponent) > MaxExponent)
                return false;

            t = t[..e];
        }

        var negative = false;

        if (t.StartsWith('-') || t.StartsWith('+'))
        {
            negative = t[0] == '-';
            t = t[1..];
        }

        var dot = t.IndexOf('.', StringComparison.Ordinal);
        var whole = dot < 0 ? t : t[..dot];
        var fraction = dot < 0 ? string.Empty : t[(dot + 1)..];

        if (whole.Length + fraction.Length == 0 || !whole.All(char.IsAsciiDigit) || !fraction.All(char.IsAsciiDigit))
            return false;

        var all = whole + fraction;
        var digits = BigInteger.Parse(all, CultureInfo.InvariantCulture);
        var unit = exponent - fraction.Length;

        // The magnitude is the exponent of the leading digit; a zero has none, so its own last place stands in.
        var leading = all.TrimStart('0');
        var magnitude = leading.Length == 0 ? unit : unit + leading.Length - 1;
        number = new Decimal10(negative ? -digits : digits, unit, magnitude);
        return true;
    }
}
