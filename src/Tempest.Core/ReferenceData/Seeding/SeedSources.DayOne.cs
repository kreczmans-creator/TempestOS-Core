namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// The second acquisition: the sources the day-one reference libraries were
/// transcribed from (PO decision 2026-10-01, "Seed as much information into
/// those databases as possible from recognised internet sources").
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value traces to one named, retrievable document.</b> Each
/// factory below names the publishing organisation, the document's own
/// title and the exact address it was read from on
/// <see cref="DayOneRetrievedOn"/>. Where a record needed a value its
/// principal document does not publish, the supplementary document is named
/// in that value's own <see cref="ReferenceQuantityValue.Conditions"/> and in
/// the record's notes — never folded silently into the principal citation.
/// </para>
/// <para>
/// <b>The standing of each kind of source is stated, not implied.</b>
/// Stockholders and grade databases restate limits set by the standard they
/// cite (secondary); manufacturers publish their own product data
/// (authoritative for that product, not for the grade in general); AZoM
/// restates handbook data (secondary); the Copper Development Association
/// is an industry body publishing its own alloy data. None is the primary
/// standard, and the records say so.
/// </para>
/// </remarks>
public static partial class SeedSources
{
    /// <summary>The date every day-one source was retrieved on.</summary>
    public static readonly DateOnly DayOneRetrievedOn = new(2026, 10, 1);

    private const string SecondaryStanding =
        "A secondary restatement of limits set by the standard it cites, not the standard itself.";

    private const string ManufacturerStanding =
        "The manufacturer's own published product data: authoritative for that product, typical rather than guaranteed "
        + "unless the document marks a value as a minimum, and not interchangeable with another maker's figures.";

    /// <summary>Builds provenance for one day-one document.</summary>
    /// <param name="organisation">The publishing organisation.</param>
    /// <param name="document">The document's own title.</param>
    /// <param name="url">The exact address it was read from.</param>
    /// <param name="location">Which table or section the values were taken from.</param>
    /// <param name="standing">What kind of source this is, in one sentence.</param>
    /// <param name="revision">The document's own revision, where it states one.</param>
    /// <returns>Provenance naming that document.</returns>
    public static ReferenceProvenance DayOneDocument(
        string organisation,
        string document,
        string url,
        string location,
        string standing,
        string? revision = null) => new(
        SourceOrganisation: organisation,
        SourceDocument: document,
        SourceRevision: revision,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {DayOneRetrievedOn:yyyy-MM-dd} from {url}. {standing} Values were read out of the "
            + "published document by tooling and each was cross-read against the document text at transcription. "
            + "Not checked back against the primary standard by a person.");

    /// <summary>An Aalco Metals Limited technical datasheet, read at its own address.</summary>
    /// <param name="documentTitle">The datasheet's own title.</param>
    /// <param name="ashxName">The datasheet's file name under https://www.aalco.co.uk/datasheets/.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that datasheet.</returns>
    public static ReferenceProvenance AalcoDatasheet(string documentTitle, string ashxName, string location) =>
        DayOneDocument("Aalco Metals Limited", $"Aalco technical datasheet — {documentTitle}",
            $"https://www.aalco.co.uk/datasheets/{ashxName}.ashx", location,
            SecondaryStanding + " The datasheet carries no revision number or publication date.");

    /// <summary>A SteelNumber grade page restating the European standard the grade belongs to.</summary>
    /// <param name="grade">The grade as the page names it.</param>
    /// <param name="nameId">The page's own grade identifier.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that page.</returns>
    public static ReferenceProvenance SteelNumber(string grade, int nameId, string location) =>
        DayOneDocument("SteelNumber (European steel and alloy grades database)", $"SteelNumber grade page — {grade}",
            $"http://www.steelnumber.com/en/steel_composition_eu.php?name_id={nameId}", location, SecondaryStanding);

    /// <summary>A Siderticino SA steel datasheet, read at its own address.</summary>
    /// <param name="documentTitle">The page's own title.</param>
    /// <param name="slug">The page's slug under https://siderticino.it/en/steel-datasheets/.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that page.</returns>
    public static ReferenceProvenance SiderticinoPage(string documentTitle, string slug, string location) =>
        DayOneDocument("Siderticino SA", $"Siderticino steel datasheet — {documentTitle}",
            $"https://siderticino.it/en/steel-datasheets/{slug}/", location, SecondaryStanding);

    /// <summary>An Ovako Steel Navigator material data sheet.</summary>
    /// <param name="grade">The grade as the sheet names it.</param>
    /// <param name="slug">The sheet's slug under https://steelnavigator.ovako.com/steel-grades/.</param>
    /// <param name="revised">The "Last revised" stamp printed on the sheet.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance Ovako(string grade, string slug, string revised) =>
        DayOneDocument("Ovako AB", $"Ovako Steel Navigator material data sheet — {grade}",
            $"https://steelnavigator.ovako.com/steel-grades/{slug}/pdf", "Physical properties block", ManufacturerStanding,
            revised);

    /// <summary>A Swiss Steel Group (Deutsche Edelstahlwerke) technical data sheet.</summary>
    /// <param name="documentTitle">The sheet's own title.</param>
    /// <param name="url">The sheet's address.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <param name="revision">The revision stamp printed on the sheet.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance SwissSteel(string documentTitle, string url, string location, string? revision = null) =>
        DayOneDocument("Swiss Steel Group (Deutsche Edelstahlwerke)", $"Swiss Steel Group technical data sheet — {documentTitle}",
            url, location, ManufacturerStanding + " Mechanical limits are stated by the sheet as those of the EN standard it cites.",
            revision);

    /// <summary>A Saarstahl material specification sheet.</summary>
    /// <param name="documentTitle">The sheet's own title.</param>
    /// <param name="url">The sheet's address.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance Saarstahl(string documentTitle, string url, string location) =>
        DayOneDocument("Saarstahl AG", $"Saarstahl material specification sheet — {documentTitle}", url, location,
            ManufacturerStanding);

    /// <summary>An AZoM (AZO Materials) materials article.</summary>
    /// <param name="articleTitle">The article's own title.</param>
    /// <param name="articleId">The article's own identifier.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that article.</returns>
    public static ReferenceProvenance Azom(string articleTitle, int articleId, string location) =>
        DayOneDocument("AZoM (AZO Materials)", $"AZoM materials article — {articleTitle}",
            $"https://www.azom.com/article.aspx?ArticleID={articleId}", location,
            "A materials-information publisher restating handbook and producer data; secondary. Values marked typical "
            + "by the article are not specification minima.");

    /// <summary>A Kaiser Aluminum technical data sheet.</summary>
    /// <param name="documentTitle">The sheet's own title.</param>
    /// <param name="url">The sheet's address.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance Kaiser(string documentTitle, string url, string location) =>
        DayOneDocument("Kaiser Aluminum", $"Kaiser Aluminum technical data — {documentTitle}", url, location,
            ManufacturerStanding + " Every mechanical value on the sheet is labelled typical.");

    /// <summary>A Copper Development Association alloy page.</summary>
    /// <param name="alloy">The UNS alloy number.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that page.</returns>
    public static ReferenceProvenance CopperDevelopmentAssociation(string alloy, string location) =>
        DayOneDocument("Copper Development Association Inc.", $"CDA alloy data — {alloy}",
            $"https://alloys.copper.org/alloy/{alloy}", location,
            "The copper industry association's own published alloy data; typical values unless marked minimum. "
            + "Published in US customary units and recorded in those units.");

    /// <summary>A CASTFAST foundry material-properties sheet restating EN 1561/EN 1563.</summary>
    /// <param name="documentTitle">The sheet's own title.</param>
    /// <param name="url">The sheet's address.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance Castfast(string documentTitle, string url, string location) =>
        DayOneDocument("CASTFAST GmbH (foundry)", $"CASTFAST material properties — {documentTitle}", url, location,
            "A foundry's restatement of the cast-iron standard and its informative annex values; secondary.");

    /// <summary>An Ensinger semi-finished product page.</summary>
    /// <param name="product">The product's own name.</param>
    /// <param name="url">The page's address.</param>
    /// <returns>Provenance naming that page.</returns>
    public static ReferenceProvenance Ensinger(string product, string url) =>
        DayOneDocument("Ensinger GmbH", $"Ensinger stock shapes product data — {product}", url,
            "Technical details: mechanical and thermal properties tables", ManufacturerStanding
            + " Ensinger states these are typical values measured on test specimens, not specification limits.");

    /// <summary>A Röchling Industrial technical data sheet or product page.</summary>
    /// <param name="product">The product's own name.</param>
    /// <param name="url">The document's address.</param>
    /// <returns>Provenance naming that document.</returns>
    public static ReferenceProvenance Roechling(string product, string url) =>
        DayOneDocument("Röchling Industrial SE & Co. KG", $"Röchling technical data — {product}", url,
            "General, mechanical and thermal properties tables", ManufacturerStanding
            + " Röchling states these are guideline values to DIN EN 15860.");

    /// <summary>A Röhm GmbH PLEXIGLAS technical information sheet.</summary>
    /// <param name="documentTitle">The sheet's own title.</param>
    /// <param name="url">The sheet's address.</param>
    /// <returns>Provenance naming that sheet.</returns>
    public static ReferenceProvenance Plexiglas(string documentTitle, string url) =>
        DayOneDocument("Röhm GmbH (PLEXIGLAS)", $"PLEXIGLAS technical information — {documentTitle}", url,
            "Physical properties table", ManufacturerStanding);

    /// <summary>The Chemours Teflon PTFE properties handbook.</summary>
    /// <param name="url">The handbook's address.</param>
    /// <returns>Provenance naming the handbook.</returns>
    public static ReferenceProvenance ChemoursPtfe(string url) =>
        DayOneDocument("The Chemours Company", "Teflon PTFE fluoropolymer resin — Properties Handbook", url,
            "Typical properties table", ManufacturerStanding);

    /// <summary>A Special Metals Corporation technical bulletin.</summary>
    /// <param name="documentTitle">The bulletin's own title.</param>
    /// <param name="url">The bulletin's address.</param>
    /// <param name="location">Which tables the values were taken from.</param>
    /// <returns>Provenance naming that bulletin.</returns>
    public static ReferenceProvenance SpecialMetals(string documentTitle, string url, string location) =>
        DayOneDocument("Special Metals Corporation", $"Special Metals technical bulletin — {documentTitle}", url, location,
            ManufacturerStanding + " The bulletin states its room-temperature tensile ranges are composites for "
            + "various product sizes and not suitable for specification purposes.");

    /// <summary>The Würth Industrie technical handbook chapter restating ISO 898-1 and ISO 3506-1.</summary>
    /// <param name="chapter">The chapter's own title.</param>
    /// <param name="url">The chapter's address.</param>
    /// <param name="location">Which tables the values were taken from.</param>
    /// <returns>Provenance naming that chapter.</returns>
    public static ReferenceProvenance Wuerth(string chapter, string url, string location) =>
        DayOneDocument("Würth Industrie Service GmbH & Co. KG", $"Würth DINO technical handbook — {chapter}", url, location,
            "A fastener distributor's extract of the ISO standard's own tables; secondary.");

    /// <summary>An RHD Bearings product specification page, read at its own address.</summary>
    /// <param name="designation">The bearing designation.</param>
    /// <param name="series">The series folder the page sits under.</param>
    /// <returns>Provenance naming that page.</returns>
    public static ReferenceProvenance RhdBearingsPage(string designation, string series) =>
        DayOneDocument("RHD Bearings", $"RHD Bearings product specification — {designation} Deep Groove Ball Bearing",
            $"https://rhdbearings.com/specs/{series}-series/{designation}/",
            "Dimensions, load ratings, speed limits and weight",
            "Load ratings and speed limits are this manufacturer's own product data and are not interchangeable with "
            + "another manufacturer's figures for the same boundary dimensions.");
}
