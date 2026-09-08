using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Creates a new object of the same Kind, same content, and same parent as
/// <see cref="IWorkspaceCommand.TargetObjectId"/> — a same-parent shorthand
/// over <see cref="CopyManufacturingObjectCommand"/>'s own mechanism (never
/// a second, independent implementation of "create a copy"), mirroring
/// every prior real-discipline Work Package's own identical shape.
/// </summary>
public sealed class DuplicateManufacturingObjectCommand : IWorkspaceCommand
{
    public DuplicateManufacturingObjectCommand(Guid targetObjectId, string targetKind)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }
}

/// <summary>Handles <see cref="DuplicateManufacturingObjectCommand"/> by delegating to <see cref="CopyManufacturingObjectCommandHandler"/> with the source's own current parent.</summary>
public sealed class DuplicateManufacturingObjectCommandHandler : ICommandHandler<DuplicateManufacturingObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly CopyManufacturingObjectCommandHandler _copyHandler;

    public DuplicateManufacturingObjectCommandHandler(EngineeringDomainContext context, CopyManufacturingObjectCommandHandler copyHandler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(copyHandler);

        _context = context;
        _copyHandler = copyHandler;
    }

    public async Task<CommandResult> HandleAsync(DuplicateManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sameParentId = source is IHasParent hasParent ? hasParent.ParentId : null;

        var copyCommand = new CopyManufacturingObjectCommand(command.TargetObjectId, command.TargetKind, sameParentId);

        return await _copyHandler.HandleAsync(copyCommand, cancellationToken).ConfigureAwait(false);
    }
}
