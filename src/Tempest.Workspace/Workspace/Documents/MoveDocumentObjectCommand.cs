using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>Reparents one Document Domain object (<see cref="IHasParent.MoveAsync"/>).</summary>
public sealed class MoveDocumentObjectCommand : IWorkspaceCommand
{
    public MoveDocumentObjectCommand(Guid targetObjectId, string targetKind, Guid? newParentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewParentId = newParentId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new parent, or <see langword="null"/> to make this a top-level object.</summary>
    public Guid? NewParentId { get; }
}

/// <summary>Handles <see cref="MoveDocumentObjectCommand"/>.</summary>
public sealed class MoveDocumentObjectCommandHandler : ICommandHandler<MoveDocumentObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="context">Where the target object is found.</param>
    /// <param name="dispatcher">Dispatches this move's own compensation (`WP 21.1A`) — optional; <see langword="null"/> means no <see cref="CommandResult.Compensation"/> is attached.</param>
    public MoveDocumentObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(MoveDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasParent hasParent)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be moved.");

        return await WorkspaceCommandBindings.MoveResultAsync(
            _context, _dispatcher, hasParent, command.TargetObjectId, command.TargetKind, command.NewParentId,
            parentId => new MoveDocumentObjectCommand(command.TargetObjectId, command.TargetKind, parentId),
            cancellationToken).ConfigureAwait(false);
    }
}
