using Tempest.Core.BackgroundServices;
using Tempest.Core.Logging;

namespace Tempest.Core.Invoicing.Xero.Sync;

/// <summary>
/// Runs the Xero sync engine (<see cref="XeroSyncService"/>, `v0.24.0` X6)
/// in the background for as long as the host runs: start-up (lost answers
/// recovered first, then the full scan), then a cycle on every wake — a
/// saved change, a Retry, a re-authorisation, the next due retry, or every
/// <see cref="XeroSyncOptions.SyncInterval"/> (default 60 s).
/// </summary>
/// <remarks>
/// <para>
/// <b>Registered like every other background service</b>: by the platform's
/// reflection-based hosted-service discovery (`ADR-0029`;
/// <see cref="HostedServiceDiscoveryService"/>, as
/// <see cref="InvoiceReconciliationService"/> and
/// <see cref="AccountsRefreshService"/> are) — no registration line names
/// it. Its engine is optional: in a host whose connector is not Xero, no
/// <see cref="XeroSyncService"/> is registered, and this service starts and
/// stops doing nothing.
/// </para>
/// <para>
/// <b>Non-critical, every failure isolated</b> (`ADR-0021`): an ordinary
/// <see cref="IHostedService"/>. Start returns at once (the work runs on its
/// own loop); a failing cycle is logged and the loop carries on; Stop cancels
/// the loop and waits for it — a request cut short is left InFlight and
/// recovered at the next start-up.
/// </para>
/// </remarks>
public sealed class XeroSyncHostedService : IHostedService
{
    private readonly XeroSyncService? _engine;
    private readonly ILogger? _logger;
    private CancellationTokenSource? _stopping;
    private Task _loop = Task.CompletedTask;
    private int _cycles;

    /// <summary>Initialises a new instance of the <see cref="XeroSyncHostedService"/> class.</summary>
    /// <param name="engine">The Xero sync engine; <see langword="null"/> (no Xero connector in this host) does nothing.</param>
    /// <param name="logger">The logger; <see langword="null"/> logs nothing.</param>
    public XeroSyncHostedService(XeroSyncService? engine = null, ILogger? logger = null)
    {
        _engine = engine;
        _logger = logger;
    }

    /// <summary>Whether this host has a Xero sync engine to run.</summary>
    public bool IsEnabled => _engine is not null;

    /// <summary>How many cycles the loop has completed (for diagnostics and tests).</summary>
    public int CompletedCycles => Volatile.Read(ref _cycles);

    /// <summary>The running loop; completed when stopped or never started. Internal test seam.</summary>
    internal Task Loop => _loop;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_engine is null)
        {
            _logger?.Information("Xero sync is not configured in this host (the invoicing connector is not Xero); the Xero sync service does nothing.");
            return Task.CompletedTask;
        }

        _stopping = new CancellationTokenSource();
        var token = _stopping.Token;
        _loop = Task.Run(() => RunAsync(_engine, token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_engine is null || _stopping is null)
            return;

        await _stopping.CancelAsync().ConfigureAwait(false);
        try
        {
            await _loop.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Stopping: a request cut short stays InFlight and is recovered at the next start-up.
        }
        finally
        {
            _engine.StopListening();
            _stopping.Dispose();
            _stopping = null;
        }
    }

    private async Task RunAsync(XeroSyncService engine, CancellationToken stopping)
    {
        try
        {
            await engine.StartAsync(stopping).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger?.Error("Xero sync start-up failed; isolated, the engine retries on its next cycle.", ex);
        }

        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await engine.RunCycleAsync(stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.Error("A Xero sync cycle failed; isolated, the next cycle runs as usual.", ex);
            }

            Interlocked.Increment(ref _cycles);

            try
            {
                var wait = engine.Options.SyncInterval;
                if (await engine.NextWorkDueAtAsync(stopping).ConfigureAwait(false) is { } due)
                {
                    var untilDue = due - engine.Clock.GetUtcNow();
                    if (untilDue < wait)
                        wait = untilDue < TimeSpan.Zero ? TimeSpan.Zero : untilDue;
                }

                await engine.WaitForWorkAsync(wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1) : wait, stopping).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger?.Error("The Xero sync loop could not work out its next wake; it waits one sync interval.", ex);
            }
        }
    }
}
