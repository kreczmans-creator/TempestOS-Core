using Avalonia;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Tests;

/// <summary>
/// <see cref="MonitorRelativePlacement"/> (`ADR-0153` decision 5) — pure
/// geometry, tested directly rather than through a real multi-monitor,
/// mixed-DPI rig: a headless test run only ever reports one screen at one
/// scaling (the ADR's own risk 2 and risk 3), so the interesting cases —
/// a saved monitor no longer attached, a 150%-scaled save restored at
/// 100% — are exercised as plain function calls instead.
/// </summary>
public sealed class MonitorRelativePlacementTests
{
    private static readonly ScreenSnapshot PrimaryAt100Percent =
        new("PRIMARY@0,0,1920x1080", new PixelRect(0, 0, 1920, 1040), 1.0);

    private static readonly ScreenSnapshot SecondaryAt150Percent =
        new("SECONDARY@1920,0,2560x1440", new PixelRect(1920, 0, 2560, 1400), 1.5);

    // ------------------------------------------------------------
    // Resolving which screen a window is on, or was saved against
    // ------------------------------------------------------------

    [Fact]
    public void ResolveCurrentScreen_FindsTheScreenTheWindowActuallyOverlaps()
    {
        var screens = new SyntheticScreenList([PrimaryAt100Percent, SecondaryAt150Percent], PrimaryAt100Percent);
        var bounds = new PixelRect(2100, 150, 420, 320); // inside the secondary screen

        var resolved = MonitorRelativePlacement.ResolveCurrentScreen(bounds, screens);

        Assert.Equal(SecondaryAt150Percent.Key, resolved.Key);
    }

    [Fact]
    public void ResolveCurrentScreen_FallsBackToPrimary_WhenTheWindowOverlapsNoScreenAtAll()
    {
        var screens = new SyntheticScreenList([PrimaryAt100Percent, SecondaryAt150Percent], PrimaryAt100Percent);
        var bounds = new PixelRect(-50000, -50000, 420, 320);

        var resolved = MonitorRelativePlacement.ResolveCurrentScreen(bounds, screens);

        Assert.Equal(PrimaryAt100Percent.Key, resolved.Key);
    }

    [Fact]
    public void ResolveSavedScreen_FindsTheNamedMonitor_WhenItIsStillAttached()
    {
        var screens = new SyntheticScreenList([PrimaryAt100Percent, SecondaryAt150Percent], PrimaryAt100Percent);

        var resolved = MonitorRelativePlacement.ResolveSavedScreen(SecondaryAt150Percent.Key, screens);

        Assert.Equal(SecondaryAt150Percent.Key, resolved.Key);
    }

    [Fact]
    public void ResolveSavedScreen_FallsBackToPrimary_WhenTheSavedMonitorIsNoLongerAttached()
    {
        // The exact scenario a laptop with the second monitor unplugged
        // hits: a MonitorKey the current screen list has no entry for.
        var screens = new SyntheticScreenList([PrimaryAt100Percent], PrimaryAt100Percent);

        var resolved = MonitorRelativePlacement.ResolveSavedScreen(SecondaryAt150Percent.Key, screens);

        Assert.Equal(PrimaryAt100Percent.Key, resolved.Key);
    }

    [Fact]
    public void ResolveSavedScreen_FallsBackToPrimary_WhenNoMonitorKeyWasEverRecorded()
    {
        var screens = new SyntheticScreenList([PrimaryAt100Percent], PrimaryAt100Percent);

        var resolved = MonitorRelativePlacement.ResolveSavedScreen(null, screens);

        Assert.Equal(PrimaryAt100Percent.Key, resolved.Key);
    }

    // ------------------------------------------------------------
    // The round trip, and its own DPI conversion
    // ------------------------------------------------------------

    [Fact]
    public void ToRelativeThenToAbsolute_OnTheSameScreen_RoundTripsExactly()
    {
        var position = new PixelPoint(2100, 150);
        const double widthDip = 420;
        const double heightDip = 320;

        var (key, x, y, width, height) = MonitorRelativePlacement.ToRelative(position, widthDip, heightDip, SecondaryAt150Percent);
        var (restoredPosition, restoredWidth, restoredHeight) = MonitorRelativePlacement.ToAbsolute(x, y, width, height, SecondaryAt150Percent);

        Assert.Equal(SecondaryAt150Percent.Key, key);
        Assert.Equal(position, restoredPosition);
        Assert.Equal(widthDip, restoredWidth, precision: 6);
        Assert.Equal(heightDip, restoredHeight, precision: 6);
    }

    [Fact]
    public void ASizeSavedAt150Percent_RestoredAt100Percent_IsNeitherOffScreenNorDegenerate()
    {
        // ADR-0153 decision 5's own named test: a rectangle saved on a
        // 150%-scaled monitor, restored after the window's own saved
        // monitor is gone and only a 100%-scaled primary remains.
        var position = new PixelPoint(2100, 150);
        const double widthDip = 420;
        const double heightDip = 320;

        var (_, relativeX, relativeY, physicalWidth, physicalHeight) =
            MonitorRelativePlacement.ToRelative(position, widthDip, heightDip, SecondaryAt150Percent);

        // The physical footprint is preserved across the save.
        Assert.Equal(630, physicalWidth, precision: 6); // 420 * 1.5
        Assert.Equal(480, physicalHeight, precision: 6); // 320 * 1.5

        var (restoredPosition, restoredWidthDip, restoredHeightDip) =
            MonitorRelativePlacement.ToAbsolute(relativeX, relativeY, physicalWidth, physicalHeight, PrimaryAt100Percent);

        // Restored at 100%, the same physical footprint now reads back as
        // a larger DIP size — not the original 420x320, and specifically
        // not zero or negative (degenerate) and not larger than any sane
        // window (off-screen-sized).
        Assert.Equal(630, restoredWidthDip, precision: 6);
        Assert.Equal(480, restoredHeightDip, precision: 6);
        Assert.True(restoredWidthDip > 0 && restoredHeightDip > 0);

        var restoredBounds = new PixelRect(restoredPosition.X, restoredPosition.Y, (int)restoredWidthDip, (int)restoredHeightDip);
        Assert.True(
            PrimaryAt100Percent.WorkingArea.Intersects(restoredBounds),
            $"{restoredBounds} does not land on the primary screen {PrimaryAt100Percent.WorkingArea}.");
    }

    [Fact]
    public void ASizeSavedAt100Percent_RestoredAt150Percent_ShrinksInDipTerms_ButStaysPositive()
    {
        var position = new PixelPoint(300, 150);
        const double widthDip = 600;
        const double heightDip = 480;

        var (_, relativeX, relativeY, physicalWidth, physicalHeight) =
            MonitorRelativePlacement.ToRelative(position, widthDip, heightDip, PrimaryAt100Percent);

        var (_, restoredWidthDip, restoredHeightDip) =
            MonitorRelativePlacement.ToAbsolute(relativeX, relativeY, physicalWidth, physicalHeight, SecondaryAt150Percent);

        Assert.Equal(400, restoredWidthDip, precision: 6); // 600 / 1.5
        Assert.Equal(320, restoredHeightDip, precision: 6); // 480 / 1.5
        Assert.True(restoredWidthDip > 0 && restoredHeightDip > 0);
    }

    [Fact]
    public void ToRelative_MeasuresFromTheScreensOwnWorkingAreaOrigin_NotTheVirtualDesktops()
    {
        var position = new PixelPoint(2020, 100); // 100 px into the secondary screen

        var (_, x, y, _, _) = MonitorRelativePlacement.ToRelative(position, 400, 300, SecondaryAt150Percent);

        Assert.Equal(100, x);
        Assert.Equal(100, y);
    }

    private sealed class SyntheticScreenList : IScreenList
    {
        public SyntheticScreenList(IReadOnlyList<ScreenSnapshot> all, ScreenSnapshot primary)
        {
            All = all;
            Primary = primary;
        }

        public IReadOnlyList<ScreenSnapshot> All { get; }
        public ScreenSnapshot Primary { get; }
    }
}
