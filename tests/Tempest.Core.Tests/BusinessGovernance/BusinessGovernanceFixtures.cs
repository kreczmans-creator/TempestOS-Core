using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

/// <summary>
/// Shared construction for the kept half of the `P07` test suite: Money,
/// EffectivePeriod, the governance/authority/evidence primitives, and
/// RateCard.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value here is fictional.</b> No real client, supplier, rate
/// or forecast appears in this suite. The fixture organisation is
/// "TestFixture Engineering", named so that nobody can mistake a fixture
/// for a business fact.
/// </para>
/// <para>
/// WP 18.0C (D-028): the Contract/Risk/Insurance/IP/Data/Finance/
/// Opportunity/Operating builders that used to live here moved to
/// tests/Frozen/Tempest.Core.Tests/BusinessGovernance/
/// BusinessGovernanceFixtures.cs alongside the namespaces they built
/// for. Governance() and Authority() stayed: RateCard's own Governance
/// property is typed BusinessGovernanceFacts, which is kept.
/// </para>
/// </remarks>
internal static class BusinessGovernanceFixtures
{
    /// <summary>A fixed instant, so a record's own timestamp is asserted rather than tolerated.</summary>
    public static DateOnly Today { get; } = new(2026, 3, 1);

    /// <summary>The fixture currency.</summary>
    public static CurrencyCode Gbp { get; } = CurrencyCode.Gbp;

    /// <summary>A clock pinned to <see cref="Today"/>.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>A sterling amount.</summary>
    public static Money Gbp_(decimal amount) => new(amount, Gbp);

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture business record (not a real document)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

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

    /// <summary>Ordinary governance facts: a named owner, a classification and a scheduled review.</summary>
    public static BusinessGovernanceFacts Governance(
        ConfidentialityClassification classification = ConfidentialityClassification.Internal,
        DateOnly? nextReview = null) => new()
    {
        Ownership = new BusinessOwnership("owner-1", "Managing Director"),
        Classification = classification,
        Review = new ReviewSchedule(nextReview ?? Today.AddMonths(6), IntervalMonths: 12),
        Evidence = [new BusinessEvidence(BusinessEvidenceKind.InternalRecord, "Fixture evidence.", Reference: "FIX-1")],
    };

    /// <summary>An act of authority a fictional person exercised.</summary>
    public static BusinessAuthorisation Authority(
        BusinessAuthorityKind kind,
        string principalId = "director-1",
        DateOnly? on = null) =>
        new(kind, principalId, "Director", on ?? Today, "Fixture basis, not a real authorisation.");

    // ---- C4 -------------------------------------------------------------

    public static RateCardCatalog BuildRateCardCatalog() => Build((d, p) => new RateCardCatalog(d, p));

    public static RateCard Card(string code = "RC-2026", DateOnly? from = null, DateOnly? to = null, bool approved = true) => new()
    {
        Code = code,
        Name = "Fixture rate card 2026",
        EffectivePeriod = new EffectivePeriod(from ?? Today.AddMonths(-2), to ?? Today.AddMonths(10)),
        Currency = Gbp,
        TaxTreatment = "Exclusive of VAT.",
        Governance = approved
            ? Governance() with { Authorisations = [Authority(BusinessAuthorityKind.InternalApproval)] }
            : Governance(),
        Entries =
        [
            new RateCardEntry("ENG-SEN", "Senior engineer", PricingBasis.Day, Gbp_(750m), MinimumCharge: Gbp_(400m)),
            new RateCardEntry("ENG-PRI", "Principal engineer", PricingBasis.Day, Gbp_(950m)),
            new RateCardEntry("ENG-HR", "Ad-hoc engineering support", PricingBasis.Hourly, Gbp_(110m), MinimumCharge: Gbp_(330m)),
        ],
    };

    // ---- construction ---------------------------------------------------

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
