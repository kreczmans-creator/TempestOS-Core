using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Timesheets;
using Tempest.Desktop.Views;
using Tempest.Desktop.Views.Dashboards;
using Tempest.Workspace.Shell;
using static Tempest.Desktop.Tests.DesktopTestHelpers;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 20.10A` acceptance journeys, through the real window: New Project's
/// own new Client/Rate card fields (Product Owner findings D1/D2/D12), the
/// project workspace's own new Details tab (D2/D12/T1), Home's own New
/// Project button (D1), and the Record dialog's own "every open project,
/// with the one real reason a specific one cannot record time stated
/// inline" behaviour (D12).
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class NewProjectDetailsAndTimesheetsJourneyTests
{
    /// <summary>
    /// New Project with a client picked and a rate card pinned → the
    /// Details tab shows both, by name (not the bare id) → Timesheets →
    /// Record lists the project and records 3h against it.
    /// </summary>
    [AvaloniaFact]
    public async Task NewProjectWithClientAndRateCard_DetailsShowsBoth_TimesheetsRecordsThreeHours()
    {
        const string organisationId = "ORG-20.10A-1";
        const string rateCardId = "CARD-20.10A-1";
        const string grade = "Senior";

        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndOrganisationAsync(
                host, organisationId, "Journey Client Ltd", rateCardId, "Journey Rate Card", grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectId = await CreateProjectViaRealDialogAsync(
                window, navigator, "New Project Journey", organisationId, rateCardId, openQuotation: false);

            // ---- The Details tab shows both, by name ----
            await navigator.OpenProjectAsync(projectId, ProjectArea.Details).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var details = window.FindUnique<ProjectDetailsView>();
            await RenderUntilAsync(window, () =>
                details.GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t => t.Text != null && t.Text.Contains("Journey Client Ltd", StringComparison.Ordinal)));

            var texts = details.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(texts, t => t.Contains("Journey Client Ltd", StringComparison.Ordinal));
            Assert.Contains(texts, t => t.Contains(rateCardId, StringComparison.Ordinal) && t.Contains("Journey Rate Card", StringComparison.Ordinal));

            // ---- Timesheets → Record lists the project and records 3h ----
            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Timesheets");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<TimesheetWeekView>().Any());
            LayOut(window);

            var week = window.GetLogicalDescendants().OfType<TimesheetWeekView>().Single();
            var recordButton = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
            recordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var prompt = GetPrivateField<TimesheetEntryPrompt>(window, "_timesheetEntryPrompt");
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var projectCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
            await RenderUntilAsync(window, () =>
                projectCombo.ItemsSource is not null && projectCombo.ItemsSource.Cast<ComboBoxItem>().Any(i => (Guid)i.Tag! == projectId));
            projectCombo.SelectedItem = projectCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == projectId);

            var gradeCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().Skip(1).First();
            var hoursUpDown = prompt.GetLogicalDescendants().OfType<NumericUpDown>().First();
            await RenderUntilAsync(window, () =>
                gradeCombo.IsEnabled && hoursUpDown.IsEnabled && gradeCombo.ItemsSource is not null && gradeCombo.ItemsSource.Cast<string>().Any());
            gradeCombo.SelectedItem = grade;
            hoursUpDown.Value = 3m;

            var taskBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
            taskBox.Text = "Design work";

            var record = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
            record.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible);

            var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
            IReadOnlyList<TimesheetEntry> entries = [];
            await RenderUntilAsync(window, () =>
            {
                entries = timesheets
                    .ListForPrincipalWeekAsync(host.SessionPrincipal!.IdentityId, TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now)))
                    .GetAwaiter().GetResult();
                return entries.Any(e => e.ProjectId == projectId && e.Hours == 3m);
            });
            Assert.Contains(entries, e => e.ProjectId == projectId && e.Hours == 3m && e.TaskDescription == "Design work");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>
    /// New Project with no rate card → Timesheets → Record disables
    /// Grade/Hours, states why inline, and Open Details closes Record and
    /// lands on that project's own Details tab.
    /// </summary>
    [AvaloniaFact]
    public async Task NewProjectWithNoRateCard_TimesheetsSaysWhy_OpenDetailsLandsOnDetailsTab()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectId = await CreateProjectViaRealDialogAsync(
                window, navigator, "No Rate Card Journey", organisationId: null, rateCardId: null, openQuotation: false);

            await navigator.GoToModuleAsync(ShellArea.Business);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            window.GetLogicalDescendants().OfType<BusinessAreaView>().Single().SelectNode("Timesheets");
            await RenderUntilAsync(window, () => window.GetLogicalDescendants().OfType<TimesheetWeekView>().Any());
            LayOut(window);

            var week = window.GetLogicalDescendants().OfType<TimesheetWeekView>().Single();
            var recordButton = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
            recordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var prompt = GetPrivateField<TimesheetEntryPrompt>(window, "_timesheetEntryPrompt");
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var projectCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
            await RenderUntilAsync(window, () =>
                projectCombo.ItemsSource is not null && projectCombo.ItemsSource.Cast<ComboBoxItem>().Any(i => (Guid)i.Tag! == projectId));
            projectCombo.SelectedItem = projectCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == projectId);

            var gradeCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().Skip(1).First();
            var hoursUpDown = prompt.GetLogicalDescendants().OfType<NumericUpDown>().First();
            await RenderUntilAsync(window, () => !gradeCombo.IsEnabled && !hoursUpDown.IsEnabled);
            Assert.False(gradeCombo.IsEnabled);
            Assert.False(hoursUpDown.IsEnabled);

            var messageTexts = prompt.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
            Assert.Contains(messageTexts, t => t.Contains("No rate card is pinned", StringComparison.Ordinal) && t.Contains("Details tab", StringComparison.Ordinal));

            var openDetails = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Open Details"));
            openDetails.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !prompt.IsVisible);
            await RenderUntilAsync(window, () =>
                navigator.Current.Area == ShellArea.ProjectWorkspace
                && navigator.Current.ProjectArea == ProjectArea.Details
                && navigator.Current.ProjectId == projectId);

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            Assert.Equal(ProjectArea.Details, navigator.Current.ProjectArea);
            Assert.Equal(projectId, navigator.Current.ProjectId);

            LayOut(window);
            var details = window.FindUnique<ProjectDetailsView>();
            await RenderUntilAsync(window, () => details.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Change client…")));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    /// <summary>Home → New Project runs the identical New Project flow and opens the created project right up.</summary>
    [AvaloniaFact]
    public async Task Home_NewProject_OpensTheNewProjectRightUp()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToModuleAsync(ShellArea.Home);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var home = window.GetLogicalDescendants().OfType<HomeDashboardView>().Single();
            var newProjectButton = home.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"));
            newProjectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var prompt = GetPrivateField<NewProjectPrompt>(window, "_newProjectPrompt");
            await RenderUntilAsync(window, () => prompt.IsVisible);

            var nameBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
            nameBox.Text = "Home New Project Journey";

            var openQuotationCheck = prompt.GetLogicalDescendants().OfType<CheckBox>().Single();
            openQuotationCheck.IsChecked = false;

            var ok = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
            ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            await RenderUntilAsync(window, () => !prompt.IsVisible && navigator.Current.ProjectId is not null);

            Assert.Equal(ShellArea.ProjectWorkspace, navigator.Current.Area);
            var createdId = navigator.Current.ProjectId!.Value;

            var created = await host.ProjectDirectory!.FindAsync(createdId).ConfigureAwait(true);
            Assert.NotNull(created);
            Assert.Contains("Home New Project Journey", created!.Label, StringComparison.Ordinal);

            LayOut(window);
            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            await RenderUntilAsync(window, () =>
                projectWorkspace.GetLogicalDescendants().OfType<TextBlock>().Any(t => t.Text == created.Label));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ---------------------------------------------------------------
    // Shared helpers
    // ---------------------------------------------------------------

    /// <summary>
    /// Drives the real New Project prompt end to end — name, Client (an
    /// existing organisation, or left unset), Rate card (a Released card,
    /// or "None", the drop-down's own first item) — and waits for the
    /// project it creates to actually be open, returning its own id.
    /// </summary>
    private static async Task<Guid> CreateProjectViaRealDialogAsync(
        MainWindow window, IShellNavigator navigator,
        string name, string? organisationId, string? rateCardId, bool openQuotation)
    {
        var browser = GetPrivateField<ProjectBrowserView>(window, "_projectBrowser");
        var newButton = browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"));
        newButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        var prompt = GetPrivateField<NewProjectPrompt>(window, "_newProjectPrompt");
        await RenderUntilAsync(window, () => prompt.IsVisible);

        var nameBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
        nameBox.Text = name;

        var combos = prompt.GetLogicalDescendants().OfType<ComboBox>().ToList();
        var clientCombo = combos[0];
        var rateCardCombo = combos[1];

        await RenderUntilAsync(window, () => clientCombo.ItemsSource is not null && rateCardCombo.ItemsSource is not null);

        if (organisationId is not null)
        {
            await RenderUntilAsync(window, () => clientCombo.ItemsSource!.Cast<ComboBoxItem>().Any(i => Equals(i.Tag, organisationId)));
            clientCombo.SelectedItem = clientCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => Equals(i.Tag, organisationId));
        }

        rateCardCombo.SelectedItem = rateCardId is not null
            ? rateCardCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => Equals(i.Tag, rateCardId))
            : rateCardCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => i.Tag is null);

        var openQuotationCheck = prompt.GetLogicalDescendants().OfType<CheckBox>().Single();
        openQuotationCheck.IsChecked = openQuotation;

        var ok = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
        ok.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        // `ProjectBrowserView.CreateAsync`'s own continuation opens the
        // created project right up once the prompt itself has closed — the
        // real navigation this test waits for, never a manual `RefreshAsync`.
        await RenderUntilAsync(window, () => !prompt.IsVisible && navigator.Current.ProjectId is not null);
        return navigator.Current.ProjectId!.Value;
    }

    private static async Task RegisterReleasedRateCardAndOrganisationAsync(
        WorkspaceHost host, string organisationId, string organisationName, string rateCardId, string rateCardName, string grade)
    {
        var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
        var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

        await organisations.RegisterAsync(
            organisationId, new Organisation { Reference = organisationId, Name = organisationName }, ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new RateCard
        {
            Code = rateCardId,
            Name = rateCardName,
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: grade)],
        };
        await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new ReferenceReviewStatement("Consulted for the journey test.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the journey test.").ConfigureAwait(true);
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = Deadline(20);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(MainWindow window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1900, 1050));
            window.Arrange(new Rect(0, 0, 1900, 1050));
        }
    }
}
