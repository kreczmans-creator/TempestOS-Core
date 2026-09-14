using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Tempest.Core.BusinessGovernance;
using Tempest.Core.Commands;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Projects;
using Tempest.Core.Quotations;
using Tempest.Desktop.Views;
using Tempest.Workspace.Mechanical;
using Tempest.Workspace.Shell;

namespace Tempest.Desktop.Tests;

/// <summary>
/// `WP 19.10H`, `TD-179`: the project workspace stops contradicting its own
/// "Archived — nothing here can be changed" banner. Opens a project closed
/// 91 days ago (Archive, not merely Closed — <see cref="ProjectArchival.ArchiveAfterDays"/>
/// is 90), through the real window, and walks the tabs this Work Package
/// owns: every write control it disables is checked for its disabled state
/// and its "Archived project — read only" tooltip; New Quote (a write the
/// brief's own list of controls to disable does not name — its service,
/// <see cref="QuotationService"/>, already refuses it) stays clickable.
/// Reopen is checked separately, below.
/// </summary>
/// <remarks>
/// <b>`WP 19.10R` extension (`TD-179`'s residual).</b> The two routes
/// `WP 19.10H` could not close because neither has its own notion of
/// "archived" — the Structure tab's Ribbon and the Command Palette, both
/// of which act only through <see cref="ICommandRegistry"/> — are added to
/// the same journey below: a Part created while the project is still
/// open, then, once the project is Archive, every mutating Mechanical
/// Ribbon button reachable with that Part selected is disabled with the
/// registry's own "Project '{code}' is archived — read only." reason, and
/// the Palette lists <c>mechanical.create</c> with the identical reason
/// and refuses it on <c>Enter</c>.
/// </remarks>
/// <remarks>
/// <b>Reopen, on a project genuinely 91 days closed, cannot both stay
/// live and succeed.</b> <see cref="IProjectLifecycleService.ReopenAsync"/>
/// refuses once <see cref="ProjectArchival.IsArchived"/> is true — the
/// identical fact this test's own disabled-controls assertions depend on,
/// read from the same real clock. A reopen that succeeded would mean the
/// project was not actually Archive, and the controls above would not have
/// been disabled to begin with. So rather than fabricate a scenario the
/// domain's own 90-day rule forbids, this test proves the other honest
/// half: Reopen stays enabled (not disabled by the new archived-flag
/// mechanism — it is deliberately left out of every list of controls this
/// Work Package's brief disables) and reaches the real
/// <see cref="IProjectLifecycleService"/>, which answers with the same
/// refusal <c>PHYSICAL_REVIEW.md</c> §7c step D19 already documents
/// ("it is Archive and read-only. It cannot be reopened.") — a live
/// control giving an honest answer, not a dead one.
/// </remarks>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ArchivedProjectReadOnlyTests
{
    [AvaloniaFact]
    public async Task AnArchivedProject_DisablesEveryReachableWriteControl_WithTheTooltip_AndReopenStaysLiveButIsHonestlyRefused()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var window = new MainWindow(host, new StubFilePicker());
            LayOut(window);
            var navigator = host.ShellNavigator!;
            var domain = (EngineeringDomainContext)host.Services!.GetService(typeof(EngineeringDomainContext));

            await navigator.GoToProjectsAsync();
            await window.RenderCurrentModuleAsync();
            var project = await host.ProjectDirectory!.CreateAsync("P-ARCH-1", "Archived Project Journey");
            await navigator.OpenProjectAsync(project.Id);
            await window.RenderCurrentModuleAsync();

            // ================================================================
            // One entry per tab, created while the project is still Open —
            // every write below is refused by the guard this Work Package
            // added to the services underneath, but the point of this test
            // is the shell: the control itself must read as disabled.
            // ================================================================
            var task = await host.ProjectTaskWorkflow!.CreateAsync(project.Id, "TSK-ARCH", "Balance the impeller");
            var milestone = await host.ProjectMilestoneWorkflow!.CreateMilestoneAsync(
                project.Id, "MS-ARCH", "Design freeze", DateTimeOffset.UtcNow.AddDays(30));

            var quotationService = (IQuotationService)host.Services!.GetService(typeof(IQuotationService));

            var draft = await quotationService.CreateAsync(project.Id, "Q-ARCH-DRAFT");
            Assert.True(draft.Succeeded, draft.Reason);
            var draftQuoteId = draft.Quotation!.Id;
            await quotationService.AddLineAsync(draftQuoteId, "Concept design", 10m, new Money(100m, draft.Quotation.Currency), null);

            var toBeSent = await quotationService.CreateAsync(project.Id, "Q-ARCH-SENT");
            Assert.True(toBeSent.Succeeded, toBeSent.Reason);
            var sentQuoteId = toBeSent.Quotation!.Id;
            await quotationService.AddLineAsync(sentQuoteId, "Detailed pack", null, null, new Money(2500m, toBeSent.Quotation.Currency));
            var sendResult = await quotationService.SendAsync(sentQuoteId);
            Assert.True(sendResult.Succeeded, sendResult.Reason);

            // `WP 19.10R`: a Part for the Structure tab's own Ribbon/Palette
            // checks below — created directly against the domain, exactly
            // as the task/milestone/quotations above are, while the
            // project is still open.
            var part = await new MechanicalObjectFactoryRegistry(domain).CreateAsync(
                MechanicalObjectFactoryRegistry.Part, "PT-ARCH", "Archived Bracket", "Initial content.", parentId: project.Id);

            // ================================================================
            // Close the project 91 days ago — Archive, not merely Closed.
            // ================================================================
            var lifecycle = new ProjectLifecycleService(domain, new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(-91)));
            var closed = await lifecycle.SignOffAsync(project.Id, "Closed for the archived-read-only journey test.");
            Assert.True(closed.Succeeded, closed.Reason);
            var closedOnBefore = ((Project)(await domain.Repository.FindAsync(project.Id))!).ClosedOn;

            // ================================================================
            // Re-enter the way the application does — walking two tabs
            // explicitly, then the shared workspace refresh (every tab's
            // own content is rebuilt on every entry, `ProjectWorkspaceView.RefreshAsync`)
            // covers the rest.
            // ================================================================
            await navigator.OpenProjectAsync(project.Id, ProjectArea.Tasks).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var projectWorkspace = GetPrivateField<ProjectWorkspaceView>(window, "_projectWorkspace");
            var tasksView = projectWorkspace.TasksView;
            await RenderUntilAsync(window, () => tasksView.Entries.Count == 1);

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Timeline).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            var timelineView = projectWorkspace.TimelineView;
            await RenderUntilAsync(window, () => timelineView.Milestones.Count == 1);
            LayOut(window);

            // ---- Tasks tab: New Task, and every per-entry write control ----
            AssertDisabledWithTooltip(tasksView, "New Task");
            AssertDisabledWithTooltip(tasksView, "Edit");
            AssertDisabledWithTooltip(tasksView, "Assign to me");
            AssertDisabledWithTooltip(tasksView, "Due date");
            AssertDisabledWithTooltip(tasksView, "Done"); // the work-state move a fresh Todo task offers

            // ---- Timeline tab: Set Milestone, Edit, Add Deliverable ----
            AssertDisabledWithTooltip(timelineView, "Set Milestone");
            AssertDisabledWithTooltip(timelineView, "Edit");
            AssertDisabledWithTooltip(timelineView, "Add Deliverable");

            // ---- Evidence tab: Create ----
            var evidenceView = window.GetLogicalDescendants().OfType<EvidenceWorkspaceView>().First();
            AssertDisabledWithTooltip(evidenceView, "Create");

            // ---- Quote tab: Add line, Send, Accept, Decline (and Edit/Remove on lines) ----
            var quoteView = projectWorkspace.QuoteView;
            await quoteView.SelectQuoteAsync(draftQuoteId).ConfigureAwait(true);
            LayOut(window);
            AssertDisabledWithTooltip(quoteView, "Add line");
            AssertDisabledWithTooltip(quoteView, "Edit");
            AssertDisabledWithTooltip(quoteView, "Remove");
            AssertDisabledWithTooltip(quoteView, "Send");

            // New Quote is deliberately not in the brief's own list of
            // controls to disable — QuotationService.CreateAsync already
            // refuses it on an archived project, so this proves the button
            // itself was left alone, not forgotten.
            var newQuoteButton = quoteView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "New Quote"));
            Assert.True(newQuoteButton.IsEnabled, "New Quote is not in this Work Package's own disabled-control list.");

            await quoteView.SelectQuoteAsync(sentQuoteId).ConfigureAwait(true);
            LayOut(window);
            AssertDisabledWithTooltip(quoteView, "Accept");
            AssertDisabledWithTooltip(quoteView, "Decline");

            // ---- Sign off tab: Reopen stays live, and is honestly refused ----
            var signOffView = window.GetLogicalDescendants().OfType<ProjectSignOffView>().First();
            var reopenButton = signOffView.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, "Reopen"));
            Assert.True(reopenButton.IsVisible, "Reopen must still be visible once closed.");
            Assert.True(reopenButton.IsEnabled, "Reopen must stay enabled — it is not in this Work Package's own disabled-control list, and reaches a service that already refuses it correctly.");

            reopenButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RenderUntilAsync(window, () =>
                signOffView.GetLogicalDescendants().OfType<TextBlock>().Any(t => (t.Text ?? string.Empty).Contains("cannot be reopened", StringComparison.Ordinal)));
            LayOut(window);

            Assert.Contains(
                signOffView.GetLogicalDescendants().OfType<TextBlock>(),
                t => (t.Text ?? string.Empty).Contains("cannot be reopened", StringComparison.Ordinal));

            var reloadedProject = (Project)(await domain.Repository.FindAsync(project.Id))!;
            Assert.Equal(closedOnBefore, reloadedProject.ClosedOn); // the refused Reopen changed nothing

            // The refused Reopen changed nothing domain-side, so every
            // control checked above is still exactly as disabled as it was.
            AssertDisabledWithTooltip(tasksView, "New Task");
            AssertDisabledWithTooltip(timelineView, "Set Milestone");
            AssertDisabledWithTooltip(evidenceView, "Create");

            // ================================================================
            // `WP 19.10R` (`TD-179`'s residual): the Structure tab's own
            // Ribbon, and the Command Palette — the two routes that acted
            // only through `ICommandRegistry`, which carried no
            // archived-project check before this Work Package.
            // ================================================================
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            await navigator.OpenProjectAsync(project.Id, ProjectArea.Engineering).ConfigureAwait(true);
            await window.RenderCurrentModuleAsync();
            LayOut(window);

            // Selected exactly as `SurfaceCommandIntegrationTests.Ribbon_Enablement_ComesFromEvaluate_NotFromTheIdsTrailingWord`
            // already establishes: `workspace.Selection.SelectAsync` is the
            // real selection service every ribbon button's own enablement
            // reads from; `RefreshEnablement` is the ribbon's own public
            // "the selection changed, recompute" entry point, called from
            // more than one production site already (`Rebuild`, a Ribbon
            // delete), never a view's own content-loading `RefreshAsync`.
            await host.Workspace!.Selection.SelectAsync(part.Id, "Part").ConfigureAwait(true);
            var ribbon = GetPrivateField<RibbonView>(window, "_ribbon");
            ribbon.RefreshEnablement();

            const string ArchivedCommandReason = "Project 'P-ARCH-1' is archived — read only.";

            // Every mutating Mechanical Ribbon button reachable with the
            // Part selected — Rename/Edit route to the Object Editor before
            // ever reading Evaluate, so they are not ribbon-disabled by
            // this guard and are covered by the Palette's own identical
            // Evaluate call below instead; Duplicate/Set BOM Line/Create
            // dispatch straight through the registry and are.
            AssertRibbonButtonDisabledWithReason(ribbon, registry, "mechanical.duplicate", ArchivedCommandReason);
            AssertRibbonButtonDisabledWithReason(ribbon, registry, "mechanical.set-bom-line", ArchivedCommandReason);
            AssertRibbonButtonDisabledWithReason(ribbon, registry, "mechanical.create", ArchivedCommandReason);

            // Rename/Edit still refuse through the identical Evaluate path
            // the Palette (and a macro) would use — proven directly against
            // the registry, since the Ribbon routes them to the Object
            // Editor before ever asking (`SurfaceCommandPolicy`).
            var renameContext = CommandContext.For(part.Id, "Part");
            var renameAvailability = registry.Evaluate("mechanical.rename", renameContext);
            Assert.False(renameAvailability.IsAvailable);
            Assert.Equal(ArchivedCommandReason, renameAvailability.Reason);

            // The Palette lists the same Create command with the same
            // reason, and refuses it on Enter — a macro replaying either
            // route stops with this identical reason.
            var palette = GetPrivateField<CommandPaletteOverlay>(window, "_commandPalette");
            palette.Open("Create Mechanical Object");

            var panel = (StackPanel)palette.Child!;
            var queryBox = (TextBox)panel.Children[0];
            var results = (ListBox)panel.Children[1];
            var items = (IReadOnlyList<ListBoxItem>)results.ItemsSource!;
            var createRowIndex = items.ToList().FindIndex(
                i => i.Content is string s && s.Contains("Create Mechanical Object", StringComparison.Ordinal));

            Assert.True(createRowIndex >= 0, "Expected the Palette to list 'Create Mechanical Object'.");
            var createRow = items[createRowIndex];
            Assert.False(createRow.IsEnabled);
            Assert.Contains(ArchivedCommandReason, (string)createRow.Content!, StringComparison.Ordinal);

            CommandDescriptor? unavailableDescriptor = null;
            string? unavailableReason = null;
            palette.CommandUnavailable += (descriptor, reason) => { unavailableDescriptor = descriptor; unavailableReason = reason; };

            results.SelectedIndex = createRowIndex;
            queryBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

            Assert.NotNull(unavailableDescriptor);
            Assert.Equal("mechanical.create", unavailableDescriptor!.Id);
            Assert.Equal(ArchivedCommandReason, unavailableReason);
            Assert.False(palette.IsOpen); // closes exactly as a real invocation would

            // Nothing was created under the archived project: the refused
            // Enter never even reached a prompt (the Palette's own stored,
            // render-time `Availability` short-circuits `InvokeSelectedAsync`
            // before any value is collected), so the Part above is still
            // the project's only direct child.
            var contentsAfterRefusedCreate = await domain.Repository.ListChildrenAsync(project.Id);
            Assert.Single(contentsAfterRefusedCreate, o => o.Id == part.Id);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    private static void AssertDisabledWithTooltip(Control root, string buttonContent)
    {
        var button = root.GetLogicalDescendants().OfType<Button>().Single(b => Equals(b.Content, buttonContent));
        Assert.False(button.IsEnabled, $"'{buttonContent}' should be disabled on an archived project.");
        Assert.Equal("Archived project — read only", ToolTip.GetTip(button));
    }

    /// <summary>
    /// `WP 19.10R`: a Ribbon command button's own <c>Content</c> is an
    /// icon+label <c>StackPanel</c>, never a plain string
    /// (<see cref="AssertDisabledWithTooltip"/>'s own match-by-Content
    /// would never find one) — found the same way every other Ribbon test
    /// does, by its command Id, scoped to that command's own discipline
    /// tab (<c>DesktopTestHelpers.FindButton</c>).
    /// </summary>
    private static void AssertRibbonButtonDisabledWithReason(RibbonView ribbon, ICommandRegistry registry, string commandId, string reason)
    {
        var button = DesktopTestHelpers.FindButton(ribbon, registry, commandId);
        Assert.False(button.IsEnabled, $"'{commandId}' should be disabled on an archived project.");
        Assert.Equal(reason, ToolTip.GetTip(button));
    }

    private static T GetPrivateField<T>(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field '{fieldName}' not found on {instance.GetType().Name}.");
        return (T)field.GetValue(instance)!;
    }

    private static async Task RenderUntilAsync(MainWindow window, Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(15);
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

    /// <summary>A clock the test pins — mirrors <c>Tempest.Core.Tests.FakeTimeProvider</c>, which this assembly cannot reference (it is <see langword="internal"/> to a different assembly).</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset now) => _now = now;

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
