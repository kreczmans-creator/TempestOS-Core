using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Records a new revision of one Requirement's own statement (<see cref="IRequirementsService.ReviseAsync"/>).</summary>
public sealed class ReviseRequirementCommand : IWorkspaceCommand
{
    public ReviseRequirementCommand(Guid targetObjectId, string newStatement, string? changeSummary = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newStatement);

        TargetObjectId = targetObjectId;
        NewStatement = newStatement;
        ChangeSummary = changeSummary;
    }

    /// <inheritdoc />
    public Guid TargetObjectId { get; }

    /// <inheritdoc />
    public string TargetKind => RequirementsService.RequirementDocumentKind;

    /// <summary>Gets the requirement's own new statement.</summary>
    public string NewStatement { get; }

    /// <summary>Gets an optional summary of what changed and why.</summary>
    public string? ChangeSummary { get; }
}

/// <summary>Handles <see cref="ReviseRequirementCommand"/>.</summary>
public sealed class ReviseRequirementCommandHandler : ICommandHandler<ReviseRequirementCommand>
{
    private readonly IRequirementsService _requirementsService;

    public ReviseRequirementCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(ReviseRequirementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var revised = await _requirementsService.ReviseAsync(command.TargetObjectId, command.NewStatement, command.ChangeSummary, cancellationToken)
                .ConfigureAwait(false);

            return CommandResult.Success($"Revised '{revised.Identifier}' to revision {revised.RevisionNumber}.");
        }
        catch (RequirementNotFoundException ex)
        {
            return CommandResult.Failure(ex.Message);
        }
    }
}
