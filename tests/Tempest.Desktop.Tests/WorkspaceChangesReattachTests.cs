using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Tempest.Workspace;
using Tempest.Workspace.Evidence;
using Tempest.Workspace.Projects;
using Tempest.Workspace.Tasks;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.Components;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Events;
using Tempest.Core.Invoicing;
using Tempest.Core.Manufacturing;
using Tempest.Core.People;
using Tempest.Core.Persistence;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Files;
using Tempest.Desktop.Quotations;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Desktop.Views.EngineeringAssets;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.7C`: for each of the thirteen views named in the brief, proves
/// <see cref="WorkspaceChangesSubscription"/> keeps a view's own change-feed
/// subscription alive across however many times it is shown, hidden and
/// shown again — the defect `WP 19.7B` found (present since `WP 18.2A`):
/// each view's own <c>WorkspaceChanges</c> setter subscribed once, and the
/// composer assigns it exactly once, by object initializer, so the first
/// detach lost the subscription for good. Each test attaches a view to a
/// bare <see cref="Window"/> (never the full <see cref="MainWindow"/> shell
/// — these views are constructed directly here, from real
/// <see cref="WorkspaceHost"/> services, exactly as
/// <c>ObjectEditorViewTests</c> already does for its own one view),
/// assigns a fake <see cref="IWorkspaceChanges"/>, detaches, re-attaches,
/// and raises a change — asserting the view re-reads only while attached.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorkspaceChangesReattachTests
{
    /// <summary>A controllable <see cref="IWorkspaceChanges"/> — <see cref="Raise"/> fires <see cref="Changed"/> on demand, standing in for a real committed transaction.</summary>
    private sealed class FakeWorkspaceChanges : IWorkspaceChanges
    {
        public event Action<WorkspaceChange>? Changed;

        /// <param name="kind">The touched object's own canonical Kind — several of the thirteen views filter <see cref="WorkspaceChange.Entries"/> by Kind before reacting.</param>
        /// <param name="objectId">The touched object's own id — <see cref="ObjectEditorView"/> filters by this instead of Kind.</param>
        public void Raise(string kind = "Test", Guid? objectId = null) =>
            Changed?.Invoke(new WorkspaceChange(1, new[] { new WorkspaceChangeEntry(objectId ?? Guid.NewGuid(), kind, WorkspaceChangeType.Updated) }));
    }

    private static T Resolve<T>(WorkspaceHost host) where T : class => (T)host.Services!.GetService(typeof(T));

    // ================================================================
    // The thirteen views — one test each, table-driven by construction
    // rather than by a runtime table (the thirteen constructors are
    // genuinely different; see the class remarks).
    // ================================================================

    [AvaloniaFact]
    public async Task TasksAreaView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var readModel = new TasksReadModelService(Resolve<IQueryablePersistenceStore>(host));
            var view = new TasksAreaView(readModel, Resolve<ICommandDispatcher>(host), () => Task.FromResult<string?>(null), (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectsAreaView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var projectBrowser = new ProjectBrowserView(host.ProjectDirectory!, host.ShellNavigator!, (_, _) => Task.FromResult(true));
            var dashboard = new ProjectsDashboardView(new ProjectStatusReadModel(Resolve<IQueryablePersistenceStore>(host)));
            var view = new ProjectsAreaView(domainContext, projectBrowser, dashboard);

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task HomeDashboardView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var queryableStore = Resolve<IQueryablePersistenceStore>(host);
            var tasksReadModel = new TasksReadModelService(queryableStore);
            var projectStatusReadModel = new ProjectStatusReadModel(queryableStore);
            var domainContext = Resolve<EngineeringDomainContext>(host);

            var view = new HomeDashboardView(
                tasksReadModel, projectStatusReadModel, Resolve<IAccountsReadModel>(host), domainContext, host.Workspace!.Cockpit,
                favourites: null,
                openObjectRightUp: (_, _) => Task.CompletedTask,
                openTasks: () => { },
                onOpenRecent: _ => Task.CompletedTask);

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task BusinessAreaView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var commandDispatcher = Resolve<ICommandDispatcher>(host);
            var commandRegistry = Resolve<ICommandRegistry>(host);
            var organisationCatalog = Resolve<IOrganisationCatalog>(host);
            var rateCardCatalog = Resolve<IRateCardCatalog>(host);
            var accountsReadModel = Resolve<IAccountsReadModel>(host);
            var sheetRenderer = new QuotationSheetRenderer();

            var quotesView = new QuotesView(
                domainContext, commandDispatcher, () => null, organisationCatalog, host.ProjectDirectory!,
                new ProjectPicker(host.ProjectDirectory!), new StubFilePicker(), sheetRenderer, () => "Issuer", () => "1.0", (_, _) => { });
            var invoicingView = new InvoicingView(domainContext, commandRegistry, () => null, (_, _) => { });
            var timesheetWeekView = new TimesheetWeekView(
                domainContext, Resolve<ITimesheetService>(host), Resolve<IWorkingPatternProvider>(host), commandDispatcher, commandRegistry,
                () => host.SessionPrincipal?.IdentityId, new TimesheetEntryPrompt(domainContext, rateCardCatalog), (_, _) => { });
            var subscriptionsView = new SubscriptionsView(accountsReadModel, Resolve<AccountsRefreshService>(host));
            var businessDashboardView = new BusinessDashboardView(accountsReadModel, domainContext, (_, _) => { });

            var view = new BusinessAreaView(quotesView, invoicingView, timesheetWeekView, subscriptionsView, businessDashboardView);

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task EngineeringAreaView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var queryableStore = Resolve<IQueryablePersistenceStore>(host);
            var tasksReadModel = new TasksReadModelService(queryableStore);

            var reportsView = new ReportsView(domainContext, host.ProjectDirectory!, host.ProjectDocuments!, (_, _) => { }, (_, _) => { });
            var engineeringCalculation = new EngineeringCalculationView();
            var referenceCitationIndex = new ReferenceCitationIndex(domainContext, host.ProjectDirectory!);
            var librariesView = new LibrariesView(
                host.Materials!, host.Fasteners!, host.Bearings!, host.Standards!, host.Constants!,
                Resolve<IProcessCatalog>(host), Resolve<IComponentCatalog>(host), Resolve<IRateCardCatalog>(host), Resolve<IPersonCatalog>(host),
                host.ReferenceReview!, host.BracketCalculations!, referenceCitationIndex, (_, _) => { });
            var engineeringDashboard = new EngineeringDashboardView(tasksReadModel, Resolve<ICommandDispatcher>(host), (_, _) => { });
            var engineeringAssets = new EngineeringAssetsView(
                host.CalculationPacks!, host.CalculationPackValidation!, host.EngineeringTemplates!, host.EngineeringTemplateValidation!,
                host.VerificationArtefacts!, host.VerificationArtefactValidation!, host.EngineeringTrace!, host.Materials!,
                host.BracketCheck!, host.BracketEngineeringRecords!, new StubFilePicker(), () => host.SessionPrincipal?.IdentityId);

            var view = new EngineeringAreaView(
                host.ShellNavigator!, tasksReadModel, reportsView, engineeringCalculation, librariesView, engineeringDashboard,
                () => Task.CompletedTask, Resolve<ICommandDispatcher>(host), (_, _) => { }, engineeringAssets);

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// <see cref="EvidenceWorkspaceView"/> takes a bare <see cref="Control"/>
    /// for its own Libraries tab (see that constructor's own remarks) — a
    /// plain <see cref="Border"/> stands in for the real
    /// <see cref="LibrariesView"/> this test does not otherwise need.
    /// </summary>
    [AvaloniaFact]
    public async Task EvidenceWorkspaceView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var view = new EvidenceWorkspaceView(
                domainContext, Resolve<ICommandDispatcher>(host), new StubFilePicker(), () => null, (_, _) => { }, new Border());

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "Evidence");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task InvoicingView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var view = new InvoicingView(Resolve<EngineeringDomainContext>(host), Resolve<ICommandRegistry>(host), () => null, (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "InvoiceRequest");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectDeliverablesView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var completionPrompt = new DeliverableCompletionPrompt(domainContext, host.ProjectDocuments!);
            var view = new ProjectDeliverablesView(
                domainContext, Resolve<ICommandDispatcher>(host), Resolve<ICommandRegistry>(host), () => null, completionPrompt, (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "Deliverable");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectQuoteView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var view = new ProjectQuoteView(
                domainContext, Resolve<ICommandDispatcher>(host), Resolve<ICommandRegistry>(host), () => null,
                Resolve<IOrganisationCatalog>(host), (_, _) => { }, new StubFilePicker(), new QuotationSheetRenderer(),
                () => "Issuer", () => "1.0");

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "Quotation");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task QuotesView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var view = new QuotesView(
                domainContext, Resolve<ICommandDispatcher>(host), () => null, Resolve<IOrganisationCatalog>(host),
                host.ProjectDirectory!, new ProjectPicker(host.ProjectDirectory!), new StubFilePicker(), new QuotationSheetRenderer(),
                () => "Issuer", () => "1.0", (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "Quotation");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ReportsView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var view = new ReportsView(Resolve<EngineeringDomainContext>(host), host.ProjectDirectory!, host.ProjectDocuments!, (_, _) => { }, (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task TimesheetWeekView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var prompt = new TimesheetEntryPrompt(domainContext, Resolve<IRateCardCatalog>(host));
            var view = new TimesheetWeekView(
                domainContext, Resolve<ITimesheetService>(host), Resolve<IWorkingPatternProvider>(host), Resolve<ICommandDispatcher>(host),
                Resolve<ICommandRegistry>(host), () => host.SessionPrincipal?.IdentityId, prompt, (_, _) => { });

            await AssertReattachAsync(view, f => view.WorkspaceChanges = f, () => view.RefreshCount, changeKind: "TimesheetEntry");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// <see cref="ObjectEditorView"/> is per object and closes rather than
    /// hides (the brief's own fix section 3) — it takes
    /// <see cref="WorkspaceChangesSubscription"/> only for uniformity, not
    /// because it shared the defect. Proven here the same way regardless:
    /// its own <c>OnWorkspaceChanged</c> filters by the edited object's id
    /// (not Kind), so the raised change names the real object this editor
    /// opened.
    /// </summary>
    [AvaloniaFact]
    public async Task ObjectEditorView_ReactsAfterDetachAndReattach()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var domainContext = Resolve<EngineeringDomainContext>(host);
            var commandDispatcher = Resolve<ICommandDispatcher>(host);
            var project = await host.ProjectDirectory!.CreateAsync("P-WCRT-01", "Reattach Test Project");
            var target = await domainContext.Repository.FindAsync(project.Id);
            Assert.NotNull(target);

            var view = ObjectEditorView.TryCreate(target!.Id, target.Kind!, domainContext, host.Manager!, (_, _) => { }, commandDispatcher);
            Assert.NotNull(view);

            await AssertReattachAsync(view!, f => view!.WorkspaceChanges = f, () => view!.RefreshCount, changeKind: target.Kind!, changeObjectId: target.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ================================================================
    // Shared mechanics.
    // ================================================================

    /// <summary>
    /// Attaches <paramref name="view"/> to a bare <see cref="Window"/>,
    /// assigns a fake feed, detaches, re-attaches and raises a change at
    /// each stage — proving the view reacts while attached, does not throw
    /// or react while detached, and reacts again once reattached (the
    /// `WP 19.7C` fix itself).
    /// </summary>
    private static async Task AssertReattachAsync(
        Control view, Action<IWorkspaceChanges?> setFeed, Func<int> getRefreshCount, string changeKind = "Test", Guid? changeObjectId = null)
    {
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var fake = new FakeWorkspaceChanges();
        setFeed(fake);

        // Reacts while first attached.
        var beforeFirstRaise = getRefreshCount();
        fake.Raise(changeKind, changeObjectId);
        await PumpUntilAsync(() => getRefreshCount() > beforeFirstRaise);
        Assert.True(getRefreshCount() > beforeFirstRaise, "Expected a refresh while first attached.");

        // Detach — no throw, and no further refresh.
        window.Content = null;
        Dispatcher.UIThread.RunJobs();

        var afterDetachBaseline = getRefreshCount();
        var thrown = Record.Exception(() => fake.Raise(changeKind, changeObjectId));
        Assert.Null(thrown);
        await PumpBrieflyAsync();
        Assert.Equal(afterDetachBaseline, getRefreshCount());

        // Re-attach — reacts again (the defect this Work Package fixes).
        window.Content = view;
        Dispatcher.UIThread.RunJobs();

        var beforeReattachRaise = getRefreshCount();
        fake.Raise(changeKind, changeObjectId);
        await PumpUntilAsync(() => getRefreshCount() > beforeReattachRaise);
        Assert.True(getRefreshCount() > beforeReattachRaise, "Expected a refresh again after reattaching — WP 19.7C.");
    }

    private static async Task PumpUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Pumps the dispatcher for a short, bounded window with nothing to wait for — used to prove a re-read did <em>not</em> happen, where waiting for a condition would mean waiting out the full timeout every time.</summary>
    private static async Task PumpBrieflyAsync()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }
    }
}
