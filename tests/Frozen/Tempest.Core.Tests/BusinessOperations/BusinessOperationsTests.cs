using System.Reflection;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessOperations;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.BusinessOperations.Finance;
using Tempest.Core.BusinessOperations.Purchasing;
using Tempest.Core.BusinessOperations.Quality;
using Tempest.Core.BusinessOperations.Records;
using Tempest.Core.Knowledge.Lessons;
using Tempest.Core.ReferenceData;
using Xunit;

namespace Tempest.Core.Tests.BusinessOperations;

// WP 18.0C (D-028): the archived half of the live
// tests/Tempest.Core.Tests/BusinessOperations/BusinessOperationsTests.cs,
// split out the same day its Organisation/Contact/Budget half stayed
// live. Interaction (Crm), FinancialEntry (Finance), all of Purchasing,
// Quality and Records.

/// <summary>`WP04.1` — the interaction half of the record `P07`'s opportunities had nothing to point at.</summary>
public sealed class CrmInteractionTests
{
    [Fact]
    public async Task An_interaction_dated_in_the_future_is_an_error()
    {
        var organisations = OperationsFixtures.BuildOrganisationCatalog();
        var contacts = OperationsFixtures.BuildContactCatalog();
        var service = new CrmValidationService(organisations, contacts, OperationsFixtures.Clock());

        var result = await service.ValidateInteractionAsync(
            OperationsFixtures.Interaction() with { OccurredOn = OperationsFixtures.Today.AddDays(1) });

        Assert.Contains(result.Errors, e => e.Code == CrmValidationRules.InteractionIsInTheFuture);
    }

    [Fact]
    public async Task An_overdue_agreed_action_is_reported()
    {
        var organisations = OperationsFixtures.BuildOrganisationCatalog();
        var contacts = OperationsFixtures.BuildContactCatalog();
        var service = new CrmValidationService(organisations, contacts, OperationsFixtures.Clock());

        var interaction = OperationsFixtures.Interaction() with
        {
            AgreedNextAction = "Send the fixture quotation.",
            NextActionDue = OperationsFixtures.Today.AddDays(-4),
        };

        Assert.True(interaction.IsActionOverdueAt(OperationsFixtures.Today));

        var result = await service.ValidateInteractionAsync(interaction);

        Assert.Contains(result.Warnings, w => w.Code == CrmValidationRules.AgreedActionIsOverdue);
    }
}

/// <summary>`WP04.3` — measuring against what was promised, not what was paid.</summary>
public sealed class FinanceTests
{
    [Fact]
    public async Task A_budget_position_measures_remaining_against_commitments()
    {
        var budgets = OperationsFixtures.BuildBudgetCatalog();
        var entries = OperationsFixtures.BuildEntryCatalog();

        await OperationsFixtures.RegisterAsync(budgets, "bud-1", OperationsFixtures.Budget());
        await OperationsFixtures.RegisterAsync(entries, "fe-1", OperationsFixtures.Entry("FE-1", 2_500m, FinancialPosture.Committed));
        await OperationsFixtures.RegisterAsync(entries, "fe-2", OperationsFixtures.Entry("FE-2", 1_500m, FinancialPosture.Actual));

        var position = await new BudgetPositionService(budgets, entries).PositionAsync("BUD-1");

        Assert.NotNull(position);
        Assert.Equal(OperationsFixtures.Gbp_(10_000m), position.Allowed);

        // Both entries are money promised away; only one has moved.
        Assert.Equal(OperationsFixtures.Gbp_(4_000m), position.Committed);
        Assert.Equal(OperationsFixtures.Gbp_(1_500m), position.Actual);
        Assert.Equal(OperationsFixtures.Gbp_(6_000m), position.UncommittedRemaining);
        Assert.Equal(OperationsFixtures.Gbp_(2_500m), position.OutstandingCommitment);
        Assert.False(position.IsOvercommitted);
    }

    [Fact]
    public async Task An_entry_in_another_currency_is_excluded_rather_than_converted()
    {
        var budgets = OperationsFixtures.BuildBudgetCatalog();
        var entries = OperationsFixtures.BuildEntryCatalog();

        await OperationsFixtures.RegisterAsync(budgets, "bud-1", OperationsFixtures.Budget());
        await OperationsFixtures.RegisterAsync(entries, "fe-1", OperationsFixtures.Entry(currency: OperationsFixtures.Eur));

        var position = await new BudgetPositionService(budgets, entries).PositionAsync("BUD-1");

        // Dropping it silently would understate the spend; converting it
        // would invent a rate. It is excluded, and validation reports it.
        Assert.Equal(OperationsFixtures.Gbp_(0m), position!.Committed);
    }

    [Fact]
    public async Task An_overcommitted_budget_says_so()
    {
        var budgets = OperationsFixtures.BuildBudgetCatalog();
        var entries = OperationsFixtures.BuildEntryCatalog();

        await OperationsFixtures.RegisterAsync(budgets, "bud-1", OperationsFixtures.Budget(amount: 1_000m));
        await OperationsFixtures.RegisterAsync(entries, "fe-1", OperationsFixtures.Entry("FE-1", 1_500m));

        var position = await new BudgetPositionService(budgets, entries).PositionAsync("BUD-1");

        Assert.True(position!.IsOvercommitted);
        Assert.False(position.IsOverspent);
    }

    [Fact]
    public async Task A_budget_nobody_set_is_an_error()
    {
        var catalog = OperationsFixtures.BuildBudgetCatalog();
        var service = new BudgetValidationService(catalog, OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Budget() with { SetUnderAuthority = null },
            OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == FinanceValidationRules.BudgetHasNoAuthority);
    }

    [Fact]
    public async Task A_budget_line_in_another_currency_is_an_error()
    {
        var catalog = OperationsFixtures.BuildBudgetCatalog();
        var service = new BudgetValidationService(catalog, OperationsFixtures.Clock());

        var budget = OperationsFixtures.Budget() with
        {
            Lines = [new BudgetLine("L1", "Fixture", new Money(100m, OperationsFixtures.Eur))],
        };

        var result = await service.ValidateDefinitionAsync(budget, OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == FinanceValidationRules.LineCurrencyMismatch);
    }

    [Fact]
    public async Task A_position_for_an_unregistered_budget_is_nothing_rather_than_a_zero()
    {
        var budgets = OperationsFixtures.BuildBudgetCatalog();
        var entries = OperationsFixtures.BuildEntryCatalog();

        Assert.Null(await new BudgetPositionService(budgets, entries).PositionAsync("NOT-A-BUDGET"));
    }
}

/// <summary>`WP04.4` — recording an order a person placed, never placing one.</summary>
public sealed class PurchasingTests
{
    [Fact]
    public async Task An_order_placed_with_nobody_named_as_having_placed_it_is_an_error()
    {
        var catalog = OperationsFixtures.BuildPurchaseOrderCatalog();
        var service = new PurchaseOrderValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var order = OperationsFixtures.PurchaseOrder(authorised: false);

        Assert.True(order.IsPlacedWithoutAuthority);

        var result = await service.ValidateDefinitionAsync(order, OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == PurchasingValidationRules.OrderPlacedWithoutAuthority);
    }

    [Fact]
    public async Task A_draft_order_with_no_authority_is_not_an_error()
    {
        var catalog = OperationsFixtures.BuildPurchaseOrderCatalog();
        var service = new PurchaseOrderValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.PurchaseOrder(state: PurchaseOrderState.Draft, authorised: false),
            OperationsFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == PurchasingValidationRules.OrderPlacedWithoutAuthority);
    }

    [Fact]
    public void An_order_totals_and_tracks_what_has_not_arrived()
    {
        var order = OperationsFixtures.PurchaseOrder(received: 4m);

        Assert.Equal(OperationsFixtures.Gbp_(125m), order.Total);
        Assert.Equal(OperationsFixtures.Gbp_(75m), order.OutstandingValue);
        Assert.False(order.IsFullyReceived);
    }

    [Fact]
    public void Over_delivery_is_recorded_rather_than_refused()
    {
        var order = OperationsFixtures.PurchaseOrder(received: 12m);

        Assert.Single(order.OverReceivedLines);
        Assert.True(order.IsFullyReceived);
    }

    [Fact]
    public async Task An_order_arising_from_an_unsourced_requisition_is_reported()
    {
        var requisitions = OperationsFixtures.BuildRequisitionCatalog();
        await OperationsFixtures.RegisterAsync(requisitions, "req-1", OperationsFixtures.Requisition(sourced: false));

        var orders = OperationsFixtures.BuildPurchaseOrderCatalog();
        var service = new PurchaseOrderValidationService(orders, requisitions, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(OperationsFixtures.PurchaseOrder(), OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == PurchasingValidationRules.OrderedWithoutSourcing);
    }

    [Fact]
    public async Task An_order_placed_against_a_superseded_quote_is_warned_about_and_never_altered()
    {
        var quotes = CommercialIntelligence.CommercialFixtures.BuildQuoteCatalog();
        await CommercialIntelligence.CommercialFixtures.RegisterReleasedAsync(
            quotes, "sq-1", CommercialIntelligence.CommercialFixtures.Quote());
        await CommercialIntelligence.CommercialFixtures.RegisterAsync(
            quotes, "sq-2", CommercialIntelligence.CommercialFixtures.Quote("SQ-2"));
        await quotes.SupersedeAsync("sq-1", "sq-2", "Repriced.");

        var orders = OperationsFixtures.BuildPurchaseOrderCatalog();
        var service = new PurchaseOrderValidationService(orders, quotes: quotes, timeProvider: OperationsFixtures.Clock());

        var order = OperationsFixtures.PurchaseOrder() with
        {
            SupplierQuotePin = new ReferencePin("CommercialSupplierQuotes", "sq-1", 1),
        };

        var result = await service.ValidateDefinitionAsync(order, OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == PurchasingValidationRules.QuoteHasBeenSuperseded);
        Assert.Equal(OperationsFixtures.Gbp_(125m), order.Total);
    }

    [Fact]
    public async Task A_requisition_recorded_as_ordered_that_names_no_order_is_an_error()
    {
        var catalog = OperationsFixtures.BuildRequisitionCatalog();
        var service = new PurchaseRequisitionValidationService(catalog, OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Requisition(state: RequisitionState.Ordered),
            OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == PurchasingValidationRules.OrderedRequisitionNamesNoOrder);
    }

    [Fact]
    public void An_order_line_refuses_a_non_positive_quantity_and_a_negative_receipt()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PurchaseOrderLine("1", "Bracket", 0m, OperationsFixtures.Gbp_(10m)));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PurchaseOrderLine("1", "Bracket", 1m, OperationsFixtures.Gbp_(10m), -1m));
    }
}

/// <summary>`WP04.5` — a clean register and a recurring fault is the failure to prevent.</summary>
public sealed class QualityTests
{
    [Fact]
    public async Task A_concession_with_no_justification_is_an_error()
    {
        var catalog = OperationsFixtures.BuildNonConformanceCatalog();
        var service = new NonConformanceValidationService(catalog, OperationsFixtures.Clock());

        var record = OperationsFixtures.NonConformance() with
        {
            Disposition = new Disposition(DispositionKind.UseAsIs, "engineer-1", OperationsFixtures.Today),
        };

        Assert.True(record.IsConcession);
        Assert.False(record.Disposition!.IsJustified);

        var result = await service.ValidateDefinitionAsync(record, OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == QualityValidationRules.ConcessionIsUnjustified);
    }

    [Fact]
    public async Task A_justified_concession_is_not_an_error()
    {
        var catalog = OperationsFixtures.BuildNonConformanceCatalog();
        var service = new NonConformanceValidationService(catalog, OperationsFixtures.Clock());

        var record = OperationsFixtures.NonConformance() with
        {
            Disposition = new Disposition(
                DispositionKind.UseAsIs,
                "engineer-1",
                OperationsFixtures.Today,
                "Fixture justification: the feature is non-functional in this application."),
        };

        var result = await service.ValidateDefinitionAsync(record, OperationsFixtures.Verified());

        Assert.DoesNotContain(result.Errors, e => e.Code == QualityValidationRules.ConcessionIsUnjustified);
    }

    [Fact]
    public async Task Closing_a_record_while_the_problem_stands_is_an_error()
    {
        var catalog = OperationsFixtures.BuildNonConformanceCatalog();
        var service = new NonConformanceValidationService(catalog, OperationsFixtures.Clock());

        var record = OperationsFixtures.NonConformance(resolved: false, state: OperationalState.Closed);

        Assert.True(record.IsClosedButUnresolved);

        var result = await service.ValidateDefinitionAsync(record, OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == QualityValidationRules.ClosedButUnresolved);
    }

    [Fact]
    public void A_record_is_genuinely_resolved_only_when_every_action_is_shown_to_have_worked()
    {
        Assert.True(OperationsFixtures.NonConformance().IsGenuinelyResolved);
        Assert.False(OperationsFixtures.NonConformance(resolved: false).IsGenuinelyResolved);
    }

    [Fact]
    public void Closed_and_effective_are_different_things()
    {
        var closed = new QualityAction("QA-1", "Did a thing.", Facts: OperationsFixtures.Facts(OperationalState.Closed));

        Assert.False(closed.IsVerifiedEffective);
    }

    [Fact]
    public void A_disposition_must_name_who_decided_it()
    {
        Assert.Throws<ArgumentException>(() => new Disposition(DispositionKind.Scrap, "   "));
    }

    [Fact]
    public async Task Records_closed_but_unresolved_are_surfaced_most_serious_first()
    {
        var catalog = OperationsFixtures.BuildNonConformanceCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "ncr-1",
            OperationsFixtures.NonConformance("NCR-1", NonConformanceSeverity.Minor, resolved: false, state: OperationalState.Closed));
        await OperationsFixtures.RegisterAsync(catalog, "ncr-2",
            OperationsFixtures.NonConformance("NCR-2", NonConformanceSeverity.Critical, resolved: false, state: OperationalState.Closed));

        var found = await catalog.FindClosedButUnresolvedAsync();

        Assert.Equal("NCR-2", found[0].Definition.Reference);
    }
}

/// <summary>`WP04.6` — records the organisation can find and has decided how long to keep.</summary>
public sealed class RecordTests
{
    [Fact]
    public async Task A_card_referring_to_nothing_is_an_error()
    {
        var catalog = OperationsFixtures.BuildRecordCatalog();
        var service = new BusinessRecordValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var result = await service.ValidateDefinitionAsync(
            OperationsFixtures.Record() with { DocumentId = null, ExternalLocation = null },
            OperationsFixtures.Verified());

        Assert.Contains(result.Errors, e => e.Code == RecordValidationRules.RecordIsNotRetrievable);
    }

    [Fact]
    public async Task An_invoice_with_no_retention_decision_is_reported()
    {
        var catalog = OperationsFixtures.BuildRecordCatalog();
        var service = new BusinessRecordValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var record = OperationsFixtures.Record(retentionDecided: false);

        Assert.True(record.NeedsRetentionDecision);

        var result = await service.ValidateDefinitionAsync(record, OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == RecordValidationRules.RetentionNotDecided);
    }

    [Fact]
    public void The_platform_computes_no_retention_period_of_its_own()
    {
        // Undecided stays undecided. Nothing infers six years from the
        // record being an invoice: statutory periods differ by
        // jurisdiction and year, and the platform gives no legal advice.
        Assert.False(RetentionTerms.Undecided.IsDecided);
        Assert.False(RetentionTerms.Undecided.IsDueForReviewAt(OperationsFixtures.Today.AddYears(50)));
    }

    [Fact]
    public async Task A_record_past_its_retention_date_is_reported_and_never_deleted()
    {
        var catalog = OperationsFixtures.BuildRecordCatalog();

        var expired = OperationsFixtures.Record() with
        {
            Retention = new RetentionTerms(OperationsFixtures.Today.AddDays(-1), "Fixture basis."),
        };

        await OperationsFixtures.RegisterAsync(catalog, "rec-1", expired);

        var due = await catalog.FindDueForRetentionReviewAsync(OperationsFixtures.Today);

        Assert.Single(due);

        // Still there afterwards. P04 disposes of nothing.
        Assert.NotNull(await catalog.FindByReferenceAsync("REC-1"));
    }

    [Fact]
    public async Task A_statutory_record_classified_only_internal_is_reported()
    {
        var catalog = OperationsFixtures.BuildRecordCatalog();
        var service = new BusinessRecordValidationService(catalog, timeProvider: OperationsFixtures.Clock());

        var record = OperationsFixtures.Record(kind: BusinessRecordKind.StatutoryRecord) with
        {
            Classification = ConfidentialityClassification.Internal,
        };

        var result = await service.ValidateDefinitionAsync(record, OperationsFixtures.Verified());

        Assert.Contains(result.Warnings, w => w.Code == RecordValidationRules.StatutoryRecordIsLooselyClassified);
    }
}

/// <summary>The persistence cycle §14 requires, for the archived P04 libraries.</summary>
public sealed class BusinessOperationsArchivedPersistenceTests
{
    [Fact]
    public async Task A_released_operational_record_cannot_be_revised_in_place()
    {
        var catalog = OperationsFixtures.BuildPurchaseOrderCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "po-1", OperationsFixtures.PurchaseOrder());
        await OperationsFixtures.ReleaseAsync(catalog, "po-1");

        await Assert.ThrowsAsync<ReleasedReferenceImmutableException>(() =>
            catalog.ReviseAsync("po-1", OperationsFixtures.PurchaseOrder() with { Subject = "Sneaky" }, OperationsFixtures.Verified(), "No."));
    }

    [Fact]
    public async Task A_purchase_order_survives_persistence_with_its_party_and_pin_intact()
    {
        var catalog = OperationsFixtures.BuildPurchaseOrderCatalog();
        var order = OperationsFixtures.PurchaseOrder() with
        {
            SupplierQuotePin = new ReferencePin("CommercialSupplierQuotes", "sq-1", 3),
        };

        await OperationsFixtures.RegisterAsync(catalog, "po-1", order);

        var loaded = (await catalog.FindByReferenceAsync("PO-1"))!.Definition;

        Assert.True(loaded.Supplier.IsResolved);
        Assert.Equal("sup-1", loaded.Supplier.SupplierRecordId);
        Assert.Equal(3, loaded.SupplierQuotePin!.RevisionNumber);
        Assert.Equal(OperationsFixtures.Gbp_(125m), loaded.Total);
    }

    [Fact]
    public async Task A_non_conformance_survives_persistence_with_its_causes_and_actions_intact()
    {
        var catalog = OperationsFixtures.BuildNonConformanceCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "ncr-1", OperationsFixtures.NonConformance());

        var loaded = (await catalog.FindByReferenceAsync("NCR-1"))!.Definition;

        Assert.Single(loaded.RootCauses);
        Assert.Equal(CauseConfidence.Probable, loaded.Causes[0].Confidence);
        Assert.Equal(DispositionKind.Rework, loaded.Disposition!.Kind);
        Assert.Single(loaded.CorrectiveActions);
        Assert.True(loaded.Actions[0].IsVerifiedEffective);
        Assert.True(loaded.IsGenuinelyResolved);
    }

    [Fact]
    public async Task A_business_record_survives_persistence_with_its_retention_intact()
    {
        var catalog = OperationsFixtures.BuildRecordCatalog();

        await OperationsFixtures.RegisterAsync(catalog, "rec-1", OperationsFixtures.Record());

        var loaded = (await catalog.FindByReferenceAsync("REC-1"))!.Definition;

        Assert.Equal(OperationsFixtures.Today.AddYears(6), loaded.Retention.RetainUntil);
        Assert.True(loaded.HasRetentionDecision);
        Assert.Equal(ConfidentialityClassification.Confidential, loaded.Classification);
        Assert.True(loaded.Party!.IsResolved);
    }

    [Theory]
    [MemberData(nameof(RoundTrippableRecords))]
    public void Every_archived_P04_type_round_trips_through_JSON(object record)
    {
        var json = JsonSerializer.Serialize(record, record.GetType());
        var restored = JsonSerializer.Deserialize(json, record.GetType());

        Assert.NotNull(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored, record.GetType()));
    }

    public static TheoryData<object> RoundTrippableRecords() =>
    [
        OperationsFixtures.Interaction(),
        OperationsFixtures.Entry(),
        OperationsFixtures.Requisition(),
        OperationsFixtures.PurchaseOrder(),
        OperationsFixtures.NonConformance(),
        OperationsFixtures.Record(),
        new RetentionTerms(OperationsFixtures.Today, "Fixture basis."),
    ];
}

/// <summary>Structural guards over the archived half of `P04`.</summary>
public sealed class BusinessOperationsArchivedStructuralTests
{
    [Fact]
    public void Every_P04_document_kind_and_library_name_is_unique_and_distinct_from_P05s()
    {
        string[] kinds =
        [
            OrganisationCatalog.OrganisationDocumentKind,
            ContactCatalog.ContactDocumentKind,
            InteractionCatalog.InteractionDocumentKind,
            BudgetCatalog.BudgetDocumentKind,
            FinancialEntryCatalog.FinancialEntryDocumentKind,
            PurchaseRequisitionCatalog.RequisitionDocumentKind,
            PurchaseOrderCatalog.PurchaseOrderDocumentKind,
            NonConformanceCatalog.NonConformanceDocumentKind,
            BusinessRecordCatalog.BusinessRecordDocumentKind,

            // A business record and a technical document are governed
            // differently and must never share a library.
            Tempest.Core.EngineeringAssets.TechnicalDocumentation.TechnicalDocumentCatalog.TechnicalDocumentKind,
        ];

        Assert.Equal(kinds.Length, kinds.Distinct(StringComparer.Ordinal).Count());
    }
}
