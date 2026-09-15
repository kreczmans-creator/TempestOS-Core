using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Tempest.Core.Commands;
using Tempest.Core.Logging;
using Tempest.Workspace.Shell;
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

        // `WP 19.4A`: the menu bar and Quick Access Toolbar — "remove this
        // taskbar, lots of generic bits in here that aren't offering
        // anything or even relevant" (`po-comments.md` #3) — are gone from
        // the engineering surface everywhere it renders (Home, standalone
        // Engineering, and the project Structure tab alike, since all
        // three share the one `engineeringSurface` instance). What still
        // matters is kept reachable, never dropped without a replacement:
        // Undo/Redo already work from Ctrl+Z/Ctrl+Y independent of any
        // button (`MainWindowComposer.Wire.cs`'s own `KeyboardShortcuts`
        // wiring calls `coordinators.UndoRedo.UndoAsync`/`RedoAsync`
        // directly); Theme already has a real control in Settings
        // (`SettingsView`'s own Appearance section); Reset Layout, Theme,
        // Macros and View Relationships each gain the one route that was
        // missing — the Command Palette — registered once per host below,
        // through one small reusable `ShellActionCommand` rather than a
        // new domain command type per action (none of these touch the
        // persistence store).
        RegisterShellActionsOnce(composition.CommandRegistry, composition.CommandDispatcher, () =>
        {
            RegisterShellAction(
                composition.CommandRegistry, "shell.resetLayout", "Reset Layout", "Workspace",
                () =>
                {
                    coordinators.LayoutPresets.Reset();
                    return Task.FromResult(CommandResult.Success("Layout reset to its default arrangement."));
                });

            // `WP 20.10D`, PO finding T4: "dock the Requirements tree
            // beside a requirement's editor ... its disappeared somewhere
            // and broken away — need to review it all." One route to every
            // registered panel regardless of how it was lost — docked,
            // floating (behind the main window, off-screen, or simply not
            // where the user is looking), or hidden entirely — so there is
            // always an answer to "where did my panel go" that does not
            // depend on finding the floating window first. Registered once
            // per panel present when the shell composes; a panel a future
            // Work Package registers after this point (an attachment
            // viewer, say) is a known, disclosed gap — see the brief's own
            // report.
            foreach (var descriptor in coordinators.DockingComposer.Registry.All)
            {
                var panelId = descriptor.Id;
                var title = descriptor.Title;

                RegisterShellAction(
                    composition.CommandRegistry, $"shell.showPanel:{panelId}", $"Show Panel: {title}", "Workspace",
                    () =>
                    {
                        coordinators.DockingComposer.ShowPanel(panelId);
                        return Task.FromResult(CommandResult.Success($"{title} panel shown."));
                    });
            }

            RegisterShellAction(
                composition.CommandRegistry, "shell.toggleTheme", "Toggle Theme", "Workspace",
                async () =>
                {
                    await views.Theme.ToggleAsync().ConfigureAwait(false);
                    return CommandResult.Success("Theme toggled.");
                });

            // `WP 19.10O`: the rail's own manual collapse, reachable from
            // the Command Palette (and Ctrl+B, bound in `Wire`) — a toggle,
            // exactly like Toggle Theme above, so the descriptor's own
            // display name never needs the current state to read sensibly.
            RegisterShellAction(
                composition.CommandRegistry, "shell.toggleNavigationRail", "Collapse navigation", "Workspace",
                () =>
                {
                    views.NavigationRail.ToggleCollapsed();
                    return Task.FromResult(CommandResult.Success(
                        views.NavigationRail.IsCollapsed ? "Navigation rail collapsed." : "Navigation rail expanded."));
                });

            RegisterShellAction(
                composition.CommandRegistry, "shell.openMacros", "Macros", "Workspace",
                async () =>
                {
                    await views.MacroManagerDialog.ShowAsync().ConfigureAwait(false);
                    return CommandResult.Success();
                });

            RegisterShellAction(
                composition.CommandRegistry, "shell.viewRelationships", "View Relationships", "Workspace",
                async () =>
                {
                    var selection = workspace.Selection.Current;
                    if (selection is null)
                    {
                        views.StatusBar.SetText("Select an object first to view its Digital Thread relationships.");
                        return CommandResult.Success("Select an object first to view its Digital Thread relationships.");
                    }

                    if (views.OpenGraphViewsByRootId.TryGetValue(selection.ObjectId, out var existingView))
                    {
                        views.DocumentArea.ShowTab(existingView);
                        return CommandResult.Success("Focused the open Digital Thread graph.", selection.ObjectId, selection.Kind);
                    }

                    var graphView = await DigitalThread.DigitalThreadGraphView
                        .TryCreateAsync(selection.ObjectId, selection.Kind, composition.DomainContext, coordinators.ViewCoordinator.NavigateToObject)
                        .ConfigureAwait(false);
                    if (graphView is null)
                    {
                        views.StatusBar.SetText("No Digital Thread graph is available for the current selection.");
                        return CommandResult.Success("No Digital Thread graph is available for the current selection.");
                    }

                    graphView.ActionCompleted += (message, _) => views.StatusBar.SetText(message);
                    views.OpenGraphViewsByRootId[selection.ObjectId] = graphView;
                    views.DocumentArea.ShowTab(graphView);
                    return CommandResult.Success("Opened the Digital Thread graph.", selection.ObjectId, selection.Kind);
                });
        });

        // `WP 19.4A` (scope item 4, `po-comments.md` #3): inside the
        // engineering surface — the only place `views.Ribbon` ever renders
        // — the category row shows the engineering disciplines only.
        // Deliverables, Invoicing, Projects and Timesheets commands stay
        // fully registered and still list in the Command Palette; they
        // just do not get a tab here, because their categories are not
        // named in this allow-list. "Projects" is deliberately excluded:
        // its own category is `ProjectCommercialWorkspaceRegistration`'s
        // client/PO/budget/rate-card/dates/manager commands — commercial
        // project setup, not something a calc sheet or drawing cites, and
        // `po-comments.md` #3 itself names Projects alongside Deliverables/
        // Invoicing/Timesheets as belonging to other rail areas.
        views.Ribbon.SetCategoryFilter(EngineeringRibbonCategories);

        // ---- The Product Spine's own shell composition (`TD-84`) ----
        // `WP 19.4A`: the Ribbon docks directly at the top now — no
        // `topStack`/command-row wrapper left once the menu/QAT row above
        // it is gone.
        var engineeringStack = new DockPanel();
        DockPanel.SetDock(views.Ribbon, Dock.Top);
        engineeringStack.Children.Add(views.Ribbon);
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
            // `WP 19.7A`: the rail's own five areas.
            ShellArea.Projects => views.ProjectsAreaView,
            ShellArea.Tasks => views.TasksAreaView,
            ShellArea.EngineeringDepartment => views.EngineeringAreaView,
            ShellArea.Business => views.BusinessAreaView,
            ShellArea.ProjectWorkspace => views.ProjectWorkspace,
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
        root.Children.Add(views.ObjectPickerDialog);
        root.Children.Add(views.DeclaredFigureEntry);
        root.Children.Add(views.CheckEntry);
        root.Children.Add(views.IssueEntry);
        root.Children.Add(views.ReviseReferenceRecordEntry);
        root.Children.Add(views.OrganisationPicker);
        root.Children.Add(views.RateCardPicker);
        root.Children.Add(views.TimesheetEntryPrompt);
        root.Children.Add(views.DeliverableCompletionPrompt);
        root.Children.Add(views.NewProjectPrompt);
        root.Children.Add(views.ProjectPicker);
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
                     views.CitationPicker, views.SubjectPicker, views.ObjectPickerDialog, views.DeclaredFigureEntry, views.CheckEntry, views.IssueEntry, views.ReviseReferenceRecordEntry,
                     views.OrganisationPicker, views.RateCardPicker, views.TimesheetEntryPrompt, views.DeliverableCompletionPrompt,
                     views.NewProjectPrompt, views.ProjectPicker,
                 })
            TrackModal(modal);

        // `TD-154`: the CI `linux-launch-smoke` job's own late-startup
        // marker used to be `TempestHost.EnterRunning`'s "Host -> Running."
        // line, which fires deep inside `WorkspaceHost.StartAsync` - before
        // `RegisterEngineeringDisciplines` even runs, let alone this
        // composer - so the smoke job proved only that the Runtime Host's
        // hosted-service pipeline started, never that the Desktop shell
        // itself composed. This is the true end of Desktop composition:
        // every view, dialog, overlay and the docking workspace this
        // method assembles already exists by this line, on the identical
        // construction path (`new MainWindow(host)`) every Desktop test in
        // this suite already drives - unlike `App.OnFrameworkInitializationCompleted`'s
        // own later `desktop.MainWindow = window;`, which no test in this
        // suite exercises (`NoBlockingPersistenceCallsTests`'s own remarks
        // name `App.cs` as running entirely pre-dispatcher-loop, excepted
        // by file rather than driven directly) and which does nothing more
        // than assign this already-fully-built window to a property - an
        // inert set, not further composition.
        var composedLogger = (ILogger)host.Services!.GetService(typeof(ILogger));
        composedLogger.Information("Desktop -> Composed.");

        return new ComposedLayout(root, engineeringSurface, dock);
    }

    /// <summary>
    /// Registers every shell action's own <see cref="CommandDescriptor"/>
    /// (and the one shared <see cref="ShellActionCommandHandler"/> they
    /// dispatch through) exactly once per host, never once per
    /// <see cref="MainWindow"/> (`WP 19.4A`).
    /// </summary>
    /// <remarks>
    /// <paramref name="registry"/> and <paramref name="dispatcher"/> are
    /// resolved from <c>host.Services</c> — a per-host singleton pair,
    /// unlike <see cref="Layout"/> itself, which runs again for every new
    /// <see cref="MainWindow"/> the same host constructs (a relaunch/
    /// reopen simulation, <c>ProductSpineAcceptanceTests.Journey_CloseAndReopen_RecoversProjectAndLocation</c>
    /// and others in this suite). An unconditional
    /// <see cref="ICommandRegistry.RegisterDescriptor"/> the second time
    /// would throw <see cref="DuplicateCommandIdException"/>; this guard
    /// is what every other command registration in this platform gets for
    /// free by running once at host start-up
    /// (<c>XxxWorkspaceRegistration.Register</c>) rather than from
    /// <see cref="MainWindowComposer"/>, which has no such single
    /// entry point of its own to move this into.
    /// </remarks>
    private static void RegisterShellActionsOnce(ICommandRegistry registry, ICommandDispatcher dispatcher, Action registerAll)
    {
        if (registry.Items.Any(d => d.Id == "shell.resetLayout"))
            return;

        dispatcher.RegisterHandler<ShellActionCommand>(new ShellActionCommandHandler());
        registerAll();
    }

    /// <summary>One shell action's own <see cref="CommandDescriptor"/>, built around <paramref name="action"/> (`WP 19.4A`).</summary>
    private static void RegisterShellAction(ICommandRegistry registry, string id, string displayName, string category, Func<Task<CommandResult>> action) =>
        registry.RegisterDescriptor(new CommandDescriptor(id, displayName, category: category, createDefault: () => new ShellActionCommand { Action = action }));

    /// <summary>
    /// The engineering-discipline <see cref="CommandDescriptor.Category"/>
    /// values <see cref="RibbonView.SetCategoryFilter"/> allow-lists inside
    /// the engineering surface (`WP 19.4A`) — the seven real discipline
    /// registrations (<c>CalculationsWorkspaceRegistration</c>,
    /// <c>DocumentsWorkspaceRegistration</c>,
    /// <c>EvidenceWorkspaceRegistration</c>,
    /// <c>ManufacturingWorkspaceRegistration</c>,
    /// <c>MechanicalWorkspaceRegistration</c>,
    /// <c>RequirementsWorkspaceRegistration</c>,
    /// <c>VerificationWorkspaceRegistration</c>), an exact, exhaustive
    /// list — not "everything except Deliverables/Invoicing/Timesheets/
    /// Projects/Quotations" — so a future business-scoped category joins
    /// the excluded set automatically, with no second edit here.
    /// </summary>
    private static readonly HashSet<string> EngineeringRibbonCategories = new(StringComparer.Ordinal)
    {
        "Calculations", "Documents", "Evidence", "Manufacturing", "Mechanical", "Requirements", "Verification",
    };
}
