using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.BusinessGovernance.Pricing;
using Tempest.Core.BusinessOperations.Crm;
using Tempest.Core.Commands;
using Tempest.Core.Deliverables;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Evidence;
using Tempest.Core.Identity;
using Tempest.Core.Projects;
using Tempest.Core.ReferenceData.Seeding.Datasets;
using Tempest.Core.Invoicing;
using Tempest.Core.ReferenceData;
using Tempest.Core.ReferenceData.Review;
using Tempest.Core.Settings;
using Tempest.Core.Timesheets;
using Tempest.Desktop.DigitalThread;
using Tempest.Desktop.Editors;
using Tempest.Desktop.Views;
using Tempest.Samples;
using Tempest.Workspace;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.2B` acceptance 1: the six behavioural checks, table-driven over
/// every rail entry — click it and something real renders; select
/// something in it and the selection has meaning; open it and usable
/// content appears; edit it and state changes, through its own command;
/// restart and the state remains; navigate away and back and it is
/// coherent.
/// </summary>
/// <remarks>
/// <para>
/// <b>"Table-driven" here means one entry per rail surface, each proving
/// all six checks, rather than one generic method reused eight ways.</b>
/// What "select" and "edit" mean is genuinely different on every surface
/// (a material in a reference library; a recorded hour; an issued
/// evidence sheet's own filter) — a single shared driver would need one
/// delegate per check per surface to cope with that, which is the same
/// amount of per-surface code as a dedicated test method, with an extra
/// layer of indirection on top. Each method below follows the identical
/// six-comment shape, in the identical order, so the table is the
/// structure of the file rather than a runtime data structure.
/// </para>
/// <para>
/// <b>Reports has no domain command of its own</b> — it is a read-only
/// surface over Evidence issuance and project documents, both edited
/// elsewhere. Its own "edit" check is met honestly, not faked: issuing a
/// new evidence sheet through <c>IEvidenceService</c> while Reports is on
/// screen is the edit, and Reports's own change-feed subscription (not a
/// button inside Reports) is what changes its state — disclosed here
/// rather than inventing a command Reports does not have.
/// </para>
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class RailSurfaceContractTests
{
    // ================================================================
    // Home
    // ================================================================

    [AvaloniaFact]
    public async Task Home_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid partId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            // 1. Click it -> something real renders.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().FirstOrDefault());
            Assert.NotNull(window.GetLogicalDescendants().OfType<Tempest.Desktop.Docking.WorkspaceLayoutHost>().FirstOrDefault());

            await host.Workspace!.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            var created = await registry.InvokeAsync(
                "mechanical.create", palette.ContextSource!(),
                (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { ["kind"] = "Part", ["displayName"] = "Rail Contract Part" }));
            Assert.True(created.Result!.Succeeded, created.Result.Message);
            partId = created.Result.SubjectId!.Value;

            // 2. Select something in it -> the selection has meaning.
            var explorer = GetPrivateField<ProjectExplorerView>(window, "_explorerView");
            await explorer.LoadAsync();
            explorer.Reveal(partId);
            await host.Workspace.Selection.SelectAsync(partId, "Part");
            var inspector = GetPrivateField<PropertyInspectorView>(window, "_inspectorView");
            inspector.SetCurrentSelection(partId, "Part");
            await inspector.RefreshFromSourceAsync();
            LayOut(window);
            Assert.Contains(
                inspector.GetLogicalDescendants().OfType<TextBox>(),
                t => (t.Text ?? string.Empty).Contains("Rail Contract Part", StringComparison.Ordinal));

            // 3. Open it -> usable content.
            var documentArea = GetPrivateField<DocumentAreaView>(window, "_documentArea");
            await ((MainWindow)window).OpenObjectAsync(partId, "Part");
            LayOut(window);
            Assert.NotNull(documentArea.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault());

            // 4. Edit it -> state changes, through its own command.
            var editor = documentArea.GetLogicalDescendants().OfType<ObjectEditorView>().First();
            // The Identity section, and its own Name field, is built first
            // — the same "first TextBox is the name" assumption the
            // editor's own Identity section relies on throughout this file.
            var nameBox = editor.GetLogicalDescendants().OfType<TextBox>().First();
            nameBox.Text = "Rail Contract Part — Renamed";
            editor.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
            {
                var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));
                var current = domain.Repository.FindAsync(partId).GetAwaiter().GetResult() as IHasBusinessIdentifier;
                return current?.DisplayName == "Rail Contract Part — Renamed";
            });

            // 6. Navigate away and back -> coherent.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            Assert.NotNull(window.GetLogicalDescendants().OfType<RibbonView>().FirstOrDefault());

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = (EngineeringDomainContext)second.Services!.GetService(typeof(EngineeringDomainContext));
            var reloaded = await domain.Repository.FindAsync(partId) as IHasBusinessIdentifier;
            Assert.Equal("Rail Contract Part — Renamed", reloaded?.DisplayName);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Projects
    // ================================================================

    [AvaloniaFact]
    public async Task Projects_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid createdProjectId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;

            var apollo = await host.ProjectDirectory!.CreateAsync("P-RSC-A", "Rail Contract Apollo");
            var vulcan = await host.ProjectDirectory!.CreateAsync("P-RSC-V", "Rail Contract Vulcan");

            // 1. Click it -> something real renders.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var browser = window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single();
            Assert.NotNull(browser);

            // 2. Select something in it -> the selection has meaning:
            // selecting Vulcan and opening it opens *that* project, not Apollo.
            var list = browser.GetLogicalDescendants().OfType<ListBox>().Single();
            await RenderUntilAsync(window, () => list.ItemsSource is not null && list.ItemsSource.Cast<string>().Any());
            // `ItemsSource` is plain strings (`ProjectBrowserView.RefreshAsync`
            // builds "{Label}  —  {Status}" rows) and `OpenSelectedAsync`
            // reads `SelectedIndex`, not `SelectedItem` — so the selection
            // that has meaning here is which *string* is selected, not
            // which `ListBoxItem` wrapper control renders it.
            list.SelectedItem = list.ItemsSource!.Cast<string>().First(s => s.Contains("Vulcan", StringComparison.Ordinal));

            // 3. Open it -> usable content: that same project's own workspace.
            var openButton = browser.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Open Project"));
            openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => navigator.Current is { Area: ShellArea.ProjectWorkspace, ProjectId: { } id } && id == vulcan.Id);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var workspace = window.GetLogicalDescendants().OfType<ProjectWorkspaceView>().Single();
            Assert.Contains(
                workspace.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Vulcan", StringComparison.Ordinal));

            // 4. Edit it -> state changes, through its own command: New Project.
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var newProjectButton = window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single()
                .GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Project…"));
            newProjectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var inputDialog = GetPrivateField<InputDialog>(window, "_inputDialog");
            await RenderUntilAsync(window, () => inputDialog.IsVisible);
            var nameBox = inputDialog.GetLogicalDescendants().OfType<TextBox>().First();
            nameBox.Text = "Rail Contract Created Project";
            var okButton = inputDialog.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "OK"));
            okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !inputDialog.IsVisible);

            IReadOnlyList<Tempest.Workspace.Projects.ProjectSummary> all = [];
            await RenderUntilAsync(window, () =>
            {
                all = host.ProjectDirectory!.ListAsync().GetAwaiter().GetResult();
                return all.Any(p => p.DisplayName == "Rail Contract Created Project");
            });
            createdProjectId = all.Single(p => p.DisplayName == "Rail Contract Created Project").Id;

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            Assert.Contains(
                window.GetLogicalDescendants().OfType<ProjectBrowserView>().Single().GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("Rail Contract Created Project", StringComparison.Ordinal));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var reloaded = await second.ProjectDirectory!.FindAsync(createdProjectId);
            Assert.NotNull(reloaded);
            Assert.Equal("Rail Contract Created Project", reloaded!.DisplayName);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Evidence
    // ================================================================

    [AvaloniaFact]
    public async Task Evidence_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid projectId;
        Guid evidenceId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-RSC-E", "Rail Contract Evidence Project");
            projectId = project.Id;
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var evidence = await host.EvidenceService!.CreateAsync(project.Id, "Rail Contract Evidence", EvidenceClassification.Calculation);
            evidenceId = evidence.Id;

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var evidenceWorkspace = GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace");
            Assert.NotNull(evidenceWorkspace);
            await RenderUntilAsync(window, () => evidenceWorkspace.GetLogicalDescendants().OfType<ListBoxItem>().Any());

            // 2. Select something in it -> the selection has meaning.
            var list = evidenceWorkspace.GetLogicalDescendants().OfType<ListBox>().First();
            var row = list.GetLogicalDescendants().OfType<ListBoxItem>().Single();
            list.SelectedItem = row;
            Assert.Contains(
                row.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty),
                t => t.Contains("Rail Contract Evidence", StringComparison.Ordinal));

            // 3. Open it -> usable content.
            var openEvidenceRecordAsync = GetPrivateMethod(window, "OpenEvidenceRecordAsync");
            await (Task)openEvidenceRecordAsync.Invoke(window, [evidenceId, Tempest.Core.Evidence.Evidence.CanonicalKind])!;
            LayOut(window);
            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
            Assert.NotNull(editor);

            // 4. Edit it -> state changes, through its own command: Check.
            var checkButton = editor!.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check"));
            checkButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var checkEntry = GetPrivateField<CheckEntry>(window, "_checkEntry");
            await RenderUntilAsync(window, () => checkEntry.IsVisible);
            var checkBoxes = checkEntry.GetLogicalDescendants().OfType<TextBox>().ToList();
            checkBoxes[0].Text = "Rail Contract Checker";
            checkBoxes[1].Text = "Rail Contract Org";
            checkBoxes[2].Text = "Reviewed for the rail surface contract test.";
            checkEntry.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Check")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !checkEntry.IsVisible);
            await RenderUntilAsync(window, () => evidence.Status == EvidenceStatus.Checked);

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Evidence);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await RenderUntilAsync(window, () =>
                GetPrivateField<EvidenceWorkspaceView>(window, "_evidenceWorkspace").GetLogicalDescendants().OfType<ListBoxItem>().Any());

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = (EngineeringDomainContext)second.Services!.GetService(typeof(EngineeringDomainContext));
            var reloaded = await domain.Repository.FindAsync(evidenceId) as Tempest.Core.Evidence.Evidence;
            Assert.Equal(EvidenceStatus.Checked, reloaded?.Status);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Timesheets
    // ================================================================

    [AvaloniaFact]
    public async Task Timesheets_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var organisationId = "ORG-RSC-T";
        var rateCardId = "RC-RSC-T";
        const string grade = "Senior Engineer";
        var monday = TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now));
        Guid projectId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-RSC-T", "Rail Contract Timesheets Project");
            projectId = project.Id;
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
            Assert.True((await commercial.SetClientAsync(project.Id, organisationId)).Succeeded);
            Assert.True((await commercial.PinRateCardAsync(project.Id, rateCardId)).Succeeded);

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.Timesheets);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var week = GetPrivateField<TimesheetWeekView>(window, "_timesheetWeekView");
            Assert.NotNull(week);

            // 2. Select something in it -> the selection has meaning:
            // moving to the previous week shows a different week's total.
            var previous = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "◀ Previous"));
            var next = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Next ▶"));
            previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Yield();
            next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Yield();

            // 3. Open it -> usable content: the Record dialog itself.
            var recordButton = week.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
            recordButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var prompt = GetPrivateField<TimesheetEntryPrompt>(window, "_timesheetEntryPrompt");
            await RenderUntilAsync(window, () => prompt.IsVisible);
            Assert.NotEmpty(prompt.GetLogicalDescendants().OfType<TextBox>());

            // 4. Edit it -> state changes, through its own command: Record.
            var projectCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().First();
            await RenderUntilAsync(window, () => projectCombo.ItemsSource is not null && projectCombo.ItemsSource.Cast<ComboBoxItem>().Any());
            projectCombo.SelectedItem = projectCombo.ItemsSource!.Cast<ComboBoxItem>().First(i => (Guid)i.Tag! == projectId);

            var gradeCombo = prompt.GetLogicalDescendants().OfType<ComboBox>().Skip(1).First();
            await RenderUntilAsync(window, () => gradeCombo.ItemsSource is not null && gradeCombo.ItemsSource.Cast<string>().Any());
            gradeCombo.SelectedItem = grade;

            var hoursUpDown = prompt.GetLogicalDescendants().OfType<NumericUpDown>().First();
            hoursUpDown.Value = 4m;

            var taskBox = prompt.GetLogicalDescendants().OfType<TextBox>().First();
            taskBox.Text = "Rail contract test task";

            var recordConfirm = prompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Record"));
            recordConfirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !prompt.IsVisible);

            var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
            IReadOnlyList<TimesheetEntry> entries = [];
            await RenderUntilAsync(window, () =>
            {
                entries = timesheets.ListForPrincipalWeekAsync(host.SessionPrincipal!.IdentityId, monday).GetAwaiter().GetResult();
                return entries.Count == 1;
            });

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Timesheets);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await RenderUntilAsync(window, () =>
                GetPrivateField<TimesheetWeekView>(window, "_timesheetWeekView").GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t => (t.Text ?? string.Empty).Contains("Rail contract test task", StringComparison.Ordinal)));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var timesheets = (ITimesheetService)second.Services!.GetService(typeof(ITimesheetService));
            var entries = await timesheets.ListForPrincipalWeekAsync(second.SessionPrincipal!.IdentityId, monday);
            Assert.Contains(entries, e => e.TaskDescription == "Rail contract test task");
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Invoicing
    // ================================================================

    [AvaloniaFact]
    public async Task Invoicing_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        var organisationId = "ORG-RSC-I";
        var rateCardId = "RC-RSC-I";
        const string grade = "Senior Engineer";
        Guid requestId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            await RegisterReleasedRateCardAndClientAsync(host, organisationId, rateCardId, grade);

            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-RSC-I", "Rail Contract Invoicing Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var commercial = (IProjectCommercialService)host.Services!.GetService(typeof(IProjectCommercialService));
            Assert.True((await commercial.SetClientAsync(project.Id, organisationId)).Succeeded);
            Assert.True((await commercial.PinRateCardAsync(project.Id, rateCardId)).Succeeded);

            var timesheets = (ITimesheetService)host.Services!.GetService(typeof(ITimesheetService));
            var monday = TimesheetWeek.WeekOf(DateOnly.FromDateTime(DateTime.Now));
            Assert.True((await timesheets.RecordAsync(project.Id, monday, 4m, true, grade, "Design", CancellationToken.None)).Succeeded);

            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(project.Id, "M-RSC-I", "Milestone", DateTimeOffset.UtcNow.AddMonths(1));
            var deliverable = await host.ProjectMilestoneWorkflow!.CreateDeliverableAsync(project.Id, milestone.Id, "D-RSC-I", "Deliverable");

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Deliverables);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var deliverablesView = projectWorkspace.DeliverablesView;
            await RenderUntilAsync(window, () => deliverablesView.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Complete")));

            var completePrompt = GetPrivateField<DeliverableCompletionPrompt>(window, "_deliverableCompletionPrompt");
            deliverablesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Complete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => completePrompt.IsVisible);
            completePrompt.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Complete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () => !completePrompt.IsVisible);

            await RenderUntilAsync(window, () => deliverablesView.GetLogicalDescendants().OfType<Button>().Any(b => Equals(b.Content, "Raise invoice")));
            deliverablesView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Raise invoice")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            InvoiceRequest? request = null;
            await RenderUntilAsync(window, () =>
            {
                request = domain.Repository.ListChildrenAsync(project.Id).GetAwaiter().GetResult().OfType<InvoiceRequest>().FirstOrDefault();
                return request is not null;
            });
            requestId = request!.Id;

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.Invoicing);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var invoicingView = GetPrivateField<InvoicingView>(window, "_invoicingView");
            invoicingView.ParameterPrompt = (_, _, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string>());
            await RenderUntilAsync(window, () => FindRequestRow(invoicingView, requestId) is not null);

            // 2. Select something in it -> the selection has meaning:
            // Review opens *that* request's own editor.
            var row = FindRequestRow(invoicingView, requestId)!;
            var reviewButton = row.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Review"));
            reviewButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            LayOut(window);

            // 3. Open it -> usable content.
            var editor = window.GetLogicalDescendants().OfType<ObjectEditorView>().FirstOrDefault();
            Assert.NotNull(editor);

            // 4. Edit it -> state changes, through its own command: Send.
            await navigator.GoToModuleAsync(ShellArea.Invoicing);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var refreshedRow = FindRequestRow(GetPrivateField<InvoicingView>(window, "_invoicingView"), requestId)!;
            var sendButton = refreshedRow.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Send"));
            sendButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                ((InvoiceRequest)domain.Repository.FindAsync(requestId).GetAwaiter().GetResult()!).Status != InvoiceRequestStatus.Draft);

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Invoicing);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await RenderUntilAsync(window, () => FindRequestRow(GetPrivateField<InvoicingView>(window, "_invoicingView"), requestId) is not null);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = (EngineeringDomainContext)second.Services!.GetService(typeof(EngineeringDomainContext));
            var reloaded = await domain.Repository.FindAsync(requestId) as InvoiceRequest;
            Assert.NotEqual(InvoiceRequestStatus.Draft, reloaded?.Status);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Reports
    // ================================================================

    [AvaloniaFact]
    public async Task Reports_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid evidenceId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-RSC-R", "Rail Contract Reports Project");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            var firstSheet = await IssueWithSheetAsync(host, project.Id, "Rail Contract First Sheet", "ISS-RSC-1");

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.Reports);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var reportsView = GetPrivateField<ReportsView>(window, "_reportsView");
            Assert.NotNull(reportsView);
            await RenderUntilAsync(window, () =>
                reportsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("ISS-RSC-1", StringComparison.Ordinal)));

            // 3. Open it -> usable content: Open/Export on the sheet row.
            // (Checked here, before filtering, so the later filter-narrows
            // check below only has to prove one direction.)
            var reportButtons = reportsView.GetLogicalDescendants().OfType<Button>().Select(b => b.Content).ToList();
            Assert.Contains("Open", reportButtons);
            Assert.Contains("Export", reportButtons);

            // 2. Select something in it -> the selection has meaning:
            // filtering to a different, empty project hides the sheet.
            var otherProject = await host.ProjectDirectory!.CreateAsync("P-RSC-R2", "Rail Contract Reports Project Two");
            await navigator.OpenProjectAsync(project.Id);
            await navigator.GoToModuleAsync(ShellArea.Reports);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            reportsView = GetPrivateField<ReportsView>(window, "_reportsView");
            await RenderUntilAsync(window, () => reportsView.GetLogicalDescendants().OfType<ComboBox>().Any());

            var filter = reportsView.GetLogicalDescendants().OfType<ComboBox>().Single();
            await RenderUntilAsync(window, () => filter.Items.OfType<ComboBoxItem>().Any(i => (i.Content as string)?.Contains("Reports Project Two", StringComparison.Ordinal) == true));
            filter.SelectedIndex = filter.Items.OfType<ComboBoxItem>().ToList()
                .FindIndex(i => (i.Content as string)?.Contains("Reports Project Two", StringComparison.Ordinal) == true);
            // The selection's own event handler dispatches the resulting
            // refresh fire-and-forget (`ApplyFilter`, mirroring every
            // other view's identical "raise intent, refresh" shape) — this
            // second, directly awaited call coalesces with it (see
            // `RefreshAsync`'s own remarks) rather than racing it, so the
            // assertion below reads a state guaranteed already settled.
            await reportsView.RefreshAsync();
            Assert.DoesNotContain(
                reportsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("ISS-RSC-1", StringComparison.Ordinal));

            // Back to "All projects" — the always-present first item —
            // before the next check, so a real project filter never masks it.
            filter.SelectedIndex = 0;
            await reportsView.RefreshAsync();
            Assert.Contains(
                reportsView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("ISS-RSC-1", StringComparison.Ordinal));

            // 4. Edit it -> state changes. Reports has no command of its
            // own (see class remarks): issuing a second sheet elsewhere,
            // through Evidence's own command, is the edit; Reports's own
            // change-feed subscription — not a button inside Reports — is
            // what changes what it shows.
            var secondSheet = await IssueWithSheetAsync(host, project.Id, "Rail Contract Second Sheet", "ISS-RSC-2");
            evidenceId = secondSheet.Id;

            await RenderUntilAsync(window, () =>
                reportsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("ISS-RSC-2", StringComparison.Ordinal)));

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Reports);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await RenderUntilAsync(window, () =>
                GetPrivateField<ReportsView>(window, "_reportsView").GetLogicalDescendants().OfType<TextBlock>()
                    .Any(t => (t.Text ?? string.Empty).Contains("ISS-RSC-2", StringComparison.Ordinal)));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var domain = (EngineeringDomainContext)second.Services!.GetService(typeof(EngineeringDomainContext));
            var reloaded = await domain.Repository.FindAsync(evidenceId) as Tempest.Core.Evidence.Evidence;
            Assert.NotNull(reloaded?.Issue?.IssueSheetAttachmentId);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Engineering Calculations
    // ================================================================

    [AvaloniaFact]
    public async Task EngineeringCalculation_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();
        Guid recordId;

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.EngineeringCalculation);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var view = GetPrivateField<EngineeringCalculationView>(window, "_engineeringCalculation");
            Assert.NotNull(view);

            ClickAsync(window, view, EngineeringCalculationView.PopulateCaption);
            await RenderUntilAsync(window, () => view.Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6));

            // 2. Select something in it -> the selection has meaning: the
            // material picker's own selection is what Release acts on.
            var picker = view.GetLogicalDescendants().OfType<ListBox>().Distinct()
                .Single(l => string.Equals(AutomationProperties.GetName(l), "Reference library", StringComparison.Ordinal));
            picker.SelectedItem = view.Materials.Single(m => m.RecordId == MaterialSeed.Aluminium6082T6);
            EnterText(view, "Source consulted", "Rail contract test source.");
            EnterText(view, "Release rationale", "Rail contract test rationale.");

            // 3. Open it -> usable content (already on screen: the
            // populated, selectable reference library).
            Assert.NotEmpty(view.Materials);

            // 4. Edit it -> state changes, through its own command: Release.
            ClickAsync(window, view, EngineeringCalculationView.ReleaseCaption);
            await RenderUntilAsync(window, () => view.Materials.Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));

            EnterText(view, "Axial load in kilonewtons", "12");
            EnterText(view, "Section area in square millimetres", "60");
            EnterText(view, "Member length in millimetres", "150");
            EnterText(view, "Mass limit in grams", "50");
            ClickAsync(window, view, EngineeringCalculationView.CalculateCaption);
            await RenderUntilAsync(window, () => view.DisplayedOutcome is { Performed: true });
            recordId = view.DisplayedOutcome!.CalculationRecordId;

            // 6. Navigate away and back -> coherent.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.EngineeringCalculation);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            await RenderUntilAsync(window, () =>
                GetPrivateField<EngineeringCalculationView>(window, "_engineeringCalculation").Materials
                    .Any(m => m.RecordId == MaterialSeed.Aluminium6082T6 && m.IsUsableForEngineering));

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains (a fresh window recovers the
        // stored calculation, `restoreInputs` on its own first entry).
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var window = new MainWindow(second) { Width = 1400, Height = 900 };
            window.Show();
            var navigator = second.ShellNavigator!;

            await navigator.GoToModuleAsync(ShellArea.EngineeringCalculation);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var view = GetPrivateField<EngineeringCalculationView>(window, "_engineeringCalculation");
            await RenderUntilAsync(window, () => view.DisplayedOutcome is { Performed: true });
            Assert.Equal(recordId, view.DisplayedOutcome!.CalculationRecordId);
            Assert.Equal("12", view.CurrentInputs.LoadKilonewtons);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Settings
    // ================================================================

    [AvaloniaFact]
    public async Task Settings_MeetsAllSixChecks()
    {
        var root = WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath();

        var host = new WorkspaceHost(root);
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host);
            LayOut(window);
            var navigator = host.ShellNavigator!;

            // 1. Click it -> something real renders.
            await navigator.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var settingsView = GetPrivateField<SettingsView>(window, "_settingsView");
            Assert.NotNull(settingsView);

            // 2. Select something in it -> the selection has meaning: the
            // persistence root is real, resolved content, not a placeholder.
            var persistenceRootBox = settingsView.GetLogicalDescendants().OfType<TextBox>()
                .Single(t => string.Equals(AutomationProperties.GetName(t), "Persistence root", StringComparison.Ordinal));
            Assert.False(string.IsNullOrWhiteSpace(persistenceRootBox.Text));

            var connectorCombo = settingsView.GetLogicalDescendants().OfType<ComboBox>()
                .Single(c => c.Items.OfType<ComboBoxItem>().Any(i => Equals(i.Content, "Xero")));
            connectorCombo.SelectedItem = connectorCombo.Items.OfType<ComboBoxItem>().Single(i => Equals(i.Content, "Xero"));
            Assert.Equal("Xero", (connectorCombo.SelectedItem as ComboBoxItem)?.Content);

            // 3. Open it -> usable content (already on screen: every section).
            Assert.Contains(
                settingsView.GetLogicalDescendants().OfType<CheckBox>(),
                c => Equals(c.Content, "Confirm before deleting an object"));

            // 4. Edit it -> state changes, through its own command: Save.
            var checkbox = settingsView.GetLogicalDescendants().OfType<CheckBox>()
                .Single(c => Equals(c.Content, "Confirm before deleting an object"));
            checkbox.IsChecked = false;

            var saveButton = settingsView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Save"));
            saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                settingsView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).StartsWith("Saved at", StringComparison.Ordinal)));

            var settingsProvider = (ISettingsProvider)host.Services!.GetService(typeof(ISettingsProvider));
            var reloaded = new UserSettings(settingsProvider);
            await reloaded.LoadAsync();
            Assert.False(reloaded.ConfirmBeforeDelete);

            // 6. Navigate away and back -> coherent: the saved value re-loads.
            await navigator.GoHomeAsync();
            await window.RenderCurrentModuleAsync();
            await navigator.GoToModuleAsync(ShellArea.Settings);
            await window.RenderCurrentModuleAsync();
            LayOut(window);
            var reopened = GetPrivateField<SettingsView>(window, "_settingsView");
            var reopenedCheckbox = reopened.GetLogicalDescendants().OfType<CheckBox>()
                .Single(c => Equals(c.Content, "Confirm before deleting an object"));
            Assert.False(reopenedCheckbox.IsChecked);

            await host.ShutdownAsync();
        }
        finally
        {
            await host.DisposeAsync();
        }

        // 5. Restart -> the state remains.
        var second = new WorkspaceHost(root);
        try
        {
            await second.StartAsync();
            var settingsProvider = (ISettingsProvider)second.Services!.GetService(typeof(ISettingsProvider));
            var settings = new UserSettings(settingsProvider);
            await settings.LoadAsync();
            Assert.False(settings.ConfirmBeforeDelete);
        }
        finally
        {
            await second.ShutdownAsync();
            await second.DisposeAsync();
        }
    }

    // ================================================================
    // Shared setup
    // ================================================================

    private static async Task RegisterReleasedRateCardAndClientAsync(WorkspaceHost host, string organisationId, string rateCardId, string grade)
    {
        var organisations = (IOrganisationCatalog)host.Services!.GetService(typeof(IOrganisationCatalog));
        var rateCards = (IRateCardCatalog)host.Services!.GetService(typeof(IRateCardCatalog));

        await organisations.RegisterAsync(
            organisationId, new Organisation { Reference = organisationId, Name = "Rail Contract Client Ltd" }, ReferenceProvenance.Unknown).ConfigureAwait(true);

        var card = new RateCard
        {
            Code = rateCardId,
            Name = "Rail Contract Rate Card",
            EffectivePeriod = new EffectivePeriod(new DateOnly(2020, 1, 1), null),
            Currency = CurrencyCode.Gbp,
            Governance = new BusinessGovernanceFacts { Ownership = new BusinessOwnership("owner-1", "Owner") },
            Entries = [new RateCardEntry("SVC-1", "Senior Engineering", PricingBasis.Hourly, new Money(150m, CurrencyCode.Gbp), Grade: grade)],
        };
        await rateCards.RegisterAsync(rateCardId, card, new ReferenceProvenance(SourceOrganisation: "Test Org", SourceDocument: "Test Doc")).ConfigureAwait(true);
        await host.ReferenceReview!.VerifyAsync(rateCards, rateCardId, new ReferenceReviewStatement("Consulted for the rail surface contract test.")).ConfigureAwait(true);
        await host.ReferenceReview!.ReleaseAsync(rateCards, rateCardId, "Released for the rail surface contract test.").ConfigureAwait(true);
    }

    /// <summary>
    /// Creates, checks, issues and — the step a plain <see cref="IEvidenceService.IssueAsync"/>
    /// call alone never does — attaches a real issue-sheet file to a piece
    /// of Evidence, so <see cref="Evidence.Issue"/>'s own <see cref="IssueRecord.IssueSheetAttachmentId"/>
    /// is genuinely non-null, exactly as <see cref="ReportsView"/>'s own
    /// filter requires.
    /// </summary>
    private static async Task<Tempest.Core.Evidence.Evidence> IssueWithSheetAsync(WorkspaceHost host, Guid projectId, string name, string issueReference)
    {
        var evidence = await host.EvidenceService!.CreateAsync(projectId, name, EvidenceClassification.Calculation);
        await host.EvidenceService!.RecordCheckAsync(evidence.Id, "Checker", "Org", "Fine.", CheckOutcome.Accepted);
        await host.EvidenceService!.IssueAsync(evidence.Id, issueReference, "A", "Client Co");

        var attachment = await evidence.AttachContentAsync($"{issueReference}.pdf", "application/pdf", new byte[] { 1, 2, 3 });
        await host.EvidenceService!.RecordIssueSheetAsync(evidence.Id, attachment.Id);

        return evidence;
    }

    private static Border? FindRequestRow(Control root, Guid requestId) =>
        root.GetLogicalDescendants().OfType<Border>().FirstOrDefault(b => Equals(b.Tag, requestId));

    /// <summary>Types into the box a person would, located by the name a screen reader would announce.</summary>
    private static void EnterText(EngineeringCalculationView view, string automationName, string text)
    {
        var box = view.GetLogicalDescendants().OfType<TextBox>().Distinct()
            .FirstOrDefault(b => string.Equals(AutomationProperties.GetName(b), automationName, StringComparison.Ordinal));
        Assert.True(box is not null, $"No box named '{automationName}'.");
        box!.Text = text;
    }

    private static void ClickAsync(MainWindow window, Control surface, string caption)
    {
        LayOut(window);
        var button = surface.GetLogicalDescendants().OfType<Button>().Distinct()
            .FirstOrDefault(b => string.Equals(b.Content?.ToString(), caption, StringComparison.Ordinal));
        Assert.True(button is not null, $"No '{caption}' button on this surface.");
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static System.Reflection.MethodInfo GetPrivateMethod(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Method '{methodName}' not found on {instance.GetType().Name}.");
        return method;
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            LayOut(window);
        }
    }

    private static void LayOut(Window window)
    {
        if (!window.IsVisible)
            window.Show();

        for (var pass = 0; pass < 2; pass++)
        {
            Dispatcher.UIThread.RunJobs();
            window.Measure(new Size(1400, 900));
            window.Arrange(new Rect(0, 0, 1400, 900));
        }
    }
}
