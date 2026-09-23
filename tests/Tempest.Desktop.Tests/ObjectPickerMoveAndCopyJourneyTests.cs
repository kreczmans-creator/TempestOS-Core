using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.History;
using Tempest.Desktop.Views;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.2A` (S2-2, FCR-0073) — the object picker's own end-to-end journey:
/// select a Part in the real Project Explorer, press <c>Ctrl+Shift+M</c>
/// (or <c>Ctrl+Shift+C</c>), choose a destination from the real
/// <see cref="ObjectPickerDialog"/>, and confirm the Move/Copy actually
/// happened — the Domain reparented (or a new object exists), and the real
/// Project Explorer's own <see cref="IProjectExplorer.GetChildrenAsync"/>
/// shows it under the chosen destination.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ObjectPickerMoveAndCopyJourneyTests
{
    [AvaloniaFact]
    public async Task CtrlShiftM_MovesTheSelectedPart_ViaThePicker_AndTheExplorerShowsItUnderTheChosenDestination()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-MOVE-1", "Move Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

            var destination = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "Destination Assembly" }));
            Assert.True(destination.Result!.Succeeded, destination.Result.Message);
            var destinationId = destination.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Movable Part" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var partId = part.Result.SubjectId!.Value;

            // Selected by Id, through the real selection service — the
            // identical (Id, Kind) pair a real Project Explorer click
            // raises, and the identical selection Ctrl+Shift+M reads. Not
            // "the first Part found in the tree": nothing about this
            // journey depends on which Part sorts first.
            await workspace.Selection.SelectAsync(partId, "Part");
            Assert.Equal(partId, workspace.Selection.Current?.ObjectId);

            var objectPicker = GetPrivateField<ObjectPickerDialog>(window, "_objectPicker");
            Assert.False(objectPicker.IsVisible);

            window.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.M,
                KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift,
            });

            await RenderUntilAsync(window, () => objectPicker.IsVisible);

            var list = GetPrivateField<ListBox>(objectPicker, "_list");
            var items = ((System.Collections.IEnumerable)list.ItemsSource!).Cast<ListBoxItem>().ToList();
            Assert.Contains(items, i => Equals(i.Tag, destinationId.ToString()));

            list.SelectedItem = items.Single(i => Equals(i.Tag, destinationId.ToString()));
            var choose = objectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose"));
            choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !objectPicker.IsVisible);

            var deadline = Deadline(10);
            IHasParent? reread = null;
            while (DateTime.UtcNow < deadline)
            {
                reread = await domainContext.Repository.FindAsync(partId) as IHasParent;
                if (reread?.ParentId == destinationId)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            Assert.Equal(destinationId, reread?.ParentId);

            var children = await workspace.ProjectExplorer.GetChildrenAsync(destinationId);
            Assert.Contains(children, n => n.Id == partId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CtrlShiftC_CopiesTheSelectedPart_ViaThePicker_UnderTheChosenDestination()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-COPY-1", "Copy Journey Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

            var destination = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "Copy Destination Assembly" }));
            Assert.True(destination.Result!.Succeeded, destination.Result.Message);
            var destinationId = destination.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Copyable Part" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var sourcePartId = part.Result.SubjectId!.Value;

            // Selected by Id, through the real selection service — see the
            // Move journey's own identical remark.
            await workspace.Selection.SelectAsync(sourcePartId, "Part");

            var before = await workspace.ProjectExplorer.GetChildrenAsync(destinationId);

            var objectPicker = GetPrivateField<ObjectPickerDialog>(window, "_objectPicker");

            window.RaiseEvent(new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.C,
                KeyModifiers = KeyModifiers.Control | KeyModifiers.Shift,
            });

            await RenderUntilAsync(window, () => objectPicker.IsVisible);

            var list = GetPrivateField<ListBox>(objectPicker, "_list");
            var items = ((System.Collections.IEnumerable)list.ItemsSource!).Cast<ListBoxItem>().ToList();
            list.SelectedItem = items.Single(i => Equals(i.Tag, destinationId.ToString()));
            var choose = objectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose"));
            choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !objectPicker.IsVisible);

            var deadline = Deadline(10);
            IReadOnlyList<ProjectExplorerNode> after = before;
            while (DateTime.UtcNow < deadline)
            {
                after = await workspace.ProjectExplorer.GetChildrenAsync(destinationId);
                if (after.Count > before.Count)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
            }

            // A copy, not a move: the source Part is untouched, and a new
            // object — a different id, the same Kind — now sits under the
            // chosen destination.
            Assert.True(after.Count > before.Count, "Expected a new child under the copy's own destination.");
            var copy = after.Except(before).Single();
            Assert.Equal("Part", copy.Kind);
            Assert.NotEqual(sourcePartId, copy.Id);

            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
            var source = await domainContext.Repository.FindAsync(sourcePartId) as IHasParent;
            Assert.NotEqual(destinationId, source?.ParentId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ==================================================================
    // `WP 20.10C` (PO finding T6) — the real repro the two journeys above
    // did not cover: a real pointer click (PointerPressed/Released,
    // through the actual headless input pipeline, never
    // `workspace.Selection.SelectAsync` called directly) on a Part's own
    // row in the Explorer *embedded in the project's Structure tab*
    // (`WP 19.2B`) — never the standalone Engineering workspace the two
    // journeys above enter — followed by a real Ctrl+Shift+M/C keypress
    // dispatched from whatever now holds real keyboard focus, not raised
    // directly on the window. Both assert the full visible-outcome
    // contract (`brief-20.10C.md` scope item 2): the Explorer reveals the
    // result, the Status Bar's own "Selected Object" segment names both
    // ends, and Command History carries the identical entry.
    // ==================================================================

    [AvaloniaFact]
    public async Task CtrlShiftM_ARealPointerClickInTheStructureTab_MovesThePart_AndTheOutcomeIsVisible()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-MOVE-REAL", "Real Move Journey Project");

            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domainContext = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            // The real surface T6 was reported against: opening the
            // project (`ProjectArea.Overview`, the real `ProjectBrowserView`'s
            // own only call shape) and entering Engineering while it is
            // open renders inside the project's own Structure tab now
            // (`WP 19.2B`'s `EnterEngineeringAsync`), never a bare module
            // swap — the identical route `GoToEngineeringAsync` already
            // took above, genuinely reused rather than a second,
            // parallel "standalone" path. This is also what makes the
            // Explorer/Inspector docking panels present at all
            // (`EnterEngineeringAsync`'s own `EnsureCorePanelsPresent`) —
            // `IShellNavigator.OpenProjectAsync(id, ProjectArea.Engineering)`
            // exists but no real caller in this codebase ever passes a
            // non-default area except `Quote` (session restore); it is
            // not how a real user reaches the Structure tab.
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var explorerView = GetPrivateField<ProjectExplorerView>(window, "_explorerView");

            // The exact pairing the Ribbon's own category click and the
            // Cockpit's own "switch area" both already use in production
            // (`MainWindowComposer.Wire.cs`/`.Coordinators.cs`) — not a
            // view refresh fabricated only for this test.
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await explorerView.LoadAsync();
            LayOut(window);

            await workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

            var destination = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "Real Destination Assembly" }));
            Assert.True(destination.Result!.Succeeded, destination.Result.Message);
            var destinationId = destination.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Real Movable Part" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var partId = part.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();

            // Let the reactive `IWorkspaceChanges` reload each `mechanical.create`
            // above just triggered actually settle — every posted UI
            // continuation drained — before a real click starts landing on
            // rows that might otherwise be rebuilt out from under it
            // mid-interaction (`ProjectExplorerView.LoadAsync`'s own "every
            // reload rebuilds an entirely fresh `ExplorerNodeItem` tree").
            await SettleAsync(window);

            var tree = GetPrivateField<TreeView>(explorerView, "_tree");

            // The tree groups every project's own objects under that
            // project's own root row — collapsed by default, exactly like
            // any other node. A real user expands it (Right Arrow, once
            // it is focused/selected) before a child row exists to click;
            // this real repro does the identical thing rather than
            // reaching around it.
            await ExpandRowAsync(window, tree, project.Id);

            var row = await FindRowAsync(window, tree, partId);

            ClickRow(window, row);
            Assert.Equal(partId, workspace.Selection.Current?.ObjectId);

            var objectPicker = GetPrivateField<ObjectPickerDialog>(window, "_objectPicker");
            Assert.False(objectPicker.IsVisible);

            // From whatever now holds real keyboard focus after that real
            // click — never raised directly on `window`.
            window.KeyPressQwerty(PhysicalKey.M, RawInputModifiers.Control | RawInputModifiers.Shift);

            await RenderUntilAsync(window, () => objectPicker.IsVisible);

            var list = GetPrivateField<ListBox>(objectPicker, "_list");
            var items = ((System.Collections.IEnumerable)list.ItemsSource!).Cast<ListBoxItem>().ToList();
            Assert.Contains(items, i => Equals(i.Tag, destinationId.ToString()));

            list.SelectedItem = items.Single(i => Equals(i.Tag, destinationId.ToString()));
            var choose = objectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose"));
            choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !objectPicker.IsVisible);

            var deadline = Deadline(10);
            IHasParent? reread = null;
            while (DateTime.UtcNow < deadline)
            {
                reread = await domainContext.Repository.FindAsync(partId) as IHasParent;
                if (reread?.ParentId == destinationId)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                LayOut(window);
            }

            Assert.Equal(destinationId, reread?.ParentId);

            // Visible outcome (`brief-20.10C.md` scope item 2): the
            // Explorer reveals the moved Part, selected, under its new
            // parent, ancestors expanded.
            await RenderUntilAsync(window, () => explorerView.IsRevealed(partId));
            Assert.True(explorerView.IsRevealed(partId), "The Explorer did not reveal the moved Part under its new parent.");

            // The last side effect, not the trigger: revealing the moved
            // Part (above) itself raises a real selection change that
            // briefly says "Selected: ..." on the very same Status Bar
            // segment — the command's own "Moved ..." result is what must
            // be left standing once everything settles.
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var expectedStatus = "Moved 'Real Movable Part' under 'Real Destination Assembly'.";
            string? statusText = null;
            var statusDeadline = Deadline(10);
            while (DateTime.UtcNow < statusDeadline)
            {
                statusText = GetPrivateField<TextBlock>(statusBar, "_selection").Text;
                if (statusText == expectedStatus)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                LayOut(window);
            }

            Assert.Equal(expectedStatus, statusText);

            var commandHistory = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            Assert.Contains(commandHistory.Entries, entry => entry.Description == expectedStatus);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CtrlShiftC_ARealPointerClickInTheStructureTab_CopiesThePart_AndTheOutcomeIsVisible()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-COPY-REAL", "Real Copy Journey Project");

            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var explorerView = GetPrivateField<ProjectExplorerView>(window, "_explorerView");

            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            await explorerView.LoadAsync();
            LayOut(window);

            await workspace.Selection.ClearAsync();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

            var destination = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Assembly", ["displayName"] = "Real Copy Destination Assembly" }));
            Assert.True(destination.Result!.Succeeded, destination.Result.Message);
            var destinationId = destination.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();
            var part = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(
                    new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Real Copyable Part" }));
            Assert.True(part.Result!.Succeeded, part.Result.Message);
            var sourcePartId = part.Result.SubjectId!.Value;

            await workspace.Selection.ClearAsync();
            await SettleAsync(window);

            var tree = GetPrivateField<TreeView>(explorerView, "_tree");
            await ExpandRowAsync(window, tree, project.Id);

            var row = await FindRowAsync(window, tree, sourcePartId);

            ClickRow(window, row);
            Assert.Equal(sourcePartId, workspace.Selection.Current?.ObjectId);

            var before = await workspace.ProjectExplorer.GetChildrenAsync(destinationId);

            var objectPicker = GetPrivateField<ObjectPickerDialog>(window, "_objectPicker");

            window.KeyPressQwerty(PhysicalKey.C, RawInputModifiers.Control | RawInputModifiers.Shift);

            await RenderUntilAsync(window, () => objectPicker.IsVisible);

            var list = GetPrivateField<ListBox>(objectPicker, "_list");
            var items = ((System.Collections.IEnumerable)list.ItemsSource!).Cast<ListBoxItem>().ToList();
            list.SelectedItem = items.Single(i => Equals(i.Tag, destinationId.ToString()));
            var choose = objectPicker.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Choose"));
            choose.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !objectPicker.IsVisible);

            var deadline = Deadline(10);
            IReadOnlyList<ProjectExplorerNode> after = before;
            while (DateTime.UtcNow < deadline)
            {
                after = await workspace.ProjectExplorer.GetChildrenAsync(destinationId);
                if (after.Count > before.Count)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                LayOut(window);
            }

            Assert.True(after.Count > before.Count, "Expected a new child under the copy's own destination.");
            var copy = after.Except(before).Single();
            Assert.Equal("Part", copy.Kind);
            Assert.NotEqual(sourcePartId, copy.Id);

            // Visible outcome: the Explorer reveals the new copy, selected
            // — not the untouched source.
            await RenderUntilAsync(window, () => explorerView.IsRevealed(copy.Id));
            Assert.True(explorerView.IsRevealed(copy.Id), "The Explorer did not reveal the new copy under its destination.");

            // The last side effect, not the trigger: revealing the copy
            // (above) itself raises a real selection change that briefly
            // says "Selected: ..." on the very same Status Bar segment —
            // the command's own "Copied ..." result is what must be left
            // standing once everything settles, so this polls for the
            // settled text rather than reading the very first render.
            var statusBar = GetPrivateField<StatusBarView>(window, "_statusBar");
            var expectedStatus = "Copied 'Real Copyable Part' as 'Real Copyable Part (Copy)' under 'Real Copy Destination Assembly'.";
            string? statusText = null;
            var seen = new List<string>();
            var statusDeadline = Deadline(10);
            while (DateTime.UtcNow < statusDeadline)
            {
                statusText = GetPrivateField<TextBlock>(statusBar, "_selection").Text;
                if (seen.Count == 0 || seen[^1] != statusText)
                    seen.Add(statusText ?? "<null>");
                if (statusText == expectedStatus)
                    break;

                await Task.Delay(20);
                Dispatcher.UIThread.RunJobs();
                LayOut(window);
            }

            Assert.True(statusText == expectedStatus, $"Expected '{expectedStatus}', saw sequence: {string.Join(" -> ", seen)}");

            var commandHistory = GetPrivateField<CommandHistoryLog>(window, "_commandHistory");
            Assert.Contains(commandHistory.Entries, entry => entry.Description == expectedStatus);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Pumps the dispatcher until every UI continuation a just-invoked command's own reactive <see cref="Tempest.Core.Events.IWorkspaceChanges"/> reload posted has actually run — so a real click lands on a row the Explorer is not about to rebuild out from under it.</summary>
    private static async Task SettleAsync(MainWindow window)
    {
        for (var i = 0; i < 15; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    /// <summary>Polls until the Explorer has realised a real <see cref="TreeViewItem"/> row for <paramref name="objectId"/>, and returns it — never a programmatic shortcut around the real container generator/visual tree a pointer click needs.</summary>
    private static async Task<TreeViewItem> FindRowAsync(MainWindow window, TreeView tree, Guid objectId)
    {
        var deadline = Deadline(10);
        while (DateTime.UtcNow < deadline)
        {
            var row = tree.GetVisualDescendants().OfType<TreeViewItem>()
                .FirstOrDefault(item => item.DataContext is ExplorerNodeItem node && node.Node.Id == objectId);
            if (row is not null)
                return row;

            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }

        var rows = tree.GetVisualDescendants().OfType<TreeViewItem>()
            .Select(i => i.DataContext is ExplorerNodeItem n ? $"{n.Node.NodeType}:{n.Node.Kind}:{n.Node.Title}:{n.Node.Id}" : $"<{i.DataContext?.GetType().Name ?? "null"}>")
            .ToList();
        throw new InvalidOperationException(
            $"The Explorer never realised a row for '{objectId}'. Realised rows: {string.Join(" | ", rows)}.");
    }

    /// <summary>A real pointer click on <paramref name="row"/> — PointerPressed then PointerReleased through the actual headless input pipeline (<c>window.MouseDown</c>/<c>MouseUp</c>), never a programmatic <c>SelectedItem</c> assignment.</summary>
    private static void ClickRow(MainWindow window, TreeViewItem row)
    {
        var center = row.TranslatePoint(new Point(row.Bounds.Width / 2, row.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The row is not connected to the window.");

        window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);

        // `_tree.KeyDown += OnTreeKeyDown` is wired on the `TreeView`
        // itself, never per-row (`ProjectExplorerView`'s own single
        // keyboard tab-stop, arrow keys navigating within it exactly as
        // `SelectingItemsControl` always has) — so the real, correct
        // post-click focus target for the next real keystroke is the
        // tree, not the individual `TreeViewItem` container.
        if (row.FindAncestorOfType<TreeView>() is { } tree && !tree.IsFocused)
            tree.Focus();
    }

    /// <summary>Real-clicks <paramref name="objectId"/>'s own row (focusing/selecting it) and presses the Right Arrow key — the standard, real way a <see cref="TreeView"/> row expands — then polls until at least one child is realised.</summary>
    private static async Task ExpandRowAsync(MainWindow window, TreeView tree, Guid objectId)
    {
        var row = await FindRowAsync(window, tree, objectId);
        if (row.DataContext is ExplorerNodeItem { IsExpanded: true })
            return;

        ClickRow(window, row);
        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);

        var deadline = Deadline(10);
        while (DateTime.UtcNow < deadline)
        {
            if (row.DataContext is ExplorerNodeItem { IsExpanded: true })
                return;

            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }

        throw new InvalidOperationException($"The row for '{objectId}' never expanded. row.IsFocused={row.IsFocused}, tree.IsFocused={tree.IsFocused}.");
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Avalonia.Size(1900, 1050));
            window.Arrange(new Avalonia.Rect(0, 0, 1900, 1050));
        }
    }
}
