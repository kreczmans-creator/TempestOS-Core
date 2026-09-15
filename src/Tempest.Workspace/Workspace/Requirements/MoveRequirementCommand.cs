using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.Workspace.Requirements;

/// <summary>Moves one Requirement into a group, or ungroups it (<see cref="IRequirementsService.MoveToGroupAsync"/>).</summary>
public sealed class MoveRequirementCommand : IWorkspaceCommand
{
    public MoveRequirementCommand(Guid targetObjectId, Guid? newGroupId)
    {
        TargetObjectId = targetObjectId;
        NewGroupId = newGroupId;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the new group, or <see langword="null"/> to ungroup this requirement.</summary>
    public Guid? NewGroupId { get; }
}

/// <summary>Handles <see cref="MoveRequirementCommand"/>.</summary>
public sealed class MoveRequirementCommandHandler : ICommandHandler<MoveRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;
    private readonly ICommandDispatcher? _dispatcher;

    /// <param name="requirementsService">Where the requirement is moved.</param>
    /// <param name="dispatcher">Dispatches this move's own compensation (`WP 21.6A`) — optional.</param>
    public MoveRequirementCommandHandler(IRequirementsService requirementsService, ICommandDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
        _dispatcher = dispatcher;
    }

    public async Task<CommandResult> HandleAsync(MoveRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            // `WP 20.10C` (PO finding T6): read the requirement's own
            // current group before moving it, so choosing that same group
            // again reports "already there" rather than running (and then
            // reporting as an ordinary move) a revision that changes
            // nothing.
            var current = await _requirementsService.FindAsync(command.TargetObjectId, cancellationToken).ConfigureAwait(false)
                ?? throw new RequirementNotFoundException(command.TargetObjectId);

            if (current.GroupId == command.NewGroupId)
            {
                var already = command.NewGroupId is { } currentGroupId
                    ? $"Already in group '{await GroupNameAsync(currentGroupId, cancellationToken).ConfigureAwait(false)}'."
                    : "Already ungrouped.";
                return CommandResult.Success(already, command.TargetObjectId, command.TargetKind);
            }

            var previousGroupId = current.GroupId;
            var moved = await _requirementsService.MoveToGroupAsync(command.TargetObjectId, command.NewGroupId, cancellationToken).ConfigureAwait(false);

            // Undo moves back to the requirement's own previous group;
            // redo moves forward again — either direction refuses first
            // when its own destination group has since been deleted (`WP
            // 21.6A`, mirrors `WorkspaceCommandBindings.MoveCompensation`/
            // `MoveDestinationGoneReasonAsync`).
            var compensation = _dispatcher is null ? null : new CommandCompensation(
                $"Move '{moved.Identifier}'",
                undo: ct => MoveOrRefuseAsync(command.TargetObjectId, previousGroupId, ct),
                redo: ct => MoveOrRefuseAsync(command.TargetObjectId, command.NewGroupId, ct));

            if (command.NewGroupId is { } groupId)
            {
                var groupName = await GroupNameAsync(groupId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success($"Moved '{moved.Identifier}' into group '{groupName}'.", moved.Id, command.TargetKind, compensation);
            }

            return CommandResult.Success($"Ungrouped '{moved.Identifier}'.", moved.Id, command.TargetKind, compensation);
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }

    /// <summary>The group's own real name for a result message — its own Id's text when, somehow, the group named by a already-validated Id cannot be re-read.</summary>
    private async Task<string> GroupNameAsync(Guid groupId, CancellationToken cancellationToken)
    {
        var group = await _requirementsService.FindGroupAsync(groupId, cancellationToken).ConfigureAwait(false);
        return group?.Name ?? groupId.ToString();
    }

    /// <summary>
    /// Dispatches a Move compensation to <paramref name="groupId"/>, or
    /// refuses first — "a group since deleted" (`WP 21.6A`) — when that
    /// destination is no longer a live group.
    /// </summary>
    private async Task<CommandResult> MoveOrRefuseAsync(Guid targetObjectId, Guid? groupId, CancellationToken cancellationToken)
    {
        if (groupId is { } id)
        {
            var group = await _requirementsService.FindGroupAsync(id, cancellationToken).ConfigureAwait(false);
            if (group is null || group.IsDeleted)
                return CommandResult.Failure($"'{id}' no longer exists.");
        }

        return await _dispatcher!.DispatchAsync(new MoveRequirementCommand(targetObjectId, groupId), cancellationToken).ConfigureAwait(false);
    }
}
