using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 21.5C` (the real-shell journey on Linux): an overlay opened *from*
/// another overlay must be drawn, and hit-tested, above the one that
/// opened it.
///
/// <para>
/// The shell's overlays are siblings in one <see cref="Grid"/>, and a
/// Grid's Z-order follows its <c>Children</c> order, so
/// <see cref="OrganisationPicker"/> — added to that Grid before
/// <see cref="NewProjectPrompt"/>, and opened *by* it through "Client →
/// Add organisation…" (`PHYSICAL_REVIEW.md` §7c D2) — used to render
/// underneath the prompt: completely obscured, and unreachable by a real
/// mouse click. Every headless test passed regardless, because a headless
/// test raises the picker's own events directly and never composites. The
/// defect was found by driving the built application on a real X11
/// display with real pointer input, and this is its regression test.
/// </para>
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class NestedOverlayZOrderTests
{
    [AvaloniaFact]
    public async Task AnOverlayOpenedFromAnotherOverlay_IsDrawnAndHitTestedAboveIt()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());

        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);

            var prompt = window.GetLogicalDescendants().OfType<NewProjectPrompt>().Single();
            var picker = window.GetLogicalDescendants().OfType<OrganisationPicker>().Single();

            // The order the shell's own Grid holds them in — the order that
            // used to decide what was on top, and the reason this defect
            // existed at all.
            var children = window.GetVisualDescendants().OfType<Grid>()
                .First(grid => grid.Children.Contains(prompt) && grid.Children.Contains(picker))
                .Children;
            Assert.True(
                children.IndexOf(picker) < children.IndexOf(prompt),
                "This test is only meaningful while the picker is still constructed before the prompt.");

            // New Project opens; then, from it, the organisation picker.
            prompt.IsVisible = true;
            LayOut(window);
            picker.IsVisible = true;
            LayOut(window);

            Assert.True(
                picker.ZIndex > prompt.ZIndex,
                $"The picker was opened from the prompt, so it must sit above it: picker ZIndex {picker.ZIndex}, prompt ZIndex {prompt.ZIndex}.");

            // And the claim a user actually cares about, stated as the thing
            // that decides it: among every overlay currently on screen, the
            // one just opened is the topmost. Z-order is what both the
            // renderer and the hit test read, so this is the same statement
            // as "a click in the middle of the shell reaches the picker",
            // without depending on a layout pass having settled.
            // The toast layer is deliberately above every overlay (it is
            // how a notification stays readable over a dialog), so it is
            // not one of the overlays this ordering is about.
            var visibleOverlays = children.OfType<Control>()
                .Where(child => child.IsVisible && child is not ToastHost)
                .ToList();
            var topmost = visibleOverlays.OrderByDescending(child => child.ZIndex).ThenByDescending(children.IndexOf).First();

            Assert.Same(picker, topmost);
        }
        finally
        {
            await host.ShutdownAsync();
        }
    }

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }
}
