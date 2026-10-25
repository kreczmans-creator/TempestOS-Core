using System.Globalization;
using System.Text.Json.Serialization;

namespace Tempest.Calc.Import;

/// <summary>Which character a file uses for the decimal mark.</summary>
public enum DecimalMark
{
    /// <summary>1234.5</summary>
    Point,

    /// <summary>1234,5</summary>
    Comma,
}

/// <summary>
/// The number format a file is declared to use. Nothing is guessed from the machine's culture or from the digits: the
/// profile states the decimal mark, and optionally the one grouping character (the other mark in a decimal-comma file,
/// <c>1.234,5</c>; a comma in a decimal-point file, <c>1,234.5</c>). A number written the other way is refused, not
/// reinterpreted.
/// </summary>
public sealed record NumberProfile(DecimalMark Decimal = DecimalMark.Point, char? Grouping = null)
{
    /// <summary>Decimal point, no grouping: the default and what every .xlsx stores.</summary>
    public static NumberProfile Point { get; } = new(DecimalMark.Point);

    /// <summary>Decimal comma, no grouping.</summary>
    public static NumberProfile Comma { get; } = new(DecimalMark.Comma);

    [JsonIgnore]
    public char DecimalChar => Decimal == DecimalMark.Comma ? ',' : '.';

    /// <summary>Whether the profile is internally consistent (a grouping character differs from the decimal mark and is one of the usual ones).</summary>
    [JsonIgnore]
    public bool IsValid => Decimal is DecimalMark.Point or DecimalMark.Comma
        && (Grouping is null || (Grouping != DecimalChar && Grouping is ',' or '.' or ' ' or '\u00A0' or '\u2009' or '\''));
}

/// <summary>Strict, culture-independent number reading for imported files.</summary>
public static class LocaleNumber
{
    /// <summary>The longest number text read.</summary>
    public const int MaxChars = 40;

    /// <summary>Reads a finite number written the way <paramref name="profile"/> declares; otherwise returns <see langword="false"/> with the reason.</summary>
    public static bool TryParse(string? text, NumberProfile profile, out double value, out string problem) =>
        TryParse(text, profile, out value, out problem, out _);

    /// <summary>
    /// As the four-argument form; <paramref name="usedGrouping"/> says the profile's grouping character was found in the number and
    /// removed (<c>1.500</c> read as 1500 under a decimal-comma profile that groups with a point), so a caller can say how the
    /// value was read.
    /// </summary>
    public static bool TryParse(string? text, NumberProfile profile, out double value, out string problem, out bool usedGrouping)
    {
        ArgumentNullException.ThrowIfNull(profile);
        value = 0;
        problem = string.Empty;
        usedGrouping = false;

        if (!profile.IsValid)
        {
            problem = "The number format is not valid (the grouping character cannot be the decimal mark).";
            return false;
        }

        var s = text?.Trim(' ', '\t') ?? string.Empty;

        if (s.Length == 0)
        {
            problem = "The number is empty.";
            return false;
        }

        if (s.Length > MaxChars)
        {
            problem = $"A number is at most {MaxChars} characters.";
            return false;
        }

        var negative = false;
        var start = 0;

        if (s[0] is '+' or '-')
        {
            negative = s[0] == '-';
            start = 1;
        }

        var end = s.IndexOfAny(['e', 'E'], start);
        var body = end < 0 ? s[start..] : s[start..end];
        var mark = profile.DecimalChar;
        var markAt = body.IndexOf(mark, StringComparison.Ordinal);

        if (markAt >= 0 && body.IndexOf(mark, markAt + 1) >= 0)
        {
            problem = $"'{Clip(s)}' has more than one '{mark}'.";
            return false;
        }

        var integer = markAt < 0 ? body : body[..markAt];
        var fraction = markAt < 0 ? string.Empty : body[(markAt + 1)..];

        if (integer.Length == 0 && fraction.Length == 0)
        {
            problem = $"'{Clip(s)}' is not a number.";
            return false;
        }

        if (profile.Grouping is { } group && integer.Contains(group, StringComparison.Ordinal))
        {
            var parts = integer.Split(group);

            if (parts[0].Length is < 1 or > 3 || parts.Skip(1).Any(static p => p.Length != 3))
            {
                problem = $"'{Clip(s)}' groups digits irregularly.";
                return false;
            }

            integer = string.Concat(parts);
            usedGrouping = true;
        }

        if (!AllDigits(integer) || !AllDigits(fraction))
        {
            problem = s.Contains(',', StringComparison.Ordinal) || s.Contains('.', StringComparison.Ordinal)
                ? $"'{Clip(s)}' is not written with '{mark}' as the decimal mark, which is how this file's numbers are declared. If the file uses the other mark, choose the other number format."
                : $"'{Clip(s)}' is not a number.";
            return false;
        }

        var exponent = string.Empty;

        if (end >= 0)
        {
            var e = s[(end + 1)..];
            var sign = string.Empty;

            if (e.Length > 0 && e[0] is '+' or '-')
            {
                sign = e[0].ToString();
                e = e[1..];
            }

            if (e.Length is 0 or > 4 || !AllDigits(e))
            {
                problem = $"'{Clip(s)}' has a malformed exponent.";
                return false;
            }

            exponent = "e" + sign + e;
        }

        var invariant = $"{(negative ? "-" : string.Empty)}{(integer.Length == 0 ? "0" : integer)}{(fraction.Length > 0 ? "." + fraction : string.Empty)}{exponent}";

        if (!double.TryParse(invariant, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value))
        {
            value = 0;
            usedGrouping = false;
            problem = $"'{Clip(s)}' is outside the range of a number.";
            return false;
        }

        // A non-zero literal that is too small to represent reads as 0, which would be a silent reinterpretation; refuse it as an overflow is refused.
        if (value == 0 && (integer.AsSpan().ContainsAnyExcept('0') || fraction.AsSpan().ContainsAnyExcept('0')))
        {
            value = 0;
            usedGrouping = false;
            problem = $"'{Clip(s)}' is too small to represent and would be read as 0.";
            return false;
        }

        return true;
    }

    /// <summary>As <see cref="TryParse"/>, throwing <see cref="ImportRefusedException"/> (<see cref="ImportRefusalCode.NumberFormat"/>) on failure.</summary>
    public static double Parse(string? text, NumberProfile profile)
    {
        if (TryParse(text, profile, out var value, out var problem))
            return value;

        throw new ImportRefusedException(ImportRefusalCode.NumberFormat, problem);
    }

    /// <summary>
    /// Whether <paramref name="text"/> is a number in the other decimal mark's notation (<c>1,5</c> in a decimal-point file):
    /// the telltale of a file whose number format was declared wrong. Used only to word a notice.
    /// </summary>
    public static bool LooksLikeOtherMark(string text, NumberProfile profile)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(profile);
        var other = profile.Decimal == DecimalMark.Point ? NumberProfile.Comma : NumberProfile.Point;
        return TryParse(text, other, out _, out _) && !TryParse(text, profile, out _, out _);
    }

    private static bool AllDigits(string s)
    {
        foreach (var c in s)
        {
            if (c is < '0' or > '9')
                return false;
        }

        return true;
    }

    private static string Clip(string s) => s.Length > 24 ? s[..24] + "…" : s;
}
