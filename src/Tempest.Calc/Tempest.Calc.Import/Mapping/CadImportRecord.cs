using System.Globalization;
using System.Text;
using Tempest.Calc.Import.Cad;

namespace Tempest.Calc.Import.Mapping;

/// <summary>One parameter of an imported file as it was when imported: what the next import is compared against.</summary>
public sealed record CadImportedParameter(
    string Name,
    CadParameterKind Kind,
    string? Target = null,
    double? Number = null,
    string? Unit = null,
    double? Si = null,
    string? Text = null,
    bool? Flag = null);

/// <summary>
/// What a calculation remembers about an import: the file (name and SHA-256), the system, version, part and revision, the
/// profile, who imported it and when, and every parameter the file held. It is kept on the instance (and so frozen with an
/// issued revision); a later import from the same source (same system, profile and part) is compared with it.
/// </summary>
public sealed record CadImportRecord(
    string Key,
    string FileName,
    string FileSha256,
    CadSystem System,
    CadSourceFormat Format,
    string? CadVersion,
    string? PartName,
    string? PartRevision,
    string? Configuration,
    string ProfileId,
    int ProfileVersion,
    DateTimeOffset ImportedAt,
    string ImportedBy,
    IReadOnlyList<CadImportedParameter> Parameters)
{
    /// <summary>
    /// The identity of an import source: the same CAD system and mapping profile. The part name is not part of it: it often falls back
    /// to the file name, and a renamed file must still be compared with the import it replaces (the record keeps the part name).
    /// </summary>
    public static string KeyFor(CadSystem system, string profileId) =>
        string.Create(CultureInfo.InvariantCulture, $"{system}|{profileId}");
}

public enum CadChangeKind
{
    Added,
    Removed,
    Changed,
}

/// <summary>One difference between an earlier import and this one. <see cref="AffectsInput"/> is true when a mapped input moves with it.</summary>
public sealed record CadChange(CadChangeKind Kind, string Name, string? Target, string? Before, string? After, bool AffectsInput);

/// <summary>The differences between the previous import from a source and this one.</summary>
public sealed record CadImportDiff(bool FirstImport, string? PreviousSha256, IReadOnlyList<CadChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;

    public int Count(CadChangeKind kind) => Changes.Count(c => c.Kind == kind);

    /// <summary>A one-line summary for a status bar.</summary>
    public string Summary => FirstImport
        ? "First import from this source."
        : Changes.Count == 0
            ? "No change since the previous import."
            : string.Create(CultureInfo.InvariantCulture, $"{Count(CadChangeKind.Added)} added, {Count(CadChangeKind.Removed)} removed, {Count(CadChangeKind.Changed)} changed since the previous import.");

    public static CadImportDiff Compute(CadImportRecord? previous, IReadOnlyList<CadImportedParameter> now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (previous is null)
            return new CadImportDiff(true, null, []);

        var before = previous.Parameters.GroupBy(p => p.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var after = now.GroupBy(p => p.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var changes = new List<CadChange>();

        foreach (var (name, p) in after)
        {
            if (!before.TryGetValue(name, out var old))
                changes.Add(new CadChange(CadChangeKind.Added, name, p.Target, null, Describe(p), p.Target is not null));
            else if (!Same(old, p))
                changes.Add(new CadChange(CadChangeKind.Changed, name, p.Target ?? old.Target, Describe(old), Describe(p), p.Target is not null || old.Target is not null));
        }

        foreach (var (name, old) in before)
        {
            if (!after.ContainsKey(name))
                changes.Add(new CadChange(CadChangeKind.Removed, name, old.Target, Describe(old), null, old.Target is not null));
        }

        return new CadImportDiff(false, previous.FileSha256, changes.OrderBy(c => c.Name, StringComparer.Ordinal).ToList());
    }

    private static bool Same(CadImportedParameter a, CadImportedParameter b)
    {
        if (a.Kind != b.Kind || !string.Equals(a.Target, b.Target, StringComparison.Ordinal) || !string.Equals(a.Text, b.Text, StringComparison.Ordinal) || a.Flag != b.Flag)
            return false;

        var x = a.Si ?? a.Number;
        var y = b.Si ?? b.Number;

        if (x is null || y is null)
            return x is null && y is null;

        return x == y || Math.Abs(x.Value - y.Value) <= 1e-12 * Math.Max(Math.Abs(x.Value), Math.Abs(y.Value));
    }

    /// <summary>A parameter written the way a person reads it: <c>25.4 mm</c>, <c>"S275"</c>, <c>yes</c>, <c>= expression</c>.</summary>
    public static string Describe(CadImportedParameter p)
    {
        ArgumentNullException.ThrowIfNull(p);

        return p.Kind switch
        {
            CadParameterKind.Number => p.Number is { } n ? Engine.Values.CalcValue.FormatNumber(n, 8) + (string.IsNullOrEmpty(p.Unit) ? string.Empty : " " + p.Unit) : "—",
            CadParameterKind.Boolean => p.Flag == true ? "yes" : "no",
            CadParameterKind.Text => "\"" + p.Text + "\"",
            _ => "= " + p.Text,
        };
    }
}

/// <summary>The preview and the diff as CSV that is safe to open in a spreadsheet (every text from a CAD file is neutralised).</summary>
public static class CadImportReport
{
    public static string RowsCsv(IEnumerable<CadMappedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["status", "cad_name", "location", "target", "value", "message"]));

        foreach (var r in rows)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                r.Status.ToString().ToLowerInvariant(),
                CsvSafeWriter.Text(r.CadName),
                CsvSafeWriter.Text(r.Location),
                CsvSafeWriter.Text(r.Target),
                CsvSafeWriter.Text(r.Value),
                CsvSafeWriter.Text(r.Message),
            ]));
        }

        return sb.ToString();
    }

    public static string ChangesCsv(CadImportDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["change", "cad_name", "target", "before", "after", "affects_input"]));

        foreach (var c in diff.Changes)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                c.Kind.ToString().ToLowerInvariant(),
                CsvSafeWriter.Text(c.Name),
                CsvSafeWriter.Text(c.Target),
                CsvSafeWriter.Text(c.Before),
                CsvSafeWriter.Text(c.After),
                c.AffectsInput ? "yes" : "no",
            ]));
        }

        return sb.ToString();
    }
}
