using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Sets one Document Domain object's own current lifecycle status
/// (<see cref="IHasLifecycle.TransitionAsync"/>, via the existing
/// <see cref="ILifecycleTransitionTable"/> — the same mechanism
/// <see cref="Calculations.SetCalculationStatusCommand"/>/
/// <see cref="Requirements.SetRequirementStatusCommand"/> already establish
/// for a different framework). Unlike Calculations' own Lock/Unlock
/// aliasing, this Work Package's own named statuses (Draft/Review/Approved/
/// Released) map directly onto <see cref="LifecycleState"/>'s own existing
/// values one-for-one — no descriptive alias is needed.
/// </summary>
public sealed class SetDocumentStatusCommand : IWorkspaceCommand
{
    public SetDocumentStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        Status = status;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the object's own new status.</summary>
    public LifecycleState Status { get; }
}

/// <summary>Handles <see cref="SetDocumentStatusCommand"/>.</summary>
public sealed class SetDocumentStatusCommandHandler : ICommandHandler<SetDocumentStatusCommand>
{
    private readonly EngineeringDomainContext _context;

    public SetDocumentStatusCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(SetDocumentStatusCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasLifecycle lifecycle)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind has no lifecycle status.");

        try
        {
            await lifecycle.TransitionAsync(command.Status, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidLifecycleTransitionException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Status set to '{command.Status}' for '{command.TargetObjectId}'.");
    }
}
