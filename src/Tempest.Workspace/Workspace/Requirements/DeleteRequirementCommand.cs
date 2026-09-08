using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Soft-deletes one Requirement (<see cref="IRequirementsService.DeleteAsync"/>).</summary>
public sealed class DeleteRequirementCommand : IWorkspaceCommand
{
    public DeleteRequirementCommand(Guid targetObjectId)
    {
        TargetObjectId = targetObjectId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;
}

/// <summary>Handles <see cref="DeleteRequirementCommand"/>.</summary>
public sealed class DeleteRequirementCommandHandler : ICommandHandler<DeleteRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;

    public DeleteRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Deleted '{deleted.Identifier}'.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
