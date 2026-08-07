using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Verification;

/// <summary>
/// Populates the Project Explorer's own Verification area from the real
/// Engineering Domain — the fifth <see cref="IProjectExplorerNodeProvider"/>
/// backed by a real Engineering discipline, after Mechanical (`WP 9.0A`),
/// Requirements (`WP 9.1A`), Calculations (`WP 9.2A`), and Documents
/// (`WP 9.4A`). Root nodes: one synthetic, read-only category node per
/// <see cref="VerificationMethodCategory"/> label (mirrors
/// <see cref="Documents.DocumentsNodeProvider"/>'s own
/// <c>DocumentCategory</c> precedent exactly, here over one real Kind —
/// <c>"VerificationActivity"</c> — rather than three), each containing
/// every live, un-parented Verification Activity that falls into it.
/// </summary>
public sealed class VerificationActivityNodeProvider : IProjectExplorerNodeProvider
{
    private static readonly IReadOnlyDictionary<string, Guid> CategoryNodeIds = new Dictionary<string, Guid>(StringComparer.Ordinal)
    {
        ["Inspection"] = new("00000000-0000-4002-8000-000000000001"),
        ["Analysis"] = new("00000000-0000-4002-8000-000000000002"),
        ["Test"] = new("00000000-0000-4002-8000-000000000003"),
        ["Demonstration"] = new("00000000-0000-4002-8000-000000000004"),
        ["Other"] = new("00000000-0000-4002-8000-000000000005"),
    };

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="VerificationActivityNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public VerificationActivityNodeProvider(string kind, EngineeringDomainContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentNullException.ThrowIfNull(context);

        Kind = kind;
        _context = context;
    }

    /// <inheritdoc />
    public string Kind { get; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetRootNodesAsync(CancellationToken cancellationToken = default)
    {
        var activities = await LiveActivitiesAsync(cancellationToken).ConfigureAwait(false);
        var byCategory = activities.ToLookup(VerificationMethodCategory.Of);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var label in VerificationMethodCategory.Labels)
        {
            var count = byCategory[label].Count(o => o is IHasParent { ParentId: null });
            nodes.Add(new(CategoryNodeIds[label], label, null, count > 0, ProjectExplorerNodeType.Category));
        }

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Verification node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var categoryLabel = CategoryNodeIds.FirstOrDefault(kv => kv.Value == nodeId).Key;
        if (categoryLabel is not null)
        {
            var activities = await LiveActivitiesAsync(cancellationToken).ConfigureAwait(false);
            var members = activities.Where(o => VerificationMethodCategory.Of(o) == categoryLabel && o is IHasParent { ParentId: null });

            var nodes = new List<ProjectExplorerNode>();
            foreach (var member in members)
                nodes.Add(await ToActivityNodeAsync(member, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        var target = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (target is IVerificationActivity)
        {
            var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
            var children = all.Where(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == nodeId);

            var nodes = new List<ProjectExplorerNode>();
            foreach (var child in children)
                nodes.Add(await ToActivityNodeAsync(child, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        throw new ArgumentException($"'{nodeId}' is not a known Verification node.", nameof(nodeId));
    }

    /// <summary>Walks <paramref name="objectId"/>'s own <see cref="IHasParent.ParentId"/> chain, root first — the Explorer's own breadcrumb source, mirroring every other real discipline's own identical, additive convenience shape.</summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var ancestry = new List<ProjectExplorerNode>();
        var current = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false);

        while (current is IHasParent { ParentId: { } parentId })
        {
            var parent = await _context.Repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);
            if (parent is null)
                break;

            ancestry.Insert(0, await ToActivityNodeAsync(parent, cancellationToken).ConfigureAwait(false));
            current = parent;
        }

        return ancestry;
    }

    private async Task<IReadOnlyList<IEngineeringObject>> LiveActivitiesAsync(CancellationToken cancellationToken) =>
        (await _context.Repository.ListByKindAsync(VerificationActivityFactoryRegistry.SupportedKind, cancellationToken).ConfigureAwait(false))
        .Where(IsLive)
        .ToList();

    private async Task<ProjectExplorerNode> ToActivityNodeAsync(IEngineeringObject activity, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var hasChildren = all.Any(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == activity.Id);

        return new ProjectExplorerNode(activity.Id, DisplayNameOf(activity), activity.Kind, hasChildren, ProjectExplorerNodeType.Object);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}

/// <summary>
/// Maps a live Verification Activity onto one of
/// <see cref="Labels"/> — its own <see cref="IVerificationActivity.Method"/>,
/// an open string (mirrors <see cref="Tempest.Core.Verification.IVerificationRecord.Method"/>'s
/// own identical, deliberately-open shape) matched against the four
/// named methods this Work Package's own scope lists; anything else falls
/// into <c>"Other"</c>, honestly, never silently dropped.
/// </summary>
public static class VerificationMethodCategory
{
    public static readonly IReadOnlyList<string> Labels = ["Inspection", "Analysis", "Test", "Demonstration", "Other"];

    public static string Of(IEngineeringObject activity) =>
        activity is IVerificationActivity { Method: { } method } && Labels.Contains(method, StringComparer.OrdinalIgnoreCase)
            ? Labels.First(l => string.Equals(l, method, StringComparison.OrdinalIgnoreCase))
            : "Other";
}
