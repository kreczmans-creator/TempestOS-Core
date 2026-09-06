using Tempest.Core.BusinessGovernance;
using Tempest.Core.CommercialIntelligence;
using Tempest.Core.CommercialIntelligence.Costs;
using Tempest.Core.CommercialIntelligence.Estimating;
using Tempest.Core.CommercialIntelligence.LeadTimes;
using Tempest.Core.CommercialIntelligence.Procurement;
using Tempest.Core.CommercialIntelligence.Suppliers;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.Tests.ReferenceData;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.EngineeringIntelligence;

namespace Tempest.Core.Tests.CommercialIntelligence;

/// <summary>
/// Shared construction for the `P03` test suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value here is fictional.</b> No real supplier, price, lead
/// time, quotation or customer appears anywhere in this suite. The
/// fixture suppliers are "Notional Machining Ltd", "Fictional Castings
/// Ltd" and "Imaginary Finishing Ltd"; the fixture customer is "Fictional
/// Client Ltd". They are named so that nobody reading a fixture can
/// mistake it for commercial intelligence the organisation actually
/// holds.
/// </para>
/// <para>
/// Nothing in this file is registered anywhere at run time. Fixtures
/// exist to exercise the code and must never become production reference
/// data — every catalogue built here is backed by an
/// <see cref="InMemoryPersistenceStore"/> that dies with the test.
/// </para>
/// </remarks>
internal static class CommercialFixtures
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

    /// <summary>Provenance a named reviewer has verified — the only kind that can reach Released.</summary>
    public static ReferenceProvenance Verified() => new(
        SourceOrganisation: "TestFixture Engineering",
        SourceDocument: "Fixture commercial record (not a real document)",
        SourceRevision: "1",
        SourceDate: new DateOnly(2026, 1, 1),
        SourceLocation: "Fixture",
        ExtractionMethod: ReferenceExtractionMethod.ManualTranscription,
        Notes: "Fictional fixture data. Not commercial intelligence.")
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

    /// <summary>A commercial source somebody dated and evidenced.</summary>
    public static CommercialSource Source(DateOnly? on = null) => new(
        on ?? Today.AddMonths(-1),
        [new BusinessEvidence(BusinessEvidenceKind.Quotation, "Fictional supplier quotation.", Reference: "FIX-Q-1")]);

    /// <summary>Applicability naming a supplier, a process and a quantity band.</summary>
    public static CommercialApplicability Applicability(
        string? supplierReference = null,
        string? processRecordId = "proc-turning",
        QuantityBand? quantities = null,
        DateOnly? from = null) => new()
    {
        SupplierReference = supplierReference,
        ProcessRecordId = processRecordId,
        Quantities = quantities ?? QuantityBand.From(1),
        Validity = new EffectivePeriod(from ?? Today.AddMonths(-2), Today.AddMonths(10)),
        Geography = new GeographicScope("GB"),
    };

    /// <summary>The enquiry the fixture cost and lead-time records answer.</summary>
    public static CommercialEnquiry Enquiry(
        string? supplierReference = null,
        string? processRecordId = "proc-turning",
        int quantity = 10) => new()
    {
        SupplierReference = supplierReference,
        ProcessRecordId = processRecordId,
        Quantity = quantity,
        AsAt = Today,
        CountryCode = "GB",
    };

    // ---- D1 -----------------------------------------------------------

    public static SupplierCatalog BuildSupplierCatalog() => Build((d, p) => new SupplierCatalog(d, p));

    public static SupplierRecord Supplier(string reference = "SUP-1", string name = "Notional Machining Ltd") => new()
    {
        Identity = new SupplierIdentity
        {
            Reference = reference,
            LegalName = name,
            RegistrationNumber = "00000000",
            RegistrationCountry = "GB",
            Confidence = IdentityConfidence.Confirmed,
        },
        Status = SupplierStatus.Active,
        TradingCurrency = Gbp,
        Source = Source(),
        Notes = "Fictional supplier. Not a real business.",
    };

    // ---- D2 -----------------------------------------------------------

    public static ProcessCostCatalog BuildCostCatalog() => Build((d, p) => new ProcessCostCatalog(d, p));

    public static ProcessCostRecord Cost(
        string reference = "COST-1",
        decimal amount = 12.50m,
        CurrencyCode? currency = null,
        string? supplierReference = null) => new()
    {
        Reference = reference,
        Description = "Fictional turning cost per part.",
        Basis = CostBasis.PerPart,
        Cost = CostFigure.Quoted(new Money(amount, currency ?? Gbp)),
        Applicability = Applicability(supplierReference),
        Source = Source(),
    };

    // ---- D3 -----------------------------------------------------------

    public static LeadTimeCatalog BuildLeadTimeCatalog() => Build((d, p) => new LeadTimeCatalog(d, p));

    public static LeadTimeRecord LeadTime(
        string reference = "LT-1",
        int weeks = 3,
        LeadTimeKind kind = LeadTimeKind.Typical,
        string? supplierReference = null) => new()
    {
        Reference = reference,
        Description = "Fictional turning lead time.",
        Kind = kind,
        Typical = LeadTimeDuration.Weeks(weeks),
        Applicability = Applicability(supplierReference),
        Source = Source(),
    };

    // ---- D4 -----------------------------------------------------------

    public static CostEstimateCatalog BuildEstimateCatalog() => Build((d, p) => new CostEstimateCatalog(d, p));

    public static SupplierQuoteCatalog BuildQuoteCatalog() => Build((d, p) => new SupplierQuoteCatalog(d, p));

    public static CustomerQuotationCatalog BuildQuotationCatalog() => Build((d, p) => new CustomerQuotationCatalog(d, p));

    public static CostEstimate Estimate(string reference = "EST-1", CurrencyCode? currency = null) => new()
    {
        Reference = reference,
        Subject = "Fictional bracket assembly, batch of ten.",
        Currency = currency ?? Gbp,
        Quantity = 10,
        PreparedByPrincipalId = "estimator-1",
        PreparedOn = Today,
        Lines =
        [
            new EstimateLine(
                "L1",
                EstimateLineKind.Process,
                "Turning.",
                10m,
                CostFigure.Quoted(new Money(12.50m, currency ?? Gbp)),
                SourcePins: [new ReferencePin("CommercialProcessCosts", "cost-1", 1)],
                LeadTime: LeadTimeDuration.Weeks(3)),
            new EstimateLine(
                "L2",
                EstimateLineKind.Material,
                "Bar stock.",
                10m,
                CostFigure.Estimated(new Money(4.00m, currency ?? Gbp)),
                SourcePins: [new ReferencePin("Materials", "mat-1", 2)]),
        ],
    };

    public static SupplierQuote Quote(
        string reference = "SQ-1",
        string supplierRecordId = "sup-1",
        QuoteFirmness firmness = QuoteFirmness.Firm,
        DateOnly? validTo = null) => new()
    {
        Reference = reference,
        SupplierRecordId = supplierRecordId,
        SupplierQuotationNumber = "FIX/2026/001",
        Subject = "Fictional turned parts, batch of ten.",
        Currency = Gbp,
        Firmness = firmness,
        QuotedOn = Today.AddDays(-7),
        Validity = new EffectivePeriod(Today.AddDays(-7), validTo ?? Today.AddDays(23)),
        Lines = [new SupplierQuoteLine("1", "Turning, ten off.", 10m, Gbp_(12.50m), LeadTimeDuration.Weeks(3))],
        Conditions = ["Fixture condition, not a real term."],
        Evidence = [new BusinessEvidence(BusinessEvidenceKind.Quotation, "Fictional quotation PDF.", Reference: "FIX/2026/001")],
    };

    public static CustomerQuotation Quotation(
        string reference = "CQ-1",
        QuotationStatus status = QuotationStatus.Draft,
        decimal unitPrice = 25.00m) => new()
    {
        Reference = reference,
        CustomerName = "Fictional Client Ltd",
        Subject = "Fictional bracket assembly, batch of ten.",
        Currency = Gbp,
        Status = status,
        Lines = [new QuotationLine("1", "Bracket assembly, ten off.", 10m, Gbp_(unitPrice), EstimateLineReference: "L1")],
        EstimatePin = new ReferencePin(CostEstimateCatalog.EstimateLibraryName, "est-1", 1),
        Validity = new EffectivePeriod(Today, Today.AddDays(30)),
    };

    /// <summary>An act of authority a fictional person exercised.</summary>
    public static BusinessAuthorisation Authority(
        BusinessAuthorityKind kind = BusinessAuthorityKind.CommercialCommitment,
        string principalId = "director-1") =>
        new(kind, principalId, "Director", Today, "Fixture basis, not a real authorisation.");

    // ---- D5 -----------------------------------------------------------

    public static SourcingRequirementCatalog BuildRequirementCatalog() => Build((d, p) => new SourcingRequirementCatalog(d, p));

    public static SourcingComparisonCatalog BuildComparisonCatalog() => Build((d, p) => new SourcingComparisonCatalog(d, p));

    /// <summary>A requirement with one mandatory criterion and three weighted ones summing to 1.</summary>
    public static SourcingRequirement Requirement(string reference = "REQ-1") => new()
    {
        Reference = reference,
        Subject = "Fictional turned parts, batch of ten.",
        ComparisonCurrency = Gbp,
        Quantity = QuantityBand.Exactly(10),
        RaisedByPrincipalId = "buyer-1",
        RaisedOn = Today,
        RequiredBy = Today.AddMonths(2),
        Criteria =
        [
            new SourcingCriterion("CAP", SourcingCriterionKind.Capability, "Must be able to turn to the drawing.", SourcingCriterionRole.Mandatory),
            new SourcingCriterion("COST", SourcingCriterionKind.Cost, "Lowest total cost.", SourcingCriterionRole.Weighted, 0.5m),
            new SourcingCriterion("LEAD", SourcingCriterionKind.LeadTime, "Shortest lead time.", SourcingCriterionRole.Weighted, 0.3m),
            new SourcingCriterion("QUAL", SourcingCriterionKind.Quality, "Holds an appropriate quality approval.", SourcingCriterionRole.Weighted, 0.2m),
        ],
    };

    /// <summary>A candidate assessed on every criterion the fixture requirement states.</summary>
    public static SourcingCandidate Candidate(
        string code,
        string supplierRecordId,
        CriterionStanding capability = CriterionStanding.Meets,
        CriterionStanding cost = CriterionStanding.Meets,
        CriterionStanding lead = CriterionStanding.Meets,
        CriterionStanding quality = CriterionStanding.Meets,
        decimal? price = 125.00m) => new()
    {
        Code = code,
        SupplierRecordId = supplierRecordId,
        SupplierPin = new ReferencePin(SupplierCatalog.SupplierLibraryName, supplierRecordId, 1),
        Price = price is { } amount ? Gbp_(amount) : null,
        LeadTime = LeadTimeDuration.Weeks(3),
        Assessments =
        [
            Assessed("CAP", capability),
            Assessed("COST", cost),
            Assessed("LEAD", lead),
            Assessed("QUAL", quality),
        ],
    };

    /// <summary>An assessment somebody supported with a pinned record.</summary>
    public static CriterionAssessment Assessed(string criterionCode, CriterionStanding standing) => new(
        criterionCode,
        standing,
        "Fixture assessment.",
        SourcePins: [new ReferencePin(SupplierCatalog.SupplierLibraryName, "sup-1", 1)]);

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
