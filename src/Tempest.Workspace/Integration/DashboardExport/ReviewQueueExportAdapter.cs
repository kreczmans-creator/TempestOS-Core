using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.ExportImport;
using Tempest.Core.Requirements;
using Tempest.Workspace.Calculations;
using Tempest.Workspace.Documents;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Verification;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports the read-only review queue — every live item awaiting review,
/// oldest first — as <c>reviews.json</c> (`ADR-0157`), pushed verbatim by
/// Tempest-Dashboard's <c>agents/tempest-core-agent.*</c> to its
/// <c>reviews</c> ingest source. Implements <see cref="IExportable"/>/
/// <see cref="IExportableKind"/> exactly as
/// <see cref="EngineeringStatusExportAdapter"/> does — see that class's own
/// remarks for why <see cref="DashboardExportHostedService"/> calls
/// <see cref="ExportAsync"/> directly rather than through
/// <see cref="IExportService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ported, not reinvented.</b> The query is the August 2026 Companion
/// app's pending-review projection (<c>CompanionQueryService.BuildPendingReviewsAsync</c>,
/// recovered ref <c>refs/recovered/claude/tempestos-companion-mobile-ubznt3</c>):
/// the Document-family Kinds (<see cref="DocumentObjectFactoryRegistry.SupportedKinds"/> —
/// the identical three Kinds <c>DocumentsCockpitReadModel</c> reads),
/// live and <see cref="LifecycleState.InReview"/>. It is adapted to
/// today's Core in two ways: liveness and status are filtered from the
/// index rows alone and only the survivors are materialised (`TD-88`,
/// `WP 21.5B` — the original cast over a fully-materialised list), and the
/// Kinds are widened to every discipline whose desktop Cockpit already
/// counts a "Review" (see below).
/// </para>
/// <para>
/// <b>What "awaiting review" means, per family.</b> Each family's own
/// equivalent of the canonical <see cref="LifecycleState.InReview"/>,
/// never a new rule:
/// </para>
/// <list type="bullet">
/// <item><description>Documents, Drawings, CAD Models, Calculations and Verification Activities — <see cref="LifecycleState.InReview"/>, the state each discipline's own "Request Review" command moves to. The date is the most recent transition into it and the submitter that transition's actor (<see cref="IHasLifecycle.History"/>).</description></item>
/// <item><description>Evidence — <see cref="EvidenceStatus.Checked"/> with a non-rejected check, the canonical <c>InReview</c> (`ADR-0148`: checked, not yet issued). The date and name are the check's own. A rejected check is not awaiting anything here — it is returned for revision, and <c>engineering-status.json</c>'s <c>items</c> already flag it.</description></item>
/// <item><description>Requirements — <see cref="RequirementStatus.Reviewed"/>, the status <c>RequirementsCockpitReadModel.InReviewCount</c> already counts as the Requirements "Review" KPI. A requirement records no status-change time or actor, so its date is its creation time (<c>sinceBasis: "created"</c>) and <c>submittedBy</c> is <see langword="null"/> — disclosed rather than guessed.</description></item>
/// </list>
/// <para>
/// <b>Project.</b> <see cref="ProjectMembership"/>'s own structural
/// parent walk for engineering objects. A requirement is not in the
/// parent tree; it belongs to the project its links reach
/// (<c>ProjectRequirementRegister</c>'s own rule), and is reported against
/// that project only when every link that reaches a project reaches the
/// same one — otherwise <see langword="null"/>.
/// </para>
/// <para>
/// Read-only by design (Product Owner, v0.23.0): there is no approve or
/// reject path from the dashboard.
/// </para>
/// </remarks>
public sealed class ReviewQueueExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version this adapter's own payload shape uses.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The <c>sinceBasis</c> word for a lifecycle object: <c>since</c> is when it was submitted for review.</summary>
    public const string BasisSubmitted = "submitted";

    /// <summary>The <c>sinceBasis</c> word for Evidence: <c>since</c> is when its check was recorded.</summary>
    public const string BasisChecked = "checked";

    /// <summary>The <c>sinceBasis</c> word when no status-change time exists: <c>since</c> is the record's creation time.</summary>
    public const string BasisCreated = "created";

    /// <summary>The <c>discipline</c> word for Evidence, which has no <see cref="CockpitDisciplines"/> constant of its own.</summary>
    public const string EvidenceDiscipline = "evidence";

    /// <summary>
    /// Every lifecycle Kind whose <see cref="LifecycleState.InReview"/>
    /// rows are queued, with the <see cref="CockpitDisciplines"/> word
    /// each reports under — the Companion's three Document-family Kinds,
    /// then Calculations and Verification Activities.
    /// </summary>
    public static readonly IReadOnlyList<(string Kind, string Discipline)> LifecycleKinds =
    [
        .. DocumentObjectFactoryRegistry.SupportedKinds.Select(kind => (kind, CockpitDisciplines.Documents)),
        (CalculationObjectFactoryRegistry.CalculationKind, CockpitDisciplines.Calculations),
        (VerificationActivityFactoryRegistry.SupportedKind, CockpitDisciplines.Verification),
    ];

    private static readonly IReadOnlyList<string> Disciplines =
    [
        CockpitDisciplines.Documents, CockpitDisciplines.Calculations, CockpitDisciplines.Verification,
        EvidenceDiscipline, CockpitDisciplines.Requirements,
    ];

    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementsService _requirementsService;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="ReviewQueueExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository.</param>
    /// <param name="requirementsService">The Requirements Framework's own service — requirements live outside the engineering object index.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>; also the clock every <c>ageDays</c> is measured against, so <c>generatedAt</c> and every age agree.</param>
    public ReviewQueueExportAdapter(
        EngineeringDomainContext domainContext,
        IRequirementsService requirementsService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);

        _domainContext = domainContext;
        _requirementsService = requirementsService;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public string Kind => "dashboard.reviews";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var now = _time.GetUtcNow();
        var pending = new List<PendingReview>();

        pending.AddRange(await LoadLifecycleReviewsAsync(cancellationToken).ConfigureAwait(false));
        pending.AddRange(await LoadEvidenceReviewsAsync(cancellationToken).ConfigureAwait(false));
        pending.AddRange(await LoadRequirementReviewsAsync(cancellationToken).ConfigureAwait(false));

        var items = pending
            .OrderBy(p => p.Since)
            .ThenBy(p => p.Identifier ?? p.Title, StringComparer.Ordinal)
            .ThenBy(p => p.Id)
            .Select(p => ToEntry(p, now))
            .ToList();

        var byDiscipline = Disciplines.ToDictionary(d => d, d => items.Count(i => i.Discipline == d));

        var export = new ReviewsExport(SchemaVersion, EngineeringStatusExportAdapter.FormatUtc(now), items.Count, byDiscipline, items);

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Whole days from <paramref name="since"/> to <paramref name="now"/>, never negative — a clock skew between writer and exporter must not read as "due in the future".</summary>
    public static int AgeInDays(DateTimeOffset since, DateTimeOffset now) =>
        Math.Max(0, (int)Math.Floor((now - since).TotalDays));

    /// <summary>A requirement has no title, only a statement; the statement is truncated to this many characters to stand in for one.</summary>
    public const int MaxTitleLength = 160;

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private async Task<List<PendingReview>> LoadLifecycleReviewsAsync(CancellationToken cancellationToken)
    {
        var result = new List<PendingReview>();
        var repository = _domainContext.Repository;

        foreach (var (kind, discipline) in LifecycleKinds)
        {
            // `TD-88`: liveness and status both come from the index row; only
            // what is actually awaiting review is materialised.
            var entries = await repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            var inReview = entries.Where(e => !e.IsDeleted && e.Status == LifecycleState.InReview).ToList();
            if (inReview.Count == 0)
                continue;

            var byId = inReview.ToDictionary(e => e.Id);
            foreach (var candidate in await repository.MaterialiseAsync<IEngineeringObject>(inReview, cancellationToken).ConfigureAwait(false))
            {
                if (candidate is not IHasLifecycle { Status: LifecycleState.InReview } lifecycle)
                    continue;

                var entry = byId[candidate.Id];
                var submitted = lifecycle.History.LastOrDefault(h => h.To == LifecycleState.InReview);

                result.Add(new PendingReview(
                    candidate.Id,
                    candidate.Kind,
                    discipline,
                    entry.Identifier,
                    entry.DisplayName,
                    await ResolveProjectAsync(candidate.Id, cancellationToken).ConfigureAwait(false),
                    EngineeringStatusExportAdapter.FormatEnum(LifecycleState.InReview),
                    submitted?.OccurredAt ?? candidate.CreatedAt,
                    submitted is null ? BasisCreated : BasisSubmitted,
                    submitted?.ActorPrincipalId));
            }
        }

        return result;
    }

    private async Task<List<PendingReview>> LoadEvidenceReviewsAsync(CancellationToken cancellationToken)
    {
        // Evidence keeps its own four-value status beside the canonical one
        // (`ADR-0148`), so the index row's canonical Status cannot answer
        // "Checked": live rows are materialised, as engineering-status.json's
        // own evidence section already does.
        var entries = await _domainContext.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind, cancellationToken).ConfigureAwait(false);
        var live = await _domainContext.Repository.MaterialiseAsync<IEvidenceRecord>(
            [.. entries.Where(e => !e.IsDeleted)], cancellationToken).ConfigureAwait(false);

        var result = new List<PendingReview>();
        foreach (var evidence in live)
        {
            if (evidence.Status != EvidenceStatus.Checked || evidence.Check is not { } check || check.Outcome == CheckOutcome.Rejected)
                continue;

            result.Add(new PendingReview(
                evidence.Id,
                evidence.Kind,
                EvidenceDiscipline,
                evidence.Identifier,
                evidence.DisplayName,
                await ResolveProjectAsync(evidence.Id, cancellationToken).ConfigureAwait(false),
                EngineeringStatusExportAdapter.FormatEnum(EvidenceStatus.Checked),
                check.DateUtc,
                BasisChecked,
                check.CheckerName));
        }

        return result;
    }

    private async Task<List<PendingReview>> LoadRequirementReviewsAsync(CancellationToken cancellationToken)
    {
        var result = new List<PendingReview>();

        foreach (var requirement in await _requirementsService.ListAsync(cancellationToken).ConfigureAwait(false))
        {
            if (requirement.IsDeleted || requirement.Status != RequirementStatus.Reviewed)
                continue;

            result.Add(new PendingReview(
                requirement.Id,
                "Requirement",
                CockpitDisciplines.Requirements,
                requirement.Identifier,
                Truncate(requirement.Statement, MaxTitleLength),
                await ResolveRequirementProjectAsync(requirement.Id, cancellationToken).ConfigureAwait(false),
                EngineeringStatusExportAdapter.FormatEnum(RequirementStatus.Reviewed),
                requirement.CreatedAt,
                BasisCreated,
                SubmittedBy: null));
        }

        return result;
    }

    private async Task<ProjectRef?> ResolveProjectAsync(Guid objectId, CancellationToken cancellationToken)
    {
        var projectId = await ProjectMembership.ResolveOwningProjectAsync(_domainContext.Repository, objectId, cancellationToken).ConfigureAwait(false);
        return projectId is { } id ? ToProjectRef(id) : null;
    }

    private async Task<ProjectRef?> ResolveRequirementProjectAsync(Guid requirementId, CancellationToken cancellationToken)
    {
        var projects = new HashSet<Guid>();
        foreach (var reference in await _requirementsService.GetRelationshipsAsync(requirementId, cancellationToken).ConfigureAwait(false))
        {
            if (await ProjectMembership.ResolveOwningProjectAsync(_domainContext.Repository, reference.TargetDocumentId, cancellationToken).ConfigureAwait(false) is { } projectId)
                projects.Add(projectId);
        }

        return projects.Count == 1 ? ToProjectRef(projects.Single()) : null;
    }

    private ProjectRef? ToProjectRef(Guid projectId) =>
        _domainContext.Repository.PeekIndexEntry(projectId) is { } project
            ? new ProjectRef(project.Id, project.Identifier, project.DisplayName)
            : null;

    private static ReviewEntry ToEntry(PendingReview p, DateTimeOffset now) =>
        new(
            p.Id,
            p.Kind,
            p.Discipline,
            p.Identifier,
            p.Title,
            p.Project,
            p.Status,
            EngineeringStatusExportAdapter.FormatUtc(p.Since),
            p.SinceBasis,
            AgeInDays(p.Since, now),
            p.SubmittedBy);

    private sealed record PendingReview(
        Guid Id,
        string Kind,
        string Discipline,
        string? Identifier,
        string Title,
        ProjectRef? Project,
        string Status,
        DateTimeOffset Since,
        string SinceBasis,
        string? SubmittedBy);

    private sealed record ReviewsExport(int SchemaVersion, string GeneratedAt, int Total, Dictionary<string, int> ByDiscipline, IReadOnlyList<ReviewEntry> Items);

    private sealed record ProjectRef(Guid Id, string? Identifier, string Name);

    private sealed record ReviewEntry(
        Guid Id,
        string Kind,
        string Discipline,
        string? Identifier,
        string Title,
        ProjectRef? Project,
        string Status,
        string Since,
        string SinceBasis,
        int AgeDays,
        string? SubmittedBy);
}
