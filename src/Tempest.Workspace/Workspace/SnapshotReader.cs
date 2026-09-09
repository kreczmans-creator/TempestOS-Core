using System.Text.Json;
using Tempest.Core.Persistence;

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
