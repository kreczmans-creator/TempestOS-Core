using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Tempest.App.Workspace;
using Tempest.Core.EngineeringDomain;
using Tempest.Desktop.Icons;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Builds the Quick Access Toolbar (`WP 10.3B`) — a small, always-visible,
/// fixed strip of the Engineering surface's own highest-frequency
/// convenience actions, sharing one row with the menu above the Ribbon —
/// extracted, `WP 12.0B` (`ADR-0103`), from <see cref="MainWindow"/>'s own
/// previous <c>BuildQuickAccessToolbar</c> member. A collaborator under
/// `ADR-0103`: a stateless static factory (rule 5's own named "pure
/// construction" shape) — takes every dependency as a parameter, builds
/// and returns the <see cref="StackPanel"/>, holds nothing itself, is
/// never itself constructed. <c>Reset Layout</c> is taken as a delegate,
/// never a direct reference to <c>WorkspaceLayoutPresetCoordinator</c>;
/// Undo/Redo are the plain <see cref="Button"/>s <c>UndoRedoCoordinator</c>
/// already owns, passed once as a value.
/// </summary>
/// <remarks>
/// Since the Desktop brand alignment the global search / command palette
/// and the theme switch live in the shell header (<see cref="ShellHeaderView"/>),
/// present in every module — so this strip no longer duplicates them and
/// carries only what is specific to working in Engineering. Every button
/// is a monochrome vector icon beside its label (the design system bans
/// emoji), classed <see cref="ChromeStyles.Flat"/>, and named for
/// automation so a test or a screen reader finds it by what it does.
/// </remarks>
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
        StatusBarView statusBar, DocumentAreaView documentArea,
        Action resetLayout, MacroManagerDialog macroManagerDialog, Button undoButton, Button redoButton,
        Dictionary<Guid, IWorkspaceView> openGraphViewsByRootId)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(domainContext);
        ArgumentNullException.ThrowIfNull(navigateToObject);
        ArgumentNullException.ThrowIfNull(statusBar);
        ArgumentNullException.ThrowIfNull(documentArea);
        ArgumentNullException.ThrowIfNull(resetLayout);
        ArgumentNullException.ThrowIfNull(macroManagerDialog);
        ArgumentNullException.ThrowIfNull(undoButton);
        ArgumentNullException.ThrowIfNull(redoButton);
        ArgumentNullException.ThrowIfNull(openGraphViewsByRootId);

        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = DesignTokens.SpaceXs,
            Margin = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceXs),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // The Digital Thread graph (`WP 10.4A`, `ADR-0093`) — opened for
        // the current selection, deduplicated per root object so
        // re-invoking this button on the same object focuses its existing
        // tab rather than opening a second one. Never routed through
        // `INavigationService.OpenAsync` (that path is Kind-factory
        // dispatch for "open this object"; the graph is a cross-cutting
        // Desktop view, not tied to one Kind's own factory) — shown
        // directly via `DocumentAreaView.ShowTab`, the same bypass the
        // Cockpit's own Home tab and `OpenRecentAsync` already use.
        var graphButton = ToolbarButton(IconGeometry.Graph, "View Relationships", "Open the Digital Thread graph for the current selection");
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

            graphView.ActionCompleted += (message, _) => statusBar.SetText(message);
            openGraphViewsByRootId[selection.ObjectId] = graphView;
            documentArea.ShowTab(graphView);
        };

        // User Command Macros (`WP 10.6A` — "foundation").
        var macrosButton = ToolbarButton(IconGeometry.Macro, "Macros", "Browse, create, and run Command Macros");
        macrosButton.Click += async (_, _) => await macroManagerDialog.ShowAsync().ConfigureAwait(true);

        var resetLayoutButton = ToolbarButton(IconGeometry.LayoutReset, "Reset Layout", "Return every panel to the default arrangement");
        resetLayoutButton.Click += (_, _) => resetLayout();

        bar.Children.Add(undoButton);
        bar.Children.Add(redoButton);
        bar.Children.Add(Divider());
        bar.Children.Add(graphButton);
        bar.Children.Add(macrosButton);
        bar.Children.Add(Divider());
        bar.Children.Add(resetLayoutButton);
        return bar;
    }

    /// <summary>One toolbar action — a vector icon beside its label, flat until hovered, named for automation by its label.</summary>
    public static Button ToolbarButton(StreamGeometry icon, string label, string tooltip)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(label);

        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = DesignTokens.SpaceSm + 2, VerticalAlignment = VerticalAlignment.Center };
        content.Children.Add(IconGeometry.Build(icon, 14));
        content.Children.Add(new TextBlock { Text = label, FontSize = DesignTokens.FontSizeBody, VerticalAlignment = VerticalAlignment.Center });

        var button = new Button
        {
            Content = content,
            MinHeight = DesignTokens.MinControlSize,
            Padding = new Avalonia.Thickness(DesignTokens.SpaceMd, DesignTokens.SpaceSm),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Classes.Add(ChromeStyles.Flat);
        AutomationProperties.SetName(button, label);
        ToolTip.SetTip(button, tooltip);
        return button;
    }

    private static Border Divider()
    {
        var line = new Border { Width = 1, Height = 16, Margin = new Avalonia.Thickness(DesignTokens.SpaceSm, 0), VerticalAlignment = VerticalAlignment.Center };
        ThemeReactiveBrush.Bind(line, Border.BackgroundProperty, BrandPalette.HairlineStrongBrushKey);
        return line;
    }
}
