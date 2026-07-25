using Tempest.Core.DependencyInjection;
using Tempest.Core.Events;
using Tempest.Core.Logging;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Core.Tests.Events;
using Tempest.Samples;

namespace Tempest.Core.Tests.Samples;

// Proves WP 4.4E end-to-end: ClockModule constructor-injects the real,
// unmodified IEventBus (WP 4.4D) and publishes its lifecycle transitions
// through it; ClockLifecycleObserverModule - a second, real, SDK-conformant
// module holding no reference of any kind to ClockModule - subscribes and
// receives them, resolved entirely through constructor injection (ADR-0020).
// Every piece composed below is the same production type TempestHost itself
// composes internally; nothing here is a mock or a test double standing in
// for a real platform service, except a level-recording ILogger used only to
// observe log output, mirroring WP 4.4D's own test conventions.
public class ClockModuleEventIntegrationTests
{
    private static (RuntimeModuleManager RuntimeManager, TempestServiceProvider ServiceProvider) BuildPipeline(
        params Type[] moduleTypes)
    {
        var descriptors = new ReflectionFrameworkDiscoveryService([typeof(ClockModule).Assembly])
            .DiscoverModules(moduleTypes);

        var runtimeManager = new RuntimeModuleManager();
        foreach (var descriptor in descriptors)
            runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.AddInstance<ILogger>(new RecordingLevelLogger());
        services.Singleton<IEventBus, EventBus>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));

        var serviceProvider = new TempestServiceProvider(services);

        return (runtimeManager, serviceProvider);
    }

    // ----------------------------------------------------------------
    // Constructor injection of IEventBus
    // ----------------------------------------------------------------

    [Fact]
    public void ClockModule_ResolvedThroughRealPipeline_ReceivesAFunctioningEventBus()
    {
        var (_, serviceProvider) = BuildPipeline(typeof(ClockModule));

        var clock = Assert.IsType<ClockModule>(serviceProvider.GetService(typeof(ClockModule)));

        // No exception resolving it proves the constructor-injected IEventBus
        // was supplied; that it is genuinely functioning is proven by the
        // publish-and-observe tests below.
        Assert.NotNull(clock);
    }

    // ----------------------------------------------------------------
    // Lifecycle event publication, ordering, and payload correctness
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullLifecycle_PublishesInitialisedStartedStopped_InOrder_WithCorrectPayloads()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(ClockModule));
        var eventBus = (IEventBus)serviceProvider.GetService(typeof(IEventBus));

        var received = new List<ClockModuleLifecycleEvent>();
        var handler = new RecordingHandler<ClockModuleLifecycleEvent>((e, ct) => { received.Add(e); return Task.CompletedTask; });
        eventBus.Subscribe(handler);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);

        var clock = Assert.IsType<ClockModule>(serviceProvider.GetService(typeof(ClockModule)));

        Assert.Equal(3, received.Count);
        Assert.Equal(ClockModuleLifecycleTransition.Initialised, received[0].Transition);
        Assert.Equal(ClockModuleLifecycleTransition.Started, received[1].Transition);
        Assert.Equal(ClockModuleLifecycleTransition.Stopped, received[2].Transition);

        Assert.All(received, e => Assert.Equal("tempest.samples.clock", e.ModuleId));
        Assert.All(received, e => Assert.Equal("System Clock", e.ModuleName));

        // Every event from one module instance shares the same correlation
        // identifier.
        var correlationId = received[0].CorrelationId;
        Assert.NotEqual(Guid.Empty, correlationId);
        Assert.All(received, e => Assert.Equal(correlationId, e.CorrelationId));

        // Each event's own timestamp matches the module's own recorded
        // timestamp for that transition exactly.
        Assert.Equal(clock.InitialisedAt, received[0].Timestamp);
        Assert.Equal(clock.StartedAt, received[1].Timestamp);
        Assert.Equal(clock.StoppedAt, received[2].Timestamp);

        // Timestamps are themselves non-decreasing across the sequence.
        Assert.True(received[0].Timestamp <= received[1].Timestamp);
        Assert.True(received[1].Timestamp <= received[2].Timestamp);
    }

    // ----------------------------------------------------------------
    // Subscriber notification / multiple subscribers
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullLifecycle_MultipleSubscribers_AllReceiveEveryEvent()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(ClockModule));
        var eventBus = (IEventBus)serviceProvider.GetService(typeof(IEventBus));

        var firstReceived = new List<ClockModuleLifecycleEvent>();
        var secondReceived = new List<ClockModuleLifecycleEvent>();
        eventBus.Subscribe(new RecordingHandler<ClockModuleLifecycleEvent>((e, ct) => { firstReceived.Add(e); return Task.CompletedTask; }));
        eventBus.Subscribe(new RecordingHandler<ClockModuleLifecycleEvent>((e, ct) => { secondReceived.Add(e); return Task.CompletedTask; }));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);

        Assert.Equal(3, firstReceived.Count);
        Assert.Equal(3, secondReceived.Count);
        Assert.Equal(
            firstReceived.Select(e => e.Transition),
            secondReceived.Select(e => e.Transition));
    }

    // ----------------------------------------------------------------
    // Companion subscriber: real, end-to-end publish -> subscribe, with no
    // direct reference between the two modules anywhere in the proof.
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullLifecycle_CompanionObserverModule_ReceivesStartedAndStopped_ButNotInitialised()
    {
        var (runtimeManager, serviceProvider) = BuildPipeline(typeof(ClockModule), typeof(ClockLifecycleObserverModule));

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
        await lifecycleManager.StartAllAsync(CancellationToken.None);
        await lifecycleManager.StopAllAsync(CancellationToken.None);

        var observer = Assert.IsType<ClockLifecycleObserverModule>(
            serviceProvider.GetService(typeof(ClockLifecycleObserverModule)));

        // ClockModule ("tempest.samples.clock") sorts before its companion
        // ("tempest.samples.clock.observer") in ModuleLifecycleManager's
        // ascending-Id Initialise batch, so ClockModule publishes its own
        // Initialised event and completes before the observer's own
        // InitialiseAsync (where it subscribes) even runs - a real,
        // load-bearing consequence of the module pipeline's per-phase batch
        // shape, not a defect in either module. Start and Stop are each
        // published only after every module has completed the phase before
        // it, so both are reliably observed regardless of Id ordering.
        Assert.DoesNotContain(observer.ObservedEvents, e => e.Transition == ClockModuleLifecycleTransition.Initialised);
        Assert.Contains(observer.ObservedEvents, e => e.Transition == ClockModuleLifecycleTransition.Started);
        Assert.Contains(observer.ObservedEvents, e => e.Transition == ClockModuleLifecycleTransition.Stopped);
        Assert.All(observer.ObservedEvents, e => Assert.Equal("tempest.samples.clock", e.ModuleId));
    }

    // ----------------------------------------------------------------
    // ClockModule / companion discovery via the real, unmodified pipeline
    // ----------------------------------------------------------------

    [Fact]
    public void ClockModuleAndCompanion_DiscoveredAndRegistered_WithCorrectMetadata()
    {
        var (runtimeManager, _) = BuildPipeline(typeof(ClockModule), typeof(ClockLifecycleObserverModule));

        Assert.True(runtimeManager.IsRegistered("tempest.samples.clock"));
        Assert.True(runtimeManager.IsRegistered("tempest.samples.clock.observer"));
    }

    // ----------------------------------------------------------------
    // End-to-end execution through the real, unmodified Host
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithClockModuleAndObserver_ObserverLogsStartedAndStopped_ThroughTheRealHost()
    {
        var host = new TempestHostBuilder([typeof(ClockModule), typeof(ClockLifecycleObserverModule)]).Build();
        var originalOut = Console.Out;
        var writer = new StringWriter();

        try
        {
            Console.SetOut(writer);

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HostState.Stopped, host.State);

        var output = writer.ToString();
        Assert.Contains("Observed 'Started' from module 'tempest.samples.clock'", output);
        Assert.Contains("Observed 'Stopped' from module 'tempest.samples.clock'", output);

        // The real, load-bearing ordering consequence documented on
        // ClockLifecycleObserverModule, now proven through the real Host
        // rather than only the manually-composed pipeline above.
        Assert.DoesNotContain("Observed 'Initialised' from module 'tempest.samples.clock'", output);
    }

    // ----------------------------------------------------------------
    // Repeated execution / deterministic behaviour
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullLifecycle_RepeatedAcrossFreshInstances_IsDeterministic()
    {
        for (var i = 0; i < 3; i++)
        {
            var (runtimeManager, serviceProvider) = BuildPipeline(typeof(ClockModule), typeof(ClockLifecycleObserverModule));

            var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);
            await lifecycleManager.InitialiseAllAsync(CancellationToken.None);
            await lifecycleManager.StartAllAsync(CancellationToken.None);
            await lifecycleManager.StopAllAsync(CancellationToken.None);

            var observer = Assert.IsType<ClockLifecycleObserverModule>(
                serviceProvider.GetService(typeof(ClockLifecycleObserverModule)));

            Assert.Equal(
                [ClockModuleLifecycleTransition.Started, ClockModuleLifecycleTransition.Stopped],
                observer.ObservedEvents.Select(e => e.Transition));
        }
    }

    [Fact]
    public async Task RunAsync_WithClockModuleAndObserver_RepeatedAcrossFreshHosts_ReachesStoppedEveryTime()
    {
        // ITempestHost is single-use (ADR-0015); repeated execution is
        // proven with a fresh TempestHostBuilder/TempestHost pair each time,
        // not by re-running the same host instance.
        for (var i = 0; i < 2; i++)
        {
            var host = new TempestHostBuilder([typeof(ClockModule), typeof(ClockLifecycleObserverModule)]).Build();

            var runTask = host.RunAsync();

            while (host.State is HostState.Created or HostState.Starting)
                await Task.Delay(5);

            Assert.Equal(HostState.Running, host.State);

            await host.StopAsync();
            await runTask;

            Assert.Equal(HostState.Stopped, host.State);
        }
    }
}
