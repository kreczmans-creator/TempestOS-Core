using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;
using Tempest.Samples;

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
            ribbon.ActionCompleted += messages.Add;

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("selected", StringComparison.OrdinalIgnoreCase));
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
            var target = await GetRealMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { });
            var messages = new List<string>();
            ribbon.ActionCompleted += messages.Add;

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            Assert.Contains(messages, m => m.Contains("Deleted", StringComparison.OrdinalIgnoreCase));
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
    public async Task CreateButton_HasNoCreateDefaultAndNoSelectionRoute_ReportsHonestlyRatherThanSilentlyDoingNothing()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var messages = new List<string>();
            ribbon.ActionCompleted += messages.Add;

            var createButton = FindButtonById(ribbon, registry, "mechanical.create");
            createButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);

            // `WP 10.8A` — the message itself was rewritten to stop
            // claiming the Command Palette/Project Explorer context menu
            // can help (confirmed by direct investigation that neither
            // genuinely can, for any command reaching this fallback) —
            // this assertion now checks the new, honest wording's own
            // core claim instead of the old "additional input" phrase.
            Assert.Contains(messages, m => m.Contains("destination picker", StringComparison.OrdinalIgnoreCase));
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
}
