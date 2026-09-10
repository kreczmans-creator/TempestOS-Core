using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The status bar's own priority collapse (`WP 19.3A-R1`) must re-run when
/// a segment's text grows after the bar was last measured — the layout walk
/// at 1180×760 found the message area squeezed 11 px short once the first
/// Explorer area's long title landed in AREA, because the bar's own
/// <c>MeasureOverride</c> never ran again (`WP 19.9.0`).
/// </summary>
public sealed class StatusBarCollapseTests
{
    private const double Width = 1180;

    [AvaloniaFact]
    public void ALongerAreaTitle_AfterTheFirstLayout_StillCollapsesTheHint_AndTheMessageAreaKeepsItsWidth()
    {
        var bar = new StatusBarView();
        bar.SetProject("P-LAYOUT Layout Walk Project");
        bar.SetLocation("Project · Requirements");
        bar.SetArea("Calculations");
        bar.SetText("Ready.");
        bar.SetHint("Ready.");
        bar.SetNotifications(0);
        GetPrivateField<TextBlock>(bar, "_hostState").Text = "Running";
        GetPrivateField<TextBlock>(bar, "_diagnostics").Text = "All modules healthy";

        var window = new Window { Content = bar, Width = Width, Height = 100 };
        window.Show();
        LayOut(window);

        var hint = GetPrivateField<TextBlock>(bar, "_hint");
        Assert.True(HintSegment(hint).IsVisible, "With a short AREA title everything fits, so nothing should be hidden.");

        // The change the walk makes after the first layout: a long title.
        bar.SetArea("Mechanical Product Structure");
        LayOut(window);

        var selected = GetPrivateField<TextBlock>(bar, "_selection");
        var selectedSegment = (StackPanel)selected.GetLogicalParent()!;
        selectedSegment.Measure(Size.Infinity);
        var natural = selectedSegment.DesiredSize.Width;
        LayOut(window);

        Assert.False(HintSegment(hint).IsVisible, "The hint is the lowest-priority segment and must give way first.");
        Assert.True(selectedSegment.Bounds.Width + 0.5 >= natural, $"The message area was squeezed to {selectedSegment.Bounds.Width:F1} px, below its own {natural:F1} px.");
        Assert.True(selected.Bounds.Right <= selectedSegment.Bounds.Width + 0.5, "The message text must lie inside its own segment.");
    }

    private static StackPanel HintSegment(TextBlock hint) => (StackPanel)hint.GetLogicalParent()!;

    private static void LayOut(Window window)
    {
        for (var pass = 0; pass < 2; pass++)
        {
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(Width, 100));
            window.Arrange(new Rect(0, 0, Width, 100));
        }
    }
}
