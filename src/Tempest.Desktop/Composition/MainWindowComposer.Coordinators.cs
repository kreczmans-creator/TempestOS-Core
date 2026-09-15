using Avalonia.Controls;
using Tempest.Workspace;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Files;
using Tempest.Desktop.Viewing;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Every coordinator <see cref="MainWindowComposer.BuildCoordinators"/>
/// builds, plus the views (<see cref="CockpitView"/>,
/// <see cref="HomeDashboardView"/>, <see cref="EvidenceWorkspaceView"/>)
/// that cannot themselves be built until <see cref="WorkspaceViewCoordinator"/>
/// exists (`WP 19.2A`; `HomeDashboardView` `WP 19.7B`, the identical
/// reason — its own Favourite rail action opens through
/// <see cref="WorkspaceViewCoordinator.NavigateToObject"/> exactly as
/// <see cref="CockpitView"/>'s own Favourite Projects card always has).
/// </summary>
internal sealed record ComposedCoordinators(
    UndoRedoCoordinator UndoRedo,
    WorkspaceViewCoordinator ViewCoordinator,
    CockpitView CockpitView,
    HomeDashboardView HomeDashboardView,
    WorkspaceDockingComposer DockingComposer,
    AttachmentViewerLauncher AttachmentViewers,
    WorkspaceLayoutPresetCoordinator LayoutPresets,
    EngineeringCalculationCoordinator EngineeringCalculationCoordinator,
    ProjectDeliveryCoordinator ProjectDelivery,
    ProjectGovernanceCoordinator ProjectGovernanceCoordinator);

internal sealed partial class MainWindowComposer
{
    // Continued from MainWindowComposer.cs.

    /// <summary>
    /// Builds every coordinator — Undo/Redo, the Workspace View
    /// coordinator, docking, layout presets, project delivery/governance,
    /// the Engineering Calculation coordinator — and, alongside
    /// <see cref="WorkspaceViewCoordinator"/> once it exists, the one view
    /// that genuinely needs it first: <see cref="CockpitView"/> (its own
    /// Favourite Projects card opens through
    /// <see cref="WorkspaceViewCoordinator.NavigateToObject"/>).
    /// <see cref="EvidenceWorkspaceView"/> moved to <see cref="BuildViews"/>
    /// (`WP 19.7A`) — it never actually depended on this coordinator, and
    /// <see cref="ProjectWorkspaceView"/>'s own new Evidence tab needs it
    /// built before this phase runs.
    /// </summary>
    public ComposedCoordinators BuildCoordinators(WorkspaceHost host, Window window, ComposedViews views, MainWindowCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(callbacks);

        var workspace = views.Workspace;
        var manager = views.Manager;
        var composition = views.Composition;

        // Undo/Redo (`ADR-0103` collaborator #3, `WP 10.6A`/`ADR-0099`) —
        // constructed before WorkspaceViewCoordinator, which needs its own
        // Stack.
        var undoRedo = new UndoRedoCoordinator(views.ActionReporter);

        // `WP 19.0A` (`ADR-0150`): the project Commercial section's own
        // pickers — the real `OrganisationPicker`/`RateCardPicker`
        // overlays `BuildViews` already built, threaded into the Object
        // Editor's own declaration-driven Commercial section exactly as
        // `evidenceSupport` threads Evidence's own pickers into its
        // declared sections. `WP 19.2B`: the same section's own name
        // resolvers, over the identical real `IOrganisationCatalog`/
        // `IRateCardCatalog` the pickers themselves already read from —
        // the Commercial section shows the client's organisation name and
        // the rate card's own code, never the bare record id either
        // stores.
        var commercialSupport = new ProjectCommercialEditorSupport(
            ct => views.OrganisationPicker.PickAsync(ct),
            ct => views.RateCardPicker.PickAsync(ct),
            () => host.SessionPrincipal?.IdentityId,
            async (organisationId, ct) => (await views.OrganisationCatalog.FindAsync(organisationId, ct).ConfigureAwait(false))?.Definition.Name,
            async (pin, ct) => await views.RateCardCatalog.FindAsync(pin.RecordId, ct).ConfigureAwait(false) is { } card
                ? (card.Definition.Code, card.Definition.Name)
                : null);

        // `WP 20.10F` (Product Owner finding D8): the requirement Owner
        // section's own People catalogue and its "Add person…" prompt —
        // threaded through exactly as `commercialSupport` just above.
        var ownerSupport = new RequirementOwnerEditorSupport(views.PersonCatalog, ct => views.PersonAddPrompt.PromptAsync(ct));

        var viewCoordinator = new WorkspaceViewCoordinator(
            workspace, manager, composition.DomainContext, composition.CommandDispatcher, composition.RequirementsService, host.CalculationTemplates,
            views.ExplorerView, views.InspectorView, views.Ribbon, views.StatusBar, views.ToastHost, views.ConfirmationDialog, undoRedo.Stack,
            views.Session.RecentObjects, views.Session.FavouriteObjects, views.OpenGraphViewsByRootId,
            views.DocumentArea, views.ActionReporter,
            workspaceChanges: composition.WorkspaceChanges, declarations: views.KindEditorDeclarations, evidenceSupport: views.EvidenceSupport,
            auditQuery: host.AuditQuery, commercialSupport: commercialSupport, ownerSupport: ownerSupport);

        // Resolves the one remaining construction-order cycle: the
        // Document Area needs the coordinator's own content builder, which
        // needs the coordinator to exist first (`WP 19.2A`;
        // `DocumentAreaView.ContentBuilder`'s own remarks).
        views.DocumentArea.ContentBuilder = viewCoordinator.BuildDocumentContent;
        views.DocumentArea.TabCloseRequested += viewId => _ = viewCoordinator.CloseDocumentAsync(viewId);

        // The Engineering Cockpit (`WP 10.1A`, `ADR-0069`) — the
        // Workspace's own default landing screen, realised as the Document
        // Area's own permanent Home tab.
        var cockpit = workspace.Cockpit;
        var cockpitView = new CockpitView(
            cockpit,
            workspace.Navigation.Areas,
            onContinue: () => cockpit.ContinueAsync(),
            onOpenRecent: async index =>
            {
                var view = await cockpit.OpenRecentAsync(index).ConfigureAwait(true);
                views.DocumentArea.ShowTab(view);
            },
            onOpenCommandPalette: () => views.CommandPalette.Open(),
            onSwitchArea: async areaId =>
            {
                await workspace.Navigation.SwitchAreaAsync(areaId).ConfigureAwait(true);
                await views.ExplorerView.LoadAsync().ConfigureAwait(true);
                callbacks.SetCurrentArea(workspace.Navigation.Areas.FirstOrDefault(a => a.Id == areaId)?.Title);
            },
            favourites: views.Session.FavouriteObjects,
            onOpenFavourite: viewCoordinator.NavigateToObject,
            onOpenRecentlyChanged: async index =>
            {
                var items = cockpit.RecentlyChanged;
                if (index < 1 || index > items.Count)
                    return;

                var item = items[index - 1];
                await callbacks.OpenObjectAsync(item.ObjectId, item.Kind).ConfigureAwait(true);
            }) { WorkspaceChanges = composition.WorkspaceChanges };
        views.DocumentArea.SetHomeTab(cockpitView);
        viewCoordinator.Attach(cockpitView);

        // `WP 19.7B` (Product Owner comment item 6, sheets 1-2): the Home
        // dashboard — the rail's own `ShellArea.Home` destination now,
        // replacing the engineering surface's own "Home renders the
        // identical surface" (`WP 19.2B`) shape (`MainWindow`'s own
        // `_areaRegistry` entry). `CockpitView` above is unchanged and
        // stays reachable exactly where it always was — the Document
        // Area's own permanent tab within the shared engineering surface
        // (standalone Engineering, or a project's own Structure tab) —
        // this view is a new, separate surface, not a replacement for it.
        // Continue/Recent/Favourite/Recently changed reuse the identical
        // `cockpit`/`views.Session.FavouriteObjects` data and open
        // callbacks `cockpitView`'s own equivalent cards already use,
        // rearranged into a right rail rather than reimplemented.
        async Task OpenRecentAsync(int index)
        {
            var view = await cockpit.OpenRecentAsync(index).ConfigureAwait(true);
            views.DocumentArea.ShowTab(view);
        }

        // `openObjectRightUp` here is `callbacks.OpenEvidenceRecordAsync` —
        // despite its name, the shell's own general "open any Kind right
        // up" delegate (every sibling rail view's identical
        // `openObjectRightUp` local in `BuildViews` wraps the same call):
        // it navigates to Engineering *first*, which is what actually
        // makes the opened tab visible. `CockpitView`'s own
        // `callbacks.OpenObjectAsync` (no navigation step) is safe only
        // because `CockpitView` is itself already embedded inside the
        // engineering surface's own Document Area — this view is not.
        var homeDashboardView = new HomeDashboardView(
            views.TasksReadModel, views.ProjectStatusReadModel, views.AccountsReadModel, composition.DomainContext, cockpit,
            views.Session.FavouriteObjects,
            openObjectRightUp: callbacks.OpenEvidenceRecordAsync,
            openTasks: () => _ = OpenTasksAsync(),
            onOpenRecent: OpenRecentAsync,
            onOpenFavourite: viewCoordinator.NavigateToObject,
            onOpenRecentlyChanged: async index =>
            {
                var items = cockpit.RecentlyChanged;
                if (index < 1 || index > items.Count)
                    return;

                var item = items[index - 1];
                await callbacks.OpenObjectAsync(item.ObjectId, item.Kind).ConfigureAwait(true);
            })
        { WorkspaceChanges = composition.WorkspaceChanges };

        async Task OpenTasksAsync()
        {
            await host.ShellNavigator!.GoToModuleAsync(ShellArea.Tasks).ConfigureAwait(true);
            await callbacks.RenderCurrentModuleAsync().ConfigureAwait(true);
        }

        // Panel construction/resize/hide/collapse/pin/flyout wiring
        // (`ADR-0103` collaborator #5, `WP 10.2B`).
        var dockingComposer = new WorkspaceDockingComposer(workspace, views.ExplorerView, views.InspectorView, views.DocumentArea, views.Session.PanelUiState, views.Session.LayoutStore);

        // `TD-80`: the document and drawing viewer. `TD-96`: the same
        // already-registered IAttachmentContentStore every domain write
        // uses, resolved the identical way WorkspaceHost resolves every
        // other Platform Service — so the viewer reads a large attachment
        // as a stream instead of materialising it whole.
        var attachmentContentStore = (Tempest.Core.EngineeringDomain.IAttachmentContentStore)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.IAttachmentContentStore));
        var attachmentViewers = new AttachmentViewerLauncher(dockingComposer.Registry, dockingComposer.Layout, dockingComposer.DocumentPanelId, attachmentContentStore);

        // Opening a document never navigates: the shell stays where it is.
        viewCoordinator.OpenAttachmentAsync = (owner, attachment) =>
            attachmentViewers.OpenAsync(owner, attachment, window.Bounds.Width, window.Bounds.Height);

        // Named layout presets (`ADR-0103` collaborator #7, `WP 10.2B`).
        var layoutPresets = new WorkspaceLayoutPresetCoordinator(dockingComposer.ApplyPreset, dockingComposer.ResetLayout, views.StatusBar);

        // The two project-CRUD collaborators (`WP-G`, `ADR-0103`).
        var projectDelivery = new ProjectDeliveryCoordinator(
            host.ProjectContext!, host.ProjectTaskWorkflow!, host.ProjectTasks!,
            host.ProjectMilestoneWorkflow!, host.ProjectMilestones!,
            views.ProjectWorkspace, views.InputDialog, views.ToastHost, callbacks.RecordHistory);

        var projectGovernanceCoordinator = new ProjectGovernanceCoordinator(
            host.ProjectContext!, host.ProjectGovernanceWorkflow!, host.ProjectGovernance!,
            views.ProjectWorkspace, views.InputDialog, views.ToastHost, callbacks.RecordHistory);

        // The Engineering Calculation surface's own collaborator
        // (`ADR-0103`, the same shape as the two above).
        var engineeringCalculationCoordinator = new EngineeringCalculationCoordinator(host.BracketCalculations!, views.EngineeringCalculation);

        return new ComposedCoordinators(
            undoRedo, viewCoordinator, cockpitView, homeDashboardView, dockingComposer, attachmentViewers, layoutPresets,
            engineeringCalculationCoordinator, projectDelivery, projectGovernanceCoordinator);
    }
}
