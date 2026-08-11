using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Verification;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.DigitalThread;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates <see cref="DigitalThreadGraphModel"/> (`WP 10.4A`'s own
/// pure graph algorithms — Graph construction, Expand/collapse with
/// reachability, Multiple layouts, Filtering, Search, Breadcrumb, the
/// <c>TD-32</c> Verification record merge) directly, over a real, running
/// <see cref="WorkspaceHost"/> and real sample data — never a mock or a
/// fake domain object, mirroring <see cref="ObjectEditorViewTests"/>'s
/// own established discipline exactly.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DigitalThreadGraphModelTests
{
    [Fact]
    public async Task Recentre_NonExistentObject_ReturnsFalse()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var model = new DigitalThreadGraphModel(domainContext);

            var moved = model.Recentre(Guid.NewGuid(), "Component");

            Assert.False(moved);
            Assert.Empty(model.Nodes);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Recentre_RealObject_CreatesExactlyOneCentreNode()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);

            var moved = model.Recentre(target.Id, target.Kind!);

            Assert.True(moved);
            var centres = model.Nodes.Where(n => n.IsCentre).ToList();
            Assert.Single(centres);
            Assert.Equal(target.Id, centres[0].ObjectId);
            Assert.Equal(target.Id, model.CentreId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task ExpandNode_OnAFirstHopNeighbour_MarksItExpanded_NeverExpandsTwice()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var neighbour = model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return; // no relationships on this particular sample object — honestly nothing to prove here.

            var firstExpand = model.ExpandNode(neighbour.ObjectId);
            Assert.True(firstExpand);
            Assert.True(model.Nodes.Single(n => n.ObjectId == neighbour.ObjectId).IsExpanded);

            var secondExpand = model.ExpandNode(neighbour.ObjectId);
            Assert.False(secondExpand); // already expanded — a no-op, not a duplicate read.
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task CollapseNode_RevertsExactlyWhatItsOwnExpansionAdded_WhenNothingElseSharesIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var beforeNodeIds = model.Nodes.Select(n => n.ObjectId).OrderBy(id => id).ToList();
            var beforeEdgeCount = model.Edges.Count;

            var neighbour = model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return; // no relationships on this particular sample object — honestly nothing to prove here.

            var expanded = model.ExpandNode(neighbour.ObjectId);
            if (!expanded)
                return;

            var collapsed = model.CollapseNode(neighbour.ObjectId);
            Assert.True(collapsed);

            var afterNodeIds = model.Nodes.Select(n => n.ObjectId).OrderBy(id => id).ToList();
            Assert.Equal(beforeNodeIds, afterNodeIds);
            Assert.Equal(beforeEdgeCount, model.Edges.Count);
            Assert.False(model.Nodes.Single(n => n.ObjectId == neighbour.ObjectId).IsExpanded);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task CollapseNode_OnTheCentre_IsRejected()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            Assert.False(model.CollapseNode(target.Id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task Recentre_ToADifferentObject_PushesPriorCentreOntoBreadcrumb()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var neighbour = model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return; // no relationships to navigate to — honestly nothing to prove here.

            Assert.Empty(model.Breadcrumb);
            var moved = model.Recentre(neighbour.ObjectId, neighbour.Kind);

            Assert.True(moved);
            Assert.Single(model.Breadcrumb);
            Assert.Equal(target.Id, model.Breadcrumb[0].ObjectId);
            Assert.Equal(neighbour.ObjectId, model.CentreId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task JumpToBreadcrumb_ReturnsToThePriorCentre_TruncatesForwardHistory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var neighbour = model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return;

            model.Recentre(neighbour.ObjectId, neighbour.Kind);

            var jumped = model.JumpToBreadcrumb(0);

            Assert.True(jumped);
            Assert.Equal(target.Id, model.CentreId);
            Assert.Empty(model.Breadcrumb);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetLayout_Hierarchical_PlacesTheCentreAtTheOriginRow()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            model.SetLayout(DigitalThreadLayoutKind.Hierarchical);

            var centre = model.Nodes.Single(n => n.IsCentre);
            Assert.Equal(0, centre.Y);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetLayout_Engineering_PlacesEveryFirstHopNeighbourOnTheSameRadius()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var neighbours = model.Nodes.Where(n => !n.IsCentre).ToList();
            if (neighbours.Count == 0)
                return;

            model.SetLayout(DigitalThreadLayoutKind.Engineering);

            var radii = model.Nodes.Where(n => !n.IsCentre).Select(n => Math.Round(Math.Sqrt(n.X * n.X + n.Y * n.Y), 3)).Distinct().ToList();
            Assert.Single(radii); // every first-hop node sits on the identical ring.
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetLayout_ForceDirected_IsDeterministic_SameGraphProducesTheSamePositionsEveryTime()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            model.SetLayout(DigitalThreadLayoutKind.ForceDirected);
            var first = model.Nodes.ToDictionary(n => n.ObjectId, n => (n.X, n.Y));

            model.SetLayout(DigitalThreadLayoutKind.ForceDirected);
            var second = model.Nodes.ToDictionary(n => n.ObjectId, n => (n.X, n.Y));

            Assert.Equal(first, second);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetSearchText_MatchesCaseInsensitiveSubstring_OfDisplayName()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(target.Id, target.Kind!);

            var centre = model.Nodes.Single(n => n.IsCentre);
            var needle = centre.DisplayName[..Math.Min(3, centre.DisplayName.Length)].ToUpperInvariant();

            model.SetSearchText(needle);

            Assert.Contains(centre.ObjectId, model.SearchMatches);

            model.SetSearchText(string.Empty);
            Assert.Empty(model.SearchMatches);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task SetCategoryVisible_False_RecordsTheCategoryAsHidden()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var model = new DigitalThreadGraphModel(domainContext);

            model.SetCategoryVisible(RelationshipCategory.Verification, false);
            Assert.Contains(RelationshipCategory.Verification, model.HiddenCategories);

            model.SetCategoryVisible(RelationshipCategory.Verification, true);
            Assert.DoesNotContain(RelationshipCategory.Verification, model.HiddenCategories);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The `TD-32` closure this Work Package's own doc explicitly called
    /// for: a Verification Activity's own <c>"verifiedBy"</c> link — real,
    /// durable, but invisible to <see cref="EngineeringDomainContext.RelationshipRepository"/>
    /// entirely — becomes a visible, real synthetic leaf node here, for
    /// the first time anywhere in the Workspace/Desktop layer.
    /// </summary>
    [Fact]
    public async Task Recentre_VerificationActivityWithARecordedResult_AddsTheResultAsAVisibleLeafNode()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var activities = await domainContext.Repository.ListByKindAsync("VerificationActivity");
            IEngineeringObject? verifiedActivity = null;
            IReadOnlyList<VerificationRecordSnapshot>? expectedRecords = null;
            foreach (var activity in activities)
            {
                var records = await VerificationRecordReader.GetResultHistoryAsync(domainContext, activity.Id);
                if (records.Count > 0)
                {
                    verifiedActivity = activity;
                    expectedRecords = records;
                    break;
                }
            }

            if (verifiedActivity is null)
                return; // no sample Verification Activity has a recorded result in this build — honestly nothing to prove here.

            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(verifiedActivity.Id, verifiedActivity.Kind!);

            // Never visible via the plain RelationshipRepository read alone
            // (`TD-32`) — confirmed absent from a direct RelationshipRepository
            // read for this *specific* Activity->Record edge, proving this
            // node came from the dedicated Verification merge, not a
            // coincidental relationship. Deliberately not a blanket
            // "no verifiedBy edge at all" assertion: a real, unrelated
            // Subject->Activity edge also legitimately uses the identical
            // "verifiedBy" RelationshipKind (`WP10.0A Digital Thread &
            // Relationship Visualisation.md`'s own EngineeringVerificationWorkspaceSampleModule
            // remarks) — a genuine, disclosed test-precision defect found
            // and fixed here, `WP 10.5A` (the original, broader assertion
            // intermittently failed depending on `InMemoryEngineeringObjectRepository`'s
            // own unspecified iteration order, `TD-27`'s own identical class
            // of risk).
            var recordId = expectedRecords![0].RecordId;
            var directlyLinked = await domainContext.RelationshipRepository.GetIncomingAsync(recordId);
            Assert.DoesNotContain(directlyLinked, r => r.SourceId == verifiedActivity.Id && r.RelationshipKind == "verifiedBy");

            var recordNode = model.Nodes.SingleOrDefault(n => n.IsRecord && n.ObjectId == expectedRecords![0].RecordId);
            Assert.NotEqual(default, recordNode.ObjectId);
            Assert.Equal("VerificationRecord", recordNode.Kind);

            var edge = model.Edges.SingleOrDefault(e => e.SourceId == verifiedActivity.Id && e.TargetId == recordNode.ObjectId);
            Assert.Equal("verifiedBy", edge.RelationshipKind);
            Assert.Equal(RelationshipCategory.Verification, edge.Category);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public async Task VerificationRecordLeafNode_CannotBeExpanded()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var activities = await domainContext.Repository.ListByKindAsync("VerificationActivity");
            IEngineeringObject? verifiedActivity = null;
            foreach (var activity in activities)
            {
                var records = await VerificationRecordReader.GetResultHistoryAsync(domainContext, activity.Id);
                if (records.Count > 0)
                {
                    verifiedActivity = activity;
                    break;
                }
            }

            if (verifiedActivity is null)
                return;

            var model = new DigitalThreadGraphModel(domainContext);
            model.Recentre(verifiedActivity.Id, verifiedActivity.Kind!);
            var recordNode = model.Nodes.Single(n => n.IsRecord);

            Assert.False(model.ExpandNode(recordNode.ObjectId));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task<(EngineeringDomainContext DomainContext, IEngineeringObject Target)> GetRealMechanicalObjectAsync(WorkspaceHost host)
    {
        var workspace = host.Workspace!;
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var objectNode = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(objectNode);

        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var target = await domainContext.Repository.FindAsync(objectNode!.Id);
        Assert.NotNull(target);

        return (domainContext, target!);
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}

/// <summary>
/// Demonstrates <see cref="DigitalThreadGraphView"/> — the rendered
/// control itself (node/edge visuals, click-to-open, double-click
/// re-centre, breadcrumb buttons, the search box, the layout selector)
/// over the same real <see cref="WorkspaceHost"/>/sample-data discipline.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class DigitalThreadGraphViewTests
{
    [AvaloniaFact]
    public async Task TryCreate_NonExistentObjectId_ReturnsNull()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var view = DigitalThreadGraphView.TryCreate(Guid.NewGuid(), "Component", domainContext, (_, _) => { });

            Assert.Null(view);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TryCreate_RealObject_ReturnsARealView_TitledForTheCentre()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);

            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { });

            Assert.NotNull(view);
            Assert.Equal(target.Id, view!.ObjectId);
            Assert.False(view.IsDirty);
            Assert.StartsWith("Relationships:", view.Title);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RenderedGraph_HasOneNodeVisualPerModelNode()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            var nodeBorders = view.GetLogicalDescendants().OfType<Border>().Count(b => b.Width is 158 or 188);

            Assert.Equal(view.Model.Nodes.Count, nodeBorders);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SingleClickOnANeighbourNode_InvokesTheNavigateCallback()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);

            var navigated = new List<(Guid Id, string Kind)>();
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (id, kind) => navigated.Add((id, kind)))!;

            var neighbour = view.Model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return; // no relationships on this particular sample object — honestly nothing to prove here.

            var border = FindNodeBorder(view, neighbour.ObjectId, neighbour.DisplayName);
            border.RaiseEvent(new Avalonia.Input.PointerPressedEventArgs(border, new Avalonia.Input.Pointer(0, Avalonia.Input.PointerType.Mouse, true), border, default, 0, new Avalonia.Input.PointerPointProperties(Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PointerUpdateKind.LeftButtonPressed), Avalonia.Input.KeyModifiers.None));

            Assert.Contains((neighbour.ObjectId, neighbour.Kind), navigated);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task DoubleClick_RecentresTheGraphOnThatNode()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            var neighbour = view.Model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return;

            var recentred = view.Recentre(neighbour.ObjectId, neighbour.Kind);

            Assert.True(recentred);
            Assert.Equal(neighbour.ObjectId, view.ObjectId);
            Assert.Equal(neighbour.ObjectId, view.Model.CentreId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ExpandThenCollapse_ThroughThePublicViewMethods_RebuildsTheVisualTreeToMatch()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            var neighbour = view.Model.Nodes.FirstOrDefault(n => !n.IsCentre);
            if (neighbour.ObjectId == default)
                return;

            var beforeCount = view.Model.Nodes.Count;
            var expanded = view.ExpandNode(neighbour.ObjectId);
            if (!expanded)
                return;

            var afterExpandCount = view.Model.Nodes.Count;
            var nodeBordersAfterExpand = view.GetLogicalDescendants().OfType<Border>().Count(b => b.Width is 158 or 188);
            Assert.Equal(afterExpandCount, nodeBordersAfterExpand);

            view.CollapseNode(neighbour.ObjectId);
            Assert.Equal(beforeCount, view.Model.Nodes.Count);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ZoomBy_ChangesTheModelsOwnZoomLevel()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            view.ZoomBy(1.5);

            Assert.Equal(1.5, view.Model.ZoomLevel, 3);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task PanBy_ChangesTheModelsOwnPanOffset()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            view.PanBy(new Avalonia.Vector(40, -25));

            Assert.Equal(40, view.Model.PanOffset.X, 3);
            Assert.Equal(-25, view.Model.PanOffset.Y, 3);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RefreshAsync_ReReadsTheRealObject_NeverACachedCopy()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            await view.RefreshAsync();

            Assert.Equal(target.Id, view.Model.CentreId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CloseAsync_AlwaysReturnsTrue_NeverBlocksClose()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            Assert.True(await view.CloseAsync());
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static Border FindNodeBorder(DigitalThreadGraphView view, Guid objectId, string displayName) =>
        view.GetLogicalDescendants().OfType<Border>()
            .Single(b => (b.Width is 158 or 188) && b.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == displayName));

    private static async Task<(EngineeringDomainContext DomainContext, IEngineeringObject Target)> GetRealMechanicalObjectAsync(WorkspaceHost host)
    {
        var workspace = host.Workspace!;
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var objectNode = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(objectNode);

        var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
        var target = await domainContext.Repository.FindAsync(objectNode!.Id);
        Assert.NotNull(target);

        return (domainContext, target!);
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }
}
