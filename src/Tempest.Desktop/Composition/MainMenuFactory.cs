using Avalonia.Controls;
using Tempest.App.Workspace;
using Tempest.Core.Diagnostics;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

using Tempest.App.Workspace.Layout;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Builds the Menu System — View/Layout/Theme/Help/Commands/Document,
/// each item dispatching an existing capability directly, never a
/// UI-only affordance with no backing behaviour — extracted, `WP 12.0B`
/// (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// <c>BuildMenuSystem</c> member, unmodified in behaviour. A collaborator
/// under `ADR-0103`: a stateless static factory (rule 5's own named
/// "pure construction" shape) — takes every dependency as a parameter,
/// builds and returns the <see cref="Menu"/>, holds nothing, is never
/// itself constructed. The two Layout preset actions are taken as
/// delegates, never a direct reference to
/// <c>WorkspaceLayoutPresetCoordinator</c> — the composition root's own
/// wiring (`ADR-0103`'s "a collaborator never depends on a sibling
/// collaborator directly" rule).
/// </summary>
internal static class MainMenuFactory
{
    /// <summary>Builds the complete Menu System.</summary>
    public static Menu Build(
        IWorkspace workspace, WorkspaceLayoutController layout,
        Guid explorerPanelId, Guid inspectorPanelId, Guid outputPanelId,
        DesktopPanelUiState uiState, OutputPanel outputPanel, OutputPanelView outputView, IDiagnosticsProvider diagnostics,
        ThemeService theme, SettingsDialog settingsDialog, MessageDialog messageDialog, CommandPaletteOverlay commandPalette, DocumentAreaView documentArea,
        RibbonView ribbon, Action<WorkspaceLayoutPreset> applyPreset, Action resetLayout)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(uiState);
        ArgumentNullException.ThrowIfNull(outputPanel);
        ArgumentNullException.ThrowIfNull(outputView);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settingsDialog);
        ArgumentNullException.ThrowIfNull(messageDialog);
        ArgumentNullException.ThrowIfNull(commandPalette);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(ribbon);
        ArgumentNullException.ThrowIfNull(applyPreset);
        ArgumentNullException.ThrowIfNull(resetLayout);

        var view = new MenuItem { Header = "_View" };

        // Showing and hiding a panel is now "is it in the arrangement",
        // and restoring one puts it back on the edge it belongs to
        // (`TD-72`) — there is no zero-width dock to toggle any more.
        var toggleExplorer = new MenuItem { Header = "Project Explorer" };
        toggleExplorer.Click += (_, _) =>
        {
            layout.TogglePanel(explorerPanelId, DockRelation.Left);
            workspace.Layout.SetPlacement(
                workspace.ProjectExplorer.Id,
                workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = layout.IsPanelVisible(explorerPanelId) });
        };

        var toggleInspector = new MenuItem { Header = "Property Inspector" };
        toggleInspector.Click += (_, _) =>
        {
            layout.TogglePanel(inspectorPanelId, DockRelation.Right);
            workspace.Layout.SetPlacement(
                workspace.PropertyInspector.Id,
                workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { IsVisible = layout.IsPanelVisible(inspectorPanelId) });
        };

        var toggleOutput = new MenuItem { Header = "Output Panel" };
        toggleOutput.Click += async (_, _) =>
        {
            layout.TogglePanel(outputPanelId, DockRelation.Below);
            var visible = layout.IsPanelVisible(outputPanelId);
            uiState.OutputVisible = visible;
            if (visible)
                await outputPanel.ShowAsync().ConfigureAwait(true);
            else
                await outputPanel.HideAsync().ConfigureAwait(true);
            if (visible)
                outputView.Refresh(diagnostics);
        };

        // `TD-70` — the ribbon's own minimise affordance, reachable from
        // the menu as well as by double-clicking a tab, so it is
        // discoverable rather than a hidden gesture.
        var toggleRibbon = new MenuItem { Header = "Minimise Ribbon" };
        toggleRibbon.Click += (_, _) => ribbon.ToggleCollapsed();

        view.Items.Add(toggleExplorer);
        view.Items.Add(toggleInspector);
        view.Items.Add(toggleOutput);
        view.Items.Add(toggleRibbon);
        view.Items.Add(new Separator());

        var layoutMenu = new MenuItem { Header = "_Layout" };
        layoutMenu.Items.Add(BuildLayoutPresetItem("Engineering", WorkspaceLayoutPreset.Engineering, applyPreset));
        layoutMenu.Items.Add(BuildLayoutPresetItem("Review", WorkspaceLayoutPreset.Review, applyPreset));
        layoutMenu.Items.Add(BuildLayoutPresetItem("Documentation", WorkspaceLayoutPreset.Documentation, applyPreset));
        layoutMenu.Items.Add(new Separator());
        // `WP-Z4` Productisation Phase 1 (P1) — a first icon pass on the
        // Menu System: the items with an existing IconGeometry glyph that
        // names them exactly now carry it, rather than every menu in the
        // shell being bare text next to a fully iconised Ribbon/QAT.
        var resetLayoutItem = new MenuItem { Header = "Reset Layout", Icon = IconGeometry.Build(IconGeometry.LayoutReset, 14) };
        resetLayoutItem.Click += (_, _) => resetLayout();
        layoutMenu.Items.Add(resetLayoutItem);
        view.Items.Add(layoutMenu);

        var themeMenu = new MenuItem { Header = "_Theme" };
        var toggleTheme = new MenuItem { Header = "Toggle Light/Dark", Icon = IconGeometry.Build(IconGeometry.Theme, 14) };
        toggleTheme.Click += async (_, _) => await theme.ToggleAsync().ConfigureAwait(true);
        themeMenu.Items.Add(toggleTheme);
        themeMenu.Items.Add(new Separator());
        var preferences = new MenuItem { Header = "Preferences...", Icon = IconGeometry.Build(IconGeometry.Gear, 14) };
        preferences.Click += async (_, _) => await settingsDialog.ShowAsync().ConfigureAwait(true);
        themeMenu.Items.Add(preferences);

        var help = new MenuItem { Header = "_Help" };
        var about = new MenuItem { Header = "About TempestOS..." };
        about.Click += async (_, _) =>
        {
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            await messageDialog.ShowAsync(
                FeedbackSeverity.Info,
                "About TempestOS",
                $"TempestOS Engineering Workspace\nVersion {version}\n\nA Claude-developed engineering platform.").ConfigureAwait(true);
        };
        help.Items.Add(about);

        var commands = new MenuItem { Header = "_Commands" };
        var openPalette = new MenuItem { Header = "Command Palette...   (Ctrl+K)", Icon = IconGeometry.Build(IconGeometry.Command, 14) };
        openPalette.Click += (_, _) => commandPalette.Open();
        commands.Items.Add(openPalette);

        var document = new MenuItem { Header = "_Document" };
        var nextDoc = new MenuItem { Header = "Next Tab   (Ctrl+Tab)" };
        nextDoc.Click += (_, _) => documentArea.SelectNextTab();
        var prevDoc = new MenuItem { Header = "Previous Tab   (Ctrl+Shift+Tab)" };
        prevDoc.Click += (_, _) => documentArea.SelectPreviousTab();
        document.Items.Add(nextDoc);
        document.Items.Add(prevDoc);

        return new Menu { ItemsSource = new[] { view, document, themeMenu, commands, help } };
    }

    /// <summary>Builds one <c>_Layout</c> submenu item applying <paramref name="preset"/> via <paramref name="applyPreset"/>.</summary>
    private static MenuItem BuildLayoutPresetItem(string header, WorkspaceLayoutPreset preset, Action<WorkspaceLayoutPreset> applyPreset)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => applyPreset(preset);
        return item;
    }
}
