using System.Globalization;

namespace Tempest.Calc.Library;

/// <summary>
/// The revision labels of a calculation or a pack (DC-15): A, B, … Z, then AA, AB, … AZ, BA, … ZZ, AAA, … as spreadsheet columns are lettered.
/// A calculation whose label is a number goes on counting numbers. Before DC-15 the label after Z was <c>Z+</c>, then <c>Z++</c>; those labels
/// may still be stored, so they are read as the 27th, 28th … revision (they are never written again), and the revision after one of them carries
/// on in letters. Any other label (one typed by hand elsewhere) is not understood: it gets a <c>+</c>, as it always did.
/// </summary>
public static class CalcRevisionLabel
{
    /// <summary>The longest letter label read as a number: 12 letters reach 26^12, which fits a 64-bit count with room to spare.</summary>
    public const int MaxLetters = 12;

    private const int MaxLegacyPluses = 10_000;

    /// <summary>The label of the revision after <paramref name="revision"/>.</summary>
    public static string Next(string revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        if (TryParseNumber(revision, out var n))
            return (n + 1).ToString(CultureInfo.InvariantCulture);

        if (TryParseLetters(revision, out var position))
            return FromPosition(position + 1);

        return revision + "+";
    }

    /// <summary>The letter label at <paramref name="position"/> (1 = A, 26 = Z, 27 = AA, 52 = AZ, 53 = BA, 702 = ZZ, 703 = AAA).</summary>
    public static string FromPosition(long position)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(position, 1);
        Span<char> buffer = stackalloc char[MaxLetters + 8];
        var at = buffer.Length;

        while (position > 0)
        {
            position--;
            buffer[--at] = (char)('A' + (int)(position % 26));
            position /= 26;
        }

        return new string(buffer[at..]);
    }

    /// <summary>
    /// The place of a label in the order revisions are made, so labels sort as they were made, not as text (AA after Z, AAA after ZZ, 10 after 9).
    /// Letters count from 1 (A); a legacy <c>Z+</c> is the 27th and each further <c>+</c> one more; digits are their own number. <see langword="false"/> for a label
    /// that is none of these.
    /// </summary>
    public static bool TryGetPosition(string? label, out long position)
    {
        position = 0;

        if (string.IsNullOrEmpty(label))
            return false;

        return TryParseNumber(label, out position) || TryParseLetters(label, out position);
    }

    /// <summary>Orders two labels by the order the revisions were made; a label that is not understood sorts after every one that is, and those by their text.</summary>
    public static int Compare(string? a, string? b)
    {
        var knownA = TryGetPosition(a, out var pa);
        var knownB = TryGetPosition(b, out var pb);

        if (knownA && knownB)
        {
            // Digits and letters are different series, so one calculation never mixes them; if two labels do, digits come first.
            var byKind = IsNumber(a!).CompareTo(IsNumber(b!));
            return byKind != 0 ? -byKind : pa.CompareTo(pb);
        }

        if (knownA != knownB)
            return knownA ? -1 : 1;

        return string.CompareOrdinal(a, b);
    }

    /// <summary>A comparer that applies <see cref="Compare"/>.</summary>
    public static IComparer<string> Comparer { get; } = Comparer<string>.Create((x, y) => Compare(x, y));

    private static bool IsNumber(string label) => label.All(char.IsAsciiDigit);

    private static bool TryParseNumber(string label, out long number)
    {
        number = 0;
        return label.Length is > 0 and <= 9 && label.All(char.IsAsciiDigit) && long.TryParse(label, NumberStyles.None, CultureInfo.InvariantCulture, out number);
    }

    private static bool TryParseLetters(string label, out long position)
    {
        position = 0;

        // Z+, Z++ … : the labels before DC-15 (the 27th revision, the 28th …).
        var letters = label.TrimEnd('+');
        var pluses = label.Length - letters.Length;

        if (pluses > 0)
        {
            if (letters != "Z" || pluses > MaxLegacyPluses)
                return false;

            position = 26 + pluses;
            return true;
        }

        if (letters.Length is 0 or > MaxLetters)
            return false;

        foreach (var c in letters)
        {
            if (c is < 'A' or > 'Z')
                return false;

            position = (position * 26) + (c - 'A' + 1);
        }

        return true;
    }
}
