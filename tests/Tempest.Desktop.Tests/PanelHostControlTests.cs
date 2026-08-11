using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tempest.App.Workspace;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates Collapse and Auto-Hide (`WP 10.2B`'s own "Demonstrate"
/// list) directly against <see cref="PanelHostControl"/> — no Workspace
/// instance required, since both are pure Desktop-local presentation
/// state, over a minimal, real <see cref="IWorkspacePanel"/> test double
/// (the identical pattern <see cref="WorkspaceModernisationTests"/>'s own
/// <c>TestWorkspaceView</c> already establishes for <c>IWorkspaceView</c>).
/// </summary>
public sealed class PanelHostControlTests
{
    [AvaloniaFact]
    public void Collapse_TogglesTheStripVisible_AndHidesHeaderAndContent()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Left);
        var content = new Border();
        var host = new PanelHostControl(panel, content);

        Assert.False(host.IsCollapsed);
        Assert.True(content.IsVisible);

        host.SetCollapsed(true);

        Assert.True(host.IsCollapsed);
        Assert.False(content.IsVisible);
        Assert.True(host.IsStripShowing);
    }

    [AvaloniaFact]
    public void SetCollapsed_RaisesCollapseToggled_WithTheNewState()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Right);
        var host = new PanelHostControl(panel, new Border());

        bool? raised = null;
        host.CollapseToggled += v => raised = v;

        host.SetCollapsed(true);

        Assert.True(raised);
    }

    [AvaloniaFact]
    public void SetPinned_False_EntersAutoHide_TheStripShowsEvenThoughNotManuallyCollapsed()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Left);
        var host = new PanelHostControl(panel, new Border());

        host.SetPinned(false);

        Assert.False(host.IsPinned);
        Assert.False(host.IsCollapsed); // never manually collapsed
        Assert.True(host.IsStripShowing); // strip shows anyway — Auto-Hide, not Collapse
    }

    [AvaloniaFact]
    public void StripButtonClick_WhilePinned_ExpandsInPlace_NeverRaisesFlyoutRequested()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Left);
        var host = new PanelHostControl(panel, new Border());
        host.SetCollapsed(true);

        var flyoutRequested = false;
        host.FlyoutRequested += () => flyoutRequested = true;

        var strip = FindCollapsedStripButton(host);
        strip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.False(host.IsCollapsed); // expanded in place
        Assert.False(flyoutRequested);
    }

    [AvaloniaFact]
    public void StripButtonClick_WhileUnpinned_RaisesFlyoutRequested_NeverExpandsInPlace()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Left);
        var host = new PanelHostControl(panel, new Border());
        host.SetPinned(false);

        var flyoutRequested = false;
        host.FlyoutRequested += () => flyoutRequested = true;

        var strip = FindCollapsedStripButton(host);
        strip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.True(flyoutRequested);
        Assert.True(host.IsStripShowing); // still showing — auto-hide, unaffected by the click itself
    }

    [AvaloniaFact]
    public void HideRequested_StillCallsThroughToTheRealPanelsOwnHideAsync_Unchanged()
    {
        var panel = new TestPanel("Test Panel", WorkspaceDockPosition.Right);
        var host = new PanelHostControl(panel, new Border());

        var hideRequested = false;
        host.HideRequested += () => hideRequested = true;

        var hideButton = FindButtonByContent(host, "✕");
        hideButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.True(hideRequested);
        Assert.False(panel.IsVisible);
    }

    // Direct Children traversal, not Avalonia's own VisualTree extensions —
    // PanelHostControl's own three top-level children are never re-parented
    // by Collapse/Auto-Hide (only re-positioned by DockingGrid.ShowFlyout),
    // so [1] (the collapsed strip Border) and [0]'s own header Grid are
    // stable, reliable lookups whether or not this control is ever attached
    // to a real Window (headless tests never attach one).
    private static Button FindCollapsedStripButton(PanelHostControl host)
    {
        var strip = (Border)host.Children[1];
        return (Button)strip.Child!;
    }

    private static Button FindButtonByContent(PanelHostControl host, string content)
    {
        var headerStack = (StackPanel)host.Children[0];
        var header = (Grid)headerStack.Children[0];
        return header.Children.OfType<Button>().Single(b => Equals(b.Content, content));
    }

    /// <summary>A minimal, real <see cref="IWorkspacePanel"/> — this test file's own fake dockable panel, mirroring every other Desktop test's own inline test-double pattern.</summary>
    private sealed class TestPanel(string title, WorkspaceDockPosition dockPosition) : IWorkspacePanel
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string Title { get; } = title;
        public WorkspaceDockPosition DockPosition { get; } = dockPosition;
        public bool IsVisible { get; private set; } = true;

        public Task ShowAsync(CancellationToken cancellationToken = default)
        {
            IsVisible = true;
            return Task.CompletedTask;
        }

        public Task HideAsync(CancellationToken cancellationToken = default)
        {
            IsVisible = false;
            return Task.CompletedTask;
        }
    }
}
