using Tempest.Core.Commands;
using Tempest.Core.EngineeringData;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

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
            var moved = await _requirementsService.MoveGroupAsync(command.TargetObjectId, command.NewParentGroupId, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success(command.NewParentGroupId is { } parentId
                ? $"Moved group '{moved.Name}' under '{parentId}'."
                : $"Moved group '{moved.Name}' to top level.");
        }
        catch (EngineeringDocumentNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
