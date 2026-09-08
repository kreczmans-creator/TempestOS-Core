using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Sets the same priority on every requirement in a set — mirrors <see cref="BulkSetRequirementStatusCommand"/>'s own identical shape, applied to <see cref="IRequirementsService.SetPriorityAsync"/> instead.</summary>
public sealed class BulkSetRequirementPriorityCommand : ICommand
{
    public BulkSetRequirementPriorityCommand(IReadOnlyList<Guid> requirementIds, RequirementPriority? priority)
    {
        ArgumentNullException.ThrowIfNull(requirementIds);

        RequirementIds = requirementIds;
        Priority = priority;
    }

    /// <summary>Gets every requirement to set the priority on.</summary>
    public IReadOnlyList<Guid> RequirementIds { get; }

    /// <summary>Gets the new priority, or <see langword="null"/> to clear it on every item.</summary>
    public RequirementPriority? Priority { get; }
}

/// <summary>Handles <see cref="BulkSetRequirementPriorityCommand"/>. Mirrors <see cref="BulkSetRequirementStatusCommandHandler"/>'s own identical per-item, never-stop-early, aggregate-result shape.</summary>
public sealed class BulkSetRequirementPriorityCommandHandler : ICommandHandler<BulkSetRequirementPriorityCommand>
{
    private readonly IRequirementsService _requirementsService;

    public BulkSetRequirementPriorityCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(BulkSetRequirementPriorityCommand command, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var failures = new List<string>();

        foreach (var requirementId in command.RequirementIds)
        {
            try
            {
                await _requirementsService.SetPriorityAsync(requirementId, command.Priority, cancellationToken).ConfigureAwait(false);
                succeeded++;
            }
            catch (RequirementNotFoundException ex)
            {
                failures.Add(ex.Message);
            }
        }

        var summary = $"Priority set to '{command.Priority?.ToString() ?? "(none)"}' for {succeeded}/{command.RequirementIds.Count} requirement(s).";

        return failures.Count == 0
            ? CommandResult.Success(summary)
            : CommandResult.Failure($"{summary} Failures: {string.Join("; ", failures)}");
    }
}
