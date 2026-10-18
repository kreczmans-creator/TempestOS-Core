using Tempest.Core.Standards;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The standards the day-one material, fastener and bearing records cite
/// (PO decision 2026-10-01).
/// </summary>
/// <remarks>
/// The same rule as the first acquisition: an index entry exists only
/// because a seeded record cites the standard, its edition is the one the
/// citing source wrote (or none, where the source gave none), and no title
/// is recorded because none was read from the publisher's own catalogue.
/// </remarks>
public sealed partial class StandardSeed
{
    /// <summary>EN 10025-3, normalised fine-grain structural steels.</summary>
    public const string En10025Part3 = "std-en-10025-3";

    /// <summary>EN 10025-4, thermomechanical fine-grain structural steels.</summary>
    public const string En10025Part4 = "std-en-10025-4";

    /// <summary>EN 10025-6, quenched and tempered high-yield structural steels.</summary>
    public const string En10025Part6 = "std-en-10025-6";

    /// <summary>EN 10210-1, hot-finished structural hollow sections.</summary>
    public const string En10210Part1 = "std-en-10210-1";

    /// <summary>EN 1993-1-1, Eurocode 3 general rules.</summary>
    public const string En1993Part1Part1 = "std-en-1993-1-1";

    /// <summary>EN 10083-3, alloy steels for quenching and tempering.</summary>
    public const string En10083Part3 = "std-en-10083-3";

    /// <summary>EN 10277-2, bright steels for general engineering.</summary>
    public const string En10277Part2 = "std-en-10277-2";

    /// <summary>EN 10277-3, bright free-cutting steels.</summary>
    public const string En10277Part3 = "std-en-10277-3";

    /// <summary>EN 1561, grey cast irons.</summary>
    public const string En1561 = "std-en-1561";

    /// <summary>EN 1563, spheroidal graphite cast irons.</summary>
    public const string En1563 = "std-en-1563";

    /// <summary>EN 12164, copper alloy rod for free machining.</summary>
    public const string En12164 = "std-en-12164";

    /// <summary>ISO 898-1, mechanical properties of carbon and alloy steel fasteners.</summary>
    public const string Iso898Part1 = "std-iso-898-1";

    /// <summary>ISO 3506-1, mechanical properties of stainless steel fasteners.</summary>
    public const string Iso3506Part1 = "std-iso-3506-1";

    /// <summary>ASTM A276, stainless steel bars and shapes.</summary>
    public const string AstmA276 = "std-astm-a276";

    /// <summary>ASTM A564, precipitation-hardening stainless bars and shapes.</summary>
    public const string AstmA564 = "std-astm-a564";

    private static StandardsBody Astm =>
        new("ASTM", "ASTM International", StandardsBodyKind.International);

    private static IEnumerable<ReferenceSeedRecord<StandardDefinition>> DayOneCitations() =>
    [
        Cited(En10025Part3, Cen, "10025-3", "2004", StandardClassification.Specification,
            [StandardDiscipline.Materials, StandardDiscipline.Structural],
            "Cited by the SteelNumber page for S460N as the source of its thickness-banded limits."),
        Cited(En10025Part4, Cen, "10025-4", "2004", StandardClassification.Specification,
            [StandardDiscipline.Materials, StandardDiscipline.Structural],
            "Cited by the SteelNumber page for S460M as the source of its thickness-banded limits."),
        Cited(En10025Part6, Cen, "10025-6", "2004", StandardClassification.Specification,
            [StandardDiscipline.Materials, StandardDiscipline.Structural],
            "Cited by the SteelNumber page for S460Q as the source of its thickness-banded limits."),
        Cited(En10210Part1, Cen, "10210-1", "2006", StandardClassification.Specification,
            [StandardDiscipline.Materials, StandardDiscipline.Structural],
            "Cited by the SteelNumber page for S355J2H, hot-finished structural hollow sections."),
        Cited(En1993Part1Part1, Cen, "1993-1-1", null, StandardClassification.CodeOfPractice,
            [StandardDiscipline.Structural],
            "Cited by the Siderticino S355 datasheet as the source of the design values E = 210 GPa, G = 81 GPa and "
            + "alpha = 12e-6 /K the structural steel records carry. No edition was stated by the citing page."),
        Cited(En10083Part3, Cen, "10083-3", "2006", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the SteelNumber 42CrMo4 page and the Swiss Steel 34CrNiMo6 sheet as the source of the "
            + "quenched-and-tempered limits by ruling section."),
        Cited(En10277Part2, Cen, "10277-2", "2008", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the SteelNumber C45 page (bright steels for general engineering purposes)."),
        Cited(En10277Part3, Cen, "10277-3", "2008", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the SteelNumber 11SMn30 page (bright free-cutting steels)."),
        Cited(En1561, Cen, "1561", "1997", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the SteelNumber EN-GJL-250 page and the CASTFAST grey-iron sheet (grey cast irons)."),
        Cited(En1563, Cen, "1563", "2011", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the SteelNumber EN-GJS pages and the CASTFAST ductile-iron sheet (spheroidal graphite cast irons)."),
        Cited(En12164, Cen, "12164", "2011", StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Cited by the Aalco CW614N datasheet (copper alloy rod for free machining purposes)."),
        Cited(Iso898Part1, Iso, "898-1", null, StandardClassification.Specification,
            [StandardDiscipline.Mechanical, StandardDiscipline.Materials],
            "Cited by the Würth technical handbook as the source of the property-class table and test forces the "
            + "steel fastener records carry. The handbook names the DIN EN ISO adoption without an edition year."),
        Cited(Iso3506Part1, Iso, "3506-1", null, StandardClassification.Specification,
            [StandardDiscipline.Mechanical, StandardDiscipline.Materials],
            "Cited by the Würth technical handbook as the source of the A2/A4 strength-class table the stainless "
            + "fastener records carry."),
        Cited(AstmA276, Astm, "A276", null, StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Named by the AZoM grade 410, 431 and 440C articles as the specification their annealed (Condition A) "
            + "properties are typical of."),
        Cited(AstmA564, Astm, "A564", null, StandardClassification.Specification,
            [StandardDiscipline.Materials],
            "Named by the AZoM grade 630 (17-4 PH) article as the specification its condition minima are taken from."),
    ];

    private static ReferenceSeedRecord<StandardDefinition> Cited(
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
                PublicationStatus = StandardPublicationStatus.Unknown,
                ScopeSummary = scope,
                Notes = "Designation and edition as the citing day-one source wrote them (acquisition of 2026-10-01). No "
                    + "official title was read from the publisher's catalogue, so none is recorded; publication status "
                    + "is unknown because nothing read confirmed this edition is current.",
            },
            new ReferenceProvenance(
                SourceOrganisation: body.Name ?? body.Code,
                SourceDocument: $"{body.Name ?? body.Code} — citation of {body.Code} {designation} in a day-one source",
                SourceLocation: scope,
                ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
                Notes: $"Retrieved {SeedSources.DayOneRetrievedOn:yyyy-MM-dd}. Bibliographic metadata only — designation, "
                    + "issuing body and the edition the citing source named. No technical content from inside the standard "
                    + "is reproduced. Not checked back against the publisher's catalogue by a person."),
            new SourceCitation(body.Name ?? body.Code, $"{body.Name ?? body.Code} public standards catalogue entry",
                RowOrEntry: $"{body.Code} {designation}"));
}
