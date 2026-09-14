using Tempest.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Workspace.Projects;

/// <summary>Creating and editing milestones and deliverables inside a project.</summary>
public interface IProjectMilestoneService
{
    /// <summary>Sets a milestone in <paramref name="projectId"/>.</summary>
    Task<Milestone> CreateMilestoneAsync(
        Guid projectId,
        string identifier,
        string title,
        DateTimeOffset targetDate,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Adds a deliverable due against <paramref name="milestoneId"/>.</summary>
    Task<Deliverable> CreateDeliverableAsync(
        Guid projectId,
        Guid milestoneId,
        string identifier,
        string title,
        string? description = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="milestoneId"/>.</summary>
    Task EditMilestoneAsync(Guid milestoneId, string? title = null, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>Retitles and/or rewrites <paramref name="deliverableId"/>.</summary>
    Task EditDeliverableAsync(Guid deliverableId, string? title = null, string? description = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Setting milestones and the deliverables due against them, as the Project
/// Workspace performs it.
/// </summary>
/// <remarks>
/// <para>
/// <b>No second milestone model, and no scheduler.</b> Every operation here
/// is performed on the real <see cref="Milestone"/> or
/// <see cref="Deliverable"/> in the domain, through the same
/// <see cref="EngineeringObjectFactory{T}"/> every other Kind is created by,
/// so a milestone set here survives a restart (`TD-85`/`TD-104`) without
/// this class knowing anything about persistence. Nothing here computes a
/// date, rolls one up from tasks, or reschedules anything: a milestone's
/// date is the one a person set.
/// </para>
/// <para>
/// <b>Project membership is set by parenting, not by a field.</b> Creating
/// a milestone in a project moves it under that project, so
/// <see cref="ProjectMembership"/> resolves it exactly as it resolves every
/// other object — the fifth family scoped this way, after documents,
/// requirements, tasks and the governance families.
/// </para>
/// <para>
/// <b>A deliverable is parented to its milestone, not to the project.</b>
/// That is what makes it a project deliverable transitively, and it means
/// the structure a user sees on the Timeline is the structure the domain
/// actually holds rather than a presentation-only grouping.
/// </para>
/// <para>
/// <b>Disclosed limitation: a milestone's target date cannot be changed.</b>
/// <see cref="Milestone.TargetDate"/> is set at construction and has no
/// setter, so rescheduling is not offered rather than being faked. Adding
/// one is a domain change this Work Package was not asked to make, and
/// pretending to reschedule while silently doing nothing would be worse
/// than the absence.
/// </para>
/// </remarks>
public sealed class ProjectMilestoneService : IProjectMilestoneService
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProjectMilestoneService"/> class.</summary>
    /// <param name="timeProvider">The clock the archived-project guard reads "now" from (`WP 19.10H`, `TD-179`). <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public ProjectMilestoneService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<Milestone> CreateMilestoneAsync(
        Guid projectId,
        string identifier,
        string title,
        DateTimeOffset targetDate,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var project = await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectNotFoundException(projectId);

        if (project is Project realProject && ProjectArchival.IsArchived(realProject, _time.GetUtcNow()))
        {
            throw new InvalidOperationException(
                $"Project '{projectId}' is archived (closed {realProject.ClosedOn:O}); no new milestone can be set on it.");
        }

        var factory = new EngineeringObjectFactory<Milestone>(
            CanonicalObjectKinds.Milestone,
            _context,
            (document, revision) => new Milestone(
                document, revision, _context, identifier.Trim(), title.Trim(),
                EngineeringObjectMetadata.Empty, targetDate));

        var created = (Milestone)await factory
            .CreateAsync(description ?? $"Milestone {identifier.Trim()} — {title.Trim()}.", cancellationToken)
            .ConfigureAwait(false);

        await created.MoveAsync(project.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task<Deliverable> CreateDeliverableAsync(
        Guid projectId,
        Guid milestoneId,
        string identifier,
        string title,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var project = await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false)
            ?? throw new ProjectNotFoundException(projectId);

        if (project is Project realProject && ProjectArchival.IsArchived(realProject, _time.GetUtcNow()))
        {
            throw new InvalidOperationException(
                $"Project '{projectId}' is archived (closed {realProject.ClosedOn:O}); no new deliverable can be added to it.");
        }

        var milestone = await _context.Repository.FindAsync(milestoneId, cancellationToken).ConfigureAwait(false) as Milestone
            ?? throw new MilestoneNotFoundException(milestoneId);

        var factory = new EngineeringObjectFactory<Deliverable>(
            CanonicalObjectKinds.Deliverable,
            _context,
            (document, revision) => new Deliverable(
                document, revision, _context, identifier.Trim(), title.Trim(),
                EngineeringObjectMetadata.Empty, milestone.Id));

        var created = (Deliverable)await factory
            .CreateAsync(description ?? $"Deliverable {identifier.Trim()} — {title.Trim()}.", cancellationToken)
            .ConfigureAwait(false);

        // Parented to the milestone, so it reaches the project through it.
        await created.MoveAsync(milestone.Id, cancellationToken).ConfigureAwait(false);

        return created;
    }

    /// <inheritdoc />
    public async Task EditMilestoneAsync(Guid milestoneId, string? title = null, string? description = null, CancellationToken cancellationToken = default)
    {
        var milestone = await _context.Repository.FindAsync(milestoneId, cancellationToken).ConfigureAwait(false) as Milestone
            ?? throw new MilestoneNotFoundException(milestoneId);

        await EnsureNotArchivedAsync(milestone.Id, cancellationToken).ConfigureAwait(false);

        await EditAsync(milestone, title, description, "Milestone description edited.", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EditDeliverableAsync(Guid deliverableId, string? title = null, string? description = null, CancellationToken cancellationToken = default)
    {
        var deliverable = await _context.Repository.FindAsync(deliverableId, cancellationToken).ConfigureAwait(false) as Deliverable
            ?? throw new DeliverableNotFoundException(deliverableId);

        await EnsureNotArchivedAsync(deliverable.Id, cancellationToken).ConfigureAwait(false);

        await EditAsync(deliverable, title, description, "Deliverable description edited.", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The archived-project guard (`WP 19.10H`, `TD-179`): every mutating command on an object belonging to an archived project is refused, here, before its own mutator ever runs. Walks the parent chain — a Deliverable reaches its project through its Milestone, not directly.</summary>
    private async Task EnsureNotArchivedAsync(Guid objectId, CancellationToken cancellationToken)
    {
        if (await ProjectMembership.ResolveOwningProjectAsync(_context.Repository, objectId, cancellationToken).ConfigureAwait(false) is not { } projectId
            || await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) is not Project project
            || !ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return;
        }

        throw new InvalidOperationException($"Project '{projectId}' is archived (closed {project.ClosedOn:O}); it is read-only.");
    }

    /// <remarks>
    /// A rewritten description is a new revision, not an overwrite — what a
    /// milestone used to say is part of its history, and the platform
    /// already records that for every other object.
    /// </remarks>
    private static async Task EditAsync(
        EngineeringObjectBase target, string? title, string? description, string revisionNote, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(title))
            await target.RenameAsync(title.Trim(), cancellationToken).ConfigureAwait(false);

        if (description is not null)
            await target.ReviseAsync(description, revisionNote, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Thrown when a milestone operation names an object that is not a milestone.</summary>
public sealed class MilestoneNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="MilestoneNotFoundException"/> class.</summary>
    public MilestoneNotFoundException(Guid milestoneId)
        : base($"No milestone with Id '{milestoneId}' exists in this session.") => MilestoneId = milestoneId;

    /// <summary>The Id that did not resolve to a milestone.</summary>
    public Guid MilestoneId { get; }
}

/// <summary>Thrown when a deliverable operation names an object that is not a deliverable.</summary>
public sealed class DeliverableNotFoundException : InvalidOperationException
{
    /// <summary>Initialises a new instance of the <see cref="DeliverableNotFoundException"/> class.</summary>
    public DeliverableNotFoundException(Guid deliverableId)
        : base($"No deliverable with Id '{deliverableId}' exists in this session.") => DeliverableId = deliverableId;

    /// <summary>The Id that did not resolve to a deliverable.</summary>
    public Guid DeliverableId { get; }
}
