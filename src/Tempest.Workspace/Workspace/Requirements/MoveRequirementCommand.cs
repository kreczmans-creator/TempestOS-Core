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

    public MoveRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
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

            var moved = await _requirementsService.MoveToGroupAsync(command.TargetObjectId, command.NewGroupId, cancellationToken).ConfigureAwait(false);

            if (command.NewGroupId is { } groupId)
            {
                var groupName = await GroupNameAsync(groupId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success($"Moved '{moved.Identifier}' into group '{groupName}'.", moved.Id, command.TargetKind);
            }

            return CommandResult.Success($"Ungrouped '{moved.Identifier}'.", moved.Id, command.TargetKind);
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
}
