namespace Tempest.Core.ReferenceData.Seeding;

/// <summary>
/// Every external document the seed datasets were transcribed from, named
/// once, in one place.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists as a type rather than as repeated literals.</b>
/// Provenance is the part of a reference record that has to be auditable:
/// somebody must be able to ask "which documents is this library actually
/// standing on?" and get one answer. Spreading organisation names,
/// document titles and retrieval dates across six datasets guarantees they
/// drift. Naming them here means a source can be corrected, re-dated or
/// retired in one edit, and means the governance record of what was
/// acquired is the code itself.
/// </para>
/// <para>
/// <b>Nothing here claims verification.</b> Every provenance this type
/// builds leaves
/// <see cref="ReferenceProvenance.VerificationStatus"/> at
/// <see cref="ReferenceVerificationStatus.NotVerified"/> and both reviewer
/// fields <see langword="null"/>, because no person has checked these
/// transcriptions back against their sources. That is not an oversight to
/// be tidied up later — it is the true state, and
/// <see cref="ReferenceValidationStates.DescribeProvenanceShortfall"/>
/// relies on it to keep every seeded record out of
/// <see cref="ReferenceValidationState.Released"/>.
/// </para>
/// <para>
/// <b>Extraction method is recorded honestly.</b> A machine-readable table
/// the publisher itself distributes is
/// <see cref="ReferenceExtractionMethod.StructuredImport"/>; a value read
/// out of a prose or HTML document by tooling is
/// <see cref="ReferenceExtractionMethod.AutomatedExtraction"/>, whose own
/// documentation says it is "inherently in need of checking". Neither is
/// <see cref="ReferenceExtractionMethod.ManualTranscription"/>, because no
/// person typed these in from the page.
/// </para>
/// </remarks>
public static class SeedSources
{
    /// <summary>The date every source below was retrieved on.</summary>
    /// <remarks>
    /// One date for the whole acquisition, because it was one acquisition.
    /// Recorded in <see cref="ReferenceProvenance.Notes"/> rather than in
    /// <see cref="ReferenceProvenance.SourceDate"/>, which means the
    /// publication date of the document and must not be overloaded with
    /// the day somebody happened to read it.
    /// </remarks>
    public static readonly DateOnly RetrievedOn = new(2026, 9, 7);

    /// <summary>
    /// The CODATA internationally recommended values of the fundamental
    /// physical constants, as NIST publishes them.
    /// </summary>
    /// <param name="location">Where in the table the value appears — the constant's own row label.</param>
    /// <returns>Provenance naming the 2022 adjustment.</returns>
    public static ReferenceProvenance NistCodata2022(string location) => new(
        SourceOrganisation: "National Institute of Standards and Technology (NIST)",
        SourceDocument: "CODATA Internationally Recommended Values of the Fundamental Physical Constants — Complete Listing (allascii.txt)",
        SourceRevision: "2022 CODATA adjustment",
        SourceDate: new DateOnly(2024, 5, 1),
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.StructuredImport,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd} from https://physics.nist.gov/cuu/Constants/Table/allascii.txt. "
            + "A work of the United States government, published without licensing restriction. "
            + "The publication date recorded is the month NIST released the 2022 adjustment tables; "
            + "the file itself carries no date field. Not checked back against the source by a person.");

    /// <summary>A technical datasheet published by Aalco Metals Limited, a UK metals stockholder.</summary>
    /// <param name="documentTitle">The datasheet's own title, exactly as printed on it.</param>
    /// <param name="location">Which table within the datasheet the values were taken from.</param>
    /// <returns>Provenance naming that datasheet.</returns>
    /// <remarks>
    /// A stockholder's datasheet is a secondary source: it restates limits
    /// the cited EN standard sets, and Aalco is not the body that set them.
    /// The record therefore cites the EN standard as the value's own
    /// <see cref="ReferenceValueOrigin"/> while naming Aalco as the
    /// document actually read, which is the honest description of what
    /// happened and leaves a reviewer able to go to the standard itself.
    /// </remarks>
    public static ReferenceProvenance Aalco(string documentTitle, string location) => new(
        SourceOrganisation: "Aalco Metals Limited",
        SourceDocument: $"Aalco technical datasheet — {documentTitle}",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd} from https://www.aalco.co.uk/datasheets/. "
            + "The datasheet carries no revision number or publication date, so both are left unrecorded "
            + "rather than guessed. A stockholder's restatement of limits set by the standards it cites, "
            + "not the standard itself. Not checked back against the source by a person.");

    /// <summary>A steel grade specification published by Siderticino SA.</summary>
    /// <param name="documentTitle">The page's own title, exactly as printed.</param>
    /// <param name="location">Which table the values were taken from.</param>
    /// <returns>Provenance naming that specification.</returns>
    public static ReferenceProvenance Siderticino(string documentTitle, string location) => new(
        SourceOrganisation: "Siderticino SA",
        SourceDocument: $"Siderticino steel datasheet — {documentTitle}",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd} from https://siderticino.it/en/steel-datasheets/. "
            + "A distributor's restatement of EN 10025-2:2019 limits, not the standard itself. "
            + "Not checked back against the source by a person.");

    /// <summary>A bearing specification published by RHD Bearings.</summary>
    /// <param name="documentTitle">The page's own title.</param>
    /// <param name="location">Which part of the specification the values were taken from.</param>
    /// <returns>Provenance naming that specification.</returns>
    /// <remarks>
    /// Load ratings and speed limits are the manufacturer's own product
    /// data and differ legitimately between manufacturers for the same ISO
    /// boundary dimensions, which is why the record names the manufacturer
    /// on the bearing itself rather than presenting the figures as generic.
    /// </remarks>
    public static ReferenceProvenance RhdBearings(string documentTitle, string location) => new(
        SourceOrganisation: "RHD Bearings",
        SourceDocument: $"RHD Bearings product specification — {documentTitle}",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd} from https://rhdbearings.com/specs/. "
            + "Load ratings and speed limits are this manufacturer's own product data and are not "
            + "interchangeable with another manufacturer's figures for the same boundary dimensions. "
            + "Not checked back against the source by a person.");

    /// <summary>
    /// A manufacturing service supplier's own published capability
    /// statement.
    /// </summary>
    /// <param name="documentTitle">The page's own title, exactly as printed.</param>
    /// <param name="location">Which part of the page the figures were taken from.</param>
    /// <returns>Provenance naming that capability statement.</returns>
    /// <remarks>
    /// A supplier's capability page describes what that supplier will
    /// undertake to do, which is a commercial commitment rather than a
    /// property of the process. Figures taken from one are worth having —
    /// they are real, current and attributable — but they generalise to
    /// nothing, and the records that carry them say so.
    /// </remarks>
    public static ReferenceProvenance Protolabs(string documentTitle, string location) => new(
        SourceOrganisation: "Proto Labs, Inc.",
        SourceDocument: $"Proto Labs service capability page — {documentTitle}",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd} from https://www.protolabs.com/services/. "
            + "A commercial capability statement, not a process specification: the figures describe what this "
            + "supplier offers and will change when its equipment or policy changes. Any price or lead time "
            + "quoted is current only as at the retrieval date. Not checked back against the source by a person.");

    /// <summary>
    /// A tertiary encyclopaedic reference, used only where the primary
    /// standard is paywalled and no manufacturer restatement was reachable.
    /// </summary>
    /// <param name="articleTitle">The article's own title.</param>
    /// <param name="location">The section the values were taken from.</param>
    /// <param name="citedStandard">The standard the article itself cites for these values.</param>
    /// <returns>Provenance naming the article and the standard behind it.</returns>
    /// <remarks>
    /// Recorded as what it is rather than dressed up as the standard. A
    /// record sourced this way is a placeholder for a proper citation, and
    /// its notes say so, so that replacing it is an obvious and findable
    /// piece of work rather than a discovery somebody makes years later.
    /// </remarks>
    public static ReferenceProvenance TertiaryReference(string articleTitle, string location, string citedStandard) => new(
        SourceOrganisation: "Wikimedia Foundation",
        SourceDocument: $"Wikipedia — {articleTitle}",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd}. TERTIARY SOURCE. The article cites {citedStandard}, "
            + "which is paywalled and was not read. This record is a placeholder for a citation of "
            + $"{citedStandard} itself and should be superseded once that document is held. "
            + "Not checked back against the source by a person.");

    /// <summary>
    /// Content Tempest Design Engineering wrote itself, as its own
    /// engineering or business artefact.
    /// </summary>
    /// <param name="artefact">The kind of artefact this is part of.</param>
    /// <param name="note">Why it was written and when it was adopted.</param>
    /// <returns>Provenance naming Tempest as the author.</returns>
    /// <remarks>
    /// The third of the three categories the population phase must keep
    /// distinguishable: source-backed, authored, and fictional test data.
    /// Authored content has a real author and a real owner, so it names
    /// them — but it is still unverified, because being the author of a
    /// rule is not the same as having reviewed it, and the reviewer fields
    /// stay empty until somebody does.
    /// </remarks>
    public static ReferenceProvenance TempestAuthored(string artefact, string note) => new(
        SourceOrganisation: "Tempest Design Engineering",
        SourceDocument: $"Tempest Design Engineering — {artefact}",
        SourceRevision: null,
        SourceDate: RetrievedOn,
        SourceLocation: null,
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: $"AUTHORED, not sourced. {note} Written by Tempest Design Engineering rather than taken from an "
            + "external document, and therefore carrying Tempest's own authority and nobody else's. Authoring "
            + "is not reviewing: this has not been checked by a second person.");

    /// <summary>
    /// Bibliographic facts about a published standard — its designation,
    /// title, issuing body and edition — as the issuing body's own public
    /// catalogue states them.
    /// </summary>
    /// <param name="body">The issuing organisation.</param>
    /// <param name="location">Where the catalogue entry was read.</param>
    /// <returns>Provenance for an index entry.</returns>
    /// <remarks>
    /// The Standards library indexes standards; it does not reproduce
    /// them. What is recorded here is bibliographic metadata — the same
    /// facts a library catalogue holds — and never a technical requirement
    /// from inside the document, which stays with its publisher.
    /// </remarks>
    public static ReferenceProvenance StandardsCatalogue(string body, string location) => new(
        SourceOrganisation: body,
        SourceDocument: $"{body} public standards catalogue entry",
        SourceRevision: null,
        SourceDate: null,
        SourceLocation: location,
        ExtractionMethod: ReferenceExtractionMethod.AutomatedExtraction,
        Notes: $"Retrieved {RetrievedOn:yyyy-MM-dd}. Bibliographic metadata only — designation, title, "
            + "issuing body and edition. No technical content from inside the standard is reproduced. "
            + "Not checked back against the source by a person.");
}
