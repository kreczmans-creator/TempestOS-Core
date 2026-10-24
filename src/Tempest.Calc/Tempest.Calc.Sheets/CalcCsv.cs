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

    /// <summary>Writes inputs as <c>symbol,value,unit,provenance,reference,revision</c>.</summary>
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

        for (var r = 1; r < rows.Count; r++)
        {
            var row = rows[r];
            var symbol = Cell(row, "symbol");

            if (symbol.Length == 0)
                continue;

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
                    set.Set(new CalcInputValue(symbol, Flag: raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1" || raw.Equals("yes", StringComparison.OrdinalIgnoreCase), Provenance: provenance));
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
                    notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, $"Row {r + 1}: {symbol} is a table and is not imported from CSV."));
                    break;
            }
        }

        return (set, notices);
    }

    private static CalcProvenance? ReadProvenance(string kind, string reference, string revision)
    {
        if (kind.Length == 0)
            return null;

        return Enum.TryParse<CalcProvenanceKind>(kind, ignoreCase: true, out var k)
            ? new CalcProvenance(k, reference.Length > 0 ? reference : null, revision.Length > 0 ? revision : null)
            : null;
    }

    /// <summary>A cell a spreadsheet would evaluate: anything starting with =, +, −, @, tab or CR — except a plain signed number (R-03: "-1+cmd|…" is not a number).</summary>
    public static bool IsFormulaLike(string s) =>
        s.Length > 0
        && s[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
        && !(s[0] is '-' or '+' && double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _));

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
                {
                    notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Block, $"The file has more than {MaxRows} rows; only the first {MaxRows} were read."));
                    return rows;
                }
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
        }

        if (inQuotes)
            notices.Add(new CalcNotice("csv", CalcNoticeSeverity.Warning, "The file ends inside a quoted cell."));

        return rows;
    }
}
