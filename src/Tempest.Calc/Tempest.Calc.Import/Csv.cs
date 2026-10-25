using System.Globalization;
using System.Text;

namespace Tempest.Calc.Import;

/// <summary>A parsed CSV table: rows of fields. Row numbers in messages are 1-based file lines of the record's first line.</summary>
public sealed record CsvTable(IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> Lines)
{
    public int ColumnCount => Rows.Count == 0 ? 0 : Rows.Max(r => r.Count);
}

/// <summary>
/// A strict RFC 4180 reader: records end at CRLF, LF or CR; a field may be quoted, and inside quotes a doubled quote is
/// a quote and line breaks are data; a quote may not appear inside an unquoted field; text after a closing quote must be
/// the delimiter or the end of the record; a quote left open at the end is refused. Blank lines are skipped. The row,
/// column and field-length caps hold while reading, so an oversized file is refused after the cap, not after it has been
/// parsed.
/// </summary>
public static class CsvReader
{
    /// <summary>Reads <paramref name="text"/> (already decoded and control-checked) with <paramref name="delimiter"/>.</summary>
    public static CsvTable Read(string text, char delimiter = ',', ImportBudget? budget = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        budget ??= new ImportBudget();
        var limits = budget.Limits;

        if (delimiter is '"' or '\r' or '\n')
            throw new ArgumentException("A CSV delimiter cannot be a quote or a line break.", nameof(delimiter));

        var rows = new List<IReadOnlyList<string>>();
        var lines = new List<int>();
        var row = new List<string>();
        var field = new StringBuilder();
        var line = 1;
        var recordLine = 1;
        var i = 0;
        var n = text.Length;
        var fieldStarted = false;

        while (i < n)
        {
            budget.Tick();
            var c = text[i];

            if (!fieldStarted && c == '"')
            {
                // Quoted field.
                i++;
                var closed = false;

                while (i < n)
                {
                    var q = text[i];

                    if (q == '"')
                    {
                        if (i + 1 < n && text[i + 1] == '"')
                        {
                            Append(field, '"', limits, recordLine);
                            i += 2;
                            continue;
                        }

                        closed = true;
                        i++;
                        break;
                    }

                    if (q == '\n')
                        line++;

                    Append(field, q, limits, recordLine);
                    i++;
                    budget.Tick();
                }

                if (!closed)
                    throw Malformed($"A quoted field starting on line {recordLine} is never closed.");

                if (i < n && text[i] != delimiter && text[i] != '\r' && text[i] != '\n')
                    throw Malformed($"Line {line}: text follows a closing quote.");

                fieldStarted = true;
                continue;
            }

            if (c == delimiter)
            {
                EndField(row, field, limits, recordLine);
                fieldStarted = false;
                i++;
                continue;
            }

            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < n && text[i + 1] == '\n')
                    i++;

                i++;
                var blank = row.Count == 0 && field.Length == 0 && !fieldStarted;

                if (!blank)
                {
                    EndField(row, field, limits, recordLine);
                    AddRow(rows, lines, row, limits, recordLine);
                }

                row = [];
                fieldStarted = false;
                line++;
                recordLine = line;
                continue;
            }

            if (c == '"')
                throw Malformed($"Line {line}: a quote appears inside an unquoted field.");

            Append(field, c, limits, recordLine);
            fieldStarted = true;
            i++;
        }

        if (row.Count > 0 || field.Length > 0 || fieldStarted)
        {
            EndField(row, field, limits, recordLine);
            AddRow(rows, lines, row, limits, recordLine);
        }

        return new CsvTable(rows, lines);
    }

    /// <summary>The delimiter the first record suggests among comma, semicolon and tab, counting outside quotes; comma when none stands out.</summary>
    public static char DetectDelimiter(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var comma = 0;
        var semicolon = 0;
        var tab = 0;
        var inQuotes = false;

        foreach (var c in text)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (!inQuotes && c is '\n' or '\r')
                break;
            else if (!inQuotes)
            {
                switch (c)
                {
                    case ',':
                        comma++;
                        break;
                    case ';':
                        semicolon++;
                        break;
                    case '\t':
                        tab++;
                        break;
                }
            }
        }

        if (semicolon > comma && semicolon >= tab)
            return ';';

        return tab > comma && tab > semicolon ? '\t' : ',';
    }

    private static void Append(StringBuilder field, char c, ImportLimits limits, int line)
    {
        if (field.Length >= limits.MaxFieldChars)
            throw new ImportRefusedException(ImportRefusalCode.FieldTooLong, $"A field on line {line} is longer than {limits.MaxFieldChars} characters.");

        field.Append(c);
    }

    private static void EndField(List<string> row, StringBuilder field, ImportLimits limits, int line)
    {
        if (row.Count >= limits.MaxColumns)
            throw new ImportRefusedException(ImportRefusalCode.TooManyColumns, $"Line {line} has more than {limits.MaxColumns} columns.");

        row.Add(field.ToString());
        field.Clear();
    }

    private static void AddRow(List<IReadOnlyList<string>> rows, List<int> lines, List<string> row, ImportLimits limits, int line)
    {
        if (rows.Count >= limits.MaxRows)
            throw new ImportRefusedException(ImportRefusalCode.TooManyRows, $"The file has more than {limits.MaxRows} rows.");

        rows.Add(row);
        lines.Add(line);
    }

    private static ImportRefusedException Malformed(string message) => new(ImportRefusalCode.MalformedStructure, "The CSV is malformed: " + message);
}

/// <summary>
/// Writes CSV that is safe to open in a spreadsheet. A text field whose first visible character is <c>=</c>, <c>+</c>,
/// <c>-</c> or <c>@</c> (or a tab or carriage return, or their full-width forms) is a formula to Excel, LibreOffice and
/// Sheets, so it is written with a leading apostrophe; numbers are written by <see cref="Number"/>, which cannot start
/// with anything but a digit or a sign and is never passed through the text path. Fields are quoted per RFC 4180.
/// </summary>
public static class CsvSafeWriter
{
    private static readonly char[] FormulaStarts = ['=', '+', '-', '@', '\t', '\r', '＝', '＋', '－', '＠'];

    /// <summary>Whether a spreadsheet would read <paramref name="text"/> as a formula.</summary>
    public static bool IsFormulaLike(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (c is ' ' or '\u00A0')
                continue;

            return Array.IndexOf(FormulaStarts, c) >= 0;
        }

        return false;
    }

    /// <summary>Defuses a formula-like text with a leading apostrophe; any other text is returned unchanged.</summary>
    public static string Neutralise(string? text)
    {
        var s = text ?? string.Empty;
        return IsFormulaLike(s) ? "'" + s : s;
    }

    /// <summary>A text field: neutralised, then quoted if it holds a delimiter, quote or line break.</summary>
    public static string Text(string? text, char delimiter = ',')
    {
        var s = Neutralise(text);
        return s.IndexOfAny([delimiter, '"', '\r', '\n']) >= 0 ? "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : s;
    }

    /// <summary>A number field: invariant, round-trippable; a non-finite value is written as an empty field (never as text a spreadsheet could read).</summary>
    public static string Number(double value) =>
        double.IsFinite(value) ? value.ToString("R", CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>One CSV record from already-built fields (use <see cref="Text"/> and <see cref="Number"/>), ended by a line break.</summary>
    public static string Row(IEnumerable<string> fields, char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(fields);
        return string.Join(delimiter, fields) + "\r\n";
    }
}
