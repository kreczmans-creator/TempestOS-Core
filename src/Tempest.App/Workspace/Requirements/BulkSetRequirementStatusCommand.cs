using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>
/// Sets the same status on every requirement in a set — reads
/// <see cref="ISelectionService.SelectedItems"/> at the call site (a
/// Command Palette/context-menu concern, outside this command itself);
/// each item still dispatches through the existing single-item
/// <see cref="IRequirementsService.SetStatusAsync"/> internally, one at a
/// time — no new batch primitive in the Domain (`WP 9.1A`'s own "Bulk
/// editing" scope item, resolved this way). Plain <see cref="ICommand"/>,
/// not <see cref="IWorkspaceCommand"/> — it acts on many targets, none
/// more "the" target than another.
/// </summary>
public sealed class BulkSetRequirementStatusCommand : ICommand
{
    public BulkSetRequirementStatusCommand(IReadOnlyList<Guid> requirementIds, RequirementStatus status)
    {
        ArgumentNullException.ThrowIfNull(requirementIds);

        RequirementIds = requirementIds;
        Status = status;
    }

    /// <summary>Gets every requirement to set the status on.</summary>
    public IReadOnlyList<Guid> RequirementIds { get; }

    /// <summary>Gets the new status.</summary>
    public RequirementStatus Status { get; }
}

/// <summary>
/// Handles <see cref="BulkSetRequirementStatusCommand"/>. A per-item
/// failure (not found, or an invalid transition from that item's own
/// current status) does not stop the remaining items — every item is
/// attempted, and the aggregate result reports how many succeeded,
/// returning <see cref="CommandResult.Failure(string)"/> (with every
/// individual failure message) if at least one item failed.
/// </summary>
public sealed class BulkSetRequirementStatusCommandHandler : ICommandHandler<BulkSetRequirementStatusCommand>
{
    private readonly IRequirementsService _requirementsService;

    public BulkSetRequirementStatusCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(BulkSetRequirementStatusCommand command, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var failures = new List<string>();

        foreach (var requirementId in command.RequirementIds)
        {
            try
            {
                await _requirementsService.SetStatusAsync(requirementId, command.Status, cancellationToken).ConfigureAwait(false);
                succeeded++;
            }
            catch (RequirementNotFoundException ex)
            {
                failures.Add(ex.Message);
            }
            catch (InvalidRequirementStatusTransitionException ex)
            {
                failures.Add(ex.Message);
            }
        }

        var summary = $"Status set to '{command.Status}' for {succeeded}/{command.RequirementIds.Count} requirement(s).";

        return failures.Count == 0
            ? CommandResult.Success(summary)
            : CommandResult.Failure($"{summary} Failures: {string.Join("; ", failures)}");
    }
}
