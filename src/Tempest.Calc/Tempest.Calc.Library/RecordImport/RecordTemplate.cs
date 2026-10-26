using System.Text.RegularExpressions;

namespace Tempest.Calc.Library;

/// <summary>Whether an issue stops an import (an error) or only says something (a warning).</summary>
public enum RecordSeverity
{
    Error,
    Warning,
}

/// <summary>The checks a record template can ask for. Each names the columns (by their template symbol) it reads.</summary>
public enum RecordCheckKind
{
    /// <summary>The cell may not be blank.</summary>
    Required,

    /// <summary>The cell, when filled, is larger than zero.</summary>
    Positive,

    /// <summary>The cell, when filled, is zero or more.</summary>
    NonNegative,

    /// <summary>The cell, when filled, is at most <see cref="RecordCheck.Hi"/>.</summary>
    AtMost,

    /// <summary>The cell, when filled, is at least <see cref="RecordCheck.Lo"/>.</summary>
    AtLeast,

    /// <summary>The cell, when filled, is a whole number.</summary>
    Integer,

    /// <summary>The cell, when filled, is one of <see cref="RecordCheck.Allowed"/>.</summary>
    OneOf,

    /// <summary>The columns together make a key; no two rows share one.</summary>
    Unique,

    /// <summary>At least one of the columns is filled in each row.</summary>
    Either,

    /// <summary>Exactly one of the columns is filled in each row.</summary>
    ExactlyOne,

    /// <summary>Either every column is filled or none is, in each row.</summary>
    AllOrNone,

    /// <summary>If the first column is filled the second must be too.</summary>
    Requires,

    /// <summary>The first column is strictly below (or, if not strict, not above) <see cref="RecordCheck.Factor"/> times the second, in each row where both are filled.</summary>
    Less,

    /// <summary>The columns, left to right, rise (or, as <see cref="RecordCheck.NotStrict"/>, do not fall) across each row.</summary>
    RisingAcross,

    /// <summary>The columns, left to right, do not rise across each row.</summary>
    NotRisingAcross,

    /// <summary>Down the rows (within the group) the column rises strictly, as the rows are listed.</summary>
    OrderedRising,

    /// <summary>Within each group, with the rows put in order of the order column, the value column moves the way <see cref="RecordCheck.Direction"/> says.</summary>
    Monotonic,

    /// <summary>Within each group the bands [from, to] of the two columns do not overlap.</summary>
    NoOverlap,

    /// <summary>The cell, when filled, lies within <see cref="RecordCheck.Lo"/> and <see cref="RecordCheck.Hi"/> (either may be open); outside it is a warning.</summary>
    Range,

    /// <summary>The number of data rows.</summary>
    Rows,

    /// <summary>The cell of the first data row may not be blank (the later rows' cells of the column are not read).</summary>
    RequiredFirstRow,

    /// <summary>A text cell that contains <see cref="RecordCheck.Text"/> gets the warning <see cref="RecordCheck.Message"/>.</summary>
    TextWarning,
}

/// <summary>How a column moves down the rows of a monotonic check.</summary>
public enum RecordDirection
{
    /// <summary>Each value is larger than the one before.</summary>
    Rising,

    /// <summary>Each value is smaller than the one before.</summary>
    Falling,

    /// <summary>No value is smaller than the one before.</summary>
    NotFalling,

    /// <summary>No value is larger than the one before.</summary>
    NotRising,
}

/// <summary>One check a record template asks of its rows (the "Importer checks" of its notes).</summary>
public sealed record RecordCheck
{
    public RecordCheckKind Kind { get; init; }

    /// <summary>The columns the check reads, by template symbol.</summary>
    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>The columns that define a group (a device, a grade, a medium), for the checks that run within one.</summary>
    public IReadOnlyList<string> Group { get; init; } = [];

    public RecordSeverity Severity { get; init; } = RecordSeverity.Error;

    public RecordDirection Direction { get; init; }

    /// <summary>Lower bound, in the unit the template's header gives.</summary>
    public double? Lo { get; init; }

    /// <summary>Upper bound, in the unit the template's header gives.</summary>
    public double? Hi { get; init; }

    public double Factor { get; init; } = 1;

    public bool NotStrict { get; init; }

    public int? MinRows { get; init; }

    public int? MaxRows { get; init; }

    public IReadOnlyList<double> Allowed { get; init; } = [];

    public string? Text { get; init; }

    public string? Message { get; init; }
}

/// <summary>The builders the template catalogue reads like a list of rules.</summary>
internal static class Rule
{
    public static RecordCheck Req(params string[] columns) => new() { Kind = RecordCheckKind.Required, Columns = columns };

    public static RecordCheck ReqWarn(params string[] columns) => new() { Kind = RecordCheckKind.Required, Columns = columns, Severity = RecordSeverity.Warning };

    public static RecordCheck ReqFirst(params string[] columns) => new() { Kind = RecordCheckKind.RequiredFirstRow, Columns = columns };

    public static RecordCheck Pos(params string[] columns) => new() { Kind = RecordCheckKind.Positive, Columns = columns };

    public static RecordCheck NonNeg(params string[] columns) => new() { Kind = RecordCheckKind.NonNegative, Columns = columns };

    public static RecordCheck AtMost(double hi, params string[] columns) => new() { Kind = RecordCheckKind.AtMost, Columns = columns, Hi = hi };

    public static RecordCheck AtLeast(double lo, params string[] columns) => new() { Kind = RecordCheckKind.AtLeast, Columns = columns, Lo = lo };

    public static RecordCheck Int(params string[] columns) => new() { Kind = RecordCheckKind.Integer, Columns = columns };

    public static RecordCheck OneOf(string column, params double[] allowed) => new() { Kind = RecordCheckKind.OneOf, Columns = [column], Allowed = allowed };

    public static RecordCheck Uniq(params string[] columns) => new() { Kind = RecordCheckKind.Unique, Columns = columns };

    public static RecordCheck Either(params string[] columns) => new() { Kind = RecordCheckKind.Either, Columns = columns };

    public static RecordCheck One(params string[] columns) => new() { Kind = RecordCheckKind.ExactlyOne, Columns = columns };

    public static RecordCheck AllOrNone(params string[] columns) => new() { Kind = RecordCheckKind.AllOrNone, Columns = columns };

    public static RecordCheck Needs(string column, string requires) => new() { Kind = RecordCheckKind.Requires, Columns = [column, requires] };

    /// <summary><paramref name="low"/> is below <paramref name="high"/> (strictly unless <paramref name="orEqual"/>), after the factor is applied to <paramref name="high"/>.</summary>
    public static RecordCheck Lt(string low, string high, bool orEqual = false, double factor = 1, RecordSeverity severity = RecordSeverity.Error) =>
        new() { Kind = RecordCheckKind.Less, Columns = [low, high], NotStrict = orEqual, Factor = factor, Severity = severity };

    public static RecordCheck Rising(RecordSeverity severity, params string[] columns) => new() { Kind = RecordCheckKind.RisingAcross, Columns = columns, Severity = severity, NotStrict = true };

    public static RecordCheck NotRisingAcross(params string[] columns) => new() { Kind = RecordCheckKind.NotRisingAcross, Columns = columns };

    public static RecordCheck Ordered(string column, RecordSeverity severity = RecordSeverity.Error, params string[] group) =>
        new() { Kind = RecordCheckKind.OrderedRising, Columns = [column], Group = group, Severity = severity };

    public static RecordCheck Mono(string[] group, string order, string value, RecordDirection direction, RecordSeverity severity = RecordSeverity.Error) =>
        new() { Kind = RecordCheckKind.Monotonic, Group = group, Columns = [order, value], Direction = direction, Severity = severity };

    public static RecordCheck NoOverlap(string[] group, string from, string to) => new() { Kind = RecordCheckKind.NoOverlap, Group = group, Columns = [from, to] };

    public static RecordCheck Warn(string column, double? lo, double? hi) => new() { Kind = RecordCheckKind.Range, Columns = [column], Lo = lo, Hi = hi, Severity = RecordSeverity.Warning };

    public static RecordCheck Rows(int? min = null, int? max = null) => new() { Kind = RecordCheckKind.Rows, MinRows = min, MaxRows = max };

    public static RecordCheck TextWarn(string column, string contains, string message) =>
        new() { Kind = RecordCheckKind.TextWarning, Columns = [column], Text = contains, Message = message, Severity = RecordSeverity.Warning };
}

/// <summary>One column of a record template's header: its symbol, the unit its header states, and whether it holds text.</summary>
/// <param name="Symbol">The column symbol (<c>I_t</c>).</param>
/// <param name="Unit">The unit as the header writes it (<c>mm2</c>), empty for a dimensionless or text column.</param>
/// <param name="IsText">The header says <c>[text]</c>.</param>
public sealed record RecordTemplateColumn(string Symbol, string Unit, bool IsText)
{
    /// <summary>The header cell as the template writes it (<c>I_t[A]</c>, <c>n[-]</c>, <c>material[text]</c>).</summary>
    public string Header => $"{Symbol}[{(IsText ? "text" : Unit.Length == 0 ? "-" : Unit)}]";

    /// <summary>The unit in the engine's notation (<c>mm^2</c>); empty for a dimensionless or text column.</summary>
    public string EngineUnit => RecordUnits.Normalise(Unit);
}

/// <summary>How a record template stands against the calculators.</summary>
public enum RecordTemplateDisposition
{
    /// <summary>A built calculator declares the record kind: the template can be imported.</summary>
    Kind,

    /// <summary>The record kind is only proposed: no calculator declares it yet, so there is no record to save it as.</summary>
    Proposed,

    /// <summary>Superseded: no calculator reads this table (the notes say why); it is not imported.</summary>
    Superseded,
}

/// <summary>
/// One of Track P's record templates (<c>docs/verification/record-templates/NN-name.csv</c>) as the importer knows it: which record kind it
/// fills, its header, the checks its notes ask for, and what to do when no calculator declares the kind. The header here is checked against the
/// file by a test, so the two cannot drift.
/// </summary>
public sealed class RecordTemplate
{
    internal RecordTemplate(string id, string name, string title, string kind, RecordTemplateDisposition disposition, string header, string note, params RecordCheck[] checks)
    {
        Id = id;
        Name = name;
        Title = title;
        Kind = kind;
        Disposition = disposition;
        Header = header;
        Note = note;
        Checks = checks;
        Columns = ParseHeader(header);
    }

    /// <summary>The file number (<c>03</c>): a fixed id, not a rank.</summary>
    public string Id { get; }

    /// <summary>The name part of the file (<c>bs7671.max-zs</c>).</summary>
    public string Name { get; }

    /// <summary>What the template is, in words.</summary>
    public string Title { get; }

    /// <summary>The record kind it fills (the exact kind string of a calculator's <c>RecordKinds</c>), or, when <see cref="Disposition"/> is not <see cref="RecordTemplateDisposition.Kind"/>, the name the notes propose or an empty string.</summary>
    public string Kind { get; }

    public RecordTemplateDisposition Disposition { get; }

    /// <summary>The header row of the template file.</summary>
    public string Header { get; }

    /// <summary>One line on how the template stands (a proposed kind, a superseded file, a table the kind only partly holds).</summary>
    public string Note { get; }

    public IReadOnlyList<RecordCheck> Checks { get; }

    public IReadOnlyList<RecordTemplateColumn> Columns { get; }

    /// <summary>The file name without its extension (<c>03-bs7671.max-zs</c>).</summary>
    public string FileName => $"{Id}-{Name}";

    public RecordTemplateColumn? Column(string symbol) => Columns.FirstOrDefault(c => string.Equals(c.Symbol, symbol, StringComparison.Ordinal));

    /// <summary>The template's own words for a list: number, name and the kind it fills.</summary>
    public string Label => Disposition == RecordTemplateDisposition.Kind ? $"{Id} {Name} ({Kind})" : $"{Id} {Name}";

    /// <summary>Splits a header row (<c>I_t[A],CSA[mm2]</c>) into columns. A cell with no unit brackets is a dimensionless column.</summary>
    public static IReadOnlyList<RecordTemplateColumn> ParseHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        var list = new List<RecordTemplateColumn>();

        foreach (var cell in header.Split(','))
        {
            var (symbol, unit) = RecordUnits.SplitHeader(cell);
            var text = string.Equals(unit, "text", StringComparison.OrdinalIgnoreCase);
            list.Add(new RecordTemplateColumn(symbol, text || unit == "-" ? string.Empty : unit, text));
        }

        return list;
    }
}

/// <summary>The few unit-text rules of the record templates: header cells, ASCII powers, and the dimensionless dash.</summary>
public static partial class RecordUnits
{
    [GeneratedRegex(@"^\s*(?<symbol>[^\[\]]*?)\s*(?:\[(?<unit>[^\[\]]*)\])?\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex HeaderPattern();

    [GeneratedRegex(@"(?<=[A-Za-z\)µΩ°])(?<power>\d+)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 250)]
    private static partial Regex PowerPattern();

    /// <summary>
    /// Splits a header cell <c>symbol[unit]</c> into its symbol and unit text (the unit is empty when there are no brackets and
    /// <c>-</c> or <c>text</c> are kept as written, for the caller to read).
    /// </summary>
    public static (string Symbol, string Unit) SplitHeader(string cell)
    {
        ArgumentNullException.ThrowIfNull(cell);

        try
        {
            var m = HeaderPattern().Match(cell);
            return m.Success ? (m.Groups["symbol"].Value.Trim(), m.Groups["unit"].Success ? m.Groups["unit"].Value.Trim() : string.Empty) : (cell.Trim(), string.Empty);
        }
        catch (RegexMatchTimeoutException)
        {
            return (cell.Trim(), string.Empty);
        }
    }

    /// <summary>
    /// A unit as the templates write it, in the engine's notation: a digit straight after a unit symbol is a power (<c>mm2</c> is
    /// <c>mm^2</c>, <c>A2*s</c> is <c>A^2*s</c>, <c>J/(K*mm3)</c> is <c>J/(K*mm^3)</c>). A bare <c>-</c> is no unit. A leading digit (<c>1/K</c>) and
    /// a power already written with <c>^</c> are left alone.
    /// </summary>
    public static string Normalise(string? unit)
    {
        var text = unit?.Trim() ?? string.Empty;

        if (text is "-" or "text")
            return string.Empty;

        try
        {
            return PowerPattern().Replace(text, "^${power}");
        }
        catch (RegexMatchTimeoutException)
        {
            return text;
        }
    }
}
