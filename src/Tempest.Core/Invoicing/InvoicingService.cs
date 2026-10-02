using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Expenses;
using Tempest.Core.Projects;
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
    /// <summary>The <see cref="Tempest.Core.Configuration.IConfigurationProvider"/> key naming which connector binds to <see cref="IInvoicingConnector"/> — <c>"Fake"</c> (default), <c>"Xero"</c>, <c>"QuickBooksOnline"</c>. Read by <see cref="Tempest.Core.Runtime.TempestHost"/>'s own composition, not by this class: parts 2 and 3 of this Work Package replace the binding the key selects, never this seam.</summary>
    public const string ConnectorConfigurationKey = "Invoicing:Connector";

    private readonly EngineeringDomainContext _context;
    private readonly IRateCardCatalog _rateCards;
    private readonly ITimesheetService _timesheets;
    private readonly IDeliverableService _deliverables;
    private readonly IInvoicingConnector _connector;
    private readonly IOrganisationCatalog _organisations;
    private readonly IExpenseService? _expenses;
    private readonly TimeProvider _time;
    private readonly IInvoiceDraftSync? _drafts;

    /// <summary>Initialises a new instance of the <see cref="InvoicingService"/> class.</summary>
    /// <param name="expenses">
    /// Where a billable, unbilled <c>ProjectExpense</c> is read from so it
    /// can join the timesheet lines a raised request already carries (`WP
    /// 21.3B`). <see langword="null"/> — honoured, not required — leaves
    /// expenses out of every raised request, for a caller (an older test
    /// host) that has not composed the Expenses discipline; every
    /// production composition root supplies it.
    /// </param>
    /// <param name="draftSync">
    /// `v0.24.0` X4: the numbered-draft seam of the connector named
    /// <see cref="IInvoiceDraftSync.ConnectorName"/> (Xero's is
    /// <c>Tempest.Core.Invoicing.Xero.Sync.Invoices.XeroInvoiceDrafts</c>,
    /// registered only when Xero is the connector). When it names
    /// <paramref name="connector"/>, a send creates the draft with
    /// TempestOS's own invoice number and the client's linked contact,
    /// reconciles a lost response by that number, and a revision or void
    /// of a sent request changes the draft only while it is still a draft.
    /// <see langword="null"/> — every other connector — keeps the `WP
    /// 19.1A` behaviour unchanged.
    /// </param>
    public InvoicingService(
        EngineeringDomainContext context, IRateCardCatalog rateCards, ITimesheetService timesheets, IDeliverableService deliverables,
        IInvoicingConnector connector, IOrganisationCatalog organisations, TimeProvider? timeProvider = null,
        IExpenseService? expenses = null, IInvoiceDraftSync? draftSync = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rateCards);
        ArgumentNullException.ThrowIfNull(timesheets);
        ArgumentNullException.ThrowIfNull(deliverables);
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(organisations);

        _context = context;
        _rateCards = rateCards;
        _timesheets = timesheets;
        _deliverables = deliverables;
        _connector = connector;
        _organisations = organisations;
        _time = timeProvider ?? TimeProvider.System;
        _expenses = expenses;
        _drafts = draftSync;
    }

    /// <summary>The numbered-draft seam, when it belongs to this service's own connector; <see langword="null"/> otherwise.</summary>
    private IInvoiceDraftSync? Drafts =>
        _drafts is not null && string.Equals(_drafts.ConnectorName, _connector.Name, StringComparison.Ordinal) ? _drafts : null;

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

        // A source is billed on at most one live request. `InvoicedBy` is
        // written only once a request reaches Sent (`ADR-0151` §5), so
        // before that the completion hook's own request (§7) and a Raise
        // invoice on the same completion from the Deliverables tab used to
        // raise two Drafts carrying the same lines — the v0.19.0 Desktop
        // journey found the second one, one run in two. A completion a
        // live request already carries is refused naming that request, as
        // an already-sent one is above; a timesheet entry a live request
        // already carries is left off, and a Rejected or Voided request
        // frees its lines.
        var carriedBy = await ListCarriedSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (carriedBy.TryGetValue(completion.Id, out var carrier))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.AlreadyInvoiced,
                $"Deliverable completion '{deliverableCompletionId}' is already invoiced by request '{carrier.Id:N}' ({carrier.Status}); send or void that request rather than raising a second.",
                carrier);
        }

        if (completion.ParentId is not { } projectId)
        {
            throw new InvalidOperationException(
                $"Deliverable completion '{deliverableCompletionId}' has no live project — every completion is parented to the project it was completed under, so this should be unreachable.");
        }

        return await RaiseAsync(
            projectId, completion, carriedBy, $"deliverable completion '{deliverableCompletionId}'",
            $"Project '{projectId}' has no unbilled time and completion '{deliverableCompletionId}' carries no fixed price; there is nothing to bill.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<InvoiceRequestResult> RaiseFromExpenseAsync(Guid expenseId, CancellationToken cancellationToken = default)
    {
        if (_expenses is null || await _context.Repository.FindAsync(expenseId, cancellationToken).ConfigureAwait(false) is not ProjectExpense expense || !IsLive(expense))
            return new InvoiceRequestResult(InvoiceRequestRefusal.ExpenseNotFound, $"No expense '{expenseId}' is registered.", null);

        if (expense.InvoicedBy is { } existingRequestId)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.AlreadyInvoiced,
                $"Expense '{expenseId}' is already invoiced (request '{existingRequestId:N}').",
                await FindRequestAsync(existingRequestId, cancellationToken).ConfigureAwait(false));
        }

        // `WP 21.3B`: the identical "a source is billed on at most one live
        // request" carried-by check `RaiseFromCompletionAsync` applies —
        // this is the entry point for a project whose only unbilled work,
        // right now, is an expense (no completion to raise from at all).
        var carriedBy = await ListCarriedSourcesAsync(cancellationToken).ConfigureAwait(false);
        if (carriedBy.TryGetValue(expense.Id, out var carrier))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.AlreadyInvoiced,
                $"Expense '{expenseId}' is already invoiced by request '{carrier.Id:N}' ({carrier.Status}); send or void that request rather than raising a second.",
                carrier);
        }

        return await RaiseAsync(
            expense.ProjectId, completion: null, carriedBy, $"expense '{expenseId}'",
            $"Project '{expense.ProjectId}' has no unbilled time or expenses; there is nothing to bill.",
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The shared body of <see cref="RaiseFromCompletionAsync"/> and
    /// <see cref="RaiseFromExpenseAsync"/> (`WP 21.3B`): resolve the
    /// project's own client, rate card and payment terms; gather every
    /// unbilled, uncarried timesheet entry and billable expense for the
    /// project; add <paramref name="completion"/>'s own fixed-price line
    /// when one is given; and raise the request, or refuse
    /// <see cref="InvoiceRequestRefusal.NothingToBill"/> with
    /// <paramref name="nothingToBillReason"/> when no line resulted.
    /// </summary>
    private async Task<InvoiceRequestResult> RaiseAsync(
        Guid projectId, DeliverableCompletion? completion, Dictionary<Guid, InvoiceRequest> carriedBy, string raisedFromDescription,
        string nothingToBillReason, CancellationToken cancellationToken)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            throw new InvalidOperationException(
                $"Project '{projectId}' is not a live project — every completion and expense is parented to the project it belongs to, so this should be unreachable.");
        }

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); no new invoice request can be raised against it.", null);
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

        // `TD-180`: the client's own standing terms, copied onto the
        // request at the moment it is raised and frozen there from then on
        // (`InvoiceRequest.PaymentTerms`'s own remarks) — an unresolved
        // client id (never validated as a real record, this class's own
        // remarks) reads as `PaymentTerms.UpFront`, same as a client that
        // has simply never set anything else.
        var client = await _organisations.FindAsync(project.ClientOrganisationId!, cancellationToken).ConfigureAwait(false);
        var paymentTerms = client?.Definition.PaymentTerms ?? PaymentTerms.UpFront;

        var unbilled = await _timesheets.ListUnbilledForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

        var lines = new List<InvoiceRequestLine>(unbilled.Count + 1);

        InvoiceRequest? carrierOfOther = null;
        foreach (var entry in unbilled)
        {
            if (carriedBy.TryGetValue(entry.Id, out var carrierOfEntry))
            {
                carrierOfOther ??= carrierOfEntry;
                continue;
            }

            var amount = entry.BillingRate * entry.Hours;
            lines.Add(new InvoiceRequestLine(TimesheetEntry.CanonicalKind, entry.Id, entry.TaskDescription, entry.Hours, entry.BillingRate, amount));
        }

        if (completion?.FixedPriceValue is { } fixedPrice)
        {
            lines.Add(new InvoiceRequestLine(
                DeliverableCompletion.CanonicalKind, completion.Id, $"Deliverable completed {completion.CompletedOn:yyyy-MM-dd}",
                1m, fixedPrice, fixedPrice));
        }

        // `WP 21.3B`: a billable, unbilled expense joins the request
        // exactly as an unbilled timesheet entry does above — the same
        // "a source is billed on at most one live request" carried-by
        // check, and the same `InvoicedBy` link once the request reaches
        // Sent (`LinkLinesAsync`, below).
        if (_expenses is not null)
        {
            var unbilledExpenses = await _expenses.ListUnbilledForProjectAsync(projectId, cancellationToken).ConfigureAwait(false);

            foreach (var expense in unbilledExpenses)
            {
                if (carriedBy.TryGetValue(expense.Id, out var carrierOfExpense))
                {
                    carrierOfOther ??= carrierOfExpense;
                    continue;
                }

                lines.Add(new InvoiceRequestLine(
                    ProjectExpense.CanonicalKind, expense.Id, expense.Description, 1m, expense.NetAmount, expense.NetAmount,
                    InferVatRate(expense.NetAmount, expense.VatAmount)));
            }
        }

        if (lines.Count == 0)
        {
            if (carrierOfOther is not null)
            {
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.NothingToBill,
                    $"Everything billable for {raisedFromDescription} is already on request '{carrierOfOther.Id:N}' ({carrierOfOther.Status}); send or void that request rather than raising a second.",
                    carrierOfOther);
            }

            return new InvoiceRequestResult(InvoiceRequestRefusal.NothingToBill, nothingToBillReason, null);
        }

        var total = Money.Sum(lines.Select(l => l.Amount), currency);
        var identifier = await NextIdentifierAsync(project, cancellationToken).ConfigureAwait(false);

        var created = await new EngineeringObjectFactory<InvoiceRequest>(
            InvoiceRequest.CanonicalKind,
            _context,
            (doc, rev) => new InvoiceRequest(
                doc, rev, _context, identifier, $"Invoice request — {project.DisplayName} — {_time.GetUtcNow():yyyy-MM-dd}",
                EngineeringObjectMetadata.Empty, project.ClientOrganisationId!, project.PurchaseOrderReference, currency, lines, total,
                paymentTerms: paymentTerms))
            .CreateAsync($"Invoice request raised from {raisedFromDescription}.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, (InvoiceRequest)created);
    }

    /// <summary>
    /// The identifier a new invoice request raised against
    /// <paramref name="project"/> carries: <c>CUSTOMER-PROJECTREF-INV-NNN</c>
    /// where the project's own identifier is project-centric (Product Owner
    /// decision 2026-10-01 §3, `ADR-0156` — one past the highest suffix any
    /// request, live or not, already used under that prefix, so a sequence
    /// per project starting at 001), otherwise <see langword="null"/> — the
    /// request is identified by its own id exactly as before this decision.
    /// </summary>
    private async Task<string?> NextIdentifierAsync(Project project, CancellationToken cancellationToken)
    {
        if (!ProjectNumbering.TryGetDocumentPrefix(project.Identifier, ProjectNumbering.InvoiceRequest, out var prefix))
            return null;

        var entries = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var existing = await _context.Repository.MaterialiseAsync<InvoiceRequest>(entries, cancellationToken).ConfigureAwait(false);

        return ProjectNumbering.NextNumber(prefix, existing.Select(r => r.Identifier));
    }

    /// <inheritdoc />
    public Task<InvoiceRequestResult> SendAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        SendAsync(requestId, queueWhenUnavailable: true, cancellationToken);

    /// <summary>
    /// <see cref="SendAsync(Guid, CancellationToken)"/>, with the choice of
    /// whether an unreachable connector re-queues the send (`v0.24.0` X4,
    /// design §4.2: "request back to Draft and PushInvoiceDraft queued").
    /// The outbox's own push handler sends with
    /// <paramref name="queueWhenUnavailable"/> <see langword="false"/> — its
    /// own entry is already the queued retry.
    /// </summary>
    /// <param name="requestId">The request to send.</param>
    /// <param name="queueWhenUnavailable">Whether an unreachable connector queues a retry through <see cref="IInvoiceDraftSync.QueueSendAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the act.</param>
    internal async Task<InvoiceRequestResult> SendAsync(Guid requestId, bool queueWhenUnavailable, CancellationToken cancellationToken)
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

        if (await ArchivedAsync(request, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        var drafts = Drafts;
        InvoiceDraftDocument? document = null;
        ConnectorResult<CreatedInvoice> result;

        if (drafts is not null)
        {
            // `v0.24.0` X4: everything the draft needs that TempestOS holds
            // locally — the client's contact link (X2), a tax type and the
            // sales account for every line (X1) — is checked before the
            // request ever moves to Sending, so an unlinked client is
            // refused with the reason rather than sent half-formed.
            document = await ToDraftDocumentAsync(request, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            if (await drafts.FindBlockingReasonAsync(document, cancellationToken).ConfigureAwait(false) is { } blocked)
            {
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{requestId}' cannot be sent to {drafts.ConnectorName} yet: {blocked}",
                    request);
            }

            await request.MoveToSendingAsync(_connector.Name, document.Reference, cancellationToken).ConfigureAwait(false);
            result = await drafts.CreateDraftAsync(document, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await request.MoveToSendingAsync(_connector.Name, cancellationToken: cancellationToken).ConfigureAwait(false);

            var snapshot = await ToSnapshotAsync(request, cancellationToken).ConfigureAwait(false);

            result = await _connector
                .CreateDraftInvoiceAsync(snapshot, requestId.ToString(), cancellationToken)
                .ConfigureAwait(false);
        }

        switch (result.Outcome)
        {
            case ConnectorOutcome.Ok:
                var invoice = result.Value!;
                await request.MarkSentAsync(
                    invoice.ExternalId, invoice.ExternalInvoiceNumber, _time.GetUtcNow(), drafts?.CreatedStatus, cancellationToken).ConfigureAwait(false);
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
                // status — InvoiceRequestStatus.Unavailable's own remarks.
                // `v0.24.0` X4: where the connector has a draft seam, the
                // send is queued again (design §4.2) rather than left for
                // someone to notice; any other connector is not retried
                // automatically (the `WP 19.1A` row's own words).
                await request.RevertToDraftAsync(result.Reason ?? "The connector could not be reached.", cancellationToken).ConfigureAwait(false);
                if (drafts is not null && document is not null && queueWhenUnavailable)
                    await drafts.QueueSendAsync(document, cancellationToken).ConfigureAwait(false);
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

        if (await ArchivedAsync(request, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

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
    /// <remarks>
    /// `v0.24.0` X4: a <see cref="InvoiceRequestStatus.Sent"/> request whose
    /// connector has a draft seam (<see cref="IInvoiceDraftSync"/>, Xero) may
    /// also be voided — by deleting its draft there, and only while the
    /// accounting system still holds it as a draft (design §4.2). Once it is
    /// approved there it is voided there instead, and read back.
    /// </remarks>
    public async Task<InvoiceRequestResult> VoidAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return NotFound(requestId);

        if (request.Status is InvoiceRequestStatus.Sent && Drafts is { } drafts && IsThroughDrafts(request, drafts))
            return await VoidSentDraftAsync(request, drafts, cancellationToken).ConfigureAwait(false);

        if (request.Status is not (InvoiceRequestStatus.Draft or InvoiceRequestStatus.Rejected))
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{requestId}' is {request.Status}; only a Draft or Rejected request can be voided locally. "
                + "A request that reached the provider is voided there, and read back through reconciliation.",
                request);
        }

        if (await ArchivedAsync(request, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        // `v0.24.0` X4: a Draft or Rejected request a send through the draft
        // seam was attempted for may already be in the accounting system —
        // the answer to a create it committed can be lost (a dropped
        // connection reads as unreachable, so the request went back to
        // Draft with no link). Before voiding it locally, which frees its
        // lines, the system is asked for its number, and TempestOS's own
        // draft found there is deleted; when that cannot be done now the
        // void is refused, never left to leave an untracked draft behind.
        if (Drafts is { } seam && MaySitInDrafts(request, seam)
            && await ClearCutOffSendAsync(request, seam, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        if (request.Status is InvoiceRequestStatus.Voided)
            return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);

        await request.VoidLocallyAsync(cancellationToken).ConfigureAwait(false);

        return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);
    }

    /// <summary>Whether <paramref name="request"/>, not linked to anything, had a send through <paramref name="drafts"/>'s connector attempted — so its draft may be there, unlinked, after a lost answer.</summary>
    private static bool MaySitInDrafts(InvoiceRequest request, IInvoiceDraftSync drafts) =>
        request.ExternalId is null && string.Equals(request.Connector, drafts.ConnectorName, StringComparison.Ordinal);

    /// <summary>
    /// `v0.24.0` X4 (defect: lost create answer, then void): looks
    /// <paramref name="request"/>'s number up through <paramref name="drafts"/>
    /// and deletes TempestOS's own draft found under it. Answers
    /// <see langword="null"/> when nothing of TempestOS's is left there and
    /// the local void may go ahead (or, for a draft already deleted or
    /// voided there, has been recorded); otherwise the refusal, with the
    /// request unchanged — or, for an invoice already approved there, now
    /// tracked as Sent so the approval is read back.
    /// </summary>
    private async Task<InvoiceRequestResult?> ClearCutOffSendAsync(InvoiceRequest request, IInvoiceDraftSync drafts, CancellationToken cancellationToken)
    {
        var document = await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var found = await drafts.FindNumberHolderAsync(document, cancellationToken).ConfigureAwait(false);

        if (found.Outcome != ConnectorOutcome.Ok)
        {
            // Unreachable, unauthorised, or the look-up itself refused (a 400
            // or 404): who holds the number is not known, so nothing is
            // voided — a void that frees the lines over a live draft would
            // let a second invoice be raised for the same work.
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{request.Id}' was not voided: its last send to {drafts.ConnectorName} was cut off, and {drafts.ConnectorName} could not be asked whether it holds the draft ({found.Reason ?? found.Outcome.ToString()}); the request is unchanged — try again.",
                request);
        }

        var finding = found.Value!;
        switch (finding.Holder)
        {
            case InvoiceNumberHolder.Nobody:
            case InvoiceNumberHolder.AnotherInvoice:
                return null; // Nothing carrying its number there is TempestOS's own: the void is local only.

            case InvoiceNumberHolder.OwnReferenceOtherContact:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{request.Id}' was not voided: {drafts.ConnectorName} holds {document.InvoiceNumber} with this invoice's reference under another contact; check {drafts.ConnectorName}. The request is unchanged.",
                    request);

            case InvoiceNumberHolder.Own when finding.Invoice is not null:
                break;

            default:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{request.Id}' was not voided: {drafts.ConnectorName}'s answer about {document.InvoiceNumber} was not understood; the request is unchanged.",
                    request);
        }

        var own = finding.Invoice;
        var change = await drafts.DeleteDraftAsync(document with { ExternalId = own.ExternalId }, cancellationToken).ConfigureAwait(false);
        if (change.Outcome != ConnectorOutcome.Ok)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{request.Id}' was not voided: {drafts.ConnectorName} holds its draft {own.ExternalInvoiceNumber ?? document.InvoiceNumber} from a send whose answer was lost, and it could not be deleted ({change.Reason ?? change.Outcome.ToString()}); the request is unchanged — try again.",
                request);
        }

        var answer = change.Value!;
        switch (answer.Outcome)
        {
            case InvoiceDraftChangeOutcome.Applied:
                await request.RecordStatusReadingAsync(
                    InvoiceRequestStatus.Voided, answer.ExternalStatus ?? "DELETED", own.ExternalInvoiceNumber, null, null,
                    $"Voided in TempestOS; the {drafts.ConnectorName} draft a lost send had created was deleted.", cancellationToken).ConfigureAwait(false);
                return null;

            case InvoiceDraftChangeOutcome.NotDraft when IsGoneThere(answer.ExternalStatus):
                await request.RecordStatusReadingAsync(
                    InvoiceRequestStatus.Voided, answer.ExternalStatus!, own.ExternalInvoiceNumber, null, null,
                    $"Already {answer.ExternalStatus!.ToLowerInvariant()} in {drafts.ConnectorName}.", cancellationToken).ConfigureAwait(false);
                return null;

            case InvoiceDraftChangeOutcome.NotDraft:
                // Approved there already: it is a real invoice TempestOS sent.
                // Track it as Sent (its lines are billed) so reconciliation
                // reads the approval back; voiding it is done in the system.
                await request.ReconcileFoundAsync(own.ExternalId, own.ExternalInvoiceNumber, cancellationToken).ConfigureAwait(false);
                await LinkLinesAsync(request, request.Id, cancellationToken).ConfigureAwait(false);
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"{drafts.ConnectorName} already holds this invoice as {answer.ExternalStatus} (a send whose answer was lost); it is now tracked as Sent — void it in {drafts.ConnectorName}, and TempestOS reads it back.",
                    request);

            default:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{request.Id}' was not voided: {answer.Reason}",
                    request);
        }
    }

    /// <summary>
    /// Revises the lines of <paramref name="requestId"/>'s own request
    /// (`v0.24.0` X4): each <see cref="InvoiceRequestLineRevision"/> names a
    /// line by its source and gives its new description, quantity, unit
    /// rate and VAT rate; lines not named are unchanged, and no source is
    /// ever added or dropped. A <see cref="InvoiceRequestStatus.Draft"/> or
    /// <see cref="InvoiceRequestStatus.Rejected"/> request is revised
    /// locally. A <see cref="InvoiceRequestStatus.Sent"/> request is revised
    /// only through its connector's draft seam, and only while the
    /// accounting system still holds the invoice as a draft — otherwise the
    /// revision is refused with the reason (<i>"Xero holds it as AUTHORISED;
    /// change it in Xero"</i>), and the request is left exactly as it was.
    /// </summary>
    /// <param name="requestId">The request to revise.</param>
    /// <param name="revisions">The line revisions (at least one; each source once).</param>
    /// <param name="cancellationToken">Cancels the act.</param>
    /// <returns>The revised request, or a refusal saying why it was not revised.</returns>
    public async Task<InvoiceRequestResult> ReviseLinesAsync(
        Guid requestId, IReadOnlyList<InvoiceRequestLineRevision> revisions, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(revisions);

        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return NotFound(requestId);

        if (await ArchivedAsync(request, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        if (ApplyRevisions(request, revisions, out var lines, out var problem) is false)
            return new InvoiceRequestResult(InvoiceRequestRefusal.TransitionNotPermitted, $"Invoice request '{requestId}' was not revised: {problem}", request);

        var total = Money.Sum(lines.Select(l => l.Amount), request.Currency);

        if (request.Status is InvoiceRequestStatus.Draft or InvoiceRequestStatus.Rejected)
        {
            await request.ReviseLinesAsync(lines, total, "Lines revised.", cancellationToken).ConfigureAwait(false);
            return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);
        }

        if (request.Status is InvoiceRequestStatus.Sent && Drafts is { } drafts && IsThroughDrafts(request, drafts))
        {
            var document = await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false) with
            {
                Lines = lines,
                Total = total,
            };

            var change = await drafts.UpdateDraftAsync(document, cancellationToken).ConfigureAwait(false);
            if (change.Outcome != ConnectorOutcome.Ok)
            {
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{requestId}' was not revised: {drafts.ConnectorName} did not take the change ({change.Reason ?? change.Outcome.ToString()}); the request is unchanged — try again.",
                    request);
            }

            var answer = change.Value!;
            switch (answer.Outcome)
            {
                case InvoiceDraftChangeOutcome.Applied:
                    await request.ReviseLinesAsync(lines, total, $"Lines revised; {drafts.ConnectorName}'s draft updated to match.", cancellationToken).ConfigureAwait(false);
                    return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);

                case InvoiceDraftChangeOutcome.NotDraft:
                    return new InvoiceRequestResult(
                        InvoiceRequestRefusal.TransitionNotPermitted,
                        $"{drafts.ConnectorName} holds it as {answer.ExternalStatus}; change it in {drafts.ConnectorName}.",
                        request);

                default:
                    return new InvoiceRequestResult(
                        InvoiceRequestRefusal.TransitionNotPermitted,
                        $"Invoice request '{requestId}' was not revised: {answer.Reason}",
                        request);
            }
        }

        return new InvoiceRequestResult(
            InvoiceRequestRefusal.TransitionNotPermitted,
            request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted
                ? $"Invoice request '{requestId}' is {request.Status} through {request.Connector}; change it there."
                : $"Invoice request '{requestId}' is {request.Status}; only a Draft or Rejected request, or a Sent one its accounting system still holds as a draft, can be revised.",
            request);
    }

    /// <summary>Whether <paramref name="request"/> went out through <paramref name="drafts"/>'s own connector and is known there.</summary>
    private static bool IsThroughDrafts(InvoiceRequest request, IInvoiceDraftSync drafts) =>
        request.ExternalId is not null && string.Equals(request.Connector, drafts.ConnectorName, StringComparison.Ordinal);

    /// <summary>`v0.24.0` X4: voids a Sent request by deleting its draft in the accounting system — only while it is still a draft there.</summary>
    private async Task<InvoiceRequestResult> VoidSentDraftAsync(InvoiceRequest request, IInvoiceDraftSync drafts, CancellationToken cancellationToken)
    {
        if (await ArchivedAsync(request, cancellationToken).ConfigureAwait(false) is { } archived)
            return archived;

        var document = await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var change = await drafts.DeleteDraftAsync(document, cancellationToken).ConfigureAwait(false);

        if (change.Outcome != ConnectorOutcome.Ok)
        {
            return new InvoiceRequestResult(
                InvoiceRequestRefusal.TransitionNotPermitted,
                $"Invoice request '{request.Id}' was not voided: its {drafts.ConnectorName} draft could not be deleted ({change.Reason ?? change.Outcome.ToString()}); the request stays Sent — try again.",
                request);
        }

        var answer = change.Value!;
        switch (answer.Outcome)
        {
            case InvoiceDraftChangeOutcome.Applied:
                await request.RecordStatusReadingAsync(
                    InvoiceRequestStatus.Voided, answer.ExternalStatus ?? "DELETED", null, null, null,
                    $"Voided in TempestOS; its {drafts.ConnectorName} draft was deleted.", cancellationToken).ConfigureAwait(false);
                return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);

            case InvoiceDraftChangeOutcome.NotDraft when IsGoneThere(answer.ExternalStatus):
                // Already deleted or voided there: read back, as reconciliation would.
                await request.RecordStatusReadingAsync(
                    InvoiceRequestStatus.Voided, answer.ExternalStatus!, null, null, null,
                    $"Already {answer.ExternalStatus!.ToLowerInvariant()} in {drafts.ConnectorName}.", cancellationToken).ConfigureAwait(false);
                return new InvoiceRequestResult(InvoiceRequestRefusal.None, null, request);

            case InvoiceDraftChangeOutcome.NotDraft:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"{drafts.ConnectorName} holds it as {answer.ExternalStatus}; void it in {drafts.ConnectorName} — TempestOS reads it back.",
                    request);

            default:
                return new InvoiceRequestResult(
                    InvoiceRequestRefusal.TransitionNotPermitted,
                    $"Invoice request '{request.Id}' was not voided: {answer.Reason}",
                    request);
        }
    }

    private static bool IsGoneThere(string? externalStatus) =>
        externalStatus is not null
        && (externalStatus.Contains("DELETE", StringComparison.OrdinalIgnoreCase) || externalStatus.Contains("VOID", StringComparison.OrdinalIgnoreCase));

    /// <summary>Applies <paramref name="revisions"/> to <paramref name="request"/>'s lines, or says why they cannot be applied.</summary>
    private static bool ApplyRevisions(
        InvoiceRequest request, IReadOnlyList<InvoiceRequestLineRevision> revisions, out List<InvoiceRequestLine> lines, out string? problem)
    {
        lines = [.. request.Lines];
        problem = null;

        if (revisions.Count == 0)
        {
            problem = "no line revision was given.";
            return false;
        }

        var seen = new HashSet<Guid>();
        foreach (var revision in revisions)
        {
            if (revision is null)
            {
                problem = "a line revision is missing.";
                return false;
            }

            if (!seen.Add(revision.SourceId))
            {
                problem = $"source '{revision.SourceId}' is revised twice.";
                return false;
            }

            var index = lines.FindIndex(l => l.SourceId == revision.SourceId);
            if (index < 0)
            {
                problem = $"it carries no line for source '{revision.SourceId}'; a revision never adds a line.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(revision.Description))
            {
                problem = $"the line for source '{revision.SourceId}' needs a description.";
                return false;
            }

            if (revision.Quantity <= 0m)
            {
                problem = $"the line for source '{revision.SourceId}' needs a quantity greater than zero.";
                return false;
            }

            if (revision.UnitRate.Currency != request.Currency)
            {
                problem = $"the line for source '{revision.SourceId}' is priced in {revision.UnitRate.Currency}, not the request's {request.Currency}.";
                return false;
            }

            if (!Enum.IsDefined(revision.VatRate))
            {
                problem = $"the line for source '{revision.SourceId}' has no recognised VAT rate.";
                return false;
            }

            var line = lines[index];
            lines[index] = line with
            {
                Description = revision.Description.Trim(),
                Quantity = revision.Quantity,
                UnitRate = revision.UnitRate,
                Amount = (revision.UnitRate * revision.Quantity).RoundTo(2),
                VatRate = revision.VatRate,
            };
        }

        return true;
    }

    private async Task ReconcileUnknownAsync(InvoiceRequest request, Guid requestId, CancellationToken cancellationToken)
    {
        // `v0.24.0` X4 (design §6.4 item 5): by the request's own invoice
        // number first; only when nothing carries it, by the reference a
        // pre-v0.24 send wrote (the request id).
        if (Drafts is { } drafts)
        {
            var document = await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            var byNumber = await drafts.FindByInvoiceNumberAsync(document, cancellationToken).ConfigureAwait(false);

            if (byNumber.Outcome != ConnectorOutcome.Ok)
                return; // Unreachable, or the number is held by another invoice: nothing safe to conclude; the reason is left for the next attempt.

            if (byNumber.Value is { } numbered)
            {
                await request.ReconcileFoundAsync(numbered.ExternalId, numbered.ExternalInvoiceNumber, cancellationToken).ConfigureAwait(false);
                await LinkLinesAsync(request, requestId, cancellationToken).ConfigureAwait(false);
                return;
            }
        }

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

        // `v0.24.0` X4 (design §4.2): a draft is not issued — its date is
        // only the date it was raised as a draft — so the issued date is
        // read once the invoice is approved (Accepted: AUTHORISED or PAID),
        // never from a draft awaiting the Product Owner or one deleted.
        var issuedDate = newStatus == InvoiceRequestStatus.Accepted ? statusReading.IssuedDate : null;
        var note = newStatus == InvoiceRequestStatus.Voided && request.Status != InvoiceRequestStatus.Voided
            && statusReading.ExternalStatus.Contains("DELETE", StringComparison.OrdinalIgnoreCase)
            ? $"Deleted in {request.Connector ?? _connector.Name}."
            : null;

        await request.RecordStatusReadingAsync(
            newStatus, statusReading.ExternalStatus, statusReading.ExternalInvoiceNumber, issuedDate, statusReading.PaidDate,
            note, cancellationToken).ConfigureAwait(false);

        if (Drafts is { } drafts && IsThroughDrafts(request, drafts))
        {
            var document = await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            await drafts.RecordStatusReadingAsync(document, statusReading.ExternalStatus, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task LinkLinesAsync(InvoiceRequest request, Guid requestId, CancellationToken cancellationToken)
    {
        foreach (var line in request.Lines)
        {
            if (string.Equals(line.SourceKind, TimesheetEntry.CanonicalKind, StringComparison.Ordinal))
                await _timesheets.MarkInvoicedAsync(line.SourceId, requestId, cancellationToken).ConfigureAwait(false);
            else if (string.Equals(line.SourceKind, DeliverableCompletion.CanonicalKind, StringComparison.Ordinal))
                await _deliverables.MarkInvoicedAsync(line.SourceId, requestId, cancellationToken).ConfigureAwait(false);
            else if (string.Equals(line.SourceKind, ProjectExpense.CanonicalKind, StringComparison.Ordinal) && _expenses is not null)
                await _expenses.MarkInvoicedAsync(line.SourceId, requestId, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Infers the closest declared <see cref="VatRate"/> from an expense's
    /// own directly-entered <paramref name="net"/>/<paramref name="vat"/>
    /// amounts (`WP 21.3B`) — <see cref="ProjectExpense"/> carries the
    /// figures a receipt actually states, never a rate; <see cref="InvoiceRequestLine.VatAmount"/>
    /// is always <see cref="VatRate"/>-derived, so this line's own rate is
    /// the closest of the standard (20%), reduced (5%) or zero/exempt/out-
    /// of-scope (0%) percentages to what the receipt actually recorded.
    /// <b>Disclosed, not hidden:</b> an expense whose own VAT is not a
    /// clean 20%, 5% or 0% split of its net (an unusual supplier VAT
    /// treatment) rounds to the nearest of the three on the raised
    /// request — the exact entered figures remain readable on the expense
    /// itself, unchanged, for what the consultant later matches in Xero.
    /// </summary>
    private static VatRate InferVatRate(Money net, Money vat)
    {
        if (net.Amount <= 0m || vat.Amount <= 0m)
            return VatRate.OutOfScope;

        var effective = vat.Amount / net.Amount;

        (VatRate Rate, decimal Percentage)[] candidates =
        [
            (VatRate.Standard, VatRate.Standard.Percentage()),
            (VatRate.Reduced, VatRate.Reduced.Percentage()),
            (VatRate.OutOfScope, VatRate.OutOfScope.Percentage()),
        ];

        return candidates.OrderBy(c => Math.Abs(c.Percentage - effective)).First().Rate;
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

    /// <summary>Every source (a timesheet entry or a deliverable completion) a live request carries, keyed by source id — a Rejected or Voided request frees its lines.</summary>
    private async Task<Dictionary<Guid, InvoiceRequest>> ListCarriedSourcesAsync(CancellationToken cancellationToken)
    {
        var carried = new Dictionary<Guid, InvoiceRequest>();
        // `TD-88`/`WP 21.5B`: `Status`/`Lines` are `InvoiceRequest`-own
        // fields, not on the index row.
        var entries = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var requests = await _context.Repository.MaterialiseAsync<InvoiceRequest>(entries, cancellationToken).ConfigureAwait(false);

        foreach (var request in requests)
        {
            if (!IsLive(request) || request.Status is InvoiceRequestStatus.Rejected or InvoiceRequestStatus.Voided)
                continue;

            foreach (var line in request.Lines)
                carried.TryAdd(line.SourceId, request);
        }

        return carried;
    }

    private static InvoiceRequestResult NotFound(Guid requestId) =>
        new(InvoiceRequestRefusal.RequestNotFound, $"No invoice request '{requestId}' is registered.", null);

    /// <summary>The archived-project guard (`WP 19.5C`): every mutating command on an archived project's objects is refused, here, before its own mutator ever runs.</summary>
    private async Task<InvoiceRequestResult?> ArchivedAsync(InvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.ParentId is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project)
        {
            return null;
        }

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? new InvoiceRequestResult(InvoiceRequestRefusal.ProjectArchived, $"Project '{projectId}' is archived (closed {project.ClosedOn:O}); this request is read-only.", request)
            : null;
    }

    /// <summary>
    /// Builds the plain-data projection a connector is actually handed —
    /// resolving <see cref="InvoiceRequestSnapshot.ClientName"/> from the
    /// Organisation catalogue by <see cref="InvoiceRequest.ClientOrganisationId"/>
    /// here, the one place in this seam that reads the catalogue at all
    /// (`WP 19.1A-R1` disclosure #3). <see langword="null"/> when the id
    /// does not resolve to any registered organisation — every connector
    /// implementation rejects outright rather than matching or creating a
    /// contact named after a raw, meaningless id.
    /// </summary>
    private async Task<InvoiceRequestSnapshot> ToSnapshotAsync(InvoiceRequest request, CancellationToken cancellationToken)
    {
        var organisation = await _organisations.FindAsync(request.ClientOrganisationId, cancellationToken).ConfigureAwait(false);

        return new InvoiceRequestSnapshot(
            request.Id, request.ClientOrganisationId, organisation?.Definition.Name, request.PurchaseOrderReference,
            request.Currency, request.Lines, request.Total);
    }

    /// <summary>
    /// The invoice number a request carries in the accounting system
    /// (`v0.24.0` X4, design §3: "the same number in both systems"): once
    /// sent, the number the accounting system recorded; before then, the
    /// request's own <see cref="EngineeringObjectBase.Identifier"/>
    /// (<c>CUSTOMER-PROJECTREF-INV-NNN</c>), or — for a request raised in a
    /// project without project-centric numbering — <c>TOS-</c> and its id.
    /// </summary>
    /// <param name="request">The request.</param>
    public static string InvoiceNumberFor(InvoiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Status is InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted or InvoiceRequestStatus.Voided
            && !string.IsNullOrWhiteSpace(request.ExternalInvoiceNumber))
        {
            return request.ExternalInvoiceNumber!;
        }

        return string.IsNullOrWhiteSpace(request.Identifier) ? $"TOS-{request.Id:N}".ToUpperInvariant() : request.Identifier!.Trim();
    }

    /// <summary>
    /// Builds what a draft seam is handed for <paramref name="request"/>
    /// (`v0.24.0` X4): its number, its client (catalogue id, reference and
    /// name), its lines, the date it is (or was) sent and its due date, and
    /// the reference text <c>{project code} · {deliverable or "time &amp;
    /// expenses"}</c>.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="sentAt">When it is (or was) sent — the invoice date.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    internal async Task<InvoiceDraftDocument> ToDraftDocumentAsync(InvoiceRequest request, DateTimeOffset sentAt, CancellationToken cancellationToken)
    {
        var organisation = await _organisations.FindAsync(request.ClientOrganisationId, cancellationToken).ConfigureAwait(false);
        var date = DateOnly.FromDateTime(sentAt.UtcDateTime);
        var dueDate = request.DueOn ?? date.AddDays(request.PaymentTerms.Days());

        return new InvoiceDraftDocument(
            request.Id,
            InvoiceNumberFor(request),
            request.ClientOrganisationId,
            organisation?.Definition.Reference,
            organisation?.Definition.Name,
            request.Currency,
            request.Lines,
            request.Total,
            date,
            dueDate,
            request.ExternalReference ?? await ReferenceForAsync(request, cancellationToken).ConfigureAwait(false),
            request.ExternalId);
    }

    /// <summary>
    /// Builds the draft-seam document for the request <paramref name="requestId"/>
    /// as it stands — for the Xero outbox's update and delete handlers
    /// (`v0.24.0` X4). <see langword="null"/> when no live request has that id.
    /// </summary>
    /// <param name="requestId">The request.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    internal async Task<(InvoiceRequest Request, InvoiceDraftDocument Document)?> FindDraftDocumentAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var request = await FindRequestAsync(requestId, cancellationToken).ConfigureAwait(false);
        if (request is null)
            return null;

        return (request, await ToDraftDocumentAsync(request, request.SentAtUtc ?? _time.GetUtcNow(), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The longest reference text built (Xero's own <c>Reference</c> limit).</summary>
    private const int MaximumReferenceLength = 255;

    /// <summary><c>{project code} · {deliverable or "time &amp; expenses"}</c> for <paramref name="request"/> (design §3).</summary>
    private async Task<string> ReferenceForAsync(InvoiceRequest request, CancellationToken cancellationToken)
    {
        var projectCode = "TempestOS";
        if (request.ParentId is { } projectId
            && await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is Project project)
        {
            projectCode = string.IsNullOrWhiteSpace(project.Identifier) ? project.DisplayName : project.Identifier!;
        }

        string? deliverable = null;
        foreach (var line in request.Lines.Where(l => string.Equals(l.SourceKind, DeliverableCompletion.CanonicalKind, StringComparison.Ordinal)))
        {
            if (await _context.Repository.FindAsync(line.SourceId, cancellationToken).ConfigureAwait(false) is DeliverableCompletion completion
                && await _context.Repository.FindAsync(completion.DeliverableId, cancellationToken).ConfigureAwait(false) is EngineeringObjectBase named)
            {
                deliverable = string.IsNullOrWhiteSpace(named.DisplayName) ? named.BusinessIdentifier : named.DisplayName;
                break;
            }
        }

        var reference = $"{projectCode.Trim()} · {deliverable?.Trim() ?? "time & expenses"}";
        return reference.Length <= MaximumReferenceLength ? reference : reference[..MaximumReferenceLength];
    }

    /// <summary>
    /// Maps a connector's own free-form status word to what it means for
    /// this Kind's own lifecycle — the one place that decision is made
    /// (<see cref="IInvoicingConnector.ReadStatusAsync"/>'s own remarks:
    /// a connector never maps its own words itself). Matches the
    /// vocabulary real accounting systems actually use (Xero: <c>DRAFT</c>,
    /// <c>SUBMITTED</c>, <c>AUTHORISED</c>, <c>PAID</c>, <c>VOIDED</c>,
    /// <c>DELETED</c>) case-insensitively and by substring, so a
    /// provider-specific decoration around the same word still matches.
    /// <c>DRAFT</c> and <c>SUBMITTED</c> leave a Sent request Sent (still a
    /// draft awaiting the Product Owner, D3); <c>DELETED</c> — a draft
    /// deleted in the accounting system — is Voided (`v0.24.0` X4, design
    /// §4.2), which frees its lines exactly as a voided invoice does.
    /// Anything unrecognised leaves <paramref name="current"/> unchanged
    /// rather than guessing.
    /// </summary>
    internal static InvoiceRequestStatus InterpretStatus(string externalStatus, InvoiceRequestStatus current)
    {
        ArgumentNullException.ThrowIfNull(externalStatus);

        if (externalStatus.Contains("VOID", StringComparison.OrdinalIgnoreCase)
            || externalStatus.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
        {
            return InvoiceRequestStatus.Voided;
        }

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

/// <summary>
/// What a draft seam (<see cref="IInvoiceDraftSync"/>) is handed for one
/// invoice request (`v0.24.0` X4) — plain data, built by
/// <see cref="InvoicingService"/> exactly as <see cref="InvoiceRequestSnapshot"/>
/// is, widened with what a numbered draft needs.
/// </summary>
/// <param name="RequestId">The request's own id.</param>
/// <param name="InvoiceNumber">The number the invoice carries in both systems (<see cref="InvoicingService.InvoiceNumberFor"/>).</param>
/// <param name="ClientOrganisationId">The client's Organisation-catalogue id.</param>
/// <param name="ClientReference">The client organisation's own reference (the key its accounting-system contact link is kept under); <see langword="null"/> when the id resolves to no organisation.</param>
/// <param name="ClientName">The client organisation's own name; <see langword="null"/> when unresolved.</param>
/// <param name="Currency">The currency every line is stated in.</param>
/// <param name="Lines">The lines, in order.</param>
/// <param name="Total">Their net total.</param>
/// <param name="Date">The invoice date — the date it is (or was) sent.</param>
/// <param name="DueDate">The invoice date plus the request's own payment terms.</param>
/// <param name="Reference">The reference text: <c>{project code} · {deliverable or "time &amp; expenses"}</c>.</param>
/// <param name="ExternalId">The accounting system's own id, once the request has been sent; <see langword="null"/> before.</param>
public sealed record InvoiceDraftDocument(
    Guid RequestId,
    string InvoiceNumber,
    string ClientOrganisationId,
    string? ClientReference,
    string? ClientName,
    CurrencyCode Currency,
    IReadOnlyList<InvoiceRequestLine> Lines,
    Money Total,
    DateOnly Date,
    DateOnly DueDate,
    string Reference,
    string? ExternalId);

/// <summary>What a draft seam did with a change to an invoice it already created (`v0.24.0` X4).</summary>
public enum InvoiceDraftChangeOutcome
{
    /// <summary>The accounting system still held the invoice as a draft and took the change (or the deletion).</summary>
    Applied,

    /// <summary>The accounting system holds the invoice at another status (<see cref="InvoiceDraftChange.ExternalStatus"/>); nothing was changed — the change belongs in the accounting system now.</summary>
    NotDraft,

    /// <summary>Something TempestOS holds locally is missing (a contact link, a tax type, an account code); nothing was sent. <see cref="InvoiceDraftChange.Reason"/> says what.</summary>
    Blocked,
}

/// <summary>The answer to a draft change (`v0.24.0` X4).</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="ExternalStatus">The accounting system's own status word for the invoice, as read when deciding.</param>
/// <param name="Reason">Why, for <see cref="InvoiceDraftChangeOutcome.Blocked"/>.</param>
public sealed record InvoiceDraftChange(InvoiceDraftChangeOutcome Outcome, string? ExternalStatus = null, string? Reason = null);

/// <summary>Who holds an invoice number in the accounting system, as <see cref="IInvoiceDraftSync.FindNumberHolderAsync"/> reads it (`v0.24.0` X4).</summary>
public enum InvoiceNumberHolder
{
    /// <summary>No invoice carries the number.</summary>
    Nobody,

    /// <summary>TempestOS's own invoice carries it: the request's reference, under the client's linked contact (linked as found).</summary>
    Own,

    /// <summary>An invoice carries it with the request's own reference, but under a contact other than the client's linked one (the client was re-linked after a send) — TempestOS's own work, never to be treated as someone else's.</summary>
    OwnReferenceOtherContact,

    /// <summary>Another invoice — not carrying the request's reference — holds the number.</summary>
    AnotherInvoice,
}

/// <summary>What <see cref="IInvoiceDraftSync.FindNumberHolderAsync"/> found under a request's number.</summary>
/// <param name="Holder">Who holds the number.</param>
/// <param name="Invoice">TempestOS's own invoice, for <see cref="InvoiceNumberHolder.Own"/>; otherwise <see langword="null"/>.</param>
/// <param name="ExternalStatus">The status word of the invoice found (the first, when several), or <see langword="null"/> when nobody holds the number.</param>
public sealed record InvoiceNumberFinding(InvoiceNumberHolder Holder, CreatedInvoice? Invoice = null, string? ExternalStatus = null);

/// <summary>
/// The numbered-draft seam of an accounting connector (`v0.24.0` X4,
/// `ADR-0162` D3/D4): create a sales invoice as a draft carrying
/// TempestOS's own invoice number and the client's linked contact; find it
/// again by that number after a lost response; change or delete it only
/// while the accounting system still holds it as a draft; re-queue a send
/// the connector could not reach. Never approves, never sends anything to
/// the client. Xero's implementation is
/// <c>Tempest.Core.Invoicing.Xero.Sync.Invoices.XeroInvoiceDrafts</c>;
/// <see cref="InvoicingService"/> uses it only when
/// <see cref="ConnectorName"/> names its own connector.
/// </summary>
/// <remarks>
/// A result, never an exception, for anything the accounting system or the
/// network did (`ADR-0151`).
/// </remarks>
public interface IInvoiceDraftSync
{
    /// <summary>The connector this seam belongs to (<see cref="IInvoicingConnector.Name"/>).</summary>
    string ConnectorName { get; }

    /// <summary>The accounting system's own status word for a draft it has just created (Xero: <c>DRAFT</c>).</summary>
    string CreatedStatus { get; }

    /// <summary>
    /// Why <paramref name="document"/> cannot be sent yet, from what
    /// TempestOS holds locally (the client's contact link, a tax type and
    /// account code for every line) — or <see langword="null"/> when nothing
    /// local stands in the way. No network call.
    /// </summary>
    Task<string?> FindBlockingReasonAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates <paramref name="document"/> as a draft — or, when the
    /// accounting system already holds an invoice with its number that is
    /// TempestOS's own (a lost response), links that one instead; an invoice
    /// with its number that is not TempestOS's own is
    /// <see cref="ConnectorOutcome.Rejected"/>, never a duplicate.
    /// <see cref="CreatedInvoice.Reference"/> is the reference text the draft carries.
    /// </summary>
    Task<ConnectorResult<CreatedInvoice>> CreateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// The invoice carrying <paramref name="document"/>'s number, when it is
    /// TempestOS's own (linked as found); <see langword="null"/> (Ok) when
    /// nothing carries the number; <see cref="ConnectorOutcome.Rejected"/>
    /// when another invoice holds it.
    /// </summary>
    Task<ConnectorResult<CreatedInvoice?>> FindByInvoiceNumberAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Who holds <paramref name="document"/>'s number (`v0.24.0` X4, the
    /// void of a request whose send was cut off): TempestOS's own invoice
    /// (linked as found, as <see cref="FindByInvoiceNumberAsync"/> does),
    /// one carrying its reference under another contact, another invoice,
    /// or nobody — each an Ok answer. Anything other than Ok (including
    /// <see cref="ConnectorOutcome.Rejected"/> for a look-up the system
    /// refused) means the holder is not known.
    /// </summary>
    Task<ConnectorResult<InvoiceNumberFinding>> FindNumberHolderAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>Changes the draft for <paramref name="document"/> to its content — only while the accounting system still holds it as a draft.</summary>
    Task<ConnectorResult<InvoiceDraftChange>> UpdateDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>Deletes the draft for <paramref name="document"/> — only while the accounting system still holds it as a draft (a voided TempestOS invoice deletes its draft only).</summary>
    Task<ConnectorResult<InvoiceDraftChange>> DeleteDraftAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>Queues the send of <paramref name="document"/> again, durably, after the connector could not be reached.</summary>
    Task QueueSendAsync(InvoiceDraftDocument document, CancellationToken cancellationToken = default);

    /// <summary>Records <paramref name="externalStatus"/>, as just read back, against the request's link — local only.</summary>
    Task RecordStatusReadingAsync(InvoiceDraftDocument document, string externalStatus, CancellationToken cancellationToken = default);
}
