namespace Tempest.Core.Runtime;

/// <summary>
/// The single entry point to TempestOS: brings every platform service up, in
/// the right order, holds the platform in a running state, and brings
/// everything back down again, cleanly, whenever asked or whenever something
/// goes wrong.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="ITempestHost"/> is single-use (ADR-0015): <see cref="RunAsync"/>
/// may be called at most once per instance. A second run always means a new
/// <see cref="ITempestHostBuilder"/> producing a new <see cref="ITempestHost"/>.
/// </para>
/// <para>
/// Instances are constructed only by <see cref="ITempestHostBuilder.Build"/> —
/// no other component may construct the runtime.
/// </para>
/// </remarks>
public interface ITempestHost : IAsyncDisposable
{
    /// <summary>
    /// Gets the host's current lifecycle state.
    /// </summary>
    HostState State { get; }

    /// <summary>
    /// Runs the host: builds every platform service in order, drives every
    /// registered module to <see cref="Modules.ModuleState.Running"/>, then
    /// holds the platform in the <see cref="HostState.Running"/> state until a
    /// shutdown is requested — via <paramref name="cancellationToken"/>, or via
    /// <see cref="StopAsync"/> — at which point a controlled shutdown runs and
    /// this method returns.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token observed for the host's entire run. Cancelling it during
    /// startup aborts startup (ADR-0018: routed through the same controlled
    /// shutdown a graceful, post-<see cref="HostState.Running"/> stop uses, not
    /// a fault); cancelling it once running requests a graceful shutdown.
    /// Cancellation is observed only between atomic operations, never in the
    /// middle of one (Engineering Principle 11, the Atomic Phase Principle).
    /// </param>
    /// <returns>
    /// A task that completes once the host reaches <see cref="HostState.Stopped"/>.
    /// </returns>
    /// <exception cref="InvalidHostStateTransitionException">
    /// The host has already been run (its state is not <see cref="HostState.Created"/>).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> — the caller's own token, as opposed
    /// to a shutdown requested via <see cref="StopAsync"/> — was the signal that
    /// ended the run. The host still reaches <see cref="HostState.Stopped"/>,
    /// never <see cref="HostState.Faulted"/>, before this is thrown; cancellation
    /// is never treated as a fault (ADR-0013, ADR-0018).
    /// </exception>
    /// <remarks>
    /// Any other exception propagating from this method is a genuine
    /// platform-service failure (ADR-0013): the host transitions to
    /// <see cref="HostState.Faulted"/> and the exception is the original
    /// failure, unwrapped. <see cref="DisposeAsync"/> must still be called
    /// afterward — disposal is always an explicit, separate call (ADR-0019),
    /// never performed automatically by this method.
    /// </remarks>
    Task RunAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests a shutdown: signals the host to begin a controlled shutdown if
    /// it has not already, and waits for <see cref="RunAsync"/> to return.
    /// </summary>
    /// <returns>A task that completes once the host reaches <see cref="HostState.Stopped"/>.</returns>
    /// <exception cref="InvalidHostStateTransitionException">
    /// The host has not yet started (<see cref="HostState.Created"/>), or is
    /// already <see cref="HostState.Disposed"/>.
    /// </exception>
    /// <remarks>
    /// Calling this more than once is safe: a repeated call while a shutdown is
    /// already in progress escalates it — see <c>Shutdown Sequence.md</c>,
    /// "Cancellation During Shutdown."
    /// </remarks>
    Task StopAsync();
}
