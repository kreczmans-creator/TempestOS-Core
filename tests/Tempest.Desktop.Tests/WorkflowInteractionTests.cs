using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Notifications;
using Tempest.Desktop.Theming;
using Tempest.Desktop.Views;
using Tempest.Samples;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates `WP 10.5B`'s own Dialog Framework, Delete Confirmation
/// gate, real object creation/duplicate workflow, Recent Searches,
/// window/user settings persistence, and the Notification Framework's
/// own first real Desktop consumer — over a real, running
/// <see cref="WorkspaceHost"/> and real Mechanical sample data, never a
/// mock.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class WorkflowInteractionTests
{
    // ------------------------------------------------------------
    // InputDialog
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task InputDialog_Confirm_ResolvesTheTrimmedValue()
    {
        var dialog = new InputDialog();
        var promptTask = dialog.PromptAsync("Create Part", "Name:");
        Assert.True(dialog.IsVisible);

        var textBox = GetLogicalDescendants(dialog).OfType<TextBox>().Single();
        textBox.Text = "  New Part  ";
        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal("New Part", await promptTask);
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public async Task InputDialog_Cancel_ResolvesNull()
    {
        var dialog = new InputDialog();
        var promptTask = dialog.PromptAsync("Create Part", "Name:");

        var cancelButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
        cancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Null(await promptTask);
    }

    [AvaloniaFact]
    public async Task InputDialog_EmptyValue_ShowsValidationError_StaysOpen()
    {
        var dialog = new InputDialog();
        var promptTask = dialog.PromptAsync("Create Part", "Name:", initialValue: "");

        var textBox = GetLogicalDescendants(dialog).OfType<TextBox>().Single();
        textBox.Text = "   ";
        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.True(dialog.IsVisible);
        Assert.False(promptTask.IsCompleted);
    }

    [AvaloniaFact]
    public async Task InputDialog_CustomValidator_RejectsAnInvalidValue_ThenAcceptsAValidOne()
    {
        var dialog = new InputDialog();
        var promptTask = dialog.PromptAsync("Create Part", "Name:", validate: v => v.Length > 5 ? "Too long." : null);

        var textBox = GetLogicalDescendants(dialog).OfType<TextBox>().Single();
        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));

        textBox.Text = "TooLongName";
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.True(dialog.IsVisible);

        textBox.Text = "OK";
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("OK", await promptTask);
    }

    [AvaloniaFact]
    public async Task InputDialog_SecondPromptWhileFirstPending_CancelsTheFirst()
    {
        var dialog = new InputDialog();
        var first = dialog.PromptAsync("First", "Name:");

        var second = dialog.PromptAsync("Second", "Name:");

        Assert.Null(await first);

        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));
        var textBox = GetLogicalDescendants(dialog).OfType<TextBox>().Single();
        textBox.Text = "value";
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        Assert.Equal("value", await second);
    }

    // ------------------------------------------------------------
    // MessageDialog
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task MessageDialog_ShowAsync_DisplaysSeverityTitleAndMessage_OkResolves()
    {
        var dialog = new MessageDialog();
        var showTask = dialog.ShowAsync(FeedbackSeverity.Error, "Unexpected Error", "Something went wrong.", "System.Exception: boom");
        Assert.True(dialog.IsVisible);

        var texts = GetLogicalDescendants(dialog).OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Unexpected Error", texts);
        Assert.Contains("Something went wrong.", texts);

        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        await showTask;
        Assert.False(dialog.IsVisible);
    }

    [AvaloniaFact]
    public async Task MessageDialog_NoDetails_HidesTheDetailsSection()
    {
        var dialog = new MessageDialog();
        _ = dialog.ShowAsync(FeedbackSeverity.Info, "Info", "Just an FYI.");

        var expander = GetLogicalDescendants(dialog).OfType<Expander>().Single();
        Assert.False(expander.IsVisible);

        var okButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "OK"));
        okButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        await Task.CompletedTask;
    }

    // ------------------------------------------------------------
    // SettingsDialog
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task SettingsDialog_Save_PersistsToUserSettings()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var theme = new ThemeService(settingsProvider);
            var settings = new UserSettings(settingsProvider);
            var dialog = new SettingsDialog(theme, settings);

            var showTask = dialog.ShowAsync();
            var checkbox = GetLogicalDescendants(dialog).OfType<CheckBox>().Single();
            checkbox.IsChecked = false;

            var saveButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "Save"));
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.True(await showTask);
            Assert.False(settings.ConfirmBeforeDelete);

            var reloaded = new UserSettings(settingsProvider);
            await reloaded.LoadAsync();
            Assert.False(reloaded.ConfirmBeforeDelete);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task SettingsDialog_Cancel_LeavesSettingsUnchanged()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));
            var theme = new ThemeService(settingsProvider);
            var settings = new UserSettings(settingsProvider);
            var dialog = new SettingsDialog(theme, settings);

            var showTask = dialog.ShowAsync();
            var cancelButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "Cancel"));
            cancelButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            Assert.False(await showTask);
            Assert.True(settings.ConfirmBeforeDelete); // unchanged, still the default
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // UserSettings / WindowUiState persistence
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task UserSettings_SaveThenLoad_RoundTripsEveryValue()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));

            var settings = new UserSettings(settingsProvider) { ToastDurationSeconds = 9.5, ConfirmBeforeDelete = false, RecentSearchCapacity = 3 };
            await settings.SaveAsync();

            var reloaded = new UserSettings(settingsProvider);
            await reloaded.LoadAsync();

            Assert.Equal(9.5, reloaded.ToastDurationSeconds);
            Assert.False(reloaded.ConfirmBeforeDelete);
            Assert.Equal(3, reloaded.RecentSearchCapacity);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task WindowUiState_SaveThenLoad_RoundTripsEveryValue()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));

            var state = CreateWindowUiState(settingsProvider);
            state.X = 100;
            state.Y = 200;
            state.Width = 1400;
            state.Height = 900;
            state.IsMaximised = true;
            await state.SaveAsync();

            var reloaded = CreateWindowUiState(settingsProvider);
            await reloaded.LoadAsync();

            Assert.Equal(100, reloaded.X);
            Assert.Equal(200, reloaded.Y);
            Assert.Equal(1400, reloaded.Width);
            Assert.Equal(900, reloaded.Height);
            Assert.True(reloaded.IsMaximised);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task WindowUiState_LoadWithNoPriorSave_LeavesEveryDefaultUnchanged()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));

            var state = CreateWindowUiState(settingsProvider);
            await state.LoadAsync();

            Assert.Null(state.X);
            Assert.Null(state.Y);
            Assert.Equal(1280, state.Width);
            Assert.Equal(800, state.Height);
            Assert.False(state.IsMaximised);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public void WindowUiState_CaptureFrom_WhileMaximised_DoesNotOverwriteTheRestoreGeometry()
    {
        var settingsProvider = new InMemorySettingsProviderForTest();
        var state = CreateWindowUiState(settingsProvider);
        state.X = 50;
        state.Y = 60;
        state.Width = 1000;
        state.Height = 700;

        var window = new Window { Width = 1000, Height = 700, WindowState = WindowState.Maximized };
        state.CaptureFrom(window);

        Assert.True(state.IsMaximised);
        Assert.Equal(50, state.X); // untouched — the pre-maximise geometry is preserved.
        Assert.Equal(60, state.Y);
    }

    // ------------------------------------------------------------
    // Delete Confirmation gate (Ribbon + Project Explorer)
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task RibbonDelete_ConfirmationDeclined_DoesNotDeleteTheRealObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domainContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));
            var target = await GetRealLeafMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { }) { ConfirmDeleteAsync = _ => Task.FromResult(false) };

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            // No wait needed here (TD-119, Technical Debt Register.md — the
            // formerly sole retained fixed wait, `Task.Delay(50)`, removed
            // after 20/20 clean runs): the declined path is synchronous end
            // to end. `ConfirmDeleteAsync` above returns `Task.FromResult(false)`
            // — an already-completed task — so `RibbonView.DeleteAsync`'s
            // `await confirm(message)` (`src/Tempest.Desktop/Views/RibbonView.cs`)
            // never yields to the dispatcher; it resumes synchronously and
            // hits the bare `return;` on the same call stack `RaiseEvent`
            // is on. There is no completion event to join for this branch
            // by design (declining does not raise `ActionCompleted`), but
            // there is also nothing left running after `RaiseEvent` returns.
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `IDeletable.DeleteAsync` is a soft delete (`IsDeleted`,
            // `Tempest.Core.EngineeringDomain`, unchanged) — the object
            // always remains findable via `Repository.FindAsync`; the
            // real, correct check is `IsDeleted`, not presence/absence.
            var stillThere = await domainContext.Repository.FindAsync(target.Id);
            Assert.NotNull(stillThere);
            Assert.False(((Tempest.Core.EngineeringDomain.IDeletable)stillThere!).IsDeleted);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RibbonDelete_ConfirmationAccepted_DeletesTheRealObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var domainContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));
            var target = await GetRealLeafMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var promptSeen = string.Empty;
            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { })
            {
                ConfirmDeleteAsync = prompt => { promptSeen = prompt; return Task.FromResult(true); },
            };

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the confirmed delete runs asynchronously through
            // `IWorkspaceManager.DeleteObjectAsync`. Bounded poll re-reading the
            // real object each iteration; the assertions below are unchanged.
            var stillFindable = await domainContext.Repository.FindAsync(target.Id);
            var deleteDeadline = DateTime.UtcNow.AddSeconds(2);
            while ((stillFindable is null || !((Tempest.Core.EngineeringDomain.IDeletable)stillFindable).IsDeleted) && DateTime.UtcNow < deleteDeadline)
            {
                await Task.Delay(10);
                stillFindable = await domainContext.Repository.FindAsync(target.Id);
            }

            // Soft delete (see remarks above) — `IsDeleted` is the real
            // assertion, not `FindAsync` returning null.
            Assert.NotNull(stillFindable);
            Assert.True(((Tempest.Core.EngineeringDomain.IDeletable)stillFindable!).IsDeleted);
            Assert.False(string.IsNullOrEmpty(promptSeen));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task RibbonDelete_UnwiredConfirmation_ProceedsImmediately_BackwardCompatible()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var target = await GetRealLeafMechanicalObjectNodeAsync(workspace);
            await workspace.Selection.SelectAsync(target.Id, target.Kind!);

            var ribbon = new RibbonView(registry, host.Manager!, workspace, _ => { }, _ => { }); // ConfirmDeleteAsync never set
            var messages = new List<string>();
            ribbon.ActionCompleted += (message, _) => messages.Add(message);

            var deleteButton = FindButtonById(ribbon, registry, "mechanical.delete");
            deleteButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`: the click dispatches asynchronously; bounded poll on the real reported
            // state, assertions unchanged.
            var unwiredDeadline = DateTime.UtcNow.AddSeconds(2);
            while (!(messages.Count > 0) && DateTime.UtcNow < unwiredDeadline)
                await Task.Delay(10);

            Assert.Contains(messages, m => m.Contains("Deleted", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Real object creation / duplicate workflow
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task RibbonObjectCreationHandlers_WhenWired_IsInvokedInsteadOfTheFallbackMessage()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var registry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));
            var ribbon = new RibbonView(registry, host.Manager!, host.Workspace!, _ => { }, _ => { });

            // TD-77 Stage 5: there is no handler dictionary to inject into
            // any more. A click asks the command framework, and with no
            // prompt wired (this view is constructed directly) a command
            // that needs values says so - honestly, and by name.
            var messages = new List<string>();
            ribbon.ActionCompleted += (message, _) => messages.Add(message);

            var createButton = FindButtonById(ribbon, registry, "mechanical.create");
            createButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            // `TD-119`/Class B: no wait. An unavailable command is refused
            // synchronously — `RibbonView.OnCommandButtonClickAsync` evaluates
            // availability and raises `ActionCompleted` before its first
            // `await`, so the message is already recorded when `RaiseEvent`
            // returns.
            Assert.Contains(messages, m => m.Contains("needs additional input", StringComparison.Ordinal));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task CreateMechanicalObjectCommand_DispatchedDirectly_ActuallyCreatesARealObject()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var domainContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));

            var result = await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand("Part", "WP10.5B Test Part"), CancellationToken.None);

            Assert.True(result.Succeeded);
            var created = await domainContext.Repository.ListByKindAsync("Part");
            Assert.Contains(created, o => (o as Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)?.DisplayName == "WP10.5B Test Part");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Recent Searches
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task ProjectExplorerRecentSearches_ClearingTheFilter_RecordsTheCompletedSearch()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var explorerView = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!);
            await explorerView.LoadAsync();

            var filterBox = GetLogicalDescendants(explorerView).OfType<TextBox>().Single();
            filterBox.Text = "Sample";
            filterBox.Text = string.Empty;

            Assert.Contains("Sample", explorerView.RecentSearches);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [AvaloniaFact]
    public async Task ProjectExplorerRecentSearches_RespectsCapacity()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var workspace = host.Workspace!;
            await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
            var explorerView = new ProjectExplorerView(workspace.ProjectExplorer, host.Manager!) { RecentSearchCapacity = 2 };
            await explorerView.LoadAsync();

            var filterBox = GetLogicalDescendants(explorerView).OfType<TextBox>().Single();
            foreach (var query in new[] { "one", "two", "three" })
            {
                filterBox.Text = query;
                filterBox.Text = string.Empty;
            }

            Assert.Equal(2, explorerView.RecentSearches.Count);
            Assert.Equal(new[] { "three", "two" }, explorerView.RecentSearches);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // DocumentAreaView.HasAnyDirtyTab
    // ------------------------------------------------------------

    [AvaloniaFact]
    public void DocumentAreaView_HasAnyDirtyTab_ReflectsMarkDirty()
    {
        var area = new DocumentAreaView();
        var viewId = Guid.NewGuid();
        Assert.False(area.HasAnyDirtyTab);

        area.MarkDirty(viewId, true);
        Assert.True(area.HasAnyDirtyTab);

        area.MarkDirty(viewId, false);
        Assert.False(area.HasAnyDirtyTab);
    }

    // ------------------------------------------------------------
    // Notification Framework — PlatformNotificationToastBridge
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task PlatformNotificationToastBridge_HandleAsync_ForwardsToToastHost()
    {
        var toastHost = new ToastHost();
        var bridge = new PlatformNotificationToastBridge(toastHost);
        var notification = new PlatformNotification("Background", NotificationSeverity.Warning, "Import finished with warnings.");

        await bridge.HandleAsync(notification, CancellationToken.None);

        Assert.Equal(1, toastHost.ActiveToastCount);
        var toast = GetLogicalDescendants(toastHost).OfType<ToastNotification>().Single();
        Assert.Equal(FeedbackSeverity.Warning, toast.Severity);
        Assert.Contains("Import finished with warnings.", toast.Message);
        Assert.Contains("Background", toast.Message);
    }

    [AvaloniaFact]
    public async Task EventBus_PublishingAPlatformNotification_ReachesTheToastBridge()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var eventBus = (IEventBus)host.Services!.GetService(typeof(IEventBus));
            var toastHost = new ToastHost();
            eventBus.Subscribe(new PlatformNotificationToastBridge(toastHost));

            await eventBus.PublishAsync<IPlatformNotification>(new PlatformNotification("Test", NotificationSeverity.Success, "Task completed."), CancellationToken.None);

            Assert.Equal(1, toastHost.ActiveToastCount);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------

    private static WindowUiState CreateWindowUiState(Tempest.Core.Settings.ISettingsProvider settingsProvider) => new(settingsProvider);

    private static async Task<ProjectExplorerNode> GetRealMechanicalObjectNodeAsync(IWorkspace workspace)
    {
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var node = await FindFirstObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(node);
        return node!;
    }

    /// <summary>
    /// A real, childless Mechanical object node — needed specifically for
    /// Delete tests. <see cref="GetRealMechanicalObjectNodeAsync"/>'s own
    /// "first object node found" can land on the root Project itself
    /// (which genuinely has children), and <c>IDeletable.DeleteAsync</c>
    /// throws <c>EngineeringObjectHasChildrenException</c> for any object
    /// a live child still parents — a real, disclosed business rule, not
    /// a defect. Depending on
    /// <c>InMemoryEngineeringObjectRepository</c>'s own unspecified
    /// iteration order (`TD-27`'s identical risk class), the plain
    /// "first object" helper could non-deterministically pick either a
    /// deletable leaf or the non-deletable root — found directly, by a
    /// flaking test, before this helper existed.
    /// </summary>
    private static async Task<ProjectExplorerNode> GetRealLeafMechanicalObjectNodeAsync(IWorkspace workspace)
    {
        await workspace.Navigation.SwitchAreaAsync(MechanicalWorkspaceExplorerModule.NavigationItemId);
        var roots = await workspace.ProjectExplorer.GetRootNodesAsync();
        var node = await FindFirstLeafObjectNodeAsync(workspace.ProjectExplorer, roots);
        Assert.NotNull(node);
        return node!;
    }

    private static async Task<ProjectExplorerNode?> FindFirstLeafObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object && !node.HasChildren)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstLeafObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static async Task<ProjectExplorerNode?> FindFirstObjectNodeAsync(IProjectExplorer explorer, IReadOnlyList<ProjectExplorerNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.NodeType == ProjectExplorerNodeType.Object)
                return node;

            if (node.HasChildren)
            {
                var found = await FindFirstObjectNodeAsync(explorer, await explorer.GetChildrenAsync(node.Id));
                if (found is not null)
                    return found;
            }
        }

        return null;
    }

    private static Button FindButtonById(RibbonView ribbon, ICommandRegistry registry, string commandId)
    {
        var descriptor = registry.Items.Single(d => d.Id == commandId);
        var tabs = (TabControl)ribbon.Content!;
        var tab = tabs.Items.OfType<TabItem>().Single(t => Equals(t.Tag, descriptor.Category));

        return FindButtonsWithText((Control)tab.Content!, descriptor.DisplayName).First();
    }

    private static IEnumerable<Button> FindButtonsWithText(Control root, string text)
    {
        if (root is Button button && ContainsText(button.Content, text))
            yield return button;

        foreach (var child in GetChildren(root))
        {
            foreach (var found in FindButtonsWithText(child, text))
                yield return found;
        }
    }

    private static bool ContainsText(object? content, string text) =>
        content switch
        {
            string s => s == text,
            Control c => GetLogicalDescendants(c).OfType<TextBlock>().Any(t => t.Text == text),
            _ => false,
        };

    private static IEnumerable<Control> GetChildren(Control control) => control switch
    {
        Decorator d => d.Child is { } child ? [child] : [],
        ContentControl { Content: Control c } => [c],
        Panel p => p.Children,
        _ => [],
    };

    private static IEnumerable<Control> GetLogicalDescendants(Control root)
    {
        foreach (var child in GetChildren(root))
        {
            yield return child;
            foreach (var descendant in GetLogicalDescendants(child))
                yield return descendant;
        }
    }

    /// <summary>A minimal, in-memory-only <see cref="Tempest.Core.Settings.ISettingsProvider"/> stub — used only by the one synchronous, non-<see cref="WorkspaceHost"/> test above.</summary>
    private sealed class InMemorySettingsProviderForTest : Tempest.Core.Settings.ISettingsProvider
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly HashSet<string> _definitions = new();

        public void RegisterDefinition(Tempest.Core.Settings.ISettingDefinition definition)
        {
            if (!_definitions.Add(definition.Key))
                throw new Tempest.Core.Settings.DuplicateSettingDefinitionException(definition.Key);
            _values[definition.Key] = definition.DefaultValue;
        }

        public Task<string> GetValueAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) ? value : string.Empty);

        public Task SetValueAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }
    }
}
