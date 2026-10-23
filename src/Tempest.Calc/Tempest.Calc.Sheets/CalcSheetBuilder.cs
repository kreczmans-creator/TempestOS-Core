using Tempest.Calc.Engine;

namespace Tempest.Calc.Sheets;

/// <summary>Assembles a <see cref="CalcSheet"/> from a template, the inputs as entered, a run result and the governance header and footer.</summary>
public static class CalcSheetBuilder
{
    public static CalcSheet Build(CalcTemplate template, CalcInputSet inputs, CalcRunResult result, CalcSheetHeader header, CalcSheetFooter footer, IReadOnlyList<CalcRevisionRow>? revisions = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(footer);

        var inputRows = new List<CalcSheetInputRow>();
        var assumptions = new List<CalcAssumption>(template.Assumptions);

        foreach (var spec in template.Inputs)
        {
            var value = inputs.Find(spec.Symbol);

            if (value is null && spec.IsOptional)
                continue;

            var shown = value ?? DefaultValue(spec);
            var unit = spec.Kind is CalcInputKind.Quantity or CalcInputKind.Vector ? (shown.Unit ?? spec.DefaultUnit) : string.Empty;
            var source = Truncate(shown.Provenance?.ToString() ?? (value is null ? "default" : "manual"));
            inputRows.Add(new CalcSheetInputRow(spec.Symbol, spec.Description, Truncate(shown.ValueText), unit, source, spec.RangeText));

            if (shown.Provenance is { Kind: CalcProvenanceKind.Assumption } p)
                assumptions.Add(new CalcAssumption($"{spec.Symbol} ({spec.Description}) is taken as {shown.ValueText}{(unit.Length > 0 ? " " + unit : string.Empty)}.", p.Reference));
        }

        assumptions.AddRange(result.RunAssumptions);

        // Governing case first (§3.4 item 6), then the rest by utilisation.
        var checks = result.Checks.OrderByDescending(c => c.Utilisation).ToList();

        return new CalcSheet(
            header, template.Scope, template.Method, template.ClauseRefs, inputRows, assumptions,
            result.Steps.Where(s => s.Kind != CalcStepKind.Input).ToList(),
            checks, result.Warnings, result.Blocks, result.Outputs, footer, revisions, null, template.DiagramKey);
    }

    private static CalcInputValue DefaultValue(CalcInputSpec spec) => spec.Kind switch
    {
        CalcInputKind.Choice => new CalcInputValue(spec.Symbol, Text: spec.DefaultChoice),
        CalcInputKind.Boolean => new CalcInputValue(spec.Symbol, Flag: spec.Default is { } d && d != 0),
        _ => new CalcInputValue(spec.Symbol, Number: spec.Default, Unit: spec.DefaultUnit.Length > 0 ? spec.DefaultUnit : null),
    };

    /// <summary>An entered value longer than the text cap is shown cut (R-06: a blocked draft must not carry a megabyte of text).</summary>
    private static string Truncate(string s) => s.Length > CalcInputValidator.MaxTextLength ? s[..CalcInputValidator.MaxTextLength] + "…" : s;
}
