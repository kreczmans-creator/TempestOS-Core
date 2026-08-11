using Avalonia.Controls;
using Avalonia.Input;

namespace Tempest.Desktop.Input;

/// <summary>
/// The Keyboard Shortcut Framework (`WP 10.0B`: one binding, <c>Ctrl+K</c>;
/// extended `WP 10.2A` with document-switching, explorer, and inline-edit
/// bindings, `WP10.0A Interaction Specification.md` §2/§6; extended `WP
/// 10.6A` with Undo/Redo/Favourite — Keyboard Productivity). Every bound
/// action is also reachable from a visible control — the Command Palette
/// is also openable from the toolbar, document switching from clicking a
/// tab directly, Undo/Redo from the Ribbon's own new buttons, and so on —
/// mirroring `WP10.0A UX Architecture Document.md` §11's own
/// "convenience, never capability" rule applied to shortcuts
/// specifically.
/// </summary>
/// <remarks>
/// Distinct from <see cref="Tempest.Desktop.Input.KeyboardCommandBindingProvider"/>
/// (`WP 10.6A`) — this class binds this small, fixed set of navigation/
/// structural actions directly; that class is a second, additive,
/// generic <c>gesture → Command Id</c> mechanism proving keyboard input
/// is just another <see cref="Tempest.Core.Input.IInputBindingProvider"/>.
/// </remarks>
public static class KeyboardShortcuts
{
    /// <summary>Registers every global shortcut this Work Package's own Navigation requirements name, on <paramref name="target"/>.</summary>
    /// <param name="target">The top-level input element shortcuts are observed on — the main window.</param>
    public static void Register(InputElement target, KeyboardShortcutActions actions)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(actions);

        target.KeyDown += (_, e) =>
        {
            // Ctrl+K — open the Command Palette (WP 10.0B, unchanged).
            if (e.Key == Key.K && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.OpenCommandPalette();
                e.Handled = true;
            }
            // Ctrl+Tab / Ctrl+Shift+Tab — document switching (WP 10.2A Navigation).
            else if (e.Key == Key.Tab && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.SelectNextDocument();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift))
            {
                actions.SelectPreviousDocument();
                e.Handled = true;
            }
            // Ctrl+W — close the active (non-Home) document tab.
            else if (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.CloseActiveDocument();
                e.Handled = true;
            }
            // Ctrl+F — focus the Project Explorer's own filter box.
            else if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.FocusExplorerFilter();
                e.Handled = true;
            }
            // Ctrl+Z / Ctrl+Y — Undo/Redo (WP 10.6A, ADR-0099).
            else if (e.Key == Key.Z && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.Undo();
                e.Handled = true;
            }
            else if (e.Key == Key.Y && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.Redo();
                e.Handled = true;
            }
            // Ctrl+D — toggle Favourite on the current selection (WP 10.6A).
            else if (e.Key == Key.D && e.KeyModifiers == KeyModifiers.Control)
            {
                actions.ToggleFavourite();
                e.Handled = true;
            }
        };
    }
}

/// <summary>The real actions <see cref="KeyboardShortcuts.Register"/> binds — one small, named delegate bundle rather than four separate <see cref="Action"/> parameters, kept together since they are always supplied together by the one owning <c>MainWindow</c>.</summary>
public sealed class KeyboardShortcutActions
{
    /// <summary>Initialises a new instance of the <see cref="KeyboardShortcutActions"/> class.</summary>
    public KeyboardShortcutActions(
        Action openCommandPalette,
        Action selectNextDocument,
        Action selectPreviousDocument,
        Action closeActiveDocument,
        Action focusExplorerFilter,
        Action undo,
        Action redo,
        Action toggleFavourite)
    {
        OpenCommandPalette = openCommandPalette ?? throw new ArgumentNullException(nameof(openCommandPalette));
        SelectNextDocument = selectNextDocument ?? throw new ArgumentNullException(nameof(selectNextDocument));
        SelectPreviousDocument = selectPreviousDocument ?? throw new ArgumentNullException(nameof(selectPreviousDocument));
        CloseActiveDocument = closeActiveDocument ?? throw new ArgumentNullException(nameof(closeActiveDocument));
        FocusExplorerFilter = focusExplorerFilter ?? throw new ArgumentNullException(nameof(focusExplorerFilter));
        Undo = undo ?? throw new ArgumentNullException(nameof(undo));
        Redo = redo ?? throw new ArgumentNullException(nameof(redo));
        ToggleFavourite = toggleFavourite ?? throw new ArgumentNullException(nameof(toggleFavourite));
    }

    /// <summary>Invoked on <c>Ctrl+K</c>.</summary>
    public Action OpenCommandPalette { get; }

    /// <summary>Invoked on <c>Ctrl+Tab</c>.</summary>
    public Action SelectNextDocument { get; }

    /// <summary>Invoked on <c>Ctrl+Shift+Tab</c>.</summary>
    public Action SelectPreviousDocument { get; }

    /// <summary>Invoked on <c>Ctrl+W</c>.</summary>
    public Action CloseActiveDocument { get; }

    /// <summary>Invoked on <c>Ctrl+F</c>.</summary>
    public Action FocusExplorerFilter { get; }

    /// <summary>Invoked on <c>Ctrl+Z</c> (`WP 10.6A`).</summary>
    public Action Undo { get; }

    /// <summary>Invoked on <c>Ctrl+Y</c> (`WP 10.6A`).</summary>
    public Action Redo { get; }

    /// <summary>Invoked on <c>Ctrl+D</c> (`WP 10.6A`).</summary>
    public Action ToggleFavourite { get; }
}
