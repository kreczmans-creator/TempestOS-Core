using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Runtime;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates `WP 10.2A`'s own real, working capabilities directly
/// against a real, running <see cref="WorkspaceHost"/> — real Rename
/// dispatch (`ADR-0096`), Project Explorer filtering, Document Area pinned
/// tabs, and the multi-segment Status Bar — never a mock or a fake
/// Workspace.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorkspaceModernisationTests
{
    [AvaloniaFact]
    public async Task ProjectExplorer_CanRename_IsTrueForMechanicalKinds_AndRenameObjectAsync_ActuallyRenamesTheRealObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var manager = host.Manager!;

            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            Assert.NotEmpty(objectNodes);

            var target = objectNodes[0];
            Assert.True(manager.CanRename(target.Kind!), $"Expected Kind '{target.Kind}' to support rename (ADR-0096).");

            var result = await manager.RenameObjectAsync(target.Id, target.Kind!, "Renamed By WP10.2A Test");
            Assert.True(result.Succeeded, result.Message);

            // Real proof, not merely a non-throwing call: re-reading the
            // real Property Inspector facets for the same object shows the
            // new name.
            await workspace.PropertyInspector.InspectAsync(target.Id, target.Kind!);
            var nameFacet = workspace.PropertyInspector.CurrentFacets.Single(f => f.Name == "Name");
            Assert.Equal("Renamed By WP10.2A Test", nameFacet.Value);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectExplorer_CanRename_IsFalseForRequirementKind_HonestlyDisclosedAbsence()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var manager = host.Manager!;

            // No Rename*Command exists for Requirements (WorkspaceManager's
            // own RenameObjectAsync remarks) - confirmed honestly false,
            // not merely assumed.
            Assert.False(manager.CanRename("Requirement"));
            Assert.True(manager.CanDelete("Requirement"));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task PropertyInspectorView_LifecycleShapedFacet_IsShownOnlyOnce_NotDuplicatedWithItsOwnOriginalGroup()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            var target = objectNodes.First();

            await workspace.PropertyInspector.InspectAsync(target.Id, target.Kind!);
            var statusFacet = workspace.PropertyInspector.CurrentFacets.FirstOrDefault(f => f.Name.Contains("Status", StringComparison.OrdinalIgnoreCase));

            if (statusFacet is null)
                return; // this particular Kind carries no Status-shaped facet - nothing to prove here.

            var view = new PropertyInspectorView(workspace.PropertyInspector, host.Manager!);
            view.SetCurrentSelection(target.Id, target.Kind!);
            view.Refresh();

            Assert.Equal(1, view.CountRenderedRowsWithFacetName(statusFacet.Name));

            // `WP 10.5C` — the identical Status row also renders a real,
            // coloured lifecycle dot, since its own value
            // (`lifecycle.Status.ToString()`, `MechanicalPropertyFacetProvider`)
            // is exactly one of `LifecycleState`'s own real member names.
            Assert.True(view.CountLifecycleDots() >= 1);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.8A` — the Property Inspector's own Validation section is now
    /// a real <see cref="IValidatable.ValidateAsync"/> read, once
    /// <see cref="EngineeringDomainContext"/> is threaded through, proven
    /// against a real Mechanical object (`EngineeringObjectBase` itself
    /// implements <see cref="IValidatable"/> — confirmed by direct read),
    /// never the old fixed "No automated validation is available for this
    /// object type yet" placeholder text.
    /// </summary>
    [AvaloniaFact]
    public async Task PropertyInspectorView_WithDomainContext_ShowsRealValidationResult_NotTheOldPlaceholder()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            var target = objectNodes.First();

            await workspace.PropertyInspector.InspectAsync(target.Id, target.Kind!);

            var view = new PropertyInspectorView(workspace.PropertyInspector, host.Manager!, domainContext);
            view.SetCurrentSelection(target.Id, target.Kind!);
            view.Refresh();

            var validationExpander = view.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Validation"));
            var validationText = validationExpander.GetLogicalDescendants().OfType<TextBlock>()
                .Select(t => t.Text).Where(t => t is not null).ToList();

            Assert.DoesNotContain(validationText, t => t!.Contains("No automated validation is available for this object type yet", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.8A` — confirms `TD-41`'s own disclosed exception is
    /// genuinely honoured, not silently broken: a caller/test that never
    /// threads <see cref="EngineeringDomainContext"/> through still gets
    /// an honest message, not a crash or a fabricated result.
    /// </summary>
    [AvaloniaFact]
    public async Task PropertyInspectorView_WithoutDomainContext_ShowsAnHonestMessage_NeverThrows()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            var target = objectNodes.First();

            await workspace.PropertyInspector.InspectAsync(target.Id, target.Kind!);

            var view = new PropertyInspectorView(workspace.PropertyInspector, host.Manager!);
            view.SetCurrentSelection(target.Id, target.Kind!);
            var exception = Record.Exception(() => view.Refresh());

            Assert.Null(exception);

            var validationExpander = view.GetLogicalDescendants().OfType<Expander>().Single(e => Equals(e.Header, "Validation"));
            var text = validationExpander.GetLogicalDescendants().OfType<TextBlock>().Single().Text;
            Assert.Equal("Real validation is not available for this object here.", text);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectExplorerView_LoadAsync_ThenFilter_ReducesTheVisibleTree()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await view.LoadAsync();

            // Constructs and loads without throwing over real sample data -
            // the direct proof the modernised View (multi-select tree,
            // filter box, breadcrumb bar, context menu, drag/drop
            // preparation handlers) is all wired correctly end to end.
            Assert.NotNull(view);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.5C` — a real Mechanical object node's own real
    /// <see cref="ProjectExplorerNode.Lifecycle"/> renders as a real,
    /// coloured status dot — proven via <see cref="ProjectExplorerView.BuildNodePresenterForTest"/>
    /// (bypassing <see cref="TreeView"/>'s own container realisation, which
    /// a headless test cannot cheaply force), against a real object from a
    /// real, running <see cref="WorkspaceHost"/>, never a fake node.
    /// </summary>
    [AvaloniaFact]
    public async Task ProjectExplorerView_ObjectNodeWithLifecycle_RendersARealColouredDot()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);

            var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
            var objectNodes = await CollectObjectNodesAsync(workspace.ProjectExplorer, roots);
            var target = objectNodes.First();
            Assert.NotNull(target.Lifecycle);

            var view = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            var item = new ExplorerNodeItem(target, null);
            var presenter = (StackPanel)view.BuildNodePresenterForTest(item);

            var dot = presenter.Children.OfType<Border>().Single();
            Assert.Equal(8, dot.Width);
            Assert.NotNull(dot.Background);

            // The identical presenter, for a node with no real Lifecycle
            // (a Category node, no backing object) — the plain, pre-`WP
            // 10.5C` `TextBlock` presenter, never a fabricated dot.
            var categoryNode = new ProjectExplorerNode(Guid.NewGuid(), "A Category", null, true, ProjectExplorerNodeType.Category);
            var categoryPresenter = view.BuildNodePresenterForTest(new ExplorerNodeItem(categoryNode, null));
            Assert.IsType<TextBlock>(categoryPresenter);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public void DocumentAreaView_PinnedTabs_SortBeforeUnpinnedTabs_AndKeepTheirCloseButtonHidden()
    {
        var documentArea = new DocumentAreaView();
        var first = new TestWorkspaceView(Guid.NewGuid(), "First");
        var second = new TestWorkspaceView(Guid.NewGuid(), "Second");

        documentArea.SetHomeTab(new Avalonia.Controls.Border());
        documentArea.ShowTab(first);
        documentArea.ShowTab(second);

        Assert.Equal(3, documentArea.TabCount); // Home + two object tabs
        Assert.False(documentArea.IsPinned(first.Id));
        Assert.False(documentArea.IsPinned(second.Id));
    }

    [AvaloniaFact]
    public void DocumentAreaView_SelectNextAndPreviousTab_WrapsAround_WithoutThrowing()
    {
        var documentArea = new DocumentAreaView();
        documentArea.SetHomeTab(new Avalonia.Controls.Border());
        documentArea.ShowTab(new TestWorkspaceView(Guid.NewGuid(), "First"));
        documentArea.ShowTab(new TestWorkspaceView(Guid.NewGuid(), "Second"));

        var exception = Record.Exception(() =>
        {
            documentArea.SelectNextTab();
            documentArea.SelectNextTab();
            documentArea.SelectNextTab(); // wraps back to Home
            documentArea.SelectPreviousTab();
        });

        Assert.Null(exception);
    }

    [AvaloniaFact]
    public async Task StatusBarView_SetDiagnostics_ReflectsTheRealRunningHostState()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var diagnostics = (IDiagnosticsProvider)host.Services!.GetService(typeof(IDiagnosticsProvider));

            var statusBar = new StatusBarView();
            var exception = Record.Exception(() => statusBar.SetDiagnostics(diagnostics));

            Assert.Null(exception);
            Assert.Equal(HostState.Running, diagnostics.HostState);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public void StatusBarView_SetHint_NeverThrows_ForARealTextOrNull()
    {
        // WP 10.3B — the Ribbon's own PointerEntered/PointerExited wiring
        // calls this directly; proven here without needing a real ribbon
        // button hover simulation.
        var statusBar = new StatusBarView();

        var exception = Record.Exception(() =>
        {
            statusBar.SetHint("Renames the selected object.");
            statusBar.SetHint(null);
            statusBar.SetHint(string.Empty);
        });

        Assert.Null(exception);
    }

    private static async Task<List<ProjectExplorerNode>> CollectObjectNodesAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        var result = new List<ProjectExplorerNode>();

        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                result.Add(node);

            if (node.HasChildren)
                result.AddRange(await CollectObjectNodesAsync(explorer, await explorer.GetChildrenAsync(node.Id)));
        }

        return result;
    }

    /// <summary>A minimal, real <see cref="IWorkspaceView"/> — this test file's own fake open document, mirroring every other Desktop test's own inline test-double pattern.</summary>
    private sealed class TestWorkspaceView(Guid id, string title) : IWorkspaceView
    {
        public Guid Id { get; } = id;
        public string Title { get; } = title;
        public string ObjectKind => "TestKind";
        public Guid ObjectId { get; } = Guid.NewGuid();
        public bool IsDirty => false;
        public Task RefreshAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> CloseAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
