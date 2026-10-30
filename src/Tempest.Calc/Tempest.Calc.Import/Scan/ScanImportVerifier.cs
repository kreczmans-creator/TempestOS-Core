using Tempest.Calc.Sheets;

namespace Tempest.Calc.Import.Scan;

/// <summary>The inputs after verification and one sentence for each scan-import provenance that was downgraded.</summary>
public sealed record ScanVerification(IReadOnlyList<CalcInputValue> Inputs, IReadOnlyList<string> Downgraded);

/// <summary>
/// A <see cref="CalcProvenanceKind.ScanImport"/> provenance says "this value was fitted from that scan, through that profile". Nothing in the
/// provenance itself stops a person (or a pasted CSV) from writing it onto a value that was not, or from editing the value afterwards and keeping the
/// claim. This check holds every claim to the calculation's own import records, as <c>CadImportVerifier</c> does for CAD imports: a record must hold a value
/// written at that very place (<c>D</c>, <c>contributors[0].upper</c>, <c>measured{D_s}.mean</c>) with exactly that provenance — software, version, stage,
/// serials, alignment, file hash, time, feature, profile — and the value must still be the one written. A claim on a whole table is never honoured
/// (the import never gives one): a table's provenance is its cells'. Where a stack row's band was written as ±kσ for the k in force, the claim lapses if k
/// changes. A claim that fails is recorded as a manual entry instead, and the caller says so. Records are searched whole, not only the latest per profile,
/// so an earlier import whose values are still in use keeps its provenance.
/// </summary>
public static class ScanImportVerifier
{
    public static ScanVerification Verify(IReadOnlyList<CalcInputValue> inputs, IReadOnlyList<ScanImportRecord>? records)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var downgraded = new List<string>();
        var result = new List<CalcInputValue>(inputs.Count);
        var k = inputs.FirstOrDefault(i => i.Symbol == "k")?.Number;

        foreach (var input in inputs)
        {
            var current = input;

            if (current.Provenance is { Kind: CalcProvenanceKind.ScanImport })
            {
                if (current.Rows is not null || !Holds(current.Provenance, current.Symbol, records, current.Number, current.Unit, current.Text, k))
                {
                    downgraded.Add($"'{current.Symbol}'");
                    current = current with { Provenance = CalcProvenance.Manual };
                }
            }

            if (current.Rows is { } rows && rows.Any(r => r is not null && r.Cells.Values.Any(c => c?.Provenance is { Kind: CalcProvenanceKind.ScanImport })))
            {
                var newRows = new List<CalcRow>(rows.Count);
                var changed = false;
                var seen = new Dictionary<string, int>(StringComparer.Ordinal);

                for (var r = 0; r < rows.Count; r++)
                {
                    var row = rows[r];

                    if (row is null)
                    {
                        newRows.Add(row!);
                        continue;
                    }

                    var key = RowKey(current.Symbol, r, row, seen);

                    if (!row.Cells.Values.Any(c => c?.Provenance is { Kind: CalcProvenanceKind.ScanImport }))
                    {
                        newRows.Add(row);
                        continue;
                    }

                    var cells = new Dictionary<string, CalcCell>(row.Cells, StringComparer.Ordinal);

                    foreach (var (column, cell) in row.Cells)
                    {
                        if (cell?.Provenance is { Kind: CalcProvenanceKind.ScanImport } p && !Holds(p, $"{key}.{column}", records, cell.Number, cell.Unit, cell.Text, k))
                        {
                            cells[column] = cell with { Provenance = CalcProvenance.Manual };
                            downgraded.Add($"'{key}.{column}'");
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

        return new ScanVerification(result, downgraded);
    }

    /// <summary>The sentence a caller shows when provenances were downgraded.</summary>
    public static string Describe(IReadOnlyList<string> downgraded) =>
        downgraded.Count == 0
            ? string.Empty
            : $"Scan-import provenance was removed from {string.Join(", ", downgraded.Take(8))}{(downgraded.Count > 8 ? $" and {downgraded.Count - 8} more" : string.Empty)}: the value no longer matches what the import wrote, or no import wrote it. It is kept as a manual entry.";

    /// <summary>
    /// The place a table row is known by: <c>symbol{dim}</c> for a row that names its dimension (a repeat of the same dimension is <c>symbol{dim}~2</c>, so a
    /// second row cannot borrow the first one's claim), else <c>symbol[index]</c>.
    /// </summary>
    public static string RowKey(string symbol, int index, CalcRow row, IDictionary<string, int> seen)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(seen);

        if (row.Get("dim")?.Text is not { Length: > 0 } dim)
            return $"{symbol}[{index}]";

        var key = $"{symbol}{{{dim}}}";
        seen[key] = seen.TryGetValue(key, out var n) ? n + 1 : 1;
        return seen[key] == 1 ? key : $"{key}~{seen[key]}";
    }

    /// <summary>
    /// The import records after <paramref name="latest"/> is added, by the same rule as the CAD import's with the test made on the provenance itself. A record of
    /// the same source (profile) that this import supersedes is kept while any input still carries, exactly, a provenance that record wrote and the new record did
    /// not write again — so a later stage or file imported through the same profile, or a new profile version that drops a feature (the same file, a different
    /// version), does not strip the values it did not rewrite — and dropped once nothing does. Records of other sources are kept. The list is capped; uncited
    /// superseded records go first, and the record just added is never the victim.
    /// </summary>
    public static IReadOnlyList<ScanImportRecord> Merge(IReadOnlyList<ScanImportRecord>? existing, ScanImportRecord latest, IReadOnlyList<CalcInputValue> inputs, int maxRecords = 20)
    {
        ArgumentNullException.ThrowIfNull(latest);
        ArgumentNullException.ThrowIfNull(inputs);
        var cited = CitedProvenances(inputs);
        var rewritten = Provenances(latest);
        var list = new List<ScanImportRecord>();

        foreach (var old in existing ?? [])
        {
            if (ReferenceEquals(old, latest))
                continue;

            if (!string.Equals(old.Key, latest.Key, StringComparison.Ordinal) || Provenances(old).Any(p => cited.Contains(p) && !rewritten.Contains(p)))
                list.Add(old);
        }

        list.Add(latest);

        while (list.Count > maxRecords)
        {
            var newest = list.GroupBy(r => r.Key, StringComparer.Ordinal).Select(g => g.MaxBy(r => r.ImportedAt)!).ToHashSet();
            var others = list.Where(r => !ReferenceEquals(r, latest)).OrderBy(r => r.ImportedAt).ToList();
            var victim = others.FirstOrDefault(r => !newest.Contains(r) && !Provenances(r).Overlaps(cited))
                ?? others.FirstOrDefault(r => !Provenances(r).Overlaps(cited))
                ?? others.FirstOrDefault(r => !newest.Contains(r))
                ?? others[0];
            list.Remove(victim);
        }

        return list;
    }

    /// <summary>The latest record of a source (the one a re-import is compared with).</summary>
    public static ScanImportRecord? Latest(IReadOnlyList<ScanImportRecord>? records, string key) =>
        records?.Where(r => string.Equals(r.Key, key, StringComparison.Ordinal)).MaxBy(r => r.ImportedAt);

    private static HashSet<CalcProvenance> Provenances(ScanImportRecord record) =>
        record.Features.SelectMany(f => f.Pushed ?? []).Select(v => v.Provenance).OfType<CalcProvenance>().ToHashSet();

    private static HashSet<CalcProvenance> CitedProvenances(IEnumerable<CalcInputValue> inputs)
    {
        var cited = new HashSet<CalcProvenance>();

        foreach (var input in inputs)
        {
            if (input.Provenance is { Kind: CalcProvenanceKind.ScanImport } p)
                cited.Add(p);

            foreach (var cell in (input.Rows ?? []).SelectMany(r => r?.Cells.Values ?? []))
            {
                if (cell?.Provenance is { Kind: CalcProvenanceKind.ScanImport } cp)
                    cited.Add(cp);
            }
        }

        return cited;
    }

    private static bool Holds(CalcProvenance provenance, string path, IReadOnlyList<ScanImportRecord>? records, double? number, string? unit, string? text, double? currentK)
    {
        if (records is null || ScanImportProvenance.TryParse(provenance) is null)
            return false;

        foreach (var record in records)
        {
            foreach (var feature in record.Features)
            {
                var pushed = feature.Pushed;

                if (pushed is null)
                    continue;

                foreach (var value in pushed)
                {
                    if (!string.Equals(value.Path, path, StringComparison.Ordinal) || value.Provenance != provenance || !SameValue(value, number, unit, text))
                        continue;

                    // A band written as ±kσ for one k is a band for that k only.
                    if (pushed.FirstOrDefault(v => v.Path == "@k") is { Number: { } writtenK } && !SameNumber(writtenK, currentK))
                        continue;

                    return true;
                }
            }
        }

        return false;
    }

    private static bool SameValue(ScanPushedValue pushed, double? number, string? unit, string? text) =>
        string.Equals(pushed.Unit, number is null ? null : unit, StringComparison.Ordinal) && string.Equals(pushed.Text, text, StringComparison.Ordinal) && SameNumber(pushed.Number, number);

    private static bool SameNumber(double? x, double? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        return x == y || Math.Abs(x.Value - y.Value) <= 1e-12 * Math.Max(Math.Abs(x.Value), Math.Abs(y.Value));
    }
}
