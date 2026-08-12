using Avalonia.Controls;
using Avalonia.Layout;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Builds the Quick Access Toolbar (`WP 10.3B`) — a small, always-visible,
/// fixed strip of the platform's own highest-frequency convenience
/// actions, above the Ribbon's own tab strip, exactly like every
/// mainstream ribbon UI's own QAT convention — extracted, `WP 12.0B`
/// (`ADR-0103`), from <see cref="MainWindow"/>'s own previous
/// <c>BuildQuickAccessToolbar</c> member, unmodified in behaviour. A
/// collaborator under `ADR-0103`: a stateless static factory (rule 5's
/// own named "pure construction" shape) — takes every dependency as a
/// parameter, builds and returns the <see cref="StackPanel"/>, holds
/// nothing itself, is never itself constructed. <c>Reset Layout</c> is
/// taken as a delegate, never a direct reference to
/// <c>WorkspaceLayoutPresetCoordinator</c>; Undo/Redo are the plain
/// <see cref="Button"/>s <c>UndoRedoCoordinator</c> already owns, passed
/// once as a value — the composition root's own wiring (`ADR-0103`'s "a
/// collaborator never depends on a sibling collaborator directly" rule).
/// </summary>
internal static class QuickAccessToolbarFactory
{
    /// <summary>Builds the complete Quick Access Toolbar.</summary>
    /// <param name="openGraphViewsByRootId">
    /// The Digital Thread graph's own per-root-object open-tab map — owned
    /// by <see cref="MainWindow"/> (the composition root), shared with
    /// <c>WorkspaceViewCoordinator</c>'s own document-close cleanup: a
    /// genuinely cross-collaborator bridge with no single natural owner
    /// (`ADR-0103`'s own Composition-root Responsibility #4), never a
    /// direct reference between the two collaborators themselves.
    /// </param>
    public static StackPanel Build(
        IWorkspace workspace, EngineeringDomainContext domainContext, Action<Guid, string> navigateToObject,
        StatusBarView statusBar, DocumentAreaView documentArea, CommandPaletteOverlay commandPalette, ThemeService theme,
        Action resetLayout, MacroManagerDialog macroManagerDialog, Button undoButton, Button redoButton,
        Dictionary<Guid, IWorkspaceView> openGraphViewsByRootId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(navigateToObject);
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(commandPalette);
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(resetLayout);
        ArgumentNullException.ThrowIfNull(macroManagerDialog);
        ArgumentNullException.ThrowIfNull(undoButton);
        ArgumentNullException.ThrowIfNull(redoButton);
        ArgumentNullException.ThrowIfNull(openGraphViewsByRootId);

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm, Margin = DesignTokens.PanelHeaderPadding };

        var paletteButton = new Button { Content = "🔎 Command Palette", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(paletteButton, "Search every registered command (Ctrl+K)");
        paletteButton.Click += (_, _) => commandPalette.Open();

        var themeButton = new Button { Content = "🌓 Theme", MinHeight = DesignTokens.MinControlSize };
        themeButton.Click += async (_, _) => await theme.ToggleAsync().ConfigureAwait(true);

        var resetLayoutButton = new Button { Content = "↺ Reset Layout", MinHeight = DesignTokens.MinControlSize };
        resetLayoutButton.Click += (_, _) => resetLayout();

        // The Digital Thread graph (`WP 10.4A`, `ADR-0093`) — opened for
        // the current selection, deduplicated per root object so
        // re-invoking this button on the same object focuses its existing
        // tab rather than opening a second one. Never routed through
        // `INavigationService.OpenAsync` (that path is Kind-factory
        // dispatch for "open this object"; the graph is a cross-cutting
        // Desktop view, not tied to one Kind's own factory) — shown
        // directly via `DocumentAreaView.ShowTab`, the same bypass the
        // Cockpit's own Home tab and `OpenRecentAsync` already use.
        var graphButton = new Button { Content = "🕸 View Relationships", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(graphButton, "Open the Digital Thread graph for the current selection");
        graphButton.Click += (_, _) =>
        {
            var selection = workspace.Selection.Current;
            if (selection is null)
            {
                statusBar.SetText("Select an object first to view its Digital Thread relationships.");
                return;
            }

            if (openGraphViewsByRootId.TryGetValue(selection.ObjectId, out var existingView))
            {
                documentArea.ShowTab(existingView);
                return;
            }

            var graphView = DigitalThread.DigitalThreadGraphView.TryCreate(selection.ObjectId, selection.Kind, domainContext, navigateToObject);
            if (graphView is null)
            {
                statusBar.SetText("No Digital Thread graph is available for the current selection.");
                return;
            }

            graphView.ActionCompleted += message => statusBar.SetText(message);
            openGraphViewsByRootId[selection.ObjectId] = graphView;
            documentArea.ShowTab(graphView);
        };

        // User Command Macros (`WP 10.6A` — "foundation").
        var macrosButton = new Button { Content = "🧩 Macros", MinHeight = DesignTokens.MinControlSize };
        ToolTip.SetTip(macrosButton, "Browse, create, and run Command Macros");
        macrosButton.Click += async (_, _) => await macroManagerDialog.ShowAsync().ConfigureAwait(true);

        bar.Children.Add(paletteButton);
        bar.Children.Add(themeButton);
        bar.Children.Add(resetLayoutButton);
        bar.Children.Add(graphButton);
        bar.Children.Add(undoButton);
        bar.Children.Add(redoButton);
        bar.Children.Add(macrosButton);
        return bar;
    }
}
