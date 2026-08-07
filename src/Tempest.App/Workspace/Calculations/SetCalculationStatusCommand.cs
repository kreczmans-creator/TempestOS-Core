using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Sets one Calculation Domain object's own current lifecycle status
/// (<see cref="IHasLifecycle.TransitionAsync"/>, via the existing
/// <see cref="ILifecycleTransitionTable"/> — the same mechanism
/// <see cref="Requirements.SetRequirementStatusCommand"/> already
/// establishes for a different framework). The Calculation Management
/// scope's own Lock/Unlock/Review/Approve/Archive verbs are all this one
/// handler, dispatched with a different <see cref="Status"/> — registered
/// as five separate, descriptive <see cref="Commands.CommandDescriptor"/>s
/// for Command Palette discoverability, never five new mechanisms:
/// "Lock" transitions to <see cref="LifecycleState.Approved"/>, "Unlock"
/// back to <see cref="LifecycleState.Draft"/>, "Request Review" to
/// <see cref="LifecycleState.InReview"/>, "Approve" to
/// <see cref="LifecycleState.Approved"/>, "Archive" to
/// <see cref="LifecycleState.Archived"/>.
/// </summary>
public sealed class SetCalculationStatusCommand : IWorkspaceCommand
{
    public SetCalculationStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
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

/// <summary>Handles <see cref="SetCalculationStatusCommand"/>.</summary>
public sealed class SetCalculationStatusCommandHandler : ICommandHandler<SetCalculationStatusCommand>
{
    private readonly EngineeringDomainContext _context;

    public SetCalculationStatusCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(SetCalculationStatusCommand command, CancellationToken cancellationToken)
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
