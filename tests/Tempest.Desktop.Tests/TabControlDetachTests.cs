using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Pins the platform fact `WP 19.7C`'s helper and the v0.19.1 warning on
/// hidden editors both rest on: a <see cref="TabControl"/> detaches the
/// content of the tab it leaves and reattaches it on return. The Document
/// Area is one, so an <c>ObjectEditorView</c> on a background tab is
/// hidden, not closed — <c>WorkspaceChangesSubscription</c> unsubscribes it
/// there and subscribes it again when its tab is reselected.
/// </summary>
public sealed class TabControlDetachTests
{
    [AvaloniaFact]
    public void LeavingATab_DetachesItsContent_AndReturningReattachesIt()
    {
        var first = new Border();
        var attached = 0;
        var detached = 0;
        first.AttachedToVisualTree += (_, _) => attached++;
        first.DetachedFromVisualTree += (_, _) => detached++;

        var tabs = new TabControl();
        var tabA = new TabItem { Header = "A", Content = first };
        var tabB = new TabItem { Header = "B", Content = new Border() };
        tabs.Items.Add(tabA);
        tabs.Items.Add(tabB);

        var window = new Window { Content = tabs };
        window.Show();
        Pump(window);
        Assert.Equal(1, attached);
        Assert.Equal(0, detached);

        tabs.SelectedItem = tabB;
        Pump(window);
        Assert.Equal(1, detached);

        tabs.SelectedItem = tabA;
        Pump(window);
        Assert.Equal(2, attached);
        Assert.Equal(1, detached);
    }

    private static void Pump(Window window)
    {
        for (var pass = 0; pass < 3; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(800, 600));
            window.Arrange(new Rect(0, 0, 800, 600));
        }
    }
}
