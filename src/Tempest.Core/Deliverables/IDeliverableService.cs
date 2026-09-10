using Tempest.Core.BusinessGovernance;

namespace Tempest.Core.Deliverables;

/// <summary>
/// The acts a <see cref="DeliverableCompletion"/> supports: complete, and
/// mark invoiced (`WP 19.0A`, `ADR-0150`). Every act is one transaction
/// with an audit row; a second completion of the same deliverable is
/// refused, as a result, with the first shown — mirroring
/// <c>Tempest.Core.Evidence.IEvidenceService</c>.
/// </summary>
public interface IDeliverableService
{
    /// <summary>
    /// Completes <paramref name="deliverableId"/> under <paramref name="projectId"/>
    /// for the current principal. Refused, as a result, when this deliverable
    /// already carries a completion (the first is returned), or when any of
    /// <paramref name="issuedEvidenceIds"/> does not identify Evidence whose
    /// own status is Issued.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="projectId"/> does not identify a live project, or <paramref name="deliverableId"/> does not identify a live deliverable.</exception>
    Task<DeliverableCompletionResult> CompleteAsync(
        Guid deliverableId, Guid projectId, DateOnly completedOn,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null, Money? fixedPriceValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>Sets <paramref name="completionId"/>'s own <see cref="DeliverableCompletion.InvoicedBy"/> link to <paramref name="requestId"/>. Refused, as a result, if it already carries one.</summary>
    Task<DeliverableCompletionResult> MarkInvoicedAsync(Guid completionId, Guid requestId, CancellationToken cancellationToken = default);
}
