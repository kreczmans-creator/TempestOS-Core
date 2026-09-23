using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

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
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the requirement is deleted.</param>
    /// <param name="dispatcher">Dispatches this delete's own compensation (`WP 21.6A`) — optional.</param>
    public DeleteRequirementCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            // Undo restores the requirement this call deleted; redo deletes
            // it again — mirrors `WorkspaceCommandBindings.DeleteCompensation`.
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Delete '{deleted.Identifier}'",
                undo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementCommand(command.TargetObjectId), ct),
                redo: ct => _dispatcher.DispatchAsync(new DeleteRequirementCommand(command.TargetObjectId), ct));

            return CommandResult.Success($"Deleted '{deleted.Identifier}'.", compensation: compensation);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
