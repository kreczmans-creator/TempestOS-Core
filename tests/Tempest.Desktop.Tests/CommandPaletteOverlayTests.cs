using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Tempest.Core.Commands;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates Command Palette Opening (`WP 10.0B`'s own "Demonstrate"
/// list) against a real <see cref="ICommandRegistry"/> resolved from a
/// running <see cref="WorkspaceHost"/> — the same registry every real
/// discipline's own commands (`mechanical.create`, `requirements.revise`,
/// etc.) are already registered against, unchanged (`ADR-0070`).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class CommandPaletteOverlayTests
{
    [AvaloniaFact]
    public async Task Open_ShowsTheOverlay_PopulatedFromTheRealCommandRegistry()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            Assert.True(registry.Items.Count > 0, "Expected real commands already registered by the six Engineering Disciplines.");

            var palette = new CommandPaletteOverlay(registry);
            Assert.False(palette.IsOpen);

            palette.Open();
            Assert.True(palette.IsOpen);

            palette.Close();
            Assert.False(palette.IsOpen);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ConfirmingACommandWithNoCreateDefault_RaisesCommandUnavailable_NotASilentNoOp()
    {
        // WP 10.3B's own genuine, disclosed defect fix (class remarks):
        // every real discipline command has CreateDefault == null, so
        // pressing Enter on one previously closed the palette and did
        // nothing else, with zero feedback. Confirmed here directly.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            Assert.Contains(registry.Items, d => d.CreateDefault is null); // the real, confirmed precondition

            var palette = new CommandPaletteOverlay(registry);
            palette.Open();

            CommandDescriptor? unavailable = null;
            CommandDescriptor? invoked = null;
            palette.CommandUnavailable += d => unavailable = d;
            palette.CommandInvoked += d => invoked = d;

            // OnQueryKeyDown is wired to the palette's own inner query
            // TextBox (its own first child), not the palette Border
            // itself — found directly rather than exposing a test-only
            // accessor.
            var queryBox = (TextBox)((StackPanel)palette.Child!).Children[0];
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(unavailable);
            Assert.Null(invoked);
            Assert.False(palette.IsOpen); // still closes, exactly as a real invocation would
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }
}
