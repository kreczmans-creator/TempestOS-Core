using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
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
