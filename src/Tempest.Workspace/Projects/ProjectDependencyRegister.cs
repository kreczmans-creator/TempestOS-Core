using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Projects;

/// <summary>One task waiting on another.</summary>
/// <param name="DependentId">The task that is waiting.</param>
/// <param name="DependentName">What it is called.</param>
/// <param name="DependentState">Where it has got to.</param>
/// <param name="DependsOnId">The task it is waiting on.</param>
/// <param name="DependsOnName">What that one is called.</param>
/// <param name="DependsOnState">Where that one has got to.</param>
public sealed record ProjectTaskDependency(
    Guid DependentId,
    string DependentName,
    TaskWorkState DependentState,
    Guid DependsOnId,
    string DependsOnName,
    TaskWorkState DependsOnState)
{
    /// <summary>Whether the task being waited on is still open.</summary>
    public bool IsBlocking => TaskWorkStates.IsOpen(DependsOnState);

    /// <summary>
    /// Whether work has started on something still waiting on open work.
    /// </summary>
    /// <remarks>
    /// Reported, never prevented. Starting before a predecessor finishes
    /// is a normal and often correct thing to do; what matters is that
    /// somebody can see it happened.
    /// </remarks>
    public bool IsStartedOutOfSequence => IsBlocking && !TaskWorkStates.IsOpen(DependentState);
}

/// <summary>Reads the dependencies between a project's tasks.</summary>
/// <remarks>
/// The read side of <see cref="TaskRelationshipKinds.DependsOn"/>. Follows
/// <see cref="ProjectMilestoneRegister"/>'s shape exactly: a register over
/// the existing domain, holding no state of its own and creating no
/// second project model (`ADR-0142`).
/// </remarks>
public interface IProjectDependencyRegister
{
    /// <summary>Every dependency between tasks in <paramref name="projectId"/>, blocking ones first.</summary>
    Task<IReadOnlyList<ProjectTaskDependency>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>The dependencies that are actually holding work up, most blocked first.</summary>
    Task<IReadOnlyList<ProjectTaskDependency>> ListBlockingAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProjectDependencyRegister"/> implementation.</summary>
public sealed class ProjectDependencyRegister : IProjectDependencyRegister
{
    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="ProjectDependencyRegister"/> class.</summary>
    /// <param name="context">The engineering domain.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ProjectDependencyRegister(EngineeringDomainContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectTaskDependency>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await ProjectMembership
            .ListProjectMembersAsync(_context.Repository, projectId, cancellationToken)
            .ConfigureAwait(false);

        var tasks = members.OfType<EngineeringTask>().ToDictionary(t => t.Id);
        var dependencies = new List<ProjectTaskDependency>();

        foreach (var task in tasks.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relationships = await task.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

            foreach (var link in relationships
                         .Where(r => string.Equals(r.RelationshipKind, TaskRelationshipKinds.DependsOn, StringComparison.Ordinal))
                         .Where(r => r.SourceId == task.Id))
            {
                // A dependency on something outside this project is
                // real and is not this register's to report: it would
                // show a task with no name and no state. The link
                // itself is untouched.
                if (!tasks.TryGetValue(link.TargetId, out var dependsOn))
                    continue;

                dependencies.Add(new ProjectTaskDependency(
                    task.Id,
                    task.DisplayName,
                    task.WorkState,
                    dependsOn.Id,
                    dependsOn.DisplayName,
                    dependsOn.WorkState));
            }
        }

        return
        [
            .. dependencies
                .OrderByDescending(d => d.IsBlocking)
                .ThenByDescending(d => d.IsStartedOutOfSequence)
                .ThenBy(d => d.DependentName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(d => d.DependentId),
        ];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectTaskDependency>> ListBlockingAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var all = await ListAsync(projectId, cancellationToken).ConfigureAwait(false);

        return [.. all.Where(d => d.IsBlocking)];
    }
}
