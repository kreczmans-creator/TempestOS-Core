using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.BusinessOperations.Purchasing;
using Tempest.Core.BusinessOperations.Quality;
using Tempest.Core.BusinessOperations.Records;
using Tempest.Core.EngineeringAssets;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Knowledge.Lessons;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.EngineeringIntelligence;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.BusinessOperations;

/// <summary>
/// Shared construction for the `P04` test suite.
/// </summary>
/// <remarks>
/// <b>Every value here is fictional.</b> No real customer, supplier,
/// budget, order, non-conformance or business record appears anywhere in
/// this suite. The fixture customer is "Fictional Client Ltd", the
/// fixture supplier is "Notional Machining Ltd", registration numbers are
/// all zeroes, and no monetary figure describes any real transaction.
/// Fixtures live only in the test project, backed by in-memory stores
/// that die with the test.
/// </remarks>
internal static class OperationsFixtures
{
    /// <summary>A fixed date, so a record's own dating is asserted rather than tolerated.</summary>
    public static DateOnly Today { get; } = new(2026, 3, 1);

    /// <summary>The fixture currency.</summary>
    public static CurrencyCode Gbp { get; } = CurrencyCode.Gbp;

    /// <summary>A second currency, for the mismatch tests.</summary>
    public static CurrencyCode Eur { get; } = new("EUR");

    /// <summary>A clock pinned to <see cref="Today"/>.</summary>
    public static FakeTimeProvider Clock() => new(new DateTimeOffset(Today.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero));

    /// <summary>A sterling amount.</summary>
    public static Money Gbp_(decimal amount) => new(amount, Gbp);

    /// <summary>Provenance a named reviewer has verified.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture operational record (not a real document)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data. Not a business record.")
    {
        VerificationStatus = ReferenceVerificationStatus.VerifiedAgainstSource,
        ReviewerPrincipalId = "reviewer-1",
        VerificationDate = new DateOnly(2026, 2, 1),
    };

    /// <summary>Registers a record under a caller-chosen Id.</summary>
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

    /// <summary>Ordinary operational facts: owned, raised, dated.</summary>
    public static OperationalFacts Facts(
        OperationalState state = OperationalState.Open,
        DateOnly? dueBy = null,
        DateOnly? closedOn = null) => new()
    {
        State = state,
        OwnerPrincipalId = "owner-1",
        RaisedByPrincipalId = "engineer-1",
        RaisedOn = Today.AddDays(-14),
        DueBy = dueBy ?? Today.AddDays(14),
        ClosedOn = closedOn ?? (state == OperationalState.Closed ? Today : null),
    };

    /// <summary>An act of authority a fictional person exercised.</summary>
    public static BusinessAuthorisation Authority(
        BusinessAuthorityKind kind = BusinessAuthorityKind.ExpenditureAuthorisation,
        string principalId = "director-1") =>
        new(kind, principalId, "Director", Today, "Fixture basis, not a real authorisation.");

    // ---- WP04.1 --------------------------------------------------------

    public static OrganisationCatalog BuildOrganisationCatalog() => Build((d, p) => new OrganisationCatalog(d, p));

    public static ContactCatalog BuildContactCatalog() => Build((d, p) => new ContactCatalog(d, p));

    public static InteractionCatalog BuildInteractionCatalog() => Build((d, p) => new InteractionCatalog(d, p));

    public static Organisation Organisation(
        string reference = "ORG-1",
        RelationshipStatus status = RelationshipStatus.Active) => new()
    {
        Reference = reference,
        Name = "Fictional Client Ltd",
        Roles = [PartyKind.Customer],
        Status = status,
        RegistrationNumber = "00000000",
        RegistrationCountry = "GB",
        Address = new PostalAddress("1 Fixture Way", "Nowhere", "FX1 1FX", "GB"),
        TradingCurrency = Gbp,
        Origin = LeadOrigin.Referral,
        Facts = Facts(),
    };

    public static Contact Contact(string reference = "CON-1", string organisation = "ORG-1", bool primary = true) => new()
    {
        Reference = reference,
        OrganisationReference = organisation,
        Name = "A. Fictional",
        JobTitle = "Notional Engineering Manager",
        EmailAddress = "nobody@example.invalid",
        IsPrimaryContact = primary,
        Facts = Facts(),
    };

    public static Interaction Interaction(string reference = "INT-1", string organisation = "ORG-1") => new()
    {
        Reference = reference,
        OrganisationReference = organisation,
        Summary = "Fixture call about a fixture enquiry.",
        OccurredOn = Today.AddDays(-3),
        Channel = InteractionChannel.Telephone,
        ContactReferences = ["CON-1"],
        OwnPrincipalId = "engineer-1",
    };

    // ---- WP04.3 --------------------------------------------------------

    public static BudgetCatalog BuildBudgetCatalog() => Build((d, p) => new BudgetCatalog(d, p));

    public static FinancialEntryCatalog BuildEntryCatalog() => Build((d, p) => new FinancialEntryCatalog(d, p));

    public static Budget Budget(string reference = "BUD-1", decimal amount = 10_000m) => new()
    {
        Reference = reference,
        Name = "Fictional project budget",
        Currency = Gbp,
        Lines = [new BudgetLine("L1", "Fixture bought-in parts", Gbp_(amount), CashDirection.Outgoing, "Materials")],
        Period = new EffectivePeriod(Today.AddMonths(-1), Today.AddMonths(11)),
        SetUnderAuthority = Authority(),
        Facts = Facts(),
    };

    public static FinancialEntry Entry(
        string reference = "FE-1",
        decimal amount = 2_500m,
        FinancialPosture posture = FinancialPosture.Committed,
        string? budget = "BUD-1",
        CurrencyCode? currency = null) => new()
    {
        Reference = reference,
        Description = "Fixture commitment.",
        Amount = new Money(amount, currency ?? Gbp),
        Posture = posture,
        Direction = CashDirection.Outgoing,
        BudgetReference = budget,
        BudgetLineReference = budget is null ? null : "L1",
        OccurredOn = Today.AddDays(-7),
        Facts = Facts(),
    };

    // ---- WP04.4 --------------------------------------------------------

    public static PurchaseRequisitionCatalog BuildRequisitionCatalog() => Build((d, p) => new PurchaseRequisitionCatalog(d, p));

    public static PurchaseOrderCatalog BuildPurchaseOrderCatalog() => Build((d, p) => new PurchaseOrderCatalog(d, p));

    public static PurchaseRequisition Requisition(
        string reference = "REQ-1",
        RequisitionState state = RequisitionState.Requested,
        bool sourced = true) => new()
    {
        Reference = reference,
        Requirement = "Ten fixture brackets, turned.",
        State = state,
        Justification = "Fixture justification.",
        EstimatedValue = Gbp_(125m),
        BudgetReference = "BUD-1",
        SourcingComparisonPin = sourced
            ? new ReferencePin("CommercialSourcingComparisons", "cmp-1", 1)
            : null,
        Facts = Facts(),
    };

    public static PurchaseOrder PurchaseOrder(
        string reference = "PO-1",
        PurchaseOrderState state = PurchaseOrderState.Placed,
        bool authorised = true,
        decimal received = 0m) => new()
    {
        Reference = reference,
        Supplier = PartyReference.Supplier("Notional Machining Ltd", "sup-1"),
        Subject = "Ten fixture brackets.",
        Currency = Gbp,
        State = state,
        Lines =
        [
            new PurchaseOrderLine("1", "Turned bracket", 10m, Gbp_(12.50m), received, Today.AddDays(21)),
        ],
        RequisitionReference = "REQ-1",
        PlacedUnderAuthority = authorised ? Authority() : null,
        PlacedOn = state == PurchaseOrderState.Draft ? null : Today.AddDays(-7),
        BudgetReference = "BUD-1",
        Facts = Facts(),
    };

    // ---- WP04.5 --------------------------------------------------------

    public static NonConformanceCatalog BuildNonConformanceCatalog() => Build((d, p) => new NonConformanceCatalog(d, p));

    public static NonConformance NonConformance(
        string reference = "NCR-1",
        NonConformanceSeverity severity = NonConformanceSeverity.Minor,
        bool resolved = true,
        OperationalState state = OperationalState.Open) => new()
    {
        Reference = reference,
        Description = "Fixture bracket bore is undersize.",
        RequirementNotMet = "Drawing FIX-DWG-001 calls 25.0 +0.05/-0.00.",
        Kind = NonConformanceKind.Product,
        Severity = severity,
        AffectedItem = "Fixture bracket, batch FIX-B-1",
        AffectedQuantity = 3,
        DetectedBy = "Fixture goods-in inspection",
        Causes =
        [
            new FailureCause(
                "C-1",
                "The fixture reamer was worn and nobody checked it.",
                IsRootCause: true,
                Confidence: CauseConfidence.Probable,
                Evidence: [new EngineeringEvidence(EngineeringEvidenceKind.InspectionRecord, "Fixture inspection note.", Reference: "FIX-I-1")]),
        ],
        Disposition = new Disposition(DispositionKind.Rework, "engineer-1", Today.AddDays(-2)),
        Actions = resolved
            ?
            [
                new QualityAction(
                    "QA-1",
                    "Add reamer wear check to the fixture setup sheet.",
                    QualityActionKind.Corrective,
                    ["C-1"],
                    Facts(OperationalState.Closed),
                    [new EngineeringEvidence(EngineeringEvidenceKind.InternalRecord, "Updated setup sheet.", Reference: "FIX-S-1")],
                    Today.AddDays(-1)),
            ]
            : [],
        Facts = Facts(state),
    };

    // ---- WP04.6 --------------------------------------------------------

    public static BusinessRecordCatalog BuildRecordCatalog() => Build((d, p) => new BusinessRecordCatalog(d, p));

    public static BusinessRecord Record(
        string reference = "REC-1",
        BusinessRecordKind kind = BusinessRecordKind.FinancialRecord,
        bool retentionDecided = true) => new()
    {
        Reference = reference,
        Title = "Fictional supplier invoice",
        Kind = kind,
        DocumentId = Guid.NewGuid(),
        RecordedOn = Today.AddMonths(-2),
        Classification = ConfidentialityClassification.Confidential,
        Retention = retentionDecided
            ? new RetentionTerms(Today.AddYears(6), "Fixture basis: the organisation's own six-year policy.")
            : RetentionTerms.Undecided,
        Party = PartyReference.Supplier("Notional Machining Ltd", "sup-1"),
        Facts = Facts(),
    };

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
