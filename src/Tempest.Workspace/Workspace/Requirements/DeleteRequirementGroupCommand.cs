using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

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
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the group is deleted.</param>
    /// <param name="dispatcher">Dispatches this delete's own compensation (`v1.0.0` RC) — optional.</param>
    public DeleteRequirementGroupCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteGroupAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            // Undo restores the group (refused, with the reason, if its own
            // parent group has since been deleted); redo deletes it again.
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Delete group '{deleted.Name}'",
                undo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementGroupCommand(command.TargetObjectId), ct),
                redo: ct => _dispatcher.DispatchAsync(new DeleteRequirementGroupCommand(command.TargetObjectId), ct));

            return CommandResult.Success($"Deleted group '{deleted.Name}'.", compensation: compensation);
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
