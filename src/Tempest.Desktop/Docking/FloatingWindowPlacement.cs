using Avalonia;

namespace Tempest.Desktop.Docking;

/// <summary>
/// Keeps a floating window's own screen rectangle honest against the
/// screens that actually exist (`WP 20.10D`, PO finding T4: a panel that
/// floats off-screen — dropped there by a coordinate bug, or restored from
/// a save made on a monitor that is no longer connected — is a panel
/// nobody can ever find again).
/// </summary>
/// <remarks>
/// Deliberately pure geometry, with no <see cref="Avalonia.Controls.Screens"/>
/// or <see cref="Avalonia.Controls.Window"/> dependency, for the same
/// reason <see cref="Tempest.Workspace.Layout.DockTargetResolver"/> is pure:
/// the edge cases worth proving — a rectangle that lands on no screen at
/// all, one that merely spills past an edge — are exactly the ones painful
/// to drive through a real window and trivial to test as a function. A
/// headless test run only ever reports one screen, so the interesting cases
/// here are exercised directly against this function rather than through a
/// simulated multi-monitor <see cref="FloatingPanelWindow"/>.
/// </remarks>
public static class FloatingWindowPlacement
{
    /// <summary>
    /// Returns <paramref name="bounds"/> unchanged when it already
    /// intersects one of <paramref name="screenWorkingAreas"/> — clamped
    /// fully inside whichever one it overlaps when it merely spills past an
    /// edge — or, when it lands on none of them at all, its own size
    /// (shrunk to fit if necessary) centred inside
    /// <paramref name="primaryWorkingArea"/>.
    /// </summary>
    /// <param name="bounds">The window's own requested screen rectangle.</param>
    /// <param name="screenWorkingAreas">Every screen's own working area, in no particular order.</param>
    /// <param name="primaryWorkingArea">Where to place <paramref name="bounds"/> when it lands on none of <paramref name="screenWorkingAreas"/>.</param>
    public static PixelRect Clamp(PixelRect bounds, IReadOnlyList<PixelRect> screenWorkingAreas, PixelRect primaryWorkingArea)
    {
        ArgumentNullException.ThrowIfNull(screenWorkingAreas);

        PixelRect? containingScreen = null;
        foreach (var screen in screenWorkingAreas)
        {
            if (screen.Intersects(bounds))
            {
                containingScreen = screen;
                break;
            }
        }

        var target = containingScreen ?? primaryWorkingArea;
        var width = Math.Min(bounds.Width, target.Width);
        var height = Math.Min(bounds.Height, target.Height);

        if (containingScreen is null)
        {
            // Nowhere near a real screen: centring is the one placement
            // that is never itself off whichever screen the user actually
            // has, regardless of what the original coordinates were.
            var centredX = target.X + Math.Max(0, (target.Width - width) / 2);
            var centredY = target.Y + Math.Max(0, (target.Height - height) / 2);
            return new PixelRect(centredX, centredY, width, height);
        }

        var clampedX = Math.Clamp(bounds.X, target.X, target.X + target.Width - width);
        var clampedY = Math.Clamp(bounds.Y, target.Y, target.Y + target.Height - height);
        return new PixelRect(clampedX, clampedY, width, height);
    }
}
