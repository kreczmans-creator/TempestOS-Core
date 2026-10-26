using System.Globalization;
using Tempest.Calc.Engine.Units;

namespace Tempest.Calc.Library;

/// <summary>One cell of the file as read: the text, and the number or flag it holds when the column takes one.</summary>
internal sealed class ParsedCell
{
    public ParsedCell(string raw) => Raw = raw;

    public string Raw { get; }

    public double? Number { get; set; }

    public bool? Flag { get; set; }

    public bool IsBlank => Raw.Length == 0;
}

/// <summary>One data row of the file: its line, its cells by template column symbol, and what was found in it.</summary>
internal sealed class ParsedRow
{
    public ParsedRow(int line, IReadOnlyList<string> raw)
    {
        Line = line;
        Raw = raw;
    }

    public int Line { get; }

    public IReadOnlyList<string> Raw { get; }

    public Dictionary<string, ParsedCell> Cells { get; } = new(StringComparer.Ordinal);

    public List<RecordImportIssue> Issues { get; } = [];

    /// <summary>The row does not have the header's number of fields: nothing else is read from it.</summary>
    public bool Broken { get; set; }

    public ParsedCell? Cell(string symbol) => Cells.GetValueOrDefault(symbol);

    public void Add(RecordSeverity severity, string? column, string message) => Issues.Add(new RecordImportIssue(severity, Line, column, message));
}

/// <summary>A template column bound to the file column that fills it (if any), to the kind's symbol it lands on, and to the units on each side.</summary>
internal sealed class BoundColumn
{
    public BoundColumn(RecordTemplateColumn template) => Template = template;

    public RecordTemplateColumn Template { get; }

    public string Symbol => Template.Symbol;

    /// <summary>The position of the file column mapped to this template column, or -1 when none is.</summary>
    public int FileIndex { get; set; } = -1;

    public string FileUnitText { get; set; } = string.Empty;

    public bool FileSaysText { get; set; }

    public Unit? FileUnit { get; set; }

    public RecordColumnRole Role { get; set; }

    /// <summary>The scalar symbol, or the table column key, this column lands on.</summary>
    public string? Target { get; set; }

    public RecordSchemaSymbol? ScalarSymbol { get; set; }

    public RecordSchemaSymbol? Table { get; set; }

    public RecordSchemaColumn? TableColumn { get; set; }

    public MaterialPropertyDef? Material { get; set; }

    /// <summary>What the calculator takes in this column.</summary>
    public RecordColumnKind ExpectedKind { get; set; } = RecordColumnKind.Text;

    public string ExpectedUnitText { get; set; } = string.Empty;

    public Unit? ExpectedUnit { get; set; }

    public bool Mapped => FileIndex >= 0;

    /// <summary>Whether numbers may be read from the column, and are checked for range.</summary>
    public bool IsNumeric => ExpectedKind is RecordColumnKind.Number or RecordColumnKind.Quantity;

    public bool UnitIsUsable { get; set; } = true;

    /// <summary>Any unit is accepted (a coefficient whose unit follows another value), but one must be given.</summary>
    public bool AnyUnit { get; set; }

    /// <summary>Any unit of the expected dimension is simply a match (a material states its properties in the unit of its datasheet; a temperature is in degC, K or degF).</summary>
    public bool DimensionOnly { get; set; }

    public RecordUnitCheck UnitCheck { get; set; } = RecordUnitCheck.NotApplicable;

    public string UnitMessage { get; set; } = string.Empty;

    /// <summary>The value in coherent SI (or, for a dimensionless column, as typed), or <see langword="null"/> when the unit cannot be read.</summary>
    public double? ToSi(double value) => FileUnit is { } u && UnitIsUsable ? u.ToSi(value) : UnitIsUsable ? value : null;

    /// <summary>The value in <paramref name="unit"/> (the template's, or a calculator's), or <see langword="null"/> when the file's unit cannot be read.</summary>
    public double? In(double value, Unit? unit) => ToSi(value) is { } si ? (unit is null ? si : unit.FromSi(si)) : null;
}

/// <summary>What a column takes: text, a bare number, or a number with a unit. Only for the importer's own bookkeeping.</summary>
internal enum RecordColumnKind
{
    Text,
    Number,
    Quantity,
    Boolean,
    Choice,
}

/// <summary>Runs a template's own checks over the parsed rows. Every finding is an issue on a row (or on the file); nothing here throws.</summary>
internal static class RecordTemplateChecks
{
    public static void Run(RecordTemplate template, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns, List<RecordImportIssue> fileIssues)
    {
        var valid = rows.Where(r => !r.Broken).ToList();

        foreach (var check in template.Checks)
        {
            switch (check.Kind)
            {
                case RecordCheckKind.Required:
                    PerCell(check, valid, columns, (row, column, cell) =>
                    {
                        if (cell.IsBlank)
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} is blank; this template needs a value there.");
                    }, includeBlank: true);
                    break;

                case RecordCheckKind.RequiredFirstRow:
                    foreach (var name in check.Columns)
                    {
                        if (columns.GetValueOrDefault(name) is { Mapped: true } && valid.Count > 0 && (valid[0].Cell(name) is null || valid[0].Cell(name)!.IsBlank))
                            valid[0].Add(check.Severity, name, $"{name} is blank on the first data row, which is the row it is read from.");
                    }

                    break;

                case RecordCheckKind.Positive:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (!(v > 0))
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} must be larger than zero; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.NonNegative:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (v < 0)
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} cannot be negative; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.AtMost:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (v > check.Hi)
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} cannot be above {Show(check.Hi!.Value)}; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.AtLeast:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (v < check.Lo)
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} cannot be below {Show(check.Lo!.Value)}; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.Integer:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (v != Math.Floor(v))
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} must be a whole number; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.OneOf:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        if (!check.Allowed.Contains(v))
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} must be {string.Join(" or ", check.Allowed.Select(Show))}; the file has {Show(v)}.");
                    });
                    break;

                case RecordCheckKind.Range:
                    PerNumber(check, valid, columns, (row, column, v) =>
                    {
                        var shown = InTemplateUnit(column, v);

                        if (shown is { } s && ((check.Lo is { } lo && s < lo) || (check.Hi is { } hi && s > hi)))
                            row.Add(check.Severity, column.Symbol, $"{column.Symbol} = {Show(s)}{UnitSuffix(column)} is outside the usual range ({Range(check.Lo, check.Hi)}{UnitSuffix(column)}). Check it against your source.");
                    }, inTemplateUnit: true);
                    break;

                case RecordCheckKind.Unique:
                    Unique(check, valid, columns);
                    break;

                case RecordCheckKind.Either:
                    foreach (var row in valid)
                    {
                        if (check.Columns.Where(c => columns.GetValueOrDefault(c) is { Mapped: true }).All(c => row.Cell(c) is null || row.Cell(c)!.IsBlank))
                            row.Add(check.Severity, null, $"No value is filled in ({string.Join(", ", check.Columns)}); at least one is needed.");
                    }

                    break;

                case RecordCheckKind.ExactlyOne:
                    foreach (var row in valid)
                    {
                        var filled = check.Columns.Count(c => row.Cell(c) is { IsBlank: false });

                        if (filled != 1)
                            row.Add(check.Severity, null, $"Exactly one of {string.Join(" and ", check.Columns)} must be filled in, not {(filled == 0 ? "neither" : "both")}.");
                    }

                    break;

                case RecordCheckKind.AllOrNone:
                    foreach (var row in valid)
                    {
                        var filled = check.Columns.Count(c => row.Cell(c) is { IsBlank: false });

                        if (filled != 0 && filled != check.Columns.Count)
                            row.Add(check.Severity, null, $"{string.Join(" and ", check.Columns)} go together: fill in all of them or none.");
                    }

                    break;

                case RecordCheckKind.Requires:
                    foreach (var row in valid)
                    {
                        if (row.Cell(check.Columns[0]) is { IsBlank: false } && columns.GetValueOrDefault(check.Columns[1]) is { Mapped: true } && row.Cell(check.Columns[1]) is not { IsBlank: false })
                            row.Add(check.Severity, check.Columns[1], $"{check.Columns[1]} must be filled in when {check.Columns[0]} is.");
                    }

                    break;

                case RecordCheckKind.Less:
                    Less(check, valid, columns);
                    break;

                case RecordCheckKind.RisingAcross:
                case RecordCheckKind.NotRisingAcross:
                    Across(check, valid, columns);
                    break;

                case RecordCheckKind.OrderedRising:
                    Ordered(check, valid, columns);
                    break;

                case RecordCheckKind.Monotonic:
                    Monotonic(check, valid, columns);
                    break;

                case RecordCheckKind.NoOverlap:
                    NoOverlap(check, valid, columns);
                    break;

                case RecordCheckKind.Rows:
                    if (check.MinRows is { } min && valid.Count < min)
                        fileIssues.Add(new RecordImportIssue(check.Severity, null, null, min == check.MaxRows ? $"This template needs exactly {min} data rows; the file has {valid.Count}." : $"This template needs at least {min} data rows; the file has {valid.Count}."));
                    else if (check.MaxRows is { } max && valid.Count > max)
                        fileIssues.Add(new RecordImportIssue(check.Severity, null, null, $"This template takes at most {max} data row(s); the file has {valid.Count}."));
                    break;

                case RecordCheckKind.TextWarning:
                    foreach (var row in valid)
                    {
                        if (row.Cell(check.Columns[0]) is { IsBlank: false } cell && check.Text is { } needle && cell.Raw.Contains(needle, StringComparison.OrdinalIgnoreCase))
                            row.Add(check.Severity, check.Columns[0], check.Message ?? "Check this value.");
                    }

                    break;
            }
        }
    }

    // ------------------------------------------------------------------

    private static void PerCell(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns, Action<ParsedRow, BoundColumn, ParsedCell> action, bool includeBlank)
    {
        foreach (var name in check.Columns)
        {
            if (columns.GetValueOrDefault(name) is not { Mapped: true } column)
                continue;

            foreach (var row in rows)
            {
                if (row.Cell(name) is { } cell && (includeBlank || !cell.IsBlank))
                    action(row, column, cell);
            }
        }
    }

    private static void PerNumber(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns, Action<ParsedRow, BoundColumn, double> action, bool inTemplateUnit = false)
    {
        PerCell(check, rows, columns, (row, column, cell) =>
        {
            if (cell.Number is { } v && (!inTemplateUnit || column.UnitIsUsable))
                action(row, column, v);
        }, includeBlank: false);
    }

    private static double? InTemplateUnit(BoundColumn column, double v)
    {
        var target = column.Template.EngineUnit;

        if (target.Length == 0 || !UnitCatalog.TryParse(target, out var unit, out _))
            return column.UnitIsUsable ? v : null;

        return column.In(v, unit);
    }

    private static string UnitSuffix(BoundColumn column) => column.Template.Unit.Length == 0 ? string.Empty : " " + column.Template.EngineUnit;

    private static string Range(double? lo, double? hi) => (lo, hi) switch
    {
        ({ } a, { } b) => $"{Show(a)} to {Show(b)}",
        ({ } a, null) => $"at least {Show(a)}",
        (null, { } b) => $"at most {Show(b)}",
        _ => "any",
    };

    internal static string Show(double v) => v.ToString("G6", CultureInfo.InvariantCulture);

    private static string KeyOf(ParsedRow row, IReadOnlyList<string> names, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        var parts = new List<string>(names.Count);

        foreach (var name in names)
        {
            var cell = row.Cell(name);
            var column = columns.GetValueOrDefault(name);
            parts.Add(cell is null || cell.IsBlank ? string.Empty : cell.Number is { } n && column is { IsNumeric: true } ? (column.ToSi(n) ?? n).ToString("R", CultureInfo.InvariantCulture) : cell.Raw.ToUpperInvariant());
        }

        return string.Join("\u001F", parts);
    }

    private static void Unique(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        var names = check.Columns.Where(c => columns.GetValueOrDefault(c) is { Mapped: true }).ToList();

        if (names.Count == 0)
            return;

        var seen = new Dictionary<string, ParsedRow>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            var key = KeyOf(row, names, columns);

            if (key.Replace("\u001F", string.Empty, StringComparison.Ordinal).Length == 0)
                continue;

            if (seen.TryGetValue(key, out var first))
                row.Add(check.Severity, names[0], $"This row repeats line {first.Line} ({string.Join(", ", names)}); each combination may appear once.");
            else
                seen[key] = row;
        }
    }

    private static void Less(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        if (columns.GetValueOrDefault(check.Columns[0]) is not { Mapped: true } low || columns.GetValueOrDefault(check.Columns[1]) is not { Mapped: true } high)
            return;

        foreach (var row in rows)
        {
            if (row.Cell(low.Symbol)?.Number is not { } a || row.Cell(high.Symbol)?.Number is not { } b || low.ToSi(a) is not { } x || high.ToSi(b) is not { } y)
                continue;

            var limit = check.Factor * y;
            var ok = check.NotStrict ? x <= limit : x < limit;

            if (!ok)
                row.Add(check.Severity, low.Symbol, check.Factor != 1
                    ? $"{low.Symbol} ({Show(a)}) must be below {Show(check.Factor)} of {high.Symbol} ({Show(b)})."
                    : $"{low.Symbol} ({Show(a)}) must be {(check.NotStrict ? "no larger than" : "below")} {high.Symbol} ({Show(b)}).");
        }
    }

    private static void Across(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        var rising = check.Kind == RecordCheckKind.RisingAcross;

        foreach (var row in rows)
        {
            double? previous = null;
            string? previousName = null;

            foreach (var name in check.Columns)
            {
                if (columns.GetValueOrDefault(name) is not { Mapped: true } column || row.Cell(name)?.Number is not { } v || column.ToSi(v) is not { } si)
                    continue;

                if (previous is { } p && (rising ? si < p : si > p))
                {
                    row.Add(check.Severity, name, rising ? $"{name} is below {previousName}; the values are expected to rise from {check.Columns[0]} to {check.Columns[^1]}." : $"{name} is above {previousName}; the values may not rise from {check.Columns[0]} to {check.Columns[^1]}.");
                    break;
                }

                previous = si;
                previousName = name;
            }
        }
    }

    private static void Ordered(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        if (columns.GetValueOrDefault(check.Columns[0]) is not { Mapped: true } column)
            return;

        var last = new Dictionary<string, (double Value, int Line)>(StringComparer.Ordinal);
        var groupNames = check.Group.Where(g => columns.GetValueOrDefault(g) is { Mapped: true }).ToList();

        foreach (var row in rows)
        {
            if (row.Cell(column.Symbol)?.Number is not { } v || column.ToSi(v) is not { } si)
                continue;

            var group = KeyOf(row, groupNames, columns);

            if (last.TryGetValue(group, out var before) && si <= before.Value)
                row.Add(check.Severity, column.Symbol, $"{column.Symbol} must rise down the rows{(groupNames.Count > 0 ? " within one " + string.Join(" and ", groupNames) : string.Empty)}; line {before.Line} is not below this row.");

            last[group] = (si, row.Line);
        }
    }

    private static void Monotonic(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        if (columns.GetValueOrDefault(check.Columns[0]) is not { Mapped: true } order || columns.GetValueOrDefault(check.Columns[1]) is not { Mapped: true } value)
            return;

        var groupNames = check.Group.Where(g => columns.GetValueOrDefault(g) is { Mapped: true }).ToList();
        var groups = new Dictionary<string, List<(double Order, double Value, ParsedRow Row)>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.Cell(order.Symbol)?.Number is not { } o || order.ToSi(o) is not { } osi || row.Cell(value.Symbol)?.Number is not { } v || value.ToSi(v) is not { } vsi)
                continue;

            var key = KeyOf(row, groupNames, columns);

            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = [];

            list.Add((osi, vsi, row));
        }

        foreach (var list in groups.Values)
        {
            var sorted = list.OrderBy(x => x.Order).ToList();

            for (var i = 1; i < sorted.Count; i++)
            {
                var (po, pv, prow) = sorted[i - 1];
                var (co, cv, crow) = sorted[i];

                if (co == po)
                    continue;

                var ok = check.Direction switch
                {
                    RecordDirection.Rising => cv > pv,
                    RecordDirection.Falling => cv < pv,
                    RecordDirection.NotFalling => cv >= pv,
                    _ => cv <= pv,
                };

                if (!ok)
                {
                    var wanted = check.Direction switch
                    {
                        RecordDirection.Rising => "rise",
                        RecordDirection.Falling => "fall",
                        RecordDirection.NotFalling => "not fall",
                        _ => "not rise",
                    };
                    var where = groupNames.Count > 0 ? $" within {string.Join(", ", groupNames.Select(g => crow.Cell(g)?.Raw))}" : string.Empty;
                    crow.Add(check.Severity, value.Symbol, $"{value.Symbol} must {wanted} as {order.Symbol} rises{where}; line {prow.Line} and this row are the other way round.");
                }
            }
        }
    }

    private static void NoOverlap(RecordCheck check, IReadOnlyList<ParsedRow> rows, IReadOnlyDictionary<string, BoundColumn> columns)
    {
        if (columns.GetValueOrDefault(check.Columns[0]) is not { Mapped: true } from || columns.GetValueOrDefault(check.Columns[1]) is not { Mapped: true } to)
            return;

        var groupNames = check.Group.Where(g => columns.GetValueOrDefault(g) is { Mapped: true }).ToList();
        var groups = new Dictionary<string, List<(double From, double To, ParsedRow Row)>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            if (row.Cell(from.Symbol)?.Number is not { } a || from.ToSi(a) is not { } lo || row.Cell(to.Symbol)?.Number is not { } b || to.ToSi(b) is not { } hi)
                continue;

            var key = KeyOf(row, groupNames, columns);

            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = [];

            list.Add((lo, hi, row));
        }

        foreach (var list in groups.Values)
        {
            var sorted = list.OrderBy(x => x.From).ThenBy(x => x.To).ToList();

            for (var i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].From < sorted[i - 1].To)
                    sorted[i].Row.Add(check.Severity, from.Symbol, $"The band {from.Symbol} to {to.Symbol} overlaps the band on line {sorted[i - 1].Row.Line}.");
            }
        }
    }
}
