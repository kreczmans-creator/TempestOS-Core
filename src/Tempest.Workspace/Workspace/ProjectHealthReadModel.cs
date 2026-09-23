using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Manufacturing;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace;

/// <summary>
/// The Engineering Cockpit's own per-Project health read-model
/// (`ADR-0155`): for every live Project, the same five discipline
/// statuses, the same rollup, the same score wording, and the same
/// blocked-item and overdue-action definitions the Cockpit reports
/// workspace-wide — computed over only the objects that Project owns.
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), never
/// DI-registered.
/// </summary>
/// <remarks>
/// <para>
/// <b>No second load, no second rule.</b> This read-model performs no
/// discipline read of its own. It takes the five discipline collaborators
/// the Cockpit has already primed and asks each for a
/// <c>ScopedTo(includes)</c> view — the identical class over a filtered
/// copy of the data it already loaded — so a Project's Verification
/// status is <see cref="VerificationCockpitReadModel.Status"/> run over
/// that Project's activities, by the same code the workspace figure
/// runs. The two cannot disagree in rule, only in scope.
/// </para>
/// <para>
/// <b>Membership is <see cref="ProjectMembership"/>, and costs one
/// in-memory parent-chain walk per candidate object.</b> The owning
/// Project of every object a discipline loaded (plus every object a live
/// requirement's relationships target, and every live Task) is resolved
/// once per <see cref="LoadAsync"/> through
/// <see cref="ProjectMembership.ResolveOwningProjectAsync"/> — the
/// platform's one definition of "belongs to a project". The repository
/// behind it is the in-memory object graph, so the walk is dictionary
/// lookups (O(objects × depth)), not persistence I/O; no projectId column
/// is added to the domain and no index is consulted. A requirement is in
/// a Project by <see cref="ProjectRequirementRegister"/>'s own rule
/// (any relationship targets a member), applied inside
/// <see cref="RequirementsCockpitReadModel.ScopedTo"/>.
/// </para>
/// </remarks>
internal sealed class ProjectHealthReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly MechanicalCockpitReadModel _mechanical;
    private readonly RequirementsCockpitReadModel _requirements;
    private readonly CalculationsCockpitReadModel _calculations;
    private readonly DocumentsCockpitReadModel _documents;
    private readonly VerificationCockpitReadModel _verification;
    private readonly ManufacturingCockpitReadModel _manufacturing;
    private readonly Func<DateTimeOffset> _now;
    private IReadOnlyList<CockpitProjectHealth> _projects = [];

    /// <summary>Initialises a new instance of the <see cref="ProjectHealthReadModel"/> class over the Cockpit's own already-constructed discipline collaborators.</summary>
    public ProjectHealthReadModel(
        EngineeringDomainContext domainContext,
        MechanicalCockpitReadModel mechanical,
        RequirementsCockpitReadModel requirements,
        CalculationsCockpitReadModel calculations,
        DocumentsCockpitReadModel documents,
        VerificationCockpitReadModel verification,
        ManufacturingCockpitReadModel manufacturing,
        Func<DateTimeOffset> now)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(mechanical);
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(calculations);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(verification);
        ArgumentNullException.ThrowIfNull(manufacturing);
        ArgumentNullException.ThrowIfNull(now);

        _domainContext = domainContext;
        _mechanical = mechanical;
        _requirements = requirements;
        _calculations = calculations;
        _documents = documents;
        _verification = verification;
        _manufacturing = manufacturing;
        _now = now;
    }

    /// <summary>Gets every live Project's own health — loaded by <see cref="LoadAsync"/>, in the same order <see cref="MechanicalCockpitReadModel.LiveProjects"/> lists them; honestly empty before the first call.</summary>
    public IReadOnlyList<CockpitProjectHealth> Projects => _projects;

    /// <summary>
    /// Computes every live Project's own health from the collaborators'
    /// already-loaded data — called by <see cref="EngineeringCockpit.PrimeAsync"/>
    /// after every discipline collaborator has loaded, never before.
    /// </summary>
    /// <param name="liveTasks">The Cockpit's own already-loaded live Task/Action set — the source <see cref="EngineeringCockpit.OverdueActions"/> reads.</param>
    /// <param name="cancellationToken">Cancels the membership walk.</param>
    public async Task LoadAsync(IReadOnlyList<ITask> liveTasks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(liveTasks);

        var projects = _mechanical.LiveProjects.OfType<IProject>().ToList();
        if (projects.Count == 0)
        {
            _projects = [];
            return;
        }

        var owners = await ResolveOwnersAsync(liveTasks, cancellationToken).ConfigureAwait(false);
        var asOf = _now();
        var results = new List<CockpitProjectHealth>(projects.Count);

        foreach (var project in projects)
        {
            var projectId = project.Id;
            bool Includes(Guid objectId) => owners.TryGetValue(objectId, out var owner) && owner == projectId;

            var requirements = _requirements.ScopedTo(Includes);
            var calculations = _calculations.ScopedTo(Includes);
            var documents = _documents.ScopedTo(Includes);
            var verification = _verification.ScopedTo(Includes);
            var manufacturing = _manufacturing.ScopedTo(Includes);

            // The same five, in the same order, EngineeringCockpit.Health rolls up.
            EngineeringHealthStatus[] statuses =
            [
                requirements.Status, calculations.Status, verification.Status, documents.Status, manufacturing.Status,
            ];

            // BlockedItems' own four contributors, and OverdueActions' own rule, scoped.
            var blocked = requirements.GetBlockedMessages().Count
                + calculations.GetBlockedMessages().Count
                + verification.GetBlockedMessages().Count
                + manufacturing.GetBlockedMessages().Count;

            var overdue = EngineeringHealthRollup.OverdueActions(liveTasks.Where(t => Includes(t.Id)), asOf).Count;

            results.Add(new CockpitProjectHealth(
                projectId,
                project.Identifier,
                project.DisplayName,
                project.Status,
                EngineeringHealthRollup.RollUp(statuses),
                requirements.Status,
                calculations.Status,
                verification.Status,
                documents.Status,
                manufacturing.Status,
                EngineeringHealthRollup.ScoreDisplay(statuses),
                blocked,
                overdue));
        }

        _projects = results;
    }

    /// <summary>One memoised <see cref="ProjectMembership.ResolveOwningProjectAsync"/> per candidate object — every object the five disciplines loaded, every object a live requirement's relationships target, and every live Task.</summary>
    private async Task<Dictionary<Guid, Guid?>> ResolveOwnersAsync(IReadOnlyList<ITask> liveTasks, CancellationToken cancellationToken)
    {
        var candidates = new HashSet<Guid>();
        candidates.UnionWith(_requirements.RelatedObjectIds);
        candidates.UnionWith(_calculations.LiveCalculations.Select(c => c.Id));
        candidates.UnionWith(_documents.LiveDocuments.Select(d => d.Id));
        candidates.UnionWith(_verification.LiveVerificationActivities.Select(a => a.Id));
        candidates.UnionWith(_manufacturing.LiveManufacturingObjects.Select(o => o.Id));
        candidates.UnionWith(liveTasks.Select(t => t.Id));

        var owners = new Dictionary<Guid, Guid?>(candidates.Count);
        foreach (var candidate in candidates)
        {
            owners[candidate] = await ProjectMembership
                .ResolveOwningProjectAsync(_domainContext.Repository, candidate, cancellationToken)
                .ConfigureAwait(false);
        }

        return owners;
    }
}
