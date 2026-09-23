using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Documents;

/// <summary>Renames one Document Domain object (<see cref="IRenamable.RenameAsync"/>).</summary>
public sealed class RenameDocumentObjectCommand : IWorkspaceCommand
{
    public RenameDocumentObjectCommand(Guid targetObjectId, string targetKind, string newDisplayName)
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

/// <summary>Handles <see cref="RenameDocumentObjectCommand"/>.</summary>
public sealed class RenameDocumentObjectCommandHandler : ICommandHandler<RenameDocumentObjectCommand>
{
    private readonly EngineeringDomainContext _context;

    public RenameDocumentObjectCommandHandler(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    public async Task<CommandResult> HandleAsync(RenameDocumentObjectCommand command, CancellationToken cancellationToken)
    {
        var target = await _context.Repository.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

        if (target is not IRenamable renamable)
            return CommandResult.Failure($"'{command.TargetObjectId}' was not found, or its own Kind cannot be renamed.");

        // `WP 20.1A2` made a rename to a sibling's business identifier refuse
        // (`DuplicateBusinessIdentifierException`); a superseded target
        // refuses likewise. Both are the user's own refusals, said in the
        // status bar — never an unhandled exception from the Ribbon.
        try
        {
            await renamable.RenameAsync(command.NewDisplayName, cancellationToken).ConfigureAwait(false);
        }
        catch (DuplicateBusinessIdentifierException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (SupersededEngineeringObjectException ex)
        {
            return CommandResult.Failure(ex.Message);
        }

        return CommandResult.Success($"Renamed to '{command.NewDisplayName}'.");
    }
}
