using System.Text.Json.Serialization;
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

    /// <summary>A generic textbook value shipped as a starting point and flagged "indicative": never a standard's or a catalogue's table (Addendum B, §B5).</summary>
    Indicative,
}

/// <summary>The licence class of one value (Addendum B, W3): freely shareable physics, or taken from a licensed source and so never exported.</summary>
public enum CalcLicenceClass
{
    [JsonStringEnumMemberName("public")]
    Public,

    [JsonStringEnumMemberName("licensed_source")]
    LicensedSource,
}

/// <summary>
/// Where one value of a record comes from: the source, its edition, the page or table, the licence class, who entered it and
/// when, and the SHA-256 (64 hex digits) of any evidence file attached to it. A value without a source cannot be saved in a
/// material record.
/// </summary>
public sealed record CalcValueSource(
    string Source,
    string Edition,
    string Reference,
    CalcLicenceClass Licence,
    string EnteredBy,
    DateTimeOffset? Date = null,
    string? EvidenceSha256 = null);

/// <summary>One value of a record: a number with its unit, a text or choice, or table rows, under the symbol of the calculator input it fills (a material record's values are under the property they give). <paramref name="Source"/> is the value's own provenance, where the record's is not fine enough.</summary>
public sealed record CalcDataValue(string Symbol, double? Number = null, string? Unit = null, string? Text = null, IReadOnlyList<CalcRow>? Rows = null, CalcValueSource? Source = null);

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
    public const string PinPrefix = CalcRecordPin.Prefix;
    public const int MaxValues = 200;
    public const int MaxRows = 500;
    public const int MaxTextLength = 200;
    public const int MaxCellsPerRow = 50;

    /// <summary>The pin an input's provenance carries: <c>record:&lt;id&gt;@&lt;revision&gt;</c>.</summary>
    public string Pin => $"{PinPrefix}{Id:N}@{Revision}";

    /// <summary>How the sheet cites it: title, edition, table or clause, and "derived" or "licensed" where it applies.</summary>
    public string Citation =>
        $"{Title} — {SourceTitle}, {Edition}, {TableOrClause}{Origin switch { CalcDataOrigin.Derived => " (derived)", CalcDataOrigin.ManufacturerExample => " (manufacturer example)", CalcDataOrigin.Indicative => " (indicative)", _ => string.Empty }}{(LicensedSource ? " (licensed source)" : string.Empty)}";

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

        if (Values.Any(v => v.Symbol is not { Length: > 0 and <= MaxTextLength } || v.Text?.Length > MaxTextLength || v.Unit?.Length > MaxTextLength))
            return $"A reference record's symbols, texts and units are at most {MaxTextLength} characters.";

        if (Values.Any(v => v.Rows?.Any(r => r is null || r.Cells.Count > MaxCellsPerRow || r.Cells.Values.Any(c => c is null || c.Text?.Length > MaxTextLength || c.Unit?.Length > MaxTextLength)) == true))
            return $"A row of a reference record holds at most {MaxCellsPerRow} cells of at most {MaxTextLength} characters.";

        if (Values.Any(v => v.Source is { } s && (s.Source?.Length > MaxTextLength || s.Edition?.Length > MaxTextLength || s.Reference?.Length > MaxTextLength || s.EnteredBy?.Length > MaxTextLength
                || (s.EvidenceSha256 is { } sha && !IsSha256(sha)))))
            return $"A value's source texts are at most {MaxTextLength} characters, and its evidence hash is 64 hexadecimal digits (SHA-256).";

        return Values.Select(v => v.Symbol).Distinct(StringComparer.Ordinal).Count() == Values.Count ? null : "A reference record names a symbol twice.";
    }

    /// <summary>Whether <paramref name="text"/> is a SHA-256 digest: 64 hexadecimal digits.</summary>
    public static bool IsSha256(string text) => text is { Length: 64 } && text.All(Uri.IsHexDigit);

    /// <summary>The record a provenance pins (id and revision), or <see langword="null"/>.</summary>
    public static (Guid Id, int Revision)? PinOf(CalcProvenance? provenance) => CalcRecordPin.From(provenance);
}
