using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>
/// Restores one soft-deleted Requirement Collection (<see cref="IRequirementsService.UndeleteCollectionAsync"/>)
/// — the Undo half of <c>requirements.delete-collection</c>'s own compensation and
/// the Redo half of <c>requirements.create-collection</c>'s (`v1.0.0` RC).
/// </summary>
/// <remarks>Never registered as a <see cref="CommandDescriptor"/>: reached only as a compensation, dispatched directly through <see cref="ICommandDispatcher"/>.</remarks>
public sealed class UndeleteRequirementCollectionCommand : IWorkspaceCommand
{
    public UndeleteRequirementCollectionCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementCollectionDocumentKind;
}

/// <summary>Handles <see cref="UndeleteRequirementCollectionCommand"/>.</summary>
public sealed class UndeleteRequirementCollectionCommandHandler : ICommandHandler<UndeleteRequirementCollectionCommand>
{
    private readonly IRequirementsService _requirementsService;

    public UndeleteRequirementCollectionCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(UndeleteRequirementCollectionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var restored = await _requirementsService.UndeleteCollectionAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Restored collection '{restored.Name}'.", restored.Id, RequirementsService.RequirementCollectionDocumentKind);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementCollectionNotDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
