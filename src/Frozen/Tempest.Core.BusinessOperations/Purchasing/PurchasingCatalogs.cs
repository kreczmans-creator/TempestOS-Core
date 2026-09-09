using Tempest.Core.CommercialIntelligence.Estimating;
using Tempest.Core.EngineeringData;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;
using Tempest.Core.Persistence;
using Tempest.Core.ReferenceData;

namespace Tempest.Core.BusinessOperations.Purchasing;

/// <summary>What the business has asked to buy.</summary>
public interface IPurchaseRequisitionCatalog : IReferenceDataCatalog<PurchaseRequisition>
{
    /// <summary>Returns the requisition registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<PurchaseRequisition>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every requisition still needing somebody's attention, oldest first. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<PurchaseRequisition>>> FindOutstandingAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPurchaseRequisitionCatalog"/> implementation.</summary>
public sealed class PurchaseRequisitionCatalog : ReferenceDataCatalog<PurchaseRequisition>, IPurchaseRequisitionCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every requisition's own backing document carries.</summary>
    public const string RequisitionDocumentKind = "BusinessPurchaseRequisition";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string RequisitionLibraryName = "BusinessPurchaseRequisitions";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>requisitionId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessPurchaseRequisitions.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>requisitionId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessPurchaseRequisitions.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="PurchaseRequisitionCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public PurchaseRequisitionCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => RequisitionLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => RequisitionDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<PurchaseRequisition>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(PurchaseRequisition.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<PurchaseRequisition>>> FindOutstandingAsync(CancellationToken cancellationToken = default)
    {
        var outstanding = await FilterAsync(
            record => record.Definition.State is RequisitionState.Requested
                or RequisitionState.Sourcing
                or RequisitionState.AwaitingAuthority,
            cancellationToken).ConfigureAwait(false);

        return outstanding
            .OrderBy(r => r.Definition.Facts.RaisedOn ?? DateOnly.MaxValue)
            .ThenBy(r => r.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(PurchaseRequisition definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(PurchaseRequisition definition) => $"Requisition reference '{definition.Reference}'";
}

/// <summary>What the business has ordered.</summary>
public interface IPurchaseOrderCatalog : IReferenceDataCatalog<PurchaseOrder>
{
    /// <summary>Returns the order registered under <paramref name="reference"/>, or <see langword="null"/> if none is.</summary>
    /// <exception cref="ArgumentException"><paramref name="reference"/> is null, empty, or whitespace.</exception>
    Task<IReferenceRecord<PurchaseOrder>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default);

    /// <summary>Every order placed and not fully received, most overdue first. Never <see langword="null"/>.</summary>
    Task<IReadOnlyList<IReferenceRecord<PurchaseOrder>>> FindAwaitingDeliveryAsync(DateOnly asAt, CancellationToken cancellationToken = default);

    /// <summary>Every order drawing on <paramref name="budgetReference"/>. Never <see langword="null"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="budgetReference"/> is null, empty, or whitespace.</exception>
    Task<IReadOnlyList<IReferenceRecord<PurchaseOrder>>> FindForBudgetAsync(string budgetReference, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IPurchaseOrderCatalog"/> implementation.</summary>
public sealed class PurchaseOrderCatalog : ReferenceDataCatalog<PurchaseOrder>, IPurchaseOrderCatalog
{
    /// <summary>The <see cref="IEngineeringDocument.Kind"/> every order's own backing document carries.</summary>
    public const string PurchaseOrderDocumentKind = "BusinessPurchaseOrder";

    /// <summary>The <see cref="ReferenceDataCatalog{TDefinition}.LibraryName"/> a <see cref="ReferencePin"/> into this library carries.</summary>
    public const string PurchaseOrderLibraryName = "BusinessPurchaseOrders";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each registered <c>orderId</c> to its own backing document Id.</summary>
    public const string IndexCollection = "BusinessPurchaseOrders.Index";

    /// <summary>The <see cref="IPersistenceStore"/> collection mapping each reference to the <c>orderId</c> holding it.</summary>
    public const string ReferenceIndexCollection = "BusinessPurchaseOrders.ReferenceIndex";

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderCatalog"/> class.</summary>
    /// <param name="documentStore">The store this instance's own records are backed by.</param>
    /// <param name="persistenceStore">The store this instance's own indexes are held in.</param>
    /// <param name="logger">An optional logger for diagnostic output.</param>
    public PurchaseOrderCatalog(IEngineeringDocumentStore documentStore, IPersistenceStore persistenceStore, ILogger? logger = null)
        : base(documentStore, persistenceStore, logger)
    {
    }

    /// <inheritdoc />
    public override string LibraryName => PurchaseOrderLibraryName;

    /// <inheritdoc />
    public override string DocumentKind => PurchaseOrderDocumentKind;

    /// <inheritdoc />
    public override string IndexCollectionName => IndexCollection;

    /// <inheritdoc />
    public override string SecondaryIndexCollectionName => ReferenceIndexCollection;

    /// <inheritdoc />
    public Task<IReferenceRecord<PurchaseOrder>?> FindByReferenceAsync(string reference, CancellationToken cancellationToken = default) =>
        FindBySecondaryKeyAsync(PurchaseOrder.ReferenceKeyFor(reference), cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<IReferenceRecord<PurchaseOrder>>> FindAwaitingDeliveryAsync(
        DateOnly asAt,
        CancellationToken cancellationToken = default)
    {
        var awaiting = await FilterAsync(
            record => record.Definition.State is PurchaseOrderState.Placed or PurchaseOrderState.PartiallyReceived,
            cancellationToken).ConfigureAwait(false);

        return awaiting
            .OrderByDescending(o => o.Definition.LateLines(asAt).Count)
            .ThenBy(o => o.Definition.Reference, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IReferenceRecord<PurchaseOrder>>> FindForBudgetAsync(
        string budgetReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(budgetReference);

        var budget = budgetReference.Trim();

        return FilterAsync(
            record => string.Equals(record.Definition.BudgetReference, budget, StringComparison.OrdinalIgnoreCase),
            cancellationToken);
    }

    /// <inheritdoc />
    protected override string? GetSecondaryKey(PurchaseOrder definition) => definition.ReferenceKey;

    /// <inheritdoc />
    protected override string DescribeSecondaryKey(PurchaseOrder definition) => $"Purchase order reference '{definition.Reference}'";
}

/// <summary>The diagnostic codes WP04.4's validation reports.</summary>
public static class PurchasingValidationRules
{
    /// <summary>The order is recorded as placed but names nobody who placed it.</summary>
    /// <remarks>
    /// The heaviest finding in `WP04.4`, and an error. An order commits
    /// the business's money, and one it cannot attribute to a person is a
    /// commitment nobody is accountable for.
    /// </remarks>
    public const string OrderPlacedWithoutAuthority = "TEMPEST-BOP-001";

    /// <summary>The order has no lines, so it orders nothing.</summary>
    public const string OrderHasNoLines = "TEMPEST-BOP-002";

    /// <summary>A line is stated in a currency other than the order's.</summary>
    public const string LineCurrencyMismatch = "TEMPEST-BOP-003";

    /// <summary>Two lines share one reference.</summary>
    public const string DuplicateLineReference = "TEMPEST-BOP-004";

    /// <summary>A placed order carries no date.</summary>
    public const string PlacedOrderHasNoDate = "TEMPEST-BOP-005";

    /// <summary>The order names a supplier that resolves to no governed record.</summary>
    public const string SupplierIsUnresolved = "TEMPEST-BOP-006";

    /// <summary>The order names a requisition the library does not hold.</summary>
    public const string RequisitionMustResolve = "TEMPEST-BOP-007";

    /// <summary>The order was placed against a supplier quote that has since been superseded.</summary>
    public const string QuoteHasBeenSuperseded = "TEMPEST-BOP-008";

    /// <summary>The order draws on no budget.</summary>
    public const string OrderIsUnbudgeted = "TEMPEST-BOP-009";

    /// <summary>More arrived on a line than was ordered.</summary>
    public const string LineIsOverReceived = "TEMPEST-BOP-010";

    /// <summary>A line has not arrived and is past its expected date.</summary>
    public const string LineIsLate = "TEMPEST-BOP-011";

    /// <summary>An order was cancelled without a stated reason.</summary>
    public const string CancellationHasNoReason = "TEMPEST-BOP-012";

    /// <summary>A requisition reached order without anybody comparing options.</summary>
    /// <remarks>
    /// A warning, never an error. Single-sourcing is frequently correct
    /// and occasionally unavoidable; what must not happen is that nobody
    /// can tell it happened.
    /// </remarks>
    public const string OrderedWithoutSourcing = "TEMPEST-BOP-013";

    /// <summary>A requisition was declined without a stated reason.</summary>
    public const string DeclineHasNoReason = "TEMPEST-BOP-014";

    /// <summary>A requisition is recorded as ordered but names no order.</summary>
    public const string OrderedRequisitionNamesNoOrder = "TEMPEST-BOP-015";
}

/// <summary>Governance of the purchase-order library.</summary>
public interface IPurchaseOrderValidationService : IReferenceValidationService<PurchaseOrder>
{
}

/// <summary>The concrete <see cref="IPurchaseOrderValidationService"/> implementation.</summary>
/// <remarks>
/// One finding is an error where the rest are warnings: an order placed
/// with nobody named as having placed it. `P03` never orders anything
/// (`ADR-0135`) and `P04` only records that a person did, so an order
/// with no authority behind it is a commitment the business cannot
/// account for.
/// </remarks>
public sealed class PurchaseOrderValidationService : ReferenceValidationService<PurchaseOrder>, IPurchaseOrderValidationService
{
    private readonly IPurchaseRequisitionCatalog? _requisitions;
    private readonly ISupplierQuoteCatalog? _quotes;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderValidationService"/> class.</summary>
    /// <param name="catalog">The order library whose records this service validates.</param>
    /// <param name="requisitions">The requisition library, for confirming a named requisition exists. Optional.</param>
    /// <param name="quotes">The `P03` supplier-quote library, for confirming a pinned quote still stands. Optional.</param>
    /// <param name="timeProvider">The clock lateness is judged against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public PurchaseOrderValidationService(
        IPurchaseOrderCatalog catalog,
        IPurchaseRequisitionCatalog? requisitions = null,
        ISupplierQuoteCatalog? quotes = null,
        TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _requisitions = requisitions;
        _quotes = quotes;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override async Task EvaluateDefinitionAsync(
        PurchaseOrder definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Purchase order '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.IsPlacedWithoutAuthority)
            errors.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.OrderPlacedWithoutAuthority,
                $"{subject} is recorded as {definition.State} and names nobody who placed it. An order commits the "
                + "business's money; TempestOS records that a person placed it and never places one itself."));

        if (definition.Lines.Count == 0)
            errors.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.OrderHasNoLines,
                $"{subject} has no lines, so it orders nothing."));

        OperationalValidation.EvaluateDuplicateReferences(
            definition.Lines.Select(l => l.Reference),
            $"{subject} has two lines sharing the reference",
            errors);

        foreach (var line in definition.Lines.Where(l => l.UnitPrice.Currency != definition.Currency))
            errors.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.LineCurrencyMismatch,
                $"{subject} line '{line.Reference}' is priced in {line.UnitPrice.Currency} but the order is in "
                + $"{definition.Currency}. TempestOS does not convert currencies."));

        if (definition.HasBeenPlaced && definition.PlacedOn is null)
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.PlacedOrderHasNoDate,
                $"{subject} has been placed and carries no date."));

        OperationalValidation.EvaluateParty(definition.Supplier, subject, warnings);

        if (definition.BudgetReference is null)
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.OrderIsUnbudgeted,
                $"{subject} draws on no budget, so nothing measures the spend against what was allowed."));

        foreach (var line in definition.OverReceivedLines)
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.LineIsOverReceived,
                $"{subject} line '{line.Reference}' received {line.QuantityReceived} against {line.Quantity} ordered."));

        foreach (var line in definition.LateLines(today))
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.LineIsLate,
                $"{subject} line '{line.Reference}' was expected on {line.ExpectedOn:O} and has not fully arrived."));

        if (definition.State == PurchaseOrderState.Cancelled && string.IsNullOrWhiteSpace(definition.CancellationReason))
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.CancellationHasNoReason,
                $"{subject} was cancelled without a stated reason."));

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings, requireDueDate: false);

        await EvaluateLinksAsync(definition, subject, warnings, cancellationToken).ConfigureAwait(false);
    }

    private async Task EvaluateLinksAsync(
        PurchaseOrder definition,
        string subject,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        if (_requisitions is not null && definition.RequisitionReference is { } requisitionReference)
        {
            var requisition = await _requisitions
                .FindByReferenceAsync(requisitionReference, cancellationToken)
                .ConfigureAwait(false);

            if (requisition is null)
                warnings.Add(OperationalValidation.Diagnostic(
                    PurchasingValidationRules.RequisitionMustResolve,
                    $"{subject} arises from requisition '{requisitionReference}', which the library does not hold."));

            else if (!requisition.Definition.WasSourced)
                warnings.Add(OperationalValidation.Diagnostic(
                    PurchasingValidationRules.OrderedWithoutSourcing,
                    $"{subject} arises from a requisition that records no supplier comparison and no quote. "
                    + "Single-sourcing is frequently correct; what must not happen is that nobody can tell it happened."));
        }

        if (_quotes is null || definition.SupplierQuotePin is not { } pin)
            return;

        var quote = await _quotes.FindAsync(pin.RecordId, cancellationToken).ConfigureAwait(false);

        if (quote?.ValidationState == ReferenceValidationState.Superseded)
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.QuoteHasBeenSuperseded,
                $"{subject} was placed against supplier quote '{pin.RecordId}' revision {pin.RevisionNumber}, which has "
                + "since been superseded. The order is unchanged and remains an accurate record of what was agreed."));
    }
}

/// <summary>Governance of the requisition library.</summary>
public interface IPurchaseRequisitionValidationService : IReferenceValidationService<PurchaseRequisition>
{
}

/// <summary>The concrete <see cref="IPurchaseRequisitionValidationService"/> implementation.</summary>
public sealed class PurchaseRequisitionValidationService
    : ReferenceValidationService<PurchaseRequisition>, IPurchaseRequisitionValidationService
{
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="PurchaseRequisitionValidationService"/> class.</summary>
    /// <param name="catalog">The requisition library whose records this service validates.</param>
    /// <param name="timeProvider">The clock date checks are made against. <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    public PurchaseRequisitionValidationService(IPurchaseRequisitionCatalog catalog, TimeProvider? timeProvider = null)
        : base(catalog, materialCatalog: null, standardResolver: null)
    {
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    protected override Task EvaluateDefinitionAsync(
        PurchaseRequisition definition,
        List<IValidationDiagnostic> errors,
        List<IValidationDiagnostic> warnings,
        CancellationToken cancellationToken)
    {
        var subject = $"Requisition '{definition.Reference}'";
        var today = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

        if (definition.State == RequisitionState.Declined && string.IsNullOrWhiteSpace(definition.DeclineReason))
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.DeclineHasNoReason,
                $"{subject} was declined without a stated reason."));

        if (definition.State == RequisitionState.Ordered && definition.PurchaseOrderReference is null)
            errors.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.OrderedRequisitionNamesNoOrder,
                $"{subject} is recorded as ordered and names no order, so nothing links the need to what was bought."));

        if (definition.State == RequisitionState.Ordered && !definition.WasSourced)
            warnings.Add(OperationalValidation.Diagnostic(
                PurchasingValidationRules.OrderedWithoutSourcing,
                $"{subject} reached order without recording a supplier comparison or a quote."));

        OperationalValidation.Evaluate(definition.Facts, subject, today, errors, warnings);

        return Task.CompletedTask;
    }
}
