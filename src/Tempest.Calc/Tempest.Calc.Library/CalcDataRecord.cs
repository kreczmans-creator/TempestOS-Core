using Tempest.Calc.Sheets;

namespace Tempest.Calc.Library;

/// <summary>Where a record's values come from (Addendum A, §A3).</summary>
public enum CalcDataOrigin
{
    /// <summary>Typed by the user from their own licensed copy of a standard or a manufacturer's datasheet.</summary>
    UserEntered,

    /// <summary>Computed from a formula (MCB trip multiples, adiabatic k from material constants, AWG geometry); labelled "derived".</summary>
    Derived,

    /// <summary>One attributed manufacturer example set, shipped where the manufacturer's terms allow.</summary>
    ManufacturerExample,
}

/// <summary>One value of a record: a number with its unit, a text or choice, or table rows, under the symbol of the calculator input it fills.</summary>
public sealed record CalcDataValue(string Symbol, double? Number = null, string? Unit = null, string? Text = null, IReadOnlyList<CalcRow>? Rows = null);

/// <summary>
/// A reference record (Addendum A, §A3): the tabulated values a calculation uses — ampacities, correction factors,
/// bearing X/Y/e factors, national-annex factor sets, derating curves — with where they came from: source title,
/// edition and amendment, table or clause, licence holder, who entered them, and whether the source is licensed. A
/// record is never edited: a change is a new revision, and a calculation pins the revision it used.
/// </summary>
/// <remarks>
/// A record flagged <see cref="LicensedSource"/> holds values from the user's own licensed copy (a BS 7671 table, a
/// CIBSE data sheet). Its values are excluded from every export path; an issued sheet may still show the value used and
/// cite the record, because that is the user's own calculation.
/// </remarks>
public sealed record CalcDataRecord(
    Guid Id,
    string Kind,
    string Title,
    int Revision,
    string SourceTitle,
    string Edition,
    string TableOrClause,
    string LicenceHolder,
    string EnteredBy,
    bool LicensedSource,
    CalcDataOrigin Origin,
    IReadOnlyList<CalcDataValue> Values,
    DateTimeOffset CreatedAt,
    string? Notes = null)
{
    public const string PinPrefix = "record:";
    public const int MaxValues = 200;
    public const int MaxRows = 500;
    public const int MaxTextLength = 200;

    /// <summary>The pin an input's provenance carries: <c>record:&lt;id&gt;@&lt;revision&gt;</c>.</summary>
    public string Pin => $"{PinPrefix}{Id:N}@{Revision}";

    /// <summary>How the sheet cites it: title, edition, table or clause, and "derived" or "licensed" where it applies.</summary>
    public string Citation =>
        $"{Title} — {SourceTitle}, {Edition}, {TableOrClause}{Origin switch { CalcDataOrigin.Derived => " (derived)", CalcDataOrigin.ManufacturerExample => " (manufacturer example)", _ => string.Empty }}{(LicensedSource ? " (licensed source)" : string.Empty)}";

    /// <summary>Why this record cannot be saved, or <see langword="null"/>.</summary>
    public string? Problem()
    {
        foreach (var (field, text) in new[] { ("kind", Kind), ("title", Title), ("source title", SourceTitle), ("edition and amendment", Edition), ("table or clause", TableOrClause), ("licence holder", LicenceHolder), ("entered by", EnteredBy) })
        {
            if (string.IsNullOrWhiteSpace(text))
                return $"A reference record needs its {field}.";

            if (text.Length > MaxTextLength)
                return $"The record's {field} is longer than {MaxTextLength} characters.";
        }

        if (Values.Count == 0 || Values.Count > MaxValues)
            return $"A reference record holds between 1 and {MaxValues} values.";

        if (Values.Any(v => v.Rows is { Count: > MaxRows }))
            return $"A table in a reference record holds at most {MaxRows} rows.";

        if (Values.Any(v => v.Number is { } n && !double.IsFinite(n)))
            return "A reference record's numbers must be finite.";

        return Values.Select(v => v.Symbol).Distinct(StringComparer.Ordinal).Count() == Values.Count ? null : "A reference record names a symbol twice.";
    }

    /// <summary>The record a provenance pins (id and revision), or <see langword="null"/>.</summary>
    public static (Guid Id, int Revision)? PinOf(CalcProvenance? provenance)
    {
        if (provenance is not { Kind: CalcProvenanceKind.Reference, Reference: { } reference } || !reference.StartsWith(PinPrefix, StringComparison.Ordinal))
            return null;

        var body = reference[PinPrefix.Length..].Split(' ', 2)[0];
        var at = body.IndexOf('@', StringComparison.Ordinal);
        return at > 0 && Guid.TryParse(body[..at], out var id) && int.TryParse(body[(at + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var revision)
            ? (id, revision)
            : null;
    }
}
