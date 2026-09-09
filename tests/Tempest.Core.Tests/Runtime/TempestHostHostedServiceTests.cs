using Tempest.Core.Runtime;
using Tempest.Core.Tests.BackgroundServices;
using Tempest.Core.Tests.Logging;

namespace Tempest.Core.Tests.Runtime;

// Proves ADR-0029/ADR-0030 end-to-end: a hosted service starts after Module
// Initialisation and stops before Module Disposal, through the real,
// unmodified TempestHost - no test-only wiring, no mock IHostedServiceManager.
//
// Deliberately does not use HostedServiceCallLog (BackgroundServices'
// own shared, static call log) - that log is used only within
// HostedServiceManagerTests' own single class, where xUnit's default
// "sequential within a class" behaviour already makes it safe. Sharing it
// here too, across two separate test classes that xUnit may run
// concurrently by default, would reintroduce exactly the cross-test-class
// static-state race already found and fixed once for SdkLifecycleLog - each
// test attaches its own private RecordingLogSink (WP 17.0C) rather than
// redirecting the process-global Console.Out, so no cross-class collection
// is needed at all.
public class TempestHostHostedServiceTests
{
    private static ITempestHostBuilder BuilderWithHostedServices(params Type[] hostedServiceTypes) =>
        new TempestHostBuilder(discoveryCandidateTypesOverride: Type.EmptyTypes,
            pluginsRootPathOverride: null,
            hostedServiceCandidateTypesOverride: hostedServiceTypes)
            .WithIsolatedPersistenceRoot();

    private static async Task<string> RunAndCollectLogMessagesAsync(RecordingLogSink sink, Func<Task> duringRun)
    {
        await duringRun();

        return string.Join(Environment.NewLine, sink.Entries.Select(entry => entry.Message));
    }

    [Fact]
    public async Task RunAsync_WithHostedService_ReachesRunningThenStopsGracefully()
    {
        var sink = new RecordingLogSink();
        var host = BuilderWithHostedServices(typeof(AlphaHostedService)).AddLogSink(sink).Build();

        var output = await RunAndCollectLogMessagesAsync(sink, async () =>
        {
            var runTask = host.RunAsync();

            await RunningHostFixture.WaitUntilRunningAsync(host);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        });

        Assert.Equal(HostState.Stopped, host.State);
        Assert.Contains($"Hosted service '{typeof(AlphaHostedService).FullName}' -> Running.", output);
        Assert.Contains($"Hosted service '{typeof(AlphaHostedService).FullName}' -> Stopped.", output);
    }

    [Fact]
    public async Task RunAsync_LogsHostedServicesStartedAndStoppedPhases()
    {
        var sink = new RecordingLogSink();
        var host = BuilderWithHostedServices(typeof(AlphaHostedService)).AddLogSink(sink).Build();

        var output = await RunAndCollectLogMessagesAsync(sink, async () =>
        {
            var runTask = host.RunAsync();
            await host.StopAsync();
            await runTask;
        });

        Assert.Contains("Host lifecycle phase completed: Hosted Services Started.", output);
        Assert.Contains("Host lifecycle phase completed: Hosted Services Stopped.", output);
    }

    [Fact]
    public async Task RunAsync_HostedServiceStartsAfterModuleInitialisation_StopsBeforeModuleDisposal()
    {
        var sink = new RecordingLogSink();
        var host = new TempestHostBuilder(
                discoveryCandidateTypesOverride: [typeof(HealthyHostTestModuleAlpha)],
                pluginsRootPathOverride: null,
                hostedServiceCandidateTypesOverride: [typeof(AlphaHostedService)])
            .AddLogSink(sink).WithIsolatedPersistenceRoot()
            .Build();

        var output = await RunAndCollectLogMessagesAsync(sink, async () =>
        {
            var runTask = host.RunAsync();
            await host.StopAsync();
            await runTask;
        });

        var lines = output
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var moduleInitIndex = lines.FindIndex(line => line.Contains("Host lifecycle phase completed: Module Initialisation."));
        var hostedStartedIndex = lines.FindIndex(line => line.Contains("Host lifecycle phase completed: Hosted Services Started."));
        var stoppingIndex = lines.FindIndex(line => line.Contains("Host -> Stopping."));
        var hostedStoppedIndex = lines.FindIndex(line => line.Contains("Host lifecycle phase completed: Hosted Services Stopped."));
        var moduleDisposalIndex = lines.FindIndex(line => line.Contains("Host lifecycle phase completed: Module Disposal (Stop)."));

        Assert.True(moduleInitIndex >= 0 && hostedStartedIndex > moduleInitIndex, "Hosted Services Started must follow Module Initialisation.");
        Assert.True(stoppingIndex >= 0 && hostedStoppedIndex > stoppingIndex, "Hosted Services Stopped must follow the shutdown request.");
        Assert.True(moduleDisposalIndex >= 0 && moduleDisposalIndex > hostedStoppedIndex, "Hosted Services Stopped must precede Module Disposal.");
    }

    [Fact]
    public async Task RunAsync_MultipleHostedServices_StartInDeterministicOrder_AllReachRunning()
    {
        var sink = new RecordingLogSink();
        var host = BuilderWithHostedServices(typeof(GammaHostedService), typeof(AlphaHostedService), typeof(BetaHostedService)).AddLogSink(sink).Build();

        var output = await RunAndCollectLogMessagesAsync(sink, async () =>
        {
            var runTask = host.RunAsync();

            await RunningHostFixture.WaitUntilRunningAsync(host);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        });

        var alphaIndex = output.IndexOf($"Hosted service '{typeof(AlphaHostedService).FullName}' -> Starting.", StringComparison.Ordinal);
        var betaIndex = output.IndexOf($"Hosted service '{typeof(BetaHostedService).FullName}' -> Starting.", StringComparison.Ordinal);
        var gammaIndex = output.IndexOf($"Hosted service '{typeof(GammaHostedService).FullName}' -> Starting.", StringComparison.Ordinal);

        Assert.True(alphaIndex >= 0 && betaIndex > alphaIndex && gammaIndex > betaIndex,
            "Hosted services must start in ascending FullName order regardless of discovery input order.");

        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_IsolatedHostedServiceFailure_HostStillReachesRunning()
    {
        var sink = new RecordingLogSink();
        var host = BuilderWithHostedServices(typeof(IsolatedThrowingHostedService), typeof(GammaHostedService)).AddLogSink(sink).Build();

        var output = await RunAndCollectLogMessagesAsync(sink, async () =>
        {
            var runTask = host.RunAsync();

            await RunningHostFixture.WaitUntilRunningAsync(host);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        });

        Assert.Contains($"Hosted service '{typeof(GammaHostedService).FullName}' -> Running.", output);
        Assert.Contains("failed to start; isolated", output);
        Assert.Equal(HostState.Stopped, host.State);
    }

    [Fact]
    public async Task RunAsync_CriticalHostedServiceStartFailure_HostFaults()
    {
        var host = BuilderWithHostedServices(typeof(CriticalStartFailureHostedService)).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunAsync());

        Assert.Equal(HostState.Faulted, host.State);
    }

    [Fact]
    public async Task RunAsync_CriticalHostedServiceStartFailure_DoesNotPreventDisposal()
    {
        var host = BuilderWithHostedServices(typeof(CriticalStartFailureHostedService)).Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => host.RunAsync());
        Assert.Equal(HostState.Faulted, host.State);

        await host.DisposeAsync();

        Assert.Equal(HostState.Disposed, host.State);
    }

    [Fact]
    public async Task StopAsync_CriticalHostedServiceStopFailure_HostFaultsButDisposalStillCompletes()
    {
        var host = BuilderWithHostedServices(typeof(CriticalStopFailureHostedService)).Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => runTask);
        Assert.Equal(HostState.Faulted, host.State);

        await host.DisposeAsync();

        Assert.Equal(HostState.Disposed, host.State);
    }

    [Fact]
    public async Task RunAsync_RepeatedAcrossFreshHosts_ReachesStoppedEveryTime()
    {
        // ITempestHost is single-use (ADR-0015); repeated execution is
        // proven with a fresh TempestHostBuilder/TempestHost pair each time.
        for (var i = 0; i < 2; i++)
        {
            var sink = new RecordingLogSink();
            var host = BuilderWithHostedServices(typeof(AlphaHostedService)).AddLogSink(sink).Build();

            var output = await RunAndCollectLogMessagesAsync(sink, async () =>
            {
                var runTask = host.RunAsync();

                await RunningHostFixture.WaitUntilRunningAsync(host);

                Assert.Equal(HostState.Running, host.State);

                await host.StopAsync();
                await runTask;
            });

            Assert.Equal(HostState.Stopped, host.State);
            Assert.Contains($"Hosted service '{typeof(AlphaHostedService).FullName}' -> Running.", output);
            Assert.Contains($"Hosted service '{typeof(AlphaHostedService).FullName}' -> Stopped.", output);
        }
    }

    [Fact]
    public async Task RunAsync_NoHostedServicesDiscovered_BehavesExactlyAsBefore()
    {
        var host = BuilderWithHostedServices().Build();

        var runTask = host.RunAsync();

        await RunningHostFixture.WaitUntilRunningAsync(host);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
