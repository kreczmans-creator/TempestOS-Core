using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>Soft-deletes one Document Domain object (<see cref="IDeletable.DeleteAsync"/>).</summary>
public sealed class DeleteDocumentObjectCommand : IWorkspaceCommand
{
    public DeleteDocumentObjectCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="DeleteDocumentObjectCommand"/>.</summary>
public sealed class DeleteDocumentObjectCommandHandler : ICommandHandler<DeleteDocumentObjectCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="context">Where the target object is found.</param>
    /// <param name="dispatcher">
    /// Dispatches this delete's own compensation (`WP 21.1A`) — optional,
    /// like <see cref="Mechanical.CreateMechanicalObjectCommandHandler"/>'s
    /// own already-optional <c>domainContext</c>: a caller that does not
    /// supply one gets exactly this command's pre-`WP 21.1A` behaviour, with
    /// no <see cref="CommandResult.Compensation"/> attached.
    /// </param>
    public DeleteDocumentObjectCommandHandler(EngineeringDomainContext context, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IDeletable deletable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be deleted.");

        var sourceName = (target as IHasBusinessIdentifier)?.DisplayName ?? command.TargetObjectId.ToString();

        try
        {
            await deletable.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (EngineeringObjectHasChildrenException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        var compensation = WorkspaceCommandBindings.DeleteCompensation(
            _context, _dispatcher, command.TargetObjectId, command.TargetKind, sourceName,
            buildDelete: () => new DeleteDocumentObjectCommand(command.TargetObjectId, command.TargetKind),
            buildUndelete: () => new UndeleteDocumentObjectCommand(command.TargetObjectId, command.TargetKind));

        return CommandResult.Success($"Deleted '{command.TargetObjectId}'.", compensation: compensation);
    }
}
