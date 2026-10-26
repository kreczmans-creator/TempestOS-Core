using System.Text.RegularExpressions;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 47 and 48: how a template is listed in the catalogue. The title is <c>&lt;Subject&gt; (&lt;standard or method&gt;)</c> in sentence case,
/// the parenthesis optional and last, the standard written without its year, no version, no colon list (the list belongs in the scope). The
/// category is one word from the discipline's list, in sentence case; a bolt or weld connection is "Connections" in mechanical and structural
/// alike. A contract test holds every template to both.
/// </summary>
public static partial class CalcCatalogueStandard
{
    /// <summary>The longest title the catalogue tree shows without cutting it.</summary>
    public const int MaxTitleLength = 70;

    [GeneratedRegex(@"[:,]\s?(19|20)\d{2}\b|\((19|20)\d{2}\)")]
    private static partial Regex Year();

    [GeneratedRegex(@"\bv\d+(\.\d+)*\b|@\d")]
    private static partial Regex VersionMark();

    [GeneratedRegex(@"^(BS |DIN |ISO |EN |IEC |ASME |VDI |IPC)")]
    private static partial Regex StartsWithStandard();

    /// <summary>The categories of each discipline: one word, sentence case. The catalogue tree is these lists and nothing else.</summary>
    public static IReadOnlyDictionary<CalcDiscipline, IReadOnlyList<string>> Categories { get; } = new Dictionary<CalcDiscipline, IReadOnlyList<string>>
    {
        [CalcDiscipline.Mechanical] = ["Tolerancing", "Fits", "Vessels", "Connections", "Bearings", "Shafts", "Springs", "Mechanisms", "Strength", "Thermal"],
        [CalcDiscipline.Structural] = ["Actions", "Analysis", "Sections", "Members", "Connections", "Lifting"],
        [CalcDiscipline.BuildingServices] = ["Hydraulics", "Air", "Thermal"],
        [CalcDiscipline.Electrical] = ["Drives", "Power", "Thermal", "Electronics"],
        [CalcDiscipline.FluidPower] = ["Hydraulics", "Seals"],
    };

    /// <summary>The problems with a template's title; empty when it conforms.</summary>
    public static IReadOnlyList<string> TitleProblems(string title)
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(title) || !char.IsUpper(title[0]))
            problems.Add("does not start with a capital letter");

        if (title.Length > MaxTitleLength)
            problems.Add($"is longer than {MaxTitleLength} characters");

        if (title.Contains(':', StringComparison.Ordinal))
            problems.Add("has a colon list (put the list in the scope)");

        if (VersionMark().IsMatch(title))
            problems.Add("carries a version");

        if (Year().IsMatch(title))
            problems.Add("carries a year (write the standard without it)");

        if (StartsWithStandard().IsMatch(title))
            problems.Add("starts with a standard (write <Subject> (<standard>))");

        var open = title.Count(c => c == '(');

        if (open > 1 || open != title.Count(c => c == ')') || (open == 1 && !title.EndsWith(')')))
            problems.Add("has more than one parenthesis or one that is not last");

        return problems;
    }

    /// <summary>The problem with a template's category, or <see langword="null"/> when it conforms.</summary>
    public static string? CategoryProblem(CalcDiscipline discipline, string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "has no category";

        return Categories.TryGetValue(discipline, out var list) && list.Contains(category, StringComparer.Ordinal)
            ? null
            : $"'{category}' is not a {discipline} category ({(list is null ? "none" : string.Join(", ", list))})";
    }
}
