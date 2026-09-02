using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Projects;

/// <summary>One deliverable due against a milestone, as the Timeline shows it.</summary>
/// <param name="ObjectId">The deliverable's own identity.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its name.</param>
/// <param name="Status">Its canonical lifecycle state.</param>
public sealed record ProjectMilestoneDeliverable(
    Guid ObjectId,
    string? Identifier,
    string DisplayName,
    LifecycleState Status);

/// <summary>One piece of work contributing to a milestone, as the Timeline shows it.</summary>
/// <param name="ObjectId">The task or action's own identity.</param>
/// <param name="Kind">Task or Action.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its title.</param>
/// <param name="WorkState">Where it is in someone's working week.</param>
/// <param name="ViaDeliverableId">
/// The deliverable this work reaches the milestone through, or
/// <see langword="null"/> when it contributes to the milestone directly.
/// </param>
public sealed record ProjectMilestoneContribution(
    Guid ObjectId,
    string Kind,
    string? Identifier,
    string DisplayName,
    TaskWorkState WorkState,
    Guid? ViaDeliverableId)
{
    /// <summary>Whether this work still needs doing.</summary>
    public bool IsOpen => TaskWorkStates.IsOpen(WorkState);

    /// <summary>Whether this work reaches the milestone through a deliverable rather than directly.</summary>
    public bool IsIndirect => ViaDeliverableId is not null;
}

/// <summary>One milestone belonging to a project, as the Timeline surface shows it.</summary>
/// <param name="ObjectId">The milestone's own identity.</param>
/// <param name="Identifier">Its business identifier, where it has one.</param>
/// <param name="DisplayName">Its name.</param>
/// <param name="Description">Its current revision content — what the milestone actually says.</param>
/// <param name="TargetDate">The date it is due.</param>
/// <param name="Status">Its canonical lifecycle state.</param>
/// <param name="IsPast">Whether <paramref name="TargetDate"/> has gone by.</param>
/// <param name="Deliverables">The deliverables due against it, in name order.</param>
/// <param name="Contributions">The tasks and actions contributing to it, directly or through one of its deliverables.</param>
public sealed record ProjectMilestoneEntry(
    Guid ObjectId,
    string? Identifier,
    string DisplayName,
    string Description,
    DateTimeOffset TargetDate,
    LifecycleState Status,
    bool IsPast,
    IReadOnlyList<ProjectMilestoneDeliverable> Deliverables,
    IReadOnlyList<ProjectMilestoneContribution> Contributions)
{
    /// <summary>How many contributing tasks still need doing.</summary>
    public int OpenContributionCount => Contributions.Count(c => c.IsOpen);

    /// <summary>Whether any work is linked to this milestone at all.</summary>
    /// <remarks>
    /// Asked directly rather than left as a count comparison, because a
    /// milestone nobody attached work to is a real and reportable state —
    /// it is a date with nothing behind it, which is exactly what a review
    /// is looking for.
    /// </remarks>
    public bool HasLinkedWork => Contributions.Count > 0 || Deliverables.Count > 0;

    /// <summary>Whether this milestone still has outstanding contributing work.</summary>
    public bool HasOutstandingWork => OpenContributionCount > 0;

    /// <summary>
    /// Whether this milestone's date has gone by while work against it is
    /// still outstanding.
    /// </summary>
    /// <remarks>
    /// <b>Deliberately not called "missed", and deliberately not derived
    /// from a completion flag.</b> The milestone model carries a target date
    /// and the canonical lifecycle, and nothing else — there is no
    /// "achieved" state to read, so claiming one would be inventing a fact.
    /// This says only what can be known: the date has passed and something
    /// linked to it is not finished.
    /// </remarks>
    public bool IsPastWithOutstandingWork => IsPast && HasOutstandingWork;

    /// <summary>
    /// Whether this milestone's date has gone by with nothing linked to it
    /// at all — a date the project set and then never attached work to.
    /// </summary>
    public bool IsPastWithNothingLinked => IsPast && !HasLinkedWork;
}

/// <summary>The milestones belonging to a project.</summary>
public interface IProjectMilestoneRegister
{
    /// <summary>Every milestone in <paramref name="projectId"/>, in date order.</summary>
    Task<IReadOnlyList<ProjectMilestoneEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>
/// The project's own milestone register — the read model behind the Project
/// Workspace's Timeline area.
/// </summary>
/// <remarks>
/// <para>
/// <b>Membership is <see cref="ProjectMembership"/>'s answer, not a second
/// one.</b> A milestone belongs to a project when the durable
/// <see cref="IHasParent"/> chain from it reaches that project, exactly as
/// documents, requirements, tasks and risks do. No <c>ProjectId</c> field
/// was added to the milestone model, for the same reason none was added to
/// the other four: it would be a competing answer to a question the
/// platform already answers.
/// </para>
/// <para>
/// <b>A read model, not a store, and not a scheduler.</b> It holds no
/// state, caches nothing, creates no persistence, and computes no schedule.
/// There is no critical path, no dependency graph, no rollup of dates from
/// tasks to milestones — a milestone's date is the one the project set, and
/// this reports it rather than deriving a different one.
/// </para>
/// <para>
/// <b>Both routes from work to a milestone are read, and distinguished.</b>
/// A task reaches a milestone either directly or through a deliverable,
/// because <see cref="TaskRelationshipKinds.ContributesTo"/> is one
/// relationship kind pointing at either — a <see cref="Deliverable"/>
/// already knows its own <see cref="IDeliverable.MilestoneId"/>, so the
/// second hop is a read rather than a second link. Which route a piece of
/// work took is kept on the entry rather than flattened away, so the
/// surface can show what a deliverable is actually carrying.
/// </para>
/// </remarks>
public sealed class ProjectMilestoneRegister : IProjectMilestoneRegister
{
    private readonly EngineeringDomainContext _context;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Initialises a new instance of the <see cref="ProjectMilestoneRegister"/> class.</summary>
    /// <param name="context">The engineering domain.</param>
    /// <param name="now">
    /// The clock "past" is measured against. Injectable so a test can state
    /// the date rather than depend on the day it runs — a date-sensitive
    /// test pinned to <c>DateTimeOffset.UtcNow</c> is a test that changes
    /// its own meaning overnight.
    /// </param>
    public ProjectMilestoneRegister(EngineeringDomainContext context, Func<DateTimeOffset>? now = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectMilestoneEntry>> ListAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var members = await ProjectMembership
            .ListProjectMembersAsync(_context.Repository, projectId, cancellationToken)
            .ConfigureAwait(false);

        var milestones = members.OfType<Milestone>().ToList();
        if (milestones.Count == 0)
            return [];

        // Read the project's deliverables and work once, then attribute
        // them, rather than walking the membership list per milestone.
        var deliverablesByMilestone = members
            .OfType<Deliverable>()
            .GroupBy(d => d.MilestoneId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var contributions = await ReadContributionsAsync(members, cancellationToken).ConfigureAwait(false);

        var asOf = _now();
        var entries = new List<ProjectMilestoneEntry>();

        foreach (var milestone in milestones)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var deliverables = deliverablesByMilestone.TryGetValue(milestone.Id, out var owned)
                ? owned
                : [];

            var deliverableIds = deliverables.Select(d => d.Id).ToHashSet();

            entries.Add(new ProjectMilestoneEntry(
                milestone.Id,
                milestone.Identifier,
                milestone.DisplayName,
                milestone.Content,
                milestone.TargetDate,
                milestone.Status,
                milestone.TargetDate < asOf,
                [
                    .. deliverables
                        .Select(d => new ProjectMilestoneDeliverable(d.Id, d.Identifier, d.DisplayName, d.Status))
                        .OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(d => d.ObjectId),
                ],
                [
                    .. contributions
                        .Where(c => c.TargetId == milestone.Id || deliverableIds.Contains(c.TargetId))
                        .Select(c => new ProjectMilestoneContribution(
                            c.Task.Id,
                            c.Task.Kind ?? string.Empty,
                            c.Task.Identifier,
                            c.Task.DisplayName,
                            c.Task.WorkState,
                            c.TargetId == milestone.Id ? null : c.TargetId))
                        .OrderBy(c => c.IsIndirect)
                        .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(c => c.ObjectId),
                ]));
        }

        // Chronological, because this is a timeline. A milestone with no
        // work and a milestone that is overdue both keep their place in the
        // sequence rather than being promoted — the order a user reads a
        // schedule in is the order the dates fall.
        return
        [
            .. entries
                .OrderBy(e => e.TargetDate)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId),
        ];
    }

    /// <summary>Every contributing link the project's own tasks and actions declare.</summary>
    /// <remarks>
    /// Read from the tasks, because the task is the side that writes the
    /// link (<see cref="EngineeringTask.ContributeToAsync"/>). Reading it
    /// from the milestone would mean querying incoming edges for every
    /// milestone in turn to answer the same question.
    /// </remarks>
    private static async Task<IReadOnlyList<(EngineeringTask Task, Guid TargetId)>> ReadContributionsAsync(
        IReadOnlyList<IEngineeringObject> members, CancellationToken cancellationToken)
    {
        var contributions = new List<(EngineeringTask, Guid)>();

        // EngineeringAction derives from EngineeringTask, so this reads both
        // — an action raised by a review contributes to a milestone exactly
        // as a planned task does, and hiding it would understate the work.
        foreach (var task in members.OfType<EngineeringTask>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relationships = await task.GetRelationshipsAsync(cancellationToken).ConfigureAwait(false);

            var link = relationships
                .Where(r => string.Equals(r.RelationshipKind, TaskRelationshipKinds.ContributesTo, StringComparison.Ordinal))
                .Where(r => r.SourceId == task.Id)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefault();

            if (link is not null)
                contributions.Add((task, link.TargetId));
        }

        return contributions;
    }
}
