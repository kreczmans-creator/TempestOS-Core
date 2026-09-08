using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Sets one Requirement's own current lifecycle status (<see cref="IRequirementsService.SetStatusAsync"/>).</summary>
public sealed class SetRequirementStatusCommand : IWorkspaceCommand
{
    public SetRequirementStatusCommand(Guid targetObjectId, RequirementStatus status)
    {
        TargetObjectId = targetObjectId;
        Status = status;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new status.</summary>
    public RequirementStatus Status { get; }
}

/// <summary>Handles <see cref="SetRequirementStatusCommand"/>.</summary>
public sealed class SetRequirementStatusCommandHandler : ICommandHandler<SetRequirementStatusCommand>
{
    private readonly IRequirementsService _requirementsService;

    public SetRequirementStatusCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(SetRequirementStatusCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _requirementsService.SetStatusAsync(command.TargetObjectId, command.Status, cancellationToken).ConfigureAwait(false);

            return CommandResult.Success($"Status set to '{command.Status}' for '{command.TargetObjectId}'.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
        catch (InvalidRequirementStatusTransitionException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
