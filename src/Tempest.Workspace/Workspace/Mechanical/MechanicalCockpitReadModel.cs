using Tempest.Workspace;
using Tempest.Core.EngineeringDomain;

namespace Tempest.Workspace.Mechanical;

/// <summary>
/// The Mechanical Product Structure discipline's own Engineering Cockpit
/// read-model — extracted, `WP 12.0B` (`ADR-0103`), from
/// <see cref="EngineeringCockpit"/>'s own previous <c>LiveProjects</c>/
/// <c>ProjectName</c>/<c>RecentProjects</c> members, unmodified in
/// behaviour. A collaborator under `ADR-0103`: constructed once by
/// <see cref="EngineeringCockpit"/> (the composition root), declaring
/// only the one dependency it actually needs, never DI-registered, never
/// referencing <see cref="EngineeringCockpit"/> or any sibling
/// discipline collaborator back.
/// </summary>
/// <remarks>
/// Mechanical carries no <c>Status</c>/<c>KpiCards</c> member of its own
/// — confirmed directly against the pre-decomposition source: unlike
/// Requirements/Calculations/Documents/Verification/Manufacturing,
/// <see cref="EngineeringCockpit.Health"/>/<see cref="EngineeringCockpit.HealthScoreDisplay"/>
/// never included a Mechanical discipline status in their own rollup —
/// a pre-existing asymmetry this move preserves exactly, not a gap this
/// Work Package introduces or silently closes.
/// </remarks>
/// <remarks>
/// <b>`WP 18.1A-R1`.</b> The one persistence-backed read
/// (<c>Repository.ListByKindAsync</c>) now runs inside <see cref="LoadAsync"/>,
/// awaited once per Cockpit render (<see cref="EngineeringCockpit.PrimeAsync"/>)
/// rather than blocked on synchronously from every property access — see
/// that method's own remarks for why. Every property below is now a pure,
/// in-memory read of what <see cref="LoadAsync"/> last loaded, honestly
/// empty until the first call completes.
/// </remarks>
internal sealed class MechanicalCockpitReadModel
{
    private readonly EngineeringDomainContext _domainContext;
    private IReadOnlyList<IHasBusinessIdentifier> _liveProjects = [];

    /// <summary>Initialises a new instance of the <see cref="MechanicalCockpitReadModel"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this read-model queries directly.</param>
    public MechanicalCockpitReadModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
    }

    /// <summary>Loads every live (non-deleted) <c>Project</c> — the one read every property below is derived from.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var projects = await _domainContext.Repository.ListByKindAsync("Project", cancellationToken).ConfigureAwait(false);

        _liveProjects = projects
            .Where(o => o is not IDeletable { IsDeleted: true })
            .OfType<IHasBusinessIdentifier>()
            .ToList();
    }

    /// <summary>Gets every live (non-deleted) <c>Project</c>, newest-created first is not guaranteed — insertion order from the repository.</summary>
    public IReadOnlyList<IHasBusinessIdentifier> LiveProjects => _liveProjects;

    /// <summary>
    /// Gets the most-recently-created live Mechanical Product Structure
    /// <c>Project</c>'s own display name — a real read, honestly
    /// reporting "No Mechanical Project yet" if none exists.
    /// </summary>
    public string ProjectName => LiveProjects.Count > 0 ? LiveProjects[^1].DisplayName : "No Mechanical Project yet";

    /// <summary>Gets every live Mechanical Product Structure <c>Project</c>'s own display name — a real read; empty, honestly, if none exist yet.</summary>
    public IReadOnlyList<string> RecentProjects => LiveProjects.Select(p => p.DisplayName).ToList();

    /// <summary>
    /// Gets this discipline's own "What Needs Attention" contribution — a
    /// single base entry, always. Mechanical carries no conditional
    /// second entry of its own — confirmed directly against the
    /// pre-decomposition source, unlike Requirements/Calculations/
    /// Documents/Verification/Manufacturing.
    /// </summary>
    public IReadOnlyList<CockpitAttentionItem> GetAttentionItems() =>
    [
        LiveProjects.Count > 0
            ? new("Mechanical Product Structure is live", $"{LiveProjects.Count} Project(s) registered - the Project Explorer's own Mechanical area reflects real Engineering Domain data (WP 9.0A).")
            : new("No Mechanical Product Structure registered yet", "The Mechanical Product Structure area has no live Project yet - this is expected, not a defect."),
    ];
}
