using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Views;
using Tempest.Samples;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// The header's project chip as a real way back into the open project
/// (`WP-Z4` Productisation Phase 1, P0) — <c>IShellNavigator.ReturnToProjectAsync</c>
/// existed with zero Desktop call sites before this change, so entering
/// Engineering from a project had no path back at all. Also carries the
/// header search box's own journey (`TD-177`): its typed text reaches the
/// Command Palette rather than being retyped there.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ShellHeaderReturnToProjectTests
{
    [AvaloniaFact]
    public async Task ProjectChip_Disabled_WithNoProjectOpen()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);

            var chip = window.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Return to project");
            Assert.False(chip.IsEnabled);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectChip_Click_FromEngineering_ActuallyReturnsToTheProjectWorkspace()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var navigator = host.ShellNavigator!;
            var directory = host.ProjectDirectory!;
            var window = new MainWindow(host);

            var project = await directory.CreateAsync("P-9000", "Chip Return Test");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            // Away from the project's own workspace — Engineering, the
            // navigation dead end this fix closes.
            await navigator.GoToEngineeringAsync();
            await window.RenderCurrentModuleAsync();
            Assert.Equal(ShellArea.Engineering, navigator.Current.Area);

            var chip = window.GetLogicalDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Return to project");
            Assert.True(chip.IsEnabled);
            chip.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var deadline = DesktopTestHelpers.Deadline(2);
            while (navigator.Current.Area != ShellArea.ProjectWorkspace && DateTime.UtcNow < deadline)
                await Task.Delay(10);

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(project.Id, navigator.Current.ProjectId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// `TD-177`: typing into the header's own search box and pressing
    /// Enter opens the real Command Palette — over the real,
    /// composer-wired <c>MainWindow</c>, not a hand-wired stand-in — with
    /// that same text already seeded as its query, and the Objects section
    /// it drives lists the project created earlier in this test. The box
    /// itself is cleared once handed over.
    /// </summary>
    [AvaloniaFact]
    public async Task TypingInTheHeaderSearch_PressingEnter_OpensThePaletteWithThatQuery_AndListsTheEarlierObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var directory = host.ProjectDirectory!;
            var window = new MainWindow(host);
            LayOut(window);

            var project = await directory.CreateAsync("P-9200", "Header Search Handoff Target");

            var searchBox = window.GetLogicalDescendants().OfType<TextBox>()
                .Single(b => AutomationProperties.GetName(b) == "Search or run a command");
            searchBox.Text = "Handoff Target";
            searchBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            // The box hands over, then clears itself — the palette now
            // holds the text, not a second copy of it.
            Assert.Equal(string.Empty, searchBox.Text);

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            Assert.True(palette.IsOpen);

            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            var resultsBox = (ListBox)((StackPanel)palette.Child!).Children[1];
            Assert.Equal("Handoff Target", queryBox.Text);

            PaletteObjectHit? selectedHit = null;
            palette.ObjectSelected += hit => selectedHit = hit;

            await RenderUntilAsync(window, () =>
                resultsBox.ItemsSource is IReadOnlyList<ListBoxItem> items
                    && items.Any(i => i.Content is string s && s.Contains("Header Search Handoff Target", StringComparison.Ordinal)));

            var rowIndex = ((IReadOnlyList<ListBoxItem>)resultsBox.ItemsSource!)
                .ToList()
                .FindIndex(i => i.Content is string s && s.Contains("Header Search Handoff Target", StringComparison.Ordinal));
            Assert.True(rowIndex >= 0, "Expected the Objects section to list the project created earlier in this test.");

            resultsBox.SelectedIndex = rowIndex;
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(selectedHit);
            Assert.Equal(project.Id, selectedHit!.ObjectId);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(10);
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
