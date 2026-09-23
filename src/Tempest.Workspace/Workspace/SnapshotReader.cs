using System.Globalization;
using System.Text.Json;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Invoicing;
using Tempest.Core.Persistence;
using Tempest.Workspace.Kpi;

namespace Tempest.Workspace;

/// <summary>
/// Takes an immutable <see cref="WorkspaceSnapshot"/> read, inside one read
/// transaction, at a given sequence (`WP 18.1A`).
/// </summary>
/// <remarks>
/// A view responds to <see cref="Tempest.Core.Events.WorkspaceChange"/> by
/// taking one snapshot read here — never by composing several independent
/// reads of its own, which could straddle a commit that lands between
/// them. See <see cref="IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>,
/// which is what makes that guarantee possible.
/// </remarks>
public interface IWorkspaceSnapshotReader
{
    /// <summary>Reads the snapshot <paramref name="request"/> names, coherently, at the store's current sequence.</summary>
    Task<WorkspaceSnapshot> ReadAsync(WorkspaceSnapshotRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// The one <see cref="IWorkspaceSnapshotReader"/> implementation
/// (`WP 18.1A`): every request is answered by exactly one call to
/// <see cref="IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>,
/// reading the same collection <c>EngineeringObjectStateStore</c> writes
/// (<c>EngineeringDomain.ObjectState</c>) directly, rather than through the
/// in-memory object cache — a genuine, independent read of what is
/// durable, at one sequence.
/// </summary>
public sealed class WorkspaceSnapshotReader : IWorkspaceSnapshotReader
{
    /// <summary>The <see cref="IPersistenceStore"/> collection engineering object state lives in — mirrors <c>EngineeringObjectStateStore.StateCollectionName</c>.</summary>
    internal const string StateCollectionName = "EngineeringDomain.ObjectState";

    private static readonly JsonSerializerOptions DeserialiseOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The fallback <see cref="StateProjection.TypeState"/> for a record whose own JSON never wrote one — mirrors <see cref="ReadFacetsAsync"/>'s own established fallback.</summary>
    private static readonly Dictionary<string, string?> EmptyTypeState = new(StringComparer.Ordinal);

    private readonly IQueryablePersistenceStore _store;

    /// <summary>Initialises a new instance of the <see cref="WorkspaceSnapshotReader"/> class.</summary>
    /// <param name="store">The single durable store every snapshot reads through.</param>
    public WorkspaceSnapshotReader(IQueryablePersistenceStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    public Task<WorkspaceSnapshot> ReadAsync(WorkspaceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return request.Kind switch
        {
            WorkspaceSnapshotKind.ExplorerTree => ReadExplorerTreeAsync(cancellationToken),
            WorkspaceSnapshotKind.Cockpit => ReadCockpitAsync(cancellationToken),
            WorkspaceSnapshotKind.ObjectEditorState => ReadObjectStateAsync(request, cancellationToken),
            WorkspaceSnapshotKind.FacetSet => ReadFacetsAsync(request, cancellationToken),
            WorkspaceSnapshotKind.Kpi => ReadKpiAsync(request, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown snapshot request kind."),
        };
    }

    private Task<WorkspaceSnapshot> ReadExplorerTreeAsync(CancellationToken cancellationToken) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var records = await transaction.ReadAllAsync(StateCollectionName, token).ConfigureAwait(false);
                var nodes = new List<WorkspaceSnapshotNode>(records.Count);

                foreach (var (key, json) in records)
                {
                    if (!Guid.TryParseExact(key, "N", out var objectId))
                        continue;

                    if (Deserialise(json) is not { } state || state.IsDeleted)
                        continue;

                    nodes.Add(new WorkspaceSnapshotNode(objectId, state.Kind ?? "Unknown", state.DisplayName ?? string.Empty, state.ParentId));
                }

                return new WorkspaceSnapshot(transaction.Sequence, WorkspaceSnapshotKind.ExplorerTree, explorerTree: nodes);
            },
            cancellationToken);

    private Task<WorkspaceSnapshot> ReadCockpitAsync(CancellationToken cancellationToken) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var records = await transaction.ReadAllAsync(StateCollectionName, token).ConfigureAwait(false);
                var live = 0;
                var deleted = 0;

                foreach (var (_, json) in records)
                {
                    if (Deserialise(json) is not { } state)
                        continue;

                    if (state.IsDeleted)
                        deleted++;
                    else
                        live++;
                }

                return new WorkspaceSnapshot(
                    transaction.Sequence, WorkspaceSnapshotKind.Cockpit, cockpit: new WorkspaceSnapshotCockpitCounts(live, deleted));
            },
            cancellationToken);

    private Task<WorkspaceSnapshot> ReadObjectStateAsync(WorkspaceSnapshotRequest request, CancellationToken cancellationToken) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var json = await transaction.ReadAsync(StateCollectionName, request.ObjectId!.Value.ToString("N"), token).ConfigureAwait(false);
                var fields = new Dictionary<string, string?>(StringComparer.Ordinal);

                if (json is not null && Deserialise(json) is { } state)
                {
                    fields["Kind"] = state.Kind;
                    fields["DisplayName"] = state.DisplayName;
                    fields["ParentId"] = state.ParentId?.ToString();
                    fields["Status"] = state.Status;
                    fields["IsDeleted"] = state.IsDeleted.ToString();
                }

                return new WorkspaceSnapshot(transaction.Sequence, WorkspaceSnapshotKind.ObjectEditorState, objectState: fields);
            },
            cancellationToken);

    private Task<WorkspaceSnapshot> ReadFacetsAsync(WorkspaceSnapshotRequest request, CancellationToken cancellationToken) =>
        _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var json = await transaction.ReadAsync(StateCollectionName, request.ObjectId!.Value.ToString("N"), token).ConfigureAwait(false);
                IReadOnlyDictionary<string, string?> facets =
                    json is not null && Deserialise(json) is { TypeState: { } typeState }
                        ? typeState
                        : new Dictionary<string, string?>(StringComparer.Ordinal);

                return new WorkspaceSnapshot(transaction.Sequence, WorkspaceSnapshotKind.FacetSet, facets: facets);
            },
            cancellationToken);

    /// <summary>
    /// The Home cockpit's own KPI read (`WP 19.1B`): one scan of the whole
    /// object-state collection — the identical single
    /// <see cref="IQueryablePersistenceStore.ExecuteInReadTransactionAsync{T}"/>
    /// call every other snapshot kind above makes — picking out every live
    /// <c>TimesheetEntry</c>, <c>DeliverableCompletion</c>, <c>InvoiceRequest</c>
    /// and issued <c>Evidence</c> record (plus every <c>Project</c>'s own
    /// display name, for the by-project cards), then handing the parsed
    /// facts to <see cref="KpiEquations"/> — the pure functions `ADR-0150`'s
    /// five equations are implemented as, unit-tested independently of this
    /// I/O.
    /// </summary>
    private Task<WorkspaceSnapshot> ReadKpiAsync(WorkspaceSnapshotRequest request, CancellationToken cancellationToken)
    {
        var period = request.KpiPeriod ?? throw new ArgumentException("A Kpi request must carry a period.", nameof(request));
        var asOf = request.KpiAsOf ?? throw new ArgumentException("A Kpi request must carry an 'as of' date.", nameof(request));

        return _store.ExecuteInReadTransactionAsync(
            async (transaction, token) =>
            {
                var records = await transaction.ReadAllAsync(StateCollectionName, token).ConfigureAwait(false);

                var timesheetEntries = new List<TimesheetEntryFacts>();
                var deliverableCompletions = new List<DeliverableCompletionFacts>();
                var invoiceRequests = new List<InvoiceRequestFacts>();
                var evidenceIssues = new List<EvidenceIssueFacts>();
                var projectDisplayNames = new Dictionary<Guid, string>();

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
                            projectDisplayNames[objectId] = state.DisplayName ?? string.Empty;
                            break;

                        case Tempest.Core.Timesheets.TimesheetEntry.CanonicalKind:
                            if (ParseTimesheetEntry(typeState) is { } entry)
                                timesheetEntries.Add(entry);
                            break;

                        case Tempest.Core.Deliverables.DeliverableCompletion.CanonicalKind:
                            if (ParseDeliverableCompletion(state.ParentId, typeState) is { } completion)
                                deliverableCompletions.Add(completion);
                            break;

                        case InvoiceRequest.CanonicalKind:
                            invoiceRequests.Add(ParseInvoiceRequest(typeState));
                            break;

                        case Tempest.Core.Evidence.Evidence.CanonicalKind:
                            if (ParseEvidenceIssue(typeState) is { } issue)
                                evidenceIssues.Add(issue);
                            break;
                    }
                }

                var financials = new KpiFinancials(
                    KpiEquations.BillableHoursByPrincipal(timesheetEntries, period),
                    KpiEquations.MarginByProject(timesheetEntries, deliverableCompletions, projectDisplayNames, period),
                    KpiEquations.WorkInProgressByProject(timesheetEntries, deliverableCompletions, projectDisplayNames, asOf),
                    KpiEquations.DaysSalesOutstanding(invoiceRequests, asOf),
                    KpiEquations.CalcThroughput(evidenceIssues, period));

                return new WorkspaceSnapshot(transaction.Sequence, WorkspaceSnapshotKind.Kpi, kpi: financials);
            },
            cancellationToken);
    }

    private static TimesheetEntryFacts? ParseTimesheetEntry(IReadOnlyDictionary<string, string?> typeState)
    {
        var principalId = TypeString(typeState, "PrincipalIdentityId");
        if (string.IsNullOrEmpty(principalId))
            return null;

        // No billing rate was ever recorded — not a real, resolved entry
        // (`TimesheetEntry.RecordAsync` always resolves and freezes one).
        // Skipped rather than defaulted to a zero-amount, unspecified-
        // currency `Money`, which every downstream equation would then
        // treat as a genuine zero rather than as absent data.
        if (TypeString(typeState, "BillingRate") is null)
            return null;

        return new TimesheetEntryFacts(
            principalId,
            TypeGuid(typeState, "ProjectId") ?? Guid.Empty,
            TypeJson<DateOnly>(typeState, "Date"),
            TypeDecimal(typeState, "Hours"),
            TypeBool(typeState, "Billable"),
            TypeJson<Money>(typeState, "BillingRate"),
            TypeJson<Money?>(typeState, "CostRate"),
            TypeGuid(typeState, "InvoicedBy"));
    }

    private static DeliverableCompletionFacts? ParseDeliverableCompletion(Guid? projectId, IReadOnlyDictionary<string, string?> typeState)
    {
        if (projectId is not { } parent)
            return null;

        return new DeliverableCompletionFacts(
            parent,
            TypeJson<DateOnly>(typeState, "CompletedOn"),
            TypeJson<Money?>(typeState, "FixedPriceValue"),
            TypeGuid(typeState, "InvoicedBy"));
    }

    private static InvoiceRequestFacts ParseInvoiceRequest(IReadOnlyDictionary<string, string?> typeState) => new(
        Enum.TryParse<InvoiceRequestStatus>(TypeString(typeState, "Status"), out var status) ? status : InvoiceRequestStatus.Draft,
        TypeJson<DateOnly?>(typeState, "IssuedDate"),
        TypeJson<DateOnly?>(typeState, "PaidDate"));

    private static EvidenceIssueFacts? ParseEvidenceIssue(IReadOnlyDictionary<string, string?> typeState)
    {
        var issue = TypeJson<Tempest.Core.Evidence.IssueRecord>(typeState, "Issue");
        return issue is null ? null : new EvidenceIssueFacts(DateOnly.FromDateTime(issue.DateUtc.UtcDateTime));
    }

    private static string? TypeString(IReadOnlyDictionary<string, string?> typeState, string key) =>
        typeState.TryGetValue(key, out var value) ? value : null;

    private static Guid? TypeGuid(IReadOnlyDictionary<string, string?> typeState, string key) =>
        Guid.TryParse(TypeString(typeState, key), out var value) ? value : null;

    private static decimal TypeDecimal(IReadOnlyDictionary<string, string?> typeState, string key) =>
        decimal.TryParse(TypeString(typeState, key), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0m;

    private static bool TypeBool(IReadOnlyDictionary<string, string?> typeState, string key) =>
        bool.TryParse(TypeString(typeState, key), out var value) && value;

    /// <summary>Reads one type-specific value written by <c>EngineeringObjectBase.WriteJson</c>, mirroring <see cref="Tempest.Core.EngineeringDomain.EngineeringObjectState.TypeJson{TValue}"/> exactly (`TD-60`): a malformed or absent value degrades to <see langword="default"/> rather than throwing.</summary>
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

    /// <summary>
    /// Reads exactly the fields a snapshot needs out of one
    /// <c>EngineeringObjectState</c> record, without depending on that
    /// type or its enum converters — a corrupt or foreign record
    /// deserialises to <see langword="null"/> rather than throwing,
    /// mirroring <c>EngineeringObjectStateStore</c>'s own passive-read
    /// discipline (`TD-60`).
    /// </summary>
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

        public string? Status { get; set; }

        public Dictionary<string, string?>? TypeState { get; set; }
    }
}
