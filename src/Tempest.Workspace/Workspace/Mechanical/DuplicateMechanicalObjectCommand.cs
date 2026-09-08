using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Mechanical;

/// <summary>
/// Creates a new object of the same Kind, same content, and same parent as
/// <see cref="IWorkspaceCommand.TargetObjectId"/> — a same-parent shorthand
/// over <see cref="CopyMechanicalObjectCommand"/>'s own mechanism (never a
/// second, independent implementation of "create a copy").
/// </summary>
public sealed class DuplicateMechanicalObjectCommand : IWorkspaceCommand
{
    public DuplicateMechanicalObjectCommand(Guid targetObjectId, string targetKind, string? newIdentifier = null)
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

/// <summary>Handles <see cref="DuplicateMechanicalObjectCommand"/> by delegating to <see cref="CopyMechanicalObjectCommandHandler"/> with the source's own current parent.</summary>
public sealed class DuplicateMechanicalObjectCommandHandler : ICommandHandler<DuplicateMechanicalObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly CopyMechanicalObjectCommandHandler _copyHandler;

    public DuplicateMechanicalObjectCommandHandler(EngineeringDomainContext context, CopyMechanicalObjectCommandHandler copyHandler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(copyHandler);

        _context = context;
        _copyHandler = copyHandler;
    }

    public async Task<CommandResult> HandleAsync(DuplicateMechanicalObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sameParentId = source is IHasParent hasParent ? hasParent.ParentId : null;

        var copyCommand = new CopyMechanicalObjectCommand(command.TargetObjectId, command.TargetKind, sameParentId, command.NewIdentifier);

        return await _copyHandler.HandleAsync(copyCommand, cancellationToken).ConfigureAwait(false);
    }
}
