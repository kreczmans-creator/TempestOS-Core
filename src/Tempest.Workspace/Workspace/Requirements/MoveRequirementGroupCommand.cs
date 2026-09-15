using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>Reparents one Requirement Group, or makes it a root group (<see cref="IRequirementsService.MoveGroupAsync"/>).</summary>
public sealed class MoveRequirementGroupCommand : IWorkspaceCommand
{
    public MoveRequirementGroupCommand(Guid targetObjectId, Guid? newParentGroupId)
    {
        TargetObjectId = targetObjectId;
        NewParentGroupId = newParentGroupId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementGroupDocumentKind;

    /// <summary>Gets the new parent group, or <see langword="null"/> to make this a root group.</summary>
    public Guid? NewParentGroupId { get; }
}

/// <summary>Handles <see cref="MoveRequirementGroupCommand"/>.</summary>
public sealed class MoveRequirementGroupCommandHandler : ICommandHandler<MoveRequirementGroupCommand>
{
    private readonly IRequirementsService _requirementsService;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the group is moved.</param>
    /// <param name="dispatcher">Dispatches this move's own compensation (`WP 21.6A`) — optional.</param>
    public MoveRequirementGroupCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(MoveRequirementGroupCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // `WP 20.10C` (PO finding T6): read the group's own current
            // parent before moving it — see MoveRequirementCommandHandler's
            // own identical remark.
            var current = await _requirementsService.FindGroupAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false)
                ?? throw new EngineeringDocumentNotFoundException(command.TargetObjectId);

            if (current.ParentGroupId == command.NewParentGroupId)
            {
                var already = command.NewParentGroupId is { } currentParentId
                    ? $"Already under '{await GroupNameAsync(currentParentId, cancellationToken).ConfigureAwait(false)}'."
                    : "Already at top level.";
                return CommandResult.Success(already, command.TargetObjectId, command.TargetKind);
            }

            var previousParentId = current.ParentGroupId;
            var moved = await _requirementsService.MoveGroupAsync(command.TargetObjectId, command.NewParentGroupId, cancellationToken).ConfigureAwait(false);

            // Undo moves back to the group's own previous parent; redo
            // moves forward again — either direction refuses first when
            // its own destination parent has since been deleted (`WP
            // 21.6A`, mirrors MoveRequirementCommandHandler's own identical
            // reasoning).
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Move group '{moved.Name}'",
                undo: ct => MoveOrRefuseAsync(command.TargetObjectId, previousParentId, ct),
                redo: ct => MoveOrRefuseAsync(command.TargetObjectId, command.NewParentGroupId, ct));

            if (command.NewParentGroupId is { } parentId)
            {
                var parentName = await GroupNameAsync(parentId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success($"Moved group '{moved.Name}' under '{parentName}'.", moved.Id, command.TargetKind, compensation);
            }

            return CommandResult.Success($"Moved group '{moved.Name}' to top level.", moved.Id, command.TargetKind, compensation);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }

    /// <summary>The group's own real name for a result message — its own Id's text when, somehow, the group named by an already-validated Id cannot be re-read.</summary>
    private async Task<string> GroupNameAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await _requirementsService.FindGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
        return group?.Name ?? groupId.ToString();
    }

    /// <summary>
    /// Dispatches a Move Group compensation to <paramref name="parentGroupId"/>,
    /// or refuses first — "a group since deleted" (`WP 21.6A`) — when that
    /// destination is no longer a live group.
    /// </summary>
    private async Task<CommandResult> MoveOrRefuseAsync(Guid targetObjectId, Guid? parentGroupId, CancellationToken cancellationToken)
    {
        if (parentGroupId is { } id)
        {
            var group = await _requirementsService.FindGroupAsync(id, cancellationToken).ConfigureAwait(false);
            if (group is null || group.IsDeleted)
                return CommandResult.Failure($"'{id}' no longer exists.");
        }

        return await _dispatcher!.DispatchAsync(new MoveRequirementGroupCommand(targetObjectId, parentGroupId), cancellationToken).ConfigureAwait(false);
    }
}
