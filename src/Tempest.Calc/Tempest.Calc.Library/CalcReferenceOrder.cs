using System.Globalization;
using System.Text.RegularExpressions;

namespace Tempest.Calc.Library;

/// <summary>
/// The order a record picker lists its records in (owner, 2026-10-29). Names are compared "naturally": a run of digits
/// is read as a number, so M3 comes before M27 and M30 after it, and S235 before S1100. A metric thread size
/// ("M12 x 1.75") sorts by diameter, then coarse pitch before fine; a property class sorts 4.6, 5.6, 8.8, 10.9, 12.9,
/// then the stainless A2-70, A4-70, A4-80.
/// </summary>
public static partial class CalcReferenceOrder
{
    /// <summary>The longest name read for a thread size or a designation; a longer one is not parsed.</summary>
    public const int MaxNameLength = 120;

    /// <summary>Compares two names with every run of digits read as a number (a decimal part included).</summary>
    public static IComparer<string> Natural { get; } = Comparer<string>.Create(CompareNatural);

    /// <summary>Compares two thread sizes: a metric size by diameter, then the larger (coarse) pitch first; any other name naturally, after the metric ones.</summary>
    public static IComparer<string> ThreadSize { get; } = Comparer<string>.Create(CompareThreadSize);

    /// <summary>Compares two grades or property classes naturally; a missing grade comes last.</summary>
    public static IComparer<string?> Grade { get; } = Comparer<string?>.Create(CompareGrade);

    /// <summary>
    /// The records in picker order: records with a thread size by size, then grade, then label; the others by label.
    /// </summary>
    public static IReadOnlyList<CalcReferenceOption> Order(IEnumerable<CalcReferenceOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options
            .OrderBy(o => o.Size is null ? 1 : 0)
            .ThenBy(o => o.Size ?? string.Empty, ThreadSize)
            .ThenBy(o => o.Grade, Grade)
            .ThenBy(o => o.Label, Natural)
            .ThenBy(o => o.RecordId, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reads a metric thread size from a name: "M12", "M12 x 1.75", "M12×1.25" (the diameter and the pitch, if given).
    /// </summary>
    public static bool TryParseThread(string? text, out double diameter, out double? pitch)
    {
        diameter = 0;
        pitch = null;
        // A name longer than any thread size is not one (and is not matched at all).
        if (text is null || text.Length > MaxNameLength)
            return false;

        var match = ThreadPattern().Match(text);

        if (!match.Success || !TryNumber(match.Groups["d"].Value, out diameter))
            return false;

        if (match.Groups["p"].Success && TryNumber(match.Groups["p"].Value, out var p))
            pitch = p;

        return true;
    }

    /// <summary>
    /// Splits a fastener record into its thread size and its grade. The record's own thread designation and property class
    /// are used where it has them; otherwise both are read from its designation ("M12 x 1.75 - 8.8"). Returns
    /// (<see langword="null"/>, <see langword="null"/>) for a record whose size cannot be read, which then keeps one picker entry.
    /// </summary>
    public static (string? Size, string? Grade) SplitFastener(string? designation, string? threadDesignation, string? propertyClass)
    {
        var grade = string.IsNullOrWhiteSpace(propertyClass) ? null : propertyClass.Trim();
        var match = designation is { Length: <= MaxNameLength } ? DesignationPattern().Match(designation) : Match.Empty;

        if (grade is null && match.Success && match.Groups["g"].Success)
            grade = match.Groups["g"].Value.Trim();

        var size = !string.IsNullOrWhiteSpace(threadDesignation) && TryParseThread(threadDesignation, out _, out _)
            ? Tidy(threadDesignation)
            : match.Success ? Tidy(match.Groups["s"].Value) : null;

        return size is null ? (null, null) : (size, grade);
    }

    /// <summary>"designation — name", unless the designation already ends with the name ("M3 x 0.5 - 8.8", not "M3 x 0.5 - 8.8 — 8.8").</summary>
    public static string Label(string? designation, string? name)
    {
        var d = designation?.Trim() ?? string.Empty;
        var n = name?.Trim() ?? string.Empty;

        if (d.Length == 0)
            return n;

        if (n.Length == 0 || string.Equals(d, n, StringComparison.Ordinal))
            return d;

        // The name is already the designation's last part: after a space, a hyphen or a dash.
        if (d.Length > n.Length && d.EndsWith(n, StringComparison.OrdinalIgnoreCase) && !char.IsLetterOrDigit(d[d.Length - n.Length - 1]))
            return d;

        return $"{d} — {n}";
    }

    private static int CompareThreadSize(string? a, string? b)
    {
        var parsedA = TryParseThread(a, out var da, out var pa);
        var parsedB = TryParseThread(b, out var db, out var pb);

        if (parsedA != parsedB)
            return parsedA ? -1 : 1;

        if (parsedA)
        {
            var byDiameter = da.CompareTo(db);

            if (byDiameter != 0)
                return byDiameter;

            // Coarse before fine: no pitch stated (the coarse series), then the larger pitch first.
            var byPitch = (pb ?? double.MaxValue).CompareTo(pa ?? double.MaxValue);

            if (byPitch != 0)
                return byPitch;
        }

        return CompareNatural(a, b);
    }

    private static int CompareGrade(string? a, string? b)
    {
        if (a is null || b is null)
            return a is null ? (b is null ? 0 : 1) : -1;

        return CompareNatural(a, b);
    }

    private static int CompareNatural(string? a, string? b)
    {
        a ??= string.Empty;
        b ??= string.Empty;
        var i = 0;
        var j = 0;

        while (i < a.Length && j < b.Length)
        {
            var digitA = char.IsAsciiDigit(a[i]);
            var digitB = char.IsAsciiDigit(b[j]);

            if (digitA && digitB)
            {
                var numberA = ReadNumber(a, ref i);
                var numberB = ReadNumber(b, ref j);
                var byValue = numberA.Value.CompareTo(numberB.Value);

                if (byValue != 0)
                    return byValue;

                var byText = string.CompareOrdinal(numberA.Text, numberB.Text);

                if (byText != 0)
                    return byText;

                continue;
            }

            // A number sorts before text ("10.9" before "A2-70").
            if (digitA != digitB)
                return digitA ? -1 : 1;

            var startA = i;
            var startB = j;

            while (i < a.Length && !char.IsAsciiDigit(a[i]))
                i++;

            while (j < b.Length && !char.IsAsciiDigit(b[j]))
                j++;

            var textA = a[startA..i];
            var textB = b[startB..j];
            var byWord = string.Compare(textA, textB, StringComparison.OrdinalIgnoreCase);

            if (byWord != 0)
                return byWord;

            byWord = string.CompareOrdinal(textA, textB);

            if (byWord != 0)
                return byWord;
        }

        return (a.Length - i).CompareTo(b.Length - j);
    }

    /// <summary>Reads a run of digits with at most one decimal part ("0.75", "12"); a name's numbers are capped well inside a double.</summary>
    private static (double Value, string Text) ReadNumber(string s, ref int i)
    {
        var start = i;

        while (i < s.Length && char.IsAsciiDigit(s[i]))
            i++;

        if (i + 1 < s.Length && s[i] == '.' && char.IsAsciiDigit(s[i + 1]))
        {
            i++;

            while (i < s.Length && char.IsAsciiDigit(s[i]))
                i++;
        }

        var text = s[start..i];
        var value = text.Length <= 30 && double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v) ? v : double.MaxValue;
        return (value, text);
    }

    private static bool TryNumber(string text, out double value) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && value > 0 && value < 1e4;

    /// <summary>"M12  x 1.75" as "M12 x 1.75".</summary>
    private static string Tidy(string size)
    {
        var match = ThreadPattern().Match(size);

        if (!match.Success)
            return size.Trim();

        return match.Groups["p"].Success ? $"M{match.Groups["d"].Value} x {match.Groups["p"].Value}" : $"M{match.Groups["d"].Value}";
    }

    [GeneratedRegex(@"^\s*M(?<d>\d{1,4}(?:[.,]\d{1,3})?)(?:\s*[x×X]\s*(?<p>\d{1,2}(?:[.,]\d{1,3})?))?\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex ThreadPattern();

    [GeneratedRegex(@"^\s*(?<s>M\d{1,4}(?:[.,]\d{1,3})?(?:\s*[x×X]\s*\d{1,2}(?:[.,]\d{1,3})?)?)\s*(?:[-–—]\s*(?<g>\S.{0,40}?))?\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 100)]
    private static partial Regex DesignationPattern();
}
