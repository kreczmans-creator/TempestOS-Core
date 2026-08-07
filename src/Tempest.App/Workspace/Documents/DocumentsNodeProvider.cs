using Tempest.Core.EngineeringDomain;

namespace Tempest.App.Workspace.Documents;

/// <summary>
/// Populates the Project Explorer's own Documents area from the real
/// Engineering Domain — the fourth <see cref="IProjectExplorerNodeProvider"/>
/// backed by a real Engineering discipline, after Mechanical's (`WP 9.0A`),
/// Requirements' (`WP 9.1A`), and Calculations' (`WP 9.2A`). Root nodes: one
/// synthetic, read-only category node per <see cref="DocumentCategory"/>
/// (mirrors <see cref="Calculations.CalculationsNodeProvider"/>'s own
/// synthetic <c>"Templates"</c> node precedent exactly — a category node has
/// no Domain identity of its own, only a stable, provider-assigned Id),
/// each containing every live, un-parented Document that falls into it. A
/// Document that is itself the real <see cref="IHasParent"/> of another
/// (e.g. a Detail Drawing structurally under a General Arrangement Drawing)
/// is reachable as that parent's own child, exactly like
/// <see cref="Mechanical.MechanicalProductStructureNodeProvider"/>'s own
/// established real-parent nesting.
/// </summary>
public sealed class DocumentsNodeProvider : IProjectExplorerNodeProvider
{
    /// <summary>
    /// The fixed category labels this Work Package's own scope names,
    /// realised via <see cref="DocumentCategory.Of"/> (`ADR-0088`) rather
    /// than as separate Domain Kinds.
    /// </summary>
    public static readonly IReadOnlyList<string> CategoryLabels =
    [
        "Drawings", "CAD Models", "Specifications", "Reports", "Procedures",
        "Standards", "Datasheets", "External References", "Resources",
        "Tooling", "Fixtures", "Uncategorized",
    ];

    private static readonly IReadOnlyDictionary<string, Guid> CategoryNodeIds = new Dictionary<string, Guid>(StringComparer.Ordinal)
    {
        ["Drawings"] = new("00000000-0000-4001-8000-000000000001"),
        ["CAD Models"] = new("00000000-0000-4001-8000-000000000002"),
        ["Specifications"] = new("00000000-0000-4001-8000-000000000003"),
        ["Reports"] = new("00000000-0000-4001-8000-000000000004"),
        ["Procedures"] = new("00000000-0000-4001-8000-000000000005"),
        ["Standards"] = new("00000000-0000-4001-8000-000000000006"),
        ["Datasheets"] = new("00000000-0000-4001-8000-000000000007"),
        ["External References"] = new("00000000-0000-4001-8000-000000000008"),
        ["Uncategorized"] = new("00000000-0000-4001-8000-000000000009"),
        // WP 9.5A: three further category nodes, extending ADR-0088's own
        // open Classification taxonomy — see DocumentObjectFactoryRegistry's
        // own Resource/Tooling/Fixture constants.
        ["Resources"] = new("00000000-0000-4001-8000-00000000000a"),
        ["Tooling"] = new("00000000-0000-4001-8000-00000000000b"),
        ["Fixtures"] = new("00000000-0000-4001-8000-00000000000c"),
    };

    private readonly EngineeringDomainContext _context;

    /// <summary>Initialises a new instance of the <see cref="DocumentsNodeProvider"/> class.</summary>
    /// <param name="kind">The top-level area this provider populates.</param>
    public DocumentsNodeProvider(string kind, EngineeringDomainContext context)
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
        var documents = await LiveDocumentsAsync(cancellationToken).ConfigureAwait(false);
        var byCategory = documents.ToLookup(DocumentCategory.Of);

        var nodes = new List<ProjectExplorerNode>();
        foreach (var label in CategoryLabels)
        {
            var count = byCategory[label].Count(o => o is IHasParent { ParentId: null });
            nodes.Add(new(CategoryNodeIds[label], label, null, count > 0, ProjectExplorerNodeType.Category));
        }

        return nodes;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException"><paramref name="nodeId"/> does not identify a known Documents node.</exception>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetChildrenAsync(Guid nodeId, CancellationToken cancellationToken = default)
    {
        var categoryLabel = CategoryNodeIds.FirstOrDefault(kv => kv.Value == nodeId).Key;
        if (categoryLabel is not null)
        {
            var documents = await LiveDocumentsAsync(cancellationToken).ConfigureAwait(false);
            var members = documents.Where(o => DocumentCategory.Of(o) == categoryLabel && o is IHasParent { ParentId: null });

            var nodes = new List<ProjectExplorerNode>();
            foreach (var member in members)
                nodes.Add(await ToDocumentNodeAsync(member, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        var target = await _context.Repository.FindAsync(nodeId, cancellationToken).ConfigureAwait(false);

        if (target is IDocument)
        {
            var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
            var children = all.Where(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == nodeId);

            var nodes = new List<ProjectExplorerNode>();
            foreach (var child in children)
                nodes.Add(await ToDocumentNodeAsync(child, cancellationToken).ConfigureAwait(false));

            return [.. nodes.OrderBy(n => n.Title, StringComparer.Ordinal)];
        }

        throw new ArgumentException($"'{nodeId}' is not a known Documents node.", nameof(nodeId));
    }

    /// <summary>
    /// Walks <paramref name="objectId"/>'s own <see cref="IHasParent.ParentId"/>
    /// chain, root first — the Explorer's own breadcrumb source, mirroring
    /// <see cref="Calculations.CalculationsNodeProvider.GetAncestryAsync"/>'s
    /// own identical, additive convenience shape. A category node is never
    /// part of this chain — only real <see cref="IHasParent"/> nesting
    /// between Documents.
    /// </summary>
    public async Task<IReadOnlyList<ProjectExplorerNode>> GetAncestryAsync(Guid objectId, CancellationToken cancellationToken = default)
    {
        var ancestry = new List<ProjectExplorerNode>();
        var current = await _context.Repository.FindAsync(objectId, cancellationToken).ConfigureAwait(false);

        while (current is IHasParent { ParentId: { } parentId })
        {
            var parent = await _context.Repository.FindAsync(parentId, cancellationToken).ConfigureAwait(false);
            if (parent is null)
                break;

            ancestry.Insert(0, await ToDocumentNodeAsync(parent, cancellationToken).ConfigureAwait(false));
            current = parent;
        }

        return ancestry;
    }

    private async Task<IReadOnlyList<IEngineeringObject>> LiveDocumentsAsync(CancellationToken cancellationToken)
    {
        var all = new List<IEngineeringObject>();
        foreach (var kind in DocumentObjectFactoryRegistry.SupportedKinds)
            all.AddRange(await _context.Repository.ListByKindAsync(kind, cancellationToken).ConfigureAwait(false));

        return all.Where(IsLive).ToList();
    }

    private async Task<ProjectExplorerNode> ToDocumentNodeAsync(IEngineeringObject document, CancellationToken cancellationToken)
    {
        var all = await _context.Repository.ListAllAsync(cancellationToken).ConfigureAwait(false);
        var hasChildren = all.Any(o => IsLive(o) && o is IHasParent { ParentId: { } parentId } && parentId == document.Id);

        return new ProjectExplorerNode(document.Id, DisplayNameOf(document), document.Kind, hasChildren, ProjectExplorerNodeType.Object);
    }

    private static string DisplayNameOf(IEngineeringObject o) => (o as IHasBusinessIdentifier)?.DisplayName ?? o.Id.ToString();

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}

/// <summary>
/// Maps a live Document Domain object onto one of
/// <see cref="DocumentsNodeProvider.CategoryLabels"/> — <c>ADR-0088</c>'s
/// own disclosed mapping: <c>"Drawing"</c>/<c>"CadModel"</c> map from their
/// own Kind directly (a real, distinct Domain type); a plain
/// <c>"Document"</c> maps from its own
/// <see cref="IHasMetadata.Classification"/> (Specification/Report/
/// Procedure/Standard/Datasheet/External Reference/Resource/Tooling/
/// Fixture — the last three extended by `WP 9.5A`); anything else —
/// an unset or unrecognised Classification — falls into <c>"Uncategorized"</c>,
/// honestly, never silently dropped.
/// </summary>
public static class DocumentCategory
{
    public static string Of(IEngineeringObject document) => document switch
    {
        IDrawing => "Drawings",
        ICadModel => "CAD Models",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Specification } => "Specifications",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Report } => "Reports",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Procedure } => "Procedures",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Standard } => "Standards",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Datasheet } => "Datasheets",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.ExternalReference } => "External References",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Resource } => "Resources",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Tooling } => "Tooling",
        IHasMetadata { Classification: DocumentObjectFactoryRegistry.Fixture } => "Fixtures",
        _ => "Uncategorized",
    };
}
