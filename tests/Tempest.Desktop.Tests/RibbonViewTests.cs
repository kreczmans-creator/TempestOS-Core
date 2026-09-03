using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Theming;
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

    /// <summary>
    /// Button hierarchy: the ribbon's own Create group is the one
    /// commit-shaped action per discipline tab (a large tile, `BuildGroup`)
    /// — before this fix it was styled identically (`ChromeStyles.Flat`) to
    /// every Organize/Lifecycle/Actions button, distinguished only by size.
    /// It must now carry the same accent-filled primary treatment every
    /// other "+ New"/commit action in the shell already uses
    /// (`ProjectRisksView`/`ProjectTasksView`/`ObjectEditorView.Save`, ...),
    /// while a secondary command (Rename, in Organize) stays flat.
    /// </summary>
    [AvaloniaFact]
    public async Task CreateGroupButtons_AreStyledPrimary_OtherGroupsStayFlat()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var createButton = FindButtonById(ribbon, registry, "mechanical.create");
            Assert.Contains(ChromeStyles.Primary, createButton.Classes);
            Assert.DoesNotContain(ChromeStyles.Flat, createButton.Classes);

            var renameButton = FindButtonById(ribbon, registry, "mechanical.rename");
            Assert.Contains(ChromeStyles.Flat, renameButton.Classes);
            Assert.DoesNotContain(ChromeStyles.Primary, renameButton.Classes);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Responsive Ribbon closure (`WP-Z4`) — at a width narrow enough that
    /// a discipline's command groups genuinely overflow the visible tab
    /// area, the horizontal <see cref="ScrollBar"/> that lets the user
    /// reach the hidden groups must actually render, not merely exist as
    /// a logically-correct but visually invisible
    /// <see cref="ScrollViewer.Extent"/>/<see cref="ScrollViewer.Viewport"/>
    /// pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>What this test would have missed before the fix, and why it does
    /// not now.</b> Before this fix, `ScrollViewer.Extent.Width` already
    /// exceeded `Viewport.Width` whenever a tab's own command groups
    /// overflowed a narrow window — the underlying scroll mechanism was
    /// never broken. What was missing was the one thing a user actually
    /// sees: nothing upstream of the ScrollViewer (a vertical
    /// <see cref="StackPanel"/>, itself sized to its own content) ever
    /// gave it more height than the button rows alone need, so `Auto`
    /// horizontal scrollbar visibility had no room to draw the scrollbar
    /// into and rendered nothing — real command groups past that width
    /// were clipped in total silence, with no affordance telling the user
    /// scrolling was even possible. A test on `Extent`/`Viewport` alone
    /// would have passed against that broken build; this one specifically
    /// finds the real `ScrollBar` control in the rendered visual tree and
    /// asserts it is actually visible.
    /// </para>
    /// <para>
    /// Real layout only happens once a control is part of a shown
    /// <see cref="Window"/>'s visual tree (the same discipline this
    /// project's own <c>DocumentAreaTabSelectionTests</c>/
    /// <c>DialogFrameworkKeyboardTests</c> already establish) — a bare
    /// <c>Measure</c>/<c>Arrange</c> call without a real
    /// <see cref="Window"/> host does not reliably reproduce the same
    /// scrollbar-visibility decision the real running application makes.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public async Task NarrowWindow_RibbonHorizontalScrollbar_ActuallyRendersWhenCommandGroupsOverflow()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            // The real Mechanical tab's own command set is wide enough,
            // at a real ~1000px-class shell width, to genuinely overflow —
            // the exact width class this closure's own diagnosis named.
            var window = new Window { Content = ribbon, Width = 1000, Height = 260 };
            window.Show();
            // Measuring/arranging the ribbon directly, not the Window —
            // the headless test platform does not reliably propagate a
            // Window's own Width/Height into a real layout pass (the
            // identical, already-documented reason
            // WorkspaceLayoutHostTests measures/arranges its own control
            // under test directly rather than driving Window.Width).
            ribbon.Measure(new Size(1000, 260));
            ribbon.Arrange(new Rect(0, 0, 1000, 260));
            // A second pass: the fix's own scrollbar-height reservation is
            // applied on the ScrollViewer's first LayoutUpdated, which
            // itself invalidates layout once more to take effect.
            ribbon.Measure(new Size(1000, 260));
            ribbon.Arrange(new Rect(0, 0, 1000, 260));

            var scroller = ribbon.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(scroller.Extent.Width > scroller.Viewport.Width, "This test's own premise requires the real command set to genuinely overflow at this width.");

            var horizontalScrollBar = ribbon.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
                .Single(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            Assert.True(horizontalScrollBar.IsEffectivelyVisible, "The horizontal scrollbar must actually render once the ribbon's command groups overflow — not merely exist as an invisible, logically-correct ScrollViewer.");
            Assert.True(horizontalScrollBar.Bounds.Height > 0, "The scrollbar must be given real, non-zero screen space, not collapsed to nothing by a starved parent.");

            window.Close();
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// The same closure's own explicit "preserve normal-width layout"
    /// requirement — at a width wide enough that no discipline's command
    /// groups ever overflow, the scrollbar-height reservation this fix
    /// adds must not force a visible horizontal scrollbar to appear where
    /// none is needed.
    /// </summary>
    [AvaloniaFact]
    public async Task WideWindow_RibbonHorizontalScrollbar_StaysHiddenWhenNothingOverflows()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var window = new Window { Content = ribbon, Width = 4000, Height = 260 };
            window.Show();
            ribbon.Measure(new Size(4000, 260));
            ribbon.Arrange(new Rect(0, 0, 4000, 260));
            ribbon.Measure(new Size(4000, 260));
            ribbon.Arrange(new Rect(0, 0, 4000, 260));

            var scroller = ribbon.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Assert.True(scroller.Extent.Width <= scroller.Viewport.Width, "This test's own premise requires the real command set to fit without overflowing at this width.");

            var horizontalScrollBar = ribbon.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.ScrollBar>()
                .Single(b => b.Orientation == Avalonia.Layout.Orientation.Horizontal);
            Assert.False(horizontalScrollBar.IsEffectivelyVisible, "Reserving room for the scrollbar must not force it to show up when nothing overflows.");

            window.Close();
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

            // `TD-119`/Class B: no wait. An unavailable command is refused
            // synchronously — `RibbonView.OnCommandButtonClickAsync` evaluates
            // availability and raises `ActionCompleted` before its first
            // `await`, so the message is already recorded when `RaiseEvent`
            // returns.
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

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var deleteDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(messages.Count > 0) && DateTime.UtcNow < deleteDeadline)
                await Task.Delay(10);

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

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var renameDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(opened is not null) && DateTime.UtcNow < renameDeadline)
                await Task.Delay(10);

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

            // `TD-119`/Class B: no wait. An unavailable command is refused
            // synchronously — `RibbonView.OnCommandButtonClickAsync` evaluates
            // availability and raises `ActionCompleted` before its first
            // `await`, so the message is already recorded when `RaiseEvent`
            // returns.
            Assert.Contains(messages, m => m.Contains("Moving a Mechanical object", StringComparison.Ordinal));
            Assert.Contains(messages, m => m.Contains("object picker", StringComparison.OrdinalIgnoreCase));

            // A Create needs values, and this view was constructed with no
            // prompt wired - so it says that, rather than running without
            // asking or silently doing nothing.
            messages.Clear();
            FindButtonById(ribbon, registry, "mechanical.create")
                .RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`/Class B: no wait. An unavailable command is refused
            // synchronously — `RibbonView.OnCommandButtonClickAsync` evaluates
            // availability and raises `ActionCompleted` before its first
            // `await`, so the message is already recorded when `RaiseEvent`
            // returns.
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

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var recentDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(CollectAllText(ribbon).Contains("Recently Used", StringComparison.Ordinal)) && DateTime.UtcNow < recentDeadline)
                await Task.Delay(10);

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

            // `TD-119`: both assertions below are negative or count-based, so
            // there is nothing to poll for in them. `ActionCompleted` is the
            // real positive signal that a click finished — every RibbonView
            // dispatch path raises it — so the test observes that, then asserts
            // exactly what it always did.
            var completions = new List<string>();
            ribbon.ActionCompleted += (message, _) => completions.Add(message);

            // Rename/Edit opens a document and records a recent command —
            // it does not change enablement inputs, so it must not
            // recompute enablement at all. The old RecordRecent→Rebuild()
            // path ran a full spurious pass here on every click (`TD-58`).
            manager.CanDeleteCalls = 0;
            var renameButton = FindButtonById(ribbon, registry, "mechanical.rename");
            completions.Clear();
            renameButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var renameClickDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(completions.Count > 0) && DateTime.UtcNow < renameClickDeadline)
                await Task.Delay(10);

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
            completions.Clear();
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var deleteClickDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(completions.Count > 0 && manager.DeleteCalls == 1) && DateTime.UtcNow < deleteClickDeadline)
                await Task.Delay(10);

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
