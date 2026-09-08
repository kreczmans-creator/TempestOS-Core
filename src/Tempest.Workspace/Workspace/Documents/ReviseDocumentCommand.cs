using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Records a new content revision of one Document Domain object
/// (<see cref="IHasRevisions.ReviseAsync"/>) — the Document Management
/// scope's own "Edit" capability, and this Work Package's own realisation of
/// "revision management"/"revision identifiers" (<see cref="IEngineeringObject.CurrentRevisionNumber"/>
/// advances by one on every call, already-existing Domain behaviour, unmodified).
/// Distinct from <see cref="RenameDocumentObjectCommand"/> (the object's own
/// business name).
/// </summary>
public sealed class ReviseDocumentCommand : IWorkspaceCommand
{
    public ReviseDocumentCommand(Guid targetObjectId, string targetKind, string newContent, string? changeSummary = null)
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

/// <summary>Handles <see cref="ReviseDocumentCommand"/>.</summary>
public sealed class ReviseDocumentCommandHandler : ICommandHandler<ReviseDocumentCommand>
{
    private readonly EngineeringDomainContext _context;

    public ReviseDocumentCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(ReviseDocumentCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IHasRevisions revisable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be revised.");

        var revised = await revisable.ReviseAsync(command.NewContent, command.ChangeSummary, cancellationToken).ConfigureAwait(false);
        var revisionNumber = (revised as IEngineeringObject)?.CurrentRevisionNumber;

        return CommandResult.Success($"Revised '{command.TargetObjectId}' to revision {revisionNumber}.");
    }
}
