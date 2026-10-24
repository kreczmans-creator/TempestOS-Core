using System.Globalization;
using System.Text.Json.Serialization;

namespace Tempest.Calc.Sheets;

/// <summary>Where an input value came from. Mandatory to issue; an assumption is allowed and lands in the assumptions register.</summary>
public enum CalcProvenanceKind
{
    Manual,
    Drawing,
    Specification,
    Assumption,
    Evidence,
    Reference,

    /// <summary>Bound to an output of another calculation, pinned to that calculation's revision (Addendum A, L1).</summary>
    Link,
}

/// <summary>The provenance of one input value: its kind, a reference (drawing number, spec clause, evidence id, record id) and a revision.</summary>
public sealed record CalcProvenance(CalcProvenanceKind Kind, string? Reference = null, string? Revision = null)
{
    public static CalcProvenance Manual { get; } = new(CalcProvenanceKind.Manual);

    public static CalcProvenance Assumed(string? basis = null) => new(CalcProvenanceKind.Assumption, basis);

    public static CalcProvenance Drawing(string number, string? revision = null) => new(CalcProvenanceKind.Drawing, number, revision);

    public static CalcProvenance Specification(string reference, string? revision = null) => new(CalcProvenanceKind.Specification, reference, revision);

    public static CalcProvenance FromEvidence(string evidenceId) => new(CalcProvenanceKind.Evidence, evidenceId);

    public static CalcProvenance FromReference(string pin) => new(CalcProvenanceKind.Reference, pin);

    /// <summary>A binding to <paramref name="outputSymbol"/> of calculation <paramref name="upstreamId"/> at revision <paramref name="revision"/>.</summary>
    public static CalcProvenance Link(Guid upstreamId, string outputSymbol, string revision) =>
        new(CalcProvenanceKind.Link, $"{CalcLink.Prefix}{upstreamId:N}#{outputSymbol}", revision);

    public const int MaxReferenceLength = 200;
    public const int MaxRevisionLength = 40;

    /// <summary>Whether this provenance is enough to issue with: one of the defined kinds other than a bare manual entry (an undefined kind value is not).</summary>
    public bool IsSufficientToIssue => Kind != CalcProvenanceKind.Manual && Enum.IsDefined(Kind);

    /// <summary>Whether the reference and revision texts are within their caps.</summary>
    [JsonIgnore]
    public bool IsWithinLimits => (Reference?.Length ?? 0) <= MaxReferenceLength && (Revision?.Length ?? 0) <= MaxRevisionLength;

    public override string ToString()
    {
        var label = Kind switch
        {
            CalcProvenanceKind.Manual => "manual",
            CalcProvenanceKind.Drawing => "drawing",
            CalcProvenanceKind.Specification => "spec",
            CalcProvenanceKind.Assumption => "assumption",
            CalcProvenanceKind.Evidence => "evidence",
            CalcProvenanceKind.Reference => "reference",
            CalcProvenanceKind.Link => "link",
            _ => Kind.ToString().ToLowerInvariant(),
        };

        if (string.IsNullOrWhiteSpace(Reference))
            return label;

        return string.IsNullOrWhiteSpace(Revision) ? $"{label}: {Reference}" : $"{label}: {Reference} rev {Revision}";
    }
}

/// <summary>
/// One cell of a table-input row: a number (with unit), a text, or a flag. A number cell may carry a
/// <see cref="CalcProvenanceKind.Link"/> provenance, binding it to an output of another calculation (a cell link,
/// ADR-0168 amendment 2); a cell without provenance serialises exactly as before.
/// </summary>
public sealed record CalcCell(double? Number = null, string? Unit = null, string? Text = null, bool? Flag = null, CalcProvenance? Provenance = null);

/// <summary>One row of a table input, keyed by column.</summary>
public sealed record CalcRow(IReadOnlyDictionary<string, CalcCell> Cells)
{
    /// <summary>A row read without a cell dictionary (JSON <c>{}</c>) is an empty row, never a null one (R-11).</summary>
    public IReadOnlyDictionary<string, CalcCell> Cells { get; init; } = Cells ?? new Dictionary<string, CalcCell>(StringComparer.Ordinal);

    public static CalcRow Of(params (string Key, CalcCell Cell)[] cells) => new(cells.ToDictionary(c => c.Key, c => c.Cell, StringComparer.Ordinal));

    public CalcCell? Get(string key) => Cells.TryGetValue(key, out var c) ? c : null;
}

/// <summary>
/// One input as entered: a number with its unit, a text, a flag, a vector
/// or a table, and the provenance of the value. Numbers are kept as
/// entered (in <see cref="Unit"/>); the engine stores SI.
/// </summary>
public sealed record CalcInputValue(
    string Symbol,
    double? Number = null,
    string? Unit = null,
    string? Text = null,
    bool? Flag = null,
    IReadOnlyList<double>? Vector = null,
    IReadOnlyList<CalcRow>? Rows = null,
    CalcProvenance? Provenance = null)
{
    public static CalcInputValue Quantity(string symbol, double value, string unit, CalcProvenance? provenance = null) => new(symbol, Number: value, Unit: unit, Provenance: provenance);

    public static CalcInputValue Of(string symbol, double value, CalcProvenance? provenance = null) => new(symbol, Number: value, Provenance: provenance);

    public static CalcInputValue Choice(string symbol, string choice, CalcProvenance? provenance = null) => new(symbol, Text: choice, Provenance: provenance);

    public static CalcInputValue Switch(string symbol, bool flag, CalcProvenance? provenance = null) => new(symbol, Flag: flag, Provenance: provenance);

    public static CalcInputValue VectorOf(string symbol, IReadOnlyList<double> values, string unit, CalcProvenance? provenance = null) => new(symbol, Unit: unit, Vector: values, Provenance: provenance);

    public static CalcInputValue Table(string symbol, IReadOnlyList<CalcRow> rows, CalcProvenance? provenance = null) => new(symbol, Rows: rows, Provenance: provenance);

    /// <summary>The value as text for the inputs table.</summary>
    public string ValueText => this switch
    {
        { Number: { } n } => Engine.Values.CalcValue.FormatNumber(n, 6),
        { Text: { } t } => t,
        { Flag: { } f } => f ? "yes" : "no",
        { Vector: { } v } => $"[{string.Join(", ", v.Take(8).Select(x => Engine.Values.CalcValue.FormatNumber(x, 5)))}{(v.Count > 8 ? ", …" : string.Empty)}]",
        { Rows: { } r } => string.Create(CultureInfo.InvariantCulture, $"{r.Count} row(s)"),
        _ => "—",
    };
}

/// <summary>The full set of inputs for one run, by symbol.</summary>
public sealed class CalcInputSet
{
    private readonly Dictionary<string, CalcInputValue> _values = new(StringComparer.Ordinal);

    public CalcInputSet() { }

    public CalcInputSet(IEnumerable<CalcInputValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var v in values)
            Set(v);
    }

    public IReadOnlyCollection<CalcInputValue> Values => _values.Values;

    public CalcInputValue? Find(string symbol) => _values.GetValueOrDefault(symbol);

    public void Set(CalcInputValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        _values[value.Symbol] = value;
    }

    public CalcInputSet With(CalcInputValue value)
    {
        var copy = new CalcInputSet(_values.Values);
        copy.Set(value);
        return copy;
    }

    /// <summary>A copy with the given numeric input replaced by an SI value (used by Monte Carlo and tornado sweeps).</summary>
    public CalcInputSet WithSi(string symbol, double si)
    {
        var existing = Find(symbol) ?? throw new Engine.CalcEvaluationException($"'{symbol}' is not an input of this set.");
        var unit = Engine.Units.UnitCatalog.Parse(existing.Unit ?? string.Empty);
        return With(existing with { Number = unit.FromSi(si) });
    }
}
