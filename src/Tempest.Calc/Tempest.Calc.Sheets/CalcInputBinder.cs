using Tempest.Calc.Engine;
using Tempest.Calc.Engine.Units;
using Tempest.Calc.Engine.Values;

namespace Tempest.Calc.Sheets;

/// <summary>
/// The one place a value's unit is read from (ADR-0177 review). A value with a unit of its own is read in it. A bare number (no unit) is read in
/// the input's default unit <em>on the template version the value was recorded on</em>: an input whose default unit changed at a version
/// (<see cref="CalcInputSpec.UnitChangedAt"/>, <see cref="CalcInputSpec.UnitWas"/>; the same on a table column) reads a bare number recorded
/// before that version in the old unit, never the new one, so a bare 0.3 stored while <c>p_margin</c> was in bar stays 0.3 bar when the default
/// becomes kPa instead of becoming 0.3 kPa, a hundred times smaller and still within the hard range. A bare number on a record that carries no
/// version (<see cref="NoVersion"/>: an inputs file, a CSV, a legacy record) is refused with a sentence naming the symbol and both units, since
/// nothing says which it is. <see cref="CalcInputSet.MadeOn"/> carries the version with the values.
/// </summary>
public static class CalcInputUnits
{
    /// <summary>The <see cref="CalcInputSet.MadeOn"/> of a record that carries no template version at all.</summary>
    public const string NoVersion = "";

    /// <summary>
    /// The unit text a value is read in: <paramref name="givenUnit"/> when it has one; otherwise <paramref name="defaultUnit"/>, or
    /// <paramref name="unitWas"/> for a bare number recorded on a version before <paramref name="unitChangedAt"/>. <paramref name="madeOn"/> is the
    /// template version the value was recorded on: <see langword="null"/> for a value entered against the installed template (a new sheet, a
    /// test case), <see cref="NoVersion"/> for a record that carries none. Returns <see langword="false"/> with the sentence when a bare number
    /// cannot be told apart.
    /// </summary>
    public static bool TryResolve(string symbol, string defaultUnit, string? unitChangedAt, string? unitWas, string? givenUnit, string? madeOn, out string unit, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        ArgumentNullException.ThrowIfNull(defaultUnit);
        refusal = null;

        if (givenUnit is not null)
        {
            unit = givenUnit;
            return true;
        }

        if (unitChangedAt is not { Length: > 0 } || madeOn is null)
        {
            unit = defaultUnit;
            return true;
        }

        if (!CalcTemplateVersion.IsWellFormed(madeOn))
        {
            unit = defaultUnit;
            refusal = $"{symbol}: the stored value has no unit and the record does not say which template version it was made on, so it cannot be read: {symbol} was in {CalcUnitText.Pretty(unitWas)} before version {unitChangedAt} and is in {CalcUnitText.Pretty(defaultUnit)} since; enter it with its unit.";
            return false;
        }

        unit = CalcTemplateVersion.Compare(madeOn, unitChangedAt) < 0 ? unitWas ?? defaultUnit : defaultUnit;
        return true;
    }

    /// <summary><see cref="TryResolve(string, string, string?, string?, string?, string?, out string, out string?)"/> for an input and the value given for it.</summary>
    public static bool TryResolve(CalcInputSpec spec, CalcInputValue? value, string? madeOn, out string unit, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(spec);
        return TryResolve(spec.Symbol, spec.DefaultUnit, spec.UnitChangedAt, spec.UnitWas, value?.Unit, madeOn, out unit, out refusal);
    }

    /// <summary>The same for one cell of a table column; <paramref name="symbol"/> names the cell ("rows[1].p") for the sentence.</summary>
    public static bool TryResolve(CalcColumnSpec column, string symbol, string? givenUnit, string? madeOn, out string unit, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(column);
        return TryResolve(symbol, column.DefaultUnit, column.UnitChangedAt, column.UnitWas, givenUnit, madeOn, out unit, out refusal);
    }

    /// <summary>
    /// The value with its unit made explicit where a bare number would otherwise be read in another version's default unit: a bare quantity,
    /// vector or table cell of an input or column that declares a unit change gains the unit it was recorded in, so it can be carried to
    /// another version (a draft upgrade, a revision) and still mean what it meant. Any other value is returned as it is. Returns
    /// <see langword="false"/> with the sentence when the record carries no version to read it by.
    /// </summary>
    public static bool TryPin(CalcInputSpec spec, CalcInputValue value, string? madeOn, out CalcInputValue pinned, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(value);
        pinned = value;
        refusal = null;

        if (spec.Kind is CalcInputKind.Quantity or CalcInputKind.Vector)
        {
            if (!spec.HasUnitChange || value.Unit is not null)
                return true;

            if (!TryResolve(spec, value, madeOn, out var unit, out refusal))
                return false;

            pinned = value with { Unit = unit };
            return true;
        }

        if (spec.Kind != CalcInputKind.Table || value.Rows is not { Count: > 0 } rows || spec.Columns is not { } columns || !columns.Any(c => c.HasUnitChange))
            return true;

        var pinnedRows = new List<CalcRow>(rows.Count);

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            Dictionary<string, CalcCell>? cells = null;

            foreach (var column in columns.Where(c => c.HasUnitChange && c.Kind == CalcInputKind.Quantity))
            {
                if (row?.Cells.GetValueOrDefault(column.Key) is not { Number: not null, Unit: null } cell)
                    continue;

                if (!TryResolve(column, $"{spec.Symbol}[{r + 1}].{column.Key}", null, madeOn, out var unit, out refusal))
                    return false;

                cells ??= new Dictionary<string, CalcCell>(row!.Cells, StringComparer.Ordinal);
                cells[column.Key] = cell with { Unit = unit };
            }

            pinnedRows.Add(cells is null ? row! : row! with { Cells = cells });
        }

        pinned = value with { Rows = pinnedRows };
        return true;
    }

    /// <summary><see cref="TryPin(CalcInputSpec, CalcInputValue, string?, out CalcInputValue, out string?)"/> over every value a template declares; values of no input are returned as they are.</summary>
    public static bool TryPin(CalcTemplate template, IReadOnlyList<CalcInputValue> values, string? madeOn, out IReadOnlyList<CalcInputValue> pinned, out string? refusal)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);
        refusal = null;
        List<CalcInputValue>? copy = null;

        for (var i = 0; i < values.Count; i++)
        {
            if (template.ResolveInput(values[i].Symbol) is not { } spec)
                continue;

            if (!TryPin(spec, values[i], madeOn, out var one, out refusal))
            {
                pinned = values;
                return false;
            }

            if (ReferenceEquals(one, values[i]))
                continue;

            copy ??= [.. values];
            copy[i] = one;
        }

        pinned = copy ?? values;
        return true;
    }
}

/// <summary>Turns one entered input into an engine value according to its spec. Assumes the input already passed <see cref="CalcInputValidator"/>.</summary>
public static class CalcInputBinder
{
    /// <param name="madeOn">The template version the value was recorded on (<see cref="CalcInputSet.MadeOn"/>), which decides the unit a bare number is read in (<see cref="CalcInputUnits"/>).</param>
    public static CalcValue Bind(CalcInputSpec spec, CalcInputValue? value, string? madeOn = null)
    {
        ArgumentNullException.ThrowIfNull(spec);

        switch (spec.Kind)
        {
            case CalcInputKind.Quantity:
            {
                var unit = UnitCatalog.Parse(ResolveUnit(spec, value, madeOn));
                var number = value?.Number ?? spec.Default ?? throw new CalcEvaluationException($"{spec.Symbol}: no value was given.");
                return ScalarValue.Of(number, unit);
            }

            case CalcInputKind.Number:
                return new ScalarValue(value?.Number ?? spec.Default ?? throw new CalcEvaluationException($"{spec.Symbol}: no value was given."));

            case CalcInputKind.Choice:
                return new TextValue(value?.Text ?? spec.DefaultChoice ?? throw new CalcEvaluationException($"{spec.Symbol}: no choice was made."));

            case CalcInputKind.Text:
                return new TextValue(value?.Text ?? string.Empty);

            case CalcInputKind.Boolean:
                return new BoolValue(value?.Flag ?? (spec.Default is { } d && d != 0));

            case CalcInputKind.Vector:
            {
                var unit = UnitCatalog.Parse(ResolveUnit(spec, value, madeOn));
                return VectorValue.Of(value?.Vector ?? [], unit);
            }

            case CalcInputKind.Table:
                return new ScalarValue(value?.Rows?.Count ?? 0);

            default:
                throw new CalcEvaluationException($"{spec.Symbol}: unknown input kind.");
        }
    }

    private static string ResolveUnit(CalcInputSpec spec, CalcInputValue? value, string? madeOn) =>
        CalcInputUnits.TryResolve(spec, value, madeOn, out var unit, out var refusal) ? unit : throw new CalcEvaluationException(refusal!);
}
