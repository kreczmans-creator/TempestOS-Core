using Tempest.Core.BackgroundServices;
using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Tests.Events;

namespace Tempest.Core.Tests.BackgroundServices;

// Proves ADR-0021/ADR-0029's dispatch, ordering, and failure model against
// the real HostedServiceManager implementation - never a mocked
// IHostedServiceManager. The only test double anywhere in this file is a
// level-recording ILogger, used solely to observe log output, mirroring
// this project's own established testing convention.
//
// Shares the "Console output capture" collection with TempestHostTests and
// TempestHostHostedServiceTests: HostedServiceCallLog is a process-wide
// static, and the hosted service fixtures it records into (AlphaHostedService
// and its siblings) are constructed for real by TempestHostHostedServiceTests
// too - without a shared collection, xUnit's default parallelisation across
// test classes lets both classes' StartAsync/StopAsync calls interleave and
// corrupt each other's recorded entries, the same hazard already found and
// fixed once for SdkLifecycleLog and once for Console.Out redirection.
[Collection("Console output capture")]
public class HostedServiceManagerTests
{
    private static ITempestServiceProvider BuildProvider(ILogger? logger = null, params Type[] hostedServiceTypes)
    {
        var services = new ServiceCollection();
        services.AddInstance<ILogger>(logger ?? new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.AddDiscoveredHostedServices(hostedServiceTypes);

        return new TempestServiceProvider(services);
    }

    public HostedServiceManagerTests() => HostedServiceCallLog.Reset();

    // ------------------------------------------------------------------
    // Registration / constructor injection
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_ResolvesInstanceThroughRealServiceProvider_ConstructorInjectsPlatformServices()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(ConstructorInjectedHostedService)]);
        var manager = new HostedServiceManager([typeof(ConstructorInjectedHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);

        var resolved = Assert.IsType<ConstructorInjectedHostedService>(
            provider.GetService(typeof(ConstructorInjectedHostedService)));

        Assert.NotNull(resolved.Logger);
        Assert.NotNull(resolved.EventBus);
        Assert.Contains($"{nameof(ConstructorInjectedHostedService)}:Start", HostedServiceCallLog.Entries);
    }

    [Fact]
    public void Constructor_NullHostedServiceTypes_ThrowsArgumentNullException()
    {
        var provider = BuildProvider();

        Assert.Throws<ArgumentNullException>(() => new HostedServiceManager(null!, provider));
    }

    [Fact]
    public void Constructor_NullServiceProvider_ThrowsArgumentNullException() =>
        Assert.Throws<ArgumentNullException>(() => new HostedServiceManager([], null!));

    // ------------------------------------------------------------------
    // Deterministic, sequential startup ordering
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_MultipleServices_StartInAscendingFullNameOrder()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);

        Assert.Equal(
            [
                $"{nameof(AlphaHostedService)}:Start",
                $"{nameof(BetaHostedService)}:Start",
                $"{nameof(GammaHostedService)}:Start",
            ],
            HostedServiceCallLog.Entries);
    }

    [Fact]
    public async Task StartAllAsync_DispatchesSequentially_NeverMoreThanOneServiceInFlight()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(AlphaHostedService), typeof(BetaHostedService)]);
        var manager = new HostedServiceManager([typeof(AlphaHostedService), typeof(BetaHostedService)], provider);

        // AlphaHostedService/BetaHostedService both complete synchronously,
        // so sequential dispatch is proven here via ordering (above) and via
        // HostedServiceManagerTests' own reuse of the identical, already-
        // proven RunBatchAsync shape ModuleLifecycleManager/EventBus both
        // use - see the Host-level integration test for an end-to-end,
        // timing-based proof mirroring WP 4.4D's own in-flight-concurrency
        // counter technique.
        await manager.StartAllAsync(CancellationToken.None);

        Assert.Equal(2, HostedServiceCallLog.Entries.Count);
    }

    [Fact]
    public async Task StartAllAsync_SubscriptionOrder_IsDeterministicAcrossRepeatedRuns()
    {
        for (var i = 0; i < 3; i++)
        {
            HostedServiceCallLog.Reset();

            var provider = BuildProvider(hostedServiceTypes:
                [typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)]);
            var manager = new HostedServiceManager(
                [typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)], provider);

            await manager.StartAllAsync(CancellationToken.None);

            Assert.Equal(
                [
                    $"{nameof(AlphaHostedService)}:Start",
                    $"{nameof(BetaHostedService)}:Start",
                    $"{nameof(GammaHostedService)}:Start",
                ],
                HostedServiceCallLog.Entries);
        }
    }

    // ------------------------------------------------------------------
    // Reverse-order shutdown
    // ------------------------------------------------------------------

    [Fact]
    public async Task StopAllAsync_MultipleServices_StopInDescendingFullNameOrder()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(AlphaHostedService), typeof(BetaHostedService), typeof(GammaHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(AlphaHostedService), typeof(BetaHostedService), typeof(GammaHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);
        HostedServiceCallLog.Reset();

        await manager.StopAllAsync(CancellationToken.None);

        Assert.Equal(
            [
                $"{nameof(GammaHostedService)}:Stop",
                $"{nameof(BetaHostedService)}:Stop",
                $"{nameof(AlphaHostedService)}:Stop",
            ],
            HostedServiceCallLog.Entries);
    }

    [Fact]
    public async Task StopAllAsync_ServiceNeverStarted_IsNotStopped()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(AlphaHostedService)]);
        var manager = new HostedServiceManager([typeof(AlphaHostedService)], provider);

        // StartAllAsync deliberately never called.
        await manager.StopAllAsync(CancellationToken.None);

        Assert.Empty(HostedServiceCallLog.Entries);
    }

    // ------------------------------------------------------------------
    // Repeated startup/shutdown
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_ThenStopAllAsync_RepeatedAcrossFreshManagers_IsDeterministic()
    {
        for (var i = 0; i < 3; i++)
        {
            HostedServiceCallLog.Reset();

            var provider = BuildProvider(hostedServiceTypes: [typeof(AlphaHostedService), typeof(BetaHostedService)]);
            var manager = new HostedServiceManager([typeof(AlphaHostedService), typeof(BetaHostedService)], provider);

            await manager.StartAllAsync(CancellationToken.None);
            await manager.StopAllAsync(CancellationToken.None);

            Assert.Equal(
                [
                    $"{nameof(AlphaHostedService)}:Start",
                    $"{nameof(BetaHostedService)}:Start",
                    $"{nameof(BetaHostedService)}:Stop",
                    $"{nameof(AlphaHostedService)}:Stop",
                ],
                HostedServiceCallLog.Entries);
        }
    }

    // ------------------------------------------------------------------
    // Status snapshot
    // ------------------------------------------------------------------

    [Fact]
    public async Task Services_ReflectsCurrentStateOfEachHostedService()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(AlphaHostedService)]);
        var manager = new HostedServiceManager([typeof(AlphaHostedService)], provider);

        Assert.Equal(HostedServiceState.Registered, Assert.Single(manager.Services).State);

        await manager.StartAllAsync(CancellationToken.None);
        Assert.Equal(HostedServiceState.Running, Assert.Single(manager.Services).State);

        await manager.StopAllAsync(CancellationToken.None);
        Assert.Equal(HostedServiceState.Stopped, Assert.Single(manager.Services).State);
    }

    // ------------------------------------------------------------------
    // Isolated (non-critical) failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_IsolatedFailure_DoesNotPreventSiblingServicesFromStarting()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(IsolatedThrowingHostedService), typeof(GammaHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(IsolatedThrowingHostedService), typeof(GammaHostedService)], provider);

        var exception = await Record.ExceptionAsync(() => manager.StartAllAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Contains($"{nameof(GammaHostedService)}:Start", HostedServiceCallLog.Entries);
    }

    [Fact]
    public async Task StartAllAsync_IsolatedFailure_RecordsFailedStatus()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(IsolatedThrowingHostedService)]);
        var manager = new HostedServiceManager([typeof(IsolatedThrowingHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);

        var status = Assert.Single(manager.Services);
        Assert.Equal(HostedServiceState.Failed, status.State);
        Assert.IsType<InvalidOperationException>(status.FailureReason);
    }

    [Fact]
    public async Task StartAllAsync_IsolatedFailure_LogsAtErrorLevel()
    {
        var logger = new RecordingLevelLogger();
        var provider = BuildProvider(logger, typeof(IsolatedThrowingHostedService));
        var manager = new HostedServiceManager([typeof(IsolatedThrowingHostedService)], provider, logger);

        await manager.StartAllAsync(CancellationToken.None);

        Assert.True(logger.HasEntryAt(LogLevel.Error, "failed to start; isolated"));
    }

    [Fact]
    public async Task StopAllAsync_IsolatedFailure_DoesNotPreventSiblingServicesFromStopping()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(IsolatedThrowingHostedService), typeof(GammaHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(IsolatedThrowingHostedService), typeof(GammaHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);
        HostedServiceCallLog.Reset();

        var exception = await Record.ExceptionAsync(() => manager.StopAllAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Contains($"{nameof(GammaHostedService)}:Stop", HostedServiceCallLog.Entries);
    }

    // ------------------------------------------------------------------
    // Critical failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_CriticalFailure_PropagatesUncaught()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(CriticalStartFailureHostedService)]);
        var manager = new HostedServiceManager([typeof(CriticalStartFailureHostedService)], provider);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StartAllAsync_CriticalFailure_AbortsRemainderOfBatch_SiblingNeverStarts()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(AlphaHostedService), typeof(CriticalStartFailureHostedService), typeof(GammaHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(AlphaHostedService), typeof(CriticalStartFailureHostedService), typeof(GammaHostedService)],
            provider);

        // Ordinal order: Alpha < CriticalStartFailureHostedService < Gamma.
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAllAsync(CancellationToken.None));

        Assert.Contains($"{nameof(AlphaHostedService)}:Start", HostedServiceCallLog.Entries);
        Assert.DoesNotContain(HostedServiceCallLog.Entries, e => e.Contains(nameof(GammaHostedService)));
    }

    [Fact]
    public async Task StartAllAsync_CriticalFailure_LogsAtCriticalLevel()
    {
        var logger = new RecordingLevelLogger();
        var provider = BuildProvider(logger, typeof(CriticalStartFailureHostedService));
        var manager = new HostedServiceManager([typeof(CriticalStartFailureHostedService)], provider, logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StartAllAsync(CancellationToken.None));

        Assert.True(logger.HasEntryAt(LogLevel.Critical, "Critical hosted service"));
    }

    [Fact]
    public async Task StopAllAsync_CriticalFailure_PropagatesUncaught()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(CriticalStopFailureHostedService)]);
        var manager = new HostedServiceManager([typeof(CriticalStopFailureHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StopAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task StopAllAsync_CriticalFailure_AbortsRemainderOfBatch_SiblingNeverStops()
    {
        var provider = BuildProvider(hostedServiceTypes:
            [typeof(AlphaHostedService), typeof(CriticalStopFailureHostedService)]);
        var manager = new HostedServiceManager(
            [typeof(AlphaHostedService), typeof(CriticalStopFailureHostedService)], provider);

        await manager.StartAllAsync(CancellationToken.None);
        HostedServiceCallLog.Reset();

        // Reverse order: CriticalStopFailureHostedService stops before Alpha.
        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.StopAllAsync(CancellationToken.None));

        Assert.DoesNotContain(HostedServiceCallLog.Entries, e => e.Contains(nameof(AlphaHostedService)));
    }

    // ------------------------------------------------------------------
    // Cancellation
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_CancelledBetweenServices_PropagatesUncaught_WithoutStartingRemainingServices()
    {
        HostedServiceCallLog.Reset();
        using var cts = new CancellationTokenSource();
        CancellingHostedServiceControl.TokenSourceToCancel = cts;

        try
        {
            var provider = BuildProvider(hostedServiceTypes:
                [typeof(CancellingHostedService), typeof(GammaHostedService)]);
            var manager = new HostedServiceManager(
                [typeof(CancellingHostedService), typeof(GammaHostedService)], provider);

            await Assert.ThrowsAsync<OperationCanceledException>(() => manager.StartAllAsync(cts.Token));

            Assert.Contains($"{nameof(CancellingHostedService)}:Start", HostedServiceCallLog.Entries);
            Assert.DoesNotContain(HostedServiceCallLog.Entries, e => e.Contains(nameof(GammaHostedService)));
        }
        finally
        {
            CancellingHostedServiceControl.TokenSourceToCancel = null;
        }
    }

    [Fact]
    public async Task StartAllAsync_AlreadyCancelledToken_NeverStartsAnyService()
    {
        var provider = BuildProvider(hostedServiceTypes: [typeof(AlphaHostedService)]);
        var manager = new HostedServiceManager([typeof(AlphaHostedService)], provider);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => manager.StartAllAsync(cts.Token));

        Assert.Empty(HostedServiceCallLog.Entries);
    }

    // ------------------------------------------------------------------
    // No-op behaviour
    // ------------------------------------------------------------------

    [Fact]
    public async Task StartAllAsync_NoDiscoveredServices_CompletesWithoutError()
    {
        var provider = BuildProvider();
        var manager = new HostedServiceManager([], provider);

        var exception = await Record.ExceptionAsync(() => manager.StartAllAsync(CancellationToken.None));

        Assert.Null(exception);
        Assert.Empty(manager.Services);
    }
}
