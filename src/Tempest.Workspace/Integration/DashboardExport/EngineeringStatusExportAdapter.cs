using System.Globalization;
using System.Text.Json;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.ExportImport;
using Tempest.Core.Requirements;
using Tempest.Workspace.Requirements;
using Tempest.Workspace.Verification;

namespace Tempest.Workspace.Integration.DashboardExport;

/// <summary>
/// Exports a plain, dashboard-facing summary of Evidence, Requirements,
/// Verification, BOM and Digital Thread status — <c>engineering-status.json</c>,
/// consumed by Tempest-Dashboard's own push agent (the Core→Dashboard
/// integration contract, §2.1). Implements <see cref="IExportable"/>/
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
/// </list>
/// <para>
/// <b>No Cockpit equivalent exists</b> for Evidence (no discipline Cockpit
/// read-model covers the <c>"Evidence"</c> Kind at all), BOM-line
/// aggregation (Mechanical's own Cockpit read-model tracks only the
/// <c>"Project"</c> Kind; Manufacturing's tracks Operations/Work
/// Instructions/Inspections, never Part/Assembly/SubAssembly/Component),
/// or a per-<see cref="RelationshipCategory"/> Digital Thread breakdown
/// (<c>EngineeringCockpit.DigitalThreadSummary</c> is a single scalar link
/// count, and constructing a full <c>EngineeringCockpit</c> here — which
/// needs a live Workspace session's own <c>NavigationService</c>/
/// <c>ICommandRegistry</c> — would be the wrong shape for a headless
/// background export). Each of those three sections is therefore
/// implemented fresh, directly against <see cref="EngineeringDomainContext"/>,
/// exactly as the integration contract document's own §3.1 originally
/// planned.
/// </para>
/// </remarks>
public sealed class EngineeringStatusExportAdapter : IExportable, IExportableKind
{
    /// <summary>The schema version this adapter's own payload shape uses — the integration contract document's §2.1.</summary>
    public const int CurrentSchemaVersion = 1;

    /// <summary>The most attention-worthy individual records <see cref="BuildItems"/> reports, capped.</summary>
    public const int MaxItems = 25;

    private static readonly IReadOnlyList<string> BomBearingKinds = ["Part", "Assembly", "SubAssembly", "Component"];

    private readonly EngineeringDomainContext _domainContext;
    private readonly IRequirementsService _requirementsService;
    private readonly IRequirementValidationService _requirementValidationService;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="EngineeringStatusExportAdapter"/> class.</summary>
    /// <param name="domainContext">The Engineering Domain's own shared repository this adapter — and the read models it constructs — queries directly.</param>
    /// <param name="requirementsService">The Requirements Framework's own service, passed straight through to a freshly-constructed <see cref="RequirementsCockpitReadModel"/>.</param>
    /// <param name="requirementValidationService">The Requirements Framework's own validation service, passed straight through to a freshly-constructed <see cref="RequirementsCockpitReadModel"/>.</param>
    /// <param name="timeProvider"><see langword="null"/> — the default — uses <see cref="TimeProvider.System"/>, mirroring <c>Tempest.Core.Evidence.EvidenceService</c>'s own identical convention.</param>
    public EngineeringStatusExportAdapter(
        EngineeringDomainContext domainContext,
        IRequirementsService requirementsService,
        IRequirementValidationService requirementValidationService,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(requirementsService);
        ArgumentNullException.ThrowIfNull(requirementValidationService);

        _domainContext = domainContext;
        _requirementsService = requirementsService;
        _requirementValidationService = requirementValidationService;
        _time = timeProvider ?? TimeProvider.System;
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

        var export = new EngineeringStatusExport(
            SchemaVersion,
            FormatUtc(_time.GetUtcNow()),
            BuildEvidenceSection(liveEvidence),
            BuildRequirementsSection(requirements),
            BuildVerificationSection(verification, requirements),
            await BuildBomSectionAsync(cancellationToken).ConfigureAwait(false),
            await BuildDigitalThreadSectionAsync(cancellationToken).ConfigureAwait(false),
            BuildItems(liveEvidence));

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
        IReadOnlyList<ExportItem> Items);

    private sealed record EvidenceSection(int Total, Dictionary<string, int> ByStatus, Dictionary<string, int> ByClassification, ChecksSection Checks, int OpenIssuesSheetsPending);

    private sealed record ChecksSection(int Accepted, int AcceptedWithComments, int Rejected);

    private sealed record RequirementsSection(int Total, Dictionary<string, int> ByStatus);

    private sealed record VerificationSection(int Recorded, VerificationOutcomeSection ByOutcome, int RequirementsUnverified);

    private sealed record VerificationOutcomeSection(int Pass, int Fail, int Conditional);

    private sealed record BomSection(int LinedObjects, Dictionary<string, int> PendingByLifecycle);

    private sealed record DigitalThreadSection(Dictionary<string, int> RelationshipsByCategory, int ObjectsWithNoRelationships);

    private sealed record ExportItem(string Id, string Type, string Title, string Status, string? Detail);
}
