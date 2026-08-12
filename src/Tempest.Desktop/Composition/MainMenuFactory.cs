using Avalonia.Controls;
using Tempest.App.Workspace;
using Tempest.Core.Diagnostics;
using Tempest.Desktop.Docking;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

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
        IWorkspace workspace, PanelHostControl explorerHost, PanelHostControl inspectorHost, PanelHostControl outputHost,
        DockingGrid docking, DesktopPanelUiState uiState, OutputPanel outputPanel, OutputPanelView outputView, IDiagnosticsProvider diagnostics,
        ThemeService theme, SettingsDialog settingsDialog, MessageDialog messageDialog, CommandPaletteOverlay commandPalette, DocumentAreaView documentArea,
        Action<PredefinedLayouts.WorkspaceLayoutPreset> applyPreset, Action resetLayout)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(explorerHost);
        ArgumentNullException.ThrowIfNull(inspectorHost);
        ArgumentNullException.ThrowIfNull(outputHost);
        ArgumentNullException.ThrowIfNull(docking);
        ArgumentNullException.ThrowIfNull(uiState);
        ArgumentNullException.ThrowIfNull(outputPanel);
        ArgumentNullException.ThrowIfNull(outputView);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settingsDialog);
        ArgumentNullException.ThrowIfNull(messageDialog);
        ArgumentNullException.ThrowIfNull(commandPalette);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(applyPreset);
        ArgumentNullException.ThrowIfNull(resetLayout);

        var view = new MenuItem { Header = "_View" };

        var toggleExplorer = new MenuItem { Header = "Project Explorer" };
        toggleExplorer.Click += (_, _) =>
        {
            var visible = !docking.IsLeftVisible;
            docking.SetLeftVisible(visible);
            workspace.Layout.SetPlacement(workspace.ProjectExplorer.Id, workspace.Layout.GetPlacement(workspace.ProjectExplorer.Id) with { IsVisible = visible });
        };

        var toggleInspector = new MenuItem { Header = "Property Inspector" };
        toggleInspector.Click += (_, _) =>
        {
            var visible = !docking.IsRightVisible;
            docking.SetRightVisible(visible);
            workspace.Layout.SetPlacement(workspace.PropertyInspector.Id, workspace.Layout.GetPlacement(workspace.PropertyInspector.Id) with { IsVisible = visible });
        };

        var toggleOutput = new MenuItem { Header = "Output Panel" };
        toggleOutput.Click += async (_, _) =>
        {
            var visible = !docking.IsBottomVisible;
            docking.SetBottomVisible(visible);
            uiState.OutputVisible = visible;
            if (visible)
                await outputPanel.ShowAsync().ConfigureAwait(true);
            else
                await outputPanel.HideAsync().ConfigureAwait(true);
            if (visible)
                outputView.Refresh(diagnostics);
        };

        view.Items.Add(toggleExplorer);
        view.Items.Add(toggleInspector);
        view.Items.Add(toggleOutput);
        view.Items.Add(new Separator());

        var layout = new MenuItem { Header = "_Layout" };
        layout.Items.Add(BuildLayoutPresetItem("Engineering", PredefinedLayouts.WorkspaceLayoutPreset.Engineering, applyPreset));
        layout.Items.Add(BuildLayoutPresetItem("Review", PredefinedLayouts.WorkspaceLayoutPreset.Review, applyPreset));
        layout.Items.Add(BuildLayoutPresetItem("Documentation", PredefinedLayouts.WorkspaceLayoutPreset.Documentation, applyPreset));
        layout.Items.Add(new Separator());
        var resetLayoutItem = new MenuItem { Header = "Reset Layout" };
        resetLayoutItem.Click += (_, _) => resetLayout();
        layout.Items.Add(resetLayoutItem);
        view.Items.Add(layout);

        var themeMenu = new MenuItem { Header = "_Theme" };
        var toggleTheme = new MenuItem { Header = "Toggle Light/Dark" };
        toggleTheme.Click += async (_, _) => await theme.ToggleAsync().ConfigureAwait(true);
        themeMenu.Items.Add(toggleTheme);
        themeMenu.Items.Add(new Separator());
        var preferences = new MenuItem { Header = "Preferences..." };
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
        var openPalette = new MenuItem { Header = "Command Palette...   (Ctrl+K)" };
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
    private static MenuItem BuildLayoutPresetItem(string header, PredefinedLayouts.WorkspaceLayoutPreset preset, Action<PredefinedLayouts.WorkspaceLayoutPreset> applyPreset)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => applyPreset(preset);
        return item;
    }
}
