using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>Renames one Manufacturing Domain object (<see cref="IRenamable.RenameAsync"/>).</summary>
public sealed class RenameManufacturingObjectCommand : IWorkspaceCommand
{
    public RenameManufacturingObjectCommand(Guid targetObjectId, string targetKind, string newDisplayName)
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

/// <summary>Handles <see cref="RenameManufacturingObjectCommand"/>.</summary>
public sealed class RenameManufacturingObjectCommandHandler : ICommandHandler<RenameManufacturingObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public RenameManufacturingObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(RenameManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IRenamable renamable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be renamed.");

        await renamable.RenameAsync(command.NewDisplayName, cancellationToken).ConfigureAwait(false);

        return CommandResult.Success($"Renamed to '{command.NewDisplayName}'.");
    }
}
