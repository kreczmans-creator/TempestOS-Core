using Tempest.Core.Events;

namespace Tempest.Core.Tests.Events;

public class EventContractTests
{
    [Fact]
    public void IEvent_ConcreteEvent_CarriesWhateverDataItsSubscribersNeed()
    {
        var raised = new ProjectSavedEvent("tempest.sample");

        Assert.IsAssignableFrom<IEvent>(raised);
        Assert.Equal("tempest.sample", raised.ProjectId);
    }

    [Fact]
    public async Task IEventHandler_HandleAsync_ReceivesThePublishedEvent()
    {
        var handler = new RecordingEventHandler();
        var raised = new ProjectSavedEvent("tempest.sample");

        await handler.HandleAsync(raised, CancellationToken.None);

        Assert.Same(raised, handler.LastHandled);
    }

    [Fact]
    public async Task IEventHandler_ObservesCancellation()
    {
        var handler = new RecordingEventHandler();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            handler.HandleAsync(new ProjectSavedEvent("tempest.sample"), cts.Token));
    }

    private sealed class ProjectSavedEvent : IEvent
    {
        public ProjectSavedEvent(string projectId)
        {
            ProjectId = projectId;
        }

        public string ProjectId { get; }
    }

    private sealed class RecordingEventHandler : IEventHandler<ProjectSavedEvent>
    {
        public ProjectSavedEvent? LastHandled { get; private set; }

        public Task HandleAsync(ProjectSavedEvent @event, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastHandled = @event;
            return Task.CompletedTask;
        }
    }
}
