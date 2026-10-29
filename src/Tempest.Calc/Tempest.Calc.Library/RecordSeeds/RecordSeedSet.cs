namespace Tempest.Calc.Library.RecordSeeds;

/// <summary>
/// How freely a shipped record set may be used, as far as is known (owner decision 2026-10-29, comment 5a: fill the records from reference websites
/// and highlight what is not lifted from a paid standard; the owner settles the licences later).
/// </summary>
public enum RecordSeedLicence
{
    /// <summary>Worked out from a formula or a physical constant, labelled "derived" with the formula. No licence is needed.</summary>
    Derived,

    /// <summary>From a public-domain or openly licensed dataset (NASA POWER, NOAA, UK Open Government Licence). Free to use with the credit given.</summary>
    OpenData,

    /// <summary>From a manufacturer's openly published catalogue or datasheet. Secondary source; the licence is still to confirm.</summary>
    ManufacturerToConfirm,

    /// <summary>From an openly published reference website or guide. Secondary source; the licence is still to confirm.</summary>
    SecondaryToConfirm,
}

/// <summary>One web page, datasheet or dataset a seed set's values were read from.</summary>
/// <param name="Title">The page or document title, with its publisher.</param>
/// <param name="Url">Where it was read.</param>
/// <param name="Read">The date it was read (ISO 8601).</param>
/// <param name="Licence">The licence status of this source.</param>
public sealed record RecordSeedSource(string Title, string Url, string Read, RecordSeedLicence Licence);

/// <summary>
/// One bundled record set: a CSV file in a Track P record template's format, the template it follows, and the provenance stamped on every record and
/// value it makes. It is imported through <see cref="RecordImportService"/>, so it passes every rule a person's own file passes (units, ranges, the
/// calculators' hard limits) or it is not shipped.
/// </summary>
/// <param name="Id">A short fixed name; the records carry it in their notes as <c>[seed-set:Id]</c>, which is how a second start-up knows they are in.</param>
/// <param name="TemplateId">The record template the CSV follows (its file number, <c>69</c>).</param>
/// <param name="Title">The record title (the importer appends each row's labels in brackets).</param>
/// <param name="Licence">The set's licence status: the most restrictive of its sources.</param>
/// <param name="Sources">Every source the values were read from or checked against, first the one they were taken from.</param>
/// <param name="TableOrClause">The table, page or clause the values come from, with the main URL.</param>
/// <param name="Edition">The edition or date of the source.</param>
/// <param name="ReproducesStandard">The paid standard whose table the source reproduces, when it says or plainly does so; <see langword="null"/> otherwise.</param>
/// <param name="Basis">How the values were taken: the cross-check, any disagreement and which value was kept, any derivation and its formula.</param>
public sealed record RecordSeedSet(
    string Id,
    string TemplateId,
    string Title,
    RecordSeedLicence Licence,
    IReadOnlyList<RecordSeedSource> Sources,
    string TableOrClause,
    string Edition,
    string? ReproducesStandard,
    string Basis)
{
    /// <summary>The date every bundled set was read from the web.</summary>
    public const string ReadDate = "2026-10-29";

    /// <summary>The marker every record of the set carries in its notes.</summary>
    public string Marker => $"[seed-set:{Id}]";

    /// <summary>The name of the bundled CSV resource.</summary>
    public string ResourceName => $"Tempest.Calc.Library.RecordSeeds.Data.{Id}.csv";

    /// <summary>
    /// Whether the set's values are locked against export until the owner confirms the licence: every set from a manufacturer or a secondary site, and
    /// every set that reproduces or derives from a paid standard, even when its own values are derived.
    /// </summary>
    public bool IsExportLocked => Licence is RecordSeedLicence.ManufacturerToConfirm or RecordSeedLicence.SecondaryToConfirm || ReproducesStandard is { Length: > 0 };

    /// <summary>The licence status in words, as the record's licence holder and the owner list print it.</summary>
    public string LicenceStatus => Licence switch
    {
        RecordSeedLicence.Derived => "Derived from a formula; no licence needed",
        RecordSeedLicence.OpenData => "Open data; free to use with credit",
        RecordSeedLicence.ManufacturerToConfirm => "Manufacturer data, secondary source — licence to confirm",
        _ => "Secondary source — licence to confirm",
    };

    /// <summary>How the record's values came to be.</summary>
    public CalcDataOrigin Origin => Licence switch
    {
        RecordSeedLicence.Derived => CalcDataOrigin.Derived,
        RecordSeedLicence.ManufacturerToConfirm => CalcDataOrigin.ManufacturerExample,
        _ => CalcDataOrigin.UserEntered,
    };

    /// <summary>The provenance the import stamps on every record and value of the set.</summary>
    public RecordProvenanceInput Provenance() => new(
        Title,
        Sources[0].Title,
        $"{Edition}; read {Sources[0].Read}",
        string.Empty,
        TableOrClause,
        LicenceStatus,
        LicensedSource: IsExportLocked,
        Origin: Origin,
        Notes: Notes());

    /// <summary>The record's notes: the marker, the licence status, the standard reproduced, every source with its URL and date, and how the values were taken.</summary>
    public string Notes()
    {
        var lines = new List<string>
        {
            $"{Marker} Shipped with TempestOS (owner decision 2026-10-29). {LicenceStatus}.",
        };

        if (ReproducesStandard is { Length: > 0 } standard)
            lines.Add($"Reproduces {standard}, via {Sources[0].Title}. The owner arranges a licence before relying on it.");

        lines.AddRange(Sources.Select(s => $"Source: {s.Title} — {s.Url} — read {s.Read} — {LicenceText(s.Licence)}."));
        lines.Add($"Basis: {Basis}");
        lines.Add("Check the values against your own source before you issue a design.");
        return string.Join("\n", lines);
    }

    /// <summary>A source's licence status in words.</summary>
    public static string LicenceText(RecordSeedLicence licence) => licence switch
    {
        RecordSeedLicence.Derived => "derived",
        RecordSeedLicence.OpenData => "open data",
        RecordSeedLicence.ManufacturerToConfirm => "manufacturer data, licence to confirm",
        _ => "secondary source, licence to confirm",
    };
}
