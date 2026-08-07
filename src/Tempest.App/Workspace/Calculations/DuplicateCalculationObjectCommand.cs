using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Calculations;

/// <summary>
/// Creates a new object of the same Kind, same content, and same parent as
/// <see cref="IWorkspaceCommand.TargetObjectId"/> — a same-parent shorthand
/// over <see cref="CopyCalculationObjectCommand"/>'s own mechanism (never a
/// second, independent implementation of "create a copy"), mirroring
/// <see cref="Mechanical.DuplicateMechanicalObjectCommand"/>'s own identical shape.
/// </summary>
public sealed class DuplicateCalculationObjectCommand : IWorkspaceCommand
{
    public DuplicateCalculationObjectCommand(Guid targetObjectId, string targetKind, string? newIdentifier = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewIdentifier = newIdentifier;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the duplicate's own new business identifier, or <see langword="null"/> to leave it unset.</summary>
    public string? NewIdentifier { get; }
}

/// <summary>Handles <see cref="DuplicateCalculationObjectCommand"/> by delegating to <see cref="CopyCalculationObjectCommandHandler"/> with the source's own current parent.</summary>
public sealed class DuplicateCalculationObjectCommandHandler : ICommandHandler<DuplicateCalculationObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly CopyCalculationObjectCommandHandler _copyHandler;

    public DuplicateCalculationObjectCommandHandler(EngineeringDomainContext context, CopyCalculationObjectCommandHandler copyHandler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(copyHandler);

        _context = context;
        _copyHandler = copyHandler;
    }

    public async Task<CommandResult> HandleAsync(DuplicateCalculationObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sameParentId = source is IHasParent hasParent ? hasParent.ParentId : null;

        var copyCommand = new CopyCalculationObjectCommand(command.TargetObjectId, command.TargetKind, sameParentId, command.NewIdentifier);

        return await _copyHandler.HandleAsync(copyCommand, cancellationToken).ConfigureAwait(false);
    }
}
