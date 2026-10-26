using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// C-2, rows 1 to 4: the one layout of a calculation sheet, written once for the text renderer, the PDF renderer and the golden files. The
/// headings and their order, what an empty section says, the form of a warning or block line, the order of the Results and the
/// separation of default-value notes from warnings are all here, so the two renderers cannot drift apart
/// (<c>docs/calcs/sheet-standard.md</c>, section A).
/// </summary>
public static class CalcSheetStandard
{
    public const string ScopeAndMethod = "1. Scope and method";
    public const string Inputs = "2. Inputs";
    public const string Assumptions = "3. Assumptions";
    public const string Working = "4. Working";
    public const string Results = "5. Results";
    public const string Checks = "6. Checks (governing first)";
    public const string Applicability = "7. Applicability";
    public const string Verification = "8. Verification";
    public const string References = "9. References";

    /// <summary>The section headings in print order. Every sheet prints all of them, in both renderers.</summary>
    public static IReadOnlyList<string> Headings { get; } = [ScopeAndMethod, Inputs, Assumptions, Working, Results, Checks, Applicability, Verification, References];

    /// <summary>What a section with nothing in it says.</summary>
    public const string Empty = "None.";

    /// <summary>The heading of the list of default-value notes under Applicability.</summary>
    public const string DefaultsHeading = "Defaults used (inputs left empty)";

    /// <summary>The marker after the result the governing check is made on.</summary>
    public const string GoverningMarker = "(governing)";

    /// <summary>
    /// One line of Applicability: <c>BLOCK  A: rating needs the heat transfer area.  [area-missing]</c>. The message already opens with the
    /// input symbol (<see cref="CalcMessageShape"/>); the rule id closes the line in brackets.
    /// </summary>
    public static string NoticeLine(CalcNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        var label = notice.Severity == CalcNoticeSeverity.Block ? "BLOCK" : "WARNING";
        return $"{label}  {notice.Message}  [{notice.RuleId}]";
    }

    /// <summary>The line of a default-value note: just the message, which names the input and the value used.</summary>
    public static string DefaultLine(CalcNotice notice)
    {
        ArgumentNullException.ThrowIfNull(notice);
        return notice.Message;
    }

    /// <summary>The line of a check's result: PASS or FAIL, the name, demand against capacity and, for a ratio check, the utilisation.</summary>
    public static string CheckLine(CalcCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);
        var line = $"{(check.Passed ? "PASS" : "FAIL")}  {check.Name}: {check.DemandText} vs {check.CapacityText}";
        return check.HasUtilisation ? $"{line}  utilisation {check.UtilisationText}" : line;
    }

    /// <summary>
    /// The outputs in the order a sheet prints them: the headline results first (the template's output ports, in the order it declares
    /// them), then the supporting values in the order they were produced, then the utilisations. A template with no ports has no headline
    /// group.
    /// </summary>
    public static IReadOnlyList<CalcOutput> OrderOutputs(CalcTemplate template, IReadOnlyList<CalcOutput> outputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(outputs);

        var headline = new List<CalcOutput>();

        foreach (var port in template.OutputPorts ?? [])
        {
            if (outputs.FirstOrDefault(o => port.Matches(o.Symbol) && !headline.Contains(o)) is { } found)
                headline.Add(found);
        }

        var rest = outputs.Where(o => !headline.Contains(o)).ToList();
        var utilisations = rest.Where(IsUtilisation).ToList();
        return [.. headline, .. rest.Where(o => !utilisations.Contains(o)), .. utilisations];
    }

    /// <summary>Whether an output is a utilisation: a plain number whose symbol or description says so (<see cref="CalcDisplay.IsUtilisation"/>).</summary>
    public static bool IsUtilisation(CalcOutput output) =>
        output.Value is ScalarValue { Dimension.IsDimensionless: true, DisplayUnit.Symbol.Length: 0 } && CalcDisplay.IsUtilisation(output.Symbol, output.Description);

    /// <summary>The description of a result as a sheet prints it: the governing result is marked.</summary>
    public static string DescriptionOf(CalcSheet sheet, CalcOutput output) =>
        sheet.GoverningOutputSymbol is { } governing && string.Equals(governing, output.Symbol, StringComparison.Ordinal)
            ? $"{output.Description} {GoverningMarker}"
            : output.Description;
}
