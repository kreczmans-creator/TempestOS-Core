using Tempest.Core.Standards;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// A governed index of the standards the rest of the seed corpus cites.
/// </summary>
/// <remarks>
/// <para>
/// <b>An index, not a copy.</b> No technical requirement from inside any
/// of these documents is reproduced here. What is recorded is
/// bibliographic: designation, issuing body, edition, and — where a source
/// actually stated it — the official title and what the document covers.
/// Standards are copyrighted works sold by their publishers, and this
/// library's purpose is to let a record say precisely which document it
/// stands on, not to substitute for holding that document.
/// </para>
/// <para>
/// <b>Every record here exists because something else cites it.</b> The
/// index was not assembled by browsing a catalogue; it is exactly the set
/// of standards named by the material datasheets, bearing specification
/// and thread tables the other datasets were transcribed from, plus the
/// dimensional and tolerance standards those documents reference in turn.
/// That keeps it honest about its own scope: it is the citation graph of
/// this seed corpus, not a claim to cover any field.
/// </para>
/// <para>
/// <b>Missing titles are left missing.</b> The ISO and CEN catalogues both
/// refused automated retrieval, so the official titles recorded here are
/// only those that a readable source actually stated. Where no title was
/// obtained, <see cref="StandardDefinition.Title"/> stays
/// <see langword="null"/> and the scope summary says what the citing
/// document said the standard covers — which is a different and weaker
/// claim, made visibly rather than papered over with a plausible-sounding
/// title.
/// </para>
/// </remarks>
public sealed class StandardSeed : IReferenceSeed<StandardDefinition>
{
    /// <summary>The identity of the ISO rolling-bearing boundary dimension standard.</summary>
    public const string Iso15 = "std-iso-15";

    /// <summary>The identity of the ISO metric screw thread basic profile standard.</summary>
    public const string Iso68Part1 = "std-iso-68-1";

    /// <summary>The identity of the ISO metric screw thread general plan.</summary>
    public const string Iso261 = "std-iso-261";

    /// <summary>The identity of the ISO metric screw thread selected sizes standard.</summary>
    public const string Iso262 = "std-iso-262";

    /// <summary>The identity of the ISO limits-and-fits basis standard.</summary>
    public const string Iso286Part1 = "std-iso-286-1";

    /// <summary>The identity of the ISO limits-and-fits tables standard.</summary>
    public const string Iso286Part2 = "std-iso-286-2";

    /// <summary>The identity of the ISO general tolerances standard.</summary>
    public const string Iso2768Part1 = "std-iso-2768-1";

    /// <summary>The identity of the European stainless steel semi-finished product standard.</summary>
    public const string En10088Part3 = "std-en-10088-3";

    /// <summary>The identity of the European hot-rolled structural steel standard.</summary>
    public const string En10025Part2 = "std-en-10025-2";

    /// <summary>The identity of the European wrought aluminium designation standard.</summary>
    public const string En573Part3 = "std-en-573-3";

    /// <summary>The identity of the European aluminium extruded product standard.</summary>
    public const string En755Part2 = "std-en-755-2";

    /// <summary>The identity of the European aluminium sheet, strip and plate standard.</summary>
    public const string En485Part2 = "std-en-485-2";

    /// <summary>The identity of the European copper sheet, plate and strip standard.</summary>
    public const string En1652 = "std-en-1652";

    /// <summary>The identity of the European copper rod, bar and section standard.</summary>
    public const string En13601 = "std-en-13601";

    private static readonly StandardsBody Iso =
        new("ISO", "International Organization for Standardization", StandardsBodyKind.International);

    private static readonly StandardsBody Cen =
        new("EN", "European Committee for Standardization (CEN)", StandardsBodyKind.Regional);

    /// <summary>The single instance of this dataset.</summary>
    public static StandardSeed Instance { get; } = new();

    private StandardSeed()
    {
    }

    /// <inheritdoc />
    public string DatasetName => "Citation index for the TempestOS seed corpus";

    /// <inheritdoc />
    public int DatasetRevision => 1;

    /// <inheritdoc />
    public IReadOnlyList<ReferenceSeedRecord<StandardDefinition>> Records { get; } =
    [
        // --- ISO, with titles obtained from a readable tertiary index ---
        Titled(Iso15, Iso, "15", "2017",
            "Rolling bearings — Radial bearings — Boundary dimensions, general plan",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Mechanical],
            "Fixes the bore, outside diameter and width series a radial rolling bearing designation implies, "
            + "which is why two manufacturers' 6205 bearings share dimensions but not load ratings."),

        Titled(Iso68Part1, Iso, "68-1", "1998",
            "ISO general purpose screw threads — Basic profile — Part 1: Metric screw threads",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Mechanical],
            "Defines the basic thread profile the metric thread dimensions are derived from."),

        Titled(Iso261, Iso, "261", "1998",
            "ISO general purpose metric screw threads — General plan",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Mechanical],
            "The general plan of metric thread diameter and pitch combinations."),

        Titled(Iso262, Iso, "262", "1998",
            "ISO general purpose metric screw threads — Selected sizes for screws, bolts and nuts",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Mechanical],
            "The selected diameter and coarse-pitch combinations the fastener seed records are stated against."),

        Titled(Iso286Part1, Iso, "286-1", "2010",
            "Part 1: Basis of tolerances, deviations and fits",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Metrology, StandardDiscipline.Manufacturing],
            "The ISO code system for limits and fits. Title recorded as the index stated it, which gives the "
            + "part title without the parent title."),

        Titled(Iso286Part2, Iso, "286-2", "2010",
            "Part 2: Tables of standard tolerance classes and limit deviations for holes and shafts",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Metrology, StandardDiscipline.Manufacturing],
            "The IT grade tables machining capability is ordinarily expressed against. Title recorded as the "
            + "index stated it, which gives the part title without the parent title."),

        Untitled(Iso2768Part1, Iso, "2768-1", "1989",
            StandardClassification.DimensionalStandard,
            [StandardDiscipline.Metrology, StandardDiscipline.Manufacturing],
            "General tolerances for linear and angular dimensions without individual tolerance indications. "
            + "Cited by the machining capability source as the tolerance class its unmarked dimensions are held "
            + "to. No title was obtained: the ISO catalogue refused automated retrieval and the tertiary index "
            + "read did not carry this entry."),

        // --- CEN, cited by the datasheets but with no title obtained ---
        Untitled(En10088Part3, Cen, "10088-3", "2005",
            StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco stainless datasheets as the source of the mechanical property limits for "
            + "1.4301 and 1.4404 bar and section up to 160 mm."),

        Untitled(En10025Part2, Cen, "10025-2", "2019",
            StandardClassification.Specification,
            [StandardDiscipline.Materials, StandardDiscipline.Structural],
            "Cited by the Siderticino datasheet as the source of the thickness-banded yield, tensile and "
            + "impact limits for non-alloy structural steels including S355J2."),

        Untitled(En573Part3, Cen, "573-3", null,
            StandardClassification.DesignationSystem,
            [StandardDiscipline.Materials],
            "The designation system the wrought aluminium grade names belong to. No edition is recorded on the "
            + "index record itself because the two Aalco datasheets read cite different editions of it — 2009 "
            + "for 6082 and 2019 for 5083 — and nothing read established which is current. Each citing record "
            + "carries the edition its own datasheet stated."),

        Untitled(En485Part2, Cen, "485-2", "2008",
            StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco aluminium datasheet as the source of the thickness-banded mechanical property "
            + "limits for 5083 sheet and plate."),

        Untitled(En755Part2, Cen, "755-2", "2008",
            StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco aluminium datasheet as the source of the size-banded mechanical property "
            + "minima for extruded 6082-T6."),

        Untitled(En1652, Cen, "1652", "1997",
            StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco copper datasheet as the source of the sheet and plate property limits for CW004A."),

        Untitled(En13601, Cen, "13601", null,
            StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco copper datasheet for rod, bar and section. The datasheet stated no edition and "
            + "gave no property values against it, so neither is recorded."),
    ];

    private static ReferenceSeedRecord<StandardDefinition> Titled(
        string recordId,
        StandardsBody body,
        string designation,
        string edition,
        string title,
        StandardClassification classification,
        IReadOnlyList<StandardDiscipline> disciplines,
        string scope) =>
        new(recordId,
            new StandardDefinition
            {
                Body = body,
                Designation = designation,
                Edition = edition,
                Title = title,
                Classification = classification,
                Disciplines = disciplines,
                PublicationStatus = StandardPublicationStatus.Current,
                ScopeSummary = scope,
                Language = "en",
                Notes = "Title and edition taken from a readable tertiary index of ISO standards, not from the "
                    + "ISO catalogue itself, which refused automated retrieval. No content from inside the "
                    + "standard is recorded.",
            },
            SeedSources.TertiaryReference(
                "List of ISO standards 1–1999",
                $"row for {body.Code} {designation}",
                $"{body.Code} {designation}"),
            new SourceCitation("Wikimedia Foundation", "Wikipedia — List of ISO standards 1–1999",
                RowOrEntry: $"{body.Code} {designation}"));

    private static ReferenceSeedRecord<StandardDefinition> Untitled(
        string recordId,
        StandardsBody body,
        string designation,
        string? edition,
        StandardClassification classification,
        IReadOnlyList<StandardDiscipline> disciplines,
        string scope) =>
        new(recordId,
            new StandardDefinition
            {
                Body = body,
                Designation = designation,
                Edition = edition,
                Title = null,
                Classification = classification,
                Disciplines = disciplines,

                // Not Current. Nothing read said this edition is the
                // current one; it is only the edition the citing datasheet
                // named, and an edition can be superseded without the
                // datasheet noticing.
                PublicationStatus = StandardPublicationStatus.Unknown,
                ScopeSummary = scope,
                Notes = "Designation and edition are as the citing datasheet wrote them. No official title was "
                    + "obtained — the CEN and ISO catalogues both refused automated retrieval — so the title is "
                    + "left unrecorded rather than reconstructed. Publication status is unknown because nothing "
                    + "read confirmed this edition is still current.",
            },
            SeedSources.StandardsCatalogue(
                body.Name ?? body.Code,
                $"citation of {body.Code} {designation} in the datasheet the citing record was transcribed from"),
            new SourceCitation(body.Name ?? body.Code, $"{body.Name ?? body.Code} public standards catalogue entry",
                RowOrEntry: $"{body.Code} {designation}"));
}
