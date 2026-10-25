using Tempest.Calc.Import.Cad;
using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Mapping;

/// <summary>The inputs after verification and one sentence for each CAD-import provenance that was downgraded.</summary>
public sealed record CadVerification(IReadOnlyList<CalcInputValue> Inputs, IReadOnlyList<string> Downgraded);

/// <summary>
/// A <see cref="CalcProvenanceKind.CadImport"/> provenance says "this value came from that file, through that profile". Nothing in the
/// provenance itself stops a person (or a pasted CSV) from writing it onto a value that did not, or from editing the value afterwards
/// and keeping the claim. This check holds the claim to the calculation's own import records: the provenance's file hash and profile
/// must name a record, the record must hold that CAD parameter mapped to that very input, and the value must still be the value the
/// record says was imported. A provenance that fails is recorded as a manual entry instead, and the caller says so.
/// </summary>
public static class CadImportVerifier
{
    public static CadVerification Verify(IReadOnlyList<CalcInputValue> inputs, IReadOnlyList<CadImportRecord>? records)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var downgraded = new List<string>();
        var result = new List<CalcInputValue>(inputs.Count);

        foreach (var input in inputs)
        {
            var current = input;

            if (current.Provenance is { Kind: CalcProvenanceKind.CadImport } && !Holds(current.Provenance, current.Symbol, records, current.Number, current.Unit, current.Text, current.Flag))
            {
                downgraded.Add($"'{current.Symbol}'");
                current = current with { Provenance = CalcProvenance.Manual };
            }

            if (current.Rows is { } rows && rows.Any(r => r is not null && r.Cells.Values.Any(c => c?.Provenance is { Kind: CalcProvenanceKind.CadImport })))
            {
                var newRows = new List<CalcRow>(rows.Count);
                var changed = false;

                for (var r = 0; r < rows.Count; r++)
                {
                    var row = rows[r];

                    if (row is null || !row.Cells.Values.Any(c => c?.Provenance is { Kind: CalcProvenanceKind.CadImport }))
                    {
                        newRows.Add(row!);
                        continue;
                    }

                    var cells = new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal);

                    foreach (var (column, cell) in row.Cells)
                    {
                        if (cell?.Provenance is { Kind: CalcProvenanceKind.CadImport } p && !Holds(p, $"{current.Symbol}[{r}].{column}", records, cell.Number, cell.Unit, cell.Text, cell.Flag))
                        {
                            cells[column] = cell with { Provenance = CalcProvenance.Manual };
                            downgraded.Add($"'{current.Symbol}[{r}].{column}'");
                            changed = true;
                        }
                    }

                    newRows.Add(new CalcRow(cells));
                }

                if (changed)
                    current = current with { Rows = newRows };
            }

            result.Add(current);
        }

        return new CadVerification(result, downgraded);
    }

    /// <summary>The sentence a caller shows when provenances were downgraded.</summary>
    public static string Describe(IReadOnlyList<string> downgraded) =>
        downgraded.Count == 0
            ? string.Empty
            : $"CAD-import provenance was removed from {string.Join(", ", downgraded.Take(8))}{(downgraded.Count > 8 ? $" and {downgraded.Count - 8} more" : string.Empty)}: the value no longer matches what the import recorded, or no import recorded it. It is kept as a manual entry.";

    private static bool Holds(CalcProvenance provenance, string target, IReadOnlyList<CadImportRecord>? records, double? number, string? unit, string? text, bool? flag)
    {
        if (CadImportProvenance.TryParse(provenance) is not { } info || records is null)
            return false;

        foreach (var record in records)
        {
            if (!string.Equals(record.FileSha256, info.FileSha256, StringComparison.Ordinal)
                || !string.Equals(record.ProfileId, info.ProfileId, StringComparison.Ordinal)
                || record.ProfileVersion != info.ProfileVersion
                || !SameMinute(record.ImportedAt, info.ImportedAt))
                continue;

            foreach (var parameter in record.Parameters)
            {
                if (!string.Equals(parameter.Target, target, StringComparison.Ordinal) || !CadImportProvenance.NameMatches(info.CadName, parameter.Name))
                    continue;

                return ValueMatches(parameter, number, unit, text, flag);
            }
        }

        return false;
    }

    private static bool SameMinute(DateTimeOffset a, DateTimeOffset b) =>
        new DateTimeOffset(a.UtcDateTime.Year, a.UtcDateTime.Month, a.UtcDateTime.Day, a.UtcDateTime.Hour, a.UtcDateTime.Minute, 0, TimeSpan.Zero) == new DateTimeOffset(b.UtcDateTime.Year, b.UtcDateTime.Month, b.UtcDateTime.Day, b.UtcDateTime.Hour, b.UtcDateTime.Minute, 0, TimeSpan.Zero);

    private static bool ValueMatches(CadImportedParameter parameter, double? number, string? unit, string? text, bool? flag)
    {
        switch (parameter.Kind)
        {
            case CadParameterKind.Number:
            {
                if (number is not { } n || !double.IsFinite(n) || parameter.Si is not { } expected)
                    return false;

                var si = n;

                if (!string.IsNullOrEmpty(unit))
                {
                    if (!CadUnits.TryParseEngine(unit, out var parsed))
                        return false;

                    si = parsed.ToSi(n);
                }

                return si == expected || Math.Abs(si - expected) <= 1e-9 * Math.Max(Math.Abs(si), Math.Abs(expected));
            }

            case CadParameterKind.Boolean:
                return flag is not null && flag == parameter.Flag;

            case CadParameterKind.Text:
                return text is not null && string.Equals(text, parameter.Text, StringComparison.OrdinalIgnoreCase);

            default:
                return false;
        }
    }
}
