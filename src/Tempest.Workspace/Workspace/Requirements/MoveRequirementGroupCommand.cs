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

    public MoveRequirementGroupCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
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

            var moved = await _requirementsService.MoveGroupAsync(command.TargetObjectId, command.NewParentGroupId, cancellationToken).ConfigureAwait(false);

            if (command.NewParentGroupId is { } parentId)
            {
                var parentName = await GroupNameAsync(parentId, cancellationToken).ConfigureAwait(false);
                return CommandResult.Success($"Moved group '{moved.Name}' under '{parentName}'.", moved.Id, command.TargetKind);
            }

            return CommandResult.Success($"Moved group '{moved.Name}' to top level.", moved.Id, command.TargetKind);
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
}
