using Tempest.Core.Settings;
using Tempest.Desktop;
using Tempest.Desktop.Docking;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Loads every independent, Desktop-local, per-session persisted state
/// <see cref="MainWindow"/> restores on construction — extracted,
/// `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// five independent synchronous state loads, unmodified in behaviour and
/// unmodified in order. A collaborator under `ADR-0103`: constructed once
/// by <see cref="MainWindow"/> (the composition root), declaring only the
/// one dependency it actually needs, never DI-registered, never
/// referencing <see cref="MainWindow"/> or any sibling collaborator back.
/// </summary>
/// <remarks>
/// Loaded synchronously here, exactly as each state's own prior
/// constructor-top load already was — the identical, established
/// discipline this codebase already applies at composition-root
/// construction time (`ADR-0103`'s own "Construction rules" explicitly
/// carries this precedent forward, introducing nothing new), so the very
/// first frame already reflects last session's own state, never a
/// default-then-jump.
/// </remarks>
internal sealed class DesktopSessionState
{
    /// <summary>Gets the restored window geometry (size/position/maximised state).</summary>
    public WindowUiState WindowUiState { get; }

    /// <summary>Gets the restored user preferences (theme, delete-confirmation, toast duration, recent-search capacity).</summary>
    public UserSettings UserSettings { get; }

    /// <summary>Gets the restored Recent Objects list (`WP 10.6A`).</summary>
    public RecentObjectsState RecentObjects { get; }

    /// <summary>Gets the restored Favourite Objects list (`WP 10.6A`).</summary>
    public FavouriteObjectsState FavouriteObjects { get; }

    /// <summary>Gets the restored Desktop-local panel UI state (Collapse/Auto-Hide/Output — `WP 10.2B`).</summary>
    public DesktopPanelUiState PanelUiState { get; }

    /// <summary>Initialises a new instance of the <see cref="DesktopSessionState"/> class, synchronously loading every Desktop-local persisted state from <paramref name="settingsProvider"/>.</summary>
    public DesktopSessionState(ISettingsProvider settingsProvider)
    {
        ArgumentNullException.ThrowIfNull(settingsProvider);

        WindowUiState = new WindowUiState(settingsProvider);
        WindowUiState.LoadAsync().GetAwaiter().GetResult();

        UserSettings = new UserSettings(settingsProvider);
        UserSettings.LoadAsync().GetAwaiter().GetResult();

        // Recent/Favourite Objects (`WP 10.6A`) — loaded synchronously
        // here, the identical established discipline every other
        // persisted Desktop-local state above already uses.
        RecentObjects = new RecentObjectsState(settingsProvider);
        RecentObjects.LoadAsync().GetAwaiter().GetResult();
        FavouriteObjects = new FavouriteObjectsState(settingsProvider);
        FavouriteObjects.LoadAsync().GetAwaiter().GetResult();

        // Desktop-local panel UI state (Collapse/Auto-Hide/Output — `WP
        // 10.2B`) — loaded synchronously here, exactly as
        // `IWorkspaceState`'s own equivalent load already completed
        // synchronously-from-the-composition-root's-perspective inside
        // `host.StartAsync()` before `MainWindow`'s own constructor ever
        // ran (`App.cs` §"Avalonia's own startup path is synchronous"),
        // so the very first frame already reflects last session's own
        // Collapse/Auto-Hide/Output state — "restore previous layout on
        // startup" applied to this Desktop-local, additional state, the
        // same way `ADR-0064` already applies it to the Workspace's own
        // contracted state.
        PanelUiState = new DesktopPanelUiState(settingsProvider);
        PanelUiState.LoadAsync().GetAwaiter().GetResult();
    }
}
