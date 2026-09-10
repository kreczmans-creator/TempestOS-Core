using Avalonia.Controls;
using Avalonia.Input;
using Tempest.Workspace;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Desktop.Input;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Composition;

internal sealed partial class MainWindowComposer
{
    // Continued from MainWindowComposer.cs / MainWindowComposer.Coordinators.cs.

    /// <summary>
    /// Wires every cross-collaborator event, delegate, palette binding and
    /// keyboard shortcut — everything that makes the views and coordinators
    /// <see cref="BuildViews"/>/<see cref="BuildCoordinators"/> built into
    /// each other actually behave as one shell.
    /// </summary>
    public void Wire(WorkspaceHost host, Window window, ComposedViews views, ComposedCoordinators coordinators, MainWindowCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(coordinators);
        ArgumentNullException.ThrowIfNull(callbacks);

        var workspace = views.Workspace;
        var manager = views.Manager;
        var composition = views.Composition;
        var navigator = host.ShellNavigator!;
        var projectContext = host.ProjectContext!;

        // Click-away: a pointer press landing directly on the Document
        // Area closes any open Auto-Hide flyout.
        views.DocumentArea.PointerPressed += (_, _) =>
        {
            if (coordinators.DockingComposer.IsFlyoutOpen)
                coordinators.DockingComposer.CloseFlyout();
        };
        window.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape && coordinators.DockingComposer.IsFlyoutOpen)
                coordinators.DockingComposer.CloseFlyout();
        };

        views.Ribbon.ActionCompleted += (message, outcome) =>
            _ = views.ActionReporter.ReportAsync(message, outcome);

        // `WP 17.9.4`: what you make opens right up.
        views.Ribbon.ObjectCreated += (id, kind) => _ = callbacks.OpenObjectAsync(id, kind);

        views.BackgroundTaskRunner.Changed += callbacks.RefreshOutputPanelExtras;

        // Ribbon minimise (`TD-70`).
        views.Ribbon.SetCollapsed(views.Session.PanelUiState.RibbonCollapsed);
        views.Ribbon.CollapsedChanged += collapsed => views.Session.PanelUiState.RibbonCollapsed = collapsed;

        views.Ribbon.CategorySelected += async category =>
        {
            var area = workspace.Navigation.Areas.FirstOrDefault(a => a.Title.Contains(category, StringComparison.OrdinalIgnoreCase));
            if (area is null)
                return;

            await views.BusyOverlay.RunAsync($"Switching to {area.Title}…", async () =>
            {
                await workspace.Navigation.SwitchAreaAsync(area.Id).ConfigureAwait(true);
                await views.ExplorerView.LoadAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
            callbacks.SetCurrentArea(area.Title);
        };

        // The Engineering Calculation surface raises intent; the
        // coordinator performs it through the App-layer workbench.
        views.EngineeringCalculation.PopulateRequested += () => _ = coordinators.EngineeringCalculationCoordinator.PopulateAsync();
        views.EngineeringCalculation.AddMaterialRequested += () => _ = coordinators.EngineeringCalculationCoordinator.AddMaterialAsync();
        views.EngineeringCalculation.ReleaseRequested += () => _ = coordinators.EngineeringCalculationCoordinator.VerifyAndReleaseAsync();
        views.EngineeringCalculation.CalculateRequested += () => _ = coordinators.EngineeringCalculationCoordinator.CalculateAsync();
        views.EngineeringCalculation.NewCalculationRequested += () => coordinators.EngineeringCalculationCoordinator.BeginNewCalculation();
        views.EngineeringCalculation.OpenCalculationRequested += recordId => _ = coordinators.EngineeringCalculationCoordinator.OpenAsync(recordId);
        views.EngineeringCalculation.RenameRequested += (objectId, name) => _ = coordinators.EngineeringCalculationCoordinator.RenameAsync(objectId, name);
        views.EngineeringCalculation.RetireRequested += objectId => _ = coordinators.EngineeringCalculationCoordinator.RetireAsync(objectId);
        views.EngineeringCalculation.ShowRetiredChanged += include => _ = coordinators.EngineeringCalculationCoordinator.SetShowRetiredAsync(include);
        views.EngineeringCalculation.SelectionMoved += () => coordinators.EngineeringCalculationCoordinator.ForgetPendingRetirement();

        views.NavigationRail.NavigationRequested += () => _ = callbacks.RenderCurrentModuleAsync();
        views.ProjectBrowser.ProjectOpened += () => _ = callbacks.RenderCurrentModuleAsync();
        views.ProjectWorkspace.EngineeringRequested += () => _ = callbacks.RenderCurrentModuleAsync();
        views.ProjectWorkspace.ProjectClosed += () => _ = callbacks.RenderCurrentModuleAsync();

        // The Documents area opens a file through the same `TD-80` launcher
        // the object editor uses.
        views.ProjectWorkspace.OpenAttachmentRequested += (ownerId, attachmentId) =>
            _ = callbacks.OpenProjectAttachmentAsync(ownerId, attachmentId, default);

        // The Tasks/Risks/Issues/Decisions/Milestones/Deliverables areas
        // raise intent; the shell performs it through the two project-CRUD
        // coordinators and re-renders.
        views.ProjectWorkspace.CreateTaskRequested += () => _ = coordinators.ProjectDelivery.CreateProjectTaskAsync();
        views.ProjectWorkspace.AssignTaskToMeRequested += taskId => _ = coordinators.ProjectDelivery.AssignProjectTaskToMeAsync(taskId);
        views.ProjectWorkspace.TaskWorkStateChangeRequested += (taskId, target) => _ = coordinators.ProjectDelivery.ChangeProjectTaskWorkStateAsync(taskId, target);
        views.ProjectWorkspace.CreateRiskRequested += () => _ = coordinators.ProjectGovernanceCoordinator.CreateProjectRiskAsync();
        views.ProjectWorkspace.CreateIssueRequested += () => _ = coordinators.ProjectGovernanceCoordinator.CreateProjectIssueAsync();
        views.ProjectWorkspace.CreateDecisionRequested += () => _ = coordinators.ProjectGovernanceCoordinator.CreateProjectDecisionAsync();
        views.ProjectWorkspace.RiskStatusChangeRequested += (id, target) => _ = coordinators.ProjectGovernanceCoordinator.ChangeProjectRiskStatusAsync(id, target);
        views.ProjectWorkspace.IssueStatusChangeRequested += (id, target) => _ = coordinators.ProjectGovernanceCoordinator.ChangeProjectIssueStatusAsync(id, target);
        views.ProjectWorkspace.DecisionStatusChangeRequested += (id, target) => _ = coordinators.ProjectGovernanceCoordinator.DecideProjectDecisionAsync(id, target);
        views.ProjectWorkspace.OwnRiskRequested += id => _ = coordinators.ProjectGovernanceCoordinator.OwnProjectRiskAsync(id);
        views.ProjectWorkspace.AssignIssueToMeRequested += id => _ = coordinators.ProjectGovernanceCoordinator.AssignProjectIssueToMeAsync(id);
        views.ProjectWorkspace.ScoreRiskRequested += id => _ = coordinators.ProjectGovernanceCoordinator.ScoreProjectRiskAsync(id);
        views.ProjectWorkspace.EditRiskRequested += id => _ = coordinators.ProjectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Risk);
        views.ProjectWorkspace.EditIssueRequested += id => _ = coordinators.ProjectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Issue);
        views.ProjectWorkspace.EditDecisionRequested += id => _ = coordinators.ProjectGovernanceCoordinator.EditProjectGovernanceObjectAsync(id, GovernanceFamily.Decision);
        views.ProjectWorkspace.CreateMilestoneRequested += () => _ = coordinators.ProjectDelivery.CreateProjectMilestoneAsync();
        views.ProjectWorkspace.AddDeliverableRequested += id => _ = coordinators.ProjectDelivery.AddProjectDeliverableAsync(id);
        views.ProjectWorkspace.EditMilestoneRequested += id => _ = coordinators.ProjectDelivery.EditProjectMilestoneAsync(id);
        views.ProjectWorkspace.EditTaskRequested += taskId => _ = coordinators.ProjectDelivery.EditProjectTaskAsync(taskId);
        views.ProjectWorkspace.TaskDueDateChangeRequested += taskId => _ = coordinators.ProjectDelivery.ChangeProjectTaskDueDateAsync(taskId);

        // `TD-104`: rehydration that could not recover everything is a fact
        // about the user's own engineering work, said out loud here.
        ReportIncompleteRehydration(views.ToastHost, host.RehydrationResult);

        // The brand header.
        views.Header.SearchRequested += () => views.CommandPalette.Open();
        views.Header.ThemeToggleRequested += async () => await views.Theme.ToggleAsync().ConfigureAwait(true);
        views.Header.ReturnToProjectRequested += async () =>
        {
            await navigator.ReturnToProjectAsync().ConfigureAwait(true);
            await callbacks.RenderCurrentModuleAsync().ConfigureAwait(true);
        };

        // Responsive shell chrome: below the compact threshold the rail
        // folds to its icons, the header's search field to its glyph, and
        // the ribbon's own command buttons to icons alone (`WP 19.2B`,
        // `TD-73`) — the same one threshold drives all three, so they
        // never disagree about what counts as "narrow".
        window.SizeChanged += (_, e) =>
        {
            var compact = e.NewSize.Width < DesignTokens.CompactShellWidth;
            views.NavigationRail.SetCompact(compact);
            views.Header.SetCompact(compact);
            views.Ribbon.SetCompact(compact);
        };

        var shortcutActions = new KeyboardShortcutActions(
            openCommandPalette: () => views.CommandPalette.Open(),
            selectNextDocument: () => views.DocumentArea.SelectNextTab(),
            selectPreviousDocument: () => views.DocumentArea.SelectPreviousTab(),
            closeActiveDocument: () =>
            {
                if (views.DocumentArea.ActiveClosableViewId is { } viewId)
                    _ = coordinators.ViewCoordinator.CloseDocumentAsync(viewId);
            },
            focusExplorerFilter: () => views.ExplorerView.FocusFilter(),
            undo: () => _ = coordinators.UndoRedo.UndoAsync(),
            redo: () => _ = coordinators.UndoRedo.RedoAsync(),
            toggleFavourite: async () =>
            {
                if (workspace.Selection.Current is { } selection)
                {
                    var target = await composition.DomainContext.Repository.FindAsync(selection.ObjectId).ConfigureAwait(true);
                    var title = (target as IHasBusinessIdentifier)?.DisplayName ?? selection.Kind;
                    coordinators.ViewCoordinator.ToggleFavourite(selection.ObjectId, selection.Kind, title);
                }
                else
                {
                    views.StatusBar.SetText("Select an object first to favourite it.");
                }
            });
        KeyboardShortcuts.Register(window, shortcutActions);

        // Keyboard as an IInputBindingProvider (`WP 10.6A`) — handled after
        // the fixed KeyboardShortcuts above, so a fixed binding always
        // takes priority over a user-configured one for the same gesture.
        window.KeyDown += (_, e) => views.KeyboardBindingProvider.HandleKeyDown(e);

        // TD-77 Stage 5: the palette evaluates and invokes against the real
        // selection, through the same adapter the Ribbon uses.
        views.CommandPalette.ContextSource = () => WorkspaceCommandContext.From(workspace.Selection, projectContext.Current?.Id);
        views.CommandPalette.ParameterPrompt = views.CommandPrompt.Prompt;
        views.Ribbon.ProjectIdSource = () => projectContext.Current?.Id;

        composition.InputBindingRegistry.ContextSource = () => WorkspaceCommandContext.From(workspace.Selection, projectContext.Current?.Id);
        composition.InputBindingRegistry.ParameterPrompt = views.CommandPrompt.Prompt;

        views.CommandPalette.InvokeOverride = async (descriptor, context) =>
        {
            if (!descriptor.Id.StartsWith(IMacroManager.CommandIdPrefix, StringComparison.Ordinal))
            {
                return await composition.CommandRegistry
                    .InvokeAsync(descriptor.Id, context, views.CommandPrompt.Prompt)
                    .ConfigureAwait(true);
            }

            var macroResult = await views.BackgroundTaskRunner.RunAsync(
                $"Running macro '{descriptor.DisplayName}'…",
                async ct =>
                {
                    var invocation = await composition.CommandRegistry
                        .InvokeAsync(descriptor.Id, context, prompt: null, ct)
                        .ConfigureAwait(false);

                    return invocation.Result
                        ?? CommandResult.Failure(invocation.Reason ?? "The macro could not be run.");
                }).ConfigureAwait(true);

            return CommandInvocation.Executed(macroResult);
        };
        views.CommandPalette.CommandInvoked += async (descriptor, result) =>
        {
            callbacks.RecordHistory(result.Succeeded
                ? $"Invoked '{descriptor.DisplayName}' via Command Palette."
                : $"'{descriptor.DisplayName}' failed via Command Palette: {result.Message ?? "Command failed."}");
            callbacks.RefreshStatusBar(manager);

            if (result.Succeeded)
            {
                if (result is { SubjectId: { } createdId, SubjectKind: { } createdKind } && RibbonView.IsCreate(descriptor.Id))
                    await callbacks.OpenObjectAsync(createdId, createdKind).ConfigureAwait(true);
            }
        };
        views.CommandPalette.CommandUnavailable += (descriptor, reason) =>
        {
            views.StatusBar.SetText(reason);
            views.ToastHost.Show(reason, FeedbackSeverity.Warning);
        };

        // `WP 18.1B` §2: the palette's own Objects section.
        var searchStore = (Tempest.Core.Persistence.IQueryablePersistenceStore)host.Services!.GetService(typeof(Tempest.Core.Persistence.IQueryablePersistenceStore));
        views.CommandPalette.ObjectSearchSource = async (query, cancellationToken) =>
        {
            var hits = await searchStore.SearchAsync(query, 10, cancellationToken).ConfigureAwait(true);
            var results = new List<PaletteObjectHit>(hits.Count);

            foreach (var hit in hits)
            {
                var found = await composition.DomainContext.Repository.FindAsync(hit.ObjectId, cancellationToken).ConfigureAwait(true);
                var title = (found as IHasBusinessIdentifier)?.DisplayName ?? hit.ObjectId.ToString();

                string? projectName = null;
                if (hit.ProjectId is { } projectId)
                {
                    var project = await composition.DomainContext.Repository.FindAsync(projectId, cancellationToken).ConfigureAwait(true);
                    projectName = (project as IHasBusinessIdentifier)?.DisplayName;
                }

                results.Add(new PaletteObjectHit(hit.ObjectId, hit.Kind, title, projectName));
            }

            return results;
        };
        views.CommandPalette.ObjectSelected += async hit =>
        {
            callbacks.RecordHistory($"Opened '{hit.Title}' from Command Palette search.");
            await callbacks.OpenObjectAsync(hit.ObjectId, hit.Kind).ConfigureAwait(true);
        };
    }

    /// <summary>
    /// Tells the user when startup rehydration could not bring everything
    /// back, and exactly what was missed.
    /// </summary>
    /// <remarks>
    /// Silence here would be the worst outcome available: the workspace
    /// would simply look emptier than the user left it, which is
    /// indistinguishable from having lost the work.
    /// </remarks>
    private static void ReportIncompleteRehydration(ToastHost toastHost, EngineeringRehydrationResult? result)
    {
        if (result is null || result.IsComplete)
            return;

        var parts = new List<string>();

        if (result.UnknownKinds.Count > 0)
            parts.Add($"{result.UnknownKinds.Count} unrecognised kind(s): {string.Join(", ", result.UnknownKinds.Distinct().Order(StringComparer.Ordinal))}");

        if (result.OrphanedStateIds.Count > 0)
            parts.Add($"{result.OrphanedStateIds.Count} object(s) with no backing document");

        if (result.FailedObjectIds.Count > 0)
            parts.Add($"{result.FailedObjectIds.Count} object(s) that could not be reconstructed");

        var message = $"Some saved engineering work could not be reopened — {string.Join("; ", parts)}. It is still on disk; see the Output panel.";

        toastHost.Show(message, FeedbackSeverity.Error, TimeSpan.FromSeconds(20));
    }
}
