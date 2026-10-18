using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// PO runbook D9 ("search shortcut doesn't work. Manually selecting the
/// search leaves the bar hanging"): Ctrl+K opens the palette even when the
/// focused control has already handled the key, and a click into the
/// header search box opens the palette straight away.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class HeaderSearchAndCtrlKTests
{
    [AvaloniaFact]
    public async Task CtrlK_OpensThePalette_EvenWhenTheFocusedControlHandlesTheKey()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);

            var searchBox = SearchBox(window);
            searchBox.KeyDown += (_, e) => e.Handled = true;
            searchBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.K, KeyModifiers = KeyModifiers.Control });

            Assert.True(GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette").IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ClickingTheHeaderSearch_OpensThePalette_WithFocusInItsQuery()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);

            var searchBox = SearchBox(window);
            var centre = searchBox.TranslatePoint(new Point(searchBox.Bounds.Width / 2, searchBox.Bounds.Height / 2), window)!.Value;
            window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            Assert.True(palette.IsOpen);
            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            Assert.True(queryBox.IsFocused);

            // Closing hands focus back to the header box without reopening.
            palette.Close();
            Dispatcher.UIThread.RunJobs();
            Assert.False(palette.IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static TextBox SearchBox(MainWindow window) =>
        window.GetLogicalDescendants().OfType<TextBox>().Single(b => AutomationProperties.GetName(b) == "Search or run a command");

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}
