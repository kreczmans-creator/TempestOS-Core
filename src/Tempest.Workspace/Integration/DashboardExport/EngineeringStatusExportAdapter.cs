using System.Globalization;
using System.Text.Json;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.ExportImport;
using Tempest.Core.Navigation;
using Tempest.Core.Requirements;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports a plain, dashboard-facing summary of Evidence, Requirements,
/// Verification, BOM and Digital Thread status — <c>engineering-status.json</c>,
/// consumed by Tempest-Dashboard's own push agent (the Core→Dashboard
/// integration contract, §2.1) — and, from schema v2, the Engineering
/// Cockpit's own health, KPI cards, attention items, blocked items and
/// overdue actions, verbatim. Implements <see cref="IExportable"/>/
/// <see cref="IExportableKind"/> exactly as
/// <c>Tempest.Samples.RequirementExportAdapter</c> does, so it remains
/// fully compatible with <see cref="IExportService"/>/<see cref="IImportService"/>
/// if a future "export everything" command ever wants the bundled-artifact
/// form too — but <see cref="DashboardExportHostedService"/> calls
/// <see cref="ExportAsync"/> directly, never through <see cref="IExportService"/>:
/// that service's own <c>JsonExportFormat</c> base64-wraps every section
/// (round-trip-import shaped), which is the wrong shape for a plain JSON
/// file an external agent reads and re-POSTs verbatim.
/// </summary>
/// <remarks>
/// <para>
/// <b>Cockpit reuse, not reimplementation (Product Owner instruction,
/// 2026-09-20).</b> Wherever the Engineering Cockpit
/// (<c>Tempest.Workspace.EngineeringCockpit</c>, its
/// <see cref="RequirementsCockpitReadModel"/>/<see cref="VerificationCockpitReadModel"/>
/// collaborators) already computes a metric this export needs, this
/// adapter constructs that same read model (composition-root style, `new`,
/// per `ADR-0103` — never DI-registered) and reads its already-computed,
/// already-tested public surface, so the desktop Cockpit and the exported
/// numbers can never silently disagree:
/// </para>
/// <list type="bullet">
/// <item><description><c>requirements.byStatus</c> — <see cref="RequirementsCockpitReadModel.StatusCounts"/>.</description></item>
/// <item><description><c>verification.recorded</c>/<c>byOutcome</c> — <see cref="VerificationCockpitReadModel.TotalVerificationRecordsCount"/>/<see cref="VerificationCockpitReadModel.PassedVerificationCount"/>/<see cref="VerificationCockpitReadModel.FailedVerificationCount"/>/<see cref="VerificationCockpitReadModel.ConditionalVerificationCount"/> — see this class's own <see cref="BuildVerificationSection"/> remarks for a disclosed definitional deviation from the contract document's own assumption.</description></item>
/// <item><description><c>health</c>/<c>kpis</c>/<c>attention</c>/<c>blockedItems</c>/<c>overdueActions</c> (schema v2) — <see cref="EngineeringCockpit.Health"/>, the five per-discipline <c>*Status</c> reads, <see cref="EngineeringCockpit.KpiCards"/> plus the five per-discipline <c>*KpiCards</c> sets, <see cref="EngineeringCockpit.AttentionItemsByDiscipline"/>, <see cref="EngineeringCockpit.BlockedItems"/> and <see cref="EngineeringCockpit.OverdueActions"/> — see <see cref="BuildHealthSection"/> and its siblings.</description></item>
/// </list>
/// <para>
/// <b>Schema v2 — "the Pi renders what the desktop cockpit computes"
/// (Product Owner direction, 2026-09-21).</b> The dashboard is to show one
/// "what needs attention" feed across every discipline, in the Cockpit's
/// own health words, not a second set of counters — so this adapter now
/// holds a real <see cref="EngineeringCockpit"/> (the identical composition
/// root <c>WorkspaceManager.StartAsync</c> builds for the desktop, reached
/// through this assembly's own internal constructor), calls
/// <see cref="EngineeringCockpit.PrimeAsync"/> once per export exactly as
/// <c>CockpitView.RefreshAsync</c> does once per render, and copies its
/// already-computed attention/health/KPI/blocked/overdue surface out
/// verbatim. None of that logic is reimplemented here. The Cockpit's two
/// session-bound dependencies (<c>NavigationService</c>, <see cref="ICommandRegistry"/>)
/// are satisfied with an empty, headless session — a
/// <c>NavigationService</c> over a real <see cref="INavigationProvider"/>
/// but with no view factories and no open views — because nothing this
/// export reads (<c>Health</c>, the discipline statuses, the KPI sets,
/// <c>AttentionItems</c>, <c>BlockedItems</c>, <c>OverdueActions</c>)
/// touches navigation or command state; the members that do
/// (<c>ContinueWhereILeftOff</c>, <c>RecentActivity</c>, <c>QuickActions</c>,
/// <c>AvailableCommands</c>) are deliberately not exported. No
/// <see cref="Tempest.Core.Audit.IAuditQuery"/> is supplied, so
/// <c>RecentlyChanged</c> is honestly empty and no audit permission is
/// needed for a background export.
/// </para>
/// <para>
/// <b>Two reads of the same store, disclosed.</b> The v1 sections still
/// read their own freshly-constructed <see cref="RequirementsCockpitReadModel"/>/
/// <see cref="VerificationCockpitReadModel"/> (they need
/// <see cref="RequirementsCockpitReadModel.StatusCounts"/>/<see cref="RequirementsCockpitReadModel.LiveRequirements"/>,
/// which <see cref="EngineeringCockpit"/> does not expose), while the v2
/// sections read the Cockpit's own private collaborators. Both load
/// within the one <see cref="ExportAsync"/> call over the same live store,
/// so they agree in every case but a write landing between the two loads
/// — the same window a desktop render already has between one card and
/// the next before `WP-E`, and one a sixty-second export cadence makes
/// immaterial.
/// </para>
/// <para>
/// <b>No Cockpit equivalent exists</b> for Evidence (no discipline Cockpit
/// read-model covers the <c>"Evidence"</c> Kind at all), BOM-line
/// aggregation (Mechanical's own Cockpit read-model tracks only the
/// <c>"Project"</c> Kind; Manufacturing's tracks Operations/Work
/// Instructions/Inspections, never Part/Assembly/SubAssembly/Component),
/// or a per-<see cref="RelationshipCategory"/> Digital Thread breakdown
/// (<c>EngineeringCockpit.DigitalThreadSummary</c> is a single scalar link
/// count). Each of those three sections is therefore implemented fresh,
/// directly against <see cref="EngineeringDomainContext"/>, exactly as the
/// integration contract document's own §3.1 originally planned.
/// </para>
/// </remarks>
public sealed class EngineeringStatusExportAdapter : IExportable, IExportableKind
{
    /// <summary>
    /// The schema version this adapter's own payload shape uses — the
    /// integration contract document's §2.1 (v1), plus the additive
    /// Cockpit sections (<c>health</c>/<c>kpis</c>/<c>attention</c>/
    /// <c>blockedItems</c>/<c>overdueActions</c>) that make it v2. Every
    /// v1 key is unchanged in name, shape and meaning.
    /// </summary>
    public const int CurrentSchemaVersion = 2;

    /// <summary>The most attention-worthy individual records <see cref="BuildItems"/> reports, capped.</summary>
    public const int MaxItems = 25;

    private static readonly IReadOnlyList<string> BomBearingKinds = ["Part", "Assembly", "SubAssembly", "Component"];

    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;
    private readonly TimeProvider _time;
    private readonly EngineeringCockpit _cockpit;

    /// <summary>Initialises a new instance of the <see cref="EngineeringStatusExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this adapter — and the read models it constructs — queries directly.</param>
    /// <param name="requirementsService">The Requirements Framework's own service, passed straight through to a freshly-constructed <see cref="RequirementsCockpitReadModel"/> and to the <see cref="EngineeringCockpit"/>.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service, passed straight through to a freshly-constructed <see cref="RequirementsCockpitReadModel"/> and to the <see cref="EngineeringCockpit"/>.</param>
    /// <param name="navigationProvider">The Platform's own navigation provider — the one session-bound dependency the <see cref="EngineeringCockpit"/>'s <c>NavigationService</c> needs; nothing this export reads ever consults it (see this class's remarks).</param>
    /// <param name="commandRegistry">The Platform's own command registry — the <see cref="EngineeringCockpit"/>'s other session-bound dependency; likewise never consulted by anything exported.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <c>Tempest.Core.Evidence.EvidenceService</c>'s own identical convention. Also the clock the Cockpit's own <see cref="EngineeringCockpit.OverdueActions"/> measures "overdue" against, so <c>generatedAt</c> and every <c>daysOverdue</c> agree.</param>
    public EngineeringStatusExportAdapter(
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
        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
        _time = timeProvider ?? TimeProvider.System;

        // The identical composition root WorkspaceManager.StartAsync builds
        // for the desktop, over a headless (empty) navigation session — see
        // this class's own "Schema v2" remarks for why that is sound.
        var navigationService = new NavigationService(navigationProvider, [], new WorkspaceContext());
        _cockpit = new EngineeringCockpit(
            navigationService, commandRegistry, domainContext, requirementsService, requirementValidationService,
            now: () => _time.GetUtcNow());
    }

    /// <inheritdoc />
    public string Kind => "dashboard.engineering-status";

    /// <inheritdoc />
    public int SchemaVersion => CurrentSchemaVersion;

    /// <inheritdoc />
    public async Task ExportAsync(Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);

        var liveEvidence = await LoadLiveEvidenceAsync(cancellationToken).ConfigureAwait(false);

        var requirements = new RequirementsCockpitReadModel(_requirementsService, _requirementValidationService);
        await requirements.LoadAsync(cancellationToken).ConfigureAwait(false);

        var verification = new VerificationCockpitReadModel(_domainContext);
        await verification.LoadAsync(cancellationToken).ConfigureAwait(false);

        // One coherent Cockpit pass per export — exactly what
        // CockpitView.RefreshAsync does once per desktop render.
        await _cockpit.PrimeAsync(cancellationToken).ConfigureAwait(false);

        var export = new EngineeringStatusExport(
            SchemaVersion,
            FormatUtc(_time.GetUtcNow()),
            BuildEvidenceSection(liveEvidence),
            BuildRequirementsSection(requirements),
            BuildVerificationSection(verification, requirements),
            await BuildBomSectionAsync(cancellationToken).ConfigureAwait(false),
            await BuildDigitalThreadSectionAsync(cancellationToken).ConfigureAwait(false),
            BuildItems(liveEvidence),
            BuildHealthSection(_cockpit),
            BuildKpiSection(_cockpit),
            BuildAttentionSection(_cockpit),
            _cockpit.BlockedItems,
            BuildOverdueActionsSection(_cockpit));

        await JsonSerializer.SerializeAsync(destination, export, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private async Task<IReadOnlyList<IEvidenceRecord>> LoadLiveEvidenceAsync(CancellationToken cancellationToken)
    {
        var all = await _domainContext.Repository.ListByKindAsync(Core.Evidence.Evidence.CanonicalKind, cancellationToken).ConfigureAwait(false);

        return all.OfType<IEvidenceRecord>().Where(e => !e.IsDeleted).ToList();
    }

    private static EvidenceSection BuildEvidenceSection(IReadOnlyList<IEvidenceRecord> liveEvidence)
    {
        var checkedEvidence = liveEvidence.Where(e => e.Check is not null).Select(e => e.Check!.Outcome).ToList();

        var openIssuesSheetsPending = liveEvidence.Count(e =>
            e.Status == EvidenceStatus.Issued && e.Issue?.IssueSheetAttachmentId is null);

        return new EvidenceSection(
            liveEvidence.Count,
            CountsByEnum(liveEvidence.Select(e => e.Status)),
            CountsByEnum(liveEvidence.Select(e => e.Classification)),
            new ChecksSection(
                checkedEvidence.Count(o => o == CheckOutcome.Accepted),
                checkedEvidence.Count(o => o == CheckOutcome.AcceptedWithComments),
                checkedEvidence.Count(o => o == CheckOutcome.Rejected)),
            openIssuesSheetsPending);
    }

    /// <summary>Reuses <see cref="RequirementsCockpitReadModel.StatusCounts"/> directly — the identical byStatus breakdown the Cockpit's own KpiCards already compute over the same live requirement set.</summary>
    private static RequirementsSection BuildRequirementsSection(RequirementsCockpitReadModel requirements) =>
        new(requirements.Count, ToStringKeyedDictionary(requirements.StatusCounts));

    /// <summary>
    /// Builds <c>verification.recorded</c>/<c>byOutcome</c> from
    /// <see cref="VerificationCockpitReadModel"/>'s own already-computed
    /// counts, and <c>requirementsUnverified</c> fresh over the same live
    /// requirement set <see cref="RequirementsCockpitReadModel"/> already
    /// loaded.
    /// </summary>
    /// <remarks>
    /// <b>Disclosed definitional deviation from the integration contract
    /// document's own §2.1/§5 Risk 1.</b> The contract document assumed
    /// <c>verification.recorded</c> would be "count of VerificationRecord
    /// reachable via <c>GetVerificationHistoryAsync</c> per Requirement" —
    /// an O(n) per-Requirement traversal it explicitly flagged as an
    /// unconfirmed risk needing "a short spike... before implementation."
    /// The Verification Cockpit read-model already answers this more
    /// directly, scoped to the real persisted Kind
    /// (<c>"VerificationActivity"</c>) rather than assumed per-Requirement:
    /// <see cref="VerificationCockpitReadModel.TotalVerificationRecordsCount"/>
    /// is the total <see cref="Verification.IVerificationRecord"/> count
    /// across every live Verification Activity, and
    /// <see cref="VerificationCockpitReadModel.PassedVerificationCount"/>/
    /// <see cref="VerificationCockpitReadModel.FailedVerificationCount"/>/
    /// <see cref="VerificationCockpitReadModel.ConditionalVerificationCount"/>
    /// each count Activities by their own most recent recorded outcome —
    /// the identical bucketing the desktop Cockpit's own "Passed"/"Failed"/
    /// "Conditional" KPI cards already show. Reusing it also removes the
    /// contract's own flagged N+1 risk outright, since no per-Requirement
    /// traversal is performed at all. <b>One nuance this reuse does not
    /// paper over, disclosed rather than hidden:</b> because "recorded" is a
    /// raw record count (an Activity re-verified twice contributes two) and
    /// "byOutcome" is an Activity count keyed by each Activity's own latest
    /// result, <c>byOutcome.pass + byOutcome.fail + byOutcome.conditional</c>
    /// is not guaranteed to equal <c>recorded</c> — both are real, honest,
    /// Cockpit-identical numbers; they simply answer two different
    /// questions ("how many results were ever recorded" vs. "how many
    /// Activities' own latest result was X"), exactly as the Cockpit's own
    /// KpiCards already present them side by side.
    /// </remarks>
    private static VerificationSection BuildVerificationSection(VerificationCockpitReadModel verification, RequirementsCockpitReadModel requirements)
    {
        var requirementsUnverified = requirements.LiveRequirements.Count(r =>
            r.Status is not (RequirementStatus.Verified or RequirementStatus.Satisfied));

        return new VerificationSection(
            verification.TotalVerificationRecordsCount,
            new VerificationOutcomeSection(
                verification.PassedVerificationCount,
                verification.FailedVerificationCount,
                verification.ConditionalVerificationCount),
            requirementsUnverified);
    }

    /// <summary>
    /// Fresh, direct implementation — no Cockpit equivalent exists (see
    /// this class's own remarks). Mirrors the integration contract
    /// document's own §5 Risk 2 disclosure: <c>IHasBomLine</c> is a static
    /// line, not a change-request record, so "pending" is approximated as
    /// BOM-bearing objects still <see cref="LifecycleState.Draft"/>/
    /// <see cref="LifecycleState.InReview"/> — a deliberately-labelled
    /// approximation, not a faithful port of a concept Core does not have.
    /// </summary>
    private async Task<BomSection> BuildBomSectionAsync(CancellationToken cancellationToken)
    {
        var lined = new List<IEngineeringObject>();
        foreach (var kind in BomBearingKinds)
        {
            var byKind = await _domainContext.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false);
            lined.AddRange(byKind.Where(o => o is not IDeletable { IsDeleted: true } && o is IHasBomLine));
        }

        var pendingByLifecycle = new Dictionary<string, int>
        {
            [JsonNamingPolicy.CamelCase.ConvertName(nameof(LifecycleState.Draft))] = 0,
            [JsonNamingPolicy.CamelCase.ConvertName(nameof(LifecycleState.InReview))] = 0,
        };

        foreach (var o in lined)
        {
            if (o is not IHasLifecycle { Status: LifecycleState.Draft or LifecycleState.InReview } lifecycle)
                continue;

            pendingByLifecycle[JsonNamingPolicy.CamelCase.ConvertName(lifecycle.Status.ToString())]++;
        }

        return new BomSection(lined.Count, pendingByLifecycle);
    }

    /// <summary>
    /// Fresh, direct implementation — no Cockpit equivalent exists (see
    /// this class's own remarks). Mirrors <c>EngineeringCockpit.PrimeAsync</c>'s
    /// own "walk every live object's outgoing relationships" shape, extended
    /// with a per-<see cref="RelationshipCategory"/> tally and an incoming
    /// check for the orphan signal, neither of which the Cockpit's own
    /// single scalar <c>DigitalThreadSummary</c> computes.
    /// </summary>
    private async Task<DigitalThreadSection> BuildDigitalThreadSectionAsync(CancellationToken cancellationToken)
    {
        var liveObjects = (await _domainContext.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false))
            .Where(o => o is not IDeletable { IsDeleted: true })
            .ToList();

        var byCategory = Enum.GetValues<RelationshipCategory>()
            .ToDictionary(c => JsonNamingPolicy.CamelCase.ConvertName(c.ToString()), _ => 0);

        var objectsWithNoRelationships = 0;

        foreach (var liveObject in liveObjects)
        {
            var outgoing = await _domainContext.RelationshipRepository.GetOutgoingAsync(liveObject.Id, cancellationToken).ConfigureAwait(false);
            foreach (var relationship in outgoing)
                byCategory[JsonNamingPolicy.CamelCase.ConvertName(relationship.Category.ToString())]++;

            if (outgoing.Count > 0)
                continue;

            var incoming = await _domainContext.RelationshipRepository.GetIncomingAsync(liveObject.Id, cancellationToken).ConfigureAwait(false);
            if (incoming.Count == 0)
                objectsWithNoRelationships++;
        }

        return new DigitalThreadSection(byCategory, objectsWithNoRelationships);
    }

    /// <summary>
    /// The capped, most-attention-worthy items list (integration contract
    /// document §2.1): Evidence with a Rejected check outcome, then
    /// Evidence Issued with no recorded issue sheet — mirrors the contract
    /// document's own disclosed v1 scope exactly (it explicitly excludes a
    /// "Requirement stale by age" bucket, since no field beyond
    /// <see cref="IRequirement.CreatedAt"/> exists to compute that
    /// honestly). Ordered newest-first within each bucket, rejected checks
    /// first (the stronger signal), each Evidence contributing at most one
    /// entry.
    /// </summary>
    private static IReadOnlyList<ExportItem> BuildItems(IReadOnlyList<IEvidenceRecord> liveEvidence)
    {
        var items = new List<ExportItem>();

        // "status" here names *why* the item is flagged, not
        // `evidence.Status` (the Evidence lifecycle value) — a rejected
        // check always leaves `Status == Checked`, so echoing that would
        // read as "checked" for an item whose whole point is that it was
        // rejected. Matches the integration contract document's own §2.1
        // example (`"status": "rejected"`) literally.
        foreach (var evidence in liveEvidence.Where(e => e.Check?.Outcome == CheckOutcome.Rejected).OrderByDescending(e => e.CreatedAt))
        {
            items.Add(new ExportItem(
                evidence.Identifier ?? evidence.Id.ToString(),
                "evidence",
                evidence.DisplayName,
                "rejected",
                Truncate(evidence.Check!.Statement, 200)));
        }

        foreach (var evidence in liveEvidence
            .Where(e => e.Status == EvidenceStatus.Issued && e.Issue?.IssueSheetAttachmentId is null && e.Check?.Outcome != CheckOutcome.Rejected)
            .OrderByDescending(e => e.CreatedAt))
        {
            items.Add(new ExportItem(
                evidence.Identifier ?? evidence.Id.ToString(),
                "evidence",
                evidence.DisplayName,
                "issued",
                "Issued with no recorded issue sheet."));
        }

        return items.Take(MaxItems).ToList();
    }

    /// <summary>
    /// <c>health</c> (schema v2): <see cref="EngineeringCockpit.Health"/>
    /// as <c>overall</c>, and each discipline's own <c>*Status</c> under
    /// <c>byDiscipline</c>, every value the Cockpit's own
    /// <see cref="EngineeringHealthStatus"/> word, lower-cased. Mechanical
    /// carries no status of its own (<c>MechanicalCockpitReadModel</c> has
    /// no <c>Status</c> member) and is omitted; <c>ReviewStatus</c> is a
    /// hard-coded <see cref="EngineeringHealthStatus.Unknown"/> that
    /// <see cref="EngineeringCockpit.Health"/> itself deliberately
    /// excludes, so it is omitted too rather than exported as a signal.
    /// </summary>
    private static HealthSection BuildHealthSection(EngineeringCockpit cockpit) =>
        new(
            FormatHealth(cockpit.Health),
            new Dictionary<string, string>
            {
                [CockpitDisciplines.Requirements] = FormatHealth(cockpit.RequirementsStatus),
                [CockpitDisciplines.Verification] = FormatHealth(cockpit.VerificationStatus),
                [CockpitDisciplines.Calculations] = FormatHealth(cockpit.CalculationStatus),
                [CockpitDisciplines.Documents] = FormatHealth(cockpit.DocumentationStatus),
                [CockpitDisciplines.Manufacturing] = FormatHealth(cockpit.ManufacturingStatus),
            });

    /// <summary>
    /// <c>kpis</c> (schema v2): every <see cref="CockpitKpiCard"/> the
    /// desktop Cockpit renders, verbatim — the cross-discipline
    /// <see cref="EngineeringCockpit.KpiCards"/> under <c>overview</c>
    /// (the "Engineering Overview" card), then each discipline's own set
    /// under its <see cref="CockpitDisciplines"/> key.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<KpiCardExport>> BuildKpiSection(EngineeringCockpit cockpit) =>
        new()
        {
            ["overview"] = ToKpiCards(cockpit.KpiCards),
            [CockpitDisciplines.Requirements] = ToKpiCards(cockpit.RequirementsKpiCards),
            [CockpitDisciplines.Verification] = ToKpiCards(cockpit.VerificationKpiCards),
            [CockpitDisciplines.Calculations] = ToKpiCards(cockpit.CalculationsKpiCards),
            [CockpitDisciplines.Documents] = ToKpiCards(cockpit.DocumentsKpiCards),
            [CockpitDisciplines.Manufacturing] = ToKpiCards(cockpit.ManufacturingKpiCards),
        };

    /// <summary><c>attention</c> (schema v2): <see cref="EngineeringCockpit.AttentionItemsByDiscipline"/>, verbatim and in the Cockpit's own order, each tagged with the discipline that contributed it.</summary>
    private static IReadOnlyList<AttentionExport> BuildAttentionSection(EngineeringCockpit cockpit) =>
        cockpit.AttentionItemsByDiscipline
            .Select(entry => new AttentionExport(entry.Discipline, entry.Item.Title, entry.Item.Detail))
            .ToList();

    /// <summary><c>overdueActions</c> (schema v2): <see cref="EngineeringCockpit.OverdueActions"/>, verbatim — each <see cref="CockpitActionItem"/>'s own title, owner, due date and whole days overdue.</summary>
    private static IReadOnlyList<OverdueActionExport> BuildOverdueActionsSection(EngineeringCockpit cockpit) =>
        cockpit.OverdueActions
            .Select(action => new OverdueActionExport(action.Title, action.Owner, action.DueDate, action.DaysOverdue))
            .ToList();

    private static IReadOnlyList<KpiCardExport> ToKpiCards(IReadOnlyList<CockpitKpiCard> cards) =>
        cards.Select(card => new KpiCardExport(card.Label, card.Value, card.IsPlaceholder, card.PercentValue)).ToList();

    /// <summary>The Cockpit's own <see cref="EngineeringHealthStatus"/> word, lower-cased — the same closed vocabulary the desktop shows, never a dashboard-specific translation.</summary>
    internal static string FormatHealth(EngineeringHealthStatus status) =>
        JsonNamingPolicy.CamelCase.ConvertName(status.ToString());

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength), "…");

    /// <summary>Every value of <typeparamref name="TEnum"/>, camelCased, pre-seeded to zero, then incremented once per occurrence in <paramref name="values"/> — every possible bucket is always present, matching the integration contract document's own examples (which show zero-valued buckets explicitly).</summary>
    private static Dictionary<string, int> CountsByEnum<TEnum>(IEnumerable<TEnum> values)
        where TEnum : struct, Enum
    {
        var counts = Enum.GetValues<TEnum>().ToDictionary(v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()), _ => 0);

        foreach (var value in values)
            counts[JsonNamingPolicy.CamelCase.ConvertName(value.ToString())]++;

        return counts;
    }

    private static Dictionary<string, int> ToStringKeyedDictionary<TEnum>(IReadOnlyDictionary<TEnum, int> counts)
        where TEnum : struct, Enum
    {
        var result = Enum.GetValues<TEnum>().ToDictionary(v => JsonNamingPolicy.CamelCase.ConvertName(v.ToString()), _ => 0);

        foreach (var (status, count) in counts)
            result[JsonNamingPolicy.CamelCase.ConvertName(status.ToString())] = count;

        return result;
    }

    internal static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private sealed record EngineeringStatusExport(
        int SchemaVersion,
        string GeneratedAt,
        EvidenceSection Evidence,
        RequirementsSection Requirements,
        VerificationSection Verification,
        BomSection Bom,
        DigitalThreadSection DigitalThread,
        IReadOnlyList<ExportItem> Items,
        HealthSection Health,
        Dictionary<string, IReadOnlyList<KpiCardExport>> Kpis,
        IReadOnlyList<AttentionExport> Attention,
        IReadOnlyList<string> BlockedItems,
        IReadOnlyList<OverdueActionExport> OverdueActions);

    private sealed record EvidenceSection(int Total, Dictionary<string, int> ByStatus, Dictionary<string, int> ByClassification, ChecksSection Checks, int OpenIssuesSheetsPending);

    private sealed record ChecksSection(int Accepted, int AcceptedWithComments, int Rejected);

    private sealed record RequirementsSection(int Total, Dictionary<string, int> ByStatus);

    private sealed record VerificationSection(int Recorded, VerificationOutcomeSection ByOutcome, int RequirementsUnverified);

    private sealed record VerificationOutcomeSection(int Pass, int Fail, int Conditional);

    private sealed record BomSection(int LinedObjects, Dictionary<string, int> PendingByLifecycle);

    private sealed record DigitalThreadSection(Dictionary<string, int> RelationshipsByCategory, int ObjectsWithNoRelationships);

    private sealed record ExportItem(string Id, string Type, string Title, string Status, string? Detail);

    private sealed record HealthSection(string Overall, Dictionary<string, string> ByDiscipline);

    private sealed record KpiCardExport(string Label, string Value, bool IsPlaceholder, int? PercentValue);

    private sealed record AttentionExport(string Discipline, string Title, string Detail);

    private sealed record OverdueActionExport(string Title, string Owner, DateTimeOffset? DueDate, int DaysOverdue);
}
