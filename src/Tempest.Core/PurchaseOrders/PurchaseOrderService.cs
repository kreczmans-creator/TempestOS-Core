using System.Globalization;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Projects;

namespace Tempest.Core.PurchaseOrders;

/// <summary>The concrete <see cref="IPurchaseOrderService"/> implementation (`WP 21.3B`).</summary>
public sealed class PurchaseOrderService : IPurchaseOrderService
{
    private const string ReferencePrefix = "PO-";

    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly IExpenseService _expenses;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="PurchaseOrderService"/> class.</summary>
    public PurchaseOrderService(EngineeringDomainContext context, IRateCardCatalog rateCards, IExpenseService expenses, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(expenses);

        _context = context;
        _rateCards = rateCards;
        _expenses = expenses;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> CreateAsync(
        Guid projectId, string? reference = null, string? supplierOrganisationId = null, string? notes = null,
        DateOnly? expectedDelivery = null, CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project || !IsLive(project))
            return new PurchaseOrderResult(PurchaseOrderRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new PurchaseOrderResult(
                PurchaseOrderRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new purchase order can be raised against it.", null);
        }

        var today = Today();
        var resolvedReference = string.IsNullOrWhiteSpace(reference)
            ? await NextReferenceAsync(today.Year, cancellationToken).ConfigureAwait(false)
            : reference.Trim();

        var currency = await ResolveCurrencyAsync(project, cancellationToken).ConfigureAwait(false);

        var created = await new EngineeringObjectFactory<PurchaseOrder>(
            PurchaseOrder.CanonicalKind,
            _context,
            (doc, rev) => new PurchaseOrder(
                doc, rev, _context, identifier: null, $"Purchase order — {resolvedReference}",
                EngineeringObjectMetadata.Empty, resolvedReference, supplierOrganisationId, currency, lines: [],
                expectedDelivery: expectedDelivery, notes: notes))
            .CreateAsync($"Purchase order '{resolvedReference}' raised against project '{projectId}'.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, (PurchaseOrder)created);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> AddLineAsync(
        Guid orderId, string description, decimal quantity, Money unitPrice, VatRate vatRate = VatRate.OutOfScope,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (order.Status != PurchaseOrderStatus.Draft)
            return NotDraft(order, orderId, "added");

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        var (line, refusal, reason) = BuildLine(order, Guid.NewGuid(), description, quantity, unitPrice, vatRate);
        if (line is null)
            return new PurchaseOrderResult(refusal, reason, order);

        await order.AddLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> UpdateLineAsync(
        Guid orderId, Guid lineId, string description, decimal quantity, Money unitPrice, VatRate vatRate,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (order.Status != PurchaseOrderStatus.Draft)
            return NotDraft(order, orderId, "changed");

        if (order.Lines.All(l => l.Id != lineId))
            return new PurchaseOrderResult(PurchaseOrderRefusal.LineNotFound, $"No line '{lineId}' on purchase order '{orderId}'.", order);

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        var (line, refusal, reason) = BuildLine(order, lineId, description, quantity, unitPrice, vatRate);
        if (line is null)
            return new PurchaseOrderResult(refusal, reason, order);

        await order.UpdateLineAsync(line, cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> RemoveLineAsync(Guid orderId, Guid lineId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (order.Status != PurchaseOrderStatus.Draft)
            return NotDraft(order, orderId, "removed");

        var line = order.Lines.FirstOrDefault(l => l.Id == lineId);
        if (line is null)
            return new PurchaseOrderResult(PurchaseOrderRefusal.LineNotFound, $"No line '{lineId}' on purchase order '{orderId}'.", order);

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await order.RemoveLineAsync(lineId, line.Description, cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> IssueAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (!PurchaseOrderStatusTransitions.IsPermitted(order.Status, PurchaseOrderStatus.Issued))
            return NotPermitted(order, orderId, "It must be Draft to issue.");

        if (order.Lines.Count == 0)
            return new PurchaseOrderResult(PurchaseOrderRefusal.NothingToIssue, $"Purchase order '{orderId}' has no lines; there is nothing to issue.", order);

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await order.MarkIssuedAsync(Today(), cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> ReceiveAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (!PurchaseOrderStatusTransitions.IsPermitted(order.Status, PurchaseOrderStatus.Received))
            return NotPermitted(order, orderId, "It must be Issued to receive.");

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await order.MarkReceivedAsync(cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> CloseAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (!PurchaseOrderStatusTransitions.IsPermitted(order.Status, PurchaseOrderStatus.Closed))
            return NotPermitted(order, orderId, "It must be Received to close.");

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await order.MarkClosedAsync(cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> CancelAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (!PurchaseOrderStatusTransitions.IsPermitted(order.Status, PurchaseOrderStatus.Cancelled))
            return NotPermitted(order, orderId, "Only a Draft or Issued order can be cancelled.");

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        await order.MarkCancelledAsync(cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    /// <inheritdoc />
    public async Task<PurchaseOrderResult> RecordLinesAsExpensesAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await FindOrderAsync(orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
            return NotFound(orderId);

        if (order.Status != PurchaseOrderStatus.Received)
        {
            return new PurchaseOrderResult(
                PurchaseOrderRefusal.NotReceived, $"Purchase order '{orderId}' is {order.Status}; only a Received order's own lines can be recorded as expenses.", order);
        }

        if (order.ExpensesRecorded)
        {
            return new PurchaseOrderResult(
                PurchaseOrderRefusal.ExpensesAlreadyRecorded, $"Purchase order '{orderId}' has already had its lines recorded as expenses.", order);
        }

        if (order.ParentId is not { } projectId)
        {
            throw new InvalidOperationException(
                $"Purchase order '{orderId}' has no live project — every order is parented to the project it was raised under, so this should be unreachable.");
        }

        if (await ArchivedAsync(order, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        var today = Today();

        // One expense per line, in order — every one billable by default
        // (a received order's own cost is what this act exists to carry to
        // the invoice seam), category `Materials` as the modal case for a
        // purchase order; a consultant amends either afterwards exactly as
        // any other recorded expense (`ExpenseService.AmendAsync`).
        foreach (var line in order.Lines)
        {
            await _expenses
                .RecordAsync(projectId, today, $"{order.Reference} — {line.Description}", ExpenseCategory.Materials, line.Net, line.VatAmount, billable: true, cancellationToken)
                .ConfigureAwait(false);
        }

        await order.MarkExpensesRecordedAsync(cancellationToken).ConfigureAwait(false);

        return new PurchaseOrderResult(PurchaseOrderRefusal.None, null, order);
    }

    private static (PurchaseOrderLine? Line, PurchaseOrderRefusal Refusal, string? Reason) BuildLine(
        PurchaseOrder order, Guid lineId, string description, decimal quantity, Money unitPrice, VatRate vatRate)
    {
        var trimmedDescription = description.Trim();

        if (quantity <= 0)
            return (null, PurchaseOrderRefusal.InvalidLine, "Quantity must be greater than zero.");

        if (unitPrice.Currency != order.Currency)
        {
            return (null, PurchaseOrderRefusal.LineCurrencyMismatch,
                $"The unit price is in {unitPrice.Currency}; this purchase order is in {order.Currency}.");
        }

        if (unitPrice.IsNegative)
            return (null, PurchaseOrderRefusal.InvalidLine, "Unit price must not be below zero.");

        return (new PurchaseOrderLine(lineId, trimmedDescription, quantity, unitPrice, unitPrice * quantity, vatRate), PurchaseOrderRefusal.None, null);
    }

    private async Task<PurchaseOrderResult?> ArchivedAsync(PurchaseOrder order, CancellationToken cancellationToken)
    {
        if (order.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new PurchaseOrderResult(PurchaseOrderRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this purchase order is read-only.", order)
            : null;
    }

    private async Task<CurrencyCode> ResolveCurrencyAsync(Project project, CancellationToken cancellationToken)
    {
        if (project.RateCardPin is not { } pin)
            return CurrencyCode.Gbp;

        var card = await _rateCards.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        return card.Definition.Currency;
    }

    /// <summary>The next <c>PO-&lt;year&gt;-&lt;nnn&gt;</c> reference — one past the highest existing suffix already used for <paramref name="year"/>, among every purchase order this store holds (live or not), the identical discipline <c>QuotationService.NextReferenceAsync</c> uses.</summary>
    private async Task<string> NextReferenceAsync(int year, CancellationToken cancellationToken)
    {
        // `WP 21.5B`: `Reference` is the order's own field, not on the index row.
        var existingEntries = await _context.Repository.ListByKindAsync(PurchaseOrder.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var existing = await _context.Repository.MaterialiseAsync<PurchaseOrder>(existingEntries, cancellationToken).ConfigureAwait(false);
        var fullPrefix = $"{ReferencePrefix}{year.ToString(CultureInfo.InvariantCulture)}-";

        var max = 0;
        foreach (var candidate in existing)
        {
            if (candidate.Reference.StartsWith(fullPrefix, StringComparison.Ordinal)
                && int.TryParse(candidate.Reference.AsSpan(fullPrefix.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > max)
            {
                max = n;
            }
        }

        return $"{fullPrefix}{(max + 1).ToString("000", CultureInfo.InvariantCulture)}";
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private async Task<PurchaseOrder?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var candidate = await _context.Repository.FindAsync(orderId, cancellationToken).ConfigureAwait(false);
        return candidate is PurchaseOrder { } order && IsLive(order) ? order : null;
    }

    private static PurchaseOrderResult NotFound(Guid orderId) =>
        new(PurchaseOrderRefusal.OrderNotFound, $"No purchase order '{orderId}' is registered.", null);

    private static PurchaseOrderResult NotDraft(PurchaseOrder order, Guid orderId, string verb) =>
        new(PurchaseOrderRefusal.OrderNotDraft, $"Purchase order '{orderId}' is {order.Status}; a line can only be {verb} while Draft.", order);

    private static PurchaseOrderResult NotPermitted(PurchaseOrder order, Guid orderId, string reason) =>
        new(PurchaseOrderRefusal.TransitionNotPermitted, $"Purchase order '{orderId}' is {order.Status}; {reason}", order);

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
