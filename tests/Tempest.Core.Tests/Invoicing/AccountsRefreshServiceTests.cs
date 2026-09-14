using Tempest.Core.BusinessGovernance;
using Tempest.Core.Configuration;
using Tempest.Core.Invoicing;
using Tempest.Core.Tests.Logging;
using Tempest.Core.Tests.Plugins;

namespace Tempest.Core.Tests.Invoicing;

/// <summary>
/// <see cref="AccountsRefreshService"/>'s own acceptance (`WP 19.8B` scope
/// §2): a scripted refresh saves a categorised reading, reloadable through
/// the store with the same values; the poller ticks on a controllable
/// <see cref="TimeProvider"/>; a connector failure — thrown, or an
/// ordinary non-<c>Ok</c> outcome — is isolated and keeps the last
/// reading, recording its own reason and time.
/// </summary>
public sealed class AccountsRefreshServiceTests
{
    [Fact]
    public async Task RefreshNowAsync_AScriptedFakeConnector_SavesACategorisedReading_ReloadableThroughTheStore()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var connector = new FakeInvoicingConnector();
        connector.ScriptBillsDue([new BillDue("Acme Ltd", "INV-1", new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 20), new Money(200m, CurrencyCode.Gbp), "AUTHORISED")]);
        connector.ScriptRepeatingBills(
        [
            new RepeatingBill("Contoso Cloud", "Software licence", new Money(99m, CurrencyCode.Gbp), "MONTHLY", new DateOnly(2026, 3, 15), "Software Subscription"),
            new RepeatingBill("Landlord Co", "Office rent", new Money(1200m, CurrencyCode.Gbp), "MONTHLY", new DateOnly(2026, 4, 1), "Office Rent"),
        ]);
        connector.ScriptCashPosition([new CashAccountBalance("Business Current Account", new Money(5000m, CurrencyCode.Gbp), new DateOnly(2026, 3, 10))]);

        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));
        var configuration = EmptyConfiguration();
        var service = new AccountsRefreshService(connector, store, configuration, timeProvider: time);

        await service.RefreshNowAsync();

        var reading = await store.ReadAsync();
        Assert.NotNull(reading);
        Assert.Equal("Fake", reading!.Connector);
        Assert.Equal(time.GetUtcNow(), reading.ReadAt);

        var bill = Assert.Single(reading.Bills);
        Assert.Equal("Acme Ltd", bill.Supplier);

        Assert.Equal(2, reading.RepeatingBills.Count);
        Assert.Contains(reading.RepeatingBills, r => r.Bill.Supplier == "Contoso Cloud" && r.Category == AccountsCategory.Software);
        Assert.Contains(reading.RepeatingBills, r => r.Bill.Supplier == "Landlord Co" && r.Category == AccountsCategory.Premises);

        var cash = Assert.Single(reading.Cash);
        Assert.Equal(new Money(5000m, CurrencyCode.Gbp), cash.Balance);

        Assert.Null(service.LastFailureReason);
        Assert.Null(service.LastFailureAtUtc);
    }

    [Fact]
    public async Task RefreshNowAsync_ListBillsDueAsksForANinetyDayHorizon()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var connector = new FakeInvoicingConnector();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));
        var service = new AccountsRefreshService(connector, store, EmptyConfiguration(), timeProvider: time);

        // A bill due at day 89 is inside the horizon; one at day 91 is
        // outside it — proving the connector was asked for (and this
        // service kept) a 90-day window, not the 30/60-day dashboard
        // buckets themselves.
        connector.ScriptBillsDue(
        [
            new BillDue("In Range", "R1", new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 7), new Money(1m, CurrencyCode.Gbp), "AUTHORISED"),
            new BillDue("Out Of Range", "R2", new DateOnly(2026, 3, 1), new DateOnly(2026, 6, 12), new Money(1m, CurrencyCode.Gbp), "AUTHORISED"),
        ]);

        await service.RefreshNowAsync();

        var reading = await store.ReadAsync();
        var bill = Assert.Single(reading!.Bills);
        Assert.Equal("In Range", bill.Supplier);
    }

    [Fact]
    public async Task TickingTheTimeProvider_RefreshesAndSaves()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var connector = new FakeInvoicingConnector();
        connector.ScriptCashPosition([new CashAccountBalance("Main", new Money(100m, CurrencyCode.Gbp), new DateOnly(2026, 3, 10))]);

        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 10, 9, 0, 0, TimeSpan.Zero));
        var service = new AccountsRefreshService(connector, store, EmptyConfiguration(), timeProvider: time);

        await service.StartAsync(CancellationToken.None);
        time.Tick();
        await service.WaitForPendingTickAsync();
        await service.StopAsync(CancellationToken.None);

        var reading = await store.ReadAsync();
        Assert.NotNull(reading);
        Assert.Single(reading!.Cash);
    }

    [Fact]
    public async Task RefreshNowAsync_AConnectorAnswerOtherThanOk_KeepsTheLastReading_RecordsTheFailure()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var connector = new FakeInvoicingConnector();
        connector.ScriptCashPosition([new CashAccountBalance("Main", new Money(100m, CurrencyCode.Gbp), new DateOnly(2026, 3, 1))]);

        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var service = new AccountsRefreshService(connector, store, EmptyConfiguration(), timeProvider: time);
        await service.RefreshNowAsync();
        var firstReading = await store.ReadAsync();
        Assert.NotNull(firstReading);

        // The second refresh fails outright — never authorised.
        connector.ScriptAuthorisationState(new ConnectorAuthorisationState(ConnectorAuthorisation.NotAuthorised));
        time.Advance(TimeSpan.FromHours(1));

        await service.RefreshNowAsync();

        var stillFirstReading = await store.ReadAsync();
        Assert.Equal(firstReading!.ReadAt, stillFirstReading!.ReadAt);

        Assert.NotNull(service.LastFailureReason);
        Assert.Contains("bills", service.LastFailureReason!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(time.GetUtcNow(), service.LastFailureAtUtc);
    }

    [Fact]
    public async Task RefreshNowAsync_AThrowingConnector_IsIsolated_LoggedAndTheHostKeepsRunning()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var recordingLogger = new RecordingLogger();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var service = new AccountsRefreshService(new ThrowingAccountsConnector(), store, EmptyConfiguration(), recordingLogger, time);

        var exception = await Record.ExceptionAsync(() => service.RefreshNowAsync());

        Assert.Null(exception);
        Assert.Contains(recordingLogger.Messages, m => m.Contains("Accounts refresh failed", StringComparison.Ordinal));
        Assert.NotNull(service.LastFailureReason);
        Assert.Equal(time.GetUtcNow(), service.LastFailureAtUtc);
        Assert.Null(await store.ReadAsync());
    }

    [Fact]
    public async Task StartAsync_UsesTheConfiguredPollMinutes_NotTheDefault()
    {
        using var temp = new TempDirectory();
        var store = new FileAccountsReadingStore(temp.Path);
        var connector = new FakeInvoicingConnector();
        var time = new ManualTimeProvider(new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        var configuration = new ConfigurationBuilder()
            .AddSource(new MemoryConfigurationSource([new(AccountsRefreshService.PollMinutesConfigurationKey, "5")]))
            .Build();
        var service = new AccountsRefreshService(connector, store, configuration, timeProvider: time);

        await service.StartAsync(CancellationToken.None);
        Assert.Equal(TimeSpan.FromMinutes(5), time.LastPeriod);

        await service.StopAsync(CancellationToken.None);
    }

    private static IConfigurationProvider EmptyConfiguration() =>
        new ConfigurationBuilder().AddSource(new MemoryConfigurationSource([])).Build();

    /// <summary>A connector that always throws — proves a defect in an implementation is isolated exactly as an ordinary <see cref="ConnectorOutcome"/> is.</summary>
    private sealed class ThrowingAccountsConnector : IAccountsConnector
    {
        public string Name => "Throwing";

        public Task<ConnectorResult<IReadOnlyList<BillDue>>> ListBillsDueAsync(DateOnly asOf, int horizonDays, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorResult<IReadOnlyList<RepeatingBill>>> ListRepeatingBillsAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");

        public Task<ConnectorResult<IReadOnlyList<CashAccountBalance>>> ReadCashPositionAsync(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The connector broke.");
    }

    /// <summary>
    /// A <see cref="TimeProvider"/> whose <see cref="CreateTimer"/> hands
    /// back a controllable timer a test ticks directly, and whose
    /// <see cref="GetUtcNow"/> is advanced by hand — mirrors
    /// <c>InvoiceReconciliationServiceTests</c>'s own private
    /// <c>ManualTimeProvider</c>, widened with a settable clock and the
    /// last requested timer period, both needed by this file's own tests.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        private TimerCallback? _callback;
        private object? _state;

        public ManualTimeProvider(DateTimeOffset now) => _now = now;

        public TimeSpan LastPeriod { get; private set; }

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _callback = callback;
            _state = state;
            LastPeriod = period;

            return new ManualTimer();
        }

        public void Tick() => _callback?.Invoke(_state);

        private sealed class ManualTimer : ITimer
        {
            public bool Change(TimeSpan dueTime, TimeSpan period) => true;

            public void Dispose()
            {
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
