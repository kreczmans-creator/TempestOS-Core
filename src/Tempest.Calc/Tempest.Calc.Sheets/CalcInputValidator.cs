using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Sheets;

/// <summary>
/// Validates a set of entered inputs against a template before anything
/// runs: every required input present, every number finite, every unit of
/// the declared dimension, every choice declared, hard limits respected
/// (block) and soft limits respected (warning). Messages are one plain
/// sentence naming the symbol. Never throws for bad input.
/// </summary>
public static class CalcInputValidator
{
    public const int MaxTextLength = 200;

    /// <summary>Notices listed per table input before the rest are summarised (R-20).</summary>
    public const int MaxNoticesPerInput = 20;
    public const int MaxVectorLength = 10_000;

    /// <summary>
    /// Whether a provenance pins a reference record (<c>reference: record:&lt;id&gt;@&lt;revision&gt;</c>). The pin must parse: the
    /// bare prefix ("record: owner's table") is not a pin and does not silence the tabulated-value warning (Red R-A-06).
    /// </summary>
    public static bool IsRecordPin(CalcProvenance? provenance) => CalcRecordPin.From(provenance) is not null;

    public static IReadOnlyList<CalcNotice> Validate(CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        var notices = new List<CalcNotice>();

        // §A3: a tabulated value (a symbol a record kind lists) should come from a reference record, which carries its
        // source, edition and licence. Typed from elsewhere it still runs, with a warning naming the record kind.
        foreach (var kind in template.RecordKinds ?? [])
        {
            foreach (var symbol in kind.Symbols)
            {
                if (inputs.Find(symbol) is { } tabulated && !IsEmpty(tabulated) && !IsRecordPin(tabulated.Provenance))
                    notices.Add(new CalcNotice("record." + symbol, CalcNoticeSeverity.Warning, $"{symbol}: a tabulated value not taken from a reference record ({kind.Title}); enter it as one so its source, edition and licence travel with it."));
            }
        }

        foreach (var spec in template.Inputs)
        {
            var value = inputs.Find(spec.Symbol);

            if (value?.Provenance is { } provenance && !provenance.IsWithinLimits)
                notices.Add(Block(spec.Symbol, $"{spec.Symbol}: the provenance reference is at most {CalcProvenance.MaxReferenceLength} characters and the revision at most {CalcProvenance.MaxRevisionLength}."));

            // A value withheld on export (a licensed record) is not a missing value that a default may stand in for: the
            // calculation does not run until the record is applied again.
            if (value is not null && IsEmpty(value) && CalcCsv.IsWithheld(value.Provenance))
            {
                notices.Add(Block(spec.Symbol, CalcCsv.WithheldMessage(spec.Symbol)));
                continue;
            }

            if (value is null || IsEmpty(value))
            {
                // A defaulted input is visible on the sheet ("default" source) and now in the notices too, so a sheet
                // computed from an empty or truncated file cannot pass for an entered one (Black B-01).
                if (!spec.IsOptional && spec.Kind is not CalcInputKind.Boolean and not CalcInputKind.Text && (spec.Default is not null || spec.DefaultChoice is not null))
                    notices.Add(new CalcNotice("input." + spec.Symbol, CalcNoticeSeverity.Warning, $"{spec.Symbol} ({spec.Description}) was not entered; the template default {DefaultText(spec)} is used."));

                if (spec.IsOptional || spec.Default is not null || spec.DefaultChoice is not null || spec.Kind is CalcInputKind.Boolean or CalcInputKind.Text)
                    continue;

                notices.Add(Block(spec.Symbol, $"{spec.Symbol}: {spec.NextStepText()}"));
                continue;
            }

            switch (spec.Kind)
            {
                case CalcInputKind.Quantity:
                case CalcInputKind.Number:
                    ValidateNumber(spec, value, notices);
                    break;

                case CalcInputKind.Choice:
                    if (value.Text is null || spec.Choices is null || !spec.Choices.Contains(value.Text, StringComparer.Ordinal))
                        notices.Add(Block(spec.Symbol, $"{spec.Symbol} must be one of: {string.Join(", ", spec.Choices ?? [])}."));
                    break;

                case CalcInputKind.Text:
                    if (value.Text is { Length: > MaxTextLength })
                        notices.Add(Block(spec.Symbol, $"{spec.Symbol} is at most {MaxTextLength} characters."));
                    break;

                case CalcInputKind.Boolean:
                    if (value.Flag is null)
                        notices.Add(Block(spec.Symbol, $"{spec.Symbol} must be yes or no."));
                    break;

                case CalcInputKind.Vector:
                    ValidateVector(spec, value, notices);
                    break;

                case CalcInputKind.Table:
                    ValidateTable(spec, value, notices);
                    break;
            }
        }

        foreach (var value in inputs.Values)
        {
            if (template.FindInput(value.Symbol) is null)
                notices.Add(new CalcNotice("input." + value.Symbol, CalcNoticeSeverity.Warning, $"{value.Symbol} is not an input of this calculation and is ignored."));
        }

        return notices;
    }

    /// <summary>Whether every input that needs provenance has one that is enough to issue with.</summary>
    public static IReadOnlyList<CalcNotice> ValidateProvenanceForIssue(CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        var notices = new List<CalcNotice>();

        foreach (var spec in template.Inputs)
        {
            var value = inputs.Find(spec.Symbol);

            if (value is null || IsEmpty(value))
                continue;

            if (value.Provenance is null || !value.Provenance.IsSufficientToIssue)
                notices.Add(Block(spec.Symbol, $"{spec.Symbol} needs a source (drawing, specification, assumption, evidence or reference) before the sheet can be issued."));
        }

        return notices;
    }

    private static bool IsEmpty(CalcInputValue v) => v.Number is null && v.Text is null && v.Flag is null && v.Vector is null && v.Rows is null;

    private static void ValidateNumber(CalcInputSpec spec, CalcInputValue value, List<CalcNotice> notices)
    {
        if (value.Number is not { } n)
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} needs a number."));
            return;
        }

        if (!double.IsFinite(n))
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} must be a finite number."));
            return;
        }

        var inDefault = n;

        if (spec.Kind == CalcInputKind.Quantity)
        {
            var unitText = value.Unit ?? spec.DefaultUnit;

            if (!UnitCatalog.TryParse(unitText, out var unit, out var error))
            {
                notices.Add(Block(spec.Symbol, $"{spec.Symbol}: {error}"));
                return;
            }

            if (unit.Dimension != spec.Dimension)
            {
                notices.Add(Block(spec.Symbol, $"{spec.Symbol} must be entered in a unit of {spec.DefaultUnit}, not {unitText}."));
                return;
            }

            inDefault = UnitCatalog.Parse(spec.DefaultUnit).FromSi(unit.ToSi(n));
        }
        else if (value.Unit is { Length: > 0 } u && u != "1")
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} is a plain number and takes no unit."));
            return;
        }

        CheckLimits(spec.Symbol, spec.Description, inDefault, spec.DefaultUnit, spec.Min, spec.Max, spec.HardMin, spec.HardMax, notices);
    }

    private static void CheckLimits(string symbol, string description, double v, string unit, double? min, double? max, double? hardMin, double? hardMax, List<CalcNotice> notices)
    {
        var suffix = unit.Length > 0 ? " " + unit : string.Empty;

        if (hardMin is { } hmin && v < hmin)
        {
            notices.Add(Block(symbol, $"{symbol} ({description}) must be at least {F(hmin)}{suffix}; {F(v)}{suffix} was given."));
            return;
        }

        if (hardMax is { } hmax && v > hmax)
        {
            notices.Add(Block(symbol, $"{symbol} ({description}) must be at most {F(hmax)}{suffix}; {F(v)}{suffix} was given."));
            return;
        }

        if ((min is { } lo && v < lo) || (max is { } hi && v > hi))
        {
            var range = min is { } l2 && max is { } h2 ? $"{F(l2)} … {F(h2)}{suffix}" : min is { } l3 ? $"≥ {F(l3)}{suffix}" : $"≤ {F(max!.Value)}{suffix}";
            notices.Add(new CalcNotice("input." + symbol, CalcNoticeSeverity.Warning, $"{symbol} ({description}) is outside its usual range {range}; {F(v)}{suffix} was given."));
        }
    }

    private static void ValidateVector(CalcInputSpec spec, CalcInputValue value, List<CalcNotice> notices)
    {
        if (value.Vector is not { } vector)
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} needs a list of numbers."));
            return;
        }

        if (vector.Count > MaxVectorLength)
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} holds at most {MaxVectorLength} values."));
            return;
        }

        if (vector.Any(x => !double.IsFinite(x)))
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} must hold finite numbers only."));
            return;
        }

        var unitText = value.Unit ?? spec.DefaultUnit;

        if (!UnitCatalog.TryParse(unitText, out var unit, out var error))
            notices.Add(Block(spec.Symbol, $"{spec.Symbol}: {error}"));
        else if (unit.Dimension != spec.Dimension)
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} must be entered in a unit of {spec.DefaultUnit}, not {unitText}."));
    }

    private static void ValidateTable(CalcInputSpec spec, CalcInputValue value, List<CalcNotice> notices)
    {
        if (value.Rows is not { } rows)
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol}: {spec.NextStepText()}"));
            return;
        }

        if (rows.Count > spec.MaxRows)
        {
            notices.Add(Block(spec.Symbol, $"{spec.Symbol} holds at most {spec.MaxRows} rows; {rows.Count} were given."));
            return;
        }

        var columns = spec.Columns ?? [];

        var before = notices.Count;

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];

            if (row is null)
            {
                notices.Add(Block(spec.Symbol, $"{spec.Symbol} row {r + 1} is empty."));
                continue;
            }

            if (notices.Count - before >= MaxNoticesPerInput)
            {
                notices.Add(Block(spec.Symbol, $"{spec.Symbol}: more problems follow in later rows ({rows.Count - r} rows not listed)."));
                break;
            }

            foreach (var col in columns)
            {
                var cell = row.Get(col.Key);
                var where = $"{spec.Symbol} row {r + 1}, {col.Label}";

                if (cell is null || (cell.Number is null && cell.Text is null && cell.Flag is null))
                {
                    if (!col.IsOptional)
                        notices.Add(Block(spec.Symbol, $"{where} needs a value."));

                    continue;
                }

                switch (col.Kind)
                {
                    case CalcInputKind.Quantity:
                    case CalcInputKind.Number:
                    {
                        if (cell.Number is not { } n || !double.IsFinite(n))
                        {
                            notices.Add(Block(spec.Symbol, $"{where} must be a finite number."));
                            break;
                        }

                        var inDefault = n;

                        if (col.Kind == CalcInputKind.Quantity)
                        {
                            var unitText = cell.Unit ?? col.DefaultUnit;

                            if (!UnitCatalog.TryParse(unitText, out var unit, out var error))
                            {
                                notices.Add(Block(spec.Symbol, $"{where}: {error}"));
                                break;
                            }

                            var expected = UnitCatalog.Parse(col.DefaultUnit);

                            if (unit.Dimension != expected.Dimension)
                            {
                                notices.Add(Block(spec.Symbol, $"{where} must be in a unit of {col.DefaultUnit}, not {unitText}."));
                                break;
                            }

                            inDefault = expected.FromSi(unit.ToSi(n));
                        }

                        CheckLimits($"{spec.Symbol}[{r + 1}].{col.Key}", col.Label, inDefault, col.DefaultUnit, col.Min, col.Max, col.HardMin, col.HardMax, notices);
                        break;
                    }

                    case CalcInputKind.Choice:
                        if (cell.Text is null || col.Choices is null || !col.Choices.Contains(cell.Text, StringComparer.Ordinal))
                            notices.Add(Block(spec.Symbol, $"{where} must be one of: {string.Join(", ", col.Choices ?? [])}."));
                        break;

                    case CalcInputKind.Text:
                        if (cell.Text is { Length: > MaxTextLength })
                            notices.Add(Block(spec.Symbol, $"{where} is at most {MaxTextLength} characters."));
                        break;

                    case CalcInputKind.Boolean:
                        if (cell.Flag is null)
                            notices.Add(Block(spec.Symbol, $"{where} must be yes or no."));
                        break;
                }
            }
        }
    }

    private static CalcNotice Block(string symbol, string message) => new("input." + symbol, CalcNoticeSeverity.Block, message);

    private static string DefaultText(CalcInputSpec spec) => spec.DefaultChoice is { } choice
        ? $"'{choice}'"
        : Engine.Values.CalcValue.FormatNumber(spec.Default ?? 0, 4) + (spec.DefaultUnit.Length > 0 ? " " + spec.DefaultUnit : string.Empty);

    private static string F(double v) => Engine.Values.CalcValue.FormatNumber(v, 5);
}
