using System.Globalization;
using System.Numerics;

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
/// expected value as the script prints it; the shown value as the form shows it, which is five significant figures with trailing zeros
/// dropped). That is exactly the allowance two roundings of one true value can differ by, so a correct result never fails and a wrong one
/// that differs in a digit both show does. A text (a governing PASS or FAIL, a choice) agrees only when it is identical. The arithmetic is
/// exact (integers scaled by powers of ten), never a floating-point comparison, so a value on the boundary is decided the same way on every machine.
/// </summary>
public static class GuidedTolerance
{
    /// <summary>The significant figures the worksheet form shows an output to.</summary>
    public const int FormSignificantFigures = 5;

    /// <summary>The rule, in the words shown to the tester.</summary>
    public const string RuleText = "A number passes when it differs from the expected value by no more than half a unit in the last digit of the less precise of the two, counting the digits as written (the form shows 5 significant figures). A text must be identical.";

    private const int MaxExponent = 300;

    /// <summary>Compares the form's shown output (a number and its unit, as <c>CalcOutput.ValueText</c> prints it) with an expected number and unit.</summary>
    /// <param name="symbol">The output's symbol, named in the reason.</param>
    /// <param name="expectedValue">The expected number text exactly as the script writes it.</param>
    /// <param name="expectedUnit">The expected unit text (empty for a ratio).</param>
    /// <param name="shown">What the form shows (for example <c>4290.3 h</c>).</param>
    /// <param name="shownSignificantFigures">The significant figures the form rounds this value to (5 for an output, 3 for a utilisation).</param>
    public static GuidedComparison CompareShown(string symbol, string expectedValue, string expectedUnit, string? shown, int shownSignificantFigures = FormSignificantFigures)
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

        return CompareNumbers(symbol, expected, actual, expectedValue, shownNumber, Math.Clamp(shownSignificantFigures, 1, 15));
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

        return CompareNumbers(symbol, expected, actual, expectedValue, shownValue.Trim(), formSignificantFigures: null);
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

    /// <summary>The allowance for an expected value as written, as text (for example <c>±0.05</c>): half a unit in its last digit; <c>exact text</c> for a text.</summary>
    public static string AllowanceText(string expectedValue)
    {
        ArgumentNullException.ThrowIfNull(expectedValue);
        return !TryParse(expectedValue, out var n) ? "exact text" : n.Digits.IsZero && n.Unit >= 0 ? "exactly zero" : "±" + HalfUnitText(n.Unit);
    }

    private static GuidedComparison CompareNumbers(string symbol, Decimal10 expected, Decimal10 actual, string expectedText, string shownText, int? formSignificantFigures)
    {
        // The last place of the shown value. A value the form wrote is rounded to its significant figures and has its trailing zeros dropped, so
        // its last place is that figure's place whatever was written ('4290' is 4290.0; '2494200' is 2.4942E+6), not the last digit that happens to show.
        var shownUnit = formSignificantFigures is { } figures ? actual.Magnitude - (figures - 1) : actual.Unit;

        // An expected zero written as a bare '0' is an exact zero, not "zero to the nearest whole number": it adds no coarseness of its own.
        var expectedUnit = expected.Digits.IsZero && expected.Unit >= 0 ? shownUnit : expected.Unit;

        // 'unit' is the exponent of the coarser last place; the allowance is half of 10^unit.
        var unit = Math.Max(expectedUnit, shownUnit);

        // Both numbers in whole multiples of 10^-s, s being the finer written scale, so the arithmetic is exact.
        var s = -Math.Min(Math.Min(expected.Unit, actual.Unit), unit);
        var difference = BigInteger.Abs(expected.AtScale(s) - actual.AtScale(s));
        var twiceAllowance = BigInteger.Pow(10, s + unit);

        var applied = "±" + HalfUnitText(unit);

        return 2 * difference <= twiceAllowance
            ? new(GuidedCompareOutcome.Match, $"{symbol}: {shownText} agrees with {expectedText} (allowance {applied}).", applied)
            : new(GuidedCompareOutcome.Mismatch, $"{symbol}: {shownText} differs from {expectedText} by more than {applied}.", applied);
    }

    private static string HalfUnitText(int unitExponent)
    {
        // Half of 10^unit is 5 × 10^(unit - 1).
        var exponent = unitExponent - 1;
        return exponent >= 0 ? "5" + new string('0', exponent) : "0." + new string('0', -exponent - 1) + "5";
    }

    private static (string Number, string Unit) SplitUnit(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);
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
