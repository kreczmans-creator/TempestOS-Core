using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Sets one Requirement's own current priority (<see cref="IRequirementsService.SetPriorityAsync"/>).</summary>
public sealed class SetRequirementPriorityCommand : IWorkspaceCommand
{
    public SetRequirementPriorityCommand(Guid targetObjectId, RequirementPriority? priority)
    {
        TargetObjectId = targetObjectId;
        Priority = priority;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new priority, or <see langword="null"/> to clear it.</summary>
    public RequirementPriority? Priority { get; }
}

/// <summary>Handles <see cref="SetRequirementPriorityCommand"/>.</summary>
public sealed class SetRequirementPriorityCommandHandler : ICommandHandler<SetRequirementPriorityCommand>
{
    private readonly IRequirementsService _requirementsService;

    public SetRequirementPriorityCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(SetRequirementPriorityCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _requirementsService.SetPriorityAsync(command.TargetObjectId, command.Priority, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Priority set to '{updated.Priority?.ToString() ?? "(none)"}' for '{updated.Identifier}'.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
