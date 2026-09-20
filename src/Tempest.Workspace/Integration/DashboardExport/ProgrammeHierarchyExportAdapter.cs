using System.Globalization;
using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.ExportImport;
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
/// This adapter therefore constructs a <see cref="MechanicalCockpitReadModel"/>
/// itself (composition-root style, `new`, per `ADR-0103` — never
/// DI-registered) and reads its already-computed, already-tested
/// <see cref="MechanicalCockpitReadModel.LiveProjects"/> for
/// <c>projects[]</c>/<c>summary.projectCount</c>/<c>summary.byStatus</c>,
/// so the desktop Cockpit's own "Recent Projects" list and this export's
/// own Project set can never silently disagree.
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
    /// <summary>The schema version this adapter's own payload shape uses — the integration contract document's §2.2.</summary>
    public const int CurrentSchemaVersion = 1;

    private readonly EngineeringDomainContext _domainContext;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ProgrammeHierarchyExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this adapter — and the <see cref="MechanicalCockpitReadModel"/> it constructs — queries directly.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <c>Tempest.Core.Evidence.EvidenceService</c>'s own identical convention.</param>
    public ProgrammeHierarchyExportAdapter(EngineeringDomainContext domainContext, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);

        _domainContext = domainContext;
        _time = timeProvider ?? TimeProvider.System;
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

        var mechanical = new MechanicalCockpitReadModel(_domainContext);
        await mechanical.LoadAsync(cancellationToken).ConfigureAwait(false);
        var liveProjects = mechanical.LiveProjects.OfType<IProject>().ToList();

        var deletedProjectCount = (await _domainContext.Repository.ListByKindAsync(MechanicalObjectFactoryRegistry.Project, cancellationToken).ConfigureAwait(false))
            .OfType<IDeletable>()
            .Count(o => o.IsDeleted);

        var export = new ProgrammeExport(
            SchemaVersion,
            EngineeringStatusExportAdapter.FormatUtc(_time.GetUtcNow()),
            portfolios.Select(ToPortfolioEntry).ToList(),
            programmes.Select(ToProgrammeEntry).ToList(),
            liveProjects.Select(ToProjectEntry).ToList(),
            new ProgrammeSummary(
                portfolios.Count,
                programmes.Count,
                liveProjects.Count,
                CountsByStatus(liveProjects),
                deletedProjectCount));

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

    private ProjectEntry ToProjectEntry(IProject project) => new(
        project.Id.ToString(),
        project.Identifier,
        project.DisplayName,
        JsonNamingPolicy.CamelCase.ConvertName(project.Status.ToString()),
        project.Owner,
        project.Discipline,
        project.ProgrammeId?.ToString(),
        project.ParentId?.ToString(),
        project.IsDeleted,
        EngineeringStatusExportAdapter.FormatUtc(project.CreatedAt));

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
        string? ProgrammeId, string? ParentId, bool IsDeleted, string LastUpdate);

    private sealed record ProgrammeSummary(int PortfolioCount, int ProgrammeCount, int ProjectCount, Dictionary<string, int> ByStatus, int DeletedCount);
}
