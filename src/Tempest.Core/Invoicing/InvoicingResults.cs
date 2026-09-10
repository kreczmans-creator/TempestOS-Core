namespace Tempest.Core.Invoicing;

/// <summary>Why an <see cref="IInvoicingService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Evidence.EvidenceRefusal</c> is for citing an
/// unreleased reference record (`WP 19.1A`, `ADR-0151`). What a connector
/// itself reports — rejected, needs re-authorising, unreachable, response
/// lost — is <b>not</b> one of these: <see cref="IInvoicingService.SendAsync"/>
/// and <see cref="IInvoicingService.ReconcileAsync"/> always succeed as
/// <em>acts</em> once their own precondition is met, and the connector's
/// own answer is read from the returned <see cref="InvoiceRequest.Status"/>
/// instead.
/// </remarks>
public enum InvoiceRequestRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No invoice request is registered under the requested id.</summary>
    RequestNotFound,

    /// <summary>No deliverable completion is registered under the requested id.</summary>
    CompletionNotFound,

    /// <summary>The project has no client recorded — an invoice cannot be raised against nobody.</summary>
    NoClient,

    /// <summary>This completion already carries an <c>InvoicedBy</c> link — the refusal names the first request.</summary>
    AlreadyInvoiced,

    /// <summary>The project has no Released rate-card pin, so no currency can be resolved for the request.</summary>
    NoRateCardPinned,

    /// <summary>Neither an unbilled timesheet entry nor a fixed price is available to put on a line — there is nothing to bill.</summary>
    NothingToBill,

    /// <summary>This request's own <see cref="InvoiceRequest.Status"/> does not permit the requested act.</summary>
    TransitionNotPermitted,
}

/// <summary>The outcome of an <see cref="IInvoicingService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="InvoiceRequestRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Request">The request acted on, when it could be resolved — for <see cref="IInvoicingService.SendAsync"/>/<see cref="IInvoicingService.ReconcileAsync"/>, read <see cref="InvoiceRequest.Status"/> here to see what the connector actually reported.</param>
public sealed record InvoiceRequestResult(InvoiceRequestRefusal Refusal, string? Reason, InvoiceRequest? Request)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == InvoiceRequestRefusal.None;
}
