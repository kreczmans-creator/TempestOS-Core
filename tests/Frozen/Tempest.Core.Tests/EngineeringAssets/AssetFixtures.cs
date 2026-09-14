using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringAssets.DesignReviews;
using Tempest.Core.EngineeringAssets.TechnicalDocumentation;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.EngineeringAssets;

/// <summary>
/// Shared construction for the archived half of the `P05` test suite:
/// DesignReviews, TechnicalDocumentation.
/// </summary>
/// <remarks>
/// WP 18.0C (D-028): split out of the live
/// tests/Tempest.Core.Tests/EngineeringAssets/AssetFixtures.cs the same
/// day Templates/CalculationPacks/Verification stayed live; every value
/// here is fictional, as it always was.
/// </remarks>
internal static class AssetFixtures
{
    /// <summary>A fixed date, so a record's own dating is asserted rather than tolerated.</summary>
    public static DateOnly Today { get; } = new(2026, 3, 1);

    /// <summary>A clock pinned to <see cref="Today"/>.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture engineering asset (not a real document)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data. Not an engineering record.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>Registers a record under a caller-chosen Id with verified provenance.</summary>
    public static Task<IReferenceRecord<TDefinition>> RegisterAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        TDefinition definition)
        where TDefinition : class =>
        catalog.RegisterAsync(recordId, definition, Verified());

    /// <summary>Walks a record through the full lifecycle to Released.</summary>
    public static async Task<IReferenceRecord<TDefinition>> ReleaseAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId)
        where TDefinition : class
    {
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Checked, "Checked.");
        await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Validated, "Rules pass.");
        return await catalog.SetValidationStateAsync(recordId, ReferenceValidationState.Released, "Released.");
    }

    /// <summary>Registers a record and walks it straight through to Released.</summary>
    public static async Task<IReferenceRecord<TDefinition>> RegisterReleasedAsync<TDefinition>(
        ReferenceDataCatalog<TDefinition> catalog,
        string recordId,
        TDefinition definition)
        where TDefinition : class
    {
        await RegisterAsync(catalog, recordId, definition);
        return await ReleaseAsync(catalog, recordId);
    }

    /// <summary>Governance facts naming an owner, an author and an independent reviewer.</summary>
    public static AssetGovernanceFacts Governance(
        string author = "engineer-1",
        string? reviewer = "reviewer-2",
        AssetReviewOutcome outcome = AssetReviewOutcome.Accepted,
        BusinessAuthorisation? approval = null) => new()
    {
        Ownership = new AssetOwnership("owner-1", "Chief Engineer"),
        Authorship = new AssetAuthorship(author, Today.AddDays(-14)),
        Reviews = reviewer is null ? [] : [new AssetReview(reviewer, outcome, Today.AddDays(-7), "Fixture review.")],
        Approval = approval,
        Classification = ConfidentialityClassification.Internal,
        Evidence = [new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, "Fixture evidence.", Reference: "FIX-E-1")],
    };

    /// <summary>An act of authority a fictional person exercised.</summary>
    public static BusinessAuthorisation Authority(
        BusinessAuthorityKind kind = BusinessAuthorityKind.InternalApproval,
        string principalId = "chief-1") =>
        new(kind, principalId, "Chief Engineer", Today, "Fixture basis, not a real authorisation.");

    // ---- E4 -----------------------------------------------------------

    public static Guid RequirementId { get; } = new("11111111-1111-1111-1111-111111111111");

    public static DesignReviewCatalog BuildDesignReviewCatalog() => Build((d, p) => new DesignReviewCatalog(d, p));

    public static DesignReviewPack Review(string reference = "DR-1", ReviewOutcome outcome = ReviewOutcome.ProceedWithActions) => new()
    {
        Reference = reference,
        Subject = "Fictional bracket critical design review.",
        Kind = DesignReviewKind.Critical,
        HeldOn = Today.AddDays(-5),
        Outcome = outcome,
        OutcomeRationale = "Fixture rationale.",
        Governance = Governance(),
        RequirementIds = [RequirementId],
        CalculationPackReferences = ["CALC-1"],
        Participants =
        [
            new ReviewParticipant("engineer-1", ReviewParticipantRole.Presenter),
            new ReviewParticipant("reviewer-2", ReviewParticipantRole.Reviewer),
            new ReviewParticipant("chief-1", ReviewParticipantRole.Chair),
        ],
        Observations =
        [
            new ReviewObservation("OBS-1", "Fixture observation.", ObservationSeverity.Minor, "reviewer-2", "Structure"),
        ],
        Actions =
        [
            new ReviewAction("ACT-1", "Fixture action.", "engineer-1", Today.AddDays(14), ObservationReferences: ["OBS-1"]),
        ],
    };

    // ---- E5 -----------------------------------------------------------

    public static TechnicalDocumentCatalog BuildDocumentCatalog() => Build((d, p) => new TechnicalDocumentCatalog(d, p));

    public static TechnicalDocument Document(
        string reference = "FIX-DWG-001",
        DocumentStatus status = DocumentStatus.Issued,
        string? issueRevision = "B") => new()
    {
        Reference = reference,
        Title = "Fictional bracket general arrangement",
        Type = TechnicalDocumentType.Drawing,
        Status = status,
        IssueRevision = issueRevision,
        DocumentId = Guid.NewGuid(),
        ProjectIdentifier = "FIX-PROJ",
        IssuedOn = status == DocumentStatus.Draft ? null : Today.AddDays(-20),
        Effectivity = new EffectivePeriod(Today.AddDays(-20), null),
        Governance = Governance(approval: Authority()),
    };

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
