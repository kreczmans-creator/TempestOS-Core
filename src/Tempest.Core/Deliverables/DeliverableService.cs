using Tempest.Core.BusinessGovernance;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Core.Deliverables;

/// <summary>The concrete <see cref="IDeliverableService"/> implementation.</summary>
public sealed class DeliverableService : IDeliverableService
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DeliverableService"/> class.</summary>
    public DeliverableService(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<DeliverableCompletionResult> CompleteAsync(
        Guid deliverableId, Guid projectId, DateOnly completedOn,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null, Money? fixedPriceValue = null,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project)
            throw new ArgumentException($"'{projectId}' does not identify a live project.", nameof(projectId));

        if (await _context.Repository.FindAsync(deliverableId, cancellationToken).ConfigureAwait(false) is not Deliverable)
            throw new ArgumentException($"'{deliverableId}' does not identify a live deliverable.", nameof(deliverableId));

        var existing = await FindExistingCompletionAsync(deliverableId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.AlreadyCompleted,
                $"Deliverable '{deliverableId}' was already completed on {existing.CompletedOn:O} (completion '{existing.Id}'). A deliverable can be completed once.",
                existing);
        }

        var evidenceIds = issuedEvidenceIds ?? [];

        foreach (var evidenceId in evidenceIds)
        {
            if (await _context.Repository.FindAsync(evidenceId, cancellationToken).ConfigureAwait(false) is not Core.Evidence.Evidence evidence
                || evidence.Status != Core.Evidence.EvidenceStatus.Issued)
            {
                return new DeliverableCompletionResult(
                    DeliverableCompletionRefusal.EvidenceNotIssued,
                    $"Evidence '{evidenceId}' is not Issued; a deliverable completion may only cite issued evidence.",
                    null);
            }
        }

        var principalId = _context.ResolveCurrentPrincipalId();

        var created = await new EngineeringObjectFactory<DeliverableCompletion>(
            DeliverableCompletion.CanonicalKind,
            _context,
            (doc, rev) => new DeliverableCompletion(
                doc, rev, _context, identifier: null, $"Deliverable completed — {completedOn:yyyy-MM-dd}", EngineeringObjectMetadata.Empty,
                deliverableId, completedOn, principalId, evidenceIds, documentIds ?? [], fixedPriceValue))
            .CreateAsync($"Deliverable '{deliverableId}' completed.", cancellationToken)
            .ConfigureAwait(false);

        if (created is IHasParent hasParent)
            await hasParent.MoveAsync(projectId, cancellationToken).ConfigureAwait(false);

        return new DeliverableCompletionResult(DeliverableCompletionRefusal.None, null, (DeliverableCompletion)created);
    }

    /// <inheritdoc />
    public async Task<DeliverableCompletionResult> MarkInvoicedAsync(Guid completionId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var completion = await _context.Repository.FindAsync(completionId, cancellationToken).ConfigureAwait(false) as DeliverableCompletion;
        if (completion is null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.CompletionNotFound, $"No deliverable completion '{completionId}' is registered.", null);
        }

        if (completion.InvoicedBy is not null)
        {
            return new DeliverableCompletionResult(
                DeliverableCompletionRefusal.CompletionInvoiced,
                $"Deliverable completion '{completionId}' is already invoiced (request '{completion.InvoicedBy:N}').",
                completion);
        }

        await completion.MarkInvoicedAsync(requestId, cancellationToken).ConfigureAwait(false);

        return new DeliverableCompletionResult(DeliverableCompletionRefusal.None, null, completion);
    }

    private async Task<DeliverableCompletion?> FindExistingCompletionAsync(Guid deliverableId, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListByKindAsync(DeliverableCompletion.CanonicalKind, cancellationToken).ConfigureAwait(false);

        return all
            .OfType<DeliverableCompletion>()
            .FirstOrDefault(c => IsLive(c) && c.DeliverableId == deliverableId);
    }

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
