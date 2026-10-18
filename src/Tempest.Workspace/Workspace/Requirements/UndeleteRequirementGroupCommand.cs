using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>
/// Restores one soft-deleted Requirement Group (<see cref="IRequirementsService.UndeleteGroupAsync"/>)
/// — the Undo half of <c>requirements.delete-group</c>'s own compensation and the
/// Redo half of <c>requirements.create-group</c>'s (`v1.0.0` RC; the identical
/// shape <see cref="UndeleteRequirementCommand"/> gave a Requirement in `WP 21.6A`).
/// </summary>
/// <remarks>Never registered as a <see cref="CommandDescriptor"/>: reached only as a compensation, dispatched directly through <see cref="ICommandDispatcher"/>.</remarks>
public sealed class UndeleteRequirementGroupCommand : IWorkspaceCommand
{
    public UndeleteRequirementGroupCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementGroupDocumentKind;
}

/// <summary>Handles <see cref="UndeleteRequirementGroupCommand"/>.</summary>
public sealed class UndeleteRequirementGroupCommandHandler : ICommandHandler<UndeleteRequirementGroupCommand>
{
    private readonly IRequirementsService _requirementsService;

    public UndeleteRequirementGroupCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(UndeleteRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var restored = await _requirementsService.UndeleteGroupAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Restored group '{restored.Name}'.", restored.Id, RequirementsService.RequirementGroupDocumentKind);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementGroupNotDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementParentGroupDeletedException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
