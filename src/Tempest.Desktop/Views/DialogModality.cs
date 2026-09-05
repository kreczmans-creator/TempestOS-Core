using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Tempest.Desktop.Views;

/// <summary>
/// Real modal focus behaviour for the Dialog Framework's own overlay
/// controls (`WP 16.5A`, closing part of `TD-65`: "focus moves in, is
/// trapped, is restored") — a small static helper, deliberately not a
/// shared base class. A shared dialog base class was considered and
/// rejected at `WP10.5B Architecture Review.md` §2 ("each dialog's own
/// layout is genuinely different enough... that forcing a common base
/// class would cost more than it would save"); the same reasoning holds
/// here — every dialog already shares the two things a base class would
/// have given it (the styling tokens, and now this), without paying for
/// a shared inheritance hierarchy across genuinely different layouts.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this installs, once per dialog instance:</b>
/// </para>
/// <list type="number">
/// <item><description><see cref="KeyboardNavigation.SetTabNavigation"/>
/// with <see cref="KeyboardNavigationMode.Cycle"/> — Tab/Shift+Tab
/// cycles within the dialog's own logical tree instead of escaping it
/// (`MainWindow` separately traps Tab from reaching the shell behind the
/// dialog — see its own "modal count" remarks).</description></item>
/// <item><description>Focus capture/restore, driven by the dialog's own
/// <see cref="Visual.IsVisibleProperty"/> — the element focused the
/// instant the dialog becomes visible (before the dialog sets its own
/// initial focus) is remembered, and refocused the instant the dialog is
/// hidden again. Every dialog already flips <c>IsVisible</c> as its own
/// open/close signal (<c>ShowAsync</c>/<c>ConfirmAsync</c>/<c>PromptAsync</c>/<c>Open</c>
/// set it true; <c>Complete</c>/<c>Close</c> set it false), so this needs
/// no new open/close vocabulary and cannot drift from what each dialog
/// already does.</description></item>
/// </list>
/// </remarks>
internal static class DialogModality
{
    /// <summary>
    /// Installs modal focus-trap/restore behaviour on
    /// <paramref name="dialog"/>. Call once, at construction.
    /// </summary>
    public static void Install(Border dialog)
    {
        ArgumentNullException.ThrowIfNull(dialog);

        KeyboardNavigation.SetTabNavigation(dialog, KeyboardNavigationMode.Cycle);

        IInputElement? restoreTarget = null;

        dialog.PropertyChanged += (_, e) =>
        {
            if (e.Property != Visual.IsVisibleProperty)
                return;

            if (dialog.IsVisible)
            {
                // Captured before the dialog's own ShowAsync/Open sets its
                // own initial focus (the very next statement after
                // `IsVisible = true` in every dialog) — this handler runs
                // synchronously as part of that assignment.
                restoreTarget = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement();
            }
            else
            {
                if (restoreTarget is InputElement { IsEffectivelyVisible: true, IsEffectivelyEnabled: true } target)
                    target.Focus();
                restoreTarget = null;
            }
        };
    }
}
