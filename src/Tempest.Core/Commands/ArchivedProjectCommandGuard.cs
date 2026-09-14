using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;

namespace Tempest.Core.Commands;

/// <summary>
/// <c>TD-179</c>'s residual (`WP 19.10R`): the one place a mutating
/// engineering command is checked against the project it would mutate
/// being Archive — closed <see cref="ProjectArchival.ArchiveAfterDays"/>
/// days or more ago, and therefore read-only.
/// </summary>
/// <remarks>
/// <para>
/// <b>The gap this closes.</b> <c>WP 19.10H</c> guarded the milestone,
/// task, evidence and manual-task <em>services</em> directly, and disabled
/// every write control the Tasks/Timeline/Evidence/Quote tabs render. Two
/// routes still reached an archived project unchecked: the Structure tab's
/// Ribbon and the Command Palette, because
/// <see cref="ICommandRegistry.Evaluate"/> — the one place both surfaces
/// (and a macro replaying either) already ask "can this command run" —
/// carried no notion of a project's archived state at all. This type is
/// the one rule <see cref="CommandRegistry"/> now consults from that same
/// place, so a Ribbon button, a Palette row and a macro step all see the
/// identical refusal, for free, the moment
/// <see cref="CommandBinding.Mutates"/> is set on a binding.
/// </para>
/// <para>
/// <b>Which project a command would mutate.</b> A selected object's own
/// project is found by walking <see cref="IHasParent.ParentId"/> upward
/// until a <see cref="Project"/> is reached — the identical walk
/// <c>Tempest.Workspace.Projects.ProjectMembership.ResolveOwningProjectAsync</c>
/// already performs, re-stated here because <c>Tempest.Core</c> cannot
/// reference <c>Tempest.Workspace</c> (the command framework lives in
/// Core; that walk lives in the Workspace layer that depends on Core, not
/// the other way around) — and because a <see cref="Project"/> is
/// identified here by its own concrete type rather than by re-declaring
/// the <c>"Project"</c> Kind string a second time. With nothing selected,
/// a creation command's target is the shell's own open project
/// (<see cref="CommandContext.ProjectId"/>) — exactly what
/// <c>MechanicalCreateParentPolicy.Resolve</c> already treats as a
/// create's own placement when no container is selected.
/// </para>
/// <para>
/// <b>Only a binding that says it mutates is checked.</b> See
/// <see cref="CommandBinding.Mutates"/>'s own remarks for why the marker
/// defaults to <see langword="false"/> and exactly which bindings this
/// Work Package sets it on.
/// </para>
/// <para>
/// <b>Not a blocking call, despite the synchronous read.</b>
/// <see cref="ICommandRegistry.Evaluate"/> is a synchronous contract this
/// Work Package does not own — the Ribbon recomputes every button's
/// enablement on each selection change and the Palette re-evaluates on
/// every keystroke — so this guard cannot be made <c>async</c> without
/// widening this Work Package's scope to every caller of that contract
/// (and several run concurrently tonight). <see cref="EngineeringDomainContext.Repository"/>'s
/// one shipped implementation (<see cref="InMemoryEngineeringObjectRepository"/>)
/// always returns an already-completed <see cref="Task"/>
/// (<c>Task.FromResult</c> — <c>ADR-0145</c> keeps the object cache
/// entirely in memory, never a co-equal writer, so a read here never
/// touches SQLite), so <c>GetAwaiter().GetResult()</c> below returns
/// immediately rather than blocking — the identical, disclosed reasoning
/// <c>Tempest.Workspace.WorkspaceManager.ThrowIfHostRunFaulted</c> already
/// carries for its own such call.
/// </para>
/// </remarks>
public sealed class ArchivedProjectCommandGuard
{
    private readonly EngineeringDomainContext _context;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ArchivedProjectCommandGuard"/> class.</summary>
    /// <param name="context">Where a command's target project is read from.</param>
    /// <param name="timeProvider">
    /// The clock "is this project archived yet" is read against.
    /// <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>;
    /// a test supplies a controllable one.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public ArchivedProjectCommandGuard(EngineeringDomainContext context, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        _context = context;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Why a mutating command against <paramref name="context"/> must be
    /// refused, or <see langword="null"/> when the project it would mutate
    /// (if any) is not archived — consulted by
    /// <see cref="CommandRegistry.Evaluate(CommandDescriptor, CommandContext)"/>
    /// only for a binding whose own <see cref="CommandBinding.Mutates"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public string? FindReason(CommandContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var project = ResolveTargetProject(context);
        if (project is null)
            return null;

        return ProjectArchival.IsArchived(project, _time.GetUtcNow())
            ? $"Project '{project.Identifier}' is archived — read only."
            : null;
    }

    /// <summary>
    /// The selected object's own project, or — with nothing selected — the
    /// shell's open project. <see langword="null"/> when neither resolves
    /// to one (standalone engineering work, or no project open): a command
    /// with no project in play cannot mutate an archived one.
    /// </summary>
    private Project? ResolveTargetProject(CommandContext context) =>
        context.Primary is { } primary
            ? FindOwningProject(primary.ObjectId)
            : context.ProjectId is { } projectId ? Find(projectId) as Project : null;

    /// <summary>
    /// Walks <see cref="IHasParent.ParentId"/> upward from
    /// <paramref name="objectId"/> until a <see cref="Project"/> is
    /// reached, or the chain ends without one (standalone engineering
    /// work, or the object no longer exists). Bounded by a visited set —
    /// the graph this reads was not necessarily built by a mutator that
    /// itself guards against a cycle.
    /// </summary>
    private Project? FindOwningProject(Guid objectId)
    {
        var visited = new HashSet<Guid>();
        var current = objectId;

        while (visited.Add(current))
        {
            var found = Find(current);
            if (found is Project project)
                return project;

            if (found is not IHasParent { ParentId: { } parentId })
                return null;

            current = parentId;
        }

        return null;
    }

    // See class remarks: always an already-completed Task, never a
    // genuine block.
    private IEngineeringObject? Find(Guid id) => _context.Repository.FindAsync(id).GetAwaiter().GetResult();
}
