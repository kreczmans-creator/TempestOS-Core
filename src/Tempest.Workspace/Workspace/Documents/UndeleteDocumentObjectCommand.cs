using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>
/// Restores one soft-deleted Document Domain object
/// (<see cref="IDeletable.UndeleteAsync"/>) — the Undo half of Delete's own
/// compensation, and the Redo half of Create's/Copy's own (`WP 21.1A`).
/// </summary>
/// <remarks>
/// Never registered as a <see cref="CommandDescriptor"/>: reached only as a
/// compensation, dispatched directly through <see cref="ICommandDispatcher"/>
/// — never a Ribbon- or Palette-visible command, and never invocable by a
/// person choosing a soft-deleted object, since none is ever shown in the
/// Project Explorer to choose.
/// </remarks>
public sealed class UndeleteDocumentObjectCommand : IWorkspaceCommand
{
    public UndeleteDocumentObjectCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="UndeleteDocumentObjectCommand"/>.</summary>
public sealed class UndeleteDocumentObjectCommandHandler : ICommandHandler<UndeleteDocumentObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public UndeleteDocumentObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(UndeleteDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be restored.");

        try
        {
            await deletable.UndeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectNotDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (EngineeringObjectParentDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();
        return CommandResult.Success($"Restored '{sourceName}'.", command.TargetObjectId, command.TargetKind);
    }
}
