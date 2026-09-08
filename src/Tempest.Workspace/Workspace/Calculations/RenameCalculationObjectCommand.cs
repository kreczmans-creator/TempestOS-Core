using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>Renames one Calculation Domain object (<see cref="IRenamable.RenameAsync"/>).</summary>
public sealed class RenameCalculationObjectCommand : IWorkspaceCommand
{
    public RenameCalculationObjectCommand(Guid targetObjectId, string targetKind, string newDisplayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(newDisplayName);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewDisplayName = newDisplayName;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new display name.</summary>
    public string NewDisplayName { get; }
}

/// <summary>Handles <see cref="RenameCalculationObjectCommand"/>.</summary>
public sealed class RenameCalculationObjectCommandHandler : ICommandHandler<RenameCalculationObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public RenameCalculationObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(RenameCalculationObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IRenamable renamable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be renamed.");

        await renamable.RenameAsync(command.NewDisplayName, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Renamed to '{command.NewDisplayName}'.");
    }
}
