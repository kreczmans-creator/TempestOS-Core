using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;
using Tempest.Samples;
using Tempest.App.Workspace.Mechanical;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates the Engineering Ribbon (`WP 10.3B`'s own "Demonstrate"
/// list — Engineering ribbon, Context-sensitive ribbon tabs, Command
/// grouping, Selection-aware commands, Context-sensitive enable/disable,
/// Recently-used commands, Workspace command categories) directly against
/// <see cref="RibbonView"/>, over a real, running <see cref="WorkspaceHost"/>
/// and real Mechanical sample data — never a mock or a fake command
/// registry.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class RibbonViewTests
{
    [AvaloniaFact]
    public async Task Construction_BuildsOneTabPerRealCommandCategory()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var expectedCategories = registry.Items.Select(d => d.Category ?? "General").Distinct().Count();
            Assert.Equal(expectedCategories, CountTabs(ribbon));
            Assert.True(CountTabs(ribbon) >= 6, "Expected at least the six real Engineering Discipline categories.");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `WP 10.5C` — "engineering colour language" — every real discipline
    /// tab's own header now carries a real, distinctly-coloured accent dot
    /// (<c>DisciplineColors</c>), and every one of the six real
    /// disciplines resolves to a genuinely different colour from every
    /// other — never the same fallback grey for two real, distinct
    /// disciplines.
    /// </summary>
    [AvaloniaFact]
    public async Task Construction_EveryDisciplineTab_HasADistinctColouredHeaderAccent()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var tabs = (TabControl)ribbon.Content!;
            var accentsByCategory = tabs.Items
                .OfType<TabItem>()
                .ToDictionary(t => (string)t.Tag!, t => ((Border)((StackPanel)t.Header!).Children[0]).Background);

            // The six real Engineering Disciplines every WorkspaceRegistration
            // file actually registers (confirmed directly, `WP10.5C Runtime
            // UX Traceability Matrix.md` §2).
            var disciplines = new[] { "Mechanical", "Requirements", "Calculations", "Verification", "Documents", "Manufacturing" };
            foreach (var discipline in disciplines)
                Assert.True(accentsByCategory.ContainsKey(discipline), $"Expected a real '{discipline}' tab.");

            var distinctAccents = disciplines.Select(d => accentsByCategory[d]).Distinct().Count();
            Assert.Equal(disciplines.Length, distinctAccents);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SelectTabForArea_MatchesByCategorySubstring_NeverThrowsForAnUnmatchedTitle()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var exception = Record.Exception(() =>
            {
                ribbon.SelectTabForArea("Mechanical Product Structure"); // real area title, contains "Mechanical"
                ribbon.SelectTabForArea("Some Unrelated Title");
                ribbon.SelectTabForArea(null);
            });

            Assert.Null(exception);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task DeleteButton_WithNoSelection_RaisesAnHonestActionCompleted_NeverThrows()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var messages = new List<string>();
            var outcomes = new List<Tempest.Desktop.ActionOutcome>();
            ribbon.ActionCompleted += (message, outcome) => { messages.Add(message); outcomes.Add(outcome); };

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("selected", StringComparison.OrdinalIgnoreCase));

            // `TD-58`: a refusal reports Failed with no workspace change,
            // so subscribers must not rebuild anything for it.
            Assert.All(outcomes, o => Assert.Equal(Tempest.Desktop.ActionOutcome.Failed, o));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task DeleteButton_WithARealSelection_ActuallyDeletesTheRealObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var target = await GetRealLeafMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });
            var messages = new List<string>();
            var outcomes = new List<Tempest.Desktop.ActionOutcome>();
            ribbon.ActionCompleted += (message, outcome) => { messages.Add(message); outcomes.Add(outcome); };

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("Deleted", StringComparison.OrdinalIgnoreCase));

            // `TD-58`: a successful delete reports Changed — the one case
            // dependent surfaces must refresh for.
            Assert.Contains(outcomes, o => o == Tempest.Desktop.ActionOutcome.Changed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RenameButton_WithARealSelection_OpensARealDocumentForEditing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var target = await GetRealMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            IWorkspaceView? opened = null;
            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, view => opened = view);

            var renameButton = FindButtonById(ribbon, registry, "mechanical.rename");
            renameButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.NotNull(opened);
            Assert.Equal(target.Id, opened!.ObjectId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task AnUninvocableCommand_ReportsItsOwnDeclaredReason_NeverAGenericFallback()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var messages = new List<string>();
            var outcomes = new List<Tempest.Desktop.ActionOutcome>();
            ribbon.ActionCompleted += (message, outcome) => { messages.Add(message); outcomes.Add(outcome); };

            // TD-77 Stage 5 replaced one catch-all sentence with each
            // command's own reason. A Move declares that it needs a
            // destination chosen from the object tree, and says so by name.
            FindButtonById(ribbon, registry, "mechanical.move")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("Moving a Mechanical object", StringComparison.Ordinal));
            Assert.Contains(messages, m => m.Contains("object picker", StringComparison.OrdinalIgnoreCase));

            // A Create needs values, and this view was constructed with no
            // prompt wired - so it says that, rather than running without
            // asking or silently doing nothing.
            messages.Clear();
            FindButtonById(ribbon, registry, "mechanical.create")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("needs additional input", StringComparison.Ordinal));

            // `TD-58`: every refusal is Failed, with no workspace change
            // and no dependent rebuild.
            Assert.All(outcomes, o => Assert.Equal(Tempest.Desktop.ActionOutcome.Failed, o));

            // And the generic sentence is gone for good.
            Assert.DoesNotContain(messages, m => m.Contains("isn't available yet", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RefreshEnablement_ReflectsTheRealCurrentSelectionsCapabilities()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            Assert.False(deleteButton.IsEnabled); // no selection yet

            var target = await GetRealMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);
            ribbon.RefreshEnablement();

            Assert.True(deleteButton.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RecentlyUsedSection_AppearsAfterARealDispatch()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var target = await GetRealMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });

            Assert.DoesNotContain("Recently Used", CollectAllText(ribbon));

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains("Recently Used", CollectAllText(ribbon));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Finds a real, childless Mechanical object node — a delete against a node with children is refused by the discipline's own handler, so delete-success tests must target a leaf.</summary>
    private static async Task<ProjectExplorerNode> GetRealLeafMechanicalObjectNodeAsync(IWorkspace workspace)
    {
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var node = await FindFirstLeafObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(node);
        return node!;
    }

    private static async Task<ProjectExplorerNode?> FindFirstLeafObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && !node.HasChildren)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstLeafObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static async Task<ProjectExplorerNode> GetRealMechanicalObjectNodeAsync(IWorkspace workspace)
    {
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var node = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(node);
        return node!;
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

    private static int CountTabs(RibbonView ribbon) =>
        ((TabControl)ribbon.Content!).Items.Count;

    private static Button FindButtonById(RibbonView ribbon, ICommandRegistry registry, string commandId)
    {
        var descriptor = registry.Items.Single(d => d.Id == commandId);
        var tabs = (TabControl)ribbon.Content!;
        var tab = tabs.Items.OfType<TabItem>().Single(t => Equals(t.Tag, descriptor.Category));

        return FindButtonsWithText((Control)tab.Content!, descriptor.DisplayName).First();
    }

    private static IEnumerable<Button> FindButtonsWithText(Control root, string text)
    {
        if (root is Button button && ContainsText(button.Content, text))
            yield return button;

        foreach (var child in GetChildren(root))
        {
            foreach (var found in FindButtonsWithText(child, text))
                yield return found;
        }
    }

    private static bool ContainsText(object? content, string text) =>
        content switch
        {
            string s => s == text,
            Control c => CollectAllText(c).Contains(text, StringComparison.Ordinal),
            _ => false,
        };

    /// <summary>
    /// Every child a real Avalonia control might hold, across the three
    /// distinct shapes this test file's own control tree actually uses —
    /// <see cref="Avalonia.Controls.Decorator.Child"/> (<see cref="Border"/>,
    /// used by <c>RibbonView.BuildSectionWithLabel</c>),
    /// <see cref="ContentControl.Content"/> (<see cref="Button"/>/
    /// <see cref="ScrollViewer"/>), and <see cref="Panel.Children"/>
    /// (<see cref="StackPanel"/>/<see cref="WrapPanel"/>) — found the hard
    /// way: a first version of this helper checked only the latter two,
    /// silently never descending into any <see cref="Border"/> (`Decorator.Child`
    /// is not `ContentControl.Content`), so every button nested inside one
    /// of <c>RibbonView</c>'s own bordered groups was silently unreachable.
    /// </summary>
    private static IEnumerable<Control> GetChildren(Control control)
    {
        if (control is Avalonia.Controls.Decorator { Child: Control decorated })
            yield return decorated;

        if (control is ContentControl { Content: Control content })
            yield return content;

        if (control is Panel panel)
        {
            foreach (var child in panel.Children.OfType<Control>())
                yield return child;
        }
    }

    private static string CollectAllText(Control root)
    {
        var parts = new List<string>();
        Walk(root);
        return string.Join(" | ", parts);

        void Walk(Control control)
        {
            if (control is TextBlock { Text: { } text })
                parts.Add(text);

            foreach (var child in GetChildren(control))
                Walk(child);

            if (control is ItemsControl { Items: var items })
            {
                foreach (var item in items)
                {
                    if (item is Control itemControl)
                        Walk(itemControl);
                }
            }
        }
    }

    // ----------------------------------------------------------------
    // `TD-58` — redundant rebuilds: a command click must not tear down
    // and rebuild the whole ribbon, must recompute enablement exactly
    // once, and must still update the Recently Used row.
    // ----------------------------------------------------------------

    [AvaloniaFact]
    public async Task CommandClicks_DoNotRebuildTabs_AvoidSpuriousEnablementPasses_AndUpdateRecentRowInPlace()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var target = await GetRealLeafMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var manager = new CountingWorkspaceManager(host.Manager!);
            var ribbon = new RibbonView(registry, manager, workspace, _ => { }, _ => { });
            var tabsBefore = ((TabControl)ribbon.Content!).Items.OfType<TabItem>().ToList();

            // Rename/Edit opens a document and records a recent command —
            // it does not change enablement inputs, so it must not
            // recompute enablement at all. The old RecordRecent→Rebuild()
            // path ran a full spurious pass here on every click (`TD-58`).
            manager.CanDeleteCalls = 0;
            var renameButton = FindButtonById(ribbon, registry, "mechanical.rename");
            renameButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Equal(0, manager.CanDeleteCalls);

            // Delete really deletes, reports Changed, clears the dead
            // selection, and refreshes enablement against the now-cleared
            // selection.
            //
            // TD-77 Stage 5: CanDelete is no longer consulted at all. Both
            // the click guard and enablement now ask
            // ICommandRegistry.Evaluate, which is the single availability
            // implementation for every command rather than a per-verb
            // manager query the Ribbon re-derived. Dispatch still goes
            // through DeleteObjectAsync, which is what clears the selection
            // (`TD-58`) - that is the assertion that must not move.
            manager.CanDeleteCalls = 0;
            manager.DeleteCalls = 0;
            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Equal(1, manager.DeleteCalls);
            Assert.Equal(0, manager.CanDeleteCalls);
            Assert.Null(workspace.Selection.Current); // `TD-58` stale-selection closure
            Assert.False(deleteButton.IsEnabled);     // enablement recomputed against the cleared selection

            // The tabs are the same live instances throughout — nothing
            // was torn down by either click.
            var tabsAfter = ((TabControl)ribbon.Content!).Items.OfType<TabItem>().ToList();
            Assert.Equal(tabsBefore.Count, tabsAfter.Count);
            for (var i = 0; i < tabsBefore.Count; i++)
                Assert.Same(tabsBefore[i], tabsAfter[i]);

            // And the Recently Used row still appeared, updated in place.
            var category = registry.Items.Single(d => d.Id == "mechanical.delete").Category;
            var commandTab = tabsAfter.Single(t => Equals(t.Tag, category));
            Assert.Contains("Recently Used", CollectAllText((Control)commandTab.Content!));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>A delegating <see cref="IWorkspaceManager"/> that counts the calls `TD-58`'s refresh-count assertions measure — every operation forwards to the real manager.</summary>
    private sealed class CountingWorkspaceManager(IWorkspaceManager inner) : IWorkspaceManager
    {
        public int CanDeleteCalls;
        public int DeleteCalls;

        public IWorkspace? Current => inner.Current;

        public Task<IWorkspace> StartAsync(CancellationToken cancellationToken = default) => inner.StartAsync(cancellationToken);
        public Task ShutdownAsync(CancellationToken cancellationToken = default) => inner.ShutdownAsync(cancellationToken);
        public void RegisterView(string kind, IWorkspaceViewFactory factory) => inner.RegisterView(kind, factory);
        public void RegisterExplorerArea(string kind, IProjectExplorerNodeProvider provider) => inner.RegisterExplorerArea(kind, provider);
        public void RegisterFacetProvider(string kind, IPropertyFacetProvider provider) => inner.RegisterFacetProvider(kind, provider);
        public void RegisterRenameFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory) => inner.RegisterRenameFactory(kind, factory);
        public void RegisterDeleteFactory(string kind, Func<Guid, string, IWorkspaceCommand> factory) => inner.RegisterDeleteFactory(kind, factory);
        public void RegisterReviseFactory(string kind, Func<Guid, string, string, IWorkspaceCommand> factory) => inner.RegisterReviseFactory(kind, factory);
        public bool CanRename(string kind) => inner.CanRename(kind);

        public bool CanDelete(string kind)
        {
            CanDeleteCalls++;
            return inner.CanDelete(kind);
        }

        public bool CanRevise(string kind) => inner.CanRevise(kind);
        public Task<CommandResult> RenameObjectAsync(Guid id, string kind, string newDisplayName, CancellationToken cancellationToken = default) => inner.RenameObjectAsync(id, kind, newDisplayName, cancellationToken);

        public Task<CommandResult> DeleteObjectAsync(Guid id, string kind, CancellationToken cancellationToken = default)
        {
            DeleteCalls++;
            return inner.DeleteObjectAsync(id, kind, cancellationToken);
        }

        public Task<CommandResult> ReviseObjectAsync(Guid id, string kind, string newContent, CancellationToken cancellationToken = default) => inner.ReviseObjectAsync(id, kind, newContent, cancellationToken);
    }
}
