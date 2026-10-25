using System.Globalization;
using System.Text;

namespace Tempest.Calc.Import.Scan;

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
