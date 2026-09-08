using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Manufacturing;

/// <summary>
/// Records a new content revision of one Manufacturing Domain object
/// (<see cref="IHasRevisions.ReviseAsync"/>) — this Work Package's own
/// "Edit" capability.
/// </summary>
public sealed class ReviseManufacturingObjectCommand : IWorkspaceCommand
{
    public ReviseManufacturingObjectCommand(Guid targetObjectId, string targetKind, string newContent, string? changeSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(newContent);

        TargetObjectId = targetObjectId;
        TargetKind = targetKind;
        NewContent = newContent;
        ChangeSummary = changeSummary;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind { get; }

    /// <summary>Gets the new revision content.</summary>
    public string NewContent { get; }

    /// <summary>Gets an optional summary of what changed.</summary>
    public string? ChangeSummary { get; }
}

/// <summary>Handles <see cref="ReviseManufacturingObjectCommand"/>.</summary>
public sealed class ReviseManufacturingObjectCommandHandler : ICommandHandler<ReviseManufacturingObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public ReviseManufacturingObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(ReviseManufacturingObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasRevisions revisable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be revised.");

        var revised = await revisable.ReviseAsync(command.NewContent, command.ChangeSummary, cancellationToken).ConfigureAwait(false);
        var revisionNumber = (revised as IEngineeringObject)?.CurrentRevisionNumber;

        return CommandResult.Success($"Revised '{command.TargetObjectId}' to revision {revisionNumber}.");
    }
}
