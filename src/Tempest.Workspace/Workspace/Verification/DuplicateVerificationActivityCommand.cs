using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Creates a new Verification Activity with the same content, subject,
/// method, and parent as <see cref="IWorkspaceCommand.TargetObjectId"/> —
/// a same-parent shorthand over <see cref="CopyVerificationActivityCommand"/>'s
/// own mechanism (never a second, independent implementation of "create a
/// copy"), mirroring
/// <see cref="Calculations.DuplicateCalculationObjectCommand"/>/
/// <see cref="Documents.DuplicateDocumentObjectCommand"/>'s own identical
/// shape.
/// </summary>
public sealed class DuplicateVerificationActivityCommand : IWorkspaceCommand
{
    public DuplicateVerificationActivityCommand(Guid targetObjectId, string targetKind)
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

/// <summary>Handles <see cref="DuplicateVerificationActivityCommand"/> by delegating to <see cref="CopyVerificationActivityCommandHandler"/> with the source's own current parent.</summary>
public sealed class DuplicateVerificationActivityCommandHandler : ICommandHandler<DuplicateVerificationActivityCommand>
{
    private readonly EngineeringDomainContext _context;
    private readonly CopyVerificationActivityCommandHandler _copyHandler;

    public DuplicateVerificationActivityCommandHandler(EngineeringDomainContext context, CopyVerificationActivityCommandHandler copyHandler)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(copyHandler);

        _context = context;
        _copyHandler = copyHandler;
    }

    public async Task<CommandResult> HandleAsync(DuplicateVerificationActivityCommand command, CancellationToken cancellationToken)
    {
        var source = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (source is null)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found.");

        var sameParentId = source is IHasParent hasParent ? hasParent.ParentId : null;

        var copyCommand = new CopyVerificationActivityCommand(command.TargetObjectId, command.TargetKind, sameParentId);

        return await _copyHandler.HandleAsync(copyCommand, cancellationToken).ConfigureAwait(false);
    }
}
