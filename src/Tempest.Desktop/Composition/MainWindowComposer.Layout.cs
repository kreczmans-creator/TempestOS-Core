using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// The root visual tree <see cref="MainWindowComposer.Layout"/> assembles —
/// what <see cref="MainWindow"/> assigns to its own <c>Content</c>, plus
/// the one further collaborator (<see cref="EngineeringSurface"/>) that
/// only exists once this phase has built it, and that
/// <c>RenderCurrentModuleAsync</c> needs as a field.
/// </summary>
internal sealed record ComposedLayout(Control Content, Control EngineeringSurface, DockPanel Dock);

internal sealed partial class MainWindowComposer
{
    // Continued from MainWindowComposer.cs / .Coordinators.cs / .Wire.cs.

    /// <summary>
    /// Assembles the root grid — command row, ribbon, docking workspace,
    /// brand header, status bar, navigation rail, module host, and every
    /// overlay (Busy, Command Palette, the six modal dialogs, Toasts) —
    /// and installs the one real Tab-trap covering all of them (`WP
    /// 16.5A`, `TD-83`).
    /// </summary>
    public ComposedLayout Layout(WorkspaceHost host, Window window, ComposedViews views, ComposedCoordinators coordinators, MainWindowCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(coordinators);
        ArgumentNullException.ThrowIfNull(callbacks);

        var workspace = views.Workspace;
        var composition = views.Composition;
        var navigator = host.ShellNavigator!;

        // `WP 19.2B`: the menu's own "Preferences..." item now navigates
        // to the Settings rail area instead of opening the retired
        // Preferences dialog — a real move through the same navigator
        // every rail button uses, not a second, dialog-shaped mechanism.
        async Task NavigateToSettingsAsync()
        {
            await navigator.GoToModuleAsync(ShellArea.Settings).ConfigureAwait(true);
            await callbacks.RenderCurrentModuleAsync().ConfigureAwait(true);
        }

        // Menu System / Quick Access Toolbar (`ADR-0103` collaborators #8
        // — stateless build functions, `WP 10.0B`/`WP 10.3B`).
        var menu = MainMenuFactory.Build(
            workspace, coordinators.DockingComposer.Layout,
            coordinators.DockingComposer.ExplorerPanelId, coordinators.DockingComposer.InspectorPanelId, coordinators.DockingComposer.OutputPanelId,
            views.Session.PanelUiState, coordinators.DockingComposer.OutputPanel, coordinators.DockingComposer.OutputView, views.Diagnostics,
            views.Theme, () => _ = NavigateToSettingsAsync(), views.MessageDialog, views.CommandPalette, views.DocumentArea, views.Ribbon,
            coordinators.LayoutPresets.Apply, coordinators.LayoutPresets.Reset);
        var quickAccessToolbar = QuickAccessToolbarFactory.Build(
            workspace, composition.DomainContext, coordinators.ViewCoordinator.NavigateToObject, views.StatusBar, views.DocumentArea,
            coordinators.LayoutPresets.Reset, views.MacroManagerDialog, coordinators.UndoRedo.UndoButton, coordinators.UndoRedo.RedoButton,
            views.OpenGraphViewsByRootId);

        // One command row, not two: the menu on the left and the Quick
        // Access Toolbar on the right share a single strip above the
        // Ribbon.
        var commandRow = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(quickAccessToolbar, Dock.Right);
        menu.VerticalAlignment = VerticalAlignment.Center;
        commandRow.Children.Add(quickAccessToolbar);
        commandRow.Children.Add(menu);
        var commandRowFrame = new Border { Child = commandRow, BorderThickness = new Thickness(0, 0, 0, 1) };
        ThemeReactiveBrush.Bind(commandRowFrame, Border.BorderBrushProperty, BrandPalette.HairlineBrushKey);
        ThemeReactiveBrush.Bind(commandRowFrame, Border.BackgroundProperty, BrandPalette.SurfaceBackgroundBrushKey);

        var topStack = new StackPanel();
        topStack.Children.Add(commandRowFrame);
        topStack.Children.Add(views.Ribbon);

        // ---- The Product Spine's own shell composition (`TD-84`) ----
        var engineeringStack = new DockPanel();
        DockPanel.SetDock(topStack, Dock.Top);
        engineeringStack.Children.Add(topStack);
        engineeringStack.Children.Add(coordinators.DockingComposer.View);
        Control engineeringSurface = engineeringStack;

        // `WP 19.2B`: the Structure tab's own placeholder
        // (`views.ProjectWorkspace`'s own `_structureHost`) is deliberately
        // left empty here — `engineeringSurface` is a single control that
        // can only ever be parented in one place, and the switch just
        // below may still hand it directly to `views.ModuleHost` for this
        // very first render (Home, or a persisted standalone-Engineering
        // location). `MainWindow.RenderCurrentModuleAsync`'s own
        // `ResolveEngineeringSurfaceHost`, which the `Opened` handler runs
        // immediately after this constructor returns, is the one place
        // that moves it into the Structure tab — never here, where doing
        // so could hand it to both places at once and throw.

        // The shell carries its module surface from construction, not from
        // a later window event. `RenderCurrentModuleAsync` (`Opened`)
        // supersedes this immediately, including for Home and Engineering
        // — see that method's own remarks; this switch only has to be a
        // reasonable placeholder for the instant between construction and
        // that first render.
        views.ModuleHost.Content = navigator.Current.Area switch
        {
            ShellArea.Projects => views.ProjectBrowser,
            ShellArea.ProjectWorkspace => views.ProjectWorkspace,
            ShellArea.EngineeringCalculation => views.EngineeringCalculation,
            ShellArea.Evidence => coordinators.EvidenceWorkspace,
            ShellArea.Timesheets => views.TimesheetWeekView,
            ShellArea.Invoicing => views.InvoicingView,
            ShellArea.Reports => views.ReportsView,
            ShellArea.Settings => views.SettingsView,
            _ => engineeringSurface,
        };

        var shell = new DockPanel();
        DockPanel.SetDock(views.NavigationRail, Dock.Left);
        shell.Children.Add(views.NavigationRail);
        shell.Children.Add(views.ModuleHost);

        var dock = new DockPanel();
        DockPanel.SetDock(views.Header, Dock.Top);
        DockPanel.SetDock(views.StatusBar, Dock.Bottom);
        dock.Children.Add(views.Header);
        dock.Children.Add(views.StatusBar);
        dock.Children.Add(shell);

        // `WP 10.5A`'s own three new overlay surfaces — added last, so
        // each renders above every other root child (Grid Z-order follows
        // Children order for overlapping siblings).
        var root = new Grid();
        root.Children.Add(dock);
        root.Children.Add(views.BusyOverlay);
        root.Children.Add(views.CommandPalette);
        root.Children.Add(views.ConfirmationDialog);
        root.Children.Add(views.InputDialog);
        root.Children.Add(views.MessageDialog);
        root.Children.Add(views.MacroManagerDialog);
        root.Children.Add(views.CitationPicker);
        root.Children.Add(views.SubjectPicker);
        root.Children.Add(views.DeclaredFigureEntry);
        root.Children.Add(views.CheckEntry);
        root.Children.Add(views.IssueEntry);
        root.Children.Add(views.ReviseReferenceRecordEntry);
        root.Children.Add(views.OrganisationPicker);
        root.Children.Add(views.RateCardPicker);
        root.Children.Add(views.TimesheetEntryPrompt);
        root.Children.Add(views.DeliverableCompletionPrompt);
        root.Children.Add(views.ToastHost);

        // `WP 16.5A` — `TD-83`: while any dialog/the palette is open, Tab
        // must never reach the shell content behind it. Each of the six
        // overlays already flips its own `IsVisible` as its open/close
        // signal.
        var modalCount = 0;
        var dockTabNavigationBeforeModal = default(KeyboardNavigationMode);

        void TrackModal(Border modal)
        {
            modal.PropertyChanged += (_, e) =>
            {
                if (e.Property != Visual.IsVisibleProperty)
                    return;

                if (modal.IsVisible)
                {
                    if (modalCount == 0)
                    {
                        dockTabNavigationBeforeModal = KeyboardNavigation.GetTabNavigation(dock);
                        KeyboardNavigation.SetTabNavigation(dock, KeyboardNavigationMode.None);
                    }
                    modalCount++;
                }
                else if (modalCount > 0)
                {
                    modalCount--;
                    if (modalCount == 0)
                        KeyboardNavigation.SetTabNavigation(dock, dockTabNavigationBeforeModal);
                }
            };
        }

        foreach (var modal in new Border[]
                 {
                     views.ConfirmationDialog, views.InputDialog, views.MessageDialog, views.MacroManagerDialog, views.CommandPalette,
                     views.CitationPicker, views.SubjectPicker, views.DeclaredFigureEntry, views.CheckEntry, views.IssueEntry, views.ReviseReferenceRecordEntry,
                     views.OrganisationPicker, views.RateCardPicker, views.TimesheetEntryPrompt, views.DeliverableCompletionPrompt,
                 })
            TrackModal(modal);

        return new ComposedLayout(root, engineeringSurface, dock);
    }
}
