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
                var completedDeliverableIds = new HashSet<Guid>();
                var closedProjectIds = new HashSet<Guid>();

                foreach (var (key, json) in records)
                {
                    if (!Guid.TryParseExact(key, "N", out var objectId))
                        continue;

                    if (Deserialise(json) is not { } state || state.IsDeleted)
                        continue;

                    var typeState = state.TypeState ?? EmptyTypeState;

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
                            evidence.Add(new EvidenceReviewFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                Enum.TryParse<EvidenceStatus>(TypeString(typeState, "Status"), out var evStatus) ? evStatus : EvidenceStatus.Draft,
                                TypeGuid(typeState, "SubjectId") is not null && state.Attachments is { Length: > 0 }));
                            break;

                        case InvoiceRequest.CanonicalKind:
                            invoiceRequests.Add(new InvoiceChaseFact(
                                objectId, state.DisplayName ?? string.Empty, state.ParentId,
                                Enum.TryParse<InvoiceRequestStatus>(TypeString(typeState, "Status"), out var invStatus) ? invStatus : InvoiceRequestStatus.Draft,
                                TypeDateTimeOffset(typeState, "SentAtUtc"),
                                TypeJson<DateOnly?>(typeState, "PaidDate")));
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
                finance.AddRange(TaskEquations.InvoiceFinanceItems(invoiceRequests, asOf));
                finance.AddRange(TaskEquations.QuotationFinanceItems(quotations, today));

                var counts = new Dictionary<TaskBucket, int>
                {
                    [TaskBucket.Overdue] = openTasks.Count(i => i.Bucket == TaskBucket.Overdue),
                    [TaskBucket.DueToday] = openTasks.Count(i => i.Bucket == TaskBucket.DueToday),
                    [TaskBucket.DueThisWeek] = openTasks.Count(i => i.Bucket == TaskBucket.DueThisWeek),
                    [TaskBucket.Later] = openTasks.Count(i => i.Bucket == TaskBucket.Later),
                    [TaskBucket.Reviews] = reviews.Count,
                    [TaskBucket.Approvals] = approvals.Count,
                    [TaskBucket.Finance] = finance.Count,
                };

                var upcomingMilestones = TaskEquations.UpcomingMilestones(milestones, UpcomingMilestoneCount);

                return new TasksSnapshot(counts, openTasks, reviews, approvals, finance, upcomingMilestones);
            },
            cancellationToken);

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
