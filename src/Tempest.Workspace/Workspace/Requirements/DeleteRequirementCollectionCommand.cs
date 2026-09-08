using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Soft-deletes one Requirement Collection — never affects any member requirement (<see cref="IRequirementsService.DeleteCollectionAsync"/>).</summary>
public sealed class DeleteRequirementCollectionCommand : IWorkspaceCommand
{
    public DeleteRequirementCollectionCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementCollectionDocumentKind;
}

/// <summary>Handles <see cref="DeleteRequirementCollectionCommand"/>.</summary>
public sealed class DeleteRequirementCollectionCommandHandler : ICommandHandler<DeleteRequirementCollectionCommand>
{
    private readonly IRequirementsService _requirementsService;

    public DeleteRequirementCollectionCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementCollectionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteCollectionAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Deleted collection '{deleted.Name}'.");
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
