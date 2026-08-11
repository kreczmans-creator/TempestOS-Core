using Tempest.Core.Commands;

namespace Tempest.Desktop.Tasks;

/// <summary>The concrete <see cref="IBackgroundTaskRunner"/> implementation.</summary>
public sealed class BackgroundTaskRunner : IBackgroundTaskRunner
{
    /// <summary>The maximum number of completed tasks retained alongside whatever is still running — the oldest completed task is discarded once exceeded.</summary>
    public const int Capacity = 50;

    private readonly List<BackgroundTaskHandle> _tasks = [];

    /// <inheritdoc />
    public event Action? Changed;

    /// <inheritdoc />
    public IReadOnlyList<BackgroundTaskHandle> Tasks => _tasks;

    /// <inheritdoc />
    public async Task<CommandResult> RunAsync(string title, Func<CancellationToken, Task<CommandResult>> work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(work);

        using var cancellationTokenSource = new CancellationTokenSource();
        var handle = new BackgroundTaskHandle(title, cancellationTokenSource);

        _tasks.Insert(0, handle);
        TrimCompleted();
        Changed?.Invoke();

        try
        {
            var result = await work(cancellationTokenSource.Token).ConfigureAwait(true);

            handle.State = result.Succeeded ? BackgroundTaskState.Succeeded : BackgroundTaskState.Failed;
            handle.OutcomeMessage = result.Message;
            Changed?.Invoke();

            return result;
        }
        catch (OperationCanceledException)
        {
            handle.State = BackgroundTaskState.Cancelled;
            handle.OutcomeMessage = "Cancelled.";
            Changed?.Invoke();

            return CommandResult.Failure($"'{title}' was cancelled.");
        }
        catch (Exception ex)
        {
            handle.State = BackgroundTaskState.Failed;
            handle.OutcomeMessage = ex.Message;
            Changed?.Invoke();

            throw;
        }
    }

    /// <summary>Discards the oldest completed (non-<see cref="BackgroundTaskState.Running"/>) tasks once <see cref="Capacity"/> is exceeded — a still-running task is never discarded.</summary>
    private void TrimCompleted()
    {
        var completedCount = _tasks.Count(t => t.State != BackgroundTaskState.Running);
        var overflow = _tasks.Count - Capacity;

        while (overflow > 0 && completedCount > 0)
        {
            var oldestCompletedIndex = _tasks.FindLastIndex(t => t.State != BackgroundTaskState.Running);
            if (oldestCompletedIndex < 0)
                break;

            _tasks.RemoveAt(oldestCompletedIndex);
            overflow--;
            completedCount--;
        }
    }
}
