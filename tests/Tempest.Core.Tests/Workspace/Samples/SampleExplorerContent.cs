using Tempest.App.Workspace;

namespace Tempest.Core.Tests.Workspace.Samples;

/// <summary>
/// A small, fixed, purely in-memory tree of fictional engineering objects —
/// a Category root, two Group nodes, and three leaf Objects — used to drive
/// the Project Explorer's own Kind-keyed provider architecture (`ADR-0067`)
/// end to end without touching Requirements, Calculations, Documents, or any
/// other Engineering Core service.
/// </summary>
/// <remarks>
/// <para>
/// A test fixture, and now filed as one. It lived in <c>Tempest.App</c> until
/// `TD-75` phase 2, where the composition root stopped registering it: the
/// navigation area it attaches to is registered only by
/// <c>Tempest.Samples.WorkspaceExplorerSampleModule</c>, which phase 1 stopped
/// the product loading, so the registration had already become unreachable in
/// every production run. Nothing in the shipped product constructs a Longeron,
/// a Frame or a Bracket any more; <see cref="WorkspaceShellTests"/> does, which
/// is what this content was always actually for.
/// </para>
/// </remarks>
internal static class SampleExplorerContent
{
    /// <summary>The <c>Kind</c> of a sample component-level object.</summary>
    public const string ComponentKind = "SampleComponent";

    private static readonly Guid AssembliesCategoryId = Guid.Parse("00000000-0000-0000-0001-000000000001");
    private static readonly Guid PrimaryStructureId = Guid.Parse("00000000-0000-0000-0001-000000000002");
    private static readonly Guid SecondaryStructureId = Guid.Parse("00000000-0000-0000-0001-000000000003");
    private static readonly Guid LongeronId = Guid.Parse("00000000-0000-0000-0001-000000000004");
    private static readonly Guid FrameId = Guid.Parse("00000000-0000-0000-0001-000000000005");
    private static readonly Guid BracketId = Guid.Parse("00000000-0000-0000-0001-000000000006");

    private static readonly Dictionary<Guid, ProjectExplorerNode> Nodes = new();
    private static readonly Dictionary<Guid, List<Guid>> Children = new();

    static SampleExplorerContent()
    {
        Add(new ProjectExplorerNode(AssembliesCategoryId, "Assemblies", null, true, ProjectExplorerNodeType.Category), parentId: null);
        Add(new ProjectExplorerNode(PrimaryStructureId, "Primary Structure", null, true, ProjectExplorerNodeType.Group), AssembliesCategoryId);
        Add(new ProjectExplorerNode(SecondaryStructureId, "Secondary Structure", null, true, ProjectExplorerNodeType.Group), AssembliesCategoryId);
        Add(new ProjectExplorerNode(LongeronId, "Longeron", ComponentKind, false, ProjectExplorerNodeType.Object), PrimaryStructureId);
        Add(new ProjectExplorerNode(FrameId, "Frame", ComponentKind, false, ProjectExplorerNodeType.Object), PrimaryStructureId);
        Add(new ProjectExplorerNode(BracketId, "Bracket", ComponentKind, false, ProjectExplorerNodeType.Object), SecondaryStructureId);

        // Static field/property initializers all run before this
        // constructor body, regardless of textual order — RootNodes is
        // therefore assigned here, not via its own initializer, so it sees
        // Nodes fully populated.
        RootNodes = [Nodes[AssembliesCategoryId]];
    }

    /// <summary>Gets the tree's own single root node (the "Assemblies" category).</summary>
    public static IReadOnlyList<ProjectExplorerNode> RootNodes { get; }

    /// <summary>Looks up a node by Id, or <see langword="null"/> if unknown.</summary>
    public static ProjectExplorerNode? Find(Guid nodeId) => Nodes.GetValueOrDefault(nodeId);

    /// <summary>Attempts to get <paramref name="nodeId"/>'s own children.</summary>
    /// <returns><see langword="false"/> if <paramref name="nodeId"/> is not a known node.</returns>
    public static bool TryGetChildren(Guid nodeId, out IReadOnlyList<ProjectExplorerNode> children)
    {
        if (!Children.TryGetValue(nodeId, out var childIds))
        {
            children = [];
            return false;
        }

        children = childIds.Select(id => Nodes[id]).ToList();
        return true;
    }

    private static void Add(ProjectExplorerNode node, Guid? parentId)
    {
        Nodes[node.Id] = node;
        Children[node.Id] = [];

        if (parentId is { } id)
            Children[id].Add(node.Id);
    }
}
