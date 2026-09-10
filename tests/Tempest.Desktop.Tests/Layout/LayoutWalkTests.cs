using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.Evidence;
using Tempest.Desktop.Views;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Shell;
using Xunit.Abstractions;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests.Layout;

/// <summary>
/// `WP 19.3A` — the layout verification CI walk: every rail entry
/// (<see cref="ShellAreas.RailModules"/>) and every project tab
/// (<see cref="ProjectAreas.All"/>), at two window sizes, screenshotted and
/// checked for the two properties a person needs a screen to actually be
/// usable (`TD-83`, the `WP 17.0A` overlap class,
/// <see cref="AssertLayoutIsSound"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>No OS-level automation.</b> The row this Work Package implements
/// describes a Windows CI job walking the built Desktop "by automation
/// id" — this repository has no FlaUI/WinAppDriver dependency, and adding
/// one is outside this brief. <see cref="MainWindow"/> already runs
/// in-process under Avalonia.Headless with real Skia rendering
/// (<see cref="TestAppBuilder"/>), so the walk is built here instead: every
/// move is driven through <see cref="IShellNavigator"/> and
/// <see cref="MainWindow.RenderCurrentModuleAsync"/>, exactly as the
/// existing journey tests drive the window — never a simulated click found
/// by searching the tree for an automation id.
/// </para>
/// <para>
/// <b>Project tabs, driven the same way <see cref="ProductGapReconciliationAuditTests"/>
/// already does.</b> "Selecting" a project tab means
/// <see cref="IShellNavigator.OpenProjectAsync"/> with that
/// <see cref="ProjectArea"/> — the same real navigator call the tab
/// strip's own <c>SelectionChanged</c> handler makes
/// (<see cref="ProjectWorkspaceView"/>), which is what then selects the
/// tab and refreshes its content through <see cref="MainWindow.RenderCurrentModuleAsync"/>'s
/// own pipeline. <see cref="ProjectArea.Engineering"/> is a real project
/// tab that leaves the project workspace entirely for the Engineering
/// Workspace (<c>ProjectWorkspaceView.EngineeringRequested</c>) — walked
/// and screenshotted identically to every other tab, whatever ends up
/// rendered.
/// </para>
/// <para>
/// <b>The whole window, every time.</b> Both the screenshot and
/// <see cref="AssertLayoutIsSound"/> cover the entire <see cref="MainWindow"/>,
/// not just the swapped-in module content — the rail, the ribbon and the
/// status bar are real, persistent siblings of every module, and
/// <c>MainWindow</c>'s own root <c>Grid</c> (the dialogs, the command
/// palette, the toast host) is only reachable by walking the whole window.
/// </para>
/// </remarks>
[Trait("Category", "LayoutWalk")]
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class LayoutWalkTests
{
    private static readonly (string Name, double Width, double Height)[] Sizes =
    [
        ("1600x900", 1600, 900),
        ("1180x760", 1180, 760), // below DesignTokens.CompactShellWidth (1240) — the rail folds.
    ];

    private readonly ITestOutputHelper _output;

    /// <summary>Initialises a new instance of the <see cref="LayoutWalkTests"/> class.</summary>
    public LayoutWalkTests(ITestOutputHelper output) => _output = output;

    [AvaloniaFact]
    public async Task EveryRailEntryAndProjectTab_LaysOutCleanly_AtBothWindowSizes()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        var outputRoot = ResolveOutputRoot();
        var pngCount = 0;
        string? captureMethod = null;

        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            var navigator = host.ShellNavigator!;

            // One Part, one Assembly, one Evidence record — so every area
            // has real content to lay out, not an empty state (`brief-19.3A.md` §1).
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-LAYOUT", "Layout Walk Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await host.Workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var assembly = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "Layout Walk Assembly" }));
            Assert.True(assembly.Result!.Succeeded, assembly.Result.Message);

            await host.Workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Layout Walk Part" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);

            await host.EvidenceService!.CreateAsync(project.Id, "Layout Walk Evidence", EvidenceClassification.Calculation);

            foreach (var size in Sizes)
            {
                var sizeDir = Path.Combine(outputRoot, size.Name);
                Directory.CreateDirectory(sizeDir);
                LayOut(window, size.Width, size.Height);

                foreach (var module in ShellAreas.RailModules)
                {
                    if (module.Area == ShellArea.Engineering)
                        await navigator.GoToEngineeringAsync();
                    else
                        await navigator.GoToModuleAsync(module.Area);

                    await window.RenderCurrentModuleAsync();
                    LayOut(window, size.Width, size.Height);

                    var area = $"{size.Name} · rail · {module.Title}";
                    captureMethod ??= SaveFrame(window, Path.Combine(sizeDir, $"rail-{module.Title}.png"));
                    pngCount++;
                    AssertLayoutIsSound(window, area);
                }

                foreach (var descriptor in ProjectAreas.All)
                {
                    await navigator.OpenProjectAsync(project.Id, descriptor.Area);
                    await window.RenderCurrentModuleAsync();
                    LayOut(window, size.Width, size.Height);

                    var area = $"{size.Name} · tab · {descriptor.Title}";
                    captureMethod ??= SaveFrame(window, Path.Combine(sizeDir, $"tab-{descriptor.Title}.png"));
                    pngCount++;
                    AssertLayoutIsSound(window, area);
                }
            }

            _output.WriteLine($"Captured {pngCount} PNGs under '{outputRoot}' via {captureMethod}.");
            Assert.Equal((ShellAreas.RailModules.Count + ProjectAreas.All.Count) * Sizes.Length, pngCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Proves <see cref="AssertLayoutIsSound"/> actually catches an overlap
    /// rather than passing trivially — a deliberate overlap in a scratch
    /// two-control Canvas, caught with a message naming both controls
    /// (acceptance 1, `brief-19.3A.md`).
    /// </summary>
    [AvaloniaFact]
    public void AssertLayoutIsSound_CatchesADeliberateOverlap_NamingBothControls()
    {
        // A real (non-decoration) control each: `IsDecorationOnly` treats a
        // childless `Border` as a selection accent by design (the same
        // exclusion `AssertNoSiblingOverlap` always had), so each carries a
        // real `TextBlock` child, exactly like any real content control.
        var first = new Border { Width = 100, Height = 100, Child = new TextBlock { Text = "A" } };
        AutomationProperties.SetName(first, "Scratch Control A");
        var second = new Border { Width = 100, Height = 100, Child = new TextBlock { Text = "B" } };
        AutomationProperties.SetName(second, "Scratch Control B");

        var canvas = new Canvas { Width = 200, Height = 200 };
        Canvas.SetLeft(first, 0);
        Canvas.SetTop(first, 0);
        Canvas.SetLeft(second, 20);
        Canvas.SetTop(second, 20);
        canvas.Children.Add(first);
        canvas.Children.Add(second);

        var scratchWindow = new Window { Content = canvas, Width = 300, Height = 300 };
        try
        {
            LayOut(scratchWindow, 300, 300);

            Exception? caught = null;
            try
            {
                AssertLayoutIsSound(scratchWindow, "Scratch");
            }
            catch (Exception ex)
            {
                caught = ex;
            }

            Assert.NotNull(caught);
            Assert.Contains("Scratch Control A", caught!.Message, StringComparison.Ordinal);
            Assert.Contains("Scratch Control B", caught.Message, StringComparison.Ordinal);
        }
        finally
        {
            scratchWindow.Close();
        }
    }

    /// <summary>
    /// The layout walk's own real render/fallback (`brief-19.3A.md`'s
    /// "the realisation"): <see cref="HeadlessWindowExtensions.CaptureRenderedFrame"/>
    /// first, and only if it cannot produce a real bitmap in this headless
    /// Skia configuration — <see langword="null"/>, or the historical 1x1
    /// stub (`TD-100`) — <see cref="RenderTargetBitmap"/> over the window's
    /// own visual instead. Both are real Avalonia render paths; neither is
    /// a placeholder image.
    /// </summary>
    private static (Bitmap Frame, string Method) CaptureFrame(Window window)
    {
        WriteableBitmap? viaHeadless;
        try
        {
            viaHeadless = window.CaptureRenderedFrame();
        }
        catch
        {
            viaHeadless = null;
        }

        if (viaHeadless is { PixelSize.Width: > 1, PixelSize.Height: > 1 })
            return (viaHeadless, "CaptureRenderedFrame");

        viaHeadless?.Dispose();

        var pixelSize = new PixelSize(Math.Max(1, (int)window.Bounds.Width), Math.Max(1, (int)window.Bounds.Height));
        var rendered = new RenderTargetBitmap(pixelSize, new Vector(96, 96));
        rendered.Render(window);
        return (rendered, "RenderTargetBitmap");
    }

    private static string SaveFrame(Window window, string path)
    {
        var (frame, method) = CaptureFrame(window);
        using (frame)
            frame.Save(path);
        return method;
    }

    /// <summary>
    /// The layout walk's own default output root: relative to the test
    /// assembly's own output directory, unless <c>TEMPEST_LAYOUT_OUTPUT</c>
    /// is set (the CI step below pins it to the repository-root-relative
    /// path the <c>layout-screenshots</c> artefact upload reads).
    /// </summary>
    private static string ResolveOutputRoot()
    {
        var custom = Environment.GetEnvironmentVariable("TEMPEST_LAYOUT_OUTPUT");
        return string.IsNullOrEmpty(custom)
            ? Path.Combine(AppContext.BaseDirectory, "TestResults", "layout")
            : Path.GetFullPath(custom);
    }

    /// <summary>Resizes the real window and runs a real layout pass, mirroring <c>MainWindowResizeTests.Resize</c>.</summary>
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
