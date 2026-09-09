using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.EngineeringData;
using Tempest.Core.Identity;
using Tempest.Core.ReferenceData;
using Tempest.Core.Tests;
using Tempest.Core.Tests.ReferenceData;

namespace Tempest.Core.Tests.BusinessOperations;

/// <summary>
/// Shared construction for the `P04` test suite.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every value here is fictional.</b> No real customer, supplier,
/// budget, order, non-conformance or business record appears anywhere in
/// this suite. The fixture customer is "Fictional Client Ltd", the
/// fixture supplier is "Notional Machining Ltd", registration numbers are
/// all zeroes, and no monetary figure describes any real transaction.
/// Fixtures live only in the test project, backed by in-memory stores
/// that die with the test.
/// </para>
/// <para>
/// WP 18.0C (D-028): the Interaction/FinancialEntry/Purchasing/Quality/
/// Records builders that used to live here moved to
/// tests/Frozen/Tempest.Core.Tests/BusinessOperations/OperationsFixtures.cs
/// alongside the namespaces they built for. What remains builds only the
/// kept kinds: Organisation, Contact, Budget.
/// </para>
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

    // ---- WP04.3 (Budget only; FinancialEntry archived) ------------------

    public static BudgetCatalog BuildBudgetCatalog() => Build((d, p) => new BudgetCatalog(d, p));

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

    private static TCatalog Build<TCatalog>(Func<EngineeringDocumentStore, InMemoryPersistenceStore, TCatalog> create)
    {
        var persistence = new InMemoryPersistenceStore();

        return create(new EngineeringDocumentStore(persistence, new CurrentPrincipalAccessor()), persistence);
    }
}
