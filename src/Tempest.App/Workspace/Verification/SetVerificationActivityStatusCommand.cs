using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Sets one Verification Activity Domain object's own current lifecycle
/// status (<see cref="IHasLifecycle.TransitionAsync"/>, via the existing
/// <see cref="ILifecycleTransitionTable"/> — the same mechanism
/// <see cref="Calculations.SetCalculationStatusCommand"/>/
/// <see cref="Documents.SetDocumentStatusCommand"/> already establish for
/// a different Kind). This Work Package's own Review/Approve/Archive
/// verbs are all this one handler, dispatched with a different
/// <see cref="Status"/> — registered as three separate, descriptive
/// <see cref="Commands.CommandDescriptor"/>s for Command Palette
/// discoverability, never three new mechanisms (`ADR-0090`, mirroring
/// `ADR-0087` exactly): "Request Review" transitions to
/// <see cref="LifecycleState.InReview"/>, "Approve" to
/// <see cref="LifecycleState.Approved"/>, "Archive" to the terminal
/// <see cref="LifecycleState.Archived"/>. <see cref="LifecycleState.Draft"/>
/// (the object's own starting status) and <see cref="LifecycleState.InReview"/>
/// are also this Work Package's own realisation of "Verification Plan"
/// vs. "Verification Activity" (`ADR-0090`) — no separate Domain Kind
/// exists for either.
/// </summary>
public sealed class SetVerificationActivityStatusCommand : IWorkspaceCommand
{
    public SetVerificationActivityStatusCommand(Guid targetObjectId, string targetKind, LifecycleState status)
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

/// <summary>Handles <see cref="SetVerificationActivityStatusCommand"/>.</summary>
public sealed class SetVerificationActivityStatusCommandHandler : ICommandHandler<SetVerificationActivityStatusCommand>
{
    private readonly EngineeringDomainContext _context;

    public SetVerificationActivityStatusCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(SetVerificationActivityStatusCommand command, CancellationToken cancellationToken)
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
