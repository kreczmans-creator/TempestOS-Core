using System.Globalization;
using System.Text.Json;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ExportImport;
using Tempest.Core.Navigation;
using Tempest.Core.Requirements;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Projects;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports a plain, dashboard-facing summary of the Programme Hierarchy
/// (Portfolio → Programme → Project) — <c>programme.json</c>, consumed by
/// Tempest-Dashboard's own push agent (the Core→Dashboard integration
/// contract, §2.2). Implements <see cref="IExportable"/>/
/// <see cref="IExportableKind"/> exactly as
/// <c>Tempest.Samples.RequirementExportAdapter</c> does — see
/// <see cref="EngineeringStatusExportAdapter"/>'s own remarks for why
/// <see cref="DashboardExportHostedService"/> calls <see cref="ExportAsync"/>
/// directly rather than through <see cref="IExportService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cockpit reuse (Product Owner instruction, 2026-09-20).</b> Core's
/// <c>"Project"</c> Kind is the exact same real class
/// (<c>Tempest.Core.EngineeringDomain.Project</c>, from the Programme
/// Hierarchy contract) the Mechanical discipline's own Cockpit read-model,
/// <see cref="MechanicalCockpitReadModel"/>, already loads as the root of
/// the Product Structure tree (confirmed directly: both
/// <c>MechanicalProductStructureSampleModule</c> and
/// <c>EngineeringDomainSampleModule</c> construct the identical
/// <c>Project</c> type under the identical <c>"Project"</c> Kind — one
/// object plays both roles, mechanical-tree root and programme-hierarchy
/// leaf, distinguished only by whether <c>Project.ProgrammeId</c> is set).
/// This adapter therefore reads the Cockpit's own live Project set —
/// from schema v2, through a headless <see cref="EngineeringCockpit"/>
/// (the identical composition root the desktop builds, constructed here
/// exactly as <see cref="EngineeringStatusExportAdapter"/> constructs
/// its own) whose <see cref="EngineeringCockpit.ProjectHealth"/> lists
/// exactly <see cref="MechanicalCockpitReadModel.LiveProjects"/> — for
/// <c>projects[]</c>/<c>summary.projectCount</c>/<c>summary.byStatus</c>,
/// so the desktop Cockpit's own Project Health card and this export's
/// own Project set can never silently disagree.
/// </para>
/// <para>
/// <b>Schema v2 — project health (`ADR-0151`).</b> Each <c>projects[]</c>
/// entry additionally carries <c>health</c> (<c>overall</c>,
/// <c>byDiscipline</c>, <c>score</c>), <c>blockedCount</c> and
/// <c>overdueActionCount</c>, and <c>summary</c> carries <c>byHealth</c>
/// — every value copied verbatim from <see cref="CockpitProjectHealth"/>,
/// the Cockpit's own health rollup scoped to that Project, in the same
/// lower-cased <see cref="EngineeringHealthStatus"/> words
/// <c>engineering-status.json</c>'s own <c>health</c> block uses
/// (<see cref="EngineeringStatusExportAdapter.FormatHealth"/>). Every v1
/// key is unchanged in name, shape and meaning.
/// </para>
/// <para>
/// <b>Schema v2 — open tasks (still v2, additive).</b> Each
/// <c>projects[]</c> entry additionally carries <c>tasks[]</c> — that
/// Project's <em>open</em> tasks (<see cref="ProjectTaskEntry.IsOpen"/>:
/// not Done, not Cancelled), read from the Project Workspace's own
/// <see cref="ProjectTaskRegister"/> (the identical read model behind the
/// desktop's <c>ProjectTasksView</c>, constructed here over the same
/// <see cref="EngineeringDomainContext"/> and the same clock the Cockpit
/// above measures <c>overdueActionCount</c> against, exactly as
/// <c>WorkspaceHost</c> constructs it for the desktop — the register is
/// not a DI service, there and here alike). Each task carries <c>id</c>,
/// <c>identifier</c>, <c>name</c>, <c>workState</c> and <c>priority</c>
/// (the <see cref="TaskWorkState"/>/<see cref="WorkPriority"/> word,
/// camelCased through <see cref="EngineeringStatusExportAdapter.FormatEnum"/>
/// — the same formatter <c>health</c> uses), <c>assignedTo</c>,
/// <c>dueDate</c> (<c>yyyy-MM-dd</c>, as the desktop shows it),
/// <c>isOverdue</c> and <c>contributesTo</c> (the milestone/deliverable
/// text the desktop's task card shows after "Contributes to":
/// <c>{Kind} “{DisplayName}”</c>). Ordered overdue first, then Blocked,
/// then by due date (undated last), then by priority (Critical first),
/// then by name — the register's own order with Blocked lifted above the
/// rest, so the dashboard's Tasks view reads top-down as "what needs
/// chasing". <c>summary.tasks</c> (<c>open</c>, <c>overdue</c>,
/// <c>blocked</c>) totals them across every live Project. The schema
/// version stays 2: this is additive within the version introduced by
/// `ADR-0151` on the same day, which no consumer has yet read, so no
/// consumer can be broken by it. Each Project's own <c>overdueActionCount</c>
/// (the Cockpit's figure) and the count of its <c>tasks[]</c> with
/// <c>isOverdue</c> agree by construction — both apply
/// <c>EngineeringTask.IsOverdue</c> to the live Task/Action members
/// <c>ProjectMembership</c> resolves to that Project, as of the same
/// instant — and the tests hold them to it.
/// </para>
/// <para>
/// <b>No Cockpit equivalent exists</b> for Portfolio/Programme (no
/// discipline Cockpit read-model, and no member of
/// <c>EngineeringCockpit</c> itself, ever reads the <c>"Portfolio"</c>/
/// <c>"Programme"</c> Kinds — confirmed by direct search) or for
/// <c>summary.deletedCount</c> (the Cockpit's own <c>LiveProjects</c>
/// deliberately excludes deleted Projects — that is what "live" means
/// throughout every Cockpit read-model — so a deleted-Project count has no
/// Cockpit source to reuse and is read fresh, directly, from the same
/// <c>"Project"</c> Kind). Both are implemented fresh, directly against
/// <see cref="EngineeringDomainContext"/>.
/// </para>
/// </remarks>
public sealed class ProgrammeHierarchyExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version this adapter's own payload shape uses — the integration contract document's §2.2 (v1), plus the additive per-project <c>health</c>/<c>blockedCount</c>/<c>overdueActionCount</c>/<c>tasks</c> and <c>summary.byHealth</c>/<c>summary.tasks</c> keys (`ADR-0151`) that make it v2.</summary>
    public const int CurrentSchemaVersion = 2;

    private readonly EngineeringDomainContext _domainContext;
    private readonly TimeProvider _time;
    private readonly EngineeringCockpit _cockpit;
    private readonly IProjectTaskRegister _tasks;

    /// <summary>Initialises a new instance of the <see cref="ProgrammeHierarchyExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this adapter — and the <see cref="EngineeringCockpit"/> it constructs — queries directly.</param>
    /// <param name="requirementsService">The Requirements Framework's own service — passed straight through to the <see cref="EngineeringCockpit"/>, whose Requirements discipline needs it to compute a Project's health.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service — likewise passed straight through to the <see cref="EngineeringCockpit"/>.</param>
    /// <param name="navigationProvider">The Platform's own navigation provider — the one session-bound dependency the <see cref="EngineeringCockpit"/>'s <c>NavigationService</c> needs; nothing this export reads ever consults it (see <see cref="EngineeringStatusExportAdapter"/>'s own remarks).</param>
    /// <param name="commandRegistry">The Platform's own command registry — the <see cref="EngineeringCockpit"/>'s other session-bound dependency; likewise never consulted by anything exported.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <c>Tempest.Core.Evidence.EvidenceService</c>'s own identical convention. Also the clock each Project's own <c>overdueActionCount</c> and each task's own <c>isOverdue</c> are measured against.</param>
    public ProgrammeHierarchyExportAdapter(
        EngineeringDomainContext domainContext,
        IRequirementsService requirementsService,
        IRequirementValidationService requirementValidationService,
        INavigationProvider navigationProvider,
        ICommandRegistry commandRegistry,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);
        ArgumentNullException.ThrowIfNull(navigationProvider);
        ArgumentNullException.ThrowIfNull(commandRegistry);

        _domainContext = domainContext;
        _time = timeProvider ?? TimeProvider.System;

        // The identical composition root WorkspaceManager.StartAsync builds
        // for the desktop, over a headless (empty) navigation session —
        // EngineeringStatusExportAdapter's own established shape.
        var navigationService = new NavigationService(navigationProvider, [], new WorkspaceContext());
        _cockpit = new EngineeringCockpit(
            navigationService, commandRegistry, domainContext, requirementsService, requirementValidationService,
            now: () => _time.GetUtcNow());

        // The Project Workspace's own task read model, over the same
        // domain and the same clock — exactly as WorkspaceHost constructs
        // it for the desktop's ProjectTasksView.
        _tasks = new ProjectTaskRegister(domainContext, () => _time.GetUtcNow());
    }

    /// <inheritdoc />
    public string Kind => "dashboard.programme";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var portfolios = (await _domainContext.Repository.ListByKindAsync("Portfolio", cancellationToken).ConfigureAwait(false))
            .OfType<IPortfolio>()
            .ToList();

        var programmes = (await _domainContext.Repository.ListByKindAsync("Programme", cancellationToken).ConfigureAwait(false))
            .OfType<IProgramme>()
            .ToList();

        // One coherent Cockpit pass per export — exactly what
        // CockpitView.RefreshAsync does once per desktop render. Its
        // ProjectHealth is the Cockpit's own live Project set, each with
        // its health already computed; the IProject behind each entry is
        // read back for the v1 identity/ownership fields.
        await _cockpit.PrimeAsync(cancellationToken).ConfigureAwait(false);

        var liveProjects = new List<(IProject Project, CockpitProjectHealth Health)>(_cockpit.ProjectHealth.Count);
        foreach (var health in _cockpit.ProjectHealth)
        {
            if (await _domainContext.Repository.FindAsync(health.ProjectId, cancellationToken).ConfigureAwait(false) is IProject project)
                liveProjects.Add((project, health));
        }

        var deletedProjectCount = (await _domainContext.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
            .OfType<IDeletable>()
            .Count(o => o.IsDeleted);

        var projectEntries = new List<ProjectEntry>(liveProjects.Count);
        foreach (var (project, health) in liveProjects)
            projectEntries.Add(ToProjectEntry(project, health, await ListOpenTasksAsync(project.Id, cancellationToken).ConfigureAwait(false)));

        var export = new ProgrammeExport(
            SchemaVersion,
            EngineeringStatusExportAdapter.FormatUtc(_time.GetUtcNow()),
            portfolios.Select(ToPortfolioEntry).ToList(),
            programmes.Select(ToProgrammeEntry).ToList(),
            projectEntries,
            new ProgrammeSummary(
                portfolios.Count,
                programmes.Count,
                liveProjects.Count,
                CountsByStatus(liveProjects.Select(entry => entry.Project).ToList()),
                deletedProjectCount,
                CountsByHealth(liveProjects.Select(entry => entry.Health).ToList()),
                SummariseTasks(projectEntries)));

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private static PortfolioEntry ToPortfolioEntry(IPortfolio portfolio) => new(
        portfolio.Id.ToString(),
        portfolio.Identifier,
        portfolio.DisplayName,
        JsonNamingPolicy.CamelCase.ConvertName(portfolio.Status.ToString()),
        portfolio.Owner,
        portfolio.ProgrammeIds.Select(id => id.ToString()).ToList());

    private static ProgrammeEntry ToProgrammeEntry(IProgramme programme) => new(
        programme.Id.ToString(),
        programme.Identifier,
        programme.DisplayName,
        JsonNamingPolicy.CamelCase.ConvertName(programme.Status.ToString()),
        programme.Owner,
        programme.PortfolioId?.ToString(),
        programme.ProjectIds.Select(id => id.ToString()).ToList());

    /// <summary><c>projects[].tasks</c> (schema v2): the Project's open tasks, from the Project Workspace's own <see cref="IProjectTaskRegister"/>, in dashboard order — overdue first, then Blocked, then soonest due (undated last), then most urgent, then by name.</summary>
    private async Task<IReadOnlyList<ProjectTaskExport>> ListOpenTasksAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var entries = await _tasks.ListAsync(projectId, cancellationToken).ConfigureAwait(false);

        return
        [
            .. entries
                .Where(e => e.IsOpen)
                .OrderByDescending(e => e.IsOverdue)
                .ThenByDescending(e => e.WorkState == TaskWorkState.Blocked)
                .ThenBy(e => e.DueDate ?? DateTimeOffset.MaxValue)
                .ThenByDescending(e => e.Priority)
                .ThenBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.ObjectId)
                .Select(ToTaskEntry),
        ];
    }

    private static ProjectTaskExport ToTaskEntry(ProjectTaskEntry entry) => new(
        entry.ObjectId.ToString(),
        entry.Identifier,
        entry.DisplayName,
        EngineeringStatusExportAdapter.FormatEnum(entry.WorkState),
        EngineeringStatusExportAdapter.FormatEnum(entry.Priority),
        entry.AssignedToPrincipalId,
        entry.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        entry.IsOverdue,
        entry.ContributesTo is { } target ? $"{target.Kind} “{target.DisplayName}”" : null);

    /// <summary><c>summary.tasks</c> (schema v2): open, overdue and Blocked task totals across every live Project's own <c>tasks[]</c>.</summary>
    private static TaskSummary SummariseTasks(IReadOnlyList<ProjectEntry> projects)
    {
        var tasks = projects.SelectMany(p => p.Tasks).ToList();
        var blocked = EngineeringStatusExportAdapter.FormatEnum(TaskWorkState.Blocked);

        return new TaskSummary(
            tasks.Count,
            tasks.Count(t => t.IsOverdue),
            tasks.Count(t => t.WorkState == blocked));
    }

    private static ProjectEntry ToProjectEntry(IProject project, CockpitProjectHealth health, IReadOnlyList<ProjectTaskExport> tasks) => new(
        project.Id.ToString(),
        project.Identifier,
        project.DisplayName,
        JsonNamingPolicy.CamelCase.ConvertName(project.Status.ToString()),
        project.Owner,
        project.Discipline,
        project.ProgrammeId?.ToString(),
        project.ParentId?.ToString(),
        project.IsDeleted,
        EngineeringStatusExportAdapter.FormatUtc(project.CreatedAt),
        ToProjectHealth(health),
        health.BlockedItemCount,
        health.OverdueActionCount,
        tasks);

    /// <summary><c>projects[].health</c> (schema v2): <see cref="CockpitProjectHealth"/>, verbatim — the same discipline keys and lower-cased words as <c>engineering-status.json</c>'s own <c>health</c> block.</summary>
    private static ProjectHealthExport ToProjectHealth(CockpitProjectHealth health) => new(
        EngineeringStatusExportAdapter.FormatHealth(health.Health),
        new Dictionary<string, string>
        {
            [CockpitDisciplines.Requirements] = EngineeringStatusExportAdapter.FormatHealth(health.RequirementsStatus),
            [CockpitDisciplines.Verification] = EngineeringStatusExportAdapter.FormatHealth(health.VerificationStatus),
            [CockpitDisciplines.Calculations] = EngineeringStatusExportAdapter.FormatHealth(health.CalculationStatus),
            [CockpitDisciplines.Documents] = EngineeringStatusExportAdapter.FormatHealth(health.DocumentationStatus),
            [CockpitDisciplines.Manufacturing] = EngineeringStatusExportAdapter.FormatHealth(health.ManufacturingStatus),
        },
        health.HealthScoreDisplay);

    /// <summary><c>summary.byHealth</c> (schema v2): live Projects per overall <see cref="EngineeringHealthStatus"/> word, every bucket always present, in the Cockpit's own severity order.</summary>
    private static Dictionary<string, int> CountsByHealth(IReadOnlyList<CockpitProjectHealth> projects)
    {
        var counts = new Dictionary<string, int>
        {
            [EngineeringStatusExportAdapter.FormatHealth(EngineeringHealthStatus.Healthy)] = 0,
            [EngineeringStatusExportAdapter.FormatHealth(EngineeringHealthStatus.Attention)] = 0,
            [EngineeringStatusExportAdapter.FormatHealth(EngineeringHealthStatus.Blocked)] = 0,
            [EngineeringStatusExportAdapter.FormatHealth(EngineeringHealthStatus.Unknown)] = 0,
        };

        foreach (var project in projects)
            counts[EngineeringStatusExportAdapter.FormatHealth(project.Health)]++;

        return counts;
    }

    private static Dictionary<string, int> CountsByStatus(IReadOnlyList<IProject> projects)
    {
        var counts = Enum.GetValues<LifecycleState>().ToDictionary(s => JsonNamingPolicy.CamelCase.ConvertName(s.ToString()), _ => 0);

        foreach (var project in projects)
            counts[JsonNamingPolicy.CamelCase.ConvertName(project.Status.ToString())]++;

        return counts;
    }

    private sealed record ProgrammeExport(
        int SchemaVersion,
        string GeneratedAt,
        IReadOnlyList<PortfolioEntry> Portfolios,
        IReadOnlyList<ProgrammeEntry> Programmes,
        IReadOnlyList<ProjectEntry> Projects,
        ProgrammeSummary Summary);

    private sealed record PortfolioEntry(string Id, string? Identifier, string Name, string Status, string? Owner, IReadOnlyList<string> ProgrammeIds);

    private sealed record ProgrammeEntry(string Id, string? Identifier, string Name, string Status, string? Owner, string? PortfolioId, IReadOnlyList<string> ProjectIds);

    private sealed record ProjectEntry(
        string Id, string? Identifier, string Name, string Status, string? Owner, string? Discipline,
        string? ProgrammeId, string? ParentId, bool IsDeleted, string LastUpdate,
        ProjectHealthExport Health, int BlockedCount, int OverdueActionCount, IReadOnlyList<ProjectTaskExport> Tasks);

    private sealed record ProjectHealthExport(string Overall, Dictionary<string, string> ByDiscipline, string Score);

    private sealed record ProjectTaskExport(
        string Id, string? Identifier, string Name, string WorkState, string Priority,
        string? AssignedTo, string? DueDate, bool IsOverdue, string? ContributesTo);

    private sealed record TaskSummary(int Open, int Overdue, int Blocked);

    private sealed record ProgrammeSummary(int PortfolioCount, int ProgrammeCount, int ProjectCount, Dictionary<string, int> ByStatus, int DeletedCount, Dictionary<string, int> ByHealth, TaskSummary Tasks);
}
