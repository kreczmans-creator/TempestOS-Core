using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>`v0.25.1` PO runbook R1: a click outside the command palette closes it, as Escape does.</summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CommandPaletteClickAwayTests
{
    [AvaloniaFact]
    public async Task AClickOutsideThePalette_ClosesIt_AndAClickInsideDoesNot()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host) { Width = 1280, Height = 800 };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var palette = window.GetLogicalDescendants().OfType<CommandPaletteOverlay>().Single();
            palette.Open();
            Dispatcher.UIThread.RunJobs();
            Assert.True(palette.IsOpen);

            // Inside: the palette's own query box.
            var inside = palette.TranslatePoint(new Point(40, 20), window)!.Value;
            window.MouseDown(inside, MouseButton.Left);
            window.MouseUp(inside, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(palette.IsOpen);

            // Outside: the bottom-left corner of the window.
            window.MouseDown(new Point(20, 780), MouseButton.Left);
            window.MouseUp(new Point(20, 780), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.False(palette.IsOpen);

            window.Close();
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
