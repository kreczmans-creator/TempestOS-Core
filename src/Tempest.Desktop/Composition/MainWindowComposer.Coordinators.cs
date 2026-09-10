using Avalonia.Controls;
using Tempest.Workspace;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Shell;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Files;
using Tempest.Desktop.Viewing;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Every coordinator <see cref="MainWindowComposer.BuildCoordinators"/>
/// builds, plus the two views (<see cref="CockpitView"/>,
/// <see cref="EvidenceWorkspaceView"/>) that cannot themselves be built
/// until <see cref="WorkspaceViewCoordinator"/> exists (`WP 19.2A`).
/// </summary>
internal sealed record ComposedCoordinators(
    UndoRedoCoordinator UndoRedo,
    WorkspaceViewCoordinator ViewCoordinator,
    CockpitView CockpitView,
    WorkspaceDockingComposer DockingComposer,
    AttachmentViewerLauncher AttachmentViewers,
    WorkspaceLayoutPresetCoordinator LayoutPresets,
    EngineeringCalculationCoordinator EngineeringCalculationCoordinator,
    ProjectDeliveryCoordinator ProjectDelivery,
    ProjectGovernanceCoordinator ProjectGovernanceCoordinator,
    EvidenceWorkspaceView EvidenceWorkspace);

internal sealed partial class MainWindowComposer
{
    // Continued from MainWindowComposer.cs.

    /// <summary>
    /// Builds every coordinator — Undo/Redo, the Workspace View
    /// coordinator, docking, layout presets, project delivery/governance,
    /// the Engineering Calculation coordinator — and, alongside
    /// <see cref="WorkspaceViewCoordinator"/> once it exists, the two views
    /// that genuinely need it first: <see cref="CockpitView"/> (its own
    /// Favourite Projects card opens through
    /// <see cref="WorkspaceViewCoordinator.NavigateToObject"/>) and
    /// <see cref="EvidenceWorkspaceView"/>.
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
            async (rateCardId, ct) => (await views.RateCardCatalog.FindAsync(rateCardId, ct).ConfigureAwait(false))?.Definition.Code);

        var viewCoordinator = new WorkspaceViewCoordinator(
            workspace, manager, composition.DomainContext, composition.CommandDispatcher, composition.RequirementsService, host.CalculationTemplates,
            views.ExplorerView, views.InspectorView, views.Ribbon, views.StatusBar, views.ToastHost, views.ConfirmationDialog, undoRedo.Stack,
            views.Session.RecentObjects, views.Session.FavouriteObjects, views.OpenGraphViewsByRootId,
            views.DocumentArea, views.ActionReporter,
            workspaceChanges: composition.WorkspaceChanges, declarations: views.KindEditorDeclarations, evidenceSupport: views.EvidenceSupport,
            auditQuery: host.AuditQuery, commercialSupport: commercialSupport);

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

        // Panel construction/resize/hide/collapse/pin/flyout wiring
        // (`ADR-0103` collaborator #5, `WP 10.2B`).
        var dockingComposer = new WorkspaceDockingComposer(workspace, views.ExplorerView, views.InspectorView, views.DocumentArea, views.Session.PanelUiState, views.Session.LayoutStore);

        // `TD-80`: the document and drawing viewer.
        var attachmentViewers = new AttachmentViewerLauncher(dockingComposer.Registry, dockingComposer.Layout, dockingComposer.DocumentPanelId);

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

        // The Evidence workspace's own Create flow and the Object Editor's
        // own declared Evidence sections both need a real prompt (`WP 18.2A`).
        var evidenceWorkspace = new EvidenceWorkspaceView(
            composition.DomainContext, composition.CommandDispatcher, views.EvidenceFilePicker,
            () => host.ProjectContext!.Current?.Id, (id, kind) => _ = callbacks.OpenEvidenceRecordAsync(id, kind), views.LibrariesView)
        {
            ParameterPrompt = views.CommandPrompt.Prompt,
            SubjectPrompt = ct => views.SubjectPicker.PickAsync(ct),
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        evidenceWorkspace.ActionCompleted += (message, outcome) => _ = views.ActionReporter.ReportAsync(message, outcome);

        return new ComposedCoordinators(
            undoRedo, viewCoordinator, cockpitView, dockingComposer, attachmentViewers, layoutPresets,
            engineeringCalculationCoordinator, projectDelivery, projectGovernanceCoordinator, evidenceWorkspace);
    }
}
