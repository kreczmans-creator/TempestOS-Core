using Tempest.Core.Events;

namespace Tempest.Core.Tests.Events;

internal sealed class RecordedEventA : IEvent
{
    public RecordedEventA(string payload) => Payload = payload;

    public string Payload { get; }
}

internal sealed class RecordedEventB : IEvent
{
}

/// <summary>
/// A configurable <see cref="IEventHandler{TEvent}"/> that records every
/// event it receives, in order, and optionally runs a caller-supplied
/// callback (used to prove re-entrant publishing, self-unsubscription,
/// late subscription, and exception isolation without a new handler type
/// per scenario).
/// </summary>
internal sealed class RecordingHandler<TEvent> : IEventHandler<TEvent>
    where TEvent : IEvent
{
    private readonly Func<TEvent, CancellationToken, Task>? _onHandle;

    public RecordingHandler(Func<TEvent, CancellationToken, Task>? onHandle = null)
    {
        _onHandle = onHandle;
    }

    public List<TEvent> Received { get; } = [];

    public Task HandleAsync(TEvent @event, CancellationToken cancellationToken)
    {
        Received.Add(@event);
        return _onHandle?.Invoke(@event, cancellationToken) ?? Task.CompletedTask;
    }
}
