using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Tempest.App.Workspace;
using Tempest.App.Workspace.Mechanical;
using Tempest.Core.Commands;
using Tempest.Core.Macros;
using Tempest.Desktop.History;
using Tempest.Desktop.Input;
using Tempest.Desktop.Tasks;
using Tempest.Desktop.Views;

namespace Tempest.Desktop.Tests;

/// <summary>
/// Demonstrates `WP 10.6A`'s own Undo/Redo architecture, Command History,
/// Background Task Framework, Recent/Favourite Objects, the Macro
/// foundation, and the Keyboard input-binding provider — every one
/// exercised against its own real, concrete implementation, never a mock.
/// </summary>
[Collection("Tempest.Desktop WorkspaceHost persistence")]
public sealed class ProductivityExperienceTests
{
    // ------------------------------------------------------------
    // UndoRedoStack
    // ------------------------------------------------------------

    [Fact]
    public void UndoRedoStack_InitialState_CannotUndoOrRedo()
    {
        var stack = new UndoRedoStack();

        Assert.False(stack.CanUndo);
        Assert.False(stack.CanRedo);
        Assert.Null(stack.NextUndoDescription);
    }

    [Fact]
    public async Task UndoRedoStack_Record_ThenUndo_InvokesTheUndoDelegate_AndMovesToRedo()
    {
        var stack = new UndoRedoStack();
        var undone = false;
        var action = new UndoableAction("Rename to 'B'", undo: _ => { undone = true; return Task.FromResult(CommandResult.Success()); }, redo: _ => Task.FromResult(CommandResult.Success()));

        stack.Record(action);
        Assert.True(stack.CanUndo);
        Assert.Equal("Rename to 'B'", stack.NextUndoDescription);

        var result = await stack.UndoAsync();

        Assert.True(undone);
        Assert.NotNull(result);
        Assert.True(result!.Succeeded);
        Assert.False(stack.CanUndo);
        Assert.True(stack.CanRedo);
    }

    [Fact]
    public async Task UndoRedoStack_UndoThenRedo_InvokesTheRedoDelegate()
    {
        var stack = new UndoRedoStack();
        var redone = false;
        var action = new UndoableAction("Toggle", undo: _ => Task.FromResult(CommandResult.Success()), redo: _ => { redone = true; return Task.FromResult(CommandResult.Success()); });
        stack.Record(action);
        await stack.UndoAsync();

        var result = await stack.RedoAsync();

        Assert.True(redone);
        Assert.True(result!.Succeeded);
        Assert.True(stack.CanUndo);
        Assert.False(stack.CanRedo);
    }

    [Fact]
    public async Task UndoRedoStack_UndoAsync_WhenEmpty_ReturnsNull() =>
        Assert.Null(await new UndoRedoStack().UndoAsync());

    [Fact]
    public async Task UndoRedoStack_Record_ClearsTheRedoStack()
    {
        var stack = new UndoRedoStack();
        var noop = new UndoableAction("Noop", _ => Task.FromResult(CommandResult.Success()), _ => Task.FromResult(CommandResult.Success()));
        stack.Record(noop);
        await stack.UndoAsync();
        Assert.True(stack.CanRedo);

        stack.Record(noop);

        Assert.False(stack.CanRedo);
    }

    [Fact]
    public void UndoRedoStack_Record_RaisesChanged()
    {
        var stack = new UndoRedoStack();
        var raised = false;
        stack.Changed += () => raised = true;

        stack.Record(new UndoableAction("X", _ => Task.FromResult(CommandResult.Success()), _ => Task.FromResult(CommandResult.Success())));

        Assert.True(raised);
    }

    [AvaloniaFact]
    public async Task RenameObjectAsync_UndoAndRedo_ActuallyRenamesTheRealObjectBackAndForth()
    {
        // Proves the real production pattern (ObjectEditorView's own
        // commit path, ADR-0099) end to end against a real, running
        // WorkspaceHost — RenameObjectAsync's own Kind-agnostic dispatch
        // (ADR-0096) used identically for Undo and Redo.
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var dispatcher = (ICommandDispatcher)host.Services!.GetService(typeof(ICommandDispatcher));
            var manager = host.Manager!;

            var createResult = await dispatcher.DispatchAsync(new CreateMechanicalObjectCommand("Part", "Original Name"), CancellationToken.None);
            Assert.True(createResult.Succeeded);
            var domainContext = (Tempest.Core.EngineeringDomain.EngineeringDomainContext)host.Services!.GetService(typeof(Tempest.Core.EngineeringDomain.EngineeringDomainContext));
            var created = (await domainContext.Repository.ListByKindAsync("Part"))
                .Single(o => (o as Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)?.DisplayName == "Original Name");

            var oldName = "Original Name";
            var newName = "Renamed";
            await manager.RenameObjectAsync(created.Id, "Part", newName);

            var stack = new UndoRedoStack();
            stack.Record(new UndoableAction(
                $"Rename to '{newName}'",
                undo: ct => manager.RenameObjectAsync(created.Id, "Part", oldName, ct),
                redo: ct => manager.RenameObjectAsync(created.Id, "Part", newName, ct)));

            await stack.UndoAsync();
            Assert.Equal(oldName, ((Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)(await domainContext.Repository.FindAsync(created.Id))!).DisplayName);

            await stack.RedoAsync();
            Assert.Equal(newName, ((Tempest.Core.EngineeringDomain.IHasBusinessIdentifier)(await domainContext.Repository.FindAsync(created.Id))!).DisplayName);
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // CommandHistoryLog
    // ------------------------------------------------------------

    [Fact]
    public void CommandHistoryLog_Record_AddsAnEntry_AndRaisesChanged()
    {
        var log = new CommandHistoryLog();
        var raised = false;
        log.Changed += () => raised = true;

        log.Record("Renamed to 'X'.", succeeded: true);

        Assert.True(raised);
        var entry = Assert.Single(log.Entries);
        Assert.Equal("Renamed to 'X'.", entry.Description);
        Assert.True(entry.Succeeded);
    }

    [Fact]
    public void CommandHistoryLog_ExceedingCapacity_DiscardsTheOldestEntry()
    {
        var log = new CommandHistoryLog();
        for (var i = 0; i < CommandHistoryLog.Capacity + 5; i++)
            log.Record($"Entry {i}", succeeded: true);

        Assert.Equal(CommandHistoryLog.Capacity, log.Entries.Count);
        Assert.Equal("Entry 5", log.Entries[0].Description);
    }

    // ------------------------------------------------------------
    // BackgroundTaskRunner
    // ------------------------------------------------------------

    [Fact]
    public async Task BackgroundTaskRunner_RunAsync_Success_TracksTheHandleAsSucceeded()
    {
        var runner = new BackgroundTaskRunner();

        var result = await runner.RunAsync("Test Task", _ => Task.FromResult(CommandResult.Success("done")));

        Assert.True(result.Succeeded);
        var handle = Assert.Single(runner.Tasks);
        Assert.Equal(BackgroundTaskState.Succeeded, handle.State);
        Assert.Equal("done", handle.OutcomeMessage);
    }

    [Fact]
    public async Task BackgroundTaskRunner_RunAsync_ForeseenFailure_TracksTheHandleAsFailed()
    {
        var runner = new BackgroundTaskRunner();

        var result = await runner.RunAsync("Test Task", _ => Task.FromResult(CommandResult.Failure("nope")));

        Assert.False(result.Succeeded);
        Assert.Equal(BackgroundTaskState.Failed, runner.Tasks.Single().State);
    }

    [Fact]
    public async Task BackgroundTaskRunner_RunAsync_Cancelled_TracksTheHandleAsCancelled()
    {
        var runner = new BackgroundTaskRunner();

        var result = await runner.RunAsync("Test Task", _ => throw new OperationCanceledException());

        Assert.False(result.Succeeded);
        Assert.Equal(BackgroundTaskState.Cancelled, runner.Tasks.Single().State);
    }

    [Fact]
    public async Task BackgroundTaskRunner_Changed_RaisedOnStartAndOnCompletion()
    {
        var runner = new BackgroundTaskRunner();
        var raiseCount = 0;
        runner.Changed += () => raiseCount++;

        await runner.RunAsync("Test Task", _ => Task.FromResult(CommandResult.Success()));

        Assert.Equal(2, raiseCount); // once on start, once on completion
    }

    // ------------------------------------------------------------
    // RecentObjectsState / FavouriteObjectsState
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task RecentObjectsState_Record_ThenSaveAndReload_RoundTrips()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));

            var state = new RecentObjectsState(settingsProvider);
            var id = Guid.NewGuid();
            state.Record(id, "Part", "My Part");
            await state.SaveAsync();

            var reloaded = new RecentObjectsState(settingsProvider);
            await reloaded.LoadAsync();

            Assert.Contains(reloaded.Entries, e => e.Id == id && e.DisplayName == "My Part");
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    [Fact]
    public void RecentObjectsState_ExceedingCapacity_DiscardsTheOldest()
    {
        var state = new RecentObjectsState(new InMemorySettingsProviderForTest());
        for (var i = 0; i < RecentObjectsState.Capacity + 3; i++)
            state.Record(Guid.NewGuid(), "Part", $"Part {i}");

        Assert.Equal(RecentObjectsState.Capacity, state.Entries.Count);
    }

    [Fact]
    public void RecentObjectsState_RecordingTheSameIdAgain_MovesItToTheFront_WithoutDuplicating()
    {
        var state = new RecentObjectsState(new InMemorySettingsProviderForTest());
        var id = Guid.NewGuid();
        state.Record(id, "Part", "A");
        state.Record(Guid.NewGuid(), "Part", "B");
        state.Record(id, "Part", "A");

        Assert.Equal(2, state.Entries.Count);
        Assert.Equal(id, state.Entries[0].Id);
    }

    [Fact]
    public void FavouriteObjectsState_Toggle_AddsThenRemoves()
    {
        var state = new FavouriteObjectsState(new InMemorySettingsProviderForTest());
        var id = Guid.NewGuid();
        Assert.False(state.IsFavourite(id));

        state.Toggle(id, "Part", "My Part");
        Assert.True(state.IsFavourite(id));

        state.Toggle(id, "Part", "My Part");
        Assert.False(state.IsFavourite(id));
    }

    [AvaloniaFact]
    public async Task FavouriteObjectsState_SaveAndReload_RoundTrips()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var settingsProvider = (Tempest.Core.Settings.ISettingsProvider)host.Services!.GetService(typeof(Tempest.Core.Settings.ISettingsProvider));

            var state = new FavouriteObjectsState(settingsProvider);
            var id = Guid.NewGuid();
            state.Add(id, "Requirement", "REQ-1");
            await state.SaveAsync();

            var reloaded = new FavouriteObjectsState(settingsProvider);
            await reloaded.LoadAsync();

            Assert.True(reloaded.IsFavourite(id));
        }
        finally
        {
            await host.ShutdownAsync();
            await host.DisposeAsync();
        }
    }

    // ------------------------------------------------------------
    // KeyboardCommandBindingProvider
    // ------------------------------------------------------------

    [Fact]
    public void KeyboardCommandBindingProvider_Bind_ThenHandleKeyDown_RaisesCommandRequested()
    {
        var provider = new KeyboardCommandBindingProvider();
        provider.Bind(new KeyGesture(Key.F9, KeyModifiers.Control), "sample.command");
        string? requested = null;
        provider.CommandRequested += id => requested = id;

        var e = new KeyEventArgs { Key = Key.F9, KeyModifiers = KeyModifiers.Control, RoutedEvent = InputElement.KeyDownEvent };
        provider.HandleKeyDown(e);

        Assert.Equal("sample.command", requested);
        Assert.True(e.Handled);
    }

    [Fact]
    public void KeyboardCommandBindingProvider_AlreadyHandledEvent_IsIgnored()
    {
        var provider = new KeyboardCommandBindingProvider();
        provider.Bind(new KeyGesture(Key.F9, KeyModifiers.Control), "sample.command");
        var requested = false;
        provider.CommandRequested += _ => requested = true;

        var e = new KeyEventArgs { Key = Key.F9, KeyModifiers = KeyModifiers.Control, RoutedEvent = InputElement.KeyDownEvent, Handled = true };
        provider.HandleKeyDown(e);

        Assert.False(requested);
    }

    [Fact]
    public void KeyboardCommandBindingProvider_Unbind_RemovesTheBinding()
    {
        var provider = new KeyboardCommandBindingProvider();
        var gesture = new KeyGesture(Key.F9, KeyModifiers.Control);
        provider.Bind(gesture, "sample.command");

        provider.Unbind(gesture);

        Assert.Empty(provider.Bindings);
    }

    [Fact]
    public void KeyboardCommandBindingProvider_UnboundGesture_DoesNotRaise()
    {
        var provider = new KeyboardCommandBindingProvider();
        var requested = false;
        provider.CommandRequested += _ => requested = true;

        var e = new KeyEventArgs { Key = Key.A, KeyModifiers = KeyModifiers.None, RoutedEvent = InputElement.KeyDownEvent };
        provider.HandleKeyDown(e);

        Assert.False(requested);
        Assert.False(e.Handled);
    }

    // ------------------------------------------------------------
    // MacroManagerDialog
    // ------------------------------------------------------------

    [AvaloniaFact]
    public async Task MacroManagerDialog_NewMacro_SaveWithSteps_CreatesARealInvokableMacro()
    {
        var host = new WorkspaceHost(WorkspacePersistenceCollection.NewIsolatedPersistenceRootPath());
        try
        {
            await host.StartAsync();
            var macroManager = (IMacroManager)host.Services!.GetService(typeof(IMacroManager));
            var commandRegistry = (ICommandRegistry)host.Services!.GetService(typeof(ICommandRegistry));

            var ranMacroIds = new List<Guid>();
            var dialog = new MacroManagerDialog(macroManager, commandRegistry, runMacro: id =>
            {
                ranMacroIds.Add(id);
                return Task.FromResult(CommandResult.Success());
            });

            await dialog.ShowAsync();
            Assert.True(dialog.IsVisible);

            var newButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "New Macro..."));
            newButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var nameBox = GetLogicalDescendants(dialog).OfType<TextBox>().Single();
            nameBox.Text = "My First Macro";

            var availableList = GetLogicalDescendants(dialog).OfType<ListBox>().ElementAt(1);
            Assert.NotEmpty(availableList.Items);
            availableList.SelectedIndex = 0;

            var addButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "Add Step →"));
            addButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var saveButton = GetLogicalDescendants(dialog).OfType<Button>().Single(b => Equals(b.Content, "Save Macro"));
            saveButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

            var macros = await macroManager.ListAsync();
            Assert.Contains(macros, m => m.Name == "My First Macro");
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

    /// <summary>A minimal, in-memory-only <see cref="Tempest.Core.Settings.ISettingsProvider"/> stub — mirrors <c>WorkflowInteractionTests</c>' own identical, deliberately-duplicated convention.</summary>
    private sealed class InMemorySettingsProviderForTest : Tempest.Core.Settings.ISettingsProvider
    {
        private readonly Dictionary<string, string> _values = new();
        private readonly HashSet<string> _definitions = new();

        public void RegisterDefinition(Tempest.Core.Settings.ISettingDefinition definition)
        {
            if (!_definitions.Add(definition.Key))
                throw new Tempest.Core.Settings.DuplicateSettingDefinitionException(definition.Key);
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
