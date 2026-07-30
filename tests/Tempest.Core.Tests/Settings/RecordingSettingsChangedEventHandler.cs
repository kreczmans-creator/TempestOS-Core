using Tempest.Core.Events;
using Tempest.Core.Settings;

namespace Tempest.Core.Tests.Settings;

/// <summary>
/// A hand-written <see cref="IEventHandler{TEvent}"/> test double that
/// records every <see cref="ISettingsChangedEvent"/> it observes, in
/// order — mirrors <c>ClockLifecycleObserverModule</c>'s own
/// subscribe-and-record pattern, purely for test observation.
/// </summary>
internal sealed class RecordingSettingsChangedEventHandler : IEventHandler<ISettingsChangedEvent>
{
    private readonly List<ISettingsChangedEvent> _received = [];

    public IReadOnlyList<ISettingsChangedEvent> Received => _received;

    public Task HandleAsync(ISettingsChangedEvent @event, CancellationToken cancellationToken)
    {
        _received.Add(@event);
        return Task.CompletedTask;
    }
}
