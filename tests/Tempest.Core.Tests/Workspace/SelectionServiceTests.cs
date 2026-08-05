using Tempest.App.Workspace;
using Tempest.Core.Configuration;
using Tempest.Core.Events;
using Tempest.Core.Persistence;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Workspace;

// Proves ISelectionService (Tempest.App.Workspace) publishes every change
// through the real, unmodified IEventBus - no new pub/sub mechanism.
[Collection("Console output capture")]
public class SelectionServiceTests
{
    private sealed class RecordingHandler : IEventHandler<WorkspaceSelectionChangedEvent>
    {
        public List<WorkspaceSelectionChangedEvent> Received { get; } = [];

        public Task HandleAsync(WorkspaceSelectionChangedEvent @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingSetHandler : IEventHandler<WorkspaceSelectionSetChangedEvent>
    {
        public List<WorkspaceSelectionSetChangedEvent> Received { get; } = [];

        public Task HandleAsync(WorkspaceSelectionSetChangedEvent @event, CancellationToken cancellationToken)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private static async Task<(IWorkspace Workspace, IEventBus EventBus, WorkspaceManager Manager)> StartAsync(string rootPath)
    {
        var host = new TempestHostBuilder(Type.EmptyTypes)
            .AddConfigurationSource(new MemoryConfigurationSource(
            [
                new KeyValuePair<string, string>(PersistenceStore.RootPathConfigurationKey, rootPath),
            ]))
            .Build();
        var manager = new WorkspaceManager(host);

        var originalOut = Console.Out;
        IWorkspace workspace;
        try
        {
            Console.SetOut(new StringWriter());
            workspace = await manager.StartAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var eventBus = (IEventBus)host.Services!.GetService(typeof(IEventBus));
        return (workspace, eventBus, manager);
    }

    [Fact]
    public async Task Current_BeforeAnySelection_IsNull()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);

        Assert.Null(workspace.Selection.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_NullOrWhitespaceKind_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => workspace.Selection.SelectAsync(Guid.NewGuid(), ""));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_SetsCurrent()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();

        await workspace.Selection.SelectAsync(objectId, "Requirement");

        Assert.Equal(new WorkspaceSelection(objectId, "Requirement"), workspace.Selection.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_PublishesWorkspaceSelectionChangedEvent_ThroughTheRealEventBus()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var handler = new RecordingHandler();
        eventBus.Subscribe(handler);
        var objectId = Guid.NewGuid();

        await workspace.Selection.SelectAsync(objectId, "Requirement");

        var published = Assert.Single(handler.Received);
        Assert.Null(published.Previous);
        Assert.Equal(new WorkspaceSelection(objectId, "Requirement"), published.Current);

        eventBus.Unsubscribe(handler);
        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_Twice_SecondEventCarriesFirstAsPrevious()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var handler = new RecordingHandler();
        eventBus.Subscribe(handler);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await workspace.Selection.SelectAsync(first, "Requirement");
        await workspace.Selection.SelectAsync(second, "Material");

        Assert.Equal(2, handler.Received.Count);
        Assert.Equal(new WorkspaceSelection(first, "Requirement"), handler.Received[1].Previous);
        Assert.Equal(new WorkspaceSelection(second, "Material"), handler.Received[1].Current);

        eventBus.Unsubscribe(handler);
        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_WithNoSelection_IsANoOp_PublishesNothing()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var handler = new RecordingHandler();
        eventBus.Subscribe(handler);

        await workspace.Selection.ClearAsync();

        Assert.Empty(handler.Received);

        eventBus.Unsubscribe(handler);
        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_WithASelection_ClearsCurrent_PublishesNullCurrent()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var handler = new RecordingHandler();
        eventBus.Subscribe(handler);
        await workspace.Selection.SelectAsync(Guid.NewGuid(), "Requirement");

        await workspace.Selection.ClearAsync();

        Assert.Null(workspace.Selection.Current);
        Assert.Null(handler.Received[^1].Current);

        eventBus.Unsubscribe(handler);
        await manager.ShutdownAsync();
    }

    // ---- WP 9.1A: multi-selection (SelectedItems, ToggleSelectionAsync, WorkspaceSelectionSetChangedEvent, ADR-0085) ----

    [Fact]
    public async Task SelectedItems_BeforeAnySelection_IsEmpty()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);

        Assert.Empty(workspace.Selection.SelectedItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_SetsSelectedItemsToCurrentAlone()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();

        await workspace.Selection.SelectAsync(objectId, "Requirement");

        Assert.Equal([new WorkspaceSelection(objectId, "Requirement")], workspace.Selection.SelectedItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_Twice_SelectedItemsIsStillASingleton()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        await workspace.Selection.SelectAsync(Guid.NewGuid(), "Requirement");
        var second = Guid.NewGuid();

        await workspace.Selection.SelectAsync(second, "Material");

        Assert.Equal([new WorkspaceSelection(second, "Material")], workspace.Selection.SelectedItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_ClearsSelectedItemsToo()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        await workspace.Selection.SelectAsync(Guid.NewGuid(), "Requirement");

        await workspace.Selection.ClearAsync();

        Assert.Empty(workspace.Selection.SelectedItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_NullOrWhitespaceKind_ThrowsArgumentException()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);

        await Assert.ThrowsAsync<ArgumentException>(() => workspace.Selection.ToggleSelectionAsync(Guid.NewGuid(), ""));

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_AbsentItem_AddsIt_BecomesCurrent()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();

        await workspace.Selection.ToggleSelectionAsync(objectId, "Requirement");

        Assert.Equal(new WorkspaceSelection(objectId, "Requirement"), workspace.Selection.Current);
        Assert.Equal([new WorkspaceSelection(objectId, "Requirement")], workspace.Selection.SelectedItems);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_Repeatedly_AccumulatesIntoASet()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        await workspace.Selection.ToggleSelectionAsync(first, "Requirement");
        await workspace.Selection.ToggleSelectionAsync(second, "Requirement");
        await workspace.Selection.ToggleSelectionAsync(third, "Requirement");

        Assert.Equal(
            [
                new WorkspaceSelection(first, "Requirement"),
                new WorkspaceSelection(second, "Requirement"),
                new WorkspaceSelection(third, "Requirement"),
            ],
            workspace.Selection.SelectedItems);
        Assert.Equal(new WorkspaceSelection(third, "Requirement"), workspace.Selection.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_PresentItem_RemovesIt()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var objectId = Guid.NewGuid();
        await workspace.Selection.ToggleSelectionAsync(objectId, "Requirement");

        await workspace.Selection.ToggleSelectionAsync(objectId, "Requirement");

        Assert.Empty(workspace.Selection.SelectedItems);
        Assert.Null(workspace.Selection.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_RemovingTheNonCurrentItem_LeavesCurrentAsTheMostRecentSurvivor()
    {
        using var temp = new TempDirectory();
        var (workspace, _, manager) = await StartAsync(temp.Path);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await workspace.Selection.ToggleSelectionAsync(first, "Requirement");
        await workspace.Selection.ToggleSelectionAsync(second, "Requirement");

        await workspace.Selection.ToggleSelectionAsync(first, "Requirement");

        Assert.Equal([new WorkspaceSelection(second, "Requirement")], workspace.Selection.SelectedItems);
        Assert.Equal(new WorkspaceSelection(second, "Requirement"), workspace.Selection.Current);

        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ToggleSelectionAsync_PublishesBothWorkspaceSelectionSetChangedEventAndWorkspaceSelectionChangedEvent()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var setHandler = new RecordingSetHandler();
        var handler = new RecordingHandler();
        eventBus.Subscribe(setHandler);
        eventBus.Subscribe(handler);
        var objectId = Guid.NewGuid();

        await workspace.Selection.ToggleSelectionAsync(objectId, "Requirement");

        var publishedSet = Assert.Single(setHandler.Received);
        Assert.Empty(publishedSet.Previous);
        Assert.Equal([new WorkspaceSelection(objectId, "Requirement")], publishedSet.Current);
        var published = Assert.Single(handler.Received);
        Assert.Null(published.Previous);
        Assert.Equal(new WorkspaceSelection(objectId, "Requirement"), published.Current);

        eventBus.Unsubscribe(setHandler);
        eventBus.Unsubscribe(handler);
        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task SelectAsync_PublishesWorkspaceSelectionSetChangedEventToo()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var setHandler = new RecordingSetHandler();
        eventBus.Subscribe(setHandler);
        var objectId = Guid.NewGuid();

        await workspace.Selection.SelectAsync(objectId, "Requirement");

        var published = Assert.Single(setHandler.Received);
        Assert.Empty(published.Previous);
        Assert.Equal([new WorkspaceSelection(objectId, "Requirement")], published.Current);

        eventBus.Unsubscribe(setHandler);
        await manager.ShutdownAsync();
    }

    [Fact]
    public async Task ClearAsync_WithNoSelection_PublishesNoSetEventEither()
    {
        using var temp = new TempDirectory();
        var (workspace, eventBus, manager) = await StartAsync(temp.Path);
        var setHandler = new RecordingSetHandler();
        eventBus.Subscribe(setHandler);

        await workspace.Selection.ClearAsync();

        Assert.Empty(setHandler.Received);

        eventBus.Unsubscribe(setHandler);
        await manager.ShutdownAsync();
    }
}
