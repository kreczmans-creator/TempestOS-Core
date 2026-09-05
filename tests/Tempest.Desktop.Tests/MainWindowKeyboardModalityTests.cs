using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `TD-83` — real, end-to-end proof (over a real <see cref="MainWindow"/>
/// on a real, started <see cref="WorkspaceHost"/>) that opening any of
/// this shell's six modal overlays actually traps keyboard Tab
/// navigation inside it, and that closing one actually restores both the
/// caller's own prior focus and the shell's own Tab navigation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why <see cref="KeyboardNavigationHandler.GetNext"/>, not simulated
/// Tab key presses.</b> A real Tab keypress only moves focus one step per
/// dispatched event and depends on Avalonia's own input pipeline routing
/// it correctly through a headless <see cref="TopLevel"/> — an extra,
/// unnecessary layer of indirection for what this test actually needs to
/// prove: <em>where would Tab go next, from here, right now</em>.
/// <see cref="KeyboardNavigationHandler.GetNext(IInputElement, NavigationDirection)"/>
/// is a real, public, <b>static</b> API (confirmed by direct reflection
/// against the exact referenced Avalonia 11.2.3 assembly before writing
/// this test — a stateless tree walk, needing no handler instance) that
/// answers that directly, without needing to also dispatch/pump anything.
/// Walking it repeatedly from the dialog's own initial focus and
/// asserting every stop stays inside the dialog (never inside
/// <c>_dock</c>) is a strictly stronger proof than one simulated Tab: it
/// exhausts every reachable stop, not just the first.
/// </para>
/// <para>
/// <b>Escape closes every one of the six</b> (`WP 16.5A` — Settings and
/// MacroManager gained it this Work Package; every other dialog already
/// had it), so one uniform open/verify/close shape covers all six without
/// six different close gestures.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class MainWindowKeyboardModalityTests
{
    [AvaloniaFact]
    public async Task EveryModalOverlay_TrapsTabInsideItself_AndRestoresFocusAndDockTabNavigationOnClose()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            window.Show();

            var dock = GetPrivateField<DockPanel>(window, "_dock");
            // Several Ribbon/rail buttons are conditionally disabled with
            // no selection — the original control must actually be
            // focusable right now, not merely carry `Focusable = true`.
            var original = dock.GetLogicalDescendants().OfType<Button>()
                .First(b => b.Focusable && b.IsEffectivelyEnabled && b.IsEffectivelyVisible);

            var confirmationDialog = GetPrivateField<ConfirmationDialog>(window, "_confirmationDialog");
            var inputDialog = GetPrivateField<InputDialog>(window, "_inputDialog");
            var messageDialog = GetPrivateField<MessageDialog>(window, "_messageDialog");
            var settingsDialog = GetPrivateField<SettingsDialog>(window, "_settingsDialog");
            var macroManagerDialog = GetPrivateField<MacroManagerDialog>(window, "_macroManagerDialog");
            var commandPalette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");

            await VerifyModalTrapAsync(window, dock, original, confirmationDialog,
                () => { _ = confirmationDialog.ConfirmAsync("Delete?", "This cannot be undone.", "Delete"); return Task.CompletedTask; });

            await VerifyModalTrapAsync(window, dock, original, inputDialog,
                () => { _ = inputDialog.PromptAsync("New Part", "Name:"); return Task.CompletedTask; });

            await VerifyModalTrapAsync(window, dock, original, messageDialog,
                () => { _ = messageDialog.ShowAsync(FeedbackSeverity.Info, "Saved", "Your changes were saved."); return Task.CompletedTask; });

            await VerifyModalTrapAsync(window, dock, original, settingsDialog,
                () => { _ = settingsDialog.ShowAsync(); return Task.CompletedTask; });

            await VerifyModalTrapAsync(window, dock, original, macroManagerDialog,
                () => macroManagerDialog.ShowAsync());

            await VerifyModalTrapAsync(window, dock, original, commandPalette,
                () => { commandPalette.Open(); return Task.CompletedTask; });
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// Focuses <paramref name="original"/>, opens <paramref name="dialog"/>
    /// via <paramref name="open"/>, walks every reachable Tab stop from its
    /// own initial focus asserting each stays inside the dialog and never
    /// inside <paramref name="dock"/>, closes it with Escape, and asserts
    /// focus and <paramref name="dock"/>'s own Tab navigation are both
    /// exactly restored.
    /// </summary>
    private static async Task VerifyModalTrapAsync(Window window, Control dock, Control original, Border dialog, Func<Task> open)
    {
        original.Focus();
        Assert.True(original.IsFocused, $"Setup failure before {dialog.GetType().Name}: could not focus the original control.");
        Assert.Equal(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(dock));

        await open();

        Assert.True(dialog.IsVisible, $"{dialog.GetType().Name} did not open.");
        Assert.False(original.IsFocused);
        // TD-83: Tab cannot reach content behind an open dialog — the
        // shell's own dock is sealed for the duration.
        Assert.Equal(KeyboardNavigationMode.None, KeyboardNavigation.GetTabNavigation(dock));

        var initialFocus = TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement();
        Assert.NotNull(initialFocus);
        AssertInsideDialogNeverInsideDock(dialog, dock, initialFocus!);

        var current = initialFocus;
        for (var step = 0; step < 25; step++)
        {
            // A static, stateless tree walk (confirmed by direct
            // reflection against the exact referenced Avalonia 11.2.3
            // assembly before writing this test) — it needs no handler
            // instance, only the element to walk from. Never moves real
            // focus itself, so `initialFocus` below is unaffected by it.
            var next = KeyboardNavigationHandler.GetNext(current!, NavigationDirection.Next);
            if (next is null)
                break; // no further stop — trivially never inside dock either.

            AssertInsideDialogNeverInsideDock(dialog, dock, next);
            current = next;
        }

        // Escape raised on whatever actually holds focus — exactly what a
        // real keypress bubbles from. Every dialog's own Escape handling
        // lives at a different depth (the dialog root for five of the six;
        // `InputDialog`'s own lives on its inner `TextBox` instead), so
        // this is the one raise target correct for all six, without special
        // casing any of them here.
        ((InputElement)initialFocus!).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });

        Assert.False(dialog.IsVisible, $"{dialog.GetType().Name} did not close on Escape.");
        Assert.True(original.IsFocused, $"{dialog.GetType().Name} did not restore focus to the originally focused control.");
        Assert.Equal(KeyboardNavigationMode.Continue, KeyboardNavigation.GetTabNavigation(dock));
    }

    private static void AssertInsideDialogNeverInsideDock(Border dialog, Control dock, IInputElement stop)
    {
        var logical = (ILogical)stop;
        var insideDialog = ReferenceEquals(dialog, stop) || dialog.IsLogicalAncestorOf(logical);
        var insideDock = dock.IsLogicalAncestorOf(logical);

        Assert.True(insideDialog, $"Tab reached '{stop}', which is not a logical descendant of the open {dialog.GetType().Name}.");
        Assert.False(insideDock, $"Tab reached '{stop}', which is a logical descendant of the shell's own dock while {dialog.GetType().Name} is open.");
    }
}
