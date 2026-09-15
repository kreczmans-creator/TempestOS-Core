using Avalonia;
using Avalonia.Controls;

namespace Tempest.Desktop.Docking;

/// <summary>
/// One screen, as <see cref="Docking.WorkspaceLayoutController"/> needs it
/// — a snapshot, not a live handle, so it can be compared, persisted and
/// synthesised in a test with no real <see cref="Screens"/> behind it
/// (`ADR-0153` decision 5).
/// </summary>
/// <param name="Key">A best-effort stable identity for this monitor — a display's own name paired with its bounds, since no platform this product ships on guarantees a persistent hardware id.</param>
/// <param name="WorkingArea">The screen's own usable area (excluding taskbars and similar chrome), in physical pixels.</param>
/// <param name="Scaling">This screen's own render scaling — 1.0 at 100%, 1.5 at 150%, and so on.</param>
public readonly record struct ScreenSnapshot(string Key, PixelRect WorkingArea, double Scaling);

/// <summary>
/// Where <see cref="Docking.WorkspaceLayoutController"/> reads screen
/// geometry from (`ADR-0153` decision 5) — real <see cref="Screens"/> in
/// production (<see cref="AvaloniaScreenList"/>), an injected, synthetic
/// list in a test, since Avalonia's headless platform reports exactly one
/// screen (the ADR's own risk 3) and monitor-fallback and per-monitor DPI
/// conversion both need more than that to prove.
/// </summary>
public interface IScreenList
{
    /// <summary>Every screen currently attached.</summary>
    IReadOnlyList<ScreenSnapshot> All { get; }

    /// <summary>
    /// The screen to fall back to when a saved <c>MonitorKey</c> matches
    /// none of <see cref="All"/> — the monitor was unplugged, or this is a
    /// different machine entirely (`ADR-0153` decision 5).
    /// </summary>
    ScreenSnapshot Primary { get; }
}

/// <summary>The real <see cref="IScreenList"/>, over a live <see cref="Window"/>'s own <see cref="Avalonia.Controls.Screens"/>.</summary>
public sealed class AvaloniaScreenList : IScreenList
{
    private readonly Window _window;

    /// <summary>Initialises a new instance of the <see cref="AvaloniaScreenList"/> class.</summary>
    public AvaloniaScreenList(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <inheritdoc />
    public IReadOnlyList<ScreenSnapshot> All =>
        _window.Screens?.All.Select(ToSnapshot).ToList() ?? [];

    /// <inheritdoc />
    public ScreenSnapshot Primary
    {
        get
        {
            var screens = _window.Screens;
            var primary = screens?.Primary ?? screens?.All.FirstOrDefault();
            return primary is null ? DefaultPrimary : ToSnapshot(primary);
        }
    }

    /// <summary>
    /// Used only when this window reports no screens at all (never yet
    /// shown, or a platform that answers nothing) — a sensible, ordinary
    /// desktop so geometry math elsewhere has something to divide by rather
    /// than needing its own null path.
    /// </summary>
    private static readonly ScreenSnapshot DefaultPrimary = new("(no screen reported)", new PixelRect(0, 0, 1920, 1080), 1.0);

    private static ScreenSnapshot ToSnapshot(Avalonia.Platform.Screen screen) =>
        new(MonitorKeyOf(screen), screen.WorkingArea, screen.Scaling);

    /// <summary>`ADR-0153` decision 5's own choice of identity: a display's own name paired with its bounds.</summary>
    public static string MonitorKeyOf(Avalonia.Platform.Screen screen) =>
        $"{screen.DisplayName}@{screen.Bounds.X},{screen.Bounds.Y},{screen.Bounds.Width}x{screen.Bounds.Height}";
}
