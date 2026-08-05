using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Soft-deletes one Requirement Group (<see cref="IRequirementsService.DeleteGroupAsync"/>).</summary>
public sealed class DeleteRequirementGroupCommand : IWorkspaceCommand
{
    public DeleteRequirementGroupCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementGroupDocumentKind;
}

/// <summary>Handles <see cref="DeleteRequirementGroupCommand"/>.</summary>
public sealed class DeleteRequirementGroupCommandHandler : ICommandHandler<DeleteRequirementGroupCommand>
{
    private readonly IRequirementsService _requirementsService;

    public DeleteRequirementGroupCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteGroupAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Deleted group '{deleted.Name}'.");
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (RequirementGroupHasChildrenException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
