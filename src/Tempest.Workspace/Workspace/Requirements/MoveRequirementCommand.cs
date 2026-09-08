using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

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
            var moved = await _requirementsService.MoveToGroupAsync(command.TargetObjectId, command.NewGroupId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success(command.NewGroupId is { } groupId
                ? $"Moved '{moved.Identifier}' into group '{groupId}'."
                : $"Ungrouped '{moved.Identifier}'.");
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
}
