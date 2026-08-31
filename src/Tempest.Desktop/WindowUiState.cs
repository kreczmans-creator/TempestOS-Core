using Tempest.Core.Logging;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Tempest.Core.Settings;

namespace Tempest.Desktop;

/// <summary>
/// Persists <see cref="MainWindow"/>'s own size, position, and Maximised
/// state (`WP 10.5B` scope: "remembered window size, remembered window
/// position, remembered maximised state") — the identical
/// <see cref="ISettingsProvider"/> substrate <see cref="Docking.DesktopPanelUiState"/>/
/// <see cref="UserSettings"/> already use, under a fourth, sibling key.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multi-monitor support, honestly scoped</b> (`WP 10.5B` scope:
/// "multiple monitor support"): <see cref="ClampToVisibleScreen"/> is
/// the real, working piece — if the persisted position no longer falls
/// within any currently-connected screen's own bounds (a monitor was
/// disconnected, or the saved position predates a resolution change),
/// the window centres on the primary screen instead of restoring
/// off-screen and unreachable. This is the one multi-monitor scenario
/// this Work Package actually verified (no physical multi-monitor rig
/// exists in this environment — disclosed directly, `WP10.5B
/// Accessibility Review.md`); remembering *which* monitor a window was
/// on for a genuine multi-monitor *placement* feature remains disclosed
/// future work.
/// </para>
/// </remarks>
internal sealed class WindowUiState
{
    /// <summary>The <see cref="ISettingDefinition.Key"/> this state is stored under.</summary>
    public const string SettingKey = "Desktop.WindowUiState";

    private readonly ISettingsProvider _settingsProvider;
    private readonly SettingsDocument<WindowUiStateDto> _document;

    /// <summary>Initialises a new instance of the <see cref="WindowUiState"/> class with every value at its own documented default.</summary>
    public WindowUiState(ISettingsProvider settingsProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);
        _settingsProvider = settingsProvider;

        _document = new SettingsDocument<WindowUiStateDto>(settingsProvider, SettingKey, "Desktop Window UI State", logger);
    }

    /// <summary>Gets or sets the window's own last known X position, or <see langword="null"/> if never saved (first run — the platform's own default centring applies).</summary>
    public double? X { get; set; }

    /// <summary>Gets or sets the window's own last known Y position.</summary>
    public double? Y { get; set; }

    /// <summary>Gets or sets the window's own last known width.</summary>
    public double Width { get; set; } = 1280;

    /// <summary>Gets or sets the window's own last known height.</summary>
    public double Height { get; set; } = 800;

    /// <summary>Gets or sets whether the window was Maximised.</summary>
    public bool IsMaximised { get; set; }

    /// <summary>Writes the current state via <see cref="ISettingsProvider.SetValueAsync"/>.</summary>
    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        var dto = new WindowUiStateDto(X, Y, Width, Height, IsMaximised);
        await _document.SaveAsync(dto, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads persisted state via <see cref="ISettingsProvider.GetValueAsync"/>. A missing/first-run value leaves every property at its own documented default — never an exception.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var dto = await _document.LoadAsync(cancellationToken).ConfigureAwait(false);

        if (dto is null)
            return;

        X = dto.X;
        Y = dto.Y;
        Width = dto.Width;
        Height = dto.Height;
        IsMaximised = dto.IsMaximised;
    }

    /// <summary>
    /// Captures <paramref name="window"/>'s own current, real bounds and
    /// state into this instance — called just before <see cref="SaveAsync"/>,
    /// never during a live resize (no per-frame write).
    /// </summary>
    public void CaptureFrom(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        IsMaximised = window.WindowState == WindowState.Maximized;

        // While maximised, Position/Width/Height reflect the maximised
        // bounds, not the real "restore" geometry a future normal-state
        // launch should reuse — captured only when not maximised, exactly
        // mirroring every mainstream desktop application's own restore
        // behaviour (maximise on next launch, then let the user un-
        // maximise back to their own last real size).
        if (IsMaximised)
            return;

        X = window.Position.X;
        Y = window.Position.Y;
        Width = window.Width;
        Height = window.Height;
    }

    /// <summary>
    /// Applies this instance's own persisted geometry to
    /// <paramref name="window"/>, clamped to fall within some
    /// currently-connected screen's own bounds (see class remarks) —
    /// called once, before the window is shown.
    /// </summary>
    public void ApplyTo(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.Width = Width;
        window.Height = Height;

        if (X is { } x && Y is { } y)
        {
            var position = new PixelPoint((int)x, (int)y);
            window.Position = ClampToVisibleScreen(window, position);
        }

        if (IsMaximised)
            window.WindowState = WindowState.Maximized;
    }

    /// <summary>
    /// Returns <paramref name="position"/> unchanged if it falls within
    /// any of <paramref name="window"/>'s own currently-known
    /// <see cref="Window.Screens"/>; otherwise returns a position
    /// centring the window on the primary screen (or <see cref="PixelPoint.Origin"/>
    /// if no screen information is available at all — a genuinely
    /// headless/first-run edge case, never an exception).
    /// </summary>
    internal static PixelPoint ClampToVisibleScreen(Window window, PixelPoint position)
    {
        var screens = window.Screens?.All;
        if (screens is null || screens.Count == 0)
            return position;

        foreach (var screen in screens)
        {
            if (screen.Bounds.Contains(position))
                return position;
        }

        var primary = window.Screens!.Primary ?? screens[0];
        var bounds = primary.WorkingArea;
        var centredX = bounds.X + (bounds.Width - (int)window.Width) / 2;
        var centredY = bounds.Y + (bounds.Height - (int)window.Height) / 2;
        return new PixelPoint(centredX, centredY);
    }

    /// <summary>The plain, JSON-serializable shape this class persists.</summary>
    private sealed record WindowUiStateDto(double? X, double? Y, double Width, double Height, bool IsMaximised);
}
