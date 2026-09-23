using System.Globalization;
using System.Text.Json;
using Tempest.Core.Evidence;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Core.Quotations;

namespace Tempest.Workspace.Tasks;

/// <summary>
/// Reads the complete Tasks read model in one coherent persistence read
/// (`WP 19.5C`, Product Owner comment item 6) — the
/// <c>Tempest.Workspace.Kpi.KpiSnapshotService</c>/<c>WorkspaceSnapshotReader.ReadKpiAsync</c>
/// shape, as a sibling reader over the identical durable
/// <c>EngineeringDomain.ObjectState</c> collection, rather than a new
/// <c>WorkspaceSnapshotKind</c> member (this Work Package's own files do
/// not touch <c>SnapshotReader.cs</c>).
/// </summary>
public interface ITasksReadModel
{
    /// <summary>Reads the complete Tasks snapshot, as of now.</summary>
    Task<TasksSnapshot> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>The concrete <see cref="ITasksReadModel"/> implementation.</summary>
public sealed class TasksReadModelService : ITasksReadModel
{
    /// <summary>The next this many upcoming milestones are shown (Product Owner comment item 6: "the next ten milestones").</summary>
    public const int UpcomingMilestoneCount = 10;

    private const string StateCollectionName = "EngineeringDomain.ObjectState";
    private static readonly JsonSerializerOptions DeserialiseOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly Dictionary<string, string?> EmptyTypeState = new(StringComparer.Ordinal);

    private readonly IQueryablePersistenceStore _store;
    private readonly TimeProvider _time;

    /// <summary>Initialises a new instance of the <see cref="TasksReadModelService"/> class.</summary>
    /// <param name="timeProvider">The clock "as of now" is read from. <see langword="null"/> — the default — is <see cref="TimeProvider.System"/>; a test supplies a controllable one.</param>
    public TasksReadModelService(IQueryablePersistenceStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(store);

        _store = store;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task<TasksSnapshot> ReadAsync(CancellationToken cancellationToken = default) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var records = await transaction.ReadAllAsync(StateCollectionName, token).ConfigureAwait(false);

                var deliverables = new List<DeliverableFact>();
                var milestones = new List<MilestoneFact>();
                var evidence = new List<EvidenceReviewFact>();
                var invoiceRequests = new List<InvoiceChaseFact>();
                var quotations = new List<QuotationChaseFact>();
                var manualTasks = new List<ManualTaskFact>();
                var calculationCandidates = new List<(Guid CalculationId, string Title, Guid? ParentId, bool Completed, DateOnly? DueOn)>();
                var completedDeliverableIds = new HashSet<Guid>();
                var closedProjectIds = new HashSet<Guid>();

                // Every live object's own Kind and direct parent (`TD-181`)
                // — not only the Kinds this reader otherwise recognises —
                // so a Calculation's own owning project can be found by
                // walking up through however many Assemblies, Parts or
                // Calculation Sets sit in between; `CalculationsWorkspaceRegistration.CalculationContainerKinds`
                // is exactly this list, and none of it is single-hop from
                // "Calculation" the way Deliverable -> Milestone -> Project
                // already is above.
                var kindByObjectId = new Dictionary<Guid, string>();
                var parentByObjectId = new Dictionary<Guid, Guid?>();

                foreach (var (key, json) in records)
                {
                    if (!Guid.TryParseExact(key, "N", out var objectId))
                        continue;

                    if (Deserialise(json) is not { } state || state.IsDeleted)
                        continue;

                    var typeState = state.TypeState ?? EmptyTypeState;

                    kindByObjectId[objectId] = state.Kind ?? string.Empty;
                    parentByObjectId[objectId] = state.ParentId;

                    switch (state.Kind)
                    {
                        case "Project":
                            if (TypeJson<DateOnly?>(typeState, "ClosedOn") is not null)
                                closedProjectIds.Add(objectId);

                            break;

                        case "Deliverable":
                            if (state.ParentId is { } milestoneId)
                                deliverables.Add(new DeliverableFact(objectId, state.DisplayName ?? string.Empty, milestoneId));

                            break;

                        case "Milestone":
                            if (state.ParentId is { } milestoneProjectId)
                            {
                                var targetDate = TypeDate(typeState, "TargetDate");
                                if (targetDate is { } td)
                                    milestones.Add(new MilestoneFact(objectId, state.DisplayName ?? string.Empty, milestoneProjectId, td));
                            }

                            break;

                        case Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind:
                            if (TypeGuid(typeState, "DeliverableId") is { } completedDeliverableId)
                                completedDeliverableIds.Add(completedDeliverableId);

                            break;

                        case Tempest.Core.Evidence.Evidence.CanonicalKind:
                            var subjectId = TypeGuid(typeState, "SubjectId");
                            evidence.Add(new EvidenceReviewFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                Enum.TryParse<EvidenceStatus>(TypeString(typeState, "Status"), out var evStatus) ? evStatus : EvidenceStatus.Draft,
                                subjectId is not null && state.Attachments is { Length: > 0 },
                                subjectId));
                            break;

                        case InvoiceRequest.CanonicalKind:
                            var sentAtUtc = TypeDateTimeOffset(typeState, "SentAtUtc");

                            // `TD-180`: a pre-`WP 20.1B` request carries no
                            // stored `DueOn` at all — backfilled here
                            // exactly as `InvoiceRequest.ReadDueOn` itself
                            // does (due the day it was sent), so the two
                            // independent readers of this same durable
                            // state — the domain object and this raw
                            // read model — agree.
                            var dueOn = TypeJson<DateOnly?>(typeState, "DueOn")
                                ?? (sentAtUtc is { } sent ? DateOnly.FromDateTime(sent.UtcDateTime) : null);

                            invoiceRequests.Add(new InvoiceChaseFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                Enum.TryParse<InvoiceRequestStatus>(TypeString(typeState, "Status"), out var invStatus) ? invStatus : InvoiceRequestStatus.Draft,
                                sentAtUtc,
                                TypeJson<DateOnly?>(typeState, "PaidDate"),
                                dueOn));
                            break;

                        // `TD-181`: every Calculation, wherever it sits —
                        // its own owning project is resolved below, once
                        // every object's own parent is known.
                        case "Calculation":
                            calculationCandidates.Add((
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                bool.TryParse(TypeString(typeState, "Completed"), out var calcDone) && calcDone,
                                TypeJson<DateOnly?>(typeState, "DueOn")));
                            break;

                        case Quotation.CanonicalKind:
                            quotations.Add(new QuotationChaseFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                Enum.TryParse<QuotationStatus>(TypeString(typeState, "Status"), out var quoteStatus) ? quoteStatus : QuotationStatus.Draft,
                                TypeJson<DateOnly?>(typeState, "SentOn")));
                            break;

                        case Tempest.Core.Tasks.ManualTask.CanonicalKind:
                            manualTasks.Add(new ManualTaskFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                TypeJson<DateOnly?>(typeState, "DueDate"),
                                bool.TryParse(TypeString(typeState, "Done"), out var done) && done));
                            break;
                    }
                }

                // Every open-project's own view: a milestone/deliverable
                // under a project that has since closed no longer needs
                // chasing on a dashboard — mirrors
                // `Tempest.Workspace.Projects.ProjectStatusReadModel`'s own
                // "per open project" scope.
                milestones = milestones.Where(m => !closedProjectIds.Contains(m.ProjectId)).ToList();

                var milestonesById = milestones.ToDictionary(m => m.MilestoneId);
                var deliverableIdsByMilestone = deliverables.Select(d => (d.MilestoneId, d.DeliverableId)).ToLookup(x => x.MilestoneId, x => x.DeliverableId);

                var asOf = _time.GetUtcNow();
                var today = DateOnly.FromDateTime(asOf.UtcDateTime);

                var openTasks = new List<TaskItem>();
                openTasks.AddRange(TaskEquations.DeliverableItems(deliverables, milestonesById, completedDeliverableIds, today));
                openTasks.AddRange(TaskEquations.MilestoneItems(milestones, deliverableIdsByMilestone, completedDeliverableIds, today));
                openTasks.AddRange(TaskEquations.ManualTaskItems(manualTasks, today));

                var reviews = TaskEquations.ReviewItems(evidence);
                var approvals = TaskEquations.ApprovalItems(evidence);

                var finance = new List<TaskItem>();
                finance.AddRange(TaskEquations.InvoiceFinanceItems(invoiceRequests, today));
                finance.AddRange(TaskEquations.QuotationFinanceItems(quotations, today));

                // `TD-181`: a calculation leaves the bucket on completion or
                // when evidence citing it is issued, whichever first — the
                // Product Owner decision's own words. "Citing" is
                // `Evidence.SubjectId`; only a live, Issued piece of
                // evidence closes the calculation it names, mirroring
                // `EvidenceStatus.Issued`'s own remarks ("Issued to the
                // client").
                var issuedSubjectIds = evidence
                    .Where(e => e.Status == EvidenceStatus.Issued && e.SubjectId is not null)
                    .Select(e => e.SubjectId!.Value)
                    .ToHashSet();

                // Not orphans outside a project (the lead's default,
                // disclosed in the brief): a calculation whose parent chain
                // never reaches a live "Project" is left off entirely,
                // rather than shown with no project to open it from.
                var calculations = calculationCandidates
                    .Select(c => (c.CalculationId, c.Title, ProjectId: ResolveProjectId(c.ParentId, kindByObjectId, parentByObjectId), c.Completed, c.DueOn))
                    .Where(c => c.ProjectId is not null && !closedProjectIds.Contains(c.ProjectId.Value))
                    .Select(c => new CalculationChaseFact(
                        c.CalculationId, c.Title, c.ProjectId!.Value, c.Completed || issuedSubjectIds.Contains(c.CalculationId), c.DueOn))
                    .ToList();

                var calculationItems = TaskEquations.CalculationItems(calculations);

                // `WP 20.10B` (T2): a dated calculation also joins the
                // Overdue/Due today/Due this week/Later buckets, exactly as
                // a manual task does — `TaskEquations.CalculationDueItems`'s
                // own remarks. Added after `openTasks`'s own three sources
                // above only because `calculations` itself needs the
                // project-resolution pass just above; `counts`, right
                // below, reads the completed list either way.
                openTasks.AddRange(TaskEquations.CalculationDueItems(calculations, today));

                var counts = new Dictionary<TaskBucket, int>
                {
                    [TaskBucket.Overdue] = openTasks.Count(i => i.Bucket == TaskBucket.Overdue),
                    [TaskBucket.DueToday] = openTasks.Count(i => i.Bucket == TaskBucket.DueToday),
                    [TaskBucket.DueThisWeek] = openTasks.Count(i => i.Bucket == TaskBucket.DueThisWeek),
                    [TaskBucket.Later] = openTasks.Count(i => i.Bucket == TaskBucket.Later),
                    [TaskBucket.Reviews] = reviews.Count,
                    [TaskBucket.Approvals] = approvals.Count,
                    [TaskBucket.Finance] = finance.Count,
                    [TaskBucket.Calculations] = calculationItems.Count,
                };

                var upcomingMilestones = TaskEquations.UpcomingMilestones(milestones, UpcomingMilestoneCount);

                return new TasksSnapshot(counts, openTasks, reviews, approvals, finance, calculationItems, upcomingMilestones);
            },
            cancellationToken);

    /// <summary>
    /// Walks up from <paramref name="startParentId"/> through <paramref name="parentByObjectId"/>
    /// until it finds an object <paramref name="kindByObjectId"/> reports as
    /// Kind <c>"Project"</c>, or runs out — a Calculation's own direct
    /// parent may be a Project, an Assembly, a Part, a Component or a
    /// Calculation Set (`CalculationsWorkspaceRegistration.CalculationContainerKinds`),
    /// so a single-hop lookup (Deliverable -> Milestone -> Project's own
    /// shape, above) is not enough. Bounded, defensively, against a cycle
    /// nothing in this platform's own write paths can actually create.
    /// </summary>
    private static Guid? ResolveProjectId(
        Guid? startParentId, IReadOnlyDictionary<Guid, string> kindByObjectId, IReadOnlyDictionary<Guid, Guid?> parentByObjectId)
    {
        var current = startParentId;

        for (var hop = 0; hop < 64 && current is { } id; hop++)
        {
            if (kindByObjectId.TryGetValue(id, out var kind) && string.Equals(kind, "Project", StringComparison.Ordinal))
                return id;

            current = parentByObjectId.TryGetValue(id, out var next) ? next : null;
        }

        return null;
    }

    private static string? TypeString(IReadOnlyDictionary<string, string?> typeState, string key) =>
        typeState.TryGetValue(key, out var value) ? value : null;

    private static Guid? TypeGuid(IReadOnlyDictionary<string, string?> typeState, string key) =>
        Guid.TryParse(TypeString(typeState, key), out var value) ? value : null;

    private static DateOnly? TypeDate(IReadOnlyDictionary<string, string?> typeState, string key) =>
        DateTimeOffset.TryParse(TypeString(typeState, key), CultureInfo.InvariantCulture, out var value)
            ? DateOnly.FromDateTime(value.UtcDateTime)
            : null;

    private static DateTimeOffset? TypeDateTimeOffset(IReadOnlyDictionary<string, string?> typeState, string key) =>
        DateTimeOffset.TryParse(
            TypeString(typeState, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)
            ? value
            : null;

    private static TValue? TypeJson<TValue>(IReadOnlyDictionary<string, string?> typeState, string key)
    {
        var raw = TypeString(typeState, key);
        if (string.IsNullOrEmpty(raw))
            return default;

        try
        {
            return JsonSerializer.Deserialize<TValue>(raw);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static StateProjection? Deserialise(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<StateProjection>(json, DeserialiseOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class StateProjection
    {
        public string? Kind { get; set; }

        public string? DisplayName { get; set; }

        public Guid? ParentId { get; set; }

        public bool IsDeleted { get; set; }

        public Dictionary<string, string?>? TypeState { get; set; }

        public JsonElement[]? Attachments { get; set; }
    }
}
