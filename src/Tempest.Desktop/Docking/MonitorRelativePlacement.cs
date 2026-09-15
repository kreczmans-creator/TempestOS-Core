using Avalonia;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Converts a window's own geometry between the shell's live, absolute
/// terms (a screen position in physical pixels, a size in device-independent
/// pixels — what <see cref="Avalonia.Controls.Window.Position"/> and
/// <see cref="Avalonia.Controls.Window.Width"/>/<see cref="Avalonia.Controls.Window.Height"/>
/// already are) and the monitor-relative, DPI-normalised terms
/// `ADR-0153` decision 5 persists (an offset from a named monitor's own
/// working-area origin, a size converted to physical pixels at the scaling
/// the window was actually on).
/// </summary>
/// <remarks>
/// Deliberately pure geometry, with no <see cref="Avalonia.Controls.Screens"/>
/// or <see cref="Avalonia.Controls.Window"/> dependency — the same reason
/// <see cref="FloatingWindowPlacement"/> and
/// <see cref="Tempest.Workspace.Layout.DockTargetResolver"/> are pure: the
/// case this exists to prove — a window saved on a 150%-scaled monitor,
/// restored onto a 100%-scaled one, landing neither off-screen nor
/// shrunk to nothing — is painful to drive through a real multi-monitor
/// rig and trivial to test as a function (`ADR-0153` risk 2).
/// </remarks>
public static class MonitorRelativePlacement
{
    /// <summary>
    /// The screen among <paramref name="screens"/> that <paramref name="bounds"/>
    /// currently overlaps, or <paramref name="screens"/>' own
    /// <see cref="IScreenList.Primary"/> when it overlaps none of them —
    /// which screen a live window's own current geometry should be saved
    /// relative to.
    /// </summary>
    public static ScreenSnapshot ResolveCurrentScreen(PixelRect bounds, IScreenList screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        foreach (var screen in screens.All)
        {
            if (screen.WorkingArea.Intersects(bounds))
                return screen;
        }

        return screens.Primary;
    }

    /// <summary>
    /// The screen named by <paramref name="monitorKey"/>, or
    /// <paramref name="screens"/>' own <see cref="IScreenList.Primary"/>
    /// when it is <see langword="null"/> or matches no screen currently
    /// attached — the monitor was unplugged, or this is a different
    /// machine entirely (`ADR-0153` decision 5's own named fallback).
    /// </summary>
    public static ScreenSnapshot ResolveSavedScreen(string? monitorKey, IScreenList screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        if (monitorKey is not null)
        {
            foreach (var screen in screens.All)
            {
                if (screen.Key == monitorKey)
                    return screen;
            }
        }

        return screens.Primary;
    }

    /// <summary>
    /// A window's own current absolute position and DIP size, expressed
    /// relative to <paramref name="screen"/>'s own working-area origin,
    /// with the size converted to physical pixels at <paramref name="screen"/>'s
    /// own scaling — the shape `ADR-0153` decision 5 persists.
    /// </summary>
    public static (string MonitorKey, double X, double Y, double Width, double Height) ToRelative(
        PixelPoint position, double widthDip, double heightDip, ScreenSnapshot screen) => (
        screen.Key,
        position.X - screen.WorkingArea.X,
        position.Y - screen.WorkingArea.Y,
        widthDip * screen.Scaling,
        heightDip * screen.Scaling);

    /// <summary>
    /// The reverse of <see cref="ToRelative"/>: a saved monitor-relative,
    /// physical-pixel rectangle, resolved against <paramref name="targetScreen"/>
    /// — the screen the saved <c>MonitorKey</c> named, or the fallback
    /// primary when it named none currently attached — back into an
    /// absolute position and a DIP size a real <see cref="Avalonia.Controls.Window"/>
    /// can be placed at directly. Converting the size through
    /// <paramref name="targetScreen"/>'s own scaling, rather than reusing
    /// the scaling it was saved at, is what keeps a window saved at 150%
    /// and restored at 100% (or the reverse) neither off-screen nor
    /// shrunk to a degenerate size: its physical footprint is preserved,
    /// not its DIP number.
    /// </summary>
    public static (PixelPoint Position, double Width, double Height) ToAbsolute(
        double relativeX, double relativeY, double physicalWidth, double physicalHeight, ScreenSnapshot targetScreen)
    {
        var scaling = targetScreen.Scaling > 0 ? targetScreen.Scaling : 1.0;

        var position = new PixelPoint(
            targetScreen.WorkingArea.X + (int)Math.Round(relativeX),
            targetScreen.WorkingArea.Y + (int)Math.Round(relativeY));

        return (position, physicalWidth / scaling, physicalHeight / scaling);
    }
}
