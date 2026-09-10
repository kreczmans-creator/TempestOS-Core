using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Deliverables;

/// <summary>
/// Completes a selected <c>Deliverable</c> (`WP 19.0A`, `ADR-0150`). Acts
/// on the existing, milestone-parented <c>Deliverable</c> object — the
/// project it belongs to is resolved from its own ancestry (deliverable →
/// milestone → project), never asked of the caller. Refused, as a result,
/// when this deliverable already carries a completion (the first is
/// shown), or when a cited evidence id is not Issued.
/// </summary>
public sealed class CompleteDeliverableCommand : IWorkspaceCommand
{
    /// <summary>Initialises a new instance of the <see cref="CompleteDeliverableCommand"/> class.</summary>
    public CompleteDeliverableCommand(
        Guid targetObjectId, string targetKind, DateOnly completedOn,
        IReadOnlyList<Guid>? issuedEvidenceIds = null, IReadOnlyList<Guid>? documentIds = null, Money? fixedPriceValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        CompletedOn = completedOn;
        IssuedEvidenceIds = issuedEvidenceIds ?? [];
        DocumentIds = documentIds ?? [];
        FixedPriceValue = fixedPriceValue;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the day the deliverable was completed.</summary>
    public DateOnly CompletedOn { get; }

    /// <summary>Gets the Evidence records, each Issued, this completion is supported by.</summary>
    public IReadOnlyList<Guid> IssuedEvidenceIds { get; }

    /// <summary>Gets the documents this completion is supported by.</summary>
    public IReadOnlyList<Guid> DocumentIds { get; }

    /// <summary>Gets the fixed price this deliverable is billed at, when it is. <see langword="null"/> otherwise.</summary>
    public Money? FixedPriceValue { get; }
}

/// <summary>Handles <see cref="CompleteDeliverableCommand"/>.</summary>
public sealed class CompleteDeliverableCommandHandler : ICommandHandler<CompleteDeliverableCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly IDeliverableService _service;

    /// <summary>Initialises a new instance of the <see cref="CompleteDeliverableCommandHandler"/> class.</summary>
    public CompleteDeliverableCommandHandler(EngineeringDomainContext context, IDeliverableService service)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(service);

        _context = context;
        _service = service;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Resolves the deliverable's own project by walking its ancestry
    /// (deliverable → milestone → project, <c>ProjectMilestoneService</c>'s
    /// own parenting) before ever calling
    /// <see cref="IDeliverableService.CompleteAsync"/> — so a deliverable
    /// selected from a malformed or incomplete hierarchy (including the
    /// non-existent id a generic contract test dispatches against) is
    /// refused as an outcome here, never a throw out of the service.
    /// </remarks>
    public async Task<CommandResult> HandleAsync(CompleteDeliverableCommand command, CancellationToken cancellationToken)
    {
        if (await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false) is not Deliverable deliverable)
            return CommandResult.Failure($"'{command.TargetObjectId}' is not a known deliverable.");

        if (deliverable.ParentId is not { } milestoneId
            || await _context.Repository.FindAsync(milestoneId, cancellationToken).ConfigureAwait(false) is not IHasParent milestone
            || milestone.ParentId is not { } projectId)
        {
            return CommandResult.Failure($"Deliverable '{command.TargetObjectId}' is not parented to a milestone in a project.");
        }

        DeliverableCompletionResult result;

        try
        {
            result = await _service
                .CompleteAsync(
                    command.TargetObjectId, projectId, command.CompletedOn,
                    command.IssuedEvidenceIds, command.DocumentIds, command.FixedPriceValue, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return result.Succeeded
            ? CommandResult.Success(
                $"Deliverable completed on {result.Completion!.CompletedOn:yyyy-MM-dd}.", result.Completion.Id, DeliverableCompletion.CanonicalKind)
            : CommandResult.Failure(result.Reason ?? "The completion was refused.");
    }
}
