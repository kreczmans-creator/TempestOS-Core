namespace Tempest.Desktop.Tasks;

/// <summary>The coarse lifecycle state of one tracked background task — see <see cref="BackgroundTaskHandle"/>'s own remarks for why this is coarse (state), not fine-grained (percentage).</summary>
public enum BackgroundTaskState
{
    /// <summary>Currently running.</summary>
    Running,

    /// <summary>Completed successfully.</summary>
    Succeeded,

    /// <summary>Completed with a failure.</summary>
    Failed,

    /// <summary>Cancelled before completion.</summary>
    Cancelled,
}

/// <summary>One task tracked by an <see cref="IBackgroundTaskRunner"/>.</summary>
public sealed class BackgroundTaskHandle
{
    private readonly CancellationTokenSource _cancellationTokenSource;

    internal BackgroundTaskHandle(string title, CancellationTokenSource cancellationTokenSource)
    {
        Title = title;
        StartedAt = DateTimeOffset.Now;
        State = BackgroundTaskState.Running;
        _cancellationTokenSource = cancellationTokenSource;
    }

    /// <summary>Gets this task's own human-readable title.</summary>
    public string Title { get; }

    /// <summary>Gets when this task started.</summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>Gets this task's own current, coarse state.</summary>
    public BackgroundTaskState State { get; internal set; }

    /// <summary>Gets a message describing the outcome, once no longer <see cref="BackgroundTaskState.Running"/> — <see langword="null"/> while running.</summary>
    public string? OutcomeMessage { get; internal set; }

    /// <summary>Requests this task be cancelled — honoured only if the task's own work observes the token it was given, exactly like any other cancellable operation in this platform.</summary>
    public void Cancel() => _cancellationTokenSource.Cancel();
}
