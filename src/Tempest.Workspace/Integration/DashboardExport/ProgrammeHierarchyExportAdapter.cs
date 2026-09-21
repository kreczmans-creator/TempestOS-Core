using System.Globalization;
using System.Text.Json;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ExportImport;
using Tempest.Core.Navigation;
using Tempest.Core.Requirements;
using Tempest.Workspace.Mechanical;

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
    /// <summary>The schema version this adapter's own payload shape uses — the integration contract document's §2.2 (v1), plus the additive per-project <c>health</c>/<c>blockedCount</c>/<c>overdueActionCount</c> and <c>summary.byHealth</c> keys (`ADR-0151`) that make it v2.</summary>
    public const int CurrentSchemaVersion = 2;

    private readonly EngineeringDomainContext _domainContext;
    private readonly TimeProvider _time;
    private readonly EngineeringCockpit _cockpit;

    /// <summary>Initialises a new instance of the <see cref="ProgrammeHierarchyExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this adapter — and the <see cref="EngineeringCockpit"/> it constructs — queries directly.</param>
    /// <param name="requirementsService">The Requirements Framework's own service — passed straight through to the <see cref="EngineeringCockpit"/>, whose Requirements discipline needs it to compute a Project's health.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service — likewise passed straight through to the <see cref="EngineeringCockpit"/>.</param>
    /// <param name="navigationProvider">The Platform's own navigation provider — the one session-bound dependency the <see cref="EngineeringCockpit"/>'s <c>NavigationService</c> needs; nothing this export reads ever consults it (see <see cref="EngineeringStatusExportAdapter"/>'s own remarks).</param>
    /// <param name="commandRegistry">The Platform's own command registry — the <see cref="EngineeringCockpit"/>'s other session-bound dependency; likewise never consulted by anything exported.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <c>Tempest.Core.Evidence.EvidenceService</c>'s own identical convention. Also the clock each Project's own <c>overdueActionCount</c> is measured against.</param>
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

        var export = new ProgrammeExport(
            SchemaVersion,
            EngineeringStatusExportAdapter.FormatUtc(_time.GetUtcNow()),
            portfolios.Select(ToPortfolioEntry).ToList(),
            programmes.Select(ToProgrammeEntry).ToList(),
            liveProjects.Select(entry => ToProjectEntry(entry.Project, entry.Health)).ToList(),
            new ProgrammeSummary(
                portfolios.Count,
                programmes.Count,
                liveProjects.Count,
                CountsByStatus(liveProjects.Select(entry => entry.Project).ToList()),
                deletedProjectCount,
                CountsByHealth(liveProjects.Select(entry => entry.Health).ToList())));

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

    private static ProjectEntry ToProjectEntry(IProject project, CockpitProjectHealth health) => new(
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
        health.OverdueActionCount);

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
        ProjectHealthExport Health, int BlockedCount, int OverdueActionCount);

    private sealed record ProjectHealthExport(string Overall, Dictionary<string, string> ByDiscipline, string Score);

    private sealed record ProgrammeSummary(int PortfolioCount, int ProgrammeCount, int ProjectCount, Dictionary<string, int> ByStatus, int DeletedCount, Dictionary<string, int> ByHealth);
}
