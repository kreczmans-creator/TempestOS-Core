using System.Globalization;
using System.Text;

namespace Tempest.Calc.Sheets;

/// <summary>
/// CSV exchange of inputs and outputs (§3.8). No formulas, no macros: a
/// cell that would be read as a formula by a spreadsheet (starting with
/// <c>=</c>, <c>+</c>, <c>-</c>, <c>@</c>, tab or carriage return) is
/// prefixed with an apostrophe on export and refused on import. Files are
/// capped in size and row count.
/// </summary>
public static class CalcCsv
{
    public const int MaxBytes = 1_000_000;
    public const int MaxRows = 10_000;
    public const int MaxFieldLength = 1_000;

    /// <summary>The text a withheld (licensed) value's provenance carries; such a value is not in the file and cannot be run until the record is applied again.</summary>
    public const string WithheldMarker = "licensed value withheld";

    /// <summary>Whether a provenance says the value it belongs to was withheld on export (a licensed record).</summary>
    public static bool IsWithheld(CalcProvenance? provenance) =>
        provenance?.Reference?.Contains(WithheldMarker, StringComparison.Ordinal) == true;

    /// <summary>What to tell the user about a value that was withheld on export: where it came from and what to do.</summary>
    public static string WithheldMessage(string symbol) =>
        $"{symbol}: this value was withheld on export (licensed record) and is not in the file; apply the record again, or enter the value from your own copy.";

    /// <summary>A yes/no given as text: yes, no, true, false, 1 or 0 in any case; <see langword="null"/> for anything else.</summary>
    public static bool? ParseFlag(string text) => text.Trim().ToLowerInvariant() switch
    {
        "true" or "yes" or "1" => true,
        "false" or "no" or "0" => false,
        _ => null,
    };

    /// <summary>
    /// Writes inputs as <c>symbol,value,unit,provenance,reference,revision</c>. A table is written as its own row (the table's
    /// provenance), then one row per table row (<c>name[1]</c>) and one per cell (<c>name[1].column</c>, with the cell's value,
    /// unit and provenance), so <see cref="ImportInputs"/> rebuilds it (Black B-A-05). A table whose rows were withheld (a licensed
    /// record) is written as its own row only and stays withheld.
    /// </summary>
    public static string ExportInputs(CalcTemplate template, CalcInputSet inputs)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(inputs);

        var sb = new StringBuilder();
        sb.AppendLine("symbol,value,unit,provenance,reference,revision");

        foreach (var spec in template.Inputs)
        {
            var v = inputs.Find(spec.Symbol);

            if (v is null)
                continue;

            if (spec.Kind == CalcInputKind.Table)
            {
                ExportTable(sb, spec, v);
                continue;
            }

            var value = v switch
            {
                { Number: { } n } => n.ToString("R", CultureInfo.InvariantCulture),
                { Text: { } t } => t,
                { Flag: { } f } => f ? "true" : "false",
                { Vector: { } vec } => string.Join(";", vec.Select(x => x.ToString("R", CultureInfo.InvariantCulture))),
                _ => string.Empty,
            };

            Row(sb, spec.Symbol, value, v.Unit ?? string.Empty, v.Provenance?.Kind.ToString() ?? string.Empty, v.Provenance?.Reference ?? string.Empty, v.Provenance?.Revision ?? string.Empty);
        }

        return sb.ToString();
    }

    private static void ExportTable(StringBuilder sb, CalcInputSpec spec, CalcInputValue table)
    {
        Row(sb, spec.Symbol, string.Empty, string.Empty, table.Provenance?.Kind.ToString() ?? string.Empty, table.Provenance?.Reference ?? string.Empty, table.Provenance?.Revision ?? string.Empty);

        for (var r = 0; r < (table.Rows?.Count ?? 0); r++)
        {
            Row(sb, $"{spec.Symbol}[{r + 1}]", string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            var cells = table.Rows![r]?.Cells ?? new Dictionary<string, CalcCell>();
            var declared = (spec.Columns ?? []).Select(c => c.Key).ToList();
            var keys = declared.Where(cells.ContainsKey).Concat(cells.Keys.Except(declared, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal));

            foreach (var key in keys)
            {
                var cell = cells[key];

                // A cell holding nothing (an optional column left blank) is the same as no cell: it is not written. A cell whose value was
                // withheld (linked to a calculation that holds a licensed record's value) is written empty with its marker, so the importer knows.
                if (cell is null || (cell.Number is null && cell.Text is null && cell.Flag is null && !IsWithheld(cell.Provenance)))
                    continue;

                var value = cell switch
                {
                    { Number: { } n } => n.ToString("R", CultureInfo.InvariantCulture),
                    { Text: { } t } => t,
                    { Flag: { } f } => f ? "true" : "false",
                    _ => string.Empty,
                };
                Row(sb, $"{spec.Symbol}[{r + 1}].{key}", value, cell.Unit ?? string.Empty, cell.Provenance?.Kind.ToString() ?? string.Empty, cell.Provenance?.Reference ?? string.Empty, cell.Provenance?.Revision ?? string.Empty);
            }
        }
    }

    /// <summary>Writes outputs as <c>symbol,description,value,unit</c>.</summary>
    public static string ExportOutputs(CalcRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var sb = new StringBuilder();
        sb.AppendLine("symbol,description,value,unit");

        foreach (var o in result.Outputs)
        {
            var (value, unit) = o.Value switch
            {
                Engine.Values.ScalarValue s => (s.Displayed.ToString("G15", CultureInfo.InvariantCulture), s.DisplayUnit.Symbol),
                Engine.Values.VectorValue v => (string.Join(";", v.Displayed.Select(x => x.ToString("G15", CultureInfo.InvariantCulture))), v.DisplayUnit.Symbol),
                var other => (other.Format(), string.Empty),
            };

            Row(sb, o.Symbol, o.Description, value, unit);
        }

        return sb.ToString();
    }

    /// <summary>Reads an inputs file written by <see cref="ExportInputs"/>; unknown symbols become warnings, bad cells become blocks. Never throws for bad content.</summary>
    public static (CalcInputSet Inputs, IReadOnlyList<CalcNotice> Notices) ImportInputs(CalcTemplate template, string csv)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(csv);

        var notices = new List<CalcNotice>();
        var set = new CalcInputSet();

        if (Encoding.UTF8.GetByteCount(csv) > MaxBytes)
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"The file is larger than {MaxBytes / 1000} kB and was not read."));
            return (set, notices);
        }

        var rows = ParseRows(csv, notices);

        if (rows.Count == 0)
            return (set, notices);

        var header = rows[0];
        // First column of a name wins; empty names are ignored (R-01: a repeated or empty header never throws).
        var columns = new Dictionary<string, int>(StringComparer.Ordinal);

        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim().ToLowerInvariant();

            if (name.Length > 0)
                columns.TryAdd(name, i);
        }

        if (!columns.ContainsKey("symbol") || !columns.ContainsKey("value"))
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, "The first row must name at least the columns symbol and value."));
            return (set, notices);
        }

        string Cell(IReadOnlyList<string> row, string name) => columns.TryGetValue(name, out var i) && i < row.Count ? row[i].Trim() : string.Empty;

        var tables = new Dictionary<string, ImportedTable>(StringComparer.Ordinal);

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var symbol = Cell(row, "symbol");

            if (symbol.Length == 0)
                continue;

            // A table's rows and cells: name[1] marks row 1, name[1].column is one cell of it (Black B-A-05).
            if (TableRef.TryParse(symbol, out var tableRef) && template.FindInput(tableRef.Table) is { Kind: CalcInputKind.Table } tableSpec)
            {
                ReadTableRow(r + 1, tableRef, tableSpec, tables, Cell(row, "value"), Cell(row, "unit"), ReadProvenance(Cell(row, "provenance"), Cell(row, "reference"), Cell(row, "revision")), notices);
                continue;
            }

            var spec = template.FindInput(symbol);

            if (spec is null)
            {
                notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, $"Row {r + 1}: {symbol} is not an input of this calculation and was skipped."));
                continue;
            }

            var raw = Cell(row, "value");

            if (IsFormulaLike(raw))
            {
                notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {r + 1}: {symbol} looks like a spreadsheet formula and was refused."));
                continue;
            }

            var unit = Cell(row, "unit");
            var provenance = ReadProvenance(Cell(row, "provenance"), Cell(row, "reference"), Cell(row, "revision"));

            // A value withheld on export (a licensed record) is not in the file. It must not come back as the template default
            // (that would size on a value nobody entered), so the import of it is refused until the record is applied again.
            if (raw.Length == 0 && spec.Kind != CalcInputKind.Table && IsWithheld(provenance))
            {
                notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {r + 1}: " + WithheldMessage(symbol)));
                continue;
            }

            switch (spec.Kind)
            {
                case CalcInputKind.Quantity:
                case CalcInputKind.Number:
                    if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
                    {
                        notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {r + 1}: {symbol} must be a finite number, not '{Truncate(raw)}'."));
                        continue;
                    }

                    set.Set(new CalcInputValue(symbol, Number: n, Unit: unit.Length > 0 ? unit : (spec.Kind == CalcInputKind.Quantity ? spec.DefaultUnit : null), Provenance: provenance));
                    break;

                case CalcInputKind.Choice:
                case CalcInputKind.Text:
                    set.Set(new CalcInputValue(symbol, Text: raw, Provenance: provenance));
                    break;

                case CalcInputKind.Boolean:
                    if (raw.Length > 0 && ParseFlag(raw) is null)
                    {
                        notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {r + 1}: {symbol} must be yes or no (true or false), not '{Truncate(raw)}'."));
                        continue;
                    }

                    set.Set(new CalcInputValue(symbol, Flag: ParseFlag(raw) ?? false, Provenance: provenance));
                    break;

                case CalcInputKind.Vector:
                {
                    var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    var values = new List<double>(parts.Length);
                    var ok = true;

                    foreach (var p in parts)
                    {
                        if (!double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out var x) || !double.IsFinite(x))
                        {
                            ok = false;
                            break;
                        }

                        values.Add(x);
                    }

                    if (!ok)
                    {
                        notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {r + 1}: {symbol} must be numbers separated by ';'."));
                        continue;
                    }

                    set.Set(new CalcInputValue(symbol, Unit: unit.Length > 0 ? unit : spec.DefaultUnit, Vector: values, Provenance: provenance));
                    break;
                }

                case CalcInputKind.Table:
                    if (!tables.TryGetValue(symbol, out var own))
                        tables[symbol] = own = new ImportedTable(spec);

                    own.Provenance = provenance;
                    own.Seen = true;
                    break;
            }
        }

        foreach (var table in tables.Values)
            FinishTable(table, set, notices);

        return (set, notices);
    }

    /// <summary>A table's rows and cells as they are read, before the table is built.</summary>
    private sealed class ImportedTable(CalcInputSpec spec)
    {
        public CalcInputSpec Spec { get; } = spec;

        public CalcProvenance? Provenance { get; set; }

        public bool Seen { get; set; }

        public SortedDictionary<int, Dictionary<string, CalcCell>> Rows { get; } = [];
    }

    private readonly record struct TableRef(string Table, int Row, string? Column)
    {
        public static bool TryParse(string symbol, out TableRef reference)
        {
            reference = default;
            var open = symbol.IndexOf('[', StringComparison.Ordinal);
            var close = symbol.IndexOf(']', StringComparison.Ordinal);

            if (open <= 0 || close <= open + 1 || !int.TryParse(symbol[(open + 1)..close], NumberStyles.None, CultureInfo.InvariantCulture, out var row) || row < 1)
                return false;

            var rest = symbol[(close + 1)..];

            if (rest.Length == 0)
                reference = new TableRef(symbol[..open], row, null);
            else if (rest.Length > 1 && rest[0] == '.')
                reference = new TableRef(symbol[..open], row, rest[1..]);
            else
                return false;

            return true;
        }
    }

    private static void ReadTableRow(int line, TableRef reference, CalcInputSpec spec, Dictionary<string, ImportedTable> tables, string raw, string unit, CalcProvenance? provenance, List<CalcNotice> notices)
    {
        if (!tables.TryGetValue(reference.Table, out var table))
            tables[reference.Table] = table = new ImportedTable(spec);

        if (reference.Row > spec.MaxRows)
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: {reference.Table} holds at most {spec.MaxRows} rows; row {reference.Row} was refused."));
            return;
        }

        if (!table.Rows.TryGetValue(reference.Row, out var cells))
            table.Rows[reference.Row] = cells = new Dictionary<string, CalcCell>(StringComparer.Ordinal);

        if (reference.Column is not { } key)
            return;

        var column = spec.Columns?.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));

        if (column is null)
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, $"Row {line}: {reference.Table} has no column '{key}' and the cell was skipped."));
            return;
        }

        if (raw.Length == 0 && IsWithheld(provenance))
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: " + WithheldMessage($"{reference.Table}[{reference.Row}].{key}")));
            return;
        }

        if (raw.Length == 0)
            return;   // a blank cell is no cell; the sheet asks for it if its column needs a value

        if (IsFormulaLike(raw))
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: {reference.Table}[{reference.Row}].{key} looks like a spreadsheet formula and was refused."));
            return;
        }

        if (cells.ContainsKey(key))
        {
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: {reference.Table}[{reference.Row}].{key} appears twice; keep one."));
            return;
        }

        switch (column.Kind)
        {
            case CalcInputKind.Quantity:
            case CalcInputKind.Number:
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
                {
                    notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: {reference.Table}[{reference.Row}].{key} must be a finite number, not '{Truncate(raw)}'."));
                    return;
                }

                cells[key] = new CalcCell(n, unit.Length > 0 ? unit : null, Provenance: provenance);
                break;

            case CalcInputKind.Boolean:
                if (ParseFlag(raw) is not { } cellFlag)
                {
                    notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"Row {line}: {reference.Table}[{reference.Row}].{key} must be yes or no (true or false), not '{Truncate(raw)}'."));
                    return;
                }

                cells[key] = new CalcCell(Flag: cellFlag);
                break;

            default:
                cells[key] = new CalcCell(Text: raw);
                break;
        }
    }

    /// <summary>Builds the table from its rows. A gap in the row numbers is a block; a table written without rows because they were withheld stays withheld.</summary>
    private static void FinishTable(ImportedTable table, CalcInputSet set, List<CalcNotice> notices)
    {
        var symbol = table.Spec.Symbol;

        if (table.Rows.Count == 0)
        {
            if (!table.Seen)
                return;

            // The table stays withheld: it comes back as an empty value carrying the pin, which the validator blocks, so the
            // calculation does not run on the template's default rows.
            if (IsWithheld(table.Provenance))
            {
                set.Set(new CalcInputValue(symbol, Provenance: table.Provenance));
                notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, $"{symbol}: its rows were withheld on export (licensed record) and are not in the file; the calculation cannot run until you apply the record again or enter the rows from your own copy."));
                return;
            }

            set.Set(new CalcInputValue(symbol, Rows: [], Provenance: table.Provenance));
            return;
        }

        var expected = 1;

        foreach (var number in table.Rows.Keys)
        {
            if (number != expected)
            {
                notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"{symbol}: the rows must be numbered 1 to {table.Rows.Count} without gaps; row {expected} is missing."));
                return;
            }

            expected++;
        }

        set.Set(new CalcInputValue(symbol, Rows: [.. table.Rows.Values.Select(c => new CalcRow(c))], Provenance: table.Provenance));
    }

    private static CalcProvenance? ReadProvenance(string kind, string reference, string revision)
    {
        if (kind.Length == 0)
            return null;

        return Enum.TryParse<CalcProvenanceKind>(kind, ignoreCase: true, out var k)
            ? new CalcProvenance(k, reference.Length > 0 ? reference : null, revision.Length > 0 ? revision : null)
            : null;
    }

    /// <summary>
    /// A cell a spreadsheet would evaluate: anything whose first visible character is =, +, −, @, tab, carriage return or line feed (or
    /// their full-width forms) — except a plain signed number (R-03: "-1+cmd|…" is not a number). Leading spaces and no-break spaces are
    /// skipped, as a spreadsheet may skip them.
    /// </summary>
    public static bool IsFormulaLike(string s) =>
        CsvFormulaGuard.StartsLikeFormula(s)
        && !(s.TrimStart(' ').Length > 0 && s.TrimStart(' ')[0] is '-' or '+' && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));

    private static void Row(StringBuilder sb, params string[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (i > 0)
                sb.Append(',');

            sb.Append(Escape(cells[i]));
        }

        sb.AppendLine();
    }

    internal static string Escape(string cell)
    {
        var s = cell.Length > MaxFieldLength ? cell[..MaxFieldLength] : cell;

        if (IsFormulaLike(s))
            s = "'" + s;

        if (s.IndexOfAny([',', '"', '\n', '\r']) >= 0)
            return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";

        return s;
    }

    private static string Truncate(string s) => s.Length > 40 ? s[..40] + "…" : s;

    /// <summary>The block for a file of too many rows, and the rows that are within the cap: the row that went over it is not one of them.</summary>
    private static List<List<string>> OverTheRowCap(List<List<string>> rows, List<CalcNotice> notices)
    {
        rows.RemoveRange(MaxRows, rows.Count - MaxRows);
        notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"The file has more than {MaxRows} rows; only the first {MaxRows} were read."));
        return rows;
    }

    private static List<List<string>> ParseRows(string text, List<CalcNotice> notices)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else if (field.Length < MaxFieldLength)
                {
                    field.Append(c); // R-02: the field cap holds inside quotes too.
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;

                row.Add(field.ToString());
                field.Clear();

                if (row.Count > 1 || row[0].Length > 0)
                    rows.Add(row);

                row = [];

                if (rows.Count > MaxRows)
                    return OverTheRowCap(rows, notices);
            }
            else
            {
                if (field.Length >= MaxFieldLength)
                    continue;

                field.Append(c);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);

            // The last line has no line break after it, so the loop above never counted it (found by the C-7 soak): the cap is the same with or without one.
            if (rows.Count > MaxRows)
                return OverTheRowCap(rows, notices);
        }

        if (inQuotes)
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, "The file ends inside a quoted cell."));

        return rows;
    }
}

/// <summary>
/// The one definition of "a spreadsheet would run this text" for everything that writes CSV: the first visible character (after spaces and
/// no-break spaces) is =, +, −, @, tab, carriage return or line feed, or the full-width form of one of the four signs.
/// </summary>
public static class CsvFormulaGuard
{
    private static readonly char[] Starts = ['=', '+', '-', '@', '\t', '\r', '\n', '\uFF1D', '\uFF0B', '\uFF0D', '\uFF20'];

    public static bool StartsLikeFormula(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        foreach (var c in text)
        {
            if (c is ' ' or '\u00A0' or '\u3000')
                continue;

            return Array.IndexOf(Starts, c) >= 0;
        }

        return false;
    }
}
