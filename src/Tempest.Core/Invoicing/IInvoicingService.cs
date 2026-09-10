namespace Tempest.Core.Invoicing;

/// <summary>
/// The acts an <see cref="InvoiceRequest"/> supports: raise from a
/// completed deliverable, send, reconcile, and void (`WP 19.1A`,
/// `ADR-0151`). Every act is one or more transactions with an audit row;
/// whether an act is <em>permitted</em> is decided here, before
/// <see cref="InvoiceRequest"/>'s own mutators ever run, and reported back
/// as a refusal result rather than an exception, mirroring
/// <c>Tempest.Core.Evidence.IEvidenceService</c>.
/// </summary>
public interface IInvoicingService
{
    /// <summary>
    /// Builds a new <see cref="InvoiceRequest"/> from
    /// <paramref name="deliverableCompletionId"/>'s own project: the
    /// project's client and purchase-order reference; the currency of the
    /// project's own pinned rate card; one line per unbilled, billable
    /// timesheet entry of the project (no <c>InvoicedBy</c>), at each
    /// entry's own frozen billing rate; and, where the completion itself
    /// carries one, one further line for its own fixed price.
    /// </summary>
    /// <remarks>
    /// Refused, as a result, when <paramref name="deliverableCompletionId"/>
    /// does not identify a live completion, when its project has no client
    /// recorded, when its project has no Released rate-card pin (no
    /// currency to raise the request in), when the completion already
    /// carries an <c>InvoicedBy</c> link (the refusal names the first
    /// request), or when there is nothing to bill at all.
    /// </remarks>
    Task<InvoiceRequestResult> RaiseFromCompletionAsync(Guid deliverableCompletionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends <paramref name="requestId"/>'s own request to its project's
    /// configured connector, with the request's own id as the idempotency
    /// key. Moves the request to <see cref="InvoiceRequestStatus.Sending"/>
    /// first; the returned <see cref="InvoiceRequest.Status"/> then shows
    /// what the connector reported — <see cref="InvoiceRequestStatus.Sent"/>
    /// (every line's own source gains its <c>InvoicedBy</c> link),
    /// <see cref="InvoiceRequestStatus.Rejected"/>,
    /// <see cref="InvoiceRequestStatus.Reauthorise"/>,
    /// <see cref="InvoiceRequestStatus.Draft"/> (the connector was
    /// unreachable; not retried automatically), or
    /// <see cref="InvoiceRequestStatus.Unknown"/> (the response was lost).
    /// </summary>
    /// <remarks>Refused, as a result, when <paramref name="requestId"/> does not identify a live request, or when that request is not currently <see cref="InvoiceRequestStatus.Draft"/>.</remarks>
    Task<InvoiceRequestResult> SendAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles <paramref name="requestId"/>'s own request against its
    /// connector. For <see cref="InvoiceRequestStatus.Unknown"/>: found by
    /// reference moves it to <see cref="InvoiceRequestStatus.Sent"/> with
    /// every line's own source linked; not found reverts it to
    /// <see cref="InvoiceRequestStatus.Draft"/>. For
    /// <see cref="InvoiceRequestStatus.Sent"/>/<see cref="InvoiceRequestStatus.Accepted"/>:
    /// reads the connector's own current status, invoice number, issued and
    /// paid dates, moving to <see cref="InvoiceRequestStatus.Accepted"/> when
    /// approved/authorised or <see cref="InvoiceRequestStatus.Voided"/> when
    /// voided there.
    /// </summary>
    /// <remarks>Refused, as a result, when <paramref name="requestId"/> does not identify a live request, or when that request's own status is not one this act reconciles.</remarks>
    Task<InvoiceRequestResult> ReconcileAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Voids <paramref name="requestId"/>'s own request locally — only for
    /// <see cref="InvoiceRequestStatus.Draft"/> or
    /// <see cref="InvoiceRequestStatus.Rejected"/>. A request that already
    /// reached the provider is voided there instead, and read back through
    /// <see cref="ReconcileAsync"/>.
    /// </summary>
    /// <remarks>Refused, as a result, when <paramref name="requestId"/> does not identify a live request, or when that request's own status is neither <see cref="InvoiceRequestStatus.Draft"/> nor <see cref="InvoiceRequestStatus.Rejected"/>.</remarks>
    Task<InvoiceRequestResult> VoidAsync(Guid requestId, CancellationToken cancellationToken = default);
}
