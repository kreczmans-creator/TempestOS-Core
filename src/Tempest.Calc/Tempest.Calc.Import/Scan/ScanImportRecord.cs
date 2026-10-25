using System.Globalization;
using System.Text;

namespace Tempest.Calc.Import.Scan;

/// <summary>One fitted feature as it was when imported: what the next import is compared against. Values are in the profile's unit.</summary>
public sealed record ScanImportedFeature(
    string Feature,
    string Stage,
    string? Target,
    int Count,
    double Mean,
    double Sigma,
    double? Cpk,
    double? Ppk,
    double? Nominal,
    string SetSha256,
    string Unit);

/// <summary>A file of an import: its name and SHA-256, and the stage and serial the person entered for it.</summary>
public sealed record ScanImportFile(string FileName, string Sha256, string? Stage, string? Serial);

/// <summary>
/// What a calculation remembers about a scan import: the files (name and SHA-256), the software and version, the alignment the person entered,
/// the profile, who imported it and when, and every feature fitted. It is kept on the instance (and so frozen with an issued revision); a later import
/// through the same profile is compared with it.
/// </summary>
public sealed record ScanImportRecord(
    string Key,
    string ProfileId,
    int ProfileVersion,
    string Software,
    string? SoftwareVersion,
    string? Alignment,
    IReadOnlyList<ScanImportFile> Files,
    DateTimeOffset ImportedAt,
    string ImportedBy,
    IReadOnlyList<ScanImportedFeature> Features)
{
    /// <summary>The identity of an import source: the same scan profile.</summary>
    public static string KeyFor(string profileId) => "scan|" + profileId;
}

public enum ScanChangeKind
{
    Added,
    Removed,
    Changed,
}

/// <summary>One difference between an earlier import and this one. <see cref="AffectsInput"/> is true when a mapped input moves with it.</summary>
public sealed record ScanChange(ScanChangeKind Kind, string Feature, string Stage, string? Target, string? Before, string? After, bool AffectsInput);

/// <summary>The differences between the previous import through a profile and this one.</summary>
public sealed record ScanImportDiff(bool FirstImport, IReadOnlyList<string> PreviousFiles, IReadOnlyList<ScanChange> Changes)
{
    public bool HasChanges => Changes.Count > 0;

    public int Count(ScanChangeKind kind) => Changes.Count(c => c.Kind == kind);

    /// <summary>A one-line summary for a status bar.</summary>
    public string Summary => FirstImport
        ? "First import through this profile."
        : Changes.Count == 0
            ? "No change since the previous import."
            : string.Create(CultureInfo.InvariantCulture, $"{Count(ScanChangeKind.Added)} added, {Count(ScanChangeKind.Removed)} removed, {Count(ScanChangeKind.Changed)} changed since the previous import.");

    public static ScanImportDiff Compute(ScanImportRecord? previous, IReadOnlyList<ScanImportedFeature> now)
    {
        ArgumentNullException.ThrowIfNull(now);

        if (previous is null)
            return new ScanImportDiff(true, [], []);

        static string KeyOf(ScanImportedFeature f) => f.Feature.ToUpperInvariant() + "\u0001" + f.Stage.ToUpperInvariant();

        var before = previous.Features.GroupBy(KeyOf, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var after = now.GroupBy(KeyOf, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var changes = new List<ScanChange>();

        foreach (var (key, f) in after)
        {
            if (!before.TryGetValue(key, out var old))
                changes.Add(new ScanChange(ScanChangeKind.Added, f.Feature, f.Stage, f.Target, null, Describe(f), f.Target is not null));
            else if (!Same(old, f))
                changes.Add(new ScanChange(ScanChangeKind.Changed, f.Feature, f.Stage, f.Target ?? old.Target, Describe(old), Describe(f), f.Target is not null || old.Target is not null));
        }

        foreach (var (key, old) in before)
        {
            if (!after.ContainsKey(key))
                changes.Add(new ScanChange(ScanChangeKind.Removed, old.Feature, old.Stage, old.Target, Describe(old), null, old.Target is not null));
        }

        return new ScanImportDiff(false, previous.Files.Select(f => f.Sha256).ToList(), changes.OrderBy(c => c.Feature, StringComparer.Ordinal).ThenBy(c => c.Stage, StringComparer.Ordinal).ToList());
    }

    private static bool Same(ScanImportedFeature a, ScanImportedFeature b) =>
        a.Count == b.Count && string.Equals(a.Target, b.Target, StringComparison.Ordinal) && Close(a.Mean, b.Mean) && Close(a.Sigma, b.Sigma) && Close(a.Nominal, b.Nominal);

    private static bool Close(double? x, double? y)
    {
        if (x is null || y is null)
            return x is null && y is null;

        return x == y || Math.Abs(x.Value - y.Value) <= 1e-12 * Math.Max(Math.Abs(x.Value), Math.Abs(y.Value));
    }

    /// <summary>A fitted feature the way a person reads it: <c>n = 30, mean 51.4855 mm, σ 0.0099 mm</c>.</summary>
    public static string Describe(ScanImportedFeature f)
    {
        ArgumentNullException.ThrowIfNull(f);
        var unit = f.Unit.Length == 0 ? string.Empty : " " + f.Unit;
        return string.Create(CultureInfo.InvariantCulture, $"n = {f.Count}, mean {Engine.Values.CalcValue.FormatNumber(f.Mean, 8)}{unit}, σ {Engine.Values.CalcValue.FormatNumber(f.Sigma, 6)}{unit}");
    }
}

/// <summary>The statistics, the problems and the preview as CSV that is safe to open in a spreadsheet (every text from a scan file is neutralised).</summary>
public static class ScanImportReport
{
    public static string StatisticsCsv(IEnumerable<ScanFeatureStats> stats, string unit)
    {
        ArgumentNullException.ThrowIfNull(stats);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["feature", "stage", "n", "n_rows", "parts", "unit", "nominal", "lsl", "usl", "mean", "sigma", "sigma_within", "min", "max", "cp", "cpk", "pp", "ppk", "ad_a2", "ad_a2_star", "ad_p", "normality", "outliers", "warnings"]));

        foreach (var s in stats)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                CsvSafeWriter.Text(s.Feature),
                CsvSafeWriter.Text(s.Stage),
                s.Count.ToString(CultureInfo.InvariantCulture),
                s.Total.ToString(CultureInfo.InvariantCulture),
                s.Parts.ToString(CultureInfo.InvariantCulture),
                CsvSafeWriter.Text(unit),
                Num(s.Nominal),
                Num(s.Lsl),
                Num(s.Usl),
                s.Computable ? CsvSafeWriter.Number(s.Mean) : string.Empty,
                s.Computable ? CsvSafeWriter.Number(s.Sigma) : string.Empty,
                s.Computable ? CsvSafeWriter.Number(s.SigmaWithin) : string.Empty,
                s.Computable ? CsvSafeWriter.Number(s.Min) : string.Empty,
                s.Computable ? CsvSafeWriter.Number(s.Max) : string.Empty,
                Num(s.Cp),
                Num(s.Cpk),
                Num(s.Pp),
                Num(s.Ppk),
                Num(s.Normality.A2),
                Num(s.Normality.AStar2),
                Num(s.Normality.PValue),
                s.Normality.Verdict.ToString().ToLowerInvariant(),
                s.Outliers.Count.ToString(CultureInfo.InvariantCulture),
                CsvSafeWriter.Text(string.Join(" | ", s.Warnings)),
            ]));
        }

        return sb.ToString();
    }

    public static string ProblemsCsv(ScanData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["kind", "column", "count", "examples"]));

        foreach (var g in data.Problems)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                g.Kind.ToString().ToLowerInvariant(),
                CsvSafeWriter.Text(g.Column),
                g.Count.ToString(CultureInfo.InvariantCulture),
                CsvSafeWriter.Text(string.Join(" | ", g.Examples)),
            ]));
        }

        return sb.ToString();
    }

    public static string RowsCsv(IEnumerable<ScanMappedRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["status", "feature", "stage", "target", "value", "message"]));

        foreach (var r in rows)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                r.Status.ToString().ToLowerInvariant(),
                CsvSafeWriter.Text(r.Feature),
                CsvSafeWriter.Text(r.Stage),
                CsvSafeWriter.Text(r.Target),
                CsvSafeWriter.Text(r.Value),
                CsvSafeWriter.Text(r.Message),
            ]));
        }

        return sb.ToString();
    }

    public static string ChangesCsv(ScanImportDiff diff)
    {
        ArgumentNullException.ThrowIfNull(diff);
        var sb = new StringBuilder();
        sb.Append(CsvSafeWriter.Row(["change", "feature", "stage", "target", "before", "after", "affects_input"]));

        foreach (var c in diff.Changes)
        {
            sb.Append(CsvSafeWriter.Row(
            [
                c.Kind.ToString().ToLowerInvariant(),
                CsvSafeWriter.Text(c.Feature),
                CsvSafeWriter.Text(c.Stage),
                CsvSafeWriter.Text(c.Target),
                CsvSafeWriter.Text(c.Before),
                CsvSafeWriter.Text(c.After),
                c.AffectsInput ? "yes" : "no",
            ]));
        }

        return sb.ToString();
    }

    private static string Num(double? v) => v is { } x ? CsvSafeWriter.Number(x) : string.Empty;
}
