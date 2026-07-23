using Tempest.Core.BackgroundServices;
using Tempest.Core.Commands;
using Tempest.Core.Events;
using Tempest.Core.Modules;

namespace Tempest.Core.Tests.Modules;

/// <summary>
/// Proves the WP 4.0 platform contracts (<see cref="IHostedService"/>,
/// <see cref="ICriticalBackgroundService"/>, <see cref="ICommand"/>,
/// <see cref="IEvent"/>, <see cref="IEventHandler{TEvent}"/>) introduce no
/// regression in Discovery's existing, already-tested behaviour — they are
/// declarations only, with no runtime wiring yet, and must not change what
/// Discovery considers a module.
/// </summary>
public class PlatformContractsCompatibilityTests
{
    [Fact]
    public void Discovery_IgnoresAHostedServiceThatIsNotAlsoAModule()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[] { typeof(StandaloneHostedService) });

        Assert.Empty(result);
    }

    [Fact]
    public void Discovery_StillDiscoversAModuleThatAlsoImplementsIHostedService()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[] { typeof(ModuleThatIsAlsoAHostedService) });

        var descriptor = Assert.Single(result);
        Assert.Equal("tempest.contracts.module-and-hosted-service", descriptor.Id);
    }

    [Fact]
    public void Discovery_IgnoresBareCommandAndEventTypes()
    {
        var service = new ReflectionFrameworkDiscoveryService();

        var result = service.DiscoverModules(new[] { typeof(SampleCommand), typeof(SampleEvent) });

        Assert.Empty(result);
    }

    private sealed class StandaloneHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ModuleThatIsAlsoAHostedService : IModule, IHostedService
    {
        public string Id => "tempest.contracts.module-and-hosted-service";

        public string Name => "Module That Is Also A Hosted Service";

        public string Version => "1.0.0";

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SampleCommand : ICommand
    {
    }

    private sealed class SampleEvent : IEvent
    {
    }
}
