namespace Tempest.Core.BackgroundServices;

/// <summary>
/// Background work that starts alongside, and stops symmetrically with, the
/// module pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Unrelated to <c>Microsoft.Extensions.Hosting.IHostedService</c>. TempestOS
/// has no dependency on that package (ADR-0005); the name is TempestOS's own,
/// chosen because it is the clearest description of what this contract is —
/// see ADR-0024 for why it was kept rather than renamed to avoid the
/// coincidence.
/// </para>
/// <para>
/// This is a contract only — <c>Tempest.Core.Runtime</c> gains no wiring to
/// start or stop a hosted service until a later work package implements it.
/// Declaring this interface today does not change the Runtime Host's
/// behaviour: it still starts and stops exactly as it did at the end of
/// v0.3.0.
/// </para>
/// <para>
/// A hosted service's failure is isolated by default, not Host-fatal — see
/// ADR-0021. A service that must be Host-fatal on failure additionally
/// implements <see cref="ICriticalBackgroundService"/>.
/// </para>
/// </remarks>
public interface IHostedService
{
    /// <summary>
    /// Starts the background service. Invoked once, between Module
    /// Initialisation and Runtime Running.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops the background service. Invoked once, at the front of
    /// Shutdown, symmetrically with <see cref="StartAsync"/>.
    /// </summary>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    Task StopAsync(CancellationToken cancellationToken);
}
