using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Viewing;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Review board findings #2, #3 and #4 (`WP 16.5A-R1`) — a `Button` whose
/// own `Content` is a `StackPanel` (icon + `TextBlock`) has no
/// <see cref="System.Object.ToString"/> override, so Avalonia's own
/// <c>ContentControlAutomationPeer.GetNameCore()</c> fallback
/// (<c>Owner.Content?.ToString()</c>) resolves to the literal type name —
/// "Avalonia.Controls.StackPanel" — once nothing else names the control.
/// Each test below resolves the button's own real
/// <see cref="AutomationPeer"/> (via <see cref="ControlAutomationPeer.CreatePeerForElement(Control)"/>)
/// and calls <see cref="AutomationPeer.GetName"/> on it — the same call a
/// real screen reader makes — rather than merely checking that
/// <see cref="AutomationProperties.SetName(Control, string)"/> was called
/// with the right argument.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class AutomationNameResolutionTests
{
    /// <summary>Review board finding #2 — every ribbon command button.</summary>
    [AvaloniaFact]
    public async Task RibbonCommandButton_ResolvedAccessibleName_IsTheCommandsDisplayName_NotTheStackPanelTypeName()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            var descriptor = registry.Items.Single(d => d.Id == "mechanical.rename");
            var tabs = (TabControl)ribbon.Content!;
            var tab = tabs.Items.OfType<TabItem>().Single(t => Equals(t.Tag, descriptor.Category));
            var button = FindButtonsWithText((Control)tab.Content!, descriptor.DisplayName).First();

            var window = new Window { Content = ribbon };
            window.Show();

            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            var resolvedName = peer.GetName();

            Assert.Equal(descriptor.DisplayName, resolvedName);
            Assert.DoesNotContain("StackPanel", resolvedName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Review board finding #3 — the Digital Thread graph's Reset View button.</summary>
    [AvaloniaFact]
    public async Task DigitalThreadGraph_ResetViewButton_ResolvedAccessibleName_IsResetView_NotTheStackPanelTypeName()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var (domainContext, target) = await GetRealMechanicalObjectAsync(host);
            var view = DigitalThreadGraphView.TryCreate(target.Id, target.Kind!, domainContext, (_, _) => { })!;

            var window = new Window { Content = view };
            window.Show();

            var button = view.GetLogicalDescendants().OfType<Button>()
                .Single(b => ContainsText(b.Content, "Reset View"));

            var peer = ControlAutomationPeer.CreatePeerForElement(button);
            var resolvedName = peer.GetName();

            Assert.Equal("Reset View", resolvedName);
            Assert.DoesNotContain("StackPanel", resolvedName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Review board finding #4 — every glyph-only <c>DocumentViewerView</c> toolbar button.</summary>
    [AvaloniaTheory]
    [InlineData("‹", "Previous page")]
    [InlineData("›", "Next page")]
    [InlineData("−", "Zoom out")]
    [InlineData("+", "Zoom in")]
    public void DocumentViewerToolbarButton_ResolvedAccessibleName_IsTheRealAction_NotTheBareGlyph(string glyph, string expectedName)
    {
        var viewer = new DocumentViewerView();
        var window = new Window { Content = viewer };
        window.Show();

        var button = viewer.GetLogicalDescendants().OfType<Button>()
            .Single(b => Equals(ToolTip.GetTip(b), expectedName) && Equals(b.Content, glyph));

        var peer = ControlAutomationPeer.CreatePeerForElement(button);
        var resolvedName = peer.GetName();

        Assert.Equal(expectedName, resolvedName);
        Assert.NotEqual(glyph, resolvedName);
    }

    // ------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------

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
                var children = await explorer.GetChildrenAsync(node.Id);
                var found = await FindFirstObjectNodeAsync(explorer, children);
                if (found is not null)
                    return found;
            }
        }

        return null;
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

    private static IEnumerable<Control> GetChildren(Control control) => control switch
    {
        ContentControl { Content: Control single } => new[] { single },
        Panel panel => panel.Children,
        Decorator { Child: Control child } => new[] { child },
        _ => Array.Empty<Control>(),
    };

    private static bool ContainsText(object? content, string text) => content switch
    {
        string s => s.Contains(text, StringComparison.Ordinal),
        TextBlock t => (t.Text ?? string.Empty).Contains(text, StringComparison.Ordinal),
        StackPanel panel => panel.Children.Any(c => ContainsText(c, text)),
        ContentControl cc => ContainsText(cc.Content, text),
        _ => false,
    };
}
