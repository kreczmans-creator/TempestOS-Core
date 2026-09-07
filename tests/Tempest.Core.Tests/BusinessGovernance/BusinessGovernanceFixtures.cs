using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Assets;
using Tempest.Core.BusinessGovernance.Contracts;
using Tempest.Core.BusinessGovernance.Development;
using Tempest.Core.BusinessGovernance.Finance;
using Tempest.Core.BusinessGovernance.Operating;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessGovernance.Risk;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests.EngineeringIntelligence;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.BusinessGovernance;

/// <summary>
/// Shared construction for the `P07` test suite.
/// </summary>
/// <remarks>
/// <b>Every value here is fictional.</b> No real insurer, client,
/// supplier, policy number, rate or forecast appears in this suite. The
/// fixture organisation is "TestFixture Engineering", its client is
/// "Fictional Client Ltd", and its insurer is "Notional Insurance plc" —
/// named so that nobody can mistake a fixture for a business fact.
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

    // ---- C1 -----------------------------------------------------------

    public static ContractTemplateCatalog BuildTemplateCatalog() => Build((d, p) => new ContractTemplateCatalog(d, p));

    public static IssuedContractCatalog BuildContractCatalog() => Build((d, p) => new IssuedContractCatalog(d, p));

    public static ContractTemplate Template(string code = "CT-CONSULT-1") => new()
    {
        Code = code,
        Name = "Fixture consultancy agreement",
        Purpose = "A fictional standard form for fixture engagements. Not a real contract template.",
        Governance = Governance(ConfidentialityClassification.Internal),
        LegalReviewState = DeterminationState.Recorded,
        Clauses =
        [
            new ContractClause("1", "Parties", ClauseCategory.Parties, IsMandatory: true, IsNegotiable: false),
            new ContractClause("2", "Scope of services", ClauseCategory.Scope),
            new ContractClause("3", "Charges", ClauseCategory.Price),
            new ContractClause("4", "Payment", ClauseCategory.Payment),
            new ContractClause("5", "Intellectual property", ClauseCategory.IntellectualProperty, IsMandatory: true, RequiresLegalReview: true),
            new ContractClause("6", "Confidentiality", ClauseCategory.Confidentiality, IsMandatory: true),
            new ContractClause("7", "Liability", ClauseCategory.Liability, IsMandatory: true, RequiresLegalReview: true),
            new ContractClause("8", "Termination", ClauseCategory.Termination),
        ],
        DefaultCommercialTerms = Terms(),
    };

    public static CommercialTerms Terms() => new()
    {
        Basis = ChargingBasis.TimeAndMaterials,
        LiabilityCap = Gbp_(250_000m),
        ChangeControlMechanism = "Written variation signed by both parties.",
        PaymentTerms = [new PaymentTerm(PaymentTrigger.OnInvoice, "Payment 30 days from invoice.", DaysToPay: 30)],
    };

    public static IReadOnlyList<ContractParty> Parties() =>
    [
        new ContractParty("TestFixture Engineering Ltd", "Consultant", "00000000"),
        new ContractParty("Fictional Client Ltd", "Client", "00000001"),
    ];

    // ---- C2 -----------------------------------------------------------

    public static BusinessRiskCatalog BuildRiskCatalog() => Build((d, p) => new BusinessRiskCatalog(d, p));

    public static InsurancePolicyCatalog BuildPolicyCatalog() => Build((d, p) => new InsurancePolicyCatalog(d, p));

    public static BusinessRisk Risk(string reference = "RSK-1") => new()
    {
        Reference = reference,
        Title = "A fictional professional-liability exposure.",
        Governance = Governance(ConfidentialityClassification.Confidential),
        Category = BusinessRiskCategory.ProfessionalLiability,
        Cause = "Fixture cause.",
        Consequence = "Fixture consequence.",
        InherentLikelihood = RiskLikelihood.Possible,
        InherentImpact = RiskImpact.Major,
        ResidualLikelihood = RiskLikelihood.Possible,
        ResidualImpact = RiskImpact.Major,
        Treatment = RiskTreatment.Reduce,
    };

    public static InsurancePolicy Policy(string reference = "POL-1", DateOnly? from = null, DateOnly? to = null) => new()
    {
        Reference = reference,
        PolicyNumber = "FIX/0000/00",
        Insurer = "Notional Insurance plc",
        InsuredParty = "TestFixture Engineering Ltd",
        Governance = Governance(ConfidentialityClassification.CommerciallySensitive),
        PeriodOfCover = new EffectivePeriod(from ?? Today.AddMonths(-6), to ?? Today.AddMonths(6)),
        Status = PolicyStatus.Active,
        PolicyDocumentId = Guid.NewGuid(),
        RenewalState = DeterminationState.Recorded,
        Coverages =
        [
            new InsuranceCoverage(
                InsuranceCoverageType.ProfessionalIndemnity,
                "Fictional professional indemnity cover.",
                LimitOfIndemnity: Gbp_(1_000_000m),
                LimitBasis: "Each and every claim"),
        ],
    };

    // ---- C3 -----------------------------------------------------------

    public static IPAssetCatalog BuildIPCatalog() => Build((d, p) => new IPAssetCatalog(d, p));

    public static DataAssetCatalog BuildDataCatalog() => Build((d, p) => new DataAssetCatalog(d, p));

    public static IPAsset IPAsset_(string reference = "IP-1") => new()
    {
        Reference = reference,
        Name = "Fixture analysis method",
        Governance = Governance(),
        Type = IPType.KnowHow,
        Origin = IPOrigin.Background,
        Ownership = IPOwnership.Organisation,
        OwnershipEvidence = [new BusinessEvidence(BusinessEvidenceKind.ExecutedDocument, "Fixture assignment.", Reference: "DOC-1")],
    };

    public static DataAsset DataAsset_(string reference = "DA-1") => new()
    {
        Reference = reference,
        Name = "Fixture client project data",
        Governance = Governance(ConfidentialityClassification.ClientConfidential),
        Category = DataCategory.ClientData,
        ProcessingPurpose = "Delivering the fixture engagement.",
        TransferRestrictions = ["Not to be sent outside the organisation."],
        Retention = new RetentionRule(
            "Held for six years after the engagement ends.",
            RetainForMonths: 72,
            RetentionTrigger: "End of engagement",
            DisposalMethod: "Secure deletion",
            Basis: "Fixture policy",
            BasisState: DeterminationState.Recorded),
    };

    // ---- C4 -----------------------------------------------------------

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

    // ---- C5 -----------------------------------------------------------

    public static FinancialAssumptionCatalog BuildAssumptionCatalog() => Build((d, p) => new FinancialAssumptionCatalog(d, p));

    public static FinancialScenarioCatalog BuildScenarioCatalog() => Build((d, p) => new FinancialScenarioCatalog(d, p));

    public static FinancialPeriod Period(string label = "FY26 Q1", int monthOffset = 0) =>
        new(label, new EffectivePeriod(Today.AddMonths(monthOffset), Today.AddMonths(monthOffset + 3).AddDays(-1)));

    public static FinancialAssumption Assumption(string reference = "ASM-1") => new()
    {
        Reference = reference,
        Statement = "Fixture: eleven chargeable days per engineer per month.",
        Governance = Governance(),
        AssumedValue = 11m,
        Unit = "days per engineer per month",
        State = DeterminationState.Assumed,
        Source = "Fixture judgement, not real historical data.",
    };

    public static FinancialScenario Scenario(string reference = "SCN-1", bool approved = false, params FinancialPeriod[] periods)
    {
        var declared = periods.Length > 0 ? periods : [Period()];

        return new FinancialScenario
        {
            Reference = reference,
            Name = "Fixture planning case",
            Purpose = "A fictional view of a fixture organisation's finances.",
            Currency = Gbp,
            Governance = approved
                ? Governance() with { Authorisations = [Authority(BusinessAuthorityKind.InternalApproval)] }
                : Governance(),
            Periods = declared,
            AssumptionReferences = ["ASM-1"],
        };
    }

    // ---- C6 -----------------------------------------------------------

    public static OpportunityCatalog BuildOpportunityCatalog() => Build((d, p) => new OpportunityCatalog(d, p));

    public static Opportunity Opportunity_(string reference = "OPP-1", PipelineStage stage = PipelineStage.Qualified) => new()
    {
        Reference = reference,
        Title = "Fixture structural review",
        OrganisationName = "Fictional Client Ltd",
        Governance = Governance(ConfidentialityClassification.CommerciallySensitive),
        Stage = stage,
        EstimatedValue = Gbp_(40_000m),
        WinProbability = 0.5m,
        NextAction = "Fixture follow-up call.",
        NextActionDue = Today.AddDays(7),
        ExpectedDecisionDate = Today.AddMonths(2),
        Interactions = [new OpportunityInteraction(Today.AddDays(-7), "Fixture scoping call.", "owner-1")],
    };

    // ---- C7 -----------------------------------------------------------

    public static OperatingScenarioCatalog BuildOperatingCatalog() => Build((d, p) => new OperatingScenarioCatalog(d, p));

    public static OperatingScenario Model(string reference = "OM-1", decimal? demand = null) => new()
    {
        Reference = reference,
        Name = "Fixture current state",
        Purpose = "A fictional view of a fixture organisation's operating model.",
        Period = new FinancialPeriodLabel("FY26", new EffectivePeriod(Today, Today.AddMonths(12).AddDays(-1))),
        Governance = Governance(),
        DemandDaysPerPeriod = demand,
        Capabilities =
        [
            new OperatingCapability("CAP-STR", "Structural analysis", IsHeld: true, HeldBy: ["eng-1", "eng-2"], ServiceCodes: ["ENG-SEN"]),
        ],
        Resources =
        [
            new ResourceCapacity("RES-1", "Fixture Engineer One", ResourceKind.Employee, 220m, 0.65m, CapabilityCodes: ["CAP-STR"]),
        ],
        Assumptions = [new OperatingAssumption("OA-1", "Fixture: demand is evenly spread across the year.")],
    };

    // ---- construction ---------------------------------------------------

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
