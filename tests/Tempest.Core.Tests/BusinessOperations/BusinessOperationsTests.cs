using System.Reflection;
using System.Text.Json;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.BusinessOperations;

// WP 18.0C (D-028): the FinanceTests (BudgetPositionService/
// BudgetValidationService), PurchasingTests, QualityTests and RecordTests
// classes, the interaction-related two CrmTests methods, and the
// archived-type persistence/structural tests moved to
// tests/Frozen/Tempest.Core.Tests/BusinessOperations/
// BusinessOperationsTests.cs the same day. What remains here tests the
// kept kinds: Organisation, Contact, Budget.

/// <summary>The shared operational core.</summary>
public sealed class OperationalCoreTests
{
    [Fact]
    public void A_party_carried_as_a_name_is_recordable_and_says_it_is_unresolved()
    {
        var unresolved = PartyReference.Unresolved(PartyKind.Supplier, "A company nobody has entered yet");

        Assert.False(unresolved.IsResolved);
        Assert.Equal("A company nobody has entered yet", unresolved.DisplayName);
    }

    [Fact]
    public void A_party_pointing_at_a_governed_record_resolves()
    {
        Assert.True(PartyReference.Supplier("Notional Machining Ltd", "sup-1").IsResolved);
        Assert.True(PartyReference.Organisation(PartyKind.Customer, "Fictional Client Ltd", "ORG-1").IsResolved);
    }

    [Fact]
    public void Closing_without_a_date_and_cancelling_without_a_reason_are_the_same_failure()
    {
        var closedBlind = OperationsFixtures.Facts(OperationalState.Closed) with { ClosedOn = null };
        var cancelledBlind = OperationsFixtures.Facts(OperationalState.Cancelled);

        Assert.True(closedBlind.IsIncompletelyClosed);
        Assert.True(cancelledBlind.IsIncompletelyClosed);

        var proper = OperationsFixtures.Facts(OperationalState.Cancelled) with { CancellationReason = "No longer needed." };

        Assert.False(proper.IsIncompletelyClosed);
    }

    [Fact]
    public void Overdue_counts_only_against_outstanding_work()
    {
        var overdue = OperationsFixtures.Facts(dueBy: OperationsFixtures.Today.AddDays(-5));

        Assert.True(overdue.IsOverdueAt(OperationsFixtures.Today));
        Assert.Equal(5, overdue.DaysOverdueAt(OperationsFixtures.Today));

        var done = overdue with { State = OperationalState.Closed, ClosedOn = OperationsFixtures.Today };

        Assert.False(done.IsOverdueAt(OperationsFixtures.Today));
        Assert.Null(done.DaysOverdueAt(OperationsFixtures.Today));
    }
}

/// <summary>`WP04.1` — the record P07's opportunities had nothing to point at.</summary>
public sealed class CrmTests
{
    [Fact]
    public async Task An_organisation_declined_without_a_reason_is_an_error()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        var service = new OrganisationValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Organisation(status: RelationshipStatus.Declined),
            OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == CrmValidationRules.DeclinedWithoutReason);
    }

    [Fact]
    public async Task An_organisation_identified_only_by_name_is_reported()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        var service = new OrganisationValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Organisation() with { RegistrationNumber = null },
            OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == CrmValidationRules.OrganisationHasNoHardIdentifier);
    }

    [Fact]
    public async Task A_tradeable_organisation_nobody_can_reach_is_reported()
    {
        var organisations = OperationsFixtures.BuildOrganisationCatalog();
        var contacts = OperationsFixtures.BuildContactCatalog();

        await OperationsFixtures.RegisterAsync(contacts, "con-1", OperationsFixtures.Contact() with
        {
            EmailAddress = null,
            TelephoneNumber = null,
        });

        var service = new OrganisationValidationService(organisations, contacts, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(OperationsFixtures.Organisation(), OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == CrmValidationRules.NoReachableContact);
    }

    [Fact]
    public async Task An_organisation_that_is_its_own_parent_is_an_error()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();
        var service = new OrganisationValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Organisation() with { ParentOrganisationReference = "ORG-1" },
            OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == CrmValidationRules.OrganisationHierarchyCycle);
    }

    [Fact]
    public async Task Contacts_come_back_primary_first_and_inactive_last()
    {
        var contacts = OperationsFixtures.BuildContactCatalog();

        await OperationsFixtures.RegisterAsync(contacts, "con-2", OperationsFixtures.Contact("CON-2", primary: false));
        await OperationsFixtures.RegisterAsync(contacts, "con-1", OperationsFixtures.Contact("CON-1"));

        var found = await contacts.FindForOrganisationAsync("ORG-1");

        Assert.Equal("CON-1", found[0].Definition.Reference);
    }
}

/// <summary>The persistence cycle §14 requires, for every P04 library.</summary>
public sealed class BusinessOperationsPersistenceTests
{
    [Fact]
    public async Task An_organisation_survives_the_full_create_revise_supersede_cycle()
    {
        var catalog = OperationsFixtures.BuildOrganisationCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "org-1", OperationsFixtures.Organisation());

        var loaded = (await catalog.FindByReferenceAsync("ORG-1"))!.Definition;

        Assert.Equal("Fictional Client Ltd", loaded.Name);
        Assert.Equal(PartyKind.Customer, Assert.Single(loaded.Roles));
        Assert.Equal("FX1 1FX", loaded.Address!.Postcode);
        Assert.Equal("GB", loaded.Address.CountryCode);
        Assert.Equal(OperationsFixtures.Gbp, loaded.TradingCurrency);
        Assert.True(loaded.HasHardIdentifier);

        var revised = await catalog.ReviseAsync(
            "org-1",
            OperationsFixtures.Organisation() with { Name = "Fictional Client (Holdings) Ltd" },
            OperationsFixtures.Verified(),
            "Renamed.");

        Assert.Equal(2, revised.RevisionNumber);
        Assert.Equal("Fictional Client Ltd", (await catalog.GetRevisionAsync("org-1", 1)).Definition.Name);

        await OperationsFixtures.ReleaseAsync(catalog, "org-1");
        await OperationsFixtures.RegisterAsync(catalog, "org-2", OperationsFixtures.Organisation("ORG-2"));
        await catalog.SupersedeAsync("org-1", "org-2", "Merged.");

        Assert.Equal(ReferenceValidationState.Superseded, (await catalog.FindAsync("org-1"))!.ValidationState);
    }

    [Fact]
    public async Task A_budget_survives_persistence_with_its_money_and_authority_intact()
    {
        var catalog = OperationsFixtures.BuildBudgetCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "bud-1", OperationsFixtures.Budget());

        var loaded = (await catalog.FindByReferenceAsync("BUD-1"))!.Definition;

        Assert.Equal(OperationsFixtures.Gbp_(10_000m), loaded.TotalOutgoing);
        Assert.Equal(OperationsFixtures.Gbp, loaded.Currency);
        Assert.True(loaded.IsAuthorised);
        Assert.Equal("director-1", loaded.SetUnderAuthority!.PrincipalId);
        Assert.True(loaded.IsCurrentAt(OperationsFixtures.Today));
    }

    [Theory]
    [MemberData(nameof(RoundTrippableRecords))]
    public void Every_P04_type_round_trips_through_JSON(object record)
    {
        var json = JsonSerializer.Serialize(record, record.GetType());
        var restored = JsonSerializer.Deserialize(json, record.GetType());

        Assert.NotNull(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored, record.GetType()));
    }

    public static TheoryData<object> RoundTrippableRecords() =>
    [
        OperationsFixtures.Organisation(),
        OperationsFixtures.Contact(),
        OperationsFixtures.Budget(),
        OperationsFixtures.Facts(),
        PartyReference.Supplier("Notional Machining Ltd", "sup-1"),
    ];
}

/// <summary>Structural guards over `P04`.</summary>
public sealed class BusinessOperationsStructuralTests
{
    [Fact]
    public void P04_builds_no_second_project_model()
    {
        // WP04.2 is satisfied by Tempest.Workspace/Projects. A project type in
        // BusinessOperations would be the competing system the audit
        // ruled out.
        var offenders = typeof(Organisation).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tempest.Core.BusinessOperations", StringComparison.Ordinal) == true)
            .Where(t => t.Name.StartsWith("Project", StringComparison.Ordinal)
                        || t.Name is "Task" or "Milestone" or "Deliverable")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void P04_declares_no_second_money_representation()
    {
        var offenders = typeof(Organisation).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tempest.Core.BusinessOperations", StringComparison.Ordinal) == true)
            .Where(t => t.Name is "Money" or "Currency" or "CurrencyCode" or "Amount")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void No_P04_type_places_an_order_or_disposes_of_a_record()
    {
        string[] forbidden = ["PlaceOrder", "SendOrder", "Dispose0f", "DeleteRecord", "PurgeRecord", "ApproveOrder"];

        var members = typeof(Organisation).Assembly
            .GetTypes()
            .Where(t => t.Namespace?.StartsWith("Tempest.Core.BusinessOperations", StringComparison.Ordinal) == true)
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(m => m is MethodInfo { IsSpecialName: false })
            .Select(m => m.Name)
            .ToList();

        foreach (var name in forbidden)
            Assert.DoesNotContain(members, m => m.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Every_kept_P04_document_kind_is_unique()
    {
        // WP 18.0C (D-028): this used to also assert the archived document
        // kinds (Interaction, FinancialEntry, Requisition, PurchaseOrder,
        // NonConformance, BusinessRecord) and P05's TechnicalDocument were
        // all distinct too; that fuller assertion moved to
        // tests/Frozen/Tempest.Core.Tests/BusinessOperations/
        // BusinessOperationsTests.cs with the archived kinds themselves.
        string[] kinds =
        [
            OrganisationCatalog.OrganisationDocumentKind,
            ContactCatalog.ContactDocumentKind,
            BudgetCatalog.BudgetDocumentKind,
        ];

        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }
}
