using Tempest.Calc.Engine.Units;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>Addendum B, W3 — applying a material to a calculation, and judging whether an input still holds what its material says.</summary>
public sealed partial class CalcInstanceService
{
    /// <summary>
    /// Fills a draft's inputs from a material record: each input the template's material record kind lists and that names a
    /// property (<see cref="CalcInputSpec.MaterialProperty"/>) takes the material's value in the input's own unit, with the
    /// material's pin (and the property, the temperature, the value's page or table) as its source.
    /// </summary>
    private async Task<CalcActionResult> ApplyMaterialAsync(CalcInstance instance, CalcDataRecord material, CalcTemplate template, CalcRecordKind kind, MaterialTemperature? at, bool singleValueAtAnyTemperature, CancellationToken cancellationToken)
    {
        var inputs = instance.Inputs.ToList();
        var applied = 0;
        var wanted = new List<string>();

        foreach (var spec in template.Inputs.Where(i => i.MaterialProperty is { Length: > 0 } && kind.Symbols.Contains(i.Symbol, StringComparer.Ordinal)))
        {
            var property = spec.MaterialProperty!;
            wanted.Add(property);
            var def = MaterialProperties.Find(property, out var temperatureTable);

            if (def is null)
                return CalcActionResult.Refused($"'{template.Title}' maps input '{spec.Symbol}' to '{property}', which is not a material property.", instance);

            var resolved = MaterialLibrary.Resolve(material, property, at, singleValueAtAnyTemperature);

            if (resolved.Refusal is not null)
                return CalcActionResult.Refused(resolved.Refusal, instance);

            if (resolved.Absent)
                continue;

            CalcInputValue value;

            if (resolved.Scalar is { } scalar)
            {
                if (spec.Kind is not (CalcInputKind.Quantity or CalcInputKind.Number))
                    return CalcActionResult.Refused($"Input '{spec.Symbol}' takes {property} as one number, but is a {spec.Kind} input.", instance);

                var (number, unit, problem) = InputNumber(spec, def, scalar);

                if (problem is not null)
                    return CalcActionResult.Refused(problem, instance);

                value = new CalcInputValue(spec.Symbol, Number: number, Unit: unit, Provenance: MaterialLibrary.ProvenanceFor(material, property, scalar.Source, at, scalar.Basis));
            }
            else
            {
                if (spec.Kind != CalcInputKind.Table)
                    return CalcActionResult.Refused($"Input '{spec.Symbol}' takes {property}, which is a table, but is a {spec.Kind} input.", instance);

                if (TableFitProblem(spec, property, resolved.Rows!) is { } fit)
                    return CalcActionResult.Refused(fit, instance);

                value = new CalcInputValue(spec.Symbol, Rows: resolved.Rows, Provenance: MaterialLibrary.ProvenanceFor(material, property, resolved.Source!, temperatureTable ? null : at, "stated"));
            }

            inputs.RemoveAll(i => string.Equals(i.Symbol, spec.Symbol, StringComparison.Ordinal));
            inputs.Add(value);
            applied++;
        }

        if (applied == 0)
        {
            return CalcActionResult.Refused(
                wanted.Count == 0
                    ? $"'{template.Title}' has no input that takes a material property."
                    : $"Material '{material.Title}' does not state {string.Join(", ", wanted)}, which '{template.Title}' takes from a material.",
                instance);
        }

        return await UpdateInputsAsync(instance.Id, inputs, instance.UpdatedAt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Why a material's table cannot go into a table input, or <see langword="null"/>: more rows than the input holds, a column the input does not have, or a required column left out (B-0 review, LOW 7).</summary>
    private static string? TableFitProblem(CalcInputSpec spec, string property, IReadOnlyList<CalcRow> rows)
    {
        if (rows.Count > spec.MaxRows)
            return $"Input '{spec.Symbol}' holds at most {spec.MaxRows} rows, but {property} of the material has {rows.Count}.";

        var columns = spec.Columns ?? [];

        for (var r = 0; r < rows.Count; r++)
        {
            var cells = rows[r]?.Cells ?? new Dictionary<string, CalcCell>();

            if (cells.Keys.FirstOrDefault(k => !columns.Any(c => string.Equals(c.Key, k, StringComparison.Ordinal))) is { } unknown)
                return $"Input '{spec.Symbol}' has no column '{unknown}', which {property} of the material gives (row {r + 1}).";

            if (columns.FirstOrDefault(c => !c.IsOptional && !cells.ContainsKey(c.Key)) is { } missing)
                return $"Input '{spec.Symbol}' needs column '{missing.Key}', which {property} of the material does not give (row {r + 1}).";
        }

        return null;
    }

    /// <summary>A material value as an input's number: in the input's default unit (a quantity) or as a plain number (a dimensionless one).</summary>
    private static (double? Number, string? Unit, string? Problem) InputNumber(CalcInputSpec spec, MaterialPropertyDef def, MaterialValue value)
    {
        if (spec.Kind == CalcInputKind.Number)
        {
            return (def.Dimension is { } d && d != Dimension.None) || (def.SiUnit is null && value.SiUnit.Length > 0)
                ? (null, null, $"Input '{spec.Symbol}' is a plain number, but {def.Key} ({def.Title}) is a quantity in {(def.SiUnit is { Length: > 0 } si ? si : value.SiUnit)}; declare the input as a quantity.")
                : (value.Si, null, null);
        }

        var (converted, refusal) = value.In(spec.DefaultUnit);
        return converted is { } number
            ? (number, spec.DefaultUnit, null)
            : (null, null, $"Input '{spec.Symbol}' ({spec.DefaultUnit}) cannot take {def.Key} ({def.Title}): {refusal}");
    }

    /// <summary>
    /// Whether an input still holds what the material it cites says: the property it maps to, read as it was when applied (the
    /// temperature is in the input's source), against the pinned revision.
    /// </summary>
    private static bool MatchesMaterial(CalcInputValue input, CalcDataRecord material, CalcInputSpec? spec)
    {
        if (spec?.MaterialProperty is not { Length: > 0 } property)
            return false;

        var resolved = MaterialLibrary.Resolve(material, property, MaterialLibrary.TemperatureOf(input.Provenance), singleValue: true);

        if (resolved.Scalar is { } scalar)
        {
            if (input.Number is null)
                return false;

            var x = InputSi(input.Number, string.IsNullOrEmpty(input.Unit) ? spec.DefaultUnit : input.Unit);

            // A property whose unit follows its coefficient (R_m's A) is kept as entered, in the unit it was entered in; compare it in SI too (B-0 review, M3).
            var y = MaterialProperties.Find(property, out _) is { SiUnit: null } && MaterialLibrary.ToSi(scalar.Si, scalar.SiUnit) is { Value: { } siValue } ? siValue : scalar.Si;
            return Math.Abs(x - y) <= 1e-9 * Math.Max(1.0, Math.Max(Math.Abs(x), Math.Abs(y)));
        }

        return resolved.Rows is { } rows && MatchesValue(input, new CalcDataValue(input.Symbol, Rows: rows), spec);
    }
}
