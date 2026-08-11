using Tempest.Core.Commands;

namespace Tempest.Desktop.Tasks;

/// <summary>
/// Runs and tracks a titled, cancellable unit of asynchronous work as a
/// <see cref="BackgroundTaskHandle"/> — the Background Task Framework
/// (`WP 10.6A`).
/// </summary>
/// <remarks>
/// <para>
/// "Background" means tracked, cancellable, and non-blocking to further
/// user interaction while it runs — not necessarily a second OS thread.
/// Every existing <see cref="ICommandHandler{TCommand}"/> in this
/// platform is already <see langword="async"/> and non-CPU-bound
/// (in-memory repository reads/writes); a dedicated background thread
/// would add real complexity (Avalonia UI-thread marshalling) for no real
/// throughput benefit, mirroring <c>BusyOverlay.RunAsync</c>'s own
/// identical, already-accepted "async without <c>Task.Run</c>"
/// precedent.
/// </para>
/// <para>
/// Reports coarse state only (<see cref="BackgroundTaskState"/>), never a
/// percentage — no <see cref="ICommandHandler{TCommand}"/> anywhere in
/// this platform reports incremental progress (that frozen contract
/// carries no <see cref="IProgress{T}"/> parameter, not redesigned this
/// Work Package); fabricating a percentage would misrepresent real
/// progress no handler actually reports.
/// </para>
/// </remarks>
public interface IBackgroundTaskRunner
{
    /// <summary>Gets every task tracked this session, most recently started first.</summary>
    IReadOnlyList<BackgroundTaskHandle> Tasks { get; }

    /// <summary>Raised whenever a tracked task's own <see cref="BackgroundTaskHandle.State"/> changes — a UI's own refresh hook.</summary>
    event Action? Changed;

    /// <summary>
    /// Starts <paramref name="work"/>, tracked under <paramref name="title"/>
    /// as a new <see cref="BackgroundTaskHandle"/>, and returns once it
    /// completes (successfully, with a foreseen
    /// <see cref="CommandResult.Failure"/>, or cancelled).
    /// </summary>
    /// <param name="title">A short, human-readable title for this task.</param>
    /// <param name="work">The work to run, observing the <see cref="CancellationToken"/> it is given.</param>
    Task<CommandResult> RunAsync(string title, Func<CancellationToken, Task<CommandResult>> work);
}
