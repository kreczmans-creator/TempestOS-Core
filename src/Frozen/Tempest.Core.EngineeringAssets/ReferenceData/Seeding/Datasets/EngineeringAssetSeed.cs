using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringAssets.Templates;
using Tempest.Core.Materials;

namespace Tempest.Core.ReferenceData.Seeding.Datasets;

/// <summary>
/// The design review and technical document half of the bracket asset
/// demonstration.
/// </summary>
/// <remarks>
/// WP 18.0C (D-028): split out of the live
/// src/Tempest.Core/ReferenceData/Seeding/Datasets/EngineeringAssetSeed.cs
/// the same day Templates/CalculationPacks/Verification stayed live;
/// DesignReviews and TechnicalDocumentation are frozen to
/// <c>src/Frozen/Tempest.Core.EngineeringAssets</c> and this class moved
/// with them. The live class still builds the template, calculation pack
/// and requirement this one references by reference/string only — this
/// file stands alone, as every file under <c>src/Frozen/</c> does, and is
/// not maintained against later changes to the live seed.
/// </remarks>
public sealed class ArchivedEngineeringAssetSeed
{
    /// <summary>The identity of the bracket design review pack.</summary>
    public const string DesignReviewRecordId = "drp-bracket-preliminary";

    /// <summary>The identity of the bracket material selection note.</summary>
    public const string TechnicalDocumentRecordId = "doc-bracket-material-selection";

    private const string CalculationPackReference = "TDE-CPK-001";

    private const string VerificationReference = "TDE-VER-001";

    private const string Subject = "Mounting bracket, machined from bar";

    private ArchivedEngineeringAssetSeed(
        IReferenceSeed<DesignReviewPack> designReviews,
        IReferenceSeed<TechnicalDocument> technicalDocuments)
    {
        DesignReviews = designReviews;
        TechnicalDocuments = technicalDocuments;
    }

    /// <summary>The design review dataset.</summary>
    public IReferenceSeed<DesignReviewPack> DesignReviews { get; }

    /// <summary>The technical document dataset.</summary>
    public IReferenceSeed<TechnicalDocument> TechnicalDocuments { get; }

    /// <summary>
    /// Builds the design review and technical document datasets against
    /// the material records currently in <paramref name="materials"/>,
    /// pinning each cited material at the revision it is actually at.
    /// </summary>
    /// <param name="materials">The material library the technical document cites. Must already be seeded.</param>
    /// <param name="requirement">
    /// The requirement the design review names. Pass <see langword="null"/>
    /// where no requirement has been written: the design review then
    /// names no verification or requirement reference.
    /// </param>
    /// <param name="cancellationToken">A token observed while reading.</param>
    /// <returns>The two datasets.</returns>
    /// <exception cref="ReferenceRecordNotFoundException">A record the assets cite is not registered.</exception>
    public static async Task<ArchivedEngineeringAssetSeed> CreateAsync(
        IReferenceDataCatalog<MaterialDefinition> materials,
        VerifiedRequirement? requirement = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(materials);

        var aluminium = await RequireAsync(materials, MaterialSeed.Aluminium6082T6, cancellationToken).ConfigureAwait(false);
        var steel = await RequireAsync(materials, MaterialSeed.S355J2, cancellationToken).ConfigureAwait(false);

        var aluminiumPin = ReferencePin.For(materials.LibraryName, aluminium);
        var steelPin = ReferencePin.For(materials.LibraryName, steel);

        return new ArchivedEngineeringAssetSeed(
            new Seed<DesignReviewPack>("Bracket preliminary design review", [BuildDesignReview(requirement)]),
            new Seed<TechnicalDocument>("Bracket material selection note", [BuildTechnicalDocument(aluminiumPin, steelPin)]));
    }

    private static async Task<IReferenceRecord<MaterialDefinition>> RequireAsync(
        IReferenceDataCatalog<MaterialDefinition> materials,
        string recordId,
        CancellationToken cancellationToken) =>
        await materials.FindAsync(recordId, cancellationToken).ConfigureAwait(false)
        ?? throw new ReferenceRecordNotFoundException(materials.LibraryName, recordId);

    private static ReferenceSeedRecord<DesignReviewPack> BuildDesignReview(VerifiedRequirement? requirement) =>
        new(DesignReviewRecordId,
            new DesignReviewPack
            {
                Reference = "TDE-DRP-001",
                Subject = Subject,
                Kind = DesignReviewKind.Preliminary,
                CalculationPackReferences = [CalculationPackReference],
                VerificationArtefactReferences = requirement is null ? [] : [VerificationReference],
                RequirementIds = requirement is null ? [] : [requirement.RequirementId],
                Outcome = ReviewOutcome.NotConcluded,
                OutcomeRationale = "The review has not been held. The pack exists to collect what would be "
                    + "reviewed, not to record a meeting that did not happen.",
                Observations =
                [
                    new ReviewObservation(
                        "OBS-1",
                        "The requirement states a margin must be positive but sets no design load, so the "
                        + "calculation cannot be completed and the verification has nothing to verify. This is "
                        + "the first thing the review would raise.",
                        ObservationSeverity.Major),
                    new ReviewObservation(
                        "OBS-2",
                        "The material record the calculation pins is unverified reference data in Draft state. "
                        + "Releasing a design on it would need that record checked against EN 755-2 first.",
                        ObservationSeverity.Minor),
                ],
                Notes = "AUTHORED by Tempest Design Engineering. No participants are listed because nobody "
                    + "attended; an empty participant list is the true record of a review that has not been "
                    + "held, and populating it with plausible names would be a fabricated meeting minute.",
            },
            SeedSources.TempestAuthored("Design review library",
                "Written to demonstrate a review pack assembling real linked assets before a review takes place."));

    private static ReferenceSeedRecord<TechnicalDocument> BuildTechnicalDocument(
        ReferencePin aluminiumPin,
        ReferencePin steelPin) =>
        new(TechnicalDocumentRecordId,
            new TechnicalDocument
            {
                Reference = "TDE-DOC-001",
                Title = "Mounting bracket — material selection note",
                Type = TechnicalDocumentType.DesignReport,
                Status = DocumentStatus.Draft,
                IssueRevision = "A",
                Notes = "AUTHORED by Tempest Design Engineering. Compares two candidate materials held in the "
                    + "reference library — 6082-T6 at 260 MPa minimum proof stress and 2.70 g/cm3, and S355J2 "
                    + "at 355 MPa and 7.85 g/cm3 — and records that the choice cannot be made until a "
                    + "requirement states whether mass or strength governs. Pinned to both material records at "
                    + "the revisions read, so the comparison can be reconstructed even after either record is "
                    + "revised. Pins: "
                    + $"{aluminiumPin.Library}/{aluminiumPin.RecordId}@r{aluminiumPin.RevisionNumber}, "
                    + $"{steelPin.Library}/{steelPin.RecordId}@r{steelPin.RevisionNumber}.",
            },
            SeedSources.TempestAuthored("Technical documentation library",
                "Written to demonstrate a technical document standing on pinned reference data."));

    private sealed class Seed<TDefinition>(string datasetName, IReadOnlyList<ReferenceSeedRecord<TDefinition>> records)
        : IReferenceSeed<TDefinition>
    {
        public string DatasetName { get; } = $"Tempest-authored engineering assets — {datasetName}";

        public int DatasetRevision => 1;

        public IReadOnlyList<ReferenceSeedRecord<TDefinition>> Records { get; } = records;
    }
}
