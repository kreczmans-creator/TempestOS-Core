using Tempest.Core.Commands;
using Tempest.Core.Requirements;

namespace Tempest.App.Workspace.Requirements;

/// <summary>Sets the same owner on every requirement in a set — mirrors <see cref="BulkSetRequirementStatusCommand"/>'s own identical shape, applied to <see cref="IRequirementsService.SetOwnerAsync"/> instead.</summary>
public sealed class BulkSetRequirementOwnerCommand : ICommand
{
    public BulkSetRequirementOwnerCommand(IReadOnlyList<Guid> requirementIds, string? owner)
    {
        ArgumentNullException.ThrowIfNull(requirementIds);

        RequirementIds = requirementIds;
        Owner = owner;
    }

    /// <summary>Gets every requirement to set the owner on.</summary>
    public IReadOnlyList<Guid> RequirementIds { get; }

    /// <summary>Gets the new owner, or <see langword="null"/> to clear it on every item.</summary>
    public string? Owner { get; }
}

/// <summary>Handles <see cref="BulkSetRequirementOwnerCommand"/>. Mirrors <see cref="BulkSetRequirementStatusCommandHandler"/>'s own identical per-item, never-stop-early, aggregate-result shape.</summary>
public sealed class BulkSetRequirementOwnerCommandHandler : ICommandHandler<BulkSetRequirementOwnerCommand>
{
    private readonly IRequirementsService _requirementsService;

    public BulkSetRequirementOwnerCommandHandler(IRequirementsService requirementsService)
    {
        ArgumentNullException.ThrowIfNull(requirementsService);

        _requirementsService = requirementsService;
    }

    public async Task<CommandResult> HandleAsync(BulkSetRequirementOwnerCommand command, CancellationToken cancellationToken)
    {
        var succeeded = 0;
        var failures = new List<string>();

        foreach (var requirementId in command.RequirementIds)
        {
            try
            {
                await _requirementsService.SetOwnerAsync(requirementId, command.Owner, cancellationToken).ConfigureAwait(false);
                succeeded++;
            }
            catch (RequirementNotFoundException ex)
            {
                failures.Add(ex.Message);
            }
        }

        var summary = $"Owner set to '{command.Owner ?? "(none)"}' for {succeeded}/{command.RequirementIds.Count} requirement(s).";

        return failures.Count == 0
            ? CommandResult.Success(summary)
            : CommandResult.Failure($"{summary} Failures: {string.Join("; ", failures)}");
    }
}
