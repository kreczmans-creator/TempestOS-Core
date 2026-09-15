using Avalonia.Controls;
using Tempest.Workspace;
using Tempest.Workspace.Editors;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Files;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Shell;
using Tempest.Core.Commands;
using Tempest.Core.Diagnostics;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Macros;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Files;
using Tempest.Desktop.History;
using Tempest.Desktop.Input;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;

namespace Tempest.Desktop.Composition;

/// <summary>
/// Every view, dialog and overlay <see cref="MainWindowComposer.BuildViews"/>
/// builds — nothing here needs any coordinator to already exist (`WP 19.2A`).
/// </summary>
internal sealed record ComposedViews(
    DesktopCompositionRoot Composition,
    IDiagnosticsProvider Diagnostics,
    DesktopSessionState Session,
    ThemeService Theme,
    ToastHost ToastHost,
    BusyOverlay BusyOverlay,
    ConfirmationDialog ConfirmationDialog,
    InputDialog InputDialog,
    MessageDialog MessageDialog,
    MacroManagerDialog MacroManagerDialog,
    ProjectExplorerView ExplorerView,
    PropertyInspectorView InspectorView,
    StatusBarView StatusBar,
    CommandPaletteOverlay CommandPalette,
    DocumentAreaView DocumentArea,
    RibbonView Ribbon,
    DesktopCommandPrompt CommandPrompt,
    ActionOutcomeReporter ActionReporter,
    CitationPicker CitationPicker,
    SubjectPicker SubjectPicker,
    DeclaredFigureEntry DeclaredFigureEntry,
    CheckEntry CheckEntry,
    IssueEntry IssueEntry,
    ReviseReferenceRecordEntry ReviseReferenceRecordEntry,
    IFilePicker EvidenceFilePicker,
    EvidenceEditorSupport EvidenceSupport,
    KindEditorDeclarationRegistry KindEditorDeclarations,
    GlobalNavigationRail NavigationRail,
    ShellHeaderView Header,
    ContentControl ModuleHost,
    IProjectDirectory ProjectDirectory,
    ProjectBrowserView ProjectBrowser,
    ProjectWorkspaceView ProjectWorkspace,
    EngineeringCalculationView EngineeringCalculation,
    LibrariesView LibrariesView,
    OrganisationPicker OrganisationPicker,
    RateCardPicker RateCardPicker,
    Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog OrganisationCatalog,
    Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog RateCardCatalog,
    TimesheetEntryPrompt TimesheetEntryPrompt,
    DeliverableCompletionPrompt DeliverableCompletionPrompt,
    TimesheetWeekView TimesheetWeekView,
    InvoicingView InvoicingView,
    ReportsView ReportsView,
    SettingsView SettingsView,
    NewProjectPrompt NewProjectPrompt,
    ProjectPicker ProjectPicker,
    ProjectQuoteView ProjectQuoteView,
    QuotesView QuotesView,
    Dictionary<Guid, IWorkspaceView> OpenGraphViewsByRootId,
    CommandHistoryLog CommandHistory,
    IBackgroundTaskRunner BackgroundTaskRunner,
    KeyboardCommandBindingProvider KeyboardBindingProvider,
    IWorkspace Workspace,
    WorkspaceManager Manager,
    Tempest.Core.Identity.IPrincipalDirectory Principals,
    // `WP 19.7A`: the rail's own five areas — Projects and Business/
    // Engineering/Tasks are each a tree over an already-built view, per
    // that Work Package's own brief.
    ProjectsAreaView ProjectsAreaView,
    TasksAreaView TasksAreaView,
    EngineeringAreaView EngineeringAreaView,
    BusinessAreaView BusinessAreaView,
    LibrariesView ReferenceDataLibrariesView,
    // `WP 19.7B`: the three sibling read models the dashboards draw from —
    // threaded through here so `BuildCoordinators` (Home's own dashboard,
    // which needs `WorkspaceViewCoordinator`) reads the identical instance
    // this phase already built, rather than standing up a second one.
    Tempest.Workspace.Tasks.ITasksReadModel TasksReadModel,
    Tempest.Workspace.Projects.IProjectStatusReadModel ProjectStatusReadModel,
    Tempest.Core.Invoicing.IAccountsReadModel AccountsReadModel);

/// <summary>
/// <see cref="MainWindow"/>'s own methods, threaded into
/// <see cref="MainWindowComposer"/>'s later phases as plain delegates —
/// never a reference to <see cref="MainWindow"/> itself (`ADR-0103`: a
/// collaborator never references the composition root back). Each of
/// these stays a real method on <see cref="MainWindow"/> because it needs
/// several of that class's own fields together (an Explorer reveal, a
/// Navigation switch, a Document-Area open, all at once) and because it is
/// reached directly by acceptance tests, unchanged (`WP 19.2A`).
/// </summary>
internal sealed record MainWindowCallbacks(
    Action<string> RecordHistory,
    Action RefreshOutputPanelExtras,
    Action<WorkspaceManager> RefreshStatusBar,
    Action<string?> SetCurrentArea,
    Func<Guid, string, Task> OpenObjectAsync,
    Func<Guid, Guid, CancellationToken, Task> OpenProjectAttachmentAsync,
    Func<Guid, string, Task> OpenEvidenceRecordAsync,
    Func<string, string, Task<bool>> PromptForNewProjectAsync,
    Func<Task> RenderCurrentModuleAsync,
    Func<Task> EnterEngineeringCalculationAsync);

/// <summary>
/// Assembles <see cref="MainWindow"/>'s entire object graph — every view,
/// dialog, overlay and coordinator, every cross-view wiring, and the root
/// visual tree — in four explicit, ordered phases (`WP 19.2A`, `TD-109`):
/// <see cref="BuildViews"/> (every view/dialog/overlay that needs no
/// coordinator), <see cref="BuildCoordinators"/> (Undo/Redo, the Workspace
/// View coordinator, docking, layout presets, project delivery/governance,
/// the Evidence workspace — everything that needs a view from the first
/// phase), <see cref="Wire"/> (every cross-collaborator event, delegate,
/// palette binding and keyboard shortcut), <see cref="Layout"/> (the root
/// grid, dock, overlays and rail). Each phase returns a record the next
/// consumes; <see cref="MainWindow"/>'s own constructor is the short call
/// sequence over these four, plus its own <c>Opened</c>/<c>Closing</c>
/// handlers (see `docs/architecture/Desktop Composition Architecture.md`).
/// </summary>
/// <remarks>
/// A collaborator under `ADR-0103`: constructed once by
/// <see cref="MainWindow"/>, never DI-registered, never retaining a
/// reference to <see cref="MainWindow"/> itself — only to the already-built
/// collaborators each phase is handed, and to <see cref="MainWindowCallbacks"/>,
/// the plain delegates standing in for the few methods that must stay on
/// <see cref="MainWindow"/> (see that type's own remarks).
/// </remarks>
internal sealed partial class MainWindowComposer
{
    /// <summary>
    /// Builds every view, dialog and overlay that needs no coordinator to
    /// already exist — the great majority of <see cref="MainWindow"/>'s
    /// own collaborators. <see cref="DocumentAreaView"/> is built here,
    /// with its content builder left at the default
    /// (<see cref="DocumentAreaView.BuildDefaultBody"/>); <see cref="BuildCoordinators"/>
    /// sets <see cref="DocumentAreaView.ContentBuilder"/> once
    /// <c>WorkspaceViewCoordinator</c> exists, resolving that one
    /// construction-order cycle without either side reading a field behind
    /// a null-forgiving <c>!</c> (`WP 19.2A`).
    /// </summary>
    public ComposedViews BuildViews(WorkspaceHost host, Window window, IFilePicker? evidenceFilePickerOverride, MainWindowCallbacks callbacks)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(callbacks);

        var workspace = host.Workspace ?? throw new InvalidOperationException("WorkspaceHost must be started before constructing MainWindow.");
        var manager = host.Manager!;
        var services = host.Services!;

        var composition = new DesktopCompositionRoot(services);
        var diagnostics = composition.Diagnostics;

        var session = new DesktopSessionState(composition.SettingsProvider, composition.Logger);
        session.WindowUiState.ApplyTo(window);
        var toastHost = new ToastHost { DefaultDuration = TimeSpan.FromSeconds(session.UserSettings.ToastDurationSeconds) };

        var keyboardBindingProvider = new KeyboardCommandBindingProvider();
        composition.InputBindingRegistry.Register(keyboardBindingProvider);

        var commandHistory = new CommandHistoryLog();
        var backgroundTaskRunner = new BackgroundTaskRunner();

        // `MacroManagerDialog` itself is constructed further below, once
        // `commandPrompt` exists (`WP 20.2C`: recording a parameterised
        // step's own values needs the identical prompt seam a live
        // invocation already uses) — this callback closes over nothing
        // from that later point in time, so it can be built here exactly
        // as before.
        async Task<CommandResult> RunMacroAsync(Guid macroId)
        {
            var descriptor = composition.CommandRegistry.Items.FirstOrDefault(d => d.Id == IMacroManager.CommandIdPrefix + macroId);
            var title = descriptor?.DisplayName ?? "Macro";

            var context = WorkspaceCommandContext.From(workspace.Selection);
            var result = await backgroundTaskRunner.RunAsync(
                $"Running macro '{title}'…",
                async ct =>
                {
                    var invocation = await composition.CommandRegistry
                        .InvokeAsync(IMacroManager.CommandIdPrefix + macroId, context, prompt: null, ct)
                        .ConfigureAwait(false);

                    return invocation.Result
                        ?? CommandResult.Failure(invocation.Reason ?? "The macro could not be run.");
                }).ConfigureAwait(true);

            commandHistory.Record($"Macro '{title}'", result.Succeeded);
            callbacks.RefreshOutputPanelExtras();

            // `WP 18.1A`: no explicit Explorer/Cockpit refresh here any
            // more — a macro is an arbitrary multi-command mutation,
            // and every one of its commands commits through the same
            // mutators as any other write, each raising its own
            // WorkspaceChanged. Explorer and Cockpit are both
            // subscribed and reload from that.
            return result;
        }

        var toastBridge = new PlatformNotificationToastBridge(toastHost);
        composition.EventBus.Subscribe(toastBridge);
        composition.NotificationDispatcher.Subscribe<Tempest.Core.Notifications.IPlatformNotification>(toastBridge);

        // `WP 19.7A` (scope item 2): the header's own notifications bell —
        // the identical dual subscription `toastBridge` above already
        // establishes, over the same platform notifications the status bar
        // counts, kept as its own bounded list for the flyout.
        var headerNotifications = new HeaderNotificationsCollector();
        composition.EventBus.Subscribe(headerNotifications);
        composition.NotificationDispatcher.Subscribe<Tempest.Core.Notifications.IPlatformNotification>(headerNotifications);

        var theme = new ThemeService(composition.SettingsProvider);

        // `WP 19.0A` (`ADR-0150`): the Timesheets section — hours per week
        // for the current principal — needs both resolved before the
        // dialog is built; `SettingsDialog` itself already knows what to
        // do with them (part 1), this composer simply had not supplied
        // them yet.
        var workingPatterns = (Tempest.Core.Timesheets.IWorkingPatternProvider)services.GetService(typeof(Tempest.Core.Timesheets.IWorkingPatternProvider));
        var currentPrincipalAccessor = (Tempest.Core.Identity.ICurrentPrincipalAccessor)services.GetService(typeof(Tempest.Core.Identity.ICurrentPrincipalAccessor));

        // `WP 19.1A` part 3 (`ADR-0151`): the Invoicing area's own connector
        // and secret store, resolved here exactly as `workingPatterns`/
        // `currentPrincipalAccessor` are above — `SettingsDialog`'s own
        // Invoicing section (Authorise button, client id/secret) and
        // `InvoicingView` (`invoicingConnector` is not itself threaded into
        // the view; only `SettingsDialog` reads it) need them.
        var invoicingConnector = (Tempest.Core.Invoicing.IInvoicingConnector)services.GetService(typeof(Tempest.Core.Invoicing.IInvoicingConnector));
        var secretStore = (Tempest.Core.Secrets.ISecretStore)services.GetService(typeof(Tempest.Core.Secrets.ISecretStore));

        // `WP 19.8B` (po-comments.md item 8): the accounts reading —
        // resolved the identical way, for the Settings area's own single
        // line and Refresh now button. `AccountsRefreshService` is
        // resolved as its own concrete type (not behind an interface, the
        // same convention `InvoiceReconciliationService` itself follows)
        // because it is the singleton the hosted-service manager starts
        // and stops.
        var accountsReadModel = (Tempest.Core.Invoicing.IAccountsReadModel)services.GetService(typeof(Tempest.Core.Invoicing.IAccountsReadModel));
        var accountsRefreshService = (Tempest.Core.Invoicing.AccountsRefreshService)services.GetService(typeof(Tempest.Core.Invoicing.AccountsRefreshService));

        // `WP 19.2B`: the Settings area's own two read-only sections —
        // the persistence root, resolved from the real store when it is
        // the real `SqlitePersistenceStore` (a test's in-memory store has
        // no file path to show), and `Identity:DisplayName`/`Identity:Role`,
        // read straight from configuration exactly as `SessionPrincipalSource`
        // itself does — never a second identity mechanism.
        var queryableStore = (Tempest.Core.Persistence.IQueryablePersistenceStore)services.GetService(typeof(Tempest.Core.Persistence.IQueryablePersistenceStore));
        var persistenceRootPath = queryableStore is Tempest.Core.Persistence.SqlitePersistenceStore sqliteStore
            ? sqliteStore.DatabasePath
            : "(in-memory persistence — no file on disk)";
        var configurationProvider = (Tempest.Core.Configuration.IConfigurationProvider)services.GetService(typeof(Tempest.Core.Configuration.IConfigurationProvider));

        var settingsView = new SettingsView(
            theme, session.UserSettings, composition.SettingsProvider, configurationProvider, persistenceRootPath,
            workingPatterns, currentPrincipalAccessor, invoicingConnector, secretStore,
            accountsReadModel, accountsRefreshService);

        var confirmationDialog = new ConfirmationDialog();
        var inputDialog = new InputDialog();
        var messageDialog = new MessageDialog();

        Task<bool> ConfirmDeleteAsync(string prompt) =>
            session.UserSettings.ConfirmBeforeDelete
                ? confirmationDialog.ConfirmAsync("Delete?", prompt, "Delete")
                : Task.FromResult(true);

        var explorerView = new ProjectExplorerView(workspace.ProjectExplorer, manager)
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
            RecentSearchCapacity = session.UserSettings.RecentSearchCapacity,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        var principals = (Tempest.Core.Identity.IPrincipalDirectory)services.GetService(typeof(Tempest.Core.Identity.IPrincipalDirectory));
        var inspectorView = new PropertyInspectorView(workspace.PropertyInspector, manager, composition.DomainContext, principals) { WorkspaceChanges = composition.WorkspaceChanges };
        var statusBar = new StatusBarView();
        var commandPalette = new CommandPaletteOverlay(composition.CommandRegistry);

        // The Object Editor Framework's own injectable content builder is
        // left at its default here — `BuildCoordinators` sets it once
        // `WorkspaceViewCoordinator.BuildDocumentContent` exists.
        var documentArea = new DocumentAreaView();

        var ribbon = new RibbonView(
            composition.CommandRegistry,
            manager,
            workspace,
            setHint: hint => statusBar.SetHint(hint),
            openDocument: view => documentArea.ShowTab(view))
        {
            ConfirmDeleteAsync = ConfirmDeleteAsync,
        };

        var commandPrompt = new DesktopCommandPrompt(
            inputDialog,
            confirm: (descriptor, message) => SurfaceCommandPolicy.DeleteCommandIds.Contains(descriptor.Id)
                ? ConfirmDeleteAsync(message)
                : confirmationDialog.ConfirmAsync("Confirm", message, "Continue"));
        ribbon.ParameterPrompt = commandPrompt.Prompt;

        // `WP 20.2C`: recording a parameterised step's own values (Add
        // Step, in the editor) uses the identical prompt seam the Ribbon
        // and the Palette already invoke a bound command through — one
        // `InputDialog`, one set of validators, no second collection UI.
        var macroManagerDialog = new MacroManagerDialog(
            composition.MacroManager, composition.CommandRegistry, commandPrompt.Prompt, RunMacroAsync);

        var actionReporter = new ActionOutcomeReporter(statusBar, toastHost, callbacks.RecordHistory);
        settingsView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var kindEditorDeclarations = new KindEditorDeclarationRegistry();
        KindEditorDeclarations.RegisterAll(kindEditorDeclarations);

        var evidenceFilePicker = evidenceFilePickerOverride ?? new AvaloniaFilePicker(window);
        var citationPicker = new CitationPicker(ct => LibrariesView.ReadAllAsync(
            host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!, ct));
        var subjectPicker = new SubjectPicker(composition.DomainContext);
        var declaredFigureEntry = new DeclaredFigureEntry();
        var checkEntry = new CheckEntry();
        var issueEntry = new IssueEntry();
        var reviseReferenceRecordEntry = new ReviseReferenceRecordEntry();

        var evidenceSupport = new EvidenceEditorSupport(
            evidenceFilePicker,
            ct => citationPicker.PickAsync(ct),
            ct => declaredFigureEntry.PromptAsync(ct),
            ct => subjectPicker.PickAsync(ct),
            ct => checkEntry.PromptAsync(ct),
            ct => issueEntry.PromptAsync(ct));

        var navigationRail = new GlobalNavigationRail(host.ShellNavigator!);

        var header = new ShellHeaderView();
        header.SetPrincipal(host.SessionPrincipal?.Identity.DisplayName, host.SessionPrincipal?.Role.ToString());
        header.SetNotifications(headerNotifications.Messages);
        headerNotifications.Changed = () => header.SetNotifications(headerNotifications.Messages);

        var moduleHost = new ContentControl();

        // `WP 19.0A` (`ADR-0150`): the Timesheets area and the project
        // Deliverables tab — each dispatches directly through the command
        // dispatcher, mirroring `EvidenceWorkspaceView`'s own identical
        // shape, and each opens what it makes right up through the same
        // callback `EvidenceWorkspaceView` uses (`WP 17.9.4`).
        var organisationCatalog = (Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog)services.GetService(typeof(Tempest.Core.BusinessOperations.Crm.IOrganisationCatalog));
        var rateCardCatalog = (Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog)services.GetService(typeof(Tempest.Core.BusinessGovernance.Pricing.IRateCardCatalog));
        var timesheetService = (Tempest.Core.Timesheets.ITimesheetService)services.GetService(typeof(Tempest.Core.Timesheets.ITimesheetService));

        // `WP 19.6A`: the two remaining governed reference libraries the
        // Libraries tab itself lists but Evidence's own citation picker
        // deliberately still does not (Manufacturing resolves the same
        // way, just below, since `LibrariesView` needs it too).
        var componentCatalog = (Tempest.Core.Components.IComponentCatalog)services.GetService(typeof(Tempest.Core.Components.IComponentCatalog));
        var processCatalog = (Tempest.Core.Manufacturing.IProcessCatalog)services.GetService(typeof(Tempest.Core.Manufacturing.IProcessCatalog));

        var organisationPicker = new OrganisationPicker(organisationCatalog);
        var rateCardPicker = new RateCardPicker(rateCardCatalog);
        var timesheetEntryPrompt = new TimesheetEntryPrompt(composition.DomainContext, rateCardCatalog);
        var deliverableCompletionPrompt = new DeliverableCompletionPrompt(composition.DomainContext, host.ProjectDocuments!);

        // Fire-and-forget at the view boundary, but never silently: an open
        // that throws is reported like any other failed action, so "it
        // created but nothing opened" has a reason on screen.
        Action<Guid, string> openObjectRightUp = (id, kind) => _ = OpenReportingAsync(id, kind);

        async Task OpenReportingAsync(Guid id, string kind)
        {
            try
            {
                await callbacks.OpenEvidenceRecordAsync(id, kind).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                await actionReporter.ReportAsync($"Created, but opening it failed: {ex.GetBaseException().Message}", ActionOutcome.Failed).ConfigureAwait(true);
            }
        }

        // `WP 19.2B`: the Reports area's own "Export"/document-row "Open"
        // — opening a file never navigates, exactly as
        // `OpenProjectAttachmentAsync`'s own remarks already establish for
        // `ProjectWorkspaceView`'s identical Documents-tab callback.
        Action<Guid, Guid> openAttachmentRightUp = (ownerId, attachmentId) => _ = callbacks.OpenProjectAttachmentAsync(ownerId, attachmentId, default);

        var timesheetWeekView = new TimesheetWeekView(
            composition.DomainContext, timesheetService, workingPatterns, composition.CommandDispatcher, composition.CommandRegistry,
            () => host.SessionPrincipal?.IdentityId, timesheetEntryPrompt, openObjectRightUp)
        {
            ParameterPrompt = commandPrompt.Prompt,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        timesheetWeekView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var deliverablesView = new ProjectDeliverablesView(
            composition.DomainContext, composition.CommandDispatcher, composition.CommandRegistry, () => host.ProjectContext!.Current?.Id,
            deliverableCompletionPrompt, openObjectRightUp)
        {
            ParameterPrompt = commandPrompt.Prompt,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        deliverablesView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // `WP 19.1A` part 3 (`ADR-0151`): the Invoicing area — every
        // InvoiceRequest across open projects (or the open project when one
        // is open), grouped by status. Dispatches Send/Reconcile/Void
        // through the command dispatcher's own registry, mirroring
        // `TimesheetWeekView`'s own Amend/Delete shape, and opens what it
        // reviews right up through the same callback `EvidenceWorkspaceView`
        // uses (`WP 17.9.4`).
        var invoicingView = new InvoicingView(
            composition.DomainContext, composition.CommandRegistry, () => host.ProjectContext!.Current?.Id, openObjectRightUp)
        {
            ParameterPrompt = commandPrompt.Prompt,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        invoicingView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var projectDirectory = host.ProjectDirectory!;
        var projectBrowser = new ProjectBrowserView(projectDirectory, host.ShellNavigator!, callbacks.PromptForNewProjectAsync);

        // `WP 19.5B` (`ADR-0152`, Product Owner comment item 4): the Quote
        // tab and the Quotes area — a quote is opened with the project,
        // defines its initial deliverables and requirements once accepted,
        // and exports as a PDF through the same SkiaSharp path the issue
        // sheet already established (comment item 9).
        var newProjectPrompt = new NewProjectPrompt();
        var projectPicker = new ProjectPicker(projectDirectory);
        var quotationSheetRenderer = new QuotationSheetRenderer();
        string IssuerName() => host.SessionPrincipal?.Identity.DisplayName ?? "TempestOS";
        string ApplicationVersionText() => MainWindow.DescribeBuild(services);

        var projectQuoteView = new ProjectQuoteView(
            composition.DomainContext, composition.CommandDispatcher, composition.CommandRegistry, () => host.ProjectContext!.Current?.Id,
            organisationCatalog, openObjectRightUp, evidenceFilePicker, quotationSheetRenderer, IssuerName, ApplicationVersionText)
        {
            ParameterPrompt = commandPrompt.Prompt,
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        projectQuoteView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // Opens a specific quotation's own project, right up in its Quote
        // tab (`WP 17.9.4`) — specialised from the generic `openObjectRightUp`
        // above because a Quotation's own "opens right up" destination is
        // the project's Quote tab, not the generic Object Editor (this Work
        // Package's own brief, scope item 5: "Open (opens the project's
        // Quote tab right up)").
        async Task OpenQuoteAsync(Guid projectId, Guid quotationId)
        {
            await host.ShellNavigator!.OpenProjectAsync(projectId, Tempest.Workspace.Shell.ProjectArea.Quote).ConfigureAwait(true);
            await callbacks.RenderCurrentModuleAsync().ConfigureAwait(true);
            await projectQuoteView.SelectQuoteAsync(quotationId).ConfigureAwait(true);
        }
        Action<Guid, Guid> openQuote = (projectId, quotationId) => _ = OpenQuoteAsync(projectId, quotationId);

        var quotesView = new QuotesView(
            composition.DomainContext, composition.CommandDispatcher, () => host.ProjectContext!.Current?.Id, organisationCatalog,
            projectDirectory, projectPicker, evidenceFilePicker, quotationSheetRenderer, IssuerName, ApplicationVersionText, openQuote)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        quotesView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // `WP 19.2B`: the Reports area — issued evidence sheets and
        // project documents, across every live project, filterable to one.
        var reportsView = new ReportsView(
            composition.DomainContext, projectDirectory, host.ProjectDocuments!, openObjectRightUp, openAttachmentRightUp)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };

        var engineeringCalculation = new EngineeringCalculationView(principals.Describe);

        // `WP 19.6A`: the "cited by" read side — a fresh Evidence scan
        // over the same already-composed domain, holding no state of its
        // own (`Workspace/Evidence/ReferenceCitationIndex.cs`'s own
        // remarks).
        var referenceCitationIndex = new ReferenceCitationIndex(composition.DomainContext, projectDirectory);

        var librariesView = new LibrariesView(
            host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!, processCatalog,
            componentCatalog, rateCardCatalog, host.ReferenceReview!, host.BracketCalculations!,
            referenceCitationIndex, openObjectRightUp)
        {
            ReviseRecordPrompt = (label, definitionJson, source, ct) => reviseReferenceRecordEntry.PromptAsync(label, definitionJson, source, ct),
        };
        librariesView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // `WP 19.7A`: the Evidence workspace never actually depended on
        // `WorkspaceViewCoordinator` — moved here (from what used to be
        // `BuildCoordinators`) so the project workspace's own new Evidence
        // tab (below) can be handed the real instance rather than waiting
        // for a later phase.
        var evidenceWorkspace = new EvidenceWorkspaceView(
            composition.DomainContext, composition.CommandDispatcher, evidenceFilePicker,
            () => host.ProjectContext!.Current?.Id, (id, kind) => _ = callbacks.OpenEvidenceRecordAsync(id, kind), librariesView)
        {
            ParameterPrompt = commandPrompt.Prompt,
            SubjectPrompt = ct => subjectPicker.PickAsync(ct),
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        evidenceWorkspace.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // `WP 19.7A` (`po-comments.md` item 6, sheet 8): a second
        // `LibrariesView` instance for Engineering → Reference data — the
        // first is already parented inside `evidenceWorkspace` above, and a
        // control can only ever be parented once.
        var referenceDataLibrariesView = new LibrariesView(
            host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!, processCatalog,
            componentCatalog, rateCardCatalog, host.ReferenceReview!, host.BracketCalculations!,
            referenceCitationIndex, openObjectRightUp)
        {
            ReviseRecordPrompt = (label, definitionJson, source, ct) => reviseReferenceRecordEntry.PromptAsync(label, definitionJson, source, ct),
        };
        referenceDataLibrariesView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        // `WP 19.7A` (Product Owner comment item 6 delta (c)): the
        // project's own Sign off tab — `IProjectLifecycleService` (`WP
        // 19.5C`) is already registered in DI with zero prior UI
        // consumers.
        var projectLifecycleService = (Tempest.Core.Projects.IProjectLifecycleService)services.GetService(typeof(Tempest.Core.Projects.IProjectLifecycleService));
        var signOffView = new ProjectSignOffView(projectLifecycleService, composition.DomainContext, () => host.ProjectContext!.Current?.Id);
        signOffView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var projectWorkspace = new ProjectWorkspaceView(
            host.ProjectContext!, host.ProjectDirectory!, host.ShellNavigator!, host.ProjectDocuments!, host.ProjectRequirements!,
            host.ProjectTasks!, host.ProjectGovernance!, host.ProjectMilestones!, deliverablesView, projectQuoteView,
            evidenceWorkspace, signOffView, composition.DomainContext);

        // `WP 19.7A` (`po-comments.md` item 6 delta (a)): the Tasks
        // read model — a "sibling reader" over the identical persistence
        // store other read models (`ProjectStatusReadModel`) already use,
        // constructed directly rather than through DI (its own
        // constructor takes only the store and an optional clock, exactly
        // as that sibling does) since neither is registered there yet and
        // this Work Package's own brief limits it to "wiring... in".
        var tasksReadModel = new Tempest.Workspace.Tasks.TasksReadModelService(queryableStore);

        // `WP 19.7B`: the Project dashboard's own status read model — the
        // identical "sibling reader" shape `tasksReadModel` above already
        // establishes, constructed directly for the same reason.
        var projectStatusReadModel = new Tempest.Workspace.Projects.ProjectStatusReadModel(queryableStore);

        var tasksAreaView = new TasksAreaView(
            tasksReadModel, composition.CommandDispatcher, () => inputDialog.PromptAsync("New task", "Title"), openObjectRightUp)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        tasksAreaView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var projectsDashboardView = new ProjectsDashboardView(projectStatusReadModel);
        var projectsAreaView = new ProjectsAreaView(composition.DomainContext, projectBrowser, projectsDashboardView)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };

        var engineeringDashboardView = new EngineeringDashboardView(tasksReadModel, composition.CommandDispatcher, openObjectRightUp);
        engineeringDashboardView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var engineeringAreaView = new EngineeringAreaView(
            host.ShellNavigator!, tasksReadModel, reportsView, engineeringCalculation, referenceDataLibrariesView,
            engineeringDashboardView, callbacks.EnterEngineeringCalculationAsync, composition.CommandDispatcher, openObjectRightUp)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };
        engineeringAreaView.ActionCompleted += (message, outcome) => _ = actionReporter.ReportAsync(message, outcome);

        var subscriptionsView = new SubscriptionsView(accountsReadModel, accountsRefreshService);

        var businessDashboardView = new BusinessDashboardView(accountsReadModel, composition.DomainContext, openObjectRightUp);
        var businessAreaView = new BusinessAreaView(quotesView, invoicingView, timesheetWeekView, subscriptionsView, businessDashboardView)
        {
            WorkspaceChanges = composition.WorkspaceChanges,
        };

        return new ComposedViews(
            composition, diagnostics, session, theme, toastHost, new BusyOverlay(), confirmationDialog, inputDialog, messageDialog,
            macroManagerDialog, explorerView, inspectorView, statusBar, commandPalette, documentArea, ribbon, commandPrompt, actionReporter,
            citationPicker, subjectPicker, declaredFigureEntry, checkEntry, issueEntry, reviseReferenceRecordEntry, evidenceFilePicker,
            evidenceSupport, kindEditorDeclarations, navigationRail, header, moduleHost, projectDirectory, projectBrowser, projectWorkspace,
            engineeringCalculation, librariesView, organisationPicker, rateCardPicker, organisationCatalog, rateCardCatalog, timesheetEntryPrompt, deliverableCompletionPrompt,
            timesheetWeekView, invoicingView, reportsView, settingsView, newProjectPrompt, projectPicker, projectQuoteView, quotesView,
            [], commandHistory, backgroundTaskRunner, keyboardBindingProvider,
            workspace, manager, principals,
            projectsAreaView, tasksAreaView, engineeringAreaView, businessAreaView, referenceDataLibrariesView,
            tasksReadModel, projectStatusReadModel, accountsReadModel);
    }
}

/// <summary>
/// The header's own notifications bell (`WP 19.7A`, scope item 2): the
/// last <see cref="Capacity"/> platform notifications, kept in memory only
/// (never persisted — mirrors <see cref="PlatformNotificationToastBridge"/>'s
/// own identical dual subscription over the same <see cref="Tempest.Core.Notifications.IPlatformNotification"/>
/// the status bar's own count already draws from).
/// </summary>
internal sealed class HeaderNotificationsCollector :
    Tempest.Core.Events.IEventHandler<Tempest.Core.Notifications.IPlatformNotification>,
    Tempest.Core.Notifications.INotificationHandler<Tempest.Core.Notifications.IPlatformNotification>
{
    private const int Capacity = 20;
    private readonly List<string> _messages = [];

    /// <summary>Every message currently held, newest first.</summary>
    public IReadOnlyList<string> Messages => _messages;

    /// <summary>Raised after a new notification is recorded, so the header can re-read <see cref="Messages"/>.</summary>
    public Action? Changed { get; set; }

    /// <inheritdoc cref="Tempest.Core.Events.IEventHandler{TEvent}.HandleAsync" />
    public Task HandleAsync(Tempest.Core.Notifications.IPlatformNotification @event, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(@event);

        var message = $"[{@event.Category}] {@event.Message}";

        void Record()
        {
            _messages.Insert(0, message);
            if (_messages.Count > Capacity)
                _messages.RemoveAt(_messages.Count - 1);
            Changed?.Invoke();
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            Record();
        else
            Avalonia.Threading.Dispatcher.UIThread.Post(Record);

        return Task.CompletedTask;
    }
}
