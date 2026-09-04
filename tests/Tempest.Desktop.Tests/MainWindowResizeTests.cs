using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.App.Workspace.Layout;
using Tempest.Desktop.Composition;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-83` closure (Technical Debt Register.md): a real window resize
/// driven through <see cref="MainWindow"/> itself.
/// </summary>
/// <remarks>
/// <para>
/// <c>WorkspaceLayoutHostTests.ShrinkingTheWindow_AppliesTheResponsiveRule_...</c>
/// already proves the responsive rule fires from a real
/// <see cref="WorkspaceLayoutHost.SizeChanged"/> subscription, and
/// <c>ResponsiveWorkspaceTests</c> proves the ribbon's own minimise state
/// persists — but neither drives a real <see cref="MainWindow"/> resize:
/// both build the panel under test directly. `TD-83` named exactly that
/// gap ("no test drives a real window resize through <c>MainWindow</c>").
/// This class closes it: a real <see cref="WorkspaceHost"/>, a real
/// <see cref="MainWindow"/>, shown under the headless platform, resized by
/// setting <see cref="Window.Width"/>/<see cref="Window.Height"/> and
/// running a real layout pass — never by calling
/// <see cref="WorkspaceLayoutHost.ApplyResponsiveLayout"/> or
/// <see cref="MainWindow"/>'s own <c>SizeChanged</c> handler directly.
/// </para>
/// <para>
/// Two independent resize-driven behaviours are asserted at each width,
/// both wired only through the real <c>SizeChanged</c> chain: the shell's
/// compact threshold (<see cref="Theming.DesignTokens.CompactShellWidth"/>,
/// <see cref="GlobalNavigationRail.IsCompact"/>) and the docking layout's
/// `TD-70` working-pane floor (<see cref="WorkspaceLayoutHost.MinPrimaryPaneWidth"/>).
/// Either one would still read correctly from a window that was simply
/// constructed at the target size and never actually resized — the
/// discriminating check is that the <em>same window</em> reports different
/// facts at 1920, 1280 and 900 as it is resized in place, and that the
/// ribbon's own minimise state (set once, before any resize) is left alone
/// by all three — proving the resize path is real and does not leak into
/// an unrelated axis of the same shell.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class MainWindowResizeTests
{
    [AvaloniaFact]
    public async Task ResizingTheRealWindow_AppliesTheShellCompactThreshold_AndKeepsTheDockingFloor()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host) { Width = 1920, Height = 1080 };
            window.Show();

            // The ribbon's own minimise state is a separate, persisted axis
            // (`TD-70`, View menu/settings-controlled) — set once here, then
            // asserted unchanged after every resize below, so a resize that
            // accidentally coupled into it would be caught.
            var ribbon = window.GetLogicalDescendants().OfType<RibbonView>().Single();
            ribbon.SetCollapsed(true);

            await WaitForInitialLayoutAsync(window, 1920, 1080);

            AssertWidth(window, expectedWidth: 1920, expectCompactShell: false);
            Assert.True(ribbon.IsCollapsed);

            // 1280 sits above `CompactShellWidth` (1240) — still the wide
            // shell, and the first proof this is a real resize: the same
            // window, the same controls, a new size.
            Resize(window, 1280, 800);
            AssertWidth(window, expectedWidth: 1280, expectCompactShell: false);
            Assert.True(ribbon.IsCollapsed);

            // ~900 requested, but MainWindow declares MinWidth = 960
            // (constructor) and the real window honours it — 960 is
            // therefore the actual narrowest width this product ever lays
            // out at, and the more honest number to assert against than
            // the unreachable 900. It still sits below the compact
            // threshold. The negative check `TD-83` asks for: even here,
            // the document pane must not be squeezed below its `TD-70`
            // floor — the responsive rule (proven wired, not merely
            // present, by `WorkspaceLayoutHostTests`) is reached through
            // this real resize too.
            Resize(window, 900, 700);
            AssertWidth(window, expectedWidth: 960, expectCompactShell: true);
            Assert.True(ribbon.IsCollapsed);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Asserts the two resize-driven facts at the window's current bounds:
    /// the shell compact threshold (navigation rail + header) and the
    /// docking layout's document-pane width, computed from the real
    /// arranged bounds of <see cref="WorkspaceLayoutHost"/> — never from
    /// the window's own nominal <see cref="Window.Width"/>, so a layout
    /// pass that silently failed to reach the host would show up as a
    /// wrong width here rather than passing on the requested value alone.
    /// </summary>
    private static void AssertWidth(MainWindow window, double expectedWidth, bool expectCompactShell)
    {
        Assert.Equal(expectedWidth, window.Bounds.Width, precision: 3);

        var navigationRail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
        Assert.Equal(expectCompactShell, navigationRail.IsCompact);

        var layoutHost = window.WorkspaceLayout.Host;
        Assert.True(layoutHost.Bounds.Width > 0, "The docking layout host never received a real, non-zero arranged width.");

        // `WorkspaceDockingComposer.DocumentAreaPanelId` — internal, reached
        // via `InternalsVisibleTo("Tempest.Desktop.Tests")` exactly like
        // `ProjectExplorerView`/`PropertyInspectorView`'s own test hooks.
        var root = Assert.IsType<LayoutSplitNode>(window.WorkspaceLayout.Tree.Root);
        var children = root.Children.ToList();
        var documentIndex = children.FindIndex(c => c.Panels.Contains(WorkspaceDockingComposer.DocumentAreaPanelId));
        Assert.True(documentIndex >= 0, "The document panel is not part of the docked arrangement.");

        // None of the three tested widths puts the *available* width (after
        // the navigation rail) below the ~700px at which the responsive
        // rule in `WorkspaceLayoutHost.ApplyResponsiveLayout` would start
        // collapsing a side panel — so the document pane's real share stays
        // the un-collapsed proportional one, and the floor check below is a
        // genuine "never even gets close" guarantee at every width this
        // product ships at, not a coincidence of one one number.
        var documentWidth = root.Weights[documentIndex] * layoutHost.Bounds.Width;
        Assert.True(
            documentWidth >= WorkspaceLayoutHost.MinPrimaryPaneWidth,
            $"At window width {window.Bounds.Width}px the document pane had {documentWidth}px, below the TD-70 floor of {WorkspaceLayoutHost.MinPrimaryPaneWidth}px.");
    }

    /// <summary>Resizes the real window and runs a real layout pass — never <see cref="WorkspaceLayoutHost.ApplyResponsiveLayout"/> directly.</summary>
    private static void Resize(MainWindow window, double width, double height)
    {
        window.Width = width;
        window.Height = height;

        // Two passes, mirroring `ProjectTaskAcceptanceTests.LayOutAsync`:
        // draining any queued dispatcher work between them so a change
        // triggered by the first pass (the responsive rule's own guarded
        // re-entry, `WorkspaceLayoutHost._applyingResponsive`) is measured
        // by the second.
        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
        }
    }

    /// <summary>
    /// Waits for <see cref="MainWindow"/>'s own <c>Opened</c> handler
    /// (theme load, layout restore, area selection — all real, awaited
    /// async work, none of it joined by <see cref="Window.Show"/> itself)
    /// to have produced a real, non-zero docking layout before the first
    /// assertion. The bounded, condition-based poll `TD-46`/`WP 11.4A`
    /// established: re-check every iteration, stop on the condition or a
    /// two-second deadline.
    /// </summary>
    private static async Task WaitForInitialLayoutAsync(MainWindow window, double width, double height)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));

            if (window.WorkspaceLayout.Host.Bounds.Width > 0 || DateTime.UtcNow >= deadline)
                return;

            await Task.Delay(10);
        }
    }
}
