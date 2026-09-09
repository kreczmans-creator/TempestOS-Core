using Tempest.Core.Runtime;

namespace Tempest.Core.Tests.Runtime;

/// <summary>
/// WP 17.0C: the single, shared replacement for the ~90 copy-pasted
/// <c>while (host.State is HostState.Created or HostState.Starting) await
/// Task.Delay(5);</c> loops that used to be hand-written at every test that
/// runs a real <see cref="ITempestHost"/> and needs to wait until it reaches
/// <see cref="HostState.Running"/> before exercising it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITempestHost"/> exposes no non-polling "reached Running" signal
/// — production code has exactly the same problem and solves it the same
/// way: <see cref="Tempest.Workspace.WorkspaceManager"/>'s own
/// <c>WaitForServicesAsync</c> polls <c>IDiagnosticsProvider.HostState</c>
/// with a bounded <c>Task.Delay</c> for the identical reason (see that
/// method's remarks, <c>TD-26</c>). <see cref="WaitUntilRunningAsync"/>
/// applies the same bounded poll here, at the test layer, so every call
/// site — whatever <see cref="ITempestHostBuilder"/> configuration it used to
/// construct its own <paramref name="host"/> — shares one implementation
/// instead of ~90 hand-rolled copies. Every call site still builds its
/// <see cref="ITempestHost"/> exactly as it did before (module list,
/// configuration sources, fault injection, and any additional
/// <c>ILogSink</c> a call site adds via WP 17.0C's own
/// <see cref="ITempestHostBuilder.AddLogSink"/> seam are all unchanged) —
/// only the polling loop itself is shared.
/// </para>
/// <para>
/// Unlike the loops it replaces, this helper is bounded by a real timeout,
/// not only by the state transition itself: a host wedged in
/// <see cref="HostState.Starting"/> forever used to hang the test process;
/// here it fails the test instead, with a message that names the state the
/// host was actually left in.
/// </para>
/// </remarks>
public static class RunningHostFixture
{
    /// <summary>
    /// The default bound <see cref="WaitUntilRunningAsync"/> polls for
    /// before giving up. Generous relative to every existing call site's own
    /// observed startup time (milliseconds) — this exists to fail a
    /// genuinely wedged host, not to constrain a healthy one.
    /// </summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Waits until <paramref name="host"/> leaves <see cref="HostState.Created"/>
    /// and <see cref="HostState.Starting"/> — i.e. until it reaches
    /// <see cref="HostState.Running"/> (the expected outcome) or
    /// <see cref="HostState.Faulted"/> (a startup failure the caller's own
    /// subsequent assertions are expected to observe, exactly as the
    /// polling loop this replaces already let happen: that loop's own exit
    /// condition was never conditioned on success).
    /// </summary>
    /// <param name="host">
    /// The host to wait on. Must already have had <see cref="ITempestHost.RunAsync"/>
    /// called — this method only observes <see cref="ITempestHost.State"/>,
    /// it never starts the host itself, since call sites build and start
    /// their hosts in many different ways.
    /// </param>
    /// <param name="timeout">
    /// The maximum time to wait before giving up. Defaults to
    /// <see cref="DefaultTimeout"/>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="host"/> is <see langword="null"/>.</exception>
    /// <exception cref="TimeoutException">
    /// <paramref name="host"/> was still <see cref="HostState.Created"/> or
    /// <see cref="HostState.Starting"/> when <paramref name="timeout"/> elapsed.
    /// </exception>
    public static async Task WaitUntilRunningAsync(ITempestHost host, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(host);

        var bound = timeout ?? DefaultTimeout;
        var deadline = DateTime.UtcNow + bound;

        while (host.State is HostState.Created or HostState.Starting)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException(
                    $"Host did not leave {HostState.Created}/{HostState.Starting} within {bound}; " +
                    $"its state is still {host.State}.");
            }

            await Task.Delay(5).ConfigureAwait(false);
        }
    }
}
