using Avalonia;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="FloatingWindowPlacement.Clamp"/> (`WP 20.10D`, PO finding T4)
/// — pure geometry, tested directly rather than through a real
/// <see cref="Avalonia.Controls.Window"/> and its <see cref="Avalonia.Controls.Screens"/>:
/// a headless test run only ever reports one screen, so the interesting
/// multi-monitor and off-screen cases below are exercised as plain function
/// calls instead.
/// </summary>
public sealed class FloatingWindowPlacementTests
{
    private static readonly PixelRect Primary = new(0, 0, 1920, 1080);
    private static readonly PixelRect Secondary = new(1920, 0, 1920, 1080);

    [Fact]
    public void ARequestFullyOnScreen_IsReturnedUnchanged()
    {
        var requested = new PixelRect(300, 200, 420, 320);

        var clamped = FloatingWindowPlacement.Clamp(requested, [Primary, Secondary], Primary);

        Assert.Equal(requested, clamped);
    }

    [Fact]
    public void ARequestOnTheSecondMonitor_StaysThereRatherThanJumpingToThePrimary()
    {
        var requested = new PixelRect(2100, 150, 420, 320);

        var clamped = FloatingWindowPlacement.Clamp(requested, [Primary, Secondary], Primary);

        Assert.Equal(requested, clamped);
    }

    [Fact]
    public void ARequestSpillingPastAnEdge_IsPulledFullyInsideTheScreenItOverlaps()
    {
        // Mostly on the primary screen, but its right edge and bottom edge
        // both run off it.
        var requested = new PixelRect(1800, 950, 420, 320);

        var clamped = FloatingWindowPlacement.Clamp(requested, [Primary, Secondary], Primary);

        Assert.True(clamped.X + clamped.Width <= Primary.Right, $"Right edge {clamped.Right} exceeds screen {Primary.Right}.");
        Assert.True(clamped.Y + clamped.Height <= Primary.Bottom, $"Bottom edge {clamped.Bottom} exceeds screen {Primary.Bottom}.");
        Assert.Equal(420, clamped.Width);
        Assert.Equal(320, clamped.Height);
    }

    [Fact]
    public void ARequestOnNoScreenAtAll_IsCentredOnThePrimaryScreen_RatherThanLeftWhereNobodyCanSeeIt()
    {
        // A coordinate no real screen covers — a disconnected second
        // monitor's own old position, or (before `WP 20.10D`) a
        // host-local pixel value mistaken for a screen one.
        var requested = new PixelRect(-50000, -50000, 420, 320);

        var clamped = FloatingWindowPlacement.Clamp(requested, [Primary, Secondary], Primary);

        Assert.True(Primary.Intersects(clamped), $"{clamped} does not land on the primary screen {Primary}.");
        Assert.Equal(420, clamped.Width);
        Assert.Equal(320, clamped.Height);
        // Centred, not merely somewhere on-screen.
        Assert.Equal(Primary.Center.X, clamped.Center.X);
        Assert.Equal(Primary.Center.Y, clamped.Center.Y);
    }

    [Fact]
    public void ARequestLargerThanEveryScreen_IsShrunkToFit_NeverLeftPartlyOffscreen()
    {
        var tinyScreen = new PixelRect(0, 0, 300, 200);
        var requested = new PixelRect(-9000, -9000, 420, 320);

        var clamped = FloatingWindowPlacement.Clamp(requested, [tinyScreen], tinyScreen);

        Assert.True(tinyScreen.Intersects(clamped));
        Assert.True(clamped.Width <= tinyScreen.Width);
        Assert.True(clamped.Height <= tinyScreen.Height);
    }

    [Fact]
    public void HeadlessSingleScreen_ARequestOffThatOneScreen_IsPlacedOnIt()
    {
        // The exact shape a headless Desktop test run sees: one screen,
        // and a request the previous coordinate bug could easily have
        // produced (host-local pixels reused as screen pixels).
        var onlyScreen = new PixelRect(0, 0, 1366, 768);
        var requested = new PixelRect(50, 40, 420, 320); // small, host-local-looking numbers

        var clamped = FloatingWindowPlacement.Clamp(requested, [onlyScreen], onlyScreen);

        // These particular numbers happen to already sit on the one
        // screen, so this proves the "already fine" path leaves them
        // exactly as they were rather than nudging them for no reason.
        Assert.Equal(requested, clamped);
    }
}
