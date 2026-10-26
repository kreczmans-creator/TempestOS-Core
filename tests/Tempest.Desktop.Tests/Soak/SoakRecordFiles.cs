using System.Globalization;
using System.Text;
using Tempest.Calc.Library;

namespace Tempest.Desktop.Tests.Soak;

/// <summary>
/// Synthetic record-import files (Addendum C, C-3) built to sit exactly at the importer's caps or one past them: 5 MB, 1,001 rows (the header
/// among them), 64 columns, 4,096-character fields. Every value is invented; nothing here is a standard's or a catalogue's table. The columns the
/// template does not have are padding, named <c>pad01</c>... and left out of the import by the column map, which is how a person leaves a spreadsheet's
/// extra columns out.
/// </summary>
internal static class SoakRecordFiles
{
    /// <summary>The importer's byte cap (the bounded reader's default).</summary>
    public static long ByteCap => Tempest.Calc.Import.ImportLimits.DefaultMaxBytes;

    /// <summary>The importer's row cap: the header row and 1,000 data rows.</summary>
    public const int RowCap = RecordImportService.MaxFileRows;

    /// <summary>The importer's column cap.</summary>
    public const int ColumnCap = 64;

    /// <summary>The importer's field cap: the longest text a single field may hold.</summary>
    public const int FieldCap = 4096;

    /// <summary>One template the soak imports, with a generator for valid data rows.</summary>
    /// <param name="Id">The template's number.</param>
    /// <param name="Shape">What kind of import it is, for the report.</param>
    /// <param name="MaxRows">The most data rows the template takes (1,000 for a per-row template; a table template has its own limit).</param>
    /// <param name="Row">The cells of data row <c>i</c> (from 0), one per template column, valid against the template's checks.</param>
    /// <param name="PerRow">Whether each row becomes a record of its own.</param>
    internal sealed record Sample(string Id, string Shape, int MaxRows, Func<int, string[]> Row, bool PerRow)
    {
        public RecordTemplate Template => RecordTemplates.Find(Id)!;
    }

    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>The representative set: a per-row text table, a per-row banded table, a per-row table with labels, a one-file table, a table with scalars, a fixed twelve-row table and the materials.</summary>
    public static IReadOnlyList<Sample> Samples { get; } =
    [
        new("24", "one record per row (a designation)", 1000, i => [$"SOAK-BRG-{i:0000}", N(10 + i * 0.5), N(5 + i * 0.25)], true),
        new("12", "one record per row (a grade and thickness band)", 1000, i => [$"SOAK-S{i:0000}", "0", "40", N(275 + i % 100), N(410 + i % 100)], true),
        new("06", "one record per row, labels in the title", 1000, i => ["air" + (i / 20).ToString("0000", CultureInfo.InvariantCulture), "PVC", N(10 + i % 20 * 5), N(1.2 - i % 20 * 0.02)], true),
        new("04", "one record per file: a table", 60, i => [N(1 + i * 0.5), string.Empty, N(10 + i * 3)], false),
        new("02", "one record per file: a table with scalars read from its first row", 40, i => i == 0 ? ["50000", "10", "0.1", "5000"] : [string.Empty, string.Empty, N(0.1 + i * 0.1), N(5000 - i * 100)], false),
        new("45", "one record per file: twelve fixed rows", 12, i => [new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" }[i], N(2 + i), N(80 - i), "30"], false),
        new("62", "materials: one per row, into the material library", 300, i => [$"SOAK-MAT-{i:0000}", N(200 + i % 10), N(80 + i % 5), "0.3", "7850", "1.2e-5", "50", N(275 + i % 100), N(410 + i % 100), string.Empty, string.Empty, string.Empty, string.Empty], true),
    ];

    /// <summary>The padding headers <c>pad01</c>... for <paramref name="count"/> columns.</summary>
    public static IReadOnlyList<string> PadHeaders(int count) => [.. Enumerable.Range(1, count).Select(i => $"pad{i:00}")];

    /// <summary>The column map that leaves the padding columns out of the import.</summary>
    public static IReadOnlyDictionary<string, string?> PadMap(int count) =>
        PadHeaders(count).ToDictionary(h => h, _ => (string?)null, StringComparer.Ordinal);

    /// <summary>
    /// A file of the sample's header (plus <paramref name="padColumns"/> padding columns) and <paramref name="dataRows"/> valid data rows. When
    /// <paramref name="totalBytes"/> is above zero the padding cells (at most <paramref name="padChars"/> characters each, row by row) are filled until the file is
    /// exactly that many bytes (the text is ASCII, so bytes and characters are the same).
    /// </summary>
    public static byte[] Build(Sample sample, int dataRows, int padColumns = 0, int padChars = 0, long totalBytes = 0)
    {
        var header = sample.Template.Header + string.Concat(PadHeaders(padColumns).Select(h => "," + h));
        var rows = Enumerable.Range(0, dataRows).Select(sample.Row).ToList();
        var text = new StringBuilder(header).Append("\r\n");
        var fixedLength = text.Length + rows.Sum(r => string.Join(',', r).Length + padColumns + 2);
        var remaining = totalBytes > 0 ? totalBytes - fixedLength : 0;

        if (totalBytes > 0 && (remaining < 0 || remaining > (long)dataRows * padColumns * padChars))
            throw new InvalidOperationException($"A file of {totalBytes:N0} bytes cannot be made of {dataRows:N0} rows of {padColumns} padding columns of at most {padChars:N0} characters.");

        foreach (var row in rows)
        {
            text.Append(string.Join(',', row));

            for (var c = 0; c < padColumns; c++)
            {
                var n = (int)Math.Min(padChars, remaining);
                remaining -= n;
                text.Append(',').Append('p', n);
            }

            text.Append("\r\n");
        }

        return new UTF8Encoding(false).GetBytes(text.ToString());
    }

    /// <summary>The same valid file with one data row (0-based) replaced by <paramref name="cells"/>.</summary>
    public static byte[] WithRow(Sample sample, int dataRows, int badRow, string[] cells)
    {
        var lines = new List<string> { sample.Template.Header };
        lines.AddRange(Enumerable.Range(0, dataRows).Select(i => string.Join(',', i == badRow ? cells : sample.Row(i))));
        return new UTF8Encoding(false).GetBytes(string.Join("\r\n", lines) + "\r\n");
    }
}
