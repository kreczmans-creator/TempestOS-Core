using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Desktop.Views;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.4A` acceptance: opening a project's Structure tab must never
/// again show the engineering surface (ribbon + docking) as a layer over
/// the project workspace's own header — the exact defect
/// <c>po-comments.md</c> #1 reported and <c>seam-map-shell.md</c> §3
/// explains (<see cref="ProjectWorkspaceView"/>'s old negative-margin
/// <c>_structureHost</c>). Driven through the real window exactly as the
/// application reaches this surface: open a project, select the Structure
/// tab through the real navigator, render — never a view's own
/// <c>RefreshAsync</c> called by hand.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class StructureTabContainmentTests
{
    private static readonly (string Name, double Width, double Height)[] Sizes =
    [
        ("1600x900", 1600, 900),
        ("1180x760", 1180, 760),
    ];

    /// <summary>
    /// After opening a project's Structure tab, every visual of the
    /// engineering surface lies within the tab content presenter's
    /// bounds, and the tab strip's bounds intersect none of them — at
    /// both window sizes the layout walk covers.
    /// </summary>
    [AvaloniaFact]
    public async Task OpeningTheStructureTab_ContainsTheEngineeringSurface_AtBothWindowSizes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-STRUCT", "Structure Containment Project");

            foreach (var size in Sizes)
            {
                LayOut(window, size.Width, size.Height);

                await navigator.OpenProjectAsync(project.Id, ProjectArea.Engineering);
                await window.RenderCurrentModuleAsync();
                LayOut(window, size.Width, size.Height);

                var area = $"{size.Name} · Structure tab";

                // The general layout walk (`DesktopTestHelpers.CollectLayoutFindings`)
                // already runs the tab-strip overlap check this Work
                // Package adds, plus the ordinary bounds-within-parent
                // walk that (now `_structureHost` carries no margin at
                // all) transitively proves containment for every
                // descendant against its own direct parent.
                var findings = CollectLayoutFindings(window, area);
                Assert.True(findings.Count == 0, $"{area}: {string.Join(" | ", findings)}");

                // The acceptance's own literal wording, checked directly
                // too, not only through the general walk above.
                var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
                var structureHost = GetPrivateField<ContentControl>(projectWorkspace, "_structureHost");

                var contentPresenter = structureHost.GetVisualAncestors()
                    .OfType<ContentPresenter>()
                    .First(p => p.Name == "PART_SelectedContentHost");
                var tabControl = (TabControl)contentPresenter.TemplatedParent!;
                var stripHost = tabControl.GetVisualDescendants()
                    .OfType<ItemsPresenter>()
                    .First(p => ReferenceEquals(p.TemplatedParent, tabControl));

                var stripOrigin = stripHost.TranslatePoint(new Point(0, 0), tabControl)!.Value;
                var stripBounds = new Rect(stripOrigin, stripHost.Bounds.Size);
                var presenterOrigin = contentPresenter.TranslatePoint(new Point(0, 0), tabControl)!.Value;
                var presenterBounds = new Rect(presenterOrigin, contentPresenter.Bounds.Size);

                // `_structureHost` itself, checked directly against the
                // presenter that is now its exact parent bounds (no margin
                // left to escape it with) — the single most direct
                // statement of "the Structure tab's content presenter is
                // the surface's parent bounds" (`brief-19.4A.md` scope
                // item 1). A deeper, whole-subtree version of this same
                // pair of checks already runs above as part of
                // `CollectLayoutFindings` (child-against-its-own-direct-
                // parent, transitively, with the same `ScrollViewer`/
                // `Viewbox`/negative-margin exemptions the rest of the
                // suite relies on) — re-deriving that here, but as one
                // absolute comparison against a single distant ancestor,
                // would also have to re-derive every one of those
                // exemptions or produce false positives on layout that
                // is correct but merely wide (a non-wrapping toolbar row
                // inside a horizontally scrolling ancestor, say) and has
                // nothing to do with this Work Package's fix.
                var hostOrigin = structureHost.TranslatePoint(new Point(0, 0), tabControl)!.Value;
                var hostBounds = new Rect(hostOrigin, structureHost.Bounds.Size);
                AssertWithinPresenter(hostBounds, presenterBounds, area, "_structureHost");
                AssertNeverOverlapsStrip(hostBounds, stripBounds, area, "_structureHost");
            }
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private const double Tolerance = 1.0;

    private static void AssertWithinPresenter(Rect bounds, Rect presenterBounds, string area, string what)
    {
        var withinPresenter =
            bounds.X >= presenterBounds.X - Tolerance
            && bounds.Y >= presenterBounds.Y - Tolerance
            && bounds.Right <= presenterBounds.Right + Tolerance
            && bounds.Bottom <= presenterBounds.Bottom + Tolerance;
        Assert.True(withinPresenter, $"{area}: {what} at {bounds} lies outside the tab content presenter at {presenterBounds}.");
    }

    private static void AssertNeverOverlapsStrip(Rect bounds, Rect stripBounds, string area, string what)
    {
        var overlap = stripBounds.Intersect(bounds);
        Assert.True(overlap.Width <= 0.5 || overlap.Height <= 0.5, $"{area}: {what} at {bounds} intersects the tab strip at {stripBounds} (overlap {overlap}).");
    }

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
