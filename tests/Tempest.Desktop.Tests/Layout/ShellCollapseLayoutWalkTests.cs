using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.Layout;

/// <summary>
/// `WP 19.10O` — the layout walk extended with the rail's and each area
/// tree's own new collapsed states, at the identical two window sizes
/// <see cref="LayoutWalkTests"/> already walks the expanded shell at. A
/// sibling walk, not an extra parameter threaded through
/// <see cref="LayoutWalkTests.EveryRailEntryAndProjectTab_LaysOutCleanly_AtBothWindowSizes"/>
/// itself: that method's own real-content setup, rail/tree-node
/// enumeration and PNG bookkeeping already cover the expanded shell
/// exhaustively, and doubling every one of its captures across two more
/// axes (rail collapsed × column collapsed) would roughly quadruple an
/// already multi-minute test for coverage this class gets far more cheaply
/// by walking only the states this Work Package actually adds.
/// </summary>
[Trait("Category", "LayoutWalk")]
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ShellCollapseLayoutWalkTests
{
    private static readonly (string Name, double Width, double Height)[] Sizes =
    [
        ("1600x900", 1600, 900),
        ("1180x760", 1180, 760), // below DesignTokens.CompactShellWidth (1200).
    ];

    [AvaloniaFact]
    public async Task RailCollapsed_AtBothWindowSizes_LaysOutCleanly_AndTheModuleHostFillsTheFreedWidth()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var findings = new List<string>();
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();

            var rail = window.GetLogicalDescendants().OfType<GlobalNavigationRail>().Single();
            var moduleHost = GetPrivateField<ContentControl>(window, "_moduleHost");

            foreach (var size in Sizes)
            {
                // Expanded first, at this size, so the collapsed comparison
                // below is against this exact size's own real arranged
                // width — never a literal constant that could drift from
                // whatever DockPanel/ScrollViewer chrome actually costs.
                rail.SetCollapsed(false);
                LayOut(window, size.Width, size.Height);
                var expandedModuleHostWidth = moduleHost.Bounds.Width;
                var railWidthBefore = rail.Bounds.Width;

                rail.SetCollapsed(true);
                LayOut(window, size.Width, size.Height);

                findings.AddRange(CollectLayoutFindings(window, $"{size.Name} · rail collapsed"));

                // Automation names complete: every module button and the
                // chevron itself still carry a name once folded.
                Assert.All(
                    rail.GetLogicalDescendants().OfType<Button>(),
                    b => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)), $"{size.Name}: an unnamed rail button after collapsing."));

                var railWidthAfter = rail.Bounds.Width;
                var moduleHostWidthAfter = moduleHost.Bounds.Width;
                Assert.Equal(
                    expandedModuleHostWidth + (railWidthBefore - railWidthAfter),
                    moduleHostWidthAfter,
                    precision: 1);
            }

            Assert.True(
                findings.Count == 0,
                $"Layout walk (rail collapsed) found {findings.Count} finding(s):{Environment.NewLine}{string.Join(Environment.NewLine, findings)}");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EachAreaTreeCollapsed_AtBothWindowSizes_LaysOutCleanly_AndTheRightPaneFillsTheFreedWidth()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var findings = new List<string>();
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            foreach (var size in Sizes)
            {
                foreach (var area in new[] { ShellArea.Projects, ShellArea.EngineeringDepartment, ShellArea.Business })
                {
                    await navigator.GoToModuleAsync(area);
                    await window.RenderCurrentModuleAsync();

                    Control areaView = area switch
                    {
                        ShellArea.Projects => window.GetLogicalDescendants().OfType<ProjectsAreaView>().Single(),
                        ShellArea.EngineeringDepartment => window.GetLogicalDescendants().OfType<EngineeringAreaView>().Single(),
                        _ => window.GetLogicalDescendants().OfType<BusinessAreaView>().Single(),
                    };

                    SetTreeCollapsed(areaView, false);
                    LayOut(window, size.Width, size.Height);
                    var column = GetPrivateField<CollapsibleColumn>(areaView, "_treeColumn");
                    var detail = GetPrivateField<ContentControl>(areaView, "_detail");
                    var expandedColumnWidth = column.Bounds.Width;
                    var expandedDetailWidth = detail.Bounds.Width;

                    SetTreeCollapsed(areaView, true);
                    LayOut(window, size.Width, size.Height);

                    var label = $"{size.Name} · {area} tree collapsed";
                    findings.AddRange(CollectLayoutFindings(window, label));

                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(GetChevron(column))), $"{label}: the collapse chevron carries no automation name.");

                    Assert.Equal(
                        expandedDetailWidth + (expandedColumnWidth - column.Bounds.Width),
                        detail.Bounds.Width,
                        precision: 1);

                    // Leave it expanded for the next area/size iteration.
                    SetTreeCollapsed(areaView, false);
                    LayOut(window, size.Width, size.Height);
                }
            }

            Assert.True(
                findings.Count == 0,
                $"Layout walk (tree collapsed) found {findings.Count} finding(s):{Environment.NewLine}{string.Join(Environment.NewLine, findings)}");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void SetTreeCollapsed(Control areaView, bool collapsed)
    {
        var method = areaView.GetType().GetMethod("SetTreeCollapsed") ?? throw new InvalidOperationException($"{areaView.GetType().Name} has no SetTreeCollapsed method.");
        method.Invoke(areaView, [collapsed]);
    }

    private static Button GetChevron(CollapsibleColumn column) => GetPrivateField<Button>(column, "_chevron");

    /// <summary>Resizes the real window and runs a real layout pass, mirroring <see cref="LayoutWalkTests"/>'s own identical helper.</summary>
    private static void LayOut(Window window, double width, double height)
    {
        if (!window.IsVisible)
            window.Show();

        window.Width = width;
        window.Height = height;

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(width, height));
            window.Arrange(new Rect(0, 0, width, height));
        }
    }
}
