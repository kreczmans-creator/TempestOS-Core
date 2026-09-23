using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Quotations;
using Tempest.Core.Tasks;

namespace Tempest.Core.Projects;

/// <summary>Why an <see cref="IProjectLifecycleService"/> act was refused, or <see cref="None"/> if it was not.</summary>
/// <remarks>
/// A refusal is a first-class answer here, exactly as
/// <c>Tempest.Core.Quotations.QuotationRefusal</c> is (`WP 19.5C`).
/// </remarks>
public enum ProjectLifecycleRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>No project is registered under the requested id.</summary>
    ProjectNotFound,

    /// <summary>The project is already on hold.</summary>
    AlreadyHeld,

    /// <summary>The project is not on hold, so it cannot be resumed.</summary>
    NotHeld,

    /// <summary>The project is closed, so it cannot be put on hold or resumed.</summary>
    ProjectClosed,

    /// <summary>The project is already closed, so it cannot be signed off again.</summary>
    AlreadyClosed,

    /// <summary>The project is not closed, so it cannot be reopened.</summary>
    NotClosed,

    /// <summary>The project closed 90 days or more ago — it is Archive, read-only, and cannot be reopened.</summary>
    ReopenWindowElapsed,

    /// <summary>
    /// The project carries a live deliverable with no completion, a live
    /// <see cref="Tasks.ManualTask"/> not done, or a live calculation not
    /// complete, and it is not carried by a live change order — sign-off is
    /// refused until every such item is complete or closed, or a change
    /// order carries it (`WP 20.10E`, Product Owner finding D18).
    /// </summary>
    WorkStillOpen,
}

/// <summary>The outcome of an <see cref="IProjectLifecycleService"/> act: either it happened, or a refusal that says why it did not.</summary>
/// <param name="Refusal">Why the act was refused, or <see cref="ProjectLifecycleRefusal.None"/>.</param>
/// <param name="Reason">The refusal in a sentence an engineer can read. <see langword="null"/> when nothing was refused.</param>
/// <param name="Project">The project acted on, when it could be resolved.</param>
public sealed record ProjectLifecycleResult(ProjectLifecycleRefusal Refusal, string? Reason, Project? Project)
{
    /// <summary>Whether the act actually happened.</summary>
    public bool Succeeded => Refusal == ProjectLifecycleRefusal.None;
}

/// <summary>
/// One live item <see cref="IProjectLifecycleService.SignOffAsync"/> counts
/// as still open against a project — a deliverable with no completion, a
/// <see cref="Tasks.ManualTask"/> not done, or a calculation not complete
/// (`WP 20.10E`, Product Owner finding D18).
/// </summary>
/// <param name="ObjectId">The open item's own id — with <see cref="Kind"/>, what opens it right up.</param>
/// <param name="Kind">The item's own canonical Kind — <c>"Deliverable"</c>, <see cref="Tasks.ManualTask.CanonicalKind"/>, or <c>"Calculation"</c>.</param>
/// <param name="Name">The item's own display name.</param>
/// <param name="CarriedByReference">
/// The <see cref="Quotation.Reference"/> of the live (not <see cref="QuotationStatus.Declined"/>)
/// change order that carries this item, or <see langword="null"/> when
/// nothing carries it — the only shape that still blocks sign-off. Only a
/// deliverable can ever be carried: a change order's own lines name a
/// deliverable by id (<see cref="QuotationLine.DeliverableId"/>), and a
/// <see cref="Tasks.ManualTask"/> or a calculation is never on a quotation
/// line, so this is always <see langword="null"/> for either — the two
/// ways out for those are the same "complete or close them" a deliverable
/// also has, minus the change-order escape.
/// </param>
public sealed record ProjectOpenWorkItem(Guid ObjectId, string Kind, string Name, string? CarriedByReference)
{
    /// <summary>Whether this item still blocks sign-off — carried items do not.</summary>
    public bool IsBlocking => CarriedByReference is null;
}

/// <summary>
/// A project's own lifecycle, distinct from the generic, unused
/// <see cref="LifecycleState"/> every <c>EngineeringObjectBase</c> carries
/// (`WP 19.5C`, Product Owner comment item 6): hold and resume (a pause,
/// not a close), sign off (closes the project, recording who, when and a
/// statement), and reopen (Closed only, within the 90-day window before a
/// project becomes Archive — see <see cref="ProjectArchival"/>). Every act
/// is one transaction with an audit row; whether an act is
/// <em>permitted</em> is decided here, before <see cref="Project"/>'s own
/// mutator ever runs, and reported back as a refusal result rather than an
/// exception, mirroring <c>Tempest.Core.Quotations.IQuotationService</c>.
/// </summary>
public interface IProjectLifecycleService
{
    /// <summary>Puts <paramref name="projectId"/> on hold, recording <paramref name="reason"/>. Refused, as a result, when the project is closed or already on hold.</summary>
    Task<ProjectLifecycleResult> HoldAsync(Guid projectId, string reason, CancellationToken cancellationToken = default);

    /// <summary>Resumes <paramref name="projectId"/> from hold. Refused, as a result, when the project is not on hold.</summary>
    Task<ProjectLifecycleResult> ResumeAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Signs off <paramref name="projectId"/>: records who (the current
    /// principal), when (today) and <paramref name="statement"/> as a
    /// <see cref="ProjectSignOff"/>, and closes the project.
    /// </summary>
    /// <remarks>
    /// Refused, as a result, when the project is already closed, or when it
    /// carries work still open against the quote — a live deliverable with
    /// no completion, a live <see cref="Tasks.ManualTask"/> not done, or a
    /// live calculation not complete — that no live change order carries
    /// (<see cref="ProjectLifecycleRefusal.WorkStillOpen"/>, `WP 20.10E`,
    /// Product Owner finding D18). <see cref="GetOpenWorkAsync"/> reads the
    /// identical list a UI can show before ever attempting to sign off.
    /// </remarks>
    Task<ProjectLifecycleResult> SignOffAsync(Guid projectId, string statement, CancellationToken cancellationToken = default);

    /// <summary>Reopens <paramref name="projectId"/> — Closed only, and only within <see cref="ProjectArchival.ArchiveAfterDays"/> days of closing.</summary>
    /// <remarks>Refused, as a result, when the project is not closed, or has already become Archive.</remarks>
    Task<ProjectLifecycleResult> ReopenAsync(Guid projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every live item still open against <paramref name="projectId"/> —
    /// the same set <see cref="SignOffAsync"/> itself reads, in one
    /// coherent snapshot, no writes (`WP 20.10E`, Product Owner finding
    /// D18). Empty when <paramref name="projectId"/> does not identify a
    /// live project, or when nothing is open.
    /// </summary>
    Task<IReadOnlyList<ProjectOpenWorkItem>> GetOpenWorkAsync(Guid projectId, CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="IProjectLifecycleService"/> implementation (`WP 19.5C`).</summary>
public sealed class ProjectLifecycleService : IProjectLifecycleService
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProjectLifecycleService"/> class.</summary>
    /// <param name="context">The domain context every write commits through.</param>
    /// <param name="timeProvider">The clock "today" and the 90-day archival window are read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public ProjectLifecycleService(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> HoldAsync(Guid projectId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.ProjectClosed, $"Project '{projectId}' is closed; it cannot be put on hold.", project);

        if (project.Held)
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.AlreadyHeld, $"Project '{projectId}' is already on hold ({project.HoldReason}).", project);

        await project.HoldAsync(reason.Trim(), cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> ResumeAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (!project.Held)
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.NotHeld, $"Project '{projectId}' is not on hold.", project);

        await project.ResumeAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> SignOffAsync(Guid projectId, string statement, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);

        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.AlreadyClosed, $"Project '{projectId}' is already closed (signed off {project.SignOff?.SignedOn:O}).", project);

        var openWork = await ComputeOpenWorkAsync(projectId, cancellationToken).ConfigureAwait(false);
        var blocking = openWork.Where(i => i.IsBlocking).ToList();

        if (blocking.Count > 0)
        {
            var itemList = string.Join("; ", blocking.Select(i => $"{i.Kind} '{i.Name}'"));
            return new ProjectLifecycleResult(
                ProjectLifecycleRefusal.WorkStillOpen,
                $"Project '{projectId}' has {blocking.Count} item(s) still open against the quote: {itemList}. "
                + "Complete or close them, or raise a change order that carries them, before signing off.",
                project);
        }

        var today = Today();
        var signOff = new ProjectSignOff(_context.ResolveCurrentPrincipalId(), today, statement.Trim());

        await project.SignOffAsync(signOff, today, cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectOpenWorkItem>> GetOpenWorkAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        if (await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false) is null)
            return [];

        return await ComputeOpenWorkAsync(projectId, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<ProjectLifecycleResult> ReopenAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var project = await FindProjectAsync(projectId, cancellationToken).ConfigureAwait(false);
        if (project is null)
            return NotFound(projectId);

        if (!ProjectArchival.IsClosed(project))
            return new ProjectLifecycleResult(ProjectLifecycleRefusal.NotClosed, $"Project '{projectId}' is not closed.", project);

        if (ProjectArchival.IsArchived(project, _time.GetUtcNow()))
        {
            return new ProjectLifecycleResult(
                ProjectLifecycleRefusal.ReopenWindowElapsed,
                $"Project '{projectId}' closed on {project.ClosedOn:O}, {ProjectArchival.ArchiveAfterDays} days or more ago; it is Archive and read-only. It cannot be reopened.",
                project);
        }

        await project.ReopenAsync(cancellationToken).ConfigureAwait(false);

        return new ProjectLifecycleResult(ProjectLifecycleRefusal.None, null, project);
    }

    /// <summary>
    /// The Kind string for a Deliverable — <c>Tempest.Workspace.CanonicalObjectKinds.Deliverable</c>'s
    /// own value, repeated here because <c>Tempest.Core</c> cannot
    /// reference <c>Tempest.Workspace</c>, mirroring
    /// <c>Tempest.Core.Deliverables.DeliverableService</c>'s own identical
    /// disclosure.
    /// </summary>
    private const string DeliverableKind = "Deliverable";

    /// <summary>
    /// The Kind string for a Calculation — <c>Tempest.Workspace.Calculations.CalculationObjectFactoryRegistry.CalculationKind</c>'s
    /// own value, repeated for the identical reason as <see cref="DeliverableKind"/>.
    /// </summary>
    private const string CalculationKind = "Calculation";

    /// <summary>
    /// The one snapshot <see cref="SignOffAsync"/> and
    /// <see cref="GetOpenWorkAsync"/> both read (`WP 20.10E`): every live
    /// deliverable with no completion, live <see cref="Tasks.ManualTask"/>
    /// not done, and live calculation not complete, under
    /// <paramref name="projectId"/> — each carrying the reference of the
    /// live (not <see cref="QuotationStatus.Declined"/>) change order that
    /// names it, when one does. Ordered by Kind then name, so the list is
    /// deterministic for a caller (a UI, a test) to read.
    /// </summary>
    private async Task<IReadOnlyList<ProjectOpenWorkItem>> ComputeOpenWorkAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var descendants = await ListLiveDescendantsAsync(projectId, cancellationToken).ConfigureAwait(false);

        var completedDeliverableIds = descendants
            .OfType<DeliverableCompletion>()
            .Select(c => c.DeliverableId)
            .ToHashSet();

        // A change order still carries what it names as long as it has not
        // been Declined — Draft or Sent or Accepted all count (`WP 20.10E`
        // scope item 1: "whose status is not Declined").
        var carryingChangeOrders = descendants
            .OfType<Quotation>()
            .Where(q => q.QuotationKind == QuotationKind.ChangeOrder && q.Status != QuotationStatus.Declined)
            .ToList();

        string? CarriedReferenceFor(Guid deliverableId) =>
            carryingChangeOrders.FirstOrDefault(co => co.Lines.Any(l => l.DeliverableId == deliverableId))?.Reference;

        var items = new List<ProjectOpenWorkItem>();

        foreach (var deliverable in descendants.OfType<Deliverable>())
        {
            if (completedDeliverableIds.Contains(deliverable.Id))
                continue;

            items.Add(new ProjectOpenWorkItem(deliverable.Id, DeliverableKind, deliverable.DisplayName, CarriedReferenceFor(deliverable.Id)));
        }

        foreach (var task in descendants.OfType<Tasks.ManualTask>())
        {
            if (task.Done)
                continue;

            // Never carriable — a ManualTask is not created from, or named
            // on, any quotation line (`WP 20.10E` scope item 1's own
            // distinction).
            items.Add(new ProjectOpenWorkItem(task.Id, Tasks.ManualTask.CanonicalKind, task.DisplayName, null));
        }

        foreach (var calculation in descendants.OfType<Calculation>())
        {
            if (calculation.Completed)
                continue;

            // Never carriable — see the ManualTask loop above.
            items.Add(new ProjectOpenWorkItem(calculation.Id, CalculationKind, calculation.DisplayName, null));
        }

        return items
            .OrderBy(i => i.Kind, StringComparer.Ordinal)
            .ThenBy(i => i.Name, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Every live object structurally under <paramref name="projectId"/>,
    /// transitively — a downward walk over <see cref="IEngineeringObjectRepository.ListChildrenAsync"/>
    /// rather than <c>Tempest.Workspace.Projects.ProjectMembership</c>'s own
    /// upward, whole-store walk (which <c>Tempest.Core</c> cannot reference
    /// anyway): a deliverable sits two hops down (project → milestone →
    /// deliverable), a calculation anywhere from one hop down to arbitrarily
    /// many (project → an Assembly/Part/Component/Calculation Set chain of
    /// any length → calculation), so the walk recurses through every
    /// discovered child rather than assuming a fixed depth. Bounded by a
    /// visited set the same way <c>ProjectMembership.ResolveOwningProjectAsync</c>
    /// bounds its own walk. A deleted node is still walked through — its own
    /// live children are still real project contents — but is left out of
    /// the returned list itself.
    /// </summary>
    private async Task<IReadOnlyList<IEngineeringObject>> ListLiveDescendantsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // `WP 21.5B` (`TD-88`), reconciled at merge: `ListChildrenAsync` answers
        // index rows now, so the walk reads `IsDeleted` from the row and
        // materialises only the live children it returns.
        var liveEntries = new List<EngineeringObjectIndexEntry>();
        var visited = new HashSet<Guid> { projectId };
        var frontier = new Queue<Guid>();
        frontier.Enqueue(projectId);

        while (frontier.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var parentId = frontier.Dequeue();
            var children = await _context.Repository.ListChildrenAsync(parentId, cancellationToken).ConfigureAwait(false);

            foreach (var child in children)
            {
                if (!visited.Add(child.Id))
                    continue;

                frontier.Enqueue(child.Id);

                if (child.IsDeleted)
                    continue;

                liveEntries.Add(child);
            }
        }

        return await _context.Repository.MaterialiseAsync<IEngineeringObject>(liveEntries, cancellationToken).ConfigureAwait(false);
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

    private async Task<Project?> FindProjectAsync(Guid projectId, CancellationToken cancellationToken) =>
        await _context.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(false) as Project;

    private static ProjectLifecycleResult NotFound(Guid projectId) =>
        new(ProjectLifecycleRefusal.ProjectNotFound, $"No project '{projectId}' is registered.", null);
}
