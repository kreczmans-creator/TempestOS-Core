using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

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
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the collection is deleted.</param>
    /// <param name="dispatcher">Dispatches this delete's own compensation (`v1.0.0` RC) — optional.</param>
    public DeleteRequirementCollectionCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(DeleteRequirementCollectionCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _requirementsService.DeleteCollectionAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false);

            // Undo restores the collection, its membership links intact
            // (Delete never removed them); redo deletes it again.
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Delete collection '{deleted.Name}'",
                undo: ct => _dispatcher.DispatchAsync(new UndeleteRequirementCollectionCommand(command.TargetObjectId), ct),
                redo: ct => _dispatcher.DispatchAsync(new DeleteRequirementCollectionCommand(command.TargetObjectId), ct));

            return CommandResult.Success($"Deleted collection '{deleted.Name}'.", compensation: compensation);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
