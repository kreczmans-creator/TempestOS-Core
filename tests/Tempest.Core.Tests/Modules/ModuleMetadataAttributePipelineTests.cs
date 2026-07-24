using Tempest.Core.DependencyInjection;
using Tempest.Core.Modules;
using Tempest.Core.Runtime;
using Tempest.Samples;

namespace Tempest.Core.Tests.Modules;

// Proves ADR-0027 end-to-end: a discovered module with a genuinely
// constructor-injected dependency travels through the real, unmodified
// Platform Services pipeline, and existing modules (including the WP 4.3
// sample module) are completely unaffected.
[Collection("Console output capture")]
public class ModuleMetadataAttributePipelineTests
{
    // ----------------------------------------------------------------
    // Constructor injection is now possible - the real, composed pipeline.
    // ----------------------------------------------------------------

    [Fact]
    public async Task FullPipeline_AttributeModuleWithRegisteredDependency_ResolvesAndInitialisesCorrectly()
    {
        var descriptor = Assert.Single(
            new ReflectionFrameworkDiscoveryService().DiscoverModules([typeof(ConstructorInjectedModule)]));

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        var services = new ServiceCollection();
        services.Singleton<ITestGreeter, TestGreeter>();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Initialised, lifecycleManager.GetState("test.attribute.injected"));

        var module = Assert.IsType<ConstructorInjectedModule>(
            serviceProvider.GetService(typeof(ConstructorInjectedModule)));

        Assert.Equal("hello", module.GreetingObservedDuringInitialise);
    }

    [Fact]
    public async Task FullPipeline_AttributeModuleWithUnregisteredDependency_IsIsolated_NotHostFatal()
    {
        var descriptor = Assert.Single(
            new ReflectionFrameworkDiscoveryService().DiscoverModules([typeof(ConstructorInjectedModule)]));

        var runtimeManager = new RuntimeModuleManager();
        runtimeManager.Register(descriptor);

        // Deliberately do NOT register ITestGreeter - the dependency
        // ConstructorInjectedModule's constructor requires.
        var services = new ServiceCollection();
        services.AddDiscoveredModules(runtimeManager.GetAll().Select(module => module.Descriptor));
        var serviceProvider = new TempestServiceProvider(services);

        var lifecycleManager = new ModuleLifecycleManager(runtimeManager, serviceProvider);

        // InitialiseAllAsync must not throw for the caller of the batch -
        // an unresolvable dependency is isolated to the one module.
        await lifecycleManager.InitialiseAllAsync(CancellationToken.None);

        Assert.Equal(ModuleState.Failed, lifecycleManager.GetState("test.attribute.injected"));
    }

    // ----------------------------------------------------------------
    // No Host behaviour changes - proven via the real, unmodified Host,
    // constructor-injecting a genuine platform service (ILogger) with no
    // test-only registration required.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_WithConstructorInjectedAttributeModule_ReachesRunning_LoggerWasInjected()
    {
        var host = new TempestHostBuilder([typeof(HostInjectedModule)]).Build();
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
        Assert.Contains("HostInjectedModule initialised with a constructor-injected ILogger.", writer.ToString());
    }

    // ----------------------------------------------------------------
    // Regression: the WP 4.3 sample module continues working unchanged,
    // alongside a new, attribute-based, constructor-injected module.
    // ----------------------------------------------------------------

    [Fact]
    public async Task RunAsync_ClockModuleAlongsideConstructorInjectedModule_BothReachRunning_NoSpecialCasing()
    {
        var host = new TempestHostBuilder([typeof(ClockModule), typeof(HostInjectedModule)]).Build();

        var runTask = host.RunAsync();

        while (host.State is HostState.Created or HostState.Starting)
            await Task.Delay(5);

        Assert.Equal(HostState.Running, host.State);

        await host.StopAsync();
        await runTask;

        Assert.Equal(HostState.Stopped, host.State);
    }
}
