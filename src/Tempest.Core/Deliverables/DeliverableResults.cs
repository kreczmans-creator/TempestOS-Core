namespace Tempest.Core.Deliverables;

/// <summary>Why an <see cref="IDeliverableService"/> act was refused, or <see cref="None"/> if it was not.</summary>
public enum DeliverableCompletionRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No deliverable completion is registered under the requested id.</summary>
    CompletionNotFound,

    /// <summary>The named deliverable already carries a completion — a deliverable can be completed once.</summary>
    AlreadyCompleted,

    /// <summary>One of the cited evidence ids does not identify Evidence whose own status is Issued.</summary>
    EvidenceNotIssued,

    /// <summary>This completion already carries an <see cref="DeliverableCompletion.InvoicedBy"/> link.</summary>
    CompletionInvoiced,
}

/// <summary>
/// The outcome of an <see cref="IDeliverableService"/> act: either it
/// happened, or a refusal that says why it did not.
/// </summary>
/// <param name="Refusal">Why the act was refused, or <see cref="DeliverableCompletionRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Completion">The completion acted on — for a refused second <see cref="IDeliverableService.CompleteAsync"/>, the first; for a successful act, the completion itself.</param>
public sealed record DeliverableCompletionResult(DeliverableCompletionRefusal Refusal, string? Reason, DeliverableCompletion? Completion)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == DeliverableCompletionRefusal.None;
}
