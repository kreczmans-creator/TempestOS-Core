using System.Reflection;
using Tempest.App.Workspace;
using Tempest.Companion.Contracts;
using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Composition;

/// <summary>
/// Projects the Engineering Workspace's own existing read models — the
/// Engineering Cockpit (`ADR-0069`) and the Engineering Domain
/// repositories — into the Companion wire DTOs
/// (<c>Tempest.Companion.Contracts</c>). A pure projection layer: every
/// value below is read from the identical source the desktop Cockpit
/// already renders (`WP 14.0A`'s own "one authoritative read model, three
/// presentation surfaces" requirement) — never a second, competing
/// computation of health, attention, or activity.
/// </summary>
/// <remarks>
/// Deliberately <c>internal</c>: the Companion API's server side is a
/// composition-layer concern reached only through
/// <see cref="CompanionApiRegistration"/>, mirroring how
/// <see cref="Workspace.Cockpit"/> itself is internal
/// (`WP 8.1C`'s own precedent).
/// </remarks>
internal sealed class CompanionQueryService
{
    /// <summary>The Document-family Kinds the pending-review list (and the set-document-status action) covers — the identical three Kinds <c>DocumentsCockpitReadModel</c> already reads.</summary>
    internal static readonly string[] DocumentKinds = ["Document", "Drawing", "CadModel"];

    private static readonly string PlatformVersion =
        typeof(CompanionQueryService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion is { Length: > 0 } informational
            ? informational.Split('+')[0]
            : "unknown";

    private readonly Workspace.Workspace _workspace;
    private readonly EngineeringDomainContext _domainContext;

    /// <summary>Initialises a new instance of the <see cref="CompanionQueryService"/> class.</summary>
    public CompanionQueryService(Workspace.Workspace workspace, EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(domainContext);

        _workspace = workspace;
        _domainContext = domainContext;
    }

    /// <summary>Builds the complete Cockpit summary — every region read from the one <see cref="EngineeringCockpit"/> the desktop renders.</summary>
    public CockpitSummaryDto BuildCockpitSummary()
    {
        var cockpit = _workspace.Cockpit;

        return new CockpitSummaryDto(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PlatformVersion: PlatformVersion,
            ProjectName: cockpit.ProjectName,
            Health: cockpit.Health.ToString(),
            HealthScoreDisplay: cockpit.HealthScoreDisplay,
            DisciplineStatuses: BuildDisciplineStatuses(cockpit),
            KpiCards: cockpit.KpiCards.Select(k => new KpiCardDto(k.Label, k.Value, k.IsPlaceholder, k.PercentValue)).ToList(),
            AttentionItems: cockpit.AttentionItems.Select(a => new AttentionItemDto(a.Title, a.Detail)).ToList(),
            OpenDecisions: cockpit.OpenDecisions,
            BlockedItems: cockpit.BlockedItems,
            OpenTaskCount: cockpit.OpenTaskCount,
            UpcomingMilestones: cockpit.UpcomingMilestones,
            RiskSummary: cockpit.RiskSummary,
            DigitalThreadSummary: cockpit.DigitalThreadSummary,
            ContinueWhereILeftOff: ToRecentItem(cockpit.ContinueWhereILeftOff),
            RecentActivity: cockpit.RecentActivity.Select(r => ToRecentItem(r)!).ToList(),
            RecentProjects: cockpit.RecentProjects);
    }

    /// <summary>Builds the live Project list — a real Engineering Domain repository read, most recently created first.</summary>
    public async Task<ProjectListDto> BuildProjectListAsync(CancellationToken cancellationToken)
    {
        var projects = new List<ProjectSummaryDto>();

        foreach (var candidate in await _domainContext.Repository.ListByKindAsync("Project", cancellationToken).ConfigureAwait(false))
        {
            if (candidate is IDeletable { IsDeleted: true } || candidate is not IProject project)
                continue;

            var identified = (IHasBusinessIdentifier)project;
            var outgoing = await _domainContext.RelationshipRepository.GetOutgoingAsync(project.Id, cancellationToken).ConfigureAwait(false);

            projects.Add(new ProjectSummaryDto(
                Id: project.Id,
                DisplayName: identified.DisplayName,
                Identifier: identified.Identifier,
                Status: project is IHasLifecycle lifecycle ? lifecycle.Status.ToString() : LifecycleState.Draft.ToString(),
                CreatedAtUtc: project.CreatedAt,
                CurrentRevisionNumber: project.CurrentRevisionNumber,
                OutgoingLinkCount: outgoing.Count));
        }

        return new ProjectListDto(
            DateTimeOffset.UtcNow,
            projects.OrderByDescending(p => p.CreatedAtUtc).ToList());
    }

    /// <summary>Builds the triage summary — the Cockpit's own attention regions plus the actionable pending-review list.</summary>
    public async Task<AttentionDto> BuildAttentionAsync(CancellationToken cancellationToken)
    {
        var cockpit = _workspace.Cockpit;

        return new AttentionDto(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            AttentionItems: cockpit.AttentionItems.Select(a => new AttentionItemDto(a.Title, a.Detail)).ToList(),
            BlockedItems: cockpit.BlockedItems,
            OpenDecisions: cockpit.OpenDecisions,
            OpenTaskCount: cockpit.OpenTaskCount,
            UpcomingMilestones: cockpit.UpcomingMilestones,
            PendingReviews: await BuildPendingReviewsAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <summary>Builds the recent-activity list — a real read of the Workspace's own recent navigation items.</summary>
    public ActivityDto BuildActivity() =>
        new(
            DateTimeOffset.UtcNow,
            _workspace.Cockpit.RecentActivity.Select(r => ToRecentItem(r)!).ToList());

    private async Task<List<PendingReviewDto>> BuildPendingReviewsAsync(CancellationToken cancellationToken)
    {
        var pending = new List<PendingReviewDto>();

        foreach (var kind in DocumentKinds)
        {
            foreach (var candidate in await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false))
            {
                if (candidate is IDeletable { IsDeleted: true })
                    continue;

                if (candidate is not IHasLifecycle { Status: LifecycleState.InReview } lifecycle || candidate is not IHasBusinessIdentifier identified)
                    continue;

                pending.Add(new PendingReviewDto(candidate.Id, candidate.Kind, identified.DisplayName, lifecycle.Status.ToString()));
            }
        }

        return pending;
    }

    private static List<DisciplineStatusDto> BuildDisciplineStatuses(EngineeringCockpit cockpit) =>
    [
        new("Requirements", cockpit.RequirementsStatus.ToString()),
        new("Calculations", cockpit.CalculationStatus.ToString()),
        new("Verification", cockpit.VerificationStatus.ToString()),
        new("Documentation", cockpit.DocumentationStatus.ToString()),
        new("Manufacturing", cockpit.ManufacturingStatus.ToString()),
        new("Review", cockpit.ReviewStatus.ToString()),
    ];

    private static RecentItemDto? ToRecentItem(RecentNavigationItem? item) =>
        item is null ? null : new RecentItemDto(item.ObjectId, item.Kind, item.Title, item.OpenedAt);
}
