namespace Tempest.Companion.Contracts;

/// <summary>
/// One "What Needs Attention" entry — the wire form of the Cockpit's own
/// <c>CockpitAttentionItem</c> (`WP8.0C Engineering Cockpit Specification`
/// §3).
/// </summary>
/// <param name="Title">The entry's own short title.</param>
/// <param name="Detail">The entry's own supporting detail line.</param>
public sealed record AttentionItemDto(string Title, string Detail);

/// <summary>
/// One KPI card value — the wire form of the Cockpit's own
/// <c>CockpitKpiCard</c>, placeholder disclosure included: a placeholder
/// is rendered as one, never silently presented as real data.
/// </summary>
/// <param name="Label">The KPI's own label.</param>
/// <param name="Value">The KPI's own display value.</param>
/// <param name="IsPlaceholder">Whether this KPI is a disclosed placeholder rather than a real read.</param>
/// <param name="PercentValue">The KPI's own coverage percentage, or <see langword="null"/> for a non-coverage KPI.</param>
public sealed record KpiCardDto(string Label, string Value, bool IsPlaceholder, int? PercentValue);

/// <summary>One discipline's own health status — the wire form of <c>EngineeringHealthStatus</c>, carried as its enum name (<c>"Healthy"</c>/<c>"Attention"</c>/<c>"Blocked"</c>/<c>"Unknown"</c>).</summary>
/// <param name="Discipline">The discipline's own display name (e.g. <c>"Requirements"</c>).</param>
/// <param name="Status">The status name.</param>
public sealed record DisciplineStatusDto(string Discipline, string Status);

/// <summary>One recently opened/jumped-to object — the wire form of the Workspace's own <c>RecentNavigationItem</c>.</summary>
/// <param name="ObjectId">The object's own Id.</param>
/// <param name="Kind">The object's own Kind.</param>
/// <param name="Title">The object's own display title, captured when it was opened.</param>
/// <param name="OpenedAtUtc">When the object was most recently opened or jumped to.</param>
public sealed record RecentItemDto(Guid ObjectId, string Kind, string Title, DateTimeOffset OpenedAtUtc);

/// <summary>
/// The complete Cockpit summary — the Companion's mobile expression of
/// the Engineering Cockpit (`ADR-0069`): the same regions, projected once
/// server-side over the same read models the desktop Cockpit renders,
/// never a second, competing computation.
/// </summary>
/// <param name="GeneratedAtUtc">When this summary was computed — the client's staleness anchor.</param>
/// <param name="PlatformVersion">The serving platform's own version (the repository <c>VERSION</c> file, via the assembly's informational version).</param>
/// <param name="ProjectName">The Cockpit's own current project name line.</param>
/// <param name="Health">The overall health rollup, as an <c>EngineeringHealthStatus</c> name.</param>
/// <param name="HealthScoreDisplay">The Cockpit's own health score display text.</param>
/// <param name="DisciplineStatuses">Every discipline's own health status, in the Cockpit's own fixed order.</param>
/// <param name="KpiCards">The Engineering Health Summary's own cross-discipline KPI cards.</param>
/// <param name="AttentionItems">The "What Needs Attention" region's own entries.</param>
/// <param name="OpenDecisions">The "Open Decisions" region's own entries.</param>
/// <param name="BlockedItems">The "Blocked Items" region's own entries.</param>
/// <param name="OpenTaskCount">The live open Task/Action count — the Cockpit's own honest substitute for "overdue" (no due-date field exists in the Domain).</param>
/// <param name="UpcomingMilestones">The "Upcoming Milestones" region's own entries, soonest first.</param>
/// <param name="RiskSummary">The Risk Summary's own display text.</param>
/// <param name="DigitalThreadSummary">The Digital Thread Summary's own display text.</param>
/// <param name="ContinueWhereILeftOff">The most recent navigation item, or <see langword="null"/> if nothing has been opened this session.</param>
/// <param name="RecentActivity">Recent Workspace activity, most recent first.</param>
/// <param name="RecentProjects">Every live Project's own display name.</param>
public sealed record CockpitSummaryDto(
    DateTimeOffset GeneratedAtUtc,
    string PlatformVersion,
    string ProjectName,
    string Health,
    string HealthScoreDisplay,
    IReadOnlyList<DisciplineStatusDto> DisciplineStatuses,
    IReadOnlyList<KpiCardDto> KpiCards,
    IReadOnlyList<AttentionItemDto> AttentionItems,
    IReadOnlyList<string> OpenDecisions,
    IReadOnlyList<string> BlockedItems,
    int OpenTaskCount,
    IReadOnlyList<string> UpcomingMilestones,
    string RiskSummary,
    string DigitalThreadSummary,
    RecentItemDto? ContinueWhereILeftOff,
    IReadOnlyList<RecentItemDto> RecentActivity,
    IReadOnlyList<string> RecentProjects);

/// <summary>One live Project — the wire form of the Engineering Domain's own <c>IProject</c> facets a mobile list needs, never the whole object graph.</summary>
/// <param name="Id">The Project's own Id.</param>
/// <param name="DisplayName">The Project's own display name.</param>
/// <param name="Identifier">The Project's own business identifier, or <see langword="null"/> if none is set.</param>
/// <param name="Status">The Project's own lifecycle status, as a <c>LifecycleState</c> name.</param>
/// <param name="CreatedAtUtc">When the Project was created.</param>
/// <param name="CurrentRevisionNumber">The Project's own current revision number.</param>
/// <param name="OutgoingLinkCount">The Project's own direct outgoing relationship count — a Digital Thread direct-link read, never a multi-hop traversal.</param>
public sealed record ProjectSummaryDto(
    Guid Id,
    string DisplayName,
    string? Identifier,
    string Status,
    DateTimeOffset CreatedAtUtc,
    int CurrentRevisionNumber,
    int OutgoingLinkCount);

/// <summary>Every live Project.</summary>
/// <param name="GeneratedAtUtc">When this list was computed.</param>
/// <param name="Projects">The projects, most recently created first.</param>
public sealed record ProjectListDto(DateTimeOffset GeneratedAtUtc, IReadOnlyList<ProjectSummaryDto> Projects);

/// <summary>One Document-family object currently awaiting a review decision — an actionable item, carrying the identity the Companion's own quick action needs.</summary>
/// <param name="Id">The object's own Id.</param>
/// <param name="Kind">The object's own Kind (<c>"Document"</c>/<c>"Drawing"</c>/<c>"CadModel"</c>).</param>
/// <param name="DisplayName">The object's own display name.</param>
/// <param name="Status">The object's own lifecycle status, as a <c>LifecycleState</c> name.</param>
public sealed record PendingReviewDto(Guid Id, string Kind, string DisplayName, string Status);

/// <summary>
/// Everything currently requiring the user's attention — the Companion's
/// own triage surface, aggregating the identical Cockpit regions the
/// desktop shows plus the actionable pending-review list.
/// </summary>
/// <param name="GeneratedAtUtc">When this summary was computed.</param>
/// <param name="AttentionItems">The "What Needs Attention" region's own entries.</param>
/// <param name="BlockedItems">The "Blocked Items" region's own entries.</param>
/// <param name="OpenDecisions">The "Open Decisions" region's own entries.</param>
/// <param name="OpenTaskCount">The live open Task/Action count.</param>
/// <param name="UpcomingMilestones">The "Upcoming Milestones" region's own entries, soonest first.</param>
/// <param name="PendingReviews">Every Document-family object currently <c>InReview</c> — each actionable through the set-document-status action where the caller holds <see cref="CompanionPermissions.Act"/>.</param>
public sealed record AttentionDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<AttentionItemDto> AttentionItems,
    IReadOnlyList<string> BlockedItems,
    IReadOnlyList<string> OpenDecisions,
    int OpenTaskCount,
    IReadOnlyList<string> UpcomingMilestones,
    IReadOnlyList<PendingReviewDto> PendingReviews);

/// <summary>Recent meaningful Workspace activity.</summary>
/// <param name="GeneratedAtUtc">When this list was computed.</param>
/// <param name="RecentActivity">Recent activity, most recent first.</param>
public sealed record ActivityDto(DateTimeOffset GeneratedAtUtc, IReadOnlyList<RecentItemDto> RecentActivity);

/// <summary>One platform notification — the wire form of <c>IPlatformNotification</c> (<c>ADR-0046</c>).</summary>
/// <param name="OccurredAtUtc">When the notification occurred.</param>
/// <param name="Category">The notification's own free-form category.</param>
/// <param name="Severity">The severity, as a <c>NotificationSeverity</c> name.</param>
/// <param name="Message">The notification's own message.</param>
public sealed record NotificationDto(DateTimeOffset OccurredAtUtc, string Category, string Severity, string Message);

/// <summary>Recent platform notifications, most recent first — a bounded window observed since the serving Host started, never a durable notification store.</summary>
/// <param name="GeneratedAtUtc">When this list was computed.</param>
/// <param name="Notifications">The notifications, most recent first.</param>
public sealed record NotificationListDto(DateTimeOffset GeneratedAtUtc, IReadOnlyList<NotificationDto> Notifications);

/// <summary>
/// The set-document-status action's own request body — bound server-side
/// to the existing <c>SetDocumentStatusCommand</c> and dispatched through
/// the Command Framework (<c>ADR-0048</c>/<c>ADR-0114</c>).
/// </summary>
/// <param name="TargetObjectId">The target object's own Id.</param>
/// <param name="TargetKind">The target object's own Kind.</param>
/// <param name="Status">The new lifecycle status, as a <c>LifecycleState</c> name (e.g. <c>"Approved"</c>).</param>
public sealed record SetObjectStatusRequest(Guid TargetObjectId, string TargetKind, string Status);
