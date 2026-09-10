using Tempest.Core.BackgroundServices;
using Tempest.Core.Configuration;
using Tempest.Core.EngineeringDomain;
using Tempest.Core.Logging;

namespace Tempest.Core.Invoicing;

/// <summary>
/// The background poller: every <see cref="PollMinutesConfigurationKey"/>
/// minutes (default <see cref="DefaultPollMinutes"/>), reconciles every
/// live <see cref="InvoiceRequest"/> whose own <see cref="InvoiceRequest.Status"/>
/// is <see cref="InvoiceRequestStatus.Unknown"/>, <see cref="InvoiceRequestStatus.Sent"/>
/// or <see cref="InvoiceRequestStatus.Accepted"/> through
/// <see cref="IInvoicingService.ReconcileAsync"/> (`WP 19.1A`, `ADR-0151`).
/// Registered by the platform's own reflection-based hosted-service
/// discovery (<see cref="HostedServiceDiscoveryService"/>) like every other
/// <see cref="IHostedService"/> — no explicit registration line names this
/// type anywhere.
/// </summary>
/// <remarks>
/// <para>
/// <b>Non-critical, and every failure is isolated.</b> This is an ordinary
/// <see cref="IHostedService"/>, not an <see cref="ICriticalBackgroundService"/>:
/// a request that never reconciles is exactly as safe as one nobody has
/// looked at yet — nothing depends on this poller running for the platform
/// itself to be correct. A failure listing pending requests, or reconciling
/// any one of them, is caught, logged, and skipped; the loop always
/// continues to the next request and the next tick, matching the Work
/// Package row's own words: "a connector failure inside the poller is
/// logged and the host keeps running."
/// </para>
/// <para>
/// <b>A <see cref="TimeProvider"/>-driven timer, deliberately.</b> The
/// timer is built from the injected <see cref="TimeProvider"/>
/// (<see cref="TimeProvider.CreateTimer"/>) rather than a bare
/// <see cref="System.Threading.Timer"/>, so a test supplies a
/// <see cref="TimeProvider"/> whose own <c>CreateTimer</c> hands back a
/// controllable timer and ticks it directly, rather than waiting on real
/// wall-clock minutes.
/// </para>
/// </remarks>
public sealed class InvoiceReconciliationService : IHostedService
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming how many minutes elapse between reconciliation polls.</summary>
    public const string PollMinutesConfigurationKey = "Invoicing:PollMinutes";

    /// <summary>The poll interval used when <see cref="PollMinutesConfigurationKey"/> is not configured, or is configured to something other than a positive integer.</summary>
    public const int DefaultPollMinutes = 15;

    private readonly IInvoicingService _invoicing;
    private readonly EngineeringDomainContext _context;
    private readonly IConfigurationProvider _configuration;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;

    private ITimer? _timer;
    private Task _lastRun = Task.CompletedTask;

    /// <summary>Initialises a new instance of the <see cref="InvoiceReconciliationService"/> class.</summary>
    public InvoiceReconciliationService(
        IInvoicingService invoicing, EngineeringDomainContext context, IConfigurationProvider configuration,
        ILogger? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(invoicing);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(configuration);

        _invoicing = invoicing;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var period = TimeSpan.FromMinutes(ResolvePollMinutes());
        _timer = _time.CreateTimer(OnTick, state: null, period, period);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _timer?.Dispose();
        _timer = null;

        return Task.CompletedTask;
    }

    private void OnTick(object? state) => _lastRun = RunOnceAsync(CancellationToken.None);

    /// <summary>Runs one reconciliation pass over every pending request, isolating every failure. Internal test seam: called directly by a test rather than through the timer, and awaited rather than fired-and-forgotten.</summary>
    internal async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        List<InvoiceRequest> pending;

        try
        {
            var all = await _context.Repository.ListByKindAsync(InvoiceRequest.CanonicalKind, cancellationToken).ConfigureAwait(false);

            pending = all
                .OfType<InvoiceRequest>()
                .Where(r => IsLive(r)
                    && r.Status is InvoiceRequestStatus.Unknown or InvoiceRequestStatus.Sent or InvoiceRequestStatus.Accepted)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger?.Error("Invoice reconciliation poll could not list pending requests; isolated, the host keeps running.", ex);
            return;
        }

        foreach (var request in pending)
        {
            try
            {
                await _invoicing.ReconcileAsync(request.Id, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.Error($"Invoice reconciliation failed for request '{request.Id}'; isolated, the host keeps running.", ex);
            }
        }
    }

    /// <summary>Awaits whatever <see cref="RunOnceAsync"/> the most recent timer tick started. Internal test seam — a fake, manually-ticked <see cref="TimeProvider"/> invokes the timer callback synchronously, but the reconciliation work it starts is still asynchronous.</summary>
    internal Task WaitForPendingTickAsync() => _lastRun;

    private int ResolvePollMinutes() =>
        _configuration.TryGetValue(PollMinutesConfigurationKey, out var raw) && int.TryParse(raw, out var minutes) && minutes > 0
            ? minutes
            : DefaultPollMinutes;

    private static bool IsLive(IEngineeringObject o) => o is not IDeletable { IsDeleted: true };
}
