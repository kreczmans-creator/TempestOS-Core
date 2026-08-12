using Avalonia;
using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Verification;

namespace Tempest.Desktop.DigitalThread;

/// <summary>The three named graph layouts (`WP 10.4A` scope: "Multiple graph layouts (hierarchical, force-directed, engineering)").</summary>
public enum DigitalThreadLayoutKind
{
    /// <summary>Rows by hop-depth from the centre, each row spread evenly — a classic tree layout.</summary>
    Hierarchical,

    /// <summary>A simple, deterministic, seeded spring simulation (repulsion between every pair, attraction along edges, weak centring) — never a random, non-reproducible layout.</summary>
    ForceDirected,

    /// <summary>Concentric rings by hop-depth, angularly spread — the "engineering" convention used by real Digital Thread/traceability tools, keeping the centre object's own visual prominence (`WP10.0A Digital Thread &amp; Relationship Visualisation.md` §2.3) regardless of graph size.</summary>
    Engineering,
}

/// <summary>One graph node's own read-only, immutable snapshot — the public surface <see cref="DigitalThreadGraphModel"/> exposes; never the live, mutable internal <c>GraphNode</c> itself.</summary>
public readonly record struct DigitalThreadNodeSnapshot(Guid ObjectId, string Kind, string DisplayName, LifecycleState? Status, bool IsCentre, bool IsExpanded, bool IsRecord, double X, double Y);

/// <summary>One graph edge's own read-only, immutable snapshot.</summary>
public readonly record struct DigitalThreadEdgeSnapshot(Guid SourceId, Guid TargetId, string RelationshipKind, RelationshipCategory Category);

/// <summary>One entry in the centre-navigation breadcrumb trail (`WP 10.4A` scope: "Breadcrumb path display").</summary>
public readonly record struct DigitalThreadBreadcrumbEntry(Guid ObjectId, string Kind, string DisplayName);

/// <summary>
/// The Digital Thread graph's own real, pure, Avalonia-rendering-free
/// state and algorithms (`WP 10.4A`, realising `ADR-0093` and
/// `WP10.0A Digital Thread &amp; Relationship Visualisation.md`) —
/// everything <see cref="Views.DigitalThreadGraphView"/> renders is
/// read from this class, never computed inline in the view.
/// </summary>
/// <remarks>
/// <para>
/// <b>Progressive, client-side, on-demand expansion only</b> — every
/// <see cref="ExpandNode"/> call issues a fresh read of that node's own
/// direct relationships (never a precomputed or cached transitive
/// traversal, `ADR-0093`). Nothing here is persisted; the entire graph is
/// discarded the moment the owning View closes (`WP10.0A` doc §2.2).
/// </para>
/// <para>
/// <b>Reads only, exactly the same already-permitted reads
/// <see cref="Editors.ObjectEditorView"/>'s own Relationship summary
/// established (`WP 10.3A`)</b> — <see cref="IHasRelationships.GetRelationshipsAsync"/>
/// (outgoing) and <see cref="EngineeringDomainContext.RelationshipRepository"/>'s
/// own <c>GetIncomingAsync</c> (incoming), generic over every Kind, zero
/// per-discipline special-casing, `ADR-0063`. This is a deliberate choice
/// over <see cref="IEvidenceComposer"/>/<see cref="IEvidence"/>: the
/// latter is outgoing-only and Requirements-specific in the ADR's own
/// wording, where the bidirectional pattern here is already proven,
/// already tested, and uniform across all six disciplines — see
/// `WP10.4A Architecture Review.md` §2 for the full comparison.
/// </para>
/// <para>
/// <b>One disclosed, deliberate exception — <c>TD-32</c></b>. A
/// Verification Activity's own <c>"verifiedBy"</c> link to its recorded
/// result is written via <c>IEngineeringDocumentStore.LinkAsync</c>
/// directly, never through <see cref="IHasRelationships.LinkAsync"/> — so
/// it is invisible to <see cref="EngineeringDomainContext.RelationshipRepository"/>
/// entirely (confirmed: even <see cref="Editors.ObjectEditorView"/>'s own
/// Relationship summary already silently misses it, a pre-existing gap
/// this Work Package newly discloses but does not remediate, out of
/// scope). Expanding a <c>"VerificationActivity"</c> node additionally
/// calls <see cref="VerificationRecordReader.GetResultHistoryAsync"/> —
/// the same raw-store read that method's own class already established
/// — and adds each result as a synthetic, non-expandable, non-editable
/// leaf node, making the previously-invisible link visible here for the
/// first time. See `WP10.4A Engineering Review.md` §3 and the Technical
/// Debt Register's own updated <c>TD-32</c> disposition.
/// </para>
/// </remarks>
/// <remarks>
/// **`WP 12.1B` (`ADR-0105`).** Previously declared three private local
/// constants (<c>VerificationActivityKind</c>, <c>VerificationRecordKind</c>,
/// <c>VerifiedByRelationshipKind</c>) duplicating values already
/// canonically owned elsewhere — <see cref="VerificationService.VerificationRecordDocumentKind"/>/
/// <see cref="VerificationService.VerifiedByRelationshipKind"/>
/// (`Tempest.Core.Verification`) and
/// <see cref="VerificationActivityFactoryRegistry.SupportedKind"/>
/// (`Tempest.App.Workspace.Verification`). This was the confirmed,
/// motivating cross-layer duplicate `WP 12.1A`'s own investigation
/// found — closed by referencing each owning constant directly instead.
/// No value, no behaviour changed.
/// </remarks>
public sealed class DigitalThreadGraphModel
{
    private readonly EngineeringDomainContext _domainContext;
    private readonly Dictionary<Guid, GraphNode> _nodes = new();
    private readonly List<Guid> _nodeOrder = new();
    private readonly List<GraphEdge> _edges = new();
    private readonly HashSet<(Guid Source, Guid Target, string Kind)> _edgeSignatures = new();
    private readonly List<DigitalThreadBreadcrumbEntry> _breadcrumb = new();
    private readonly HashSet<RelationshipCategory> _hiddenCategories = new();

    private string _searchText = string.Empty;
    private IReadOnlyList<Guid> _searchMatches = Array.Empty<Guid>();

    public DigitalThreadGraphModel(EngineeringDomainContext domainContext)
    {
        ArgumentNullException.ThrowIfNull(domainContext);
        _domainContext = domainContext;
    }

    /// <summary>The graph's own current centre object — "Selected object centring" (`WP 10.4A` scope), always the object last centred via <see cref="Recentre"/>.</summary>
    public Guid CentreId { get; private set; }

    /// <summary>Every node currently in the graph, in stable insertion order.</summary>
    public IReadOnlyList<DigitalThreadNodeSnapshot> Nodes => _nodeOrder.Select(id => ToSnapshot(_nodes[id])).ToList();

    /// <summary>Every edge currently in the graph, in stable insertion order.</summary>
    public IReadOnlyList<DigitalThreadEdgeSnapshot> Edges => _edges.Select(ToSnapshot).ToList();

    /// <summary>The centre-navigation trail — every centre this graph has moved through via <see cref="Recentre"/>, oldest first, current centre not included (`WP 10.4A` scope: "Breadcrumb path display").</summary>
    public IReadOnlyList<DigitalThreadBreadcrumbEntry> Breadcrumb => _breadcrumb;

    /// <summary>The currently active layout algorithm.</summary>
    public DigitalThreadLayoutKind Layout { get; private set; } = DigitalThreadLayoutKind.Hierarchical;

    /// <summary>The current zoom factor, clamped to [0.2, 3.0].</summary>
    public double ZoomLevel { get; private set; } = 1.0;

    /// <summary>The current pan offset, applied to every node position at render time.</summary>
    public Point PanOffset { get; private set; }

    /// <summary>Every <see cref="RelationshipCategory"/> currently hidden by "Relationship filtering" (`WP 10.4A` scope) — empty by default (everything visible).</summary>
    public IReadOnlySet<RelationshipCategory> HiddenCategories => _hiddenCategories;

    /// <summary>The current "Object search" (`WP 10.4A` scope) query text.</summary>
    public string SearchText => _searchText;

    /// <summary>Every node Id whose <see cref="DigitalThreadNodeSnapshot.DisplayName"/> currently matches <see cref="SearchText"/> — empty whenever the query is blank.</summary>
    public IReadOnlyList<Guid> SearchMatches => _searchMatches;

    /// <summary>The currently selected node, if any — drives node highlighting ("Relationship highlighting", `WP 10.4A` scope).</summary>
    public Guid? SelectedNodeId { get; private set; }

    /// <summary>The currently selected edge, if any — drives the Relationship inspector (`WP 10.4A` scope).</summary>
    public DigitalThreadEdgeSnapshot? SelectedEdge { get; private set; }

    /// <summary>
    /// Rebuilds the graph fresh around <paramref name="objectId"/> — the
    /// object becomes the new centre node, styled distinctly, with its
    /// own direct relationships added as collapsed neighbour nodes
    /// (`WP10.0A` doc §2.1 steps 1-3). Returns <see langword="false"/>,
    /// leaving the existing graph untouched, if no object with that Id
    /// exists. The prior centre (if any) is pushed onto
    /// <see cref="Breadcrumb"/> first — "Double-click navigation"
    /// (`WP 10.4A` scope) re-centres this way.
    /// </summary>
    public bool Recentre(Guid objectId, string kind)
    {
        if (!BuildGraphAround(objectId, kind, pushCurrentCentreToBreadcrumb: true))
            return false;
        return true;
    }

    /// <summary>
    /// Jumps back to a prior centre named in <see cref="Breadcrumb"/> at
    /// <paramref name="index"/>, discarding every later entry — including
    /// the entry for the centre being navigated <i>away</i> from, which
    /// standard breadcrumb "back" semantics never re-adds (unlike
    /// <see cref="Recentre"/>'s own forward-navigation push).
    /// </summary>
    public bool JumpToBreadcrumb(int index)
    {
        if (index < 0 || index >= _breadcrumb.Count)
            return false;

        var target = _breadcrumb[index];
        _breadcrumb.RemoveRange(index, _breadcrumb.Count - index);
        return BuildGraphAround(target.ObjectId, target.Kind, pushCurrentCentreToBreadcrumb: false);
    }

    private bool BuildGraphAround(Guid objectId, string kind, bool pushCurrentCentreToBreadcrumb)
    {
        var target = _domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();
        if (target is null)
            return false;

        if (pushCurrentCentreToBreadcrumb && _nodeOrder.Count > 0 && _nodes.TryGetValue(CentreId, out var previousCentre))
            _breadcrumb.Add(new DigitalThreadBreadcrumbEntry(previousCentre.ObjectId, previousCentre.Kind, previousCentre.DisplayName));

        _nodes.Clear();
        _nodeOrder.Clear();
        _edges.Clear();
        _edgeSignatures.Clear();
        SelectedNodeId = null;
        SelectedEdge = null;
        SetSearchText(string.Empty);

        CentreId = objectId;
        var displayName = (target as IHasBusinessIdentifier)?.DisplayName ?? kind;
        var status = (target as IHasLifecycle)?.Status;
        AddNode(new GraphNode(objectId, kind, displayName, status, isCentre: true, isExpanded: true, isRecord: false));

        LoadRelationships(objectId);
        RecomputeLayout();
        ResetView();
        return true;
    }

    /// <summary>
    /// Expands <paramref name="nodeId"/> — issues a fresh read of its own
    /// direct relationships, adding any not already present as new,
    /// collapsed neighbour nodes/edges (`WP10.0A` doc §2.1 step 4).
    /// Returns <see langword="false"/> if the node does not exist, is
    /// already expanded, or is a synthetic Verification record leaf
    /// (nothing further to expand).
    /// </summary>
    public bool ExpandNode(Guid nodeId)
    {
        if (!_nodes.TryGetValue(nodeId, out var node) || node.IsExpanded || node.IsRecord)
            return false;

        node.IsExpanded = true;
        LoadRelationships(nodeId);
        RecomputeLayout();
        return true;
    }

    /// <summary>
    /// Collapses <paramref name="nodeId"/> — removes every node/edge added
    /// only because of that node's own expansion, unless still reachable
    /// through another currently-expanded path (`WP10.0A` doc §2.1 step
    /// 5, implemented as a full reachability recompute from the centre,
    /// see <see cref="Prune"/>). The centre node can never be collapsed.
    /// </summary>
    public bool CollapseNode(Guid nodeId)
    {
        if (nodeId == CentreId || !_nodes.TryGetValue(nodeId, out var node) || !node.IsExpanded)
            return false;

        node.IsExpanded = false;
        Prune();
        RecomputeLayout();
        return true;
    }

    /// <summary>Switches the active layout and recomputes every node position (`WP 10.4A` scope: "Multiple graph layouts").</summary>
    public void SetLayout(DigitalThreadLayoutKind layout)
    {
        Layout = layout;
        RecomputeLayout();
    }

    /// <summary>Multiplies <see cref="ZoomLevel"/> by <paramref name="factor"/>, clamped to [0.2, 3.0] (`WP 10.4A` scope: "Zoom").</summary>
    public void ZoomBy(double factor) => ZoomLevel = Math.Clamp(ZoomLevel * factor, 0.2, 3.0);

    /// <summary>Offsets <see cref="PanOffset"/> by <paramref name="delta"/> (`WP 10.4A` scope: "Pan").</summary>
    public void PanBy(Vector delta) => PanOffset += delta;

    /// <summary>Resets zoom to 1.0 and pan to the origin — also applied automatically after every <see cref="Recentre"/>, keeping the new centre visually prominent (`WP10.0A` doc §2.3).</summary>
    public void ResetView()
    {
        ZoomLevel = 1.0;
        PanOffset = default;
    }

    /// <summary>Shows or hides every edge (and, transitively, any node left with no other visible edge) of <paramref name="category"/> — "Relationship filtering" (`WP 10.4A` scope). Never removes data from the model, only from the render-visible set surfaced separately by the caller via <see cref="HiddenCategories"/>.</summary>
    public void SetCategoryVisible(RelationshipCategory category, bool visible)
    {
        if (visible)
            _hiddenCategories.Remove(category);
        else
            _hiddenCategories.Add(category);
    }

    /// <summary>Updates the "Object search" (`WP 10.4A` scope) query and recomputes <see cref="SearchMatches"/> against every node currently in the graph.</summary>
    public void SetSearchText(string text)
    {
        _searchText = text ?? string.Empty;
        _searchMatches = string.IsNullOrWhiteSpace(_searchText)
            ? Array.Empty<Guid>()
            : _nodeOrder.Where(id => _nodes[id].DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>Selects <paramref name="nodeId"/> (or clears selection if <see langword="null"/>) — drives "Relationship highlighting".</summary>
    public void SelectNode(Guid? nodeId) => SelectedNodeId = nodeId;

    /// <summary>Selects <paramref name="edge"/> (or clears selection if <see langword="null"/>) — drives the Relationship inspector.</summary>
    public void SelectEdge(DigitalThreadEdgeSnapshot? edge) => SelectedEdge = edge;

    private void AddNode(GraphNode node)
    {
        if (_nodes.TryAdd(node.ObjectId, node))
            _nodeOrder.Add(node.ObjectId);
    }

    private void AddEdge(Guid sourceId, Guid targetId, string relationshipKind, RelationshipCategory category)
    {
        var signature = (sourceId, targetId, relationshipKind);
        if (_edgeSignatures.Add(signature))
            _edges.Add(new GraphEdge(sourceId, targetId, relationshipKind, category));
    }

    /// <summary>
    /// The single, generic composed read every discipline shares — bidirectional
    /// relationships via the same reads <see cref="Editors.ObjectEditorView"/>
    /// already established, plus the Verification-specific <c>TD-32</c>
    /// merge described in this class's own remarks. "No new read is added
    /// for any discipline" (`WP10.0A` doc §4) — this is the identical read
    /// regardless of <paramref name="objectId"/>'s own Kind.
    /// </summary>
    private void LoadRelationships(Guid objectId)
    {
        var target = _domainContext.Repository.FindAsync(objectId).GetAwaiter().GetResult();
        if (target is null)
            return;

        if (target is IHasRelationships hasRelationships)
        {
            var outgoing = hasRelationships.GetRelationshipsAsync().GetAwaiter().GetResult();
            foreach (var relationship in outgoing)
                AddNeighbour(objectId, relationship.SourceId, relationship.TargetId, relationship.RelationshipKind, relationship.Category);
        }

        var incoming = _domainContext.RelationshipRepository.GetIncomingAsync(objectId).GetAwaiter().GetResult();
        foreach (var relationship in incoming)
            AddNeighbour(objectId, relationship.SourceId, relationship.TargetId, relationship.RelationshipKind, relationship.Category);

        if (string.Equals(target.Kind, VerificationActivityFactoryRegistry.SupportedKind, StringComparison.Ordinal))
        {
            var records = VerificationRecordReader.GetResultHistoryAsync(_domainContext, objectId).GetAwaiter().GetResult();
            foreach (var record in records)
            {
                var recordNode = new GraphNode(record.RecordId, VerificationService.VerificationRecordDocumentKind, $"{record.Outcome} — {record.Method}", status: null, isCentre: false, isExpanded: true, isRecord: true);
                AddNode(recordNode);
                AddEdge(objectId, record.RecordId, VerificationService.VerifiedByRelationshipKind, RelationshipCategory.Verification);
            }
        }
    }

    /// <summary>
    /// Adds one relationship read while expanding <paramref name="expandingId"/> —
    /// both <see cref="LoadRelationships"/> call sites read relationships
    /// naming <paramref name="expandingId"/> on exactly one side (outgoing:
    /// always the source; incoming: always the target), so the neighbour
    /// is simply whichever side is not <paramref name="expandingId"/>,
    /// never inferred from graph state.
    /// </summary>
    private void AddNeighbour(Guid expandingId, Guid sourceId, Guid targetId, string relationshipKind, RelationshipCategory category)
    {
        var neighbourId = sourceId == expandingId ? targetId : sourceId;

        if (!_nodes.ContainsKey(neighbourId))
        {
            var neighbour = _domainContext.Repository.FindAsync(neighbourId).GetAwaiter().GetResult();
            var displayName = (neighbour as IHasBusinessIdentifier)?.DisplayName ?? neighbourId.ToString();
            var kind = neighbour?.Kind ?? string.Empty;
            var status = (neighbour as IHasLifecycle)?.Status;
            AddNode(new GraphNode(neighbourId, kind, displayName, status, isCentre: false, isExpanded: false, isRecord: false));
        }

        AddEdge(sourceId, targetId, relationshipKind, category);
    }

    /// <summary>Reachability recompute from the centre, honouring shared reachability across multiple expanded paths (`WP10.0A` doc §2.1 step 5).</summary>
    private void Prune()
    {
        var reachable = new HashSet<Guid> { CentreId };
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var edge in _edges)
            {
                var sourceActive = edge.SourceId == CentreId || (_nodes.TryGetValue(edge.SourceId, out var sourceNode) && sourceNode.IsExpanded);
                var targetActive = edge.TargetId == CentreId || (_nodes.TryGetValue(edge.TargetId, out var targetNode) && targetNode.IsExpanded);

                if (reachable.Contains(edge.SourceId) && sourceActive && reachable.Add(edge.TargetId))
                    changed = true;
                if (reachable.Contains(edge.TargetId) && targetActive && reachable.Add(edge.SourceId))
                    changed = true;
            }
        }

        for (var i = _nodeOrder.Count - 1; i >= 0; i--)
        {
            if (reachable.Contains(_nodeOrder[i]))
                continue;
            _nodes.Remove(_nodeOrder[i]);
            _nodeOrder.RemoveAt(i);
        }

        _edges.RemoveAll(edge => !reachable.Contains(edge.SourceId) || !reachable.Contains(edge.TargetId));
        _edgeSignatures.RemoveWhere(sig => !reachable.Contains(sig.Source) || !reachable.Contains(sig.Target));

        if (SelectedNodeId is { } selected && !reachable.Contains(selected))
            SelectedNodeId = null;
        if (SelectedEdge is { } selectedEdge && (!reachable.Contains(selectedEdge.SourceId) || !reachable.Contains(selectedEdge.TargetId)))
            SelectedEdge = null;
    }

    private Dictionary<Guid, int> ComputeDepths()
    {
        var depths = new Dictionary<Guid, int> { [CentreId] = 0 };
        var frontier = new Queue<Guid>();
        frontier.Enqueue(CentreId);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();
            var currentDepth = depths[current];

            foreach (var edge in _edges)
            {
                var other = edge.SourceId == current ? edge.TargetId : edge.TargetId == current ? edge.SourceId : (Guid?)null;
                if (other is not { } otherId || depths.ContainsKey(otherId))
                    continue;

                depths[otherId] = currentDepth + 1;
                frontier.Enqueue(otherId);
            }
        }

        return depths;
    }

    private void RecomputeLayout()
    {
        switch (Layout)
        {
            case DigitalThreadLayoutKind.Hierarchical:
                LayoutHierarchical();
                break;
            case DigitalThreadLayoutKind.Engineering:
                LayoutEngineering();
                break;
            case DigitalThreadLayoutKind.ForceDirected:
                LayoutForceDirected();
                break;
        }
    }

    private void LayoutHierarchical()
    {
        const double rowSpacing = 140;
        const double colSpacing = 190;

        var depths = ComputeDepths();
        var byDepth = _nodeOrder
            .Select(id => (Node: _nodes[id], Depth: depths.GetValueOrDefault(id, 0)))
            .GroupBy(x => x.Depth)
            .OrderBy(g => g.Key);

        foreach (var group in byDepth)
        {
            var items = group.Select(x => x.Node).OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            var totalWidth = (items.Count - 1) * colSpacing;
            var startX = -totalWidth / 2.0;
            for (var i = 0; i < items.Count; i++)
            {
                items[i].X = startX + i * colSpacing;
                items[i].Y = group.Key * rowSpacing;
            }
        }
    }

    private void LayoutEngineering()
    {
        const double ringSpacing = 170;

        var depths = ComputeDepths();
        var byDepth = _nodeOrder
            .Select(id => (Node: _nodes[id], Depth: depths.GetValueOrDefault(id, 0)))
            .GroupBy(x => x.Depth)
            .OrderBy(g => g.Key);

        foreach (var group in byDepth)
        {
            if (group.Key == 0)
            {
                foreach (var (node, _) in group)
                {
                    node.X = 0;
                    node.Y = 0;
                }
                continue;
            }

            var items = group.Select(x => x.Node).OrderBy(n => n.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
            var radius = group.Key * ringSpacing;
            var angleStep = 2 * Math.PI / items.Count;
            for (var i = 0; i < items.Count; i++)
            {
                var angle = i * angleStep;
                items[i].X = radius * Math.Cos(angle);
                items[i].Y = radius * Math.Sin(angle);
            }
        }
    }

    /// <summary>
    /// A simple, deterministic Fruchterman-Reingold-style spring
    /// simulation: seeded initial placement (reusing the Engineering
    /// layout's own radial start, never random node ordering), 80 fixed
    /// iterations of pairwise repulsion + edge attraction + weak
    /// centring, damped. Deterministic given the same graph — required
    /// for reproducible tests and a stable-feeling UI (re-running the
    /// same layout twice never jumps).
    /// </summary>
    private void LayoutForceDirected()
    {
        const int iterations = 80;
        const double repulsion = 12000;
        const double springLength = 170;
        const double springStrength = 0.02;
        const double centring = 0.01;
        const double damping = 0.85;

        LayoutEngineering();

        var ids = _nodeOrder;
        var velocity = ids.ToDictionary(id => id, _ => new Vector(0, 0));

        for (var iter = 0; iter < iterations; iter++)
        {
            var forces = ids.ToDictionary(id => id, _ => new Vector(0, 0));

            for (var i = 0; i < ids.Count; i++)
            {
                for (var j = i + 1; j < ids.Count; j++)
                {
                    var a = _nodes[ids[i]];
                    var b = _nodes[ids[j]];
                    var dx = a.X - b.X;
                    var dy = a.Y - b.Y;
                    var distSq = Math.Max(dx * dx + dy * dy, 1.0);
                    var dist = Math.Sqrt(distSq);
                    var force = repulsion / distSq;
                    var fx = dx / dist * force;
                    var fy = dy / dist * force;
                    forces[ids[i]] += new Vector(fx, fy);
                    forces[ids[j]] -= new Vector(fx, fy);
                }
            }

            foreach (var edge in _edges)
            {
                if (!_nodes.TryGetValue(edge.SourceId, out var a) || !_nodes.TryGetValue(edge.TargetId, out var b))
                    continue;
                var dx = b.X - a.X;
                var dy = b.Y - a.Y;
                var dist = Math.Max(Math.Sqrt(dx * dx + dy * dy), 1.0);
                var displacement = dist - springLength;
                var fx = dx / dist * displacement * springStrength;
                var fy = dy / dist * displacement * springStrength;
                forces[edge.SourceId] += new Vector(fx, fy);
                forces[edge.TargetId] -= new Vector(fx, fy);
            }

            foreach (var id in ids)
            {
                var node = _nodes[id];
                if (node.IsCentre)
                    continue;

                forces[id] -= new Vector(node.X * centring, node.Y * centring);
                velocity[id] = (velocity[id] + forces[id]) * damping;
                node.X += velocity[id].X;
                node.Y += velocity[id].Y;
            }
        }

        _nodes[CentreId].X = 0;
        _nodes[CentreId].Y = 0;
    }

    private static DigitalThreadNodeSnapshot ToSnapshot(GraphNode node) =>
        new(node.ObjectId, node.Kind, node.DisplayName, node.Status, node.IsCentre, node.IsExpanded, node.IsRecord, node.X, node.Y);

    private static DigitalThreadEdgeSnapshot ToSnapshot(GraphEdge edge) =>
        new(edge.SourceId, edge.TargetId, edge.RelationshipKind, edge.Category);

    /// <summary>One graph node's own live, mutable internal state — never exposed directly, only via <see cref="DigitalThreadNodeSnapshot"/>.</summary>
    private sealed class GraphNode
    {
        public GraphNode(Guid objectId, string kind, string displayName, LifecycleState? status, bool isCentre, bool isExpanded, bool isRecord)
        {
            ObjectId = objectId;
            Kind = kind;
            DisplayName = displayName;
            Status = status;
            IsCentre = isCentre;
            IsExpanded = isExpanded;
            IsRecord = isRecord;
        }

        public Guid ObjectId { get; }
        public string Kind { get; }
        public string DisplayName { get; }
        public LifecycleState? Status { get; }
        public bool IsCentre { get; }
        public bool IsExpanded { get; set; }
        public bool IsRecord { get; }
        public double X { get; set; }
        public double Y { get; set; }
    }

    private sealed record GraphEdge(Guid SourceId, Guid TargetId, string RelationshipKind, RelationshipCategory Category);
}
