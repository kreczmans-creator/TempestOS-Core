using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Timesheets;

namespace Tempest.Core.Invoicing;

/// <summary>The concrete <see cref="IInvoicingService"/> implementation (`WP 19.1A`, `ADR-0151`).</summary>
/// <remarks>
/// <para>
/// <b>The request and its line links do not commit in one transaction.</b>
/// <see cref="SendAsync"/> is, at minimum, two: one that moves
/// <see cref="InvoiceRequest"/> to <see cref="InvoiceRequestStatus.Sending"/>,
/// then — after the connector call, which is not itself a transaction —
/// one that records the outcome, and, only on a successful send, one
/// further transaction per line for <see cref="ITimesheetService.MarkInvoicedAsync"/>
/// or <see cref="IDeliverableService.MarkInvoicedAsync"/>.
/// <c>EngineeringDomainContext.ExecuteWriteAsync</c> is internal to
/// <c>Tempest.Core.EngineeringDomain</c> and folds one object's own state
/// write and its audit row into one transaction (`ADR-0145`); it is not a
/// multi-object transaction primitive, and widening it to one would be a
/// substrate change outside this Work Package's own files. The order is
/// therefore the request first, its lines second, exactly as
/// <c>WP 19.1A</c>'s own row permits — disclosed here and in
/// <c>ADR-0151</c> rather than left for a reader to discover.
/// </para>
/// <para>
/// <b>Currency is resolved from the project's own pinned rate card, not
/// from the client organisation.</b> <c>Tempest.Core.EngineeringDomain.Project</c>
/// carries no currency of its own; its pinned <c>RateCard</c> does
/// (<c>RateCard.Currency</c>), and every line on a request is already
/// priced from that same card — a timesheet entry's own frozen
/// <c>BillingRate</c> was resolved from it at record time
/// (`WP 19.0A`, `ADR-0150`), and a fixed-price deliverable completion's own
/// value is written by hand against the same project. Using the pinned
/// card's own currency, rather than <c>Organisation.TradingCurrency</c>
/// (which may be unset, or differ from what the card actually prices in),
/// is what keeps <see cref="Money.Sum(IEnumerable{Money}, CurrencyCode)"/>
/// safe to call unconditionally when totalling a request's own lines.
/// </para>
/// </remarks>
public sealed class InvoicingService : IInvoicingService
{
    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly ITimesheetService _timesheets;
    private readonly IDeliverableService _deliverables;
    private readonly IInvoicingConnector _connector;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="InvoicingService"/> class.</summary>
    public InvoicingService(
        EngineeringDomainContext context, IRateCardCatalog rateCards, ITimesheetService timesheets, IDeliverableService deliverables,
        IInvoicingConnector connector, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(timesheets);
        ArgumentNullException.ThrowIfNull(deliverables);
        ArgumentNullException.ThrowIfNull(connector);

        _context = context;
        _rateCards = rateCards;
        _timesheets = timesheets;
        _deliverables = deliverables;
        _connector = connector;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<InvoiceRequestResult> RaiseFromCompletionAsync(Guid deliverableCompletionId, CancellationToken cancellationToken = default)
    {
        var completion = await FindCompletionAsync(deliverableCompletionId, cancellationToken).ConfigureAwait(false);
        if (completion is null)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.CompletionNotFound, $"No deliverable completion '{deliverableCompletionId}' is registered.", null);
        }

        if (completion.InvoicedBy is { } existingRequestId)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.AlreadyInvoiced,
                $"Deliverable completion '{deliverableCompletionId}' is already invoiced (request '{existingRequestId:N}').",
                await FindRequestAsync(existingRequestId, cancellationToken).ConfigureAwait(false));
        }

        if (completion.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            throw new InvalidOperationException(
                $"Deliverable completion '{deliverableCompletionId}' has no live project — every completion is parented to the project it was completed under, so this should be unreachable.");
        }

        if (string.IsNullOrWhiteSpace(project.ClientOrganisationId))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.NoClient, $"Project '{projectId}' has no client recorded; an invoice cannot be raised.", null);
        }

        if (project.RateCardPin is not { } pin)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.NoRateCardPinned,
                $"Project '{projectId}' has no Released rate-card pin; an invoice request cannot be priced or currencied without one.",
                null);
        }

        var card = await _rateCards.GetRevisionAsync(pin.RecordId, pin.RevisionNumber, cancellationToken).ConfigureAwait(false);
        var currency = card.Definition.Currency;

        var unbilled = await _timesheets.ListUnbilledForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        var lines = new List<InvoiceRequestLine>(unbilled.Count + 1);

        foreach (var entry in unbilled)
        {
            var amount = entry.BillingRate * entry.Hours;
            lines.Add(new InvoiceRequestLine(TimesheetEntry.CanonicalKind, entry.Id, entry.TaskDescription, entry.Hours, entry.BillingRate, amount));
        }

        if (completion.FixedPriceValue is { } fixedPrice)
        {
            lines.Add(new InvoiceRequestLine(
                DeliverableCompletion.CanonicalKind, completion.Id, $"Deliverable completed {completion.CompletedOn:yyyy-MM-dd}",
                1m, fixedPrice, fixedPrice));
        }

        if (lines.Count == 0)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.NothingToBill,
                $"Project '{projectId}' has no unbilled time and completion '{deliverableCompletionId}' carries no fixed price; there is nothing to bill.",
                null);
        }

        var total = Money.Sum(lines.Select(l => l.Amount), currency);

        var created = await new EngineeringObjectFactory<InvoiceRequest>(
            InvoiceRequest.CanonicalKind,
            _context,
            (doc, rev) => new InvoiceRequest(
                doc, rev, _context, identifier: null, $"Invoice request — {project.DisplayName} — {_time.GetUtcNow():yyyy-MM-dd}",
                EngineeringObjectMetadata.Empty, project.ClientOrganisationId!, project.PurchaseOrderReference, currency, lines, total))
            .CreateAsync($"Invoice request raised from deliverable completion '{deliverableCompletionId}'.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, (InvoiceRequest)created);
    }

    /// <inheritdoc />
    public async Task<InvoiceRequestResult> SendAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return NotFound(requestId);

        if (!InvoiceRequestStatusTransitions.IsPermitted(request.Status, InvoiceRequestStatus.Sending))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{requestId}' is {request.Status}; it must be Draft to send.",
                request);
        }

        await request.MoveToSendingAsync(_connector.Name, cancellationToken).ConfigureAwait(false);

        var result = await _connector
            .CreateDraftInvoiceAsync(ToSnapshot(request), requestId.ToString(), cancellationToken)
            .ConfigureAwait(false);

        switch (result.Outcome)
        {
            case ConnectorOutcome.Ok:
                var invoice = result.Value!;
                await request.MarkSentAsync(invoice.ExternalId, invoice.ExternalInvoiceNumber, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
                await LinkLinesAsync(request, requestId, cancellationToken).ConfigureAwait(false);
                break;

            case ConnectorOutcome.Rejected:
                await request.MarkRejectedAsync(result.Reason ?? "Rejected by the connector.", cancellationToken).ConfigureAwait(false);
                break;

            case ConnectorOutcome.Reauthorise:
                await request.MarkReauthoriseAsync(result.Reason, cancellationToken).ConfigureAwait(false);
                break;

            case ConnectorOutcome.Unavailable:
                // Stays Draft, deliberately never a stored `Unavailable`
                // status — InvoiceRequestStatus.Unavailable's own remarks,
                // and not retried automatically (the row's own words).
                await request.RevertToDraftAsync(result.Reason ?? "The connector could not be reached.", cancellationToken).ConfigureAwait(false);
                break;

            case ConnectorOutcome.Unknown:
            default:
                await request.MarkUnknownAsync(result.Reason ?? "The connector's own response was lost.", cancellationToken).ConfigureAwait(false);
                break;
        }

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);
    }

    /// <inheritdoc />
    public async Task<InvoiceRequestResult> ReconcileAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return NotFound(requestId);

        switch (request.Status)
        {
            case InvoiceRequestStatus.Unknown:
                await ReconcileUnknownAsync(request, requestId, cancellationToken).ConfigureAwait(false);
                break;

            case InvoiceRequestStatus.Sent:
            case InvoiceRequestStatus.Accepted:
                await ReconcileSentOrAcceptedAsync(request, cancellationToken).ConfigureAwait(false);
                break;

            default:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{requestId}' is {request.Status}; there is nothing to reconcile.",
                    request);
        }

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);
    }

    /// <inheritdoc />
    public async Task<InvoiceRequestResult> VoidAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return NotFound(requestId);

        if (request.Status is not (InvoiceRequestStatus.Draft or InvoiceRequestStatus.Rejected))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{requestId}' is {request.Status}; only a Draft or Rejected request can be voided locally. "
                + "A request that reached the provider is voided there, and read back through reconciliation.",
                request);
        }

        await request.VoidLocallyAsync(cancellationToken).ConfigureAwait(false);

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);
    }

    private async Task ReconcileUnknownAsync(InvoiceRequest request, Guid requestId, CancellationToken cancellationToken)
    {
        var found = await _connector.FindByReferenceAsync(requestId.ToString(), cancellationToken).ConfigureAwait(false);

        if (found.Outcome != ConnectorOutcome.Ok)
            return; // Connector still unreachable or the call itself was refused; nothing to report — the poller tries again next tick.

        if (found.Value is { } invoice)
        {
            await request.ReconcileFoundAsync(invoice.ExternalId, invoice.ExternalInvoiceNumber, cancellationToken).ConfigureAwait(false);
            await LinkLinesAsync(request, requestId, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await request.RevertToDraftAsync("Not found by reference on reconciliation.", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ReconcileSentOrAcceptedAsync(InvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.ExternalId is not { } externalId)
            return; // Defensive: a Sent/Accepted request always carries one; nothing to read without it.

        var reading = await _connector.ReadStatusAsync(externalId, cancellationToken).ConfigureAwait(false);

        if (reading.Outcome != ConnectorOutcome.Ok || reading.Value is not { } statusReading)
            return; // Connector unreachable, or the call was refused; nothing to report — the poller tries again next tick.

        var interpreted = InterpretStatus(statusReading.ExternalStatus, request.Status);

        // Interpretation only ever proposes a move the table itself
        // permits (Sent -> Accepted/Voided, Accepted -> Voided) or no move
        // at all; this guard is defence in depth, not a codepath any
        // fixture reaches.
        var newStatus = interpreted == request.Status || InvoiceRequestStatusTransitions.IsPermitted(request.Status, interpreted)
            ? interpreted
            : request.Status;

        await request.RecordStatusReadingAsync(
            newStatus, statusReading.ExternalStatus, statusReading.ExternalInvoiceNumber, statusReading.IssuedDate, statusReading.PaidDate,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task LinkLinesAsync(InvoiceRequest request, Guid requestId, CancellationToken cancellationToken)
    {
        foreach (var line in request.Lines)
        {
            if (string.Equals(line.SourceKind, TimesheetEntry.CanonicalKind, StringComparison.Ordinal))
                await _timesheets.MarkInvoicedAsync(line.SourceId, requestId, cancellationToken).ConfigureAwait(false);
            else if (string.Equals(line.SourceKind, DeliverableCompletion.CanonicalKind, StringComparison.Ordinal))
                await _deliverables.MarkInvoicedAsync(line.SourceId, requestId, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<DeliverableCompletion?> FindCompletionAsync(Guid completionId, CancellationToken cancellationToken)
    {
        var candidate = await _context.Repository.FindAsync(completionId, cancellationToken).ConfigureAwait(false);
        return candidate is DeliverableCompletion { } completion && IsLive(completion) ? completion : null;
    }

    private async Task<InvoiceRequest?> FindRequestAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var candidate = await _context.Repository.FindAsync(requestId, cancellationToken).ConfigureAwait(false);
        return candidate is InvoiceRequest { } request && IsLive(request) ? request : null;
    }

    private static InvoiceRequestResult NotFound(Guid requestId) =>
        new(InvoiceRequestRefusal.RequestNotFound, $"No invoice request '{requestId}' is registered.", null);

    private static InvoiceRequestSnapshot ToSnapshot(InvoiceRequest request) =>
        new(request.Id, request.ClientOrganisationId, request.PurchaseOrderReference, request.Currency, request.Lines, request.Total);

    /// <summary>
    /// Maps a connector's own free-form status word to what it means for
    /// this Kind's own lifecycle — the one place that decision is made
    /// (<see cref="IInvoicingConnector.ReadStatusAsync"/>'s own remarks:
    /// a connector never maps its own words itself). Matches the
    /// vocabulary real accounting systems actually use (Xero: <c>DRAFT</c>,
    /// <c>SUBMITTED</c>, <c>AUTHORISED</c>, <c>PAID</c>, <c>VOIDED</c>) case-
    /// insensitively and by substring, so a provider-specific decoration
    /// around the same word still matches. Anything unrecognised leaves
    /// <paramref name="current"/> unchanged rather than guessing.
    /// </summary>
    private static InvoiceRequestStatus InterpretStatus(string externalStatus, InvoiceRequestStatus current)
    {
        if (externalStatus.Contains("VOID", StringComparison.OrdinalIgnoreCase))
            return InvoiceRequestStatus.Voided;

        if (externalStatus.Contains("AUTHORIS", StringComparison.OrdinalIgnoreCase)
            || externalStatus.Contains("APPROV", StringComparison.OrdinalIgnoreCase)
            || externalStatus.Contains("PAID", StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceRequestStatus.Accepted;
        }

        return current;
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
