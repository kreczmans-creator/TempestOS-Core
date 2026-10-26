using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>Assembles a <see cref="CalcSheet"/> from a template, the inputs as entered, a run result and the governance header and footer.</summary>
public static class CalcSheetBuilder
{
    /// <summary>
    /// Builds the sheet. <paramref name="withheldTables"/> names the table inputs pinned to a licensed reference record (symbol
    /// to the record's pin): their rows are not printed. The sheet states the pin and that the rows are withheld, and the values
    /// the calculation used appear in its working (Red R-A-01: the sheet shows the value used, never the licensed table).
    /// </summary>
    public static CalcSheet Build(CalcTemplate template, CalcInputSet inputs, CalcRunResult result, CalcSheetHeader header, CalcSheetFooter footer, IReadOnlyList<CalcRevisionRow>? revisions = null, IReadOnlyDictionary<string, string>? withheldTables = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(footer);

        // C-2: a symbol an input had before it was renamed is read as the input it has become.
        // (An input given under two of its names with different values has blocked the run; the sheet then lists the inputs as they were given.)
        CalcInputAliases.TryCanonicalise(template, inputs, out inputs, out _);

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
            var range = RangeIn(spec, unit);

            // A pressure prints its basis beside the unit ("bar a", "bar g"): the label names the basis and converts nothing.
            if (unit.Length > 0 && spec.Kind == CalcInputKind.Quantity && spec.BasisLabel(s => inputs.Find(s)?.Text ?? template.FindInput(s)?.DefaultChoice) is { } basis)
            {
                range = range.EndsWith(unit, StringComparison.Ordinal) ? $"{range} {basis}" : range;
                unit = $"{unit} {basis}";
            }

            // The unit and the range are shown as symbols (°C, Ω, kN·m, mm²), as everywhere else on the sheet (C-2, row 27).
            inputRows.Add(new CalcSheetInputRow(spec.Symbol, spec.Description, Truncate(shown.ValueText), CalcUnitText.Pretty(unit), source, CalcUnitText.Pretty(range)));

            // A table's rows are reproduced under their summary row so the model (nodes, loads, vertices, contributors)
            // can be checked from the sheet alone (Black B-17); past the cap the count is stated.
            if (spec.Kind == CalcInputKind.Table && withheldTables is not null && withheldTables.TryGetValue(spec.Symbol, out var pin))
            {
                inputRows.Add(new CalcSheetInputRow($"{spec.Symbol}[…]", string.Empty, "rows withheld (licensed record); the values used appear in the working", string.Empty, Truncate(pin), string.Empty));
            }
            else if (spec.Kind == CalcInputKind.Table && shown.Rows is { Count: > 0 } rows && spec.Columns is { Count: > 0 } columns)
            {
                for (var r = 0; r < Math.Min(rows.Count, MaxTableRowsOnSheet); r++)
                    inputRows.Add(new CalcSheetInputRow($"{spec.Symbol}[{r + 1}]", string.Empty, Truncate(RowText(rows[r], columns)), string.Empty, string.Empty, string.Empty));

                if (rows.Count > MaxTableRowsOnSheet)
                    inputRows.Add(new CalcSheetInputRow($"{spec.Symbol}[…]", string.Empty, $"… and {rows.Count - MaxTableRowsOnSheet} more row(s)", string.Empty, string.Empty, string.Empty));
            }

            if (shown.Provenance is { Kind: CalcProvenanceKind.Assumption } p)
                assumptions.Add(new CalcAssumption($"{spec.Symbol} ({spec.Description}) is taken as {shown.ValueText}{(unit.Length > 0 ? " " + CalcUnitText.Pretty(unit) : string.Empty)}.", p.Reference));
        }

        assumptions.AddRange(result.RunAssumptions);

        // Governing case first (§3.4 item 6), then the rest by utilisation.
        var checks = result.Checks.OrderByDescending(c => c.Utilisation).ToList();

        // C-2: the headline results first, then the supporting values, then the utilisations; the default-value notes apart from the warnings.
        return new CalcSheet(
            header, template.Scope, template.Method, template.ClauseRefs, inputRows, assumptions,
            result.Steps.Where(s => s.Kind != CalcStepKind.Input).ToList(),
            checks, CollapseUnsourced(result.EngineeringWarnings, template), result.Blocks, CalcSheetStandard.OrderOutputs(template, result.Outputs), footer, revisions, null, template.DiagramKey, inputs,
            result.DefaultNotices);
    }

    private const string UnsourcedPrefix = "record.";

    /// <summary>
    /// The "tabulated value not taken from a reference record" warnings, one per symbol, become one line listing the symbols, so
    /// they do not bury the warnings that matter (Black B-A-18). The run's own warnings are unchanged.
    /// </summary>
    private static IReadOnlyList<CalcNotice> CollapseUnsourced(IReadOnlyList<CalcNotice> warnings, CalcTemplate template)
    {
        var unsourced = warnings.Where(w => w.RuleId.StartsWith(UnsourcedPrefix, StringComparison.Ordinal)).ToList();

        if (unsourced.Count < 2)
            return warnings;

        // One line, symbols grouped under the record kind that supplies them: "rho_20, alpha (Conductor material constants); …".
        var symbols = unsourced.Select(w => w.RuleId[UnsourcedPrefix.Length..]).ToList();
        var groups = new List<string>();

        foreach (var kind in template.RecordKinds ?? [])
        {
            var named = symbols.Where(s => kind.Symbols.Contains(s, StringComparer.Ordinal)).ToList();

            if (named.Count > 0)
                groups.Add($"{string.Join(", ", named)} ({kind.Title})");
        }

        var rest = symbols.Where(s => !(template.RecordKinds ?? []).Any(k => k.Symbols.Contains(s, StringComparer.Ordinal))).ToList();

        if (rest.Count > 0)
            groups.Add(string.Join(", ", rest));

        var merged = new CalcNotice("record", CalcNoticeSeverity.Warning, $"records: tabulated values not taken from a reference record are {string.Join("; ", groups)}. Enter each as one so its source, edition and licence travel with it.");
        var list = new List<CalcNotice>();

        foreach (var w in warnings)
        {
            if (!w.RuleId.StartsWith(UnsourcedPrefix, StringComparison.Ordinal))
                list.Add(w);
            else if (ReferenceEquals(w, unsourced[0]))
                list.Add(merged);
        }

        return list;
    }

    private static CalcInputValue DefaultValue(CalcInputSpec spec) => spec.Kind switch
    {
        CalcInputKind.Choice => new CalcInputValue(spec.Symbol, Text: spec.DefaultChoice),
        CalcInputKind.Boolean => new CalcInputValue(spec.Symbol, Flag: spec.Default is { } d && d != 0),
        _ => new CalcInputValue(spec.Symbol, Number: spec.Default, Unit: spec.DefaultUnit.Length > 0 ? spec.DefaultUnit : null),
    };

    /// <summary>Rows of one table input written on the sheet; the rest is counted.</summary>
    public const int MaxTableRowsOnSheet = 100;

    private static string RowText(CalcRow row, IReadOnlyList<CalcColumnSpec> columns)
    {
        var parts = new List<string>(columns.Count);

        foreach (var col in columns)
        {
            var cell = row.Get(col.Key);

            if (cell is null)
                continue;

            var text = cell switch
            {
                { Number: { } n } => CalcValue.FormatNumber(n) + ((cell.Unit ?? col.DefaultUnit) is { Length: > 0 } u ? (CalcUnitText.Pretty(u) is "°" ? "°" : " " + CalcUnitText.Pretty(u)) : string.Empty),
                { Text: { } t } => t,
                { Flag: { } f } => f ? "yes" : "no",
                _ => "—",
            };
            parts.Add($"{col.Key} = {text}");
        }

        return string.Join("; ", parts);
    }

    /// <summary>The valid range in the unit the value was entered in, so "0.003 in" is not checked against "… mm" (Black B-20); the template's text when the unit is unknown or the same.</summary>
    private static string RangeIn(CalcInputSpec spec, string unit)
    {
        if (spec.Kind != CalcInputKind.Quantity || unit.Length == 0 || string.Equals(unit, spec.DefaultUnit, StringComparison.Ordinal) || (spec.Min ?? spec.HardMin) is null && (spec.Max ?? spec.HardMax) is null)
            return spec.RangeText;

        try
        {
            var from = UnitCatalog.Parse(spec.DefaultUnit);
            var to = UnitCatalog.Parse(unit);

            if (from.Dimension != to.Dimension)
                return spec.RangeText;

            string F(double v) => CalcValue.FormatNumber(to.FromSi(from.ToSi(v)), 4);
            var lo = spec.Min ?? spec.HardMin;
            var hi = spec.Max ?? spec.HardMax;

            if (lo is null)
                return $"≤ {F(hi!.Value)} {CalcUnitText.Pretty(unit)}";

            if (hi is null)
                return $"≥ {F(lo.Value)} {CalcUnitText.Pretty(unit)}";

            return $"{F(lo.Value)} … {F(hi.Value)} {CalcUnitText.Pretty(unit)}";
        }
        catch (CalcUnitException)
        {
            return spec.RangeText;
        }
    }

    /// <summary>An entered value longer than the text cap is shown cut (R-06: a blocked draft must not carry a megabyte of text).</summary>
    private static string Truncate(string s) => s.Length > CalcInputValidator.MaxTextLength ? s[..CalcInputValidator.MaxTextLength] + "…" : s;
}
