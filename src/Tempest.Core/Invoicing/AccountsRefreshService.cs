using Tempest.Core.BackgroundServices;
using Tempest.Core.Configuration;
using Tempest.Core.Logging;

namespace Tempest.Core.Invoicing;

/// <summary>
/// The background poller: every <see cref="PollMinutesConfigurationKey"/>
/// minutes (default <see cref="DefaultPollMinutes"/>), reads
/// <see cref="IAccountsConnector"/>, categorises every repeating bill and
/// saves the result through <see cref="IAccountsReadingStore"/>
/// (`WP 19.8B`, po-comments.md item 8, scope §2). Mirrors
/// <see cref="InvoiceReconciliationService"/>'s own shape exactly: an
/// ordinary <see cref="IHostedService"/> (not
/// <see cref="ICriticalBackgroundService"/>), discovered the same
/// reflection-based way, a <see cref="TimeProvider"/>-driven timer for the
/// identical reason — a fake, manually-ticked provider drives this
/// deterministically in a test rather than waiting on real wall-clock
/// minutes.
/// </summary>
/// <remarks>
/// <para>
/// <b>A failed refresh keeps the last reading.</b> This service never
/// writes anything to <see cref="IAccountsReadingStore"/> except a whole,
/// successfully-read <see cref="AccountsReading"/> — a failure (any of the
/// three connector reads answering other than <see cref="ConnectorOutcome.Ok"/>,
/// or an unhandled exception from a connector implementation's own defect)
/// leaves the store exactly as it was and only records the failure's own
/// reason and time, through <see cref="LastFailureReason"/>/
/// <see cref="LastFailureAtUtc"/>, for <see cref="IAccountsReadModel"/> to
/// report when there has never been a successful reading at all.
/// </para>
/// <para>
/// <b>Every failure is isolated</b>, exactly as
/// <see cref="InvoiceReconciliationService"/>'s own remarks describe: a
/// connector implementation that throws is caught, logged, and recorded as
/// a failure like any other outcome — never a crash that takes the host
/// down.
/// </para>
/// </remarks>
public sealed class AccountsRefreshService : IHostedService
{
    /// <summary>The <see cref="IConfigurationProvider"/> key naming how many minutes elapse between refreshes.</summary>
    public const string PollMinutesConfigurationKey = "Accounts:PollMinutes";

    /// <summary>The poll interval used when <see cref="PollMinutesConfigurationKey"/> is not configured, or is configured to something other than a positive integer.</summary>
    public const int DefaultPollMinutes = 60;

    /// <summary>How far ahead <see cref="IAccountsConnector.ListBillsDueAsync"/> is asked to read — wide enough to cover both the 30-day payable tile and the 12-week (84-day) cash-flow series <see cref="AccountsSnapshot"/> computes from this reading.</summary>
    internal const int BillsHorizonDays = 90;

    private readonly IAccountsConnector _connector;
    private readonly IAccountsReadingStore _store;
    private readonly IConfigurationProvider _configuration;
    private readonly AccountsCategoriser _categoriser;
    private readonly ILogger? _logger;
    private readonly TimeProvider _time;
    private readonly object _gate = new();

    private ITimer? _timer;
    private Task _lastRun = Task.CompletedTask;
    private string? _lastFailureReason;
    private DateTimeOffset? _lastFailureAtUtc;

    /// <summary>Initialises a new instance of the <see cref="AccountsRefreshService"/> class.</summary>
    public AccountsRefreshService(
        IAccountsConnector connector, IAccountsReadingStore store, IConfigurationProvider configuration,
        ILogger? logger = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(connector);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(configuration);

        _connector = connector;
        _store = store;
        _configuration = configuration;
        _logger = logger;
        _time = timeProvider ?? TimeProvider.System;
        _categoriser = new AccountsCategoriser(configuration);
    }

    /// <summary>The reason the most recent refresh attempt failed, or <see langword="null"/> if the most recent attempt (if any) succeeded.</summary>
    public string? LastFailureReason
    {
        get { lock (_gate) return _lastFailureReason; }
    }

    /// <summary>When the most recent failed refresh attempt happened, or <see langword="null"/> if the most recent attempt (if any) succeeded.</summary>
    public DateTimeOffset? LastFailureAtUtc
    {
        get { lock (_gate) return _lastFailureAtUtc; }
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

    private void OnTick(object? state) => _lastRun = RefreshNowAsync(CancellationToken.None);

    /// <summary>Reads the connector and saves the result — the Settings area's own "Refresh now" entry point, and what every timer tick calls.</summary>
    public async Task RefreshNowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var asOf = DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);

            var billsResult = await _connector.ListBillsDueAsync(asOf, BillsHorizonDays, cancellationToken).ConfigureAwait(false);
            if (billsResult.Outcome != ConnectorOutcome.Ok)
            {
                RecordFailure(Describe("bills", billsResult.Outcome, billsResult.Reason));
                return;
            }

            var repeatingResult = await _connector.ListRepeatingBillsAsync(cancellationToken).ConfigureAwait(false);
            if (repeatingResult.Outcome != ConnectorOutcome.Ok)
            {
                RecordFailure(Describe("repeating bills", repeatingResult.Outcome, repeatingResult.Reason));
                return;
            }

            var cashResult = await _connector.ReadCashPositionAsync(cancellationToken).ConfigureAwait(false);
            if (cashResult.Outcome != ConnectorOutcome.Ok)
            {
                RecordFailure(Describe("cash position", cashResult.Outcome, cashResult.Reason));
                return;
            }

            var categorised = repeatingResult.Value!
                .Select(bill => new CategorisedRepeatingBill(bill, _categoriser.Categorise(bill.AccountName)))
                .ToList();

            var reading = new AccountsReading(billsResult.Value!, categorised, cashResult.Value!, _time.GetUtcNow(), _connector.Name);
            await _store.SaveAsync(reading, cancellationToken).ConfigureAwait(false);

            lock (_gate)
            {
                _lastFailureReason = null;
                _lastFailureAtUtc = null;
            }
        }
        catch (Exception ex)
        {
            _logger?.Error("Accounts refresh failed; isolated, the host keeps running and the last reading (if any) is kept.", ex);
            RecordFailure(ex.Message);
        }
    }

    /// <summary>Awaits whatever <see cref="RefreshNowAsync"/> the most recent timer tick started. Internal test seam — a fake, manually-ticked <see cref="TimeProvider"/> invokes the timer callback synchronously, but the refresh it starts is still asynchronous.</summary>
    internal Task WaitForPendingTickAsync() => _lastRun;

    private void RecordFailure(string reason)
    {
        lock (_gate)
        {
            _lastFailureReason = reason;
            _lastFailureAtUtc = _time.GetUtcNow();
        }

        _logger?.Warning($"Accounts refresh failed: {reason}. The last reading, if any, is kept.");
    }

    private static string Describe(string what, ConnectorOutcome outcome, string? reason) =>
        reason is { Length: > 0 } ? $"Could not read {what}: {reason}" : $"Could not read {what} ({outcome}).";

    private int ResolvePollMinutes() =>
        _configuration.TryGetValue(PollMinutesConfigurationKey, out var raw) && int.TryParse(raw, out var minutes) && minutes > 0
            ? minutes
            : DefaultPollMinutes;
}
